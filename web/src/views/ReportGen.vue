<script setup>
import { ref, computed, onMounted, h } from 'vue'
import { useMessage, NSelect } from 'naive-ui'
import api, { errMsg } from '../api'
import Pager from '../components/Pager.vue'
import { usePager } from '../composables/usePager'

// 报告生成（自 EnterpriseMS「报告生成」页原样移植，工作流与口径保持一致）：
// 导入 Excel（多 Sheet）→ 点左侧子节点看映射预览、给每列选字段 → 确认映射解析全部 Sheet →
// 勾选项目 + 筛选 → 生成报告预览 → 套内置 Word 模板导出。Excel 结构约定（与 EM 相同）：
// 第 2 行 A 列是项目名（支持「父\子」两级）、第 3 行是表头、数据从第 6 行开始。
const message = useMessage()

const FIELDS = ['项目编码', '项目名称', '计量单位', '送审金额', '审定金额', '审核差额', '审核原因',
  '送审工程量', '审定工程量', '工程量差额', '送审单价', '审定单价', '单价差额']
// EM 的默认列映射（0 基列序号 → 字段）：第 1 列默认是序号，不映射
const DEFAULT_MAP = { 1: '项目编码', 2: '项目名称', 4: '计量单位', 5: '送审工程量', 6: '送审单价', 8: '送审金额', 9: '审定工程量', 10: '审定单价', 11: '审定金额', 12: '工程量差额', 13: '审核差额', 14: '审核原因' }
const REASON_OPTIONS = ['工程量调整', '综合单价调整', '工程量及综合单价调整', '送审清单漏项', '图纸无该项内容', '设计变更', '材料调整', '其他']

const workbookSheets = ref([])   // [{ name, data: any[][] }]
const sheetsStore = ref([])      // [{ sheetIdx, sheetName, parsedRows }]
const parents = ref([])          // [{ name, childrenIds }]
const childNodes = ref([])       // [{ id, label, sheetIdx, parentName }]
const checkedKeys = ref([])      // n-tree 勾选（含父键，取子集时过滤）
const currentChildId = ref(null)
const isMapped = ref(false)
const mappingOpen = ref(true)
const colFields = ref([])        // 当前预览 Sheet 每列选中的字段（'' = 未识别）
const previewHeaders = ref([])
const previewSamples = ref([])

const filterForm = ref({ type: '', mode: 'all', min: '', max: '' })
const currentFilter = ref({ type: '', mode: 'all', min: 0, max: 0 })

const templates = ref([])
const tplId = ref(null)
const exporting = ref(false)
const showPreview = ref(false)
const previewSections = ref([])  // [{ label, lines: [{ text, diff }] }]

const pager = usePager(15)

const treeData = computed(() => parents.value.map(p => ({
  key: 'parent_' + p.name,
  label: p.name,
  children: p.childrenIds.map(cid => {
    const ch = childNodes.value.find(c => c.id === cid)
    return { key: cid, label: ch ? ch.label : cid }
  })
})))
const currentChild = computed(() => childNodes.value.find(c => c.id === currentChildId.value) || null)
const currentRows = computed(() => {
  const ch = currentChild.value
  if (!isMapped.value || !ch || !sheetsStore.value[ch.sheetIdx]) return []
  return filterRows(sheetsStore.value[ch.sheetIdx].parsedRows)
})
const pagedRows = computed(() => pager.slice(currentRows.value))
const checkedChildren = computed(() => childNodes.value.filter(c => checkedKeys.value.includes(c.id)))

