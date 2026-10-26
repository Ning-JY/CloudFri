<script setup>
import { ref, computed, onMounted, h } from 'vue'
import { NButton } from 'naive-ui'
import { useRoute, useRouter } from 'vue-router'
import { useMessage, useDialog } from 'naive-ui'
import api, { errMsg } from '../api'
import { fmt, fmtSize, avatarFor, cutRate, fileKind, todayStr } from '../utils'
import PreviewModal from '../components/PreviewModal.vue'
import GcfxPanel from '../components/GcfxPanel.vue'

const route = useRoute()
const router = useRouter()
function goBack() { router.push('/projects') }
const message = useMessage()
const dialog = useDialog()
const id = Number(route.params.id)

const d = ref(null)
const members = ref([])
const allContracts = ref([])
const previewRef = ref(null)
const BIZ_TYPES = ['结算审核', '预算编制', '清单控制价', '竣工决算', '跟踪审计', '全过程咨询']
const STATUSES = ['编制中', '审核中', '已出报告', '已归档']
const ROLES = ['项目负责人', '编制人', '审核人', '审定人', '配合人员']
const DOC_CATS = ['设计图纸', '隐蔽工程资料', '工作联系函', '会议纪要', '其他资料']
const FILE_CATS = [
  { key: 'Software', label: '软件版', icon: 'GCFX', cls: 'cg' },
  { key: 'Document', label: '文档版', icon: 'XLS', cls: 'cb' },
  { key: 'Report', label: '报告', icon: 'PDF', cls: 'co' }
]
const catIcon = (key) => FILE_CATS.find(c => c.key === key)?.icon || 'FILE'
const catCls = (key) => FILE_CATS.find(c => c.key === key)?.cls || 'cg'
const showFeeDetail = ref(false)

async function load() {
  // 带时间戳参数，避免浏览器缓存旧详情（重传 GCFX 后解析不刷新的投诉对策）
  const { data } = await api.get('/projects/' + id, { params: { t: Date.now() } })
  d.value = data
}
onMounted(async () => {
  const [m, c] = await Promise.all([api.get('/members'), api.get('/contracts')])
  members.value = m.data
  allContracts.value = c.data
  await load()
})

const p = computed(() => d.value?.project)
const isAudit = computed(() => p.value && (p.value.bizType === '结算审核' || p.value.bizType === '竣工决算'))
const isProcess = computed(() => p.value && (p.value.bizType === '跟踪审计' || p.value.bizType === '全过程咨询'))
const contractAmount = computed(() => {
  if (!d.value) return null
  const cs = d.value.contracts
  if (!cs.length) return null
  if (cs.some(c => c.amount == null)) return '按实结算'
  return cs.reduce((a, c) => a + (c.amount || 0), 0)
})
const received = computed(() => d.value?.receivedTotal || 0)
const feePct = computed(() => (p.value && p.value.fee > 0) ? Math.min(100, Math.round(received.value / p.value.fee * 100)) : 0)
const ringOffset = computed(() => (251.3 * (1 - feePct.value / 100)).toFixed(1))
const lastReceipt = computed(() => {
  const rs = d.value?.receipts || []
  return rs.length ? rs[rs.length - 1] : null
})
const stepIdx = computed(() => p.value ? STATUSES.indexOf(p.value.status) : 0)

const amounts = computed(() => {
  if (!p.value) return []
  const cr = cutRate(p.value)
  if (isAudit.value) return [
    { lb: '送审金额', vl: fmt(p.value.submitAmount) + ' 万', sb: '委托方送审' },
    { lb: '审定金额', vl: fmt(p.value.approvedAmount) + ' 万', sb: p.value.reportNo ? '报告 ' + p.value.reportNo : '审定结果' },
    { lb: '核减额 / 核减率', vl: (p.value.submitAmount != null && p.value.approvedAmount != null) ? fmt(p.value.submitAmount - p.value.approvedAmount) + ' 万' : '—', sb: cr != null ? '核减率 ' + cr.toFixed(2) + '%' : '—' },
    { lb: '咨询费（应收 / 已收）', vl: fmt(p.value.fee) + ' 万', sb: '已收 ' + fmt(received.value) + ' 万' }
  ]
  if (isProcess.value) return [
    { lb: '建安造价', vl: fmt(p.value.submitAmount) + ' 万', sb: '过程控制对象' },
    { lb: '合同金额', vl: contractAmount.value === '按实结算' ? '按实结算' : fmt(contractAmount.value) + ' 万', sb: '关联合同合计' },
    { lb: '咨询费应收', vl: fmt(p.value.fee) + ' 万', sb: '按合同约定' },
    { lb: '已收咨询费', vl: fmt(received.value) + ' 万', sb: '累计收款' }
  ]
  return [
    { lb: p.value.bizType === '清单控制价' ? '控制价金额' : '预算金额', vl: fmt(p.value.submitAmount) + ' 万', sb: '编制成果' },
    { lb: '合同金额', vl: contractAmount.value === '按实结算' ? '按实结算' : (contractAmount.value == null ? '—' : fmt(contractAmount.value) + ' 万'), sb: '关联合同合计' },
    { lb: '咨询费应收', vl: fmt(p.value.fee) + ' 万', sb: '按合同约定' },
    { lb: '已收咨询费', vl: fmt(received.value) + ' 万', sb: '累计收款' }
  ]
})

