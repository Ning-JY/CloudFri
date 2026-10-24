<script setup>
import { ref, onMounted, h } from 'vue'
import { useRouter } from 'vue-router'
import { NTag, useMessage } from 'naive-ui'
import api, { errMsg } from '../api'
import { fmt, statusType, todayStr } from '../utils'

const router = useRouter()
const message = useMessage()
const rows = ref([])
const totals = ref({ feeTotal: 0, receivedTotal: 0, projectCount: 0 })

async function load() {
  const { data } = await api.get('/fees')
  rows.value = data.rows
  totals.value = data.totals
}
onMounted(load)

// 新增回款（直接在本页登记，不必进项目详情）
const showAdd = ref(false)
const rf = ref({ projectId: null, date: todayStr(), amount: null, method: '银行转账', handler: '', note: '' })
function openAdd() {
  rf.value = { projectId: null, date: todayStr(), amount: null, method: '银行转账', handler: '', note: '' }
  showAdd.value = true
}
async function addReceipt() {
  if (!rf.value.projectId) { message.warning('请选择项目'); return }
  if (!rf.value.date || !rf.value.amount) { message.warning('请填写日期和金额'); return }
  try {
    await api.post(`/projects/${rf.value.projectId}/receipts`, {
      date: rf.value.date, amount: rf.value.amount, method: rf.value.method, handler: rf.value.handler, note: rf.value.note
    })
    message.success('回款已登记')
    showAdd.value = false
    load()
  } catch (e) { message.error(errMsg(e)) }
}

const columns = [
  { title: '编号', key: 'code', width: 118, render: (r) => h('span', { class: 'num faint' }, r.code) },
  { title: '项目名称', key: 'name', ellipsis: { tooltip: true }, render: (r) => h('div', [h('div', { class: 'pname' }, r.name), h('div', { class: 'psub' }, r.client)]) },
  { title: '状态', key: 'status', width: 96, render: (r) => h(NTag, { size: 'small', type: statusType(r.status), bordered: false }, () => r.status) },
  { title: '应收（万元）', key: 'fee', width: 110, align: 'right', render: (r) => h('span', { class: 'num' }, fmt(r.fee)) },
  { title: '已收（万元）', key: 'received', width: 110, align: 'right', render: (r) => h('span', { class: 'num' }, fmt(r.received)) },
  { title: '待收（万元）', key: 'left', width: 110, align: 'right', render: (r) => h('span', { class: 'num' + (r.fee - r.received > 0 ? ' neg' : '') }, fmt(Math.max(r.fee - r.received, 0))) },
  { title: '收款进度', key: 'pct', width: 150, render: (r) => {
      const pct = r.fee > 0 ? Math.min(100, Math.round(r.received / r.fee * 100)) : 0
      return h('div', { style: 'display:flex;align-items:center;gap:8px' }, [
        h('div', { style: 'flex:1;height:7px;border-radius:99px;background:#e9efeb;overflow:hidden' }, [
          h('div', { style: `height:100%;width:${pct}%;background:var(--pri);border-radius:99px` })
        ]),
        h('span', { class: 'small num' }, pct + '%')
      ])
    } },
  { title: '最近收款', key: 'lastReceiptDate', width: 110, render: (r) => r.lastReceiptDate || '—' },
  { title: '笔数', key: 'receiptCount', width: 70, align: 'right' }
]
</script>

<template>
  <div>
    <div class="card sumstrip" style="padding:0">
      <div class="cell"><div class="lb">项目数</div><div class="vl num">{{ totals.projectCount }} <small>个</small></div></div>
      <div class="cell"><div class="lb">咨询费应收合计</div><div class="vl num">{{ fmt(totals.feeTotal) }} <small>万元</small></div></div>
      <div class="cell"><div class="lb">已收合计</div><div class="vl num">{{ fmt(totals.receivedTotal) }} <small>万元</small></div></div>
      <div class="cell"><div class="lb">待收合计</div><div class="vl num neg">{{ fmt(Math.max(totals.feeTotal - totals.receivedTotal, 0)) }} <small>万元</small></div></div>
    </div>
    <div class="card" style="padding:14px 18px">
      <div style="display:flex;gap:10px;align-items:center">
        <b style="font-size:14.5px">项目收费明细</b>
        <div style="flex:1"></div>
        <n-button type="primary" @click="openAdd">+ 新增回款</n-button>
      </div>
    </div>
    <div class="card" style="padding:6px 10px">
      <n-data-table :columns="columns" :data="rows" :bordered="false"
        :row-props="(row) => ({ style: 'cursor:pointer', onClick: () => router.push('/projects/' + row.id) })" />
      <div v-if="!rows.length" class="faint" style="text-align:center;padding:26px">暂无项目收费数据</div>
      <p class="small faint" style="padding:6px 10px">点右上角「新增回款」直接登记；点击任意行进入项目详情查看收款记录。</p>
    </div>

    <n-modal v-model:show="showAdd" preset="card" title="新增回款" style="width:480px;border-radius:16px">
      <n-form label-placement="top">
        <n-form-item label="项目">
          <n-select v-model:value="rf.projectId" filterable :options="rows.map(r => ({ label: r.code + ' · ' + r.name, value: r.id }))" placeholder="选择项目" />
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
          <n-button @click="showAdd = false">取消</n-button>
          <n-button type="primary" @click="addReceipt">保存回款</n-button>
        </div>
      </template>
    </n-modal>
  </div>
</template>