function parseMoney(v) {
  const n = parseFloat(String(v ?? '').replace(/,/g, '').replace(/￥/g, ''))
  return isNaN(n) ? 0 : n
}
function f2(v) { return Number(v || 0).toFixed(2) }
function parseParentChild(a2) {
  if (!a2) return null
  const s = String(a2).trim()
  if (!s) return null
  if (s.indexOf('\\') > -1) {
    const parts = s.split('\\')
    return { parent: parts[0].trim(), child: parts.slice(1).join('\\').trim() || parts[parts.length - 1].trim() }
  }
  return { parent: null, child: s }
}
function filterRows(rows) {
  const { type, mode, min, max } = currentFilter.value
  return (rows || []).filter(d => {
    if (type === 'inc' && d.diff <= 0) return false
    if (type === 'dec' && d.diff >= 0) return false
    if (mode === 'gt') return d.diff > min
    if (mode === 'lt') return d.diff < min
    if (mode === 'between') return d.diff >= min && d.diff <= max
    if (mode === 'abs') return Math.abs(d.diff) > min
    return true
  })
}

// ── 导入 Excel ──────────────────────────────────────────────
const fileInput = ref(null)
function pickFile() { fileInput.value?.click() }
async function onFileChange(e) {
  const file = e.target.files?.[0]
  e.target.value = ''
  if (!file) return
  try {
    const XLSX = await import('xlsx')
    const buffer = await file.arrayBuffer()
    const wb = XLSX.read(buffer, { type: 'array' })
    const sheets = []
    for (const name of wb.SheetNames) {
      const data = XLSX.utils.sheet_to_json(wb.Sheets[name], { header: 1, defval: '' })
      if (data.length >= 3) sheets.push({ name, data })
    }
    if (!sheets.length) { message.error('无有效工作表（每个 Sheet 至少需要 3 行数据）'); return }
    workbookSheets.value = sheets
    sheetsStore.value = []
    isMapped.value = false
    checkedKeys.value = []
    currentChildId.value = null
    buildTree()
    if (childNodes.value.length) onChildClick(childNodes.value[0].id)
    message.success(`已导入 ${sheets.length} 个工作表`)
  } catch (err) {
    message.error('导入失败：' + (err?.message || err))
  }
}
function buildTree() {
  const parentMap = new Map()
  const children = []
  workbookSheets.value.forEach((sheet, sIdx) => {
    const a2 = (sheet.data[1] && sheet.data[1][0]) || ''
    const parsed = parseParentChild(a2)
    if (!parsed) return
    const cid = `child_${sIdx}`
    children.push({ id: cid, label: parsed.child || '未命名', sheetIdx: sIdx, parentName: parsed.parent })
    const pKey = parsed.parent || '未分组'
    if (!parentMap.has(pKey)) parentMap.set(pKey, { name: pKey, childrenIds: [] })
    parentMap.get(pKey).childrenIds.push(cid)
  })
  childNodes.value = children
  parents.value = [...parentMap.values()]
}

// ── 子节点点击：切换映射预览 + 明细 ────────────────────────
function onChildClick(cid) {
  const child = childNodes.value.find(c => c.id === cid)
  if (!child) return
  currentChildId.value = cid
  const sheet = workbookSheets.value[child.sheetIdx]
  if (sheet) {
    previewHeaders.value = sheet.data[2] || []
    previewSamples.value = sheet.data.slice(3, 13)
    colFields.value = previewHeaders.value.map((_, i) => DEFAULT_MAP[i] || '')
    mappingOpen.value = true
  }
  pager.reset()
}
function onTreeSelect(keys) {
  const key = keys?.[0]
  if (key && childNodes.value.some(c => c.id === key)) onChildClick(key)
}
function onColFieldChange(idx, val) {
  colFields.value[idx] = val || ''
  if (!val) return
  // 同一字段只能映射到一列：更左侧的列优先，后出现的重复选择清掉（与 EM 相同）
  const seen = new Map()
  colFields.value = colFields.value.map((f, i) => {
    if (!f) return f
    if (seen.has(f)) return ''
    seen.set(f, i)
    return f
  })
}
function isMappedCol(i) { return !!colFields.value[i] }