// 成果文件整体版本：三类文件同属一个版本集合；SetId 为空的旧文件按最早集合（V1）处理
// 当前版本：被设为最终版的集合优先，否则版本号最大者
const sets = computed(() => d.value?.deliverableSets || [])
const currentSet = computed(() => sets.value.length ? (sets.value.find(s => s.isFinal) || sets.value[sets.value.length - 1]) : null)
const currentVersionNo = computed(() => currentSet.value?.version ?? 1)
const effSetId = (f) => f.setId ?? (sets.value[0]?.id ?? 0)
const currentSetKey = computed(() => currentSet.value?.id ?? (sets.value[0]?.id ?? 0))
const curFiles = computed(() => (d.value?.files || []).filter(f => effSetId(f) === currentSetKey.value))
const fileGroups = computed(() => {
  if (!d.value) return []
  return FILE_CATS.map(c => {
    const fs = curFiles.value.filter(f => f.category === c.key)
    return { ...c, cur: fs[0] || null, extra: fs.slice(1) }
  })
})
const gotCount = computed(() => fileGroups.value.filter(g => g.cur).length)
const catLabel = (key) => FILE_CATS.find(c => c.key === key)?.label || key
const historyGroups = computed(() => {
  if (!d.value) return []
  const groups = []
  const replaced = fileGroups.value.flatMap(g => g.extra.map(f => ({ ...f, catLabel: g.label })))
  if (replaced.length) groups.push({ key: 'rep', label: `当前版本 V${currentVersionNo.value} · 已替换的旧文件`, files: replaced })
  const older = sets.value.filter(s => s.id !== currentSet.value?.id).sort((a, b) => b.version - a.version)
  for (const s of older) {
    const fs = (d.value.files || []).filter(f => effSetId(f) === s.id).map(f => ({ ...f, catLabel: catLabel(f.category) }))
    if (fs.length) groups.push({ key: 'set' + s.id, setId: s.id, label: `历史版本 V${s.version}`, files: fs })
  }
  return groups
})
// 把历史版本设为最终版（重新成为当前版本，承接后续上传）
function setFinal(hg) {
  dialog.warning({
    title: '设为最终版', content: `把「${hg.label}」设为最终版？它将重新成为当前版本，后续上传的成果文件会进入该版本；现在的当前版本转入历史。`,
    positiveText: '设为最终版', negativeText: '取消',
    onPositiveClick: async () => {
      try {
        await api.post(`/projects/${id}/files/sets/${hg.setId}/set-final`)
        message.success('已设为最终版')
        await load()
      } catch (e) { message.error(errMsg(e)) }
    }
  })
}
// 删除项目（连同团队、合同关联、收款、成果与资料一并删除，不可恢复）
function delProject() {
  dialog.error({
    title: '删除项目', content: `确定删除项目「${p.value.name}」（${p.value.code}）？其团队分工、合同关联、收款记录、成果文件与项目资料将一并删除，不可恢复。`,
    positiveText: '删除', negativeText: '取消',
    onPositiveClick: async () => {
      try {
        await api.delete('/projects/' + id)
        message.success('项目已删除')
        router.push('/projects')
      } catch (e) { message.error(errMsg(e)) }
    }
  })
}
const parseOf = (fileId) => (d.value?.gcfxParses || []).find(g => g.projectFileId === fileId)
// 造价解析卡片展示对象：优先当前版本软件版文件的解析，否则取最近一次解析
const gcfxTarget = computed(() => {
  if (!d.value) return null
  const sw = fileGroups.value.find(g => g.key === 'Software')?.cur
  if (sw) { const pr = parseOf(sw.id); if (pr) return { parse: pr, fileName: sw.fileName } }
  const all = [...(d.value.gcfxParses || [])].sort((a, b) => String(b.parsedAt).localeCompare(String(a.parsedAt)))
  if (all.length) {
    const f = d.value.files.find(x => x.id === all[0].projectFileId)
    return { parse: all[0], fileName: f?.fileName || '' }
  }
  return null
})
const docGroups = computed(() => {
  if (!d.value) return []
  return DOC_CATS.map(cat => ({ cat, files: d.value.docs.filter(x => x.category === cat) }))
})
const openDocCat = ref('设计图纸')
const histOpen = ref({})
function toggleHist(key) { histOpen.value = { ...histOpen.value, [key]: !histOpen.value[key] } }

async function downloadFile(kind, fid, name) {
  const url = kind === 'lib' ? `/library/${fid}/download` : `/projects/${id}/files/${kind}/${fid}/download`
  const { data } = await api.get(url, { responseType: 'blob' })
  const a = document.createElement('a')
  a.href = URL.createObjectURL(data)
  a.download = name
  a.click()
  setTimeout(() => URL.revokeObjectURL(a.href), 800)
}
function previewFile(kind, f) {
  previewRef.value.open(`/projects/${id}/files/${kind}/${f.id}/download`, f.fileName)
}

