<script setup>
import { ref, computed, onMounted, h } from 'vue'
import { NButton, useMessage, useDialog } from 'naive-ui'
import api, { errMsg } from '../api'

const message = useMessage()
const dialog = useDialog()
const templates = ref([])
const records = ref([])
const activeTpl = ref(0)

const schemaOf = (t) => { try { return JSON.parse(t.schemaJson) } catch { return { fields: [], opinions: [] } } }
const tplById = (id) => templates.value.find(t => t.id === id)

async function load() {
  const [t, r] = await Promise.all([api.get('/forms/templates'), api.get('/forms/records')])
  templates.value = t.data
  records.value = r.data
}
onMounted(load)

const filteredRecords = computed(() => activeTpl.value ? records.value.filter(r => r.templateId === activeTpl.value) : records.value)
const summaryOf = (r) => { try { return JSON.parse(r.dataJson).reason || '—' } catch { return '—' } }

// 填写 / 编辑 / 查看
const showForm = ref(false)
const mode = ref('fill') // fill | view
const curTpl = ref(null)
const curRecord = ref(null)
const formData = ref({})

function openFill(t, record = null, prefill = null) {
  curTpl.value = t
  curRecord.value = record
  formData.value = record ? JSON.parse(record.dataJson) : (prefill ? { ...prefill } : {})
  mode.value = 'fill'
  showForm.value = true
}
function openView(r) {
  curTpl.value = tplById(r.templateId)
  curRecord.value = r
  formData.value = JSON.parse(r.dataJson)
  mode.value = 'view'
  showForm.value = true
}
async function save() {
  try {
    if (curRecord.value) {
      await api.put('/forms/records/' + curRecord.value.id, { data: formData.value })
      message.success('已保存修改')
    } else {
      const { data } = await api.post('/forms/records', { templateId: curTpl.value.id, data: formData.value })
      message.success('已保存 ' + data.no)
    }
    showForm.value = false
    load()
  } catch (e) { message.error(errMsg(e)) }
}
function copyOf(r) { openFill(tplById(r.templateId), null, JSON.parse(r.dataJson)) }
function del(r) {
  dialog.warning({ title: '删除记录', content: `确定删除记录 ${r.no}？`, positiveText: '删除', negativeText: '取消',
    onPositiveClick: async () => { await api.delete('/forms/records/' + r.id); message.success('已删除'); load() } })
}

function formalHTML(t, data) {
  const sc = schemaOf(t)
  let rows = ''
  let i = 0
  const fs = sc.fields || []
  while (i < fs.length) {
    const f = fs[i]
    if (f.w === 2 || i + 1 >= fs.length || fs[i + 1].w === 2) {
      rows += `<tr><td class="lb">${f.lb}</td><td colspan="3">${data[f.k] ?? ''}</td></tr>`; i += 1
    } else {
      const g = fs[i + 1]
      rows += `<tr><td class="lb">${f.lb}</td><td>${data[f.k] ?? ''}</td><td class="lb">${g.lb}</td><td>${data[g.k] ?? ''}</td></tr>`; i += 2
    }
  }
  rows += (sc.opinions || []).map(o => `<tr><td class="lb">${o}</td><td colspan="3" class="opinion"><div class="signline">签字：　　　　　　日期：　　　　　</div></td></tr>`).join('')
  return `<h2>宜宾规划设计咨询集团有限公司</h2><h4>${t.name}</h4><table class="ftab">${rows}</table>`
}

function downloadWord(r) {
  const t = tplById(r.templateId)
  const html = `<html xmlns:o="urn:schemas-microsoft-com:office:office" xmlns:w="urn:schemas-microsoft-com:office:word"><head><meta charset="utf-8"><title>${t.name}</title><style>table{border-collapse:collapse;width:100%}td{border:1px solid #888;padding:8px 12px}h2,h4{text-align:center}</style></head><body>${formalHTML(t, JSON.parse(r.dataJson))}</body></html>`
  const blob = new Blob(['\ufeff' + html], { type: 'application/msword' })
  const a = document.createElement('a')
  a.href = URL.createObjectURL(blob)
  a.download = `${r.no}-${t.name}.doc`
  a.click()
  setTimeout(() => URL.revokeObjectURL(a.href), 600)
}
function printRecord(r) {
  const t = tplById(r.templateId)
  const w = window.open('', '_blank')
  if (!w) { message.warning('浏览器拦截了打印窗口'); return }
  w.document.write(`<html><head><meta charset="utf-8"><title>${t.name}</title><style>body{font-family:"Microsoft YaHei",sans-serif;padding:24px}h2{text-align:center;font-size:20px}h4{text-align:center;font-size:16px}.ftab{width:100%;border-collapse:collapse;font-size:14px}.ftab td{border:1px solid #888;padding:9px 12px}.ftab td.lb{width:120px;background:#f2f2f2;font-weight:600}.signline{margin-top:44px;text-align:right;color:#999;font-size:12px}.opinion{min-height:84px}</style></head><body>${formalHTML(t, JSON.parse(r.dataJson))}</body></html>`)
  w.document.close(); w.focus(); w.print()
}

