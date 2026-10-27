<script setup>
import { ref, computed, onMounted, h } from 'vue'
import { useRouter } from 'vue-router'
import { NTag, useMessage } from 'naive-ui'
import api, { errMsg } from '../api'
import { fmt, statusType, todayStr, iconBtn } from '../utils'

const router = useRouter()
const message = useMessage()
const rows = ref([])
const totals = ref({ feeTotal: 0, receivedTotal: 0, projectCount: 0 })
const invoicedTotal = computed(() => rows.value.reduce((a, r) => a + (r.invoiced || 0), 0))
const allContracts = ref([])

async function load() {
  const { data } = await api.get('/fees')
  rows.value = data.rows
  totals.value = data.totals
  loadReceipts()
}
onMounted(async () => {
  const { data } = await api.get('/contracts')
  allContracts.value = data
  load()
})

// 开票与收款明细多维查看：按项目 / 按合同 / 关键词筛选（v1.1 两段式：开票在前、确认收款在后）
const recRows = ref([])
const recTotal = ref(0)
const recReceived = ref(0)
const recCount = ref(0)
const fProject = ref(null)
const fContract = ref(null)
const fQ = ref('')
async function loadReceipts() {
  const params = {}
  if (fProject.value) params.projectId = fProject.value
  if (fContract.value) params.contractId = fContract.value
  if (fQ.value) params.q = fQ.value
  const { data } = await api.get('/fees/receipts', { params })
  recRows.value = data.rows
  recTotal.value = data.total
  recReceived.value = data.receivedTotal || 0
  recCount.value = data.count
}
// 确认收款（与项目/合同详情同一接口与弹窗口径，这里用行内日期=今天快速确认，改日期去项目详情）
async function confirmReceipt(r) {
  try {
    await api.post(`/projects/${r.projectId}/receipts/${r.id}/confirm`, { receivedDate: todayStr() })
    message.success('已确认收款（到账日期为今天，如需改日期请到项目详情）')
    load()
  } catch (e) { message.error(errMsg(e)) }
}
const recColumns = [
  { title: '开票日期', key: 'date', width: 102 },
  { title: '发票号', key: 'invoiceNo', width: 120, ellipsis: { tooltip: true }, render: (r) => r.invoiceNo || h('span', { class: 'faint' }, '—') },
  { title: '项目', key: 'projectName', ellipsis: { tooltip: true }, render: (r) => h('span', [h('span', { class: 'num faint' }, r.projectCode + ' '), r.projectName]) },
  { title: '所属合同', key: 'contractNo', width: 130, render: (r) => r.contractNo ? h('span', { title: r.contractName || '' }, r.contractNo) : h('span', { class: 'faint' }, '未指定') },
  { title: '金额（万元）', key: 'amount', align: 'right', render: (r) => fmt(r.amount) },
  { title: '状态', key: 'isReceived', width: 148, render: (r) => r.isReceived
      ? h(NTag, { size: 'small', type: 'success', bordered: false }, () => '已收款 · ' + (r.receivedDate || ''))
      : h(NTag, { size: 'small', type: 'warning', bordered: false }, () => '已开票 · 待收款') },
  { title: '备注', key: 'note', ellipsis: { tooltip: true } },
  { title: '', key: 'act', width: 46, render: (r) => r.isReceived ? null : iconBtn('check', 'green', '确认收款（今天到账）', () => confirmReceipt(r)) }
]