// ── 确认映射：解析全部 Sheet ───────────────────────────────
function confirmMapping() {
  if (!previewHeaders.value.length) { message.warning('请先导入 Excel 并点击左侧子节点'); return }
  const mapping = {}
  FIELDS.forEach(f => { mapping[f] = -1 })
  colFields.value.forEach((f, i) => { if (f && mapping[f] !== undefined) mapping[f] = i })
  if (mapping['项目名称'] === -1 || mapping['送审金额'] === -1) {
    message.error('至少需要映射「项目名称」和「送审金额」')
    return
  }
  const store = []
  workbookSheets.value.forEach((sheet, sIdx) => {
    const parsed = []
    for (const row of sheet.data.slice(5)) {
      if (!row.length || (row.length === 1 && !row[0])) continue
      const code = row[mapping['项目编码']]
      if (code === undefined || code === null || code === '') continue
      const name = mapping['项目名称'] !== -1 && row[mapping['项目名称']] ? String(row[mapping['项目名称']]) : ''
      const unit = mapping['计量单位'] !== -1 && row[mapping['计量单位']] ? String(row[mapping['计量单位']]) : ''
      const submitQty = mapping['送审工程量'] !== -1 ? parseMoney(row[mapping['送审工程量']]) : 0
      const approveQty = mapping['审定工程量'] !== -1 ? parseMoney(row[mapping['审定工程量']]) : 0
      const volDiff = mapping['工程量差额'] !== -1 ? parseMoney(row[mapping['工程量差额']]) : (approveQty - submitQty)
      const submitPrice = mapping['送审单价'] !== -1 ? parseMoney(row[mapping['送审单价']]) : 0
      const approvePrice = mapping['审定单价'] !== -1 ? parseMoney(row[mapping['审定单价']]) : 0
      const priceDiff = mapping['单价差额'] !== -1 ? parseMoney(row[mapping['单价差额']]) : (approvePrice - submitPrice)
      const submit = parseMoney(row[mapping['送审金额']])
      const approve = mapping['审定金额'] !== -1 ? parseMoney(row[mapping['审定金额']]) : 0
      const diff = mapping['审核差额'] !== -1 ? parseMoney(row[mapping['审核差额']]) : (approve - submit)
      let reason = mapping['审核原因'] !== -1 && row[mapping['审核原因']] ? String(row[mapping['审核原因']]) : ''
      let auto = false
      if (!reason.trim()) {
        auto = true
        if (submitQty === 0) reason = '送审清单漏项'
        else if (approveQty === 0) reason = '图纸无该项内容'
        else if (priceDiff !== 0 && volDiff !== 0) reason = '工程量及综合单价调整'
        else if (priceDiff !== 0) reason = '综合单价调整'
        else if (volDiff !== 0) reason = '工程量调整'
        else reason = '无调整'
      }
      parsed.push({
        code: String(code).trim(), name: name.trim(), unit,
        submit, approve, diff, submitQty, approveQty, volDiff,
        submitPrice, approvePrice, priceDiff, reason, auto
      })
    }
    store[sIdx] = { sheetIdx: sIdx, sheetName: sheet.name, parsedRows: parsed }
  })
  sheetsStore.value = store
  isMapped.value = true
  mappingOpen.value = false
  pager.reset()
  const totalRows = store.reduce((s, sh) => s + (sh?.parsedRows.length || 0), 0)
  message.success(`映射完成！共加载 ${store.length} 个 Sheet，共 ${totalRows} 条明细`)
}

// ── 筛选 ────────────────────────────────────────────────────
function applyFilters() {
  if (!isMapped.value || !currentChildId.value) { message.warning('请先确认映射并选择左侧子节点'); return }
  currentFilter.value = {
    type: filterForm.value.type || '',
    mode: filterForm.value.mode || 'all',
    min: parseFloat(filterForm.value.min) || 0,
    max: parseFloat(filterForm.value.max) || 0
  }
  pager.reset()
}
function resetFilters() {
  filterForm.value = { type: '', mode: 'all', min: '', max: '' }
  currentFilter.value = { type: '', mode: 'all', min: 0, max: 0 }
  pager.reset()
}