async function uploadDeliverable(cat, file) {
  const fd = new FormData()
  fd.append('category', cat)
  fd.append('file', file)
  try {
    const { data } = await api.post(`/projects/${id}/files/deliverables`, fd)
    if (data.filledField) message.success(`上传成功，已自动解析 GCFX，${data.filledField}已回填 ${fmt(data.filledAmountWan)} 万`)
    else message.success(data.parseId ? '上传成功，已自动解析 GCFX' : '上传成功，已存入当前版本')
    await load()
  } catch (e) { message.error(errMsg(e, '上传失败')) }
}
async function uploadDoc(cat, file) {
  const fd = new FormData()
  fd.append('category', cat)
  fd.append('file', file)
  try {
    await api.post(`/projects/${id}/files/docs`, fd)
    message.success('已归档到「' + cat + '」')
    await load()
  } catch (e) { message.error(errMsg(e, '上传失败')) }
}
// 新增成果版本：当前三类成果整体转历史，开启新版本
function newVersion() {
  if (!fileGroups.value.some(g => g.cur)) { message.info('当前版本还没有成果文件，先上传文件再新增版本'); return }
  dialog.warning({
    title: '新增版本', content: `把当前成果（软件版 / 文档版 / 报告）整体转入历史版本，并开始 V${currentVersionNo.value + 1}？`,
    positiveText: '新增版本', negativeText: '取消',
    onPositiveClick: async () => {
      try {
        await api.post(`/projects/${id}/files/new-version`)
        message.success(`已开启 V${currentVersionNo.value + 1}，原成果整体转入历史版本`)
        await load()
      } catch (e) { message.error(errMsg(e)) }
    }
  })
}
function scrollToParse() {
  document.getElementById('gcfxPanel')?.scrollIntoView({ behavior: 'smooth', block: 'start' })
}
// 重新解析当前展示的 GCFX（解析器升级后刷新旧结果）
async function reparse() {
  const t = gcfxTarget.value
  if (!t) return
  const f = d.value.files.find(x => x.id === t.parse.projectFileId)
  if (!f) { message.warning('源文件不存在'); return }
  try {
    const { data } = await api.post(`/projects/${id}/files/deliverables/${f.id}/reparse`)
    message.success(`重新解析完成，工程总造价 ¥${fmt(data.totalAmount)}` + (data.filledField ? `，${data.filledField}已回填 ${fmt(data.filledAmountWan)} 万` : ''))
    await load()
  } catch (e) { message.error(errMsg(e, '重新解析失败')) }
}
async function downloadContractFile(c, f) {
  const { data } = await api.get(`/contracts/${c.id}/files/${f.id}/download`, { responseType: 'blob' })
  const a = document.createElement('a')
  a.href = URL.createObjectURL(data)
  a.download = f.fileName
  a.click()
  setTimeout(() => URL.revokeObjectURL(a.href), 800)
}
function pickFile(cb) {
  const inp = document.createElement('input')
  inp.type = 'file'
  inp.onchange = () => { if (inp.files[0]) cb(inp.files[0]) }
  inp.click()
}
function confirmDelete(text, fn) {
  dialog.warning({ title: '确认删除', content: text, positiveText: '删除', negativeText: '取消', onPositiveClick: fn })
}

// 更改状态（头部按钮下拉直接切换，不必打开编辑弹窗）
const statusOptions = computed(() => STATUSES.map(s => ({ label: s, key: s, disabled: p.value?.status === s })))
async function changeStatus(key) {
  const v = p.value
  try {
    await api.put('/projects/' + id, {
      name: v.name, client: v.client, contactName: v.contactName, contactPhone: v.contactPhone,
      bizType: v.bizType, scale: v.scale, status: key, ownerMemberId: v.ownerMemberId,
      acceptedDate: v.acceptedDate, dueDate: v.dueDate, reportDate: v.reportDate, reportNo: v.reportNo,
      submitAmount: v.submitAmount, approvedAmount: v.approvedAmount, fee: v.fee || 0, description: v.description
    })
    message.success('状态已改为「' + key + '」')
    await load()
  } catch (e) { message.error(errMsg(e)) }
}

// 编辑项目
const showEdit = ref(false)
const ef = ref({})
function openEdit() {
  ef.value = { ...p.value }
  showEdit.value = true
}
async function saveEdit() {
  try {
    await api.put('/projects/' + id, {
      name: ef.value.name, client: ef.value.client, contactName: ef.value.contactName, contactPhone: ef.value.contactPhone,
      bizType: ef.value.bizType, scale: ef.value.scale, status: ef.value.status, ownerMemberId: ef.value.ownerMemberId,
      acceptedDate: ef.value.acceptedDate, dueDate: ef.value.dueDate, reportDate: ef.value.reportDate, reportNo: ef.value.reportNo,
      submitAmount: ef.value.submitAmount, approvedAmount: ef.value.approvedAmount, fee: ef.value.fee || 0, description: ef.value.description
    })
    message.success('已保存')
    showEdit.value = false
    await load()
  } catch (e) { message.error(errMsg(e)) }
}

// 团队编辑
const showTeam = ref(false)
const teamRows = ref([])
function openTeam() {
  teamRows.value = d.value.team.map(t => ({ memberId: t.memberId, role: t.role, duty: t.duty || '', share: t.share }))
  if (!teamRows.value.length) teamRows.value = [{ memberId: null, role: '编制人', duty: '', share: 40 }]
  showTeam.value = true
}
async function saveTeam() {
  const items = teamRows.value.filter(r => r.memberId)
  try {
    await api.put(`/projects/${id}/team`, items)
    message.success('团队已更新')
    showTeam.value = false
    await load()
  } catch (e) { message.error(errMsg(e)) }
}

// 合同关联
const showLink = ref(false)
const linkForm = ref({ contractId: null, isPrimary: false })
async function doLink() {
  try {
    await api.post(`/contracts/${linkForm.value.contractId}/link`, { projectId: id, isPrimary: linkForm.value.isPrimary })
    message.success('已关联')
    showLink.value = false
    await load()
  } catch (e) { message.error(errMsg(e)) }
}
async function unlink(contractId) {
  await api.delete(`/contracts/${contractId}/link/${id}`)
  message.success('已取消关联')
  await load()
}

