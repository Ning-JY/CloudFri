<script setup>
import { ref, computed, onMounted, h } from 'vue'
import { NButton, useMessage, useDialog } from 'naive-ui'
import { useRoute, useRouter } from 'vue-router'
import api, { errMsg } from '../api'
import { fmt, fmtSize, todayStr } from '../utils'
import PreviewModal from '../components/PreviewModal.vue'

const route = useRoute()
const router = useRouter()
const message = useMessage()
const dialog = useDialog()
const id = Number(route.params.id)
const TYPES = ['框架合同', '单项合同', '补充协议']

const d = ref(null)
const allProjects = ref([])
const previewRef = ref(null)

async function load() {
  const { data } = await api.get('/contracts/' + id, { params: { t: Date.now() } })
  d.value = data
}
onMounted(async () => {
  const { data } = await api.get('/projects')
  allProjects.value = data
  await load()
})
const c = computed(() => d.value?.contract)

// 编辑合同（与合同台账同一表单口径）
const showForm = ref(false)
const cf = ref({})
function openEdit() { cf.value = { ...c.value }; showForm.value = true }
async function save() {
  if (!cf.value.no || !cf.value.name) { message.warning('请填写合同编号和名称'); return }
  try {
    await api.put('/contracts/' + id, cf.value)
    message.success('已保存')
    showForm.value = false
    await load()
  } catch (e) { message.error(errMsg(e)) }
}

// 附件：可多选上传多个
function pickFiles(cb, multiple = true) {
  const inp = document.createElement('input')
  inp.type = 'file'
  if (multiple) inp.multiple = true
  inp.onchange = () => { if (inp.files.length) cb([...inp.files]) }
  inp.click()
}
async function uploadFiles(files) {
  try {
    for (const f of files) {
      const fd = new FormData()
      fd.append('file', f)
      await api.post(`/contracts/${id}/files`, fd)
    }
    message.success(files.length > 1 ? `已上传 ${files.length} 个附件` : '附件已上传')
    await load()
  } catch (e) { message.error(errMsg(e, '上传失败')) }
}
async function downloadFile(f) {
  const { data } = await api.get(`/contracts/${id}/files/${f.id}/download`, { responseType: 'blob' })
  const a = document.createElement('a')
  a.href = URL.createObjectURL(data)
  a.download = f.fileName
  a.click()
  setTimeout(() => URL.revokeObjectURL(a.href), 800)
}
function delFile(f) {
  dialog.warning({ title: '删除附件', content: `确定删除「${f.fileName}」？`, positiveText: '删除', negativeText: '取消',
    onPositiveClick: async () => { await api.delete(`/contracts/${id}/files/${f.id}`); message.success('已删除'); await load() } })
}

// 关联项目
const showLink = ref(false)
const linkProjectId = ref(null)
const linkPrimary = ref(false)
function openLink() { linkProjectId.value = null; linkPrimary.value = false; showLink.value = true }
async function doLink() {
  try {
    await api.post(`/contracts/${id}/link`, { projectId: linkProjectId.value, isPrimary: linkPrimary.value })
    message.success('已关联')
    showLink.value = false
    await load()
  } catch (e) { message.error(errMsg(e)) }
}
async function unlink(pid) {
  await api.delete(`/contracts/${id}/link/${pid}`)
  message.success('已取消关联')
  await load()
}

// 新增回款（挂到本页合同名下，必须从关联项目中选一个）
const showReceipt = ref(false)
const rf = ref({ projectId: null, date: todayStr(), amount: null, method: '银行转账', handler: '', note: '' })
function openReceipt() {
  rf.value = { projectId: d.value.projects.length === 1 ? d.value.projects[0].id : null, date: todayStr(), amount: null, method: '银行转账', handler: '', note: '' }
  showReceipt.value = true
}
async function addReceipt() {
  if (!rf.value.projectId) { message.warning('请选择收款所属项目'); return }
  if (!rf.value.date || !rf.value.amount) { message.warning('请填写日期和金额'); return }
  try {
    await api.post(`/projects/${rf.value.projectId}/receipts`, { ...rf.value, contractId: id })
    message.success('回款已登记')
    showReceipt.value = false
    await load()
  } catch (e) { message.error(errMsg(e)) }
}