const columns = [
  { title: '编号', key: 'no', width: 130, render: (r) => h('span', { class: 'num faint' }, r.no) },
  { title: '模板', key: 'templateName', width: 120 },
  { title: '摘要', key: 'summary', ellipsis: { tooltip: true }, render: (r) => summaryOf(r) },
  { title: '填写人', key: 'filledByName', width: 90, render: (r) => r.filledByName || '—' },
  { title: '填写日期', key: 'createdAt', width: 110, render: (r) => String(r.createdAt).slice(0, 10) },
  { title: '操作', key: 'act', width: 330, render: (r) => h('div', { style: 'display:flex;gap:6px' }, [
    h(NButton, { size: 'small', onClick: () => openView(r) }, () => '查看'),
    h(NButton, { size: 'small', onClick: () => openFill(tplById(r.templateId), r) }, () => '编辑'),
    h(NButton, { size: 'small', onClick: () => copyOf(r) }, () => '复制'),
    h(NButton, { size: 'small', onClick: () => downloadWord(r) }, () => '下载'),
    h(NButton, { size: 'small', onClick: () => printRecord(r) }, () => '打印'),
    h(NButton, { size: 'small', quaternary: true, onClick: () => del(r) }, () => '删除')
  ]) }
]
</script>

<template>
  <div>
    <div class="card">
      <div class="card-h"><h3>选择模板</h3><div class="sp"></div><span class="small faint">点击模板开始填写，保存后进入下方记录</span></div>
      <div class="tpl-grid">
        <button v-for="t in templates" :key="t.id" class="tpl-card" @click="openFill(t)">
          <b>{{ t.name }}</b><p>{{ t.desc }}</p>
          <n-button size="small" type="primary" @click.stop="openFill(t)">开始填写</n-button>
        </button>
      </div>
    </div>

    <div class="card">
      <div class="card-h"><h3>填写记录</h3><div class="sp"></div>
        <n-select v-model:value="activeTpl" style="width:170px" :options="[{label:'全部模板',value:0},...templates.map(t=>({label:t.name,value:t.id}))]" />
      </div>
      <n-data-table :columns="columns" :data="filteredRecords" :bordered="false" />
      <div v-if="!filteredRecords.length" class="faint" style="text-align:center;padding:22px">暂无填写记录</div>
    </div>

    <n-modal v-model:show="showForm" preset="card" :title="curTpl ? curTpl.name + (mode === 'view' ? ' · 查看' : curRecord ? ' · 编辑' : ' · 填写') : '表单'" style="width:680px;border-radius:16px">
      <div v-if="mode === 'fill' && curTpl">
        <n-form label-placement="top">
          <div style="display:grid;grid-template-columns:1fr 1fr;gap:0 14px">
            <n-form-item v-for="f in schemaOf(curTpl).fields" :key="f.k" :label="f.lb" :style="f.w === 2 ? 'grid-column:1/-1' : ''">
              <n-select v-if="f.type === 'sel'" v-model:value="formData[f.k]" :options="f.opts.map(o => ({ label: o, value: o }))" />
              <n-date-picker v-else-if="f.type === 'date'" v-model:formatted-value="formData[f.k]" value-format="yyyy-MM-dd" type="date" style="width:100%" />
              <n-input v-else v-model:value="formData[f.k]" :placeholder="f.ph || ''" />
            </n-form-item>
          </div>
        </n-form>
      </div>
      <div v-else-if="curTpl" class="paper" v-html="formalHTML(curTpl, formData)"></div>
      <template #footer>
        <div style="display:flex;justify-content:flex-end;gap:10px" v-if="mode === 'fill'">
          <n-button @click="showForm = false">取消</n-button>
          <n-button type="primary" @click="save">保存记录</n-button>
        </div>
        <div style="display:flex;justify-content:flex-end;gap:10px" v-else>
          <n-button @click="showForm = false">关闭</n-button>
          <n-button @click="downloadWord(curRecord)">下载 Word</n-button>
          <n-button @click="printRecord(curRecord)">打印</n-button>
          <n-button @click="copyOf(curRecord)">复制新建</n-button>
          <n-button type="primary" @click="openFill(curTpl, curRecord)">编辑</n-button>
        </div>
      </template>
    </n-modal>
  </div>
</template>