// 新增回款（与收费台账的弹窗保持一致：日期默认今天，字段相同；可指定所属合同）
const showReceipt = ref(false)
const rf = ref({ date: todayStr(), amount: null, method: '银行转账', handler: '', note: '', contractId: null })
function openReceipt() {
  rf.value = { date: todayStr(), amount: null, method: '银行转账', handler: '', note: '', contractId: null }
  showReceipt.value = true
}
async function addReceipt() {
  if (!rf.value.date || !rf.value.amount) { message.warning('请填写日期和金额'); return }
  try {
    await api.post(`/projects/${id}/receipts`, rf.value)
    message.success('回款已登记')
    showReceipt.value = false
    await load()
  } catch (e) { message.error(errMsg(e)) }
}
async function delReceipt(rid) {
  await api.delete(`/projects/${id}/receipts/${rid}`)
  message.success('已删除')
  await load()
}

const receiptColumns = [
  { title: '收款日期', key: 'date', width: 110 },
  { title: '金额（万元）', key: 'amount', align: 'right', render: (r) => fmt(r.amount) },
  { title: '所属合同', key: 'contractNo', width: 130, render: (r) => r.contractNo ? h('span', { title: r.contractName || '' }, r.contractNo) : h('span', { class: 'faint' }, '未指定') },
  { title: '方式', key: 'method', width: 100 },
  { title: '经办人', key: 'handler', width: 90 },
  { title: '备注', key: 'note' },
  { title: '', key: 'act', width: 60, render: (r) => h(NButton, { size: 'tiny', quaternary: true, onClick: () => delReceipt(r.id) }, () => '删除') }
]
</script>