const projectColumns = [
  { title: '编号', key: 'code', width: 118, render: (r) => h('span', { class: 'num faint' }, r.code) },
  { title: '项目名称', key: 'name', ellipsis: { tooltip: true }, render: (r) => h('div', [h('div', { class: 'pname' }, r.name), h('div', { class: 'psub' }, r.client)]) },
  { title: '', key: 'primary', width: 76, render: (r) => r.isPrimary ? h('span', { class: 'role-tag' }, '主合同') : '' },
  { title: '状态', key: 'status', width: 92 },
  { title: '咨询费应收（万元）', key: 'fee', width: 130, align: 'right', render: (r) => fmt(r.fee) },
  { title: '该项目已收（万元）', key: 'receivedTotal', width: 130, align: 'right', render: (r) => fmt(r.receivedTotal) },
  { title: '其中本 合同名下（万元）', key: 'contractReceived', width: 150, align: 'right', render: (r) => fmt(r.contractReceived) },
  { title: '操作', key: 'act', width: 150, render: (r) => h('div', { style: 'display:flex;gap:6px' }, [
    h(NButton, { size: 'small', onClick: () => router.push('/projects/' + r.id) }, () => '项目详情'),
    h(NButton, { size: 'small', quaternary: true, onClick: () => unlink(r.id) }, () => '取消关联')
  ]) }
]
const receiptColumns = [
  { title: '收款日期', key: 'date', width: 110 },
  { title: '所属项目', key: 'projectName', ellipsis: { tooltip: true }, render: (r) => h('span', [h('span', { class: 'num faint' }, r.projectCode + ' '), r.projectName]) },
  { title: '金额（万元）', key: 'amount', align: 'right', render: (r) => fmt(r.amount) },
  { title: '方式', key: 'method', width: 100 },
  { title: '经办人', key: 'handler', width: 90 },
  { title: '备注', key: 'note' }
]
</script>