// ── 报告预览与导出 ─────────────────────────────────────────
function buildSections() {
  const sections = []
  for (const child of checkedChildren.value) {
    const sheet = sheetsStore.value[child.sheetIdx]
    if (!sheet || !sheet.parsedRows.length) continue
    const rows = filterRows(sheet.parsedRows)
    if (!rows.length) continue
    sections.push({
      label: child.label,
      lines: rows.map((item, idx) => {
        const typeText = item.diff > 0 ? '审增' : (item.diff < 0 ? '审减' : '无调整')
        return {
          diff: item.diff,
          text: `${idx + 1}. ${item.name}：送审工程量 ${f2(item.submitQty)}${item.unit}，审定工程量 ${f2(item.approveQty)}${item.unit}，送审单价 ${f2(item.submitPrice)} 元/${item.unit}，审定单价 ${f2(item.approvePrice)} 元/${item.unit}，送审金额 ${f2(item.submit)} 元，审定金额 ${f2(item.approve)} 元，${typeText}金额 ${f2(Math.abs(item.diff))} 元，${typeText}原因：${item.reason}`
        }
      })
    })
  }
  return sections
}
function previewReport() {
  if (!isMapped.value) { message.warning('请先确认映射'); return }
  if (!checkedChildren.value.length) { message.warning('请勾选至少一个子节点'); return }
  const sections = buildSections()
  if (!sections.length) { message.warning('所选节点在当前筛选条件下无数据'); return }
  previewSections.value = sections
  showPreview.value = true
}
async function exportWord() {
  if (!checkedChildren.value.length) { message.warning('请勾选至少一个子节点'); return }
  if (!tplId.value) { message.warning('请先在上方选择模板'); return }
  const allRows = []
  let submitTotal = 0, approveTotal = 0, diffTotal = 0, projCount = 0
  for (const child of checkedChildren.value) {
    const sheet = sheetsStore.value[child.sheetIdx]
    if (!sheet) continue
    const rows = filterRows(sheet.parsedRows)
    if (!rows.length) continue
    projCount++
    rows.forEach(item => {
      allRows.push({
        name: item.name, unit: item.unit,
        submitQty: f2(item.submitQty), submitPrice: f2(item.submitPrice), submit: f2(item.submit),
        approveQty: f2(item.approveQty), approvePrice: f2(item.approvePrice), approve: f2(item.approve),
        diff: f2(item.diff), reason: item.reason
      })
      submitTotal += item.submit; approveTotal += item.approve; diffTotal += item.diff
    })
  }
  if (!allRows.length) { message.warning('所选节点在当前筛选条件下无数据'); return }
  allRows.forEach((r, i) => { r['序号'] = i + 1 })
  const excelColumns = [
    { columnName: '序号', fieldName: '序号' },
    { columnName: 'name', fieldName: '项目名称' },
    { columnName: 'unit', fieldName: '计量单位' },
    { columnName: 'submitQty', fieldName: '送审工程量' },
    { columnName: 'submitPrice', fieldName: '送审单价' },
    { columnName: 'submit', fieldName: '送审金额' },
    { columnName: 'approveQty', fieldName: '审定工程量' },
    { columnName: 'approvePrice', fieldName: '审定单价' },
    { columnName: 'approve', fieldName: '审定金额' },
    { columnName: 'diff', fieldName: '差额' },
    { columnName: 'reason', fieldName: '审核原因' }
  ]
  const excelRows = allRows.map(r => ({
    '序号': String(r['序号']), name: r.name, unit: r.unit,
    submitQty: r.submitQty, submitPrice: r.submitPrice, submit: r.submit,
    approveQty: r.approveQty, approvePrice: r.approvePrice, approve: r.approve,
    diff: r.diff, reason: r.reason
  }))
  const today = new Date().toISOString().slice(0, 10)
  const payload = {
    templateId: tplId.value,
    supplementaryFields: {
      '工程名称': checkedChildren.value.length === 1 ? checkedChildren.value[0].label : `共 ${checkedChildren.value.length} 个项目`,
      '报告日期': today,
      '送审合计': f2(submitTotal),
      '审定合计': f2(approveTotal),
      '审减合计': f2(diffTotal),
      '项目数': String(projCount)
    },
    excelColumns,
    excelRows
  }
  exporting.value = true
  try {
    const { data } = await api.post('/reportgen/generate', payload, { responseType: 'blob' })
    const a = document.createElement('a')
    a.href = URL.createObjectURL(data)
    a.download = `造价审核报告_${today}.docx`
    a.click()
    URL.revokeObjectURL(a.href)
    message.success('已生成报告')
  } catch (e) {
    message.error(errMsg(e, '生成失败'))
  } finally { exporting.value = false }
}