// 新增开票（直接在本页登记，不必进项目详情；选项目后可指定该项目关联的合同）
const showAdd = ref(false)
const rf = ref({ projectId: null, contractId: null, date: todayStr(), amount: null, method: '银行转账', handler: '', note: '', invoiceNo: '' })
const rfContracts = ref([])
function openAdd() {
  rf.value = { projectId: null, contractId: null, date: todayStr(), amount: null, method: '银行转账', handler: '', note: '', invoiceNo: '' }
  rfContracts.value = []
  showAdd.value = true
}
async function onRfProject(pid) {
  rf.value.contractId = null
  rfContracts.value = []
  if (!pid) return
  try {
    const { data } = await api.get('/projects/' + pid)
    rfContracts.value = data.contracts || []
  } catch { /* 拉不到合同时不阻塞登记 */ }
}
async function addReceipt() {
  if (!rf.value.projectId) { message.warning('请选择项目'); return }
  if (!rf.value.date || !rf.value.amount) { message.warning('请填写开票日期和金额'); return }
  try {
    await api.post(`/projects/${rf.value.projectId}/receipts`, {
      date: rf.value.date, amount: rf.value.amount, method: rf.value.method, handler: rf.value.handler, note: rf.value.note, contractId: rf.value.contractId, invoiceNo: rf.value.invoiceNo || null
    })
    message.success('开票已登记（待收款）')
    showAdd.value = false
    load()
  } catch (e) { message.error(errMsg(e)) }
}

const columns = [
  { title: '编号', key: 'code', width: 118, render: (r) => h('span', { class: 'num faint' }, r.code) },
  { title: '项目名称', key: 'name', ellipsis: { tooltip: true }, render: (r) => h('div', [h('div', { class: 'pname' }, r.name), h('div', { class: 'psub' }, r.client)]) },
  { title: '状态', key: 'status', width: 96, render: (r) => h(NTag, { size: 'small', type: statusType(r.status), bordered: false }, () => r.status) },
  { title: '应收（万元）', key: 'fee', width: 105, align: 'right', render: (r) => h('span', { class: 'num' }, fmt(r.fee)) },
  { title: '已开票（万元）', key: 'invoiced', width: 110, align: 'right', render: (r) => h('span', { class: 'num' }, fmt(r.invoiced)) },
  { title: '已收（万元）', key: 'received', width: 105, align: 'right', render: (r) => h('span', { class: 'num' }, fmt(r.received)) },
  { title: '待收（万元）', key: 'left', width: 105, align: 'right', render: (r) => h('span', { class: 'num' + (r.fee - r.received > 0 ? ' neg' : '') }, fmt(Math.max(r.fee - r.received, 0))) },
  { title: '收款进度', key: 'pct', width: 150, render: (r) => {
      const pct = r.fee > 0 ? Math.min(100, Math.round(r.received / r.fee * 100)) : 0
      return h('div', { style: 'display:flex;align-items:center;gap:8px' }, [
        h('div', { style: 'flex:1;height:7px;border-radius:99px;background:#e9efeb;overflow:hidden' }, [
          h('div', { style: `height:100%;width:${pct}%;background:var(--pri);border-radius:99px` })
        ]),
        h('span', { class: 'small num' }, pct + '%')
      ])
    } },
  { title: '最近开票', key: 'lastReceiptDate', width: 110, render: (r) => r.lastReceiptDate || '—' },
  { title: '开票笔数', key: 'receiptCount', width: 76, align: 'right' }
]
</script>