<template>
  <div v-if="c">
    <div class="card d-head">
      <div style="display:flex;gap:10px;align-items:flex-start;flex-wrap:wrap">
        <div style="flex:1;min-width:260px">
          <div style="display:flex;gap:8px;align-items:center;margin-bottom:7px;flex-wrap:wrap">
            <button class="back-ic" @click="router.push('/contracts')" title="返回合同台账"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><line x1="19" y1="12" x2="5" y2="12"/><polyline points="12 19 5 12 12 5"/></svg></button>
            <span class="num faint">{{ c.no }}</span>
            <n-tag size="small" :bordered="false">{{ c.type }}</n-tag>
            <n-tag size="small" type="success" :bordered="false">{{ c.status || '—' }}</n-tag>
          </div>
          <div class="d-title">{{ c.name }}</div>
          <div class="small mut" style="margin-top:6px">甲方 {{ c.partyA || '—' }} · 签订 {{ c.signDate || '—' }}</div>
        </div>
        <n-button @click="openEdit">编辑合同</n-button>
      </div>
      <div class="d-amounts" style="grid-template-columns:repeat(3,1fr)">
        <div class="a t0"><span class="a-ic t0"><svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/></svg></span>
          <div style="flex:1;min-width:0"><div class="lb">合同金额</div><div class="vl num">{{ c.amount == null ? '按实结算' : fmt(c.amount) + ' 万' }}</div><div class="sb">{{ c.amountNote || '合同约定' }}</div></div></div>
        <div class="a t1"><span class="a-ic t1"><svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 12V7H5a2 2 0 0 1 0-4h14v4"/><path d="M3 5v14a2 2 0 0 0 2 2h16v-5"/><path d="M18 12a2 2 0 0 0 0 4h4v-4z"/></svg></span>
          <div style="flex:1;min-width:0"><div class="lb">本合同名下已收</div><div class="vl num">{{ fmt(d.receivedTotal) }} 万</div><div class="sb">登记回款时指定本 合同的收款合计</div></div></div>
        <div class="a t3"><span class="a-ic t3"><svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/></svg></span>
          <div style="flex:1;min-width:0"><div class="lb">关联项目</div><div class="vl num">{{ d.projects.length }} <small style="font-size:13px">个</small></div><div class="sb">一本合同可关联多个项目</div></div></div>
      </div>
    </div>

    <div class="d-top2">
      <div class="card">
        <div class="card-h"><span class="card-ic g"><svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21.44 11.05l-9.19 9.19a6 6 0 0 1-8.49-8.49l8.57-8.57A4 4 0 1 1 18 8.84l-8.59 8.57a2 2 0 0 1-2.83-2.83l8.49-8.48"/></svg></span><h3>合同附件</h3><div class="sp"></div>
          <n-button size="small" type="primary" @click="pickFiles(uploadFiles)">+ 上传附件（可多选）</n-button>
        </div>
        <div v-if="!d.files.length" class="faint small">暂无附件，点右上角上传合同扫描件、电子版等，可一次多选多个文件。</div>
        <div v-for="f in d.files" :key="f.id" style="display:flex;gap:8px;align-items:center;padding:8px 0;border-top:1px solid var(--line-soft)">
          <div style="flex:1;min-width:0">
            <div style="font-size:13.5px">{{ f.fileName }}</div>
            <div class="psub">{{ fmtSize(f.sizeBytes) }} · {{ String(f.uploadedAt).slice(0, 10) }} · {{ f.uploadedBy || '' }}</div>
          </div>
          <n-button size="small" @click="previewRef.open(`/contracts/${id}/files/${f.id}/download`, f.fileName)">预览</n-button>
          <n-button size="small" @click="downloadFile(f)">下载</n-button>
          <n-button size="small" quaternary @click="delFile(f)">删除</n-button>
        </div>
      </div>

      <div class="card">
        <div class="card-h"><span class="card-ic o"><svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 12V7H5a2 2 0 0 1 0-4h14v4"/><path d="M3 5v14a2 2 0 0 0 2 2h16v-5"/><path d="M18 12a2 2 0 0 0 0 4h4v-4z"/></svg></span><h3>本合同收款</h3><div class="sp"></div>
          <n-button size="small" type="primary" ghost @click="openReceipt">+ 新增回款</n-button>
        </div>
        <div class="small mut" style="margin-bottom:6px">合计 <b class="num">{{ fmt(d.receivedTotal) }}</b> 万元 · {{ d.receipts.length }} 笔（仅统计登记时指定了本 合同的收款）</div>
        <n-data-table v-if="d.receipts.length" size="small" :bordered="false" :columns="receiptColumns" :data="d.receipts" :scroll-x="640" />
        <div v-else class="faint small" style="padding:8px 0">暂无挂在本 合同名下的收款。在项目详情或收费台账登记回款时选择「所属合同」即可归到这里。</div>
      </div>
    </div>

    <div class="card">
      <div class="card-h"><span class="card-ic b"><svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="2" y="7" width="20" height="14" rx="2"/><path d="M16 7V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v2"/></svg></span><h3>关联项目</h3><div class="sp"></div>
        <n-button size="small" :type="d.projects.length ? 'default' : 'primary'" @click="openLink">关联项目</n-button>
      </div>
      <n-data-table v-if="d.projects.length" size="small" :bordered="false" :columns="projectColumns" :data="d.projects" :scroll-x="900" />
      <div v-else class="faint small">暂未关联项目，点右上角「关联项目」添加（框架合同可挂多个项目）。</div>
    </div>

    <!-- 编辑合同 -->
    <n-drawer v-model:show="showForm" :width="560" placement="right">
      <n-drawer-content title="编辑合同" closable>
        <n-form label-placement="top">
          <n-grid :cols="2" :x-gap="12">
            <n-form-item-gi label="合同编号 *"><n-input v-model:value="cf.no" /></n-form-item-gi>
            <n-form-item-gi label="合同类型"><n-select v-model:value="cf.type" :options="TYPES.map(t=>({label:t,value:t}))" /></n-form-item-gi>
          </n-grid>
          <n-form-item label="合同名称 *"><n-input v-model:value="cf.name" /></n-form-item>
          <n-grid :cols="2" :x-gap="12">
            <n-form-item-gi label="甲方（委托方）"><n-input v-model:value="cf.partyA" /></n-form-item-gi>
            <n-form-item-gi label="签订日期"><n-date-picker v-model:formatted-value="cf.signDate" value-format="yyyy-MM-dd" type="date" clearable style="width:100%" /></n-form-item-gi>
            <n-form-item-gi label="合同金额（万元）"><n-input-number v-model:value="cf.amount" :min="0" clearable style="width:100%" placeholder="框架合同可留空" /></n-form-item-gi>
            <n-form-item-gi label="金额备注"><n-input v-model:value="cf.amountNote" placeholder="如：按实结算" /></n-form-item-gi>
            <n-form-item-gi label="状态"><n-select v-model:value="cf.status" :options="['履行中','已完成','已终止'].map(t=>({label:t,value:t}))" /></n-form-item-gi>
          </n-grid>
        </n-form>
        <template #footer>
          <div style="display:flex;justify-content:flex-end;gap:10px">
            <n-button @click="showForm = false">取消</n-button>
            <n-button type="primary" @click="save">保存</n-button>
          </div>
        </template>
      </n-drawer-content>
    </n-drawer>

    <n-modal v-model:show="showLink" preset="card" title="关联到项目" style="width:480px;border-radius:16px">
      <n-form label-placement="top">
        <n-form-item label="选择项目">
          <n-select v-model:value="linkProjectId" filterable :options="allProjects.map(p=>({label:p.code + ' · ' + p.name, value:p.id}))" placeholder="选择项目" />
        </n-form-item>
        <n-checkbox v-model:checked="linkPrimary">设为该项目的主合同</n-checkbox>
      </n-form>
      <template #footer>
        <div style="display:flex;justify-content:flex-end;gap:10px">
          <n-button @click="showLink = false">取消</n-button>
          <n-button type="primary" :disabled="!linkProjectId" @click="doLink">确认关联</n-button>
        </div>
      </template>
    </n-modal>

    <n-modal v-model:show="showReceipt" preset="card" title="新增回款（本合同）" style="width:480px;border-radius:16px">
      <n-form label-placement="top">
        <n-form-item label="所属项目">
          <n-select v-model:value="rf.projectId" :options="d.projects.map(p => ({ label: p.code + ' · ' + p.name, value: p.id }))" placeholder="从关联项目中选择" />
        </n-form-item>
        <n-form-item label="回款日期"><n-date-picker v-model:formatted-value="rf.date" value-format="yyyy-MM-dd" type="date" style="width:100%" /></n-form-item>
        <n-form-item label="金额（万元）"><n-input-number v-model:value="rf.amount" :min="0" style="width:100%" /></n-form-item>
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
