<script setup>
import { ref, watch } from 'vue'
import { useMessage } from 'naive-ui'
import api, { errMsg } from '../api'
import { todayStr } from '../utils'

// 开票记录编辑弹窗（收费台账与项目详情共用同一组件，别再各写一套）：
// 改开票日期/发票号/金额/所属合同/方式/经办人/备注，以及收款状态与到账日期。
// 用法：<ReceiptEditModal v-model:show="showEdit" :receipt="row" :project-id="pid" :project-label="label" @saved="reload" />
const props = defineProps({
  show: { type: Boolean, default: false },
  receipt: { type: Object, default: null },
  projectId: { type: Number, default: null },
  projectLabel: { type: String, default: '' }
})
const emit = defineEmits(['update:show', 'saved'])
const message = useMessage()
const f = ref({})
const contracts = ref([])

watch(() => props.show, async (v) => {
  if (!v || !props.receipt) return
  const r = props.receipt
  f.value = {
    date: r.date, invoiceNo: r.invoiceNo || '', amount: r.amount,
    contractId: r.contractId ?? null, method: r.method || '银行转账',
    handler: r.handler || '', note: r.note || '',
    isReceived: !!r.isReceived, receivedDate: r.receivedDate || todayStr()
  }
  contracts.value = []
  const pid = props.projectId ?? r.projectId
  if (pid) {
    try {
      const { data } = await api.get('/projects/' + pid)
      contracts.value = data.contracts || []
    } catch { /* 拉不到合同不阻塞编辑其他字段 */ }
  }
})

async function save() {
  if (!f.value.date || !f.value.amount) { message.warning('请填写开票日期和金额'); return }
  const r = props.receipt
  const pid = props.projectId ?? r.projectId
  try {
    await api.put(`/projects/${pid}/receipts/${r.id}`, {
      date: f.value.date, amount: f.value.amount, method: f.value.method,
      handler: f.value.handler, note: f.value.note,
      contractId: f.value.contractId, invoiceNo: f.value.invoiceNo || null,
      isReceived: f.value.isReceived,
      receivedDate: f.value.isReceived ? f.value.receivedDate : null
    })
    message.success('已保存修改')
    emit('update:show', false)
    emit('saved')
  } catch (e) { message.error(errMsg(e)) }
}
</script>

<template>
  <n-modal :show="show" @update:show="(v) => emit('update:show', v)" preset="card" title="编辑开票" style="width:480px;border-radius:16px">
    <n-form label-placement="top">
      <n-form-item v-if="projectLabel" label="项目"><n-input :value="projectLabel" disabled /></n-form-item>
      <n-form-item label="开票日期"><n-date-picker v-model:formatted-value="f.date" value-format="yyyy-MM-dd" type="date" style="width:100%" /></n-form-item>
      <n-form-item label="发票号"><n-input v-model:value="f.invoiceNo" placeholder="发票号码" /></n-form-item>
      <n-form-item label="金额（万元）"><n-input-number v-model:value="f.amount" :min="0" style="width:100%" /></n-form-item>
      <n-form-item label="所属合同（可选）">
        <n-select v-model:value="f.contractId" clearable :options="contracts.map(c => ({ label: c.no + ' · ' + c.name, value: c.id }))" placeholder="未指定合同" />
      </n-form-item>
      <n-grid :cols="2" :x-gap="12">
        <n-form-item-gi label="方式"><n-select v-model:value="f.method" :options="['银行转账', '现金', '承兑汇票', '其他'].map(t => ({ label: t, value: t }))" /></n-form-item-gi>
        <n-form-item-gi label="经办人"><n-input v-model:value="f.handler" /></n-form-item-gi>
      </n-grid>
      <n-form-item label="备注"><n-input v-model:value="f.note" type="textarea" :rows="2" /></n-form-item>
      <n-grid :cols="2" :x-gap="12">
        <n-form-item-gi label="收款状态">
          <n-select v-model:value="f.isReceived" :options="[{ label: '已开票 · 待收款', value: false }, { label: '已收款', value: true }]" />
        </n-form-item-gi>
        <n-form-item-gi v-if="f.isReceived" label="到账日期"><n-date-picker v-model:formatted-value="f.receivedDate" value-format="yyyy-MM-dd" type="date" style="width:100%" /></n-form-item-gi>
      </n-grid>
    </n-form>
    <template #footer>
      <div style="display:flex;justify-content:flex-end;gap:10px">
        <n-button @click="emit('update:show', false)">取消</n-button>
        <n-button type="primary" @click="save">保存</n-button>
      </div>
    </template>
  </n-modal>
</template>