function reasonOptionsFor(row) {
  const opts = REASON_OPTIONS.map(o => ({ label: o, value: o }))
  if (row.reason && !REASON_OPTIONS.includes(row.reason)) opts.unshift({ label: row.reason, value: row.reason })
  return opts
}
function onReasonChange(row, v) { row.reason = v || ''; row.auto = false }

const detailColumns = computed(() => [
  { title: '项目名称', key: 'name', ellipsis: { tooltip: true } },
  { title: '送审工程量', key: 'submitQty', width: 96, align: 'right', render: r => f2(r.submitQty) },
  { title: '送审单价(元)', key: 'submitPrice', width: 104, align: 'right', render: r => f2(r.submitPrice) },
  { title: '审定工程量', key: 'approveQty', width: 96, align: 'right', render: r => f2(r.approveQty) },
  { title: '审定单价(元)', key: 'approvePrice', width: 104, align: 'right', render: r => f2(r.approvePrice) },
  { title: '送审金额(元)', key: 'submit', width: 108, align: 'right', render: r => f2(r.submit) },
  { title: '审定金额(元)', key: 'approve', width: 108, align: 'right', render: r => f2(r.approve) },
  {
    title: '差额(元)', key: 'diff', width: 104, align: 'right',
    render: r => h('span', { style: { color: r.diff > 0 ? '#d03050' : (r.diff < 0 ? '#18a058' : 'var(--faint)'), fontWeight: 600 } }, f2(r.diff))
  },
  {
    title: '审核原因', key: 'reason', width: 190,
    render: r => h(NSelect, {
      value: r.reason, size: 'small', filterable: true, tag: true,
      options: reasonOptionsFor(r),
      style: r.auto ? 'opacity:.65' : '',
      'onUpdate:value': (v) => onReasonChange(r, v)
    })
  }
])

const typeOptions = [
  { label: '全部', value: '' },
  { label: '审增', value: 'inc' },
  { label: '审减', value: 'dec' }
]
const modeOptions = [
  { label: '无限制', value: 'all' },
  { label: '差额 >', value: 'gt' },
  { label: '差额 <', value: 'lt' },
  { label: '介于', value: 'between' },
  { label: '|差额| >', value: 'abs' }
]
const fieldOptions = computed(() => [
  { label: '未识别', value: '' },
  ...FIELDS.map(f => ({ label: f, value: f }))
])
const tplOptions = computed(() => templates.value.map(t => ({ label: t.name, value: t.id })))

onMounted(async () => {
  try {
    const { data } = await api.get('/reportgen/templates')
    templates.value = Array.isArray(data) ? data : []
    if (templates.value.length) tplId.value = templates.value[0].id
  } catch { templates.value = [] }
})
</script>