<template>
  <div>
    <div class="card sumstrip" style="padding:0">
      <div class="cell"><div class="lb">项目数</div><div class="vl num">{{ totals.projectCount }} <small>个</small></div></div>
      <div class="cell"><div class="lb">咨询费应收合计</div><div class="vl num">{{ fmt(totals.feeTotal) }} <small>万元</small></div></div>
      <div class="cell"><div class="lb">已开票合计</div><div class="vl num">{{ fmt(invoicedTotal) }} <small>万元</small></div></div>
      <div class="cell"><div class="lb">已收合计</div><div class="vl num">{{ fmt(totals.receivedTotal) }} <small>万元</small></div></div>
      <div class="cell"><div class="lb">待收合计</div><div class="vl num neg">{{ fmt(Math.max(totals.feeTotal - totals.receivedTotal, 0)) }} <small>万元</small></div></div>
    </div>
    <div class="card" style="padding:14px 18px">
      <div style="display:flex;gap:10px;align-items:center">
        <b style="font-size:14.5px">项目收费明细</b>
        <div style="flex:1"></div>
        <n-button type="primary" @click="openAdd">+ 新增开票</n-button>
      </div>
    </div>
    <div class="card" style="padding:6px 10px">
      <n-data-table :columns="columns" :data="rows" :bordered="false" :scroll-x="1060"
        :row-props="(row) => ({ style: 'cursor:pointer', onClick: () => router.push('/projects/' + row.id) })" />
      <div v-if="!rows.length" class="faint" style="text-align:center;padding:26px">暂无项目收费数据</div>
      <p class="small faint" style="padding:6px 10px">点右上角「新增开票」直接登记；点击任意行进入项目详情查看开票与收款记录。</p>
    </div>

    <div class="card" style="padding:14px 18px">
      <div style="display:flex;gap:10px;align-items:center;flex-wrap:wrap">
        <b style="font-size:14.5px">开票与收款明细</b>
        <span class="small faint">按项目、按合同两个维度查看每一笔开票与到账状态</span>
        <div style="flex:1"></div>
        <n-select v-model:value="fProject" clearable filterable :options="rows.map(r => ({ label: r.code + ' · ' + r.name, value: r.id }))" placeholder="全部项目" style="width:230px" @update:value="loadReceipts" />
        <n-select v-model:value="fContract" clearable filterable :options="allContracts.map(c => ({ label: c.no + ' · ' + c.name, value: c.id }))" placeholder="全部合同" style="width:230px" @update:value="loadReceipts" />
        <n-input v-model:value="fQ" placeholder="搜索项目 / 合同" style="width:180px" clearable @update:value="loadReceipts" />
      </div>
      <div class="small mut" style="margin-top:8px">当前筛选：{{ recCount }} 笔 · 开票合计 <b class="num">{{ fmt(recTotal) }}</b> 万元 · 其中已收 <b class="num">{{ fmt(recReceived) }}</b> 万元</div>
    </div>
    <div class="card" style="padding:6px 10px">
      <n-data-table :columns="recColumns" :data="recRows" :bordered="false" :scroll-x="1020" />
      <div v-if="!recRows.length" class="faint" style="text-align:center;padding:26px">暂无符合条件的开票</div>
    </div>

    <n-modal v-model:show="showAdd" preset="card" title="新增开票" style="width:480px;border-radius:16px">
      <n-form label-placement="top">
        <n-form-item label="项目">
          <n-select v-model:value="rf.projectId" filterable :options="rows.map(r => ({ label: r.code + ' · ' + r.name, value: r.id }))" placeholder="选择项目" @update:value="onRfProject" />
        </n-form-item>
        <n-form-item label="所属合同（可选）">
          <n-select v-model:value="rf.contractId" clearable :options="rfContracts.map(c => ({ label: c.no + ' · ' + c.name, value: c.id }))" :placeholder="rf.projectId ? '未指定合同' : '先选择项目'" />
        </n-form-item>
        <n-form-item label="开票日期"><n-date-picker v-model:formatted-value="rf.date" value-format="yyyy-MM-dd" type="date" style="width:100%" /></n-form-item>
        <n-form-item label="发票号"><n-input v-model:value="rf.invoiceNo" placeholder="发票号码（可后补）" /></n-form-item>
        <n-form-item label="金额（万元）"><n-input-number v-model:value="rf.amount" :min="0" style="width:100%" /></n-form-item>
        <n-grid :cols="2" :x-gap="12">
          <n-form-item-gi label="方式"><n-select v-model:value="rf.method" :options="['银行转账', '现金', '承兑汇票', '其他'].map(t => ({ label: t, value: t }))" /></n-form-item-gi>
          <n-form-item-gi label="经办人"><n-input v-model:value="rf.handler" /></n-form-item-gi>
        </n-grid>
        <n-form-item label="备注"><n-input v-model:value="rf.note" /></n-form-item>
      </n-form>
      <template #footer>
        <div style="display:flex;justify-content:flex-end;gap:10px">
          <n-button @click="showAdd = false">取消</n-button>
          <n-button type="primary" @click="addReceipt">保存开票</n-button>
        </div>
      </template>
    </n-modal>
  </div>
</template>