<template>
  <div v-if="p">
    <!-- 头部 -->
    <div class="card d-head">
      <div style="display:flex;gap:10px;align-items:flex-start;flex-wrap:wrap">
        <div style="flex:1;min-width:260px">
          <div style="display:flex;gap:8px;align-items:center;margin-bottom:7px;flex-wrap:wrap">
            <button class="back-ic" @click="goBack" title="返回台账"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><line x1="19" y1="12" x2="5" y2="12"/><polyline points="12 19 5 12 12 5"/></svg></button>
            <span class="num faint">{{ p.code }}</span>
            <n-tag size="small" :type="p.status === '已归档' ? 'default' : p.status === '已出报告' ? 'success' : 'warning'" :bordered="false">{{ p.status }}</n-tag>
            <n-tag size="small" :bordered="false">{{ p.bizType }}</n-tag>
            <span class="rpt-chip">报告编号：{{ p.reportNo || '—' }}</span>
          </div>
          <div class="d-title">{{ p.name }}</div>
          <div class="small mut" style="margin-top:6px">{{ p.client }}<span v-if="p.contactName"> · 联系人 {{ p.contactName }}<span v-if="p.contactPhone"> {{ p.contactPhone }}</span></span> · 负责人 {{ p.ownerName || '—' }} · 承接 {{ p.acceptedDate || '—' }}</div>
        </div>
        <n-button @click="openEdit">编辑项目</n-button>
        <n-dropdown :options="statusOptions" @select="changeStatus"><n-button type="primary">更改状态</n-button></n-dropdown>
        <n-button type="error" ghost @click="delProject">删除项目</n-button>
      </div>
      <div class="d-steps">
        <template v-for="(s, i) in STATUSES" :key="s">
          <div class="d-step" :class="{ done: i < stepIdx, cur: i === stepIdx }">
            <span class="dot">{{ i < stepIdx ? '✓' : i + 1 }}</span>{{ s }}
          </div>
          <div v-if="i < STATUSES.length - 1" class="d-step-line" :class="{ done: i < stepIdx }"></div>
        </template>
      </div>
      <div class="d-amounts">
        <div class="a" :class="'t' + (i % 4)" v-for="(a, i) in amounts" :key="a.lb">
          <span class="a-ic" :class="'t' + (i % 4)">
            <svg v-if="i === 0" viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="12" y1="18" x2="12" y2="11"/><polyline points="8.5 14.5 12 11 15.5 14.5"/></svg>
            <svg v-else-if="i === 1" viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/></svg>
            <svg v-else-if="i === 2" viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="19" y1="5" x2="5" y2="19"/><circle cx="6.5" cy="6.5" r="2.5"/><circle cx="17.5" cy="17.5" r="2.5"/></svg>
            <svg v-else viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 12V7H5a2 2 0 0 1 0-4h14v4"/><path d="M3 5v14a2 2 0 0 0 2 2h16v-5"/><path d="M18 12a2 2 0 0 0 0 4h4v-4z"/></svg>
          </span>
          <div style="flex:1;min-width:0"><div class="lb">{{ a.lb }}</div><div class="vl num">{{ a.vl }}</div><div class="sb">{{ a.sb }}</div></div>
        </div>
      </div>
    </div>

    <!-- 第一行：参与人员 / 合同与收费（按比例分；基本信息已并入头部与编辑弹窗，不再单设卡片） -->
    <div class="d-top2">
      <div class="card">
        <div class="card-h"><span class="card-ic b"><svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/></svg></span><h3>参与人员</h3><div class="sp"></div><n-button size="small" @click="openTeam">编辑分工</n-button></div>
        <div v-if="!d.team.length" class="faint small">暂未设置团队，点「编辑分工」添加成员、职责与占比。</div>
        <div>
          <div class="team-it" v-for="t in d.team" :key="t.id">
            <img :src="avatarFor(t)" :alt="t.name" />
            <div style="flex:1;min-width:0">
              <div style="display:flex;align-items:center;gap:6px;flex-wrap:wrap"><b style="font-size:13.5px">{{ t.name }}</b><span class="role-tag">{{ t.role }}</span></div>
              <div class="duty">{{ t.duty || '暂无职责说明' }}</div>
            </div>
            <div class="share">
              <b class="num">{{ fmt(t.share) }}%</b>
              <div class="pbar"><i :style="{ width: Math.min(Number(t.share) || 0, 100) + '%' }"></i></div>
              <span>工作占比</span>
            </div>
          </div>
        </div>
      </div>

      <div class="card">
        <div class="card-h"><span class="card-ic o"><svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/></svg></span><h3>合同与收费</h3><div class="sp"></div>
          <n-button size="small" :type="d.contracts.length ? 'default' : 'primary'" @click="showLink = true">关联合同</n-button>
        </div>
        <div style="display:flex;gap:14px;align-items:center;flex-wrap:wrap">
          <div class="fring sm">
            <svg width="76" height="76" viewBox="0 0 96 96">
              <circle class="bgc" cx="48" cy="48" r="40" />
              <circle class="vlc" cx="48" cy="48" r="40" stroke-dasharray="251.3" :stroke-dashoffset="ringOffset" />
            </svg>
            <div class="fring-t"><b class="num" style="font-size:16px">{{ feePct }}%</b><span>收款进度</span></div>
          </div>
          <div class="fee-stats" style="gap:18px">
            <div><span>咨询费应收</span><b class="num">{{ fmt(p.fee) }} 万</b></div>
            <div><span>已收</span><b class="num">{{ fmt(received) }} 万</b></div>
            <div><span>待收</span><b class="num neg">{{ fmt(Math.max(p.fee - received, 0)) }} 万</b></div>
          </div>
        </div>
        <div class="small mut" style="margin-top:10px">{{ d.contracts.length }} 份合同<span v-if="contractAmount != null"> · 合同金额 {{ contractAmount === '按实结算' ? '按实结算' : fmt(contractAmount) + ' 万' }}</span> · 最近收款 {{ lastReceipt ? lastReceipt.date + ' · ' + fmt(lastReceipt.amount) + ' 万元' : '暂无' }}</div>
        <div style="margin-top:6px">
          <div v-if="!d.contracts.length" class="faint small" style="padding:6px 0 12px">暂未关联合同，点右上角「关联合同」添加（合同金额在合同管理中维护）。</div>
          <div v-for="c in d.contracts" :key="c.id" style="padding:9px 0;border-top:1px solid var(--line-soft)">
            <div style="display:flex;gap:10px;align-items:center;flex-wrap:wrap">
              <div style="flex:1;min-width:0">
                <b style="font-size:13.5px">{{ c.name }}</b>
                <n-tag v-if="c.isPrimary" size="tiny" type="success" :bordered="false" style="margin-left:6px">主合同</n-tag>
                <n-tag size="tiny" :bordered="false" style="margin-left:4px">{{ c.type }}</n-tag>
                <div class="psub">{{ c.no }} · {{ c.partyA || '—' }}</div>
              </div>
              <div class="num" style="font-weight:650">{{ c.amount == null ? '按实结算' : fmt(c.amount) + ' 万' }}</div>
              <n-button size="tiny" quaternary @click="unlink(c.id)">取消关联</n-button>
            </div>
            <div v-if="c.files && c.files.length" style="margin-top:5px">
              <div v-for="f in c.files" :key="f.id" style="display:flex;gap:8px;align-items:center;font-size:12.5px;color:var(--sub);padding:3px 0;flex-wrap:wrap">
                <svg viewBox="0 0 24 24" width="13" height="13" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="flex:0 0 13px"><path d="M21.44 11.05l-9.19 9.19a6 6 0 0 1-8.49-8.49l8.57-8.57A4 4 0 1 1 18 8.84l-8.59 8.57a2 2 0 0 1-2.83-2.83l8.49-8.48"/></svg>
                <span style="min-width:0">{{ f.fileName }}</span>
                <span class="faint">{{ fmtSize(f.sizeBytes) }}</span>
                <n-button size="tiny" quaternary @click="previewRef.open(`/contracts/${c.id}/files/${f.id}/download`, f.fileName)">预览</n-button>
                <n-button size="tiny" quaternary @click="downloadContractFile(c, f)">下载</n-button>
              </div>
            </div>
          </div>
          <div style="display:flex;align-items:center;margin:14px 0 4px"><b style="font-size:13.5px">收款记录</b><div style="flex:1"></div><n-button size="small" type="primary" ghost @click="openReceipt">+ 新增回款</n-button></div>
          <n-data-table v-if="d.receipts.length" size="small" :bordered="false" :columns="receiptColumns" :data="d.receipts" :scroll-x="540" />
          <div v-if="!d.receipts.length" class="faint small" style="padding:8px 0">暂无收款记录</div>
        </div>
      </div>
    </div>

    <!-- 第二行：左列 成果文件 + 项目资料（上下放）；右列 造价解析（并排） -->
    <div class="d-row2">
      <div style="display:flex;flex-direction:column;min-width:0">
        <div class="card">
          <div class="card-h"><span class="card-ic g"><svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z"/><polyline points="3.27 6.96 12 12.01 20.73 6.96"/><line x1="12" y1="22.08" x2="12" y2="12"/></svg></span><h3>成果文件</h3><div class="sp"></div>
            <n-tag size="small" type="success" :bordered="false">{{ currentSet?.isFinal ? '最终版' : '当前版本' }} V{{ currentVersionNo }}</n-tag>
            <n-tag v-if="gotCount === 3" type="success" size="small" :bordered="false">已集齐 3/3</n-tag>
            <n-tag v-else type="warning" size="small" :bordered="false">已传 {{ gotCount }}/3</n-tag>
            <n-button size="small" type="primary" @click="newVersion">+ 新增版本</n-button>
          </div>
          <div class="small faint" style="margin-bottom:10px">软件版 / 文档版 / 报告是一个整体版本；点「新增版本」，三类一起转入历史版本。</div>
          <div class="ftiles">
            <div class="ftile" v-for="g in fileGroups" :key="g.key">
              <div class="ftile-ic" :class="g.cls">
                <svg v-if="g.key === 'Software'" viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="16 18 22 12 16 6"/><polyline points="8 6 2 12 8 18"/></svg>
                <svg v-else-if="g.key === 'Document'" viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="3" width="18" height="18" rx="2"/><line x1="3" y1="9" x2="21" y2="9"/><line x1="3" y1="15" x2="21" y2="15"/><line x1="9" y1="3" x2="9" y2="21"/><line x1="15" y1="3" x2="15" y2="21"/></svg>
                <svg v-else viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/><line x1="10" y1="9" x2="8" y2="9"/></svg>
              </div>
              <div style="font-size:13px;font-weight:650;margin-top:8px">{{ g.label }}
                <n-tag v-if="g.cur" size="tiny" type="success" :bordered="false">已上传</n-tag>
                <span v-else class="small" style="color:#9a6a12;font-weight:500">未上传</span>
              </div>
              <div class="psub" v-if="g.cur" :title="g.cur.fileName">{{ fmtSize(g.cur.sizeBytes) }} · {{ String(g.cur.uploadedAt).slice(0, 10) }}</div>
              <div class="psub" v-if="g.cur && parseOf(g.cur.id)" style="color:var(--pri);font-weight:600">解析造价 ¥{{ fmt(parseOf(g.cur.id).totalAmount) }}</div>
              <div class="psub" v-else-if="!g.cur">暂无文件</div>
              <div class="psub" v-if="g.key === 'Report'" style="color:#6d4fc2;font-weight:600">报告编号：{{ p.reportNo || '—' }}</div>
              <div style="display:flex;gap:2px;margin-top:7px;flex-wrap:wrap;justify-content:center">
                <n-button size="tiny" type="primary" ghost @click="pickFile(f => uploadDeliverable(g.key, f))">{{ g.cur ? '替换' : '上传' }}</n-button>
                <n-button v-if="g.cur && parseOf(g.cur.id)" size="tiny" quaternary @click="scrollToParse">解析</n-button>
                <n-button v-if="g.cur && fileKind(g.cur.fileName) !== 'other'" size="tiny" quaternary @click="previewFile('deliverables', g.cur)">预览</n-button>
                <n-button v-if="g.cur" size="tiny" quaternary @click="downloadFile('deliverables', g.cur.id, g.cur.fileName)">下载</n-button>
                <n-button v-if="g.cur" size="tiny" quaternary @click="confirmDelete('删除当前版本中的该文件？', async () => { await api.delete(`/projects/${id}/files/deliverables/${g.cur.id}`); load() })">删除</n-button>
              </div>
            </div>
          </div>
          <div v-if="historyGroups.length" style="margin-top:10px;border-top:1px solid var(--line-soft);padding-top:8px">
            <div v-for="hg in historyGroups" :key="hg.key" style="margin-bottom:2px">
              <div style="display:flex;align-items:center;gap:8px;cursor:pointer;padding:4px 0" @click="toggleHist(hg.key)">
                <span class="small mut">{{ hg.label }}（{{ hg.files.length }}）</span>
                <n-button v-if="hg.setId" size="tiny" type="primary" ghost @click.stop="setFinal(hg)">设为最终版</n-button>
                <span class="faint" style="margin-left:auto">{{ histOpen[hg.key] ? '▾ 收起' : '▸ 展开' }}</span>
              </div>
              <div v-if="histOpen[hg.key]">
                <div class="fhist" v-for="hf in hg.files" :key="hf.id" style="padding-left:0">
                  <span class="fhist-ic" :class="catCls(hf.category)"><svg viewBox="0 0 24 24" width="12" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/></svg></span>
                  <n-tag size="tiny" :bordered="false">{{ hf.catLabel }}</n-tag>
                  <span class="faint">{{ fmtSize(hf.sizeBytes) }} · {{ String(hf.uploadedAt).slice(0, 10) }}</span>
                  <span style="flex:1"></span>
                  <n-button v-if="fileKind(hf.fileName) !== 'other'" size="tiny" quaternary @click="previewFile('deliverables', hf)">预览</n-button>
                  <n-button size="tiny" quaternary @click="downloadFile('deliverables', hf.id, hf.fileName)">下载</n-button>
                  <n-button size="tiny" quaternary @click="confirmDelete('删除该历史文件？', async () => { await api.delete(`/projects/${id}/files/deliverables/${hf.id}`); load() })">删除</n-button>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div class="card">
          <div class="card-h"><span class="card-ic b"><svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"/></svg></span><h3>项目资料</h3></div>
          <div class="small faint" style="margin-bottom:4px">图纸、隐蔽资料、联系函等过程文件按分类归档，与成果文件分开保存。</div>
          <div class="doc-cat" v-for="g in docGroups" :key="g.cat">
            <div style="display:flex;align-items:center;gap:9px;cursor:pointer;padding:3px 0" @click="openDocCat = openDocCat === g.cat ? '' : g.cat">
              <span class="role-tag" style="background:#eef4f0;display:inline-flex;padding:4px 5px"><svg viewBox="0 0 24 24" width="13" height="13" fill="none" stroke="#186140" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"/></svg></span>
              <b style="font-size:13px">{{ g.cat }}</b>
              <span class="faint small">{{ g.files.length }} 个文件</span>
              <span class="faint" style="margin-left:auto">{{ openDocCat === g.cat ? '▾' : '▸' }}</span>
            </div>
            <div v-if="openDocCat === g.cat" style="padding:2px 0 6px 35px">
              <div class="doc-f" v-for="f in g.files" :key="f.id">
                <span style="flex:1;min-width:0">{{ f.fileName }}</span>
                <span class="faint">{{ String(f.uploadedAt).slice(0, 10) }}</span>
                <n-button v-if="fileKind(f.fileName) !== 'other'" size="tiny" quaternary @click="previewFile('docs', f)">预览</n-button>
                <n-button size="tiny" quaternary @click="downloadFile('docs', f.id, f.fileName)">下载</n-button>
                <n-button size="tiny" quaternary @click="confirmDelete('删除该资料文件？', async () => { await api.delete(`/projects/${id}/files/docs/${f.id}`); load() })">删除</n-button>
              </div>
              <div v-if="!g.files.length" class="doc-f faint">暂无文件</div>
              <n-button size="tiny" style="margin-top:4px" @click="pickFile(f => uploadDoc(g.cat, f))">+ 上传到此分类</n-button>
            </div>
          </div>
        </div>
      </div>

      <!-- 造价解析（与成果文件并排） -->
      <div class="card" id="gcfxPanel" v-if="gcfxTarget">
        <div class="card-h"><span class="card-ic p"><svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21.21 15.89A10 10 0 1 1 8 2.83"/><path d="M22 12A10 10 0 0 0 12 2v10z"/></svg></span><h3>造价解析</h3><div class="sp"></div>
          <span class="small faint">{{ String(gcfxTarget.parse.parsedAt).slice(0, 16).replace('T', ' ') }} 解析</span>
          <n-button size="small" @click="reparse">重新解析</n-button>
        </div>
        <GcfxPanel :key="gcfxTarget.parse.id + '-' + gcfxTarget.parse.parsedAt" :parse="gcfxTarget.parse" :project="p" :file-name="gcfxTarget.fileName" />
      </div>
      <div class="card" v-else>
        <div class="card-h"><span class="card-ic p"><svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21.21 15.89A10 10 0 1 1 8 2.83"/><path d="M22 12A10 10 0 0 0 12 2v10z"/></svg></span><h3>造价解析</h3></div>
        <div class="faint small">暂无解析结果。上传 GCFX / SGCFX 成果文件后会自动解析并在这里展示。</div>
      </div>
    </div>

    <!-- 编辑项目 -->
    <n-modal v-model:show="showEdit" preset="card" title="编辑项目" style="width:640px;border-radius:16px">
      <n-form label-placement="top">
        <n-form-item label="项目名称"><n-input v-model:value="ef.name" /></n-form-item>
        <n-grid :cols="2" :x-gap="12">
          <n-form-item-gi label="委托单位"><n-input v-model:value="ef.client" /></n-form-item-gi>
          <n-form-item-gi label="业务类型"><n-select v-model:value="ef.bizType" :options="BIZ_TYPES.map(t => ({ label: t, value: t }))" /></n-form-item-gi>
          <n-form-item-gi label="联系人"><n-input v-model:value="ef.contactName" /></n-form-item-gi>
          <n-form-item-gi label="联系电话"><n-input v-model:value="ef.contactPhone" /></n-form-item-gi>
          <n-form-item-gi label="项目规模"><n-input v-model:value="ef.scale" /></n-form-item-gi>
          <n-form-item-gi label="状态"><n-select v-model:value="ef.status" :options="STATUSES.map(t => ({ label: t, value: t }))" /></n-form-item-gi>
          <n-form-item-gi label="负责人"><n-select v-model:value="ef.ownerMemberId" clearable :options="members.map(m => ({ label: m.name, value: m.id }))" /></n-form-item-gi>
          <n-form-item-gi label="报告编号"><n-input v-model:value="ef.reportNo" /></n-form-item-gi>
          <n-form-item-gi label="承接日期"><n-date-picker v-model:formatted-value="ef.acceptedDate" value-format="yyyy-MM-dd" type="date" style="width:100%" /></n-form-item-gi>
          <n-form-item-gi label="要求完成"><n-date-picker v-model:formatted-value="ef.dueDate" value-format="yyyy-MM-dd" type="date" clearable style="width:100%" /></n-form-item-gi>
          <n-form-item-gi label="出报告日期"><n-date-picker v-model:formatted-value="ef.reportDate" value-format="yyyy-MM-dd" type="date" clearable style="width:100%" /></n-form-item-gi>
          <n-form-item-gi label="咨询费应收（万元）"><n-input-number v-model:value="ef.fee" :min="0" style="width:100%" /></n-form-item-gi>
          <n-form-item-gi label="送审 / 控制价 / 预算 / 建安（万元）"><n-input-number v-model:value="ef.submitAmount" :min="0" style="width:100%" /></n-form-item-gi>
          <n-form-item-gi label="审定金额（万元，审核类）"><n-input-number v-model:value="ef.approvedAmount" :min="0" style="width:100%" /></n-form-item-gi>
        </n-grid>
        <n-form-item label="项目概况"><n-input v-model:value="ef.description" type="textarea" /></n-form-item>
      </n-form>
      <template #footer>
        <div style="display:flex;justify-content:flex-end;gap:10px">
          <n-button @click="showEdit = false">取消</n-button>
          <n-button type="primary" @click="saveEdit">保存</n-button>
        </div>
      </template>
    </n-modal>

    <!-- 编辑团队 -->
    <n-modal v-model:show="showTeam" preset="card" title="编辑团队分工" style="width:760px;max-width:94vw;border-radius:16px">
      <div style="display:flex;gap:8px;margin-bottom:6px;padding:0 2px" class="small faint">
        <span style="flex:1.1">成员</span><span style="flex:0.9">角色</span><span style="flex:1.6">人员职责</span><span style="width:104px">占比</span><span style="width:32px"></span>
      </div>
      <div v-for="(r, i) in teamRows" :key="i" style="display:flex;gap:8px;margin-bottom:10px;align-items:center">
        <n-select v-model:value="r.memberId" :options="members.map(m => ({ label: m.name, value: m.id }))" placeholder="成员" style="flex:1.1" />
        <n-select v-model:value="r.role" :options="ROLES.map(t => ({ label: t, value: t }))" style="flex:0.9" />
        <n-input v-model:value="r.duty" placeholder="如：土建清单编制与复核" style="flex:1.6" />
        <n-input-number v-model:value="r.share" :min="0" :max="100" style="width:104px" />
        <span class="small mut">%</span>
        <n-button quaternary @click="teamRows.splice(i, 1)">✕</n-button>
      </div>
      <n-button dashed block @click="teamRows.push({ memberId: null, role: '编制人', duty: '', share: 0 })">+ 添加成员</n-button>
      <p class="small faint" style="margin-top:10px">占比合计：{{ teamRows.reduce((a, r) => a + (r.share || 0), 0) }}%（建议合计 100%）</p>
      <template #footer>
        <div style="display:flex;justify-content:flex-end;gap:10px">
          <n-button @click="showTeam = false">取消</n-button>
          <n-button type="primary" @click="saveTeam">保存分工</n-button>
        </div>
      </template>
    </n-modal>

    <!-- 关联合同 -->
    <n-modal v-model:show="showLink" preset="card" title="关联合同" style="width:480px;border-radius:16px">
      <n-form label-placement="top">
        <n-form-item label="选择合同">
          <n-select v-model:value="linkForm.contractId" filterable :options="allContracts.map(c => ({ label: c.no + ' · ' + c.name, value: c.id }))" placeholder="在合同台账中选择" />
        </n-form-item>
        <n-checkbox v-model:checked="linkForm.isPrimary">设为主合同</n-checkbox>
      </n-form>
      <template #footer>
        <div style="display:flex;justify-content:flex-end;gap:10px">
          <n-button @click="showLink = false">取消</n-button>
          <n-button type="primary" :disabled="!linkForm.contractId" @click="doLink">确认关联</n-button>
        </div>
      </template>
    </n-modal>

    <!-- 新增回款（与收费台账一致） -->
    <n-modal v-model:show="showReceipt" preset="card" title="新增回款" style="width:480px;border-radius:16px">
      <n-form label-placement="top">
        <n-form-item label="回款日期"><n-date-picker v-model:formatted-value="rf.date" value-format="yyyy-MM-dd" type="date" style="width:100%" /></n-form-item>
        <n-form-item label="金额（万元）"><n-input-number v-model:value="rf.amount" :min="0" style="width:100%" /></n-form-item>
        <n-form-item label="所属合同（可选）">
          <n-select v-model:value="rf.contractId" clearable :options="d.contracts.map(c => ({ label: c.no + ' · ' + c.name, value: c.id }))" placeholder="未指定合同" />
        </n-form-item>
        <n-grid :cols="2" :x-gap="12">
          <n-form-item-gi label="方式"><n-select v-model:value="rf.method" :options="['银行转账', '现金', '承兑汇票', '其他'].map(t => ({ label: t, value: t }))" /></n-form-item-gi>
          <n-form-item-gi label="经办人"><n-input v-model:value="rf.handler" /></n-form-item-gi>
        </n-grid>
        <n-form-item label="备注"><n-input v-model:value="rf.note" /></n-form-item>
      </n-form>
      <template #footer>
        <div style="display:flex;justify-content:flex-end;gap:10px">
          <n-button @click="showReceipt = false">取消</n-button>
          <n-button type="primary" @click="addReceipt">保存回款</n-button>
        </div>
      </template>
    </n-modal>

    <PreviewModal ref="previewRef" />
  </div>
  <div v-else class="card"><n-spin size="large" /></div>
</template>