<template>
  <div>
    <!-- 工具栏 -->
    <div class="card" style="margin-bottom:14px">
      <div style="display:flex;align-items:center;gap:10px;flex-wrap:wrap">
        <input ref="fileInput" type="file" accept=".xlsx,.xls" style="display:none" @change="onFileChange" />
        <n-button type="primary" @click="pickFile">
          <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="margin-right:4px;vertical-align:-2px"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="17 8 12 3 7 8"/><line x1="12" y1="3" x2="12" y2="15"/></svg>
          导入 Excel
        </n-button>
        <n-button @click="previewReport">
          <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="margin-right:4px;vertical-align:-2px"><path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z"/><path d="M14 3v5h5"/></svg>
          生成报告
        </n-button>
        <span class="small mut">模板</span>
        <n-select v-model:value="tplId" :options="tplOptions" style="width:200px" :placeholder="templates.length ? '选择模板' : '（无可用模板）'" />
        <span class="small faint">导入后请在下方字段映射卡片中完成映射，再点击「确认映射」</span>
      </div>
    </div>

    <!-- 操作说明 -->
    <n-collapse style="margin-bottom:14px">
      <n-collapse-item title="操作说明" name="help">
        <div class="card" style="margin:0">
          <ol class="small mut" style="margin:0;padding-left:20px;line-height:2">
            <li>点击「导入 Excel」上传工作簿（支持多 Sheet）。</li>
            <li>点击左侧树形结构中的子节点，右侧将自动显示该 Sheet 的字段映射预览表。</li>
            <li>在映射预览表中为每列选择正确的字段类型（项目编码、项目名称、送审金额等），同一字段只能映射到一列，已映射列高亮显示。</li>
            <li>点击映射卡片底部的「确认映射」，系统将解析所有 Sheet 数据并自动折叠映射卡片。</li>
            <li>勾选左侧项目（父节点可批量全选/全不选子节点），用于后续生成报告。</li>
            <li>使用筛选栏（审增/减、差额条件）过滤审核明细，表格支持分页。</li>
            <li>点击「生成报告」预览选中项目的审核结果，可导出 Word 文档。</li>
            <li>审核原因可下拉选择或手动输入，修改后实时生效。</li>
          </ol>
          <p class="small faint" style="margin:8px 0 0">提示：每个 Sheet 第 2 行 A 列支持「父\子」格式，将自动生成两级树结构；第 3 行为表头，数据从第 6 行开始读取。</p>
        </div>
      </n-collapse-item>
    </n-collapse>

    <div class="rg-layout">
      <!-- 左：项目结构树 -->
      <div class="card" style="align-self:start">
        <div style="font-weight:700;margin-bottom:10px">项目结构</div>
        <n-tree v-if="treeData.length" block-line checkable cascade :data="treeData"
          :checked-keys="checkedKeys" :selected-keys="currentChildId ? [currentChildId] : []"
          default-expand-all
          @update:checked-keys="k => checkedKeys = k"
          @update:selected-keys="onTreeSelect" />
        <div v-else class="faint small" style="text-align:center;padding:22px 0">请先导入 Excel</div>
        <p class="small faint" style="margin:10px 0 0">勾选后生成报告包含数据</p>
      </div>

      <!-- 右：筛选 + 映射 + 明细 -->
      <div style="display:flex;flex-direction:column;gap:14px;min-width:0">
        <div class="card">
          <div style="display:flex;align-items:center;gap:10px;flex-wrap:wrap">
            <span class="small mut">审增/减</span>
            <n-select v-model:value="filterForm.type" :options="typeOptions" style="width:96px" />
            <span class="small mut">条件</span>
            <n-select v-model:value="filterForm.mode" :options="modeOptions" style="width:110px" />
            <n-input v-model:value="filterForm.min" placeholder="数值1" type="number" style="width:100px" />
            <n-input v-model:value="filterForm.max" placeholder="数值2" type="number" style="width:100px" />
            <n-button type="primary" @click="applyFilters">筛选</n-button>
            <n-button @click="resetFilters">重置</n-button>
          </div>
        </div>

        <div class="card">
          <div style="display:flex;align-items:center;cursor:pointer" @click="mappingOpen = !mappingOpen">
            <span style="font-weight:700">字段映射</span>
            <span class="small faint" style="margin-left:8px">{{ mappingOpen ? '（点击收起）' : '（点击展开）' }}</span>
          </div>
          <div v-show="mappingOpen" style="margin-top:12px">
            <div v-if="!previewHeaders.length" class="small mut" style="background:var(--green-soft, #e2efe7);border-radius:8px;padding:10px 12px">
              请先导入 Excel，然后点击左侧子节点切换预览
            </div>
            <div v-else class="mapping-scroll">
              <table class="map-table">
                <thead>
                  <tr>
                    <th v-for="(hd, i) in previewHeaders" :key="i" :class="{ 'hl-col': isMappedCol(i) }">
                      <n-select :value="colFields[i]" :options="fieldOptions" size="small" style="width:118px"
                        @update:value="v => onColFieldChange(i, v)" />
                    </th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="(row, ri) in previewSamples" :key="ri">
                    <td v-for="(hd, i) in previewHeaders" :key="i" :class="{ 'hl-col': isMappedCol(i) }"
                      :title="row[i] !== undefined && row[i] !== '' ? String(row[i]) : ''">
                      {{ row[i] !== undefined ? String(row[i]) : '' }}
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
            <div style="display:flex;justify-content:flex-end;margin-top:12px">
              <n-button type="primary" @click="confirmMapping">确认映射</n-button>
            </div>
          </div>
        </div>

        <div class="card">
          <div style="display:flex;align-items:center;gap:8px;margin-bottom:10px">
            <span style="font-weight:700">审核明细</span>
            <span class="small faint">（当前：{{ currentChild ? currentChild.label : '未选择' }}）</span>
            <div style="flex:1"></div>
            <n-tag size="small" :bordered="false">{{ currentRows.length }}</n-tag>
          </div>
          <n-data-table :columns="detailColumns" :data="pagedRows" :row-key="r => r.code + '|' + r.name"
            table-layout="fixed" size="small" :bordered="false" />
          <div v-if="!currentRows.length" class="faint small" style="text-align:center;padding:18px 0">暂无数据</div>
          <Pager v-model:page="pager.page.value" v-model:page-size="pager.pageSize.value" :total="currentRows.length" />
        </div>
      </div>
    </div>

    <!-- 报告预览 -->
    <n-modal v-model:show="showPreview" preset="card" title="工程审核报告预览" style="width:min(1060px,94vw);border-radius:16px">
      <div style="max-height:65vh;overflow-y:auto;padding:2px 4px">
        <div v-for="(sec, si) in previewSections" :key="si" style="margin-bottom:16px">
          <div style="font-weight:700;border-bottom:1px solid var(--line);padding-bottom:5px;margin-bottom:8px">{{ sec.label }}</div>
          <ul style="margin:0;padding-left:18px;line-height:1.9">
            <li v-for="(ln, li) in sec.lines" :key="li" class="small">
              {{ ln.text }}
            </li>
          </ul>
        </div>
      </div>
      <template #footer>
        <div style="display:flex;justify-content:flex-end;gap:10px">
          <n-button @click="showPreview = false">关闭</n-button>
          <n-button type="primary" :loading="exporting" @click="exportWord">导出 Word</n-button>
        </div>
      </template>
    </n-modal>
  </div>
</template>

<style scoped>
.rg-layout { display: grid; grid-template-columns: 264px 1fr; gap: 14px; align-items: start; }
@media (max-width: 900px) { .rg-layout { grid-template-columns: 1fr; } }
.mapping-scroll { overflow: auto; max-height: 420px; border: 1px solid var(--line); border-radius: 8px; }
.map-table { border-collapse: collapse; width: max-content; min-width: 100%; font-size: 12.5px; }
.map-table th, .map-table td { border: 1px solid var(--line); padding: 5px 7px; max-width: 180px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.map-table thead th { background: var(--card-2, #f4f6f4); position: sticky; top: 0; z-index: 1; }
.hl-col { background: #fff7e6 !important; }
body.dark .hl-col { background: rgba(217, 164, 31, .16) !important; }
</style>
