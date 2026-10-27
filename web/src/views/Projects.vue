<script setup>
import { ref, computed, onMounted, h } from 'vue'
import { useRouter } from 'vue-router'
import { NTag, NButton, useMessage, useDialog } from 'naive-ui'
import api, { errMsg } from '../api'
import { fmt, priceOf, statusType, todayStr } from '../utils'
import { useDicts } from '../stores/dicts'
import TableTools from '../components/TableTools.vue'

const router = useRouter()
const message = useMessage()
const dialog = useDialog()
const rows = ref([])
const members = ref([])
const q = ref('')
const status = ref('')
const bizType = ref('')
const ownerId = ref('')
const CUR_YEAR = new Date().getFullYear()
const year = ref(CUR_YEAR)
const YEAR_OPTS = [
  { label: CUR_YEAR + ' 年', value: CUR_YEAR },
  { label: (CUR_YEAR - 1) + ' 年', value: CUR_YEAR - 1 },
  { label: '全部年度', value: '' }
]
const loading = ref(false)

// 业务类型/状态走全站字典（字典维护里增删排序即生效），store 未加载时用兜底值
const dicts = useDicts()
const BIZ_TYPES = computed(() => dicts.names('BizType'))
const STATUSES = computed(() => dicts.names('ProjectStatus'))

async function load() {
  loading.value = true
  try {
    const params = {}
    if (q.value) params.q = q.value
    if (status.value) params.status = status.value
    if (bizType.value) params.bizType = bizType.value
    if (ownerId.value) params.ownerId = ownerId.value
    if (year.value !== '') params.year = year.value
    const { data } = await api.get('/projects', { params })
    rows.value = data
  } finally { loading.value = false }
}

const sums = computed(() => ({
  n: rows.value.length,
  price: rows.value.reduce((a, p) => a + (priceOf(p).v || 0), 0),
  fee: rows.value.reduce((a, p) => a + (p.fee || 0), 0),
  received: rows.value.reduce((a, p) => a + (p.received || 0), 0)
}))

const ALL_COLUMNS = [
  { title: '编号', key: 'code', width: 118, render: (r) => h('span', { class: 'num faint' }, r.code) },
  { title: '项目名称', key: 'name', ellipsis: { tooltip: true }, render: (r) => h('div', [h('div', { class: 'pname' }, r.name), h('div', { class: 'psub' }, r.client)]) },
  { title: '业务类型', key: 'bizType', width: 108, render: (r) => h(NTag, { size: 'small', bordered: false }, () => r.bizType) },
  { title: '控制价 / 结算价（万元）', key: 'price', width: 150, align: 'right', exportValue: (r) => priceOf(r).v, render: (r) => { const pr = priceOf(r); return h('div', [h('div', { class: 'num', style: 'font-weight:650' }, fmt(pr.v)), h('div', { class: 'psub' }, pr.lb)]) } },
  { title: '咨询费（万元）', key: 'fee', width: 105, align: 'right', render: (r) => h('span', { class: 'num' }, fmt(r.fee)) },
  { title: '已收', key: 'received', width: 95, align: 'right', render: (r) => h('span', { class: 'num' }, fmt(r.received)) },
  { title: '负责人', key: 'ownerName', width: 86, render: (r) => r.ownerName || '—' },
  { title: '状态', key: 'status', width: 96, render: (r) => h(NTag, { size: 'small', type: statusType(r.status), bordered: false }, () => r.status) },
  { title: '承接日期', key: 'acceptedDate', width: 108, render: (r) => h('span', { class: 'num mut' }, r.acceptedDate || '—') },
  { title: '创建', key: 'createdAt', width: 132, exportValue: (r) => (r.createdAt || '').slice(0, 10) + (r.createdBy ? ' ' + r.createdBy : ''), render: (r) => h('div', [h('div', { class: 'num mut' }, (r.createdAt || '').slice(0, 10) || '—'), h('div', { class: 'psub' }, r.createdBy || '')]) }
]
// 列显隐（TableTools 工具条控制，存本地）
const visibleKeys = ref(ALL_COLUMNS.map(c => c.key))
const columns = computed(() => ALL_COLUMNS.filter(c => visibleKeys.value.includes(c.key)))
// 删除项目不在列表里操作（防误删）：统一在项目详情页头部删除。

// 新建登记（精简版：金额与合同不在登记时填写）
const showCreate = ref(false)
const form = ref({ name: '', client: '', contactName: '', contactPhone: '', bizType: '结算审核', ownerMemberId: null, acceptedDate: todayStr(), dueDate: null, status: '编制中', scale: '', description: '' })
async function create() {
  if (!form.value.name || !form.value.client) { message.warning('请填写项目名称和委托单位'); return }
  try {
    const { data } = await api.post('/projects', {
      name: form.value.name, client: form.value.client,
      contactName: form.value.contactName || null, contactPhone: form.value.contactPhone || null,
      bizType: form.value.bizType, scale: form.value.scale || null, status: form.value.status,
      ownerMemberId: form.value.ownerMemberId,
      acceptedDate: form.value.acceptedDate, dueDate: form.value.dueDate,
      reportDate: null, reportNo: null, submitAmount: null, approvedAmount: null, fee: 0,
      description: form.value.description || null
    })
    message.success('已登记 ' + data.code)
    showCreate.value = false
    router.push('/projects/' + data.id)
  } catch (e) { message.error(errMsg(e)) }
}

onMounted(async () => {
  const { data } = await api.get('/members')
  members.value = data
  load()
})
</script>

<template>
  <div>
    <div class="card" style="padding:14px 18px">
      <div style="display:flex;gap:10px;align-items:center;flex-wrap:wrap">
        <n-input v-model:value="q" placeholder="搜索项目名称 / 委托单位 / 编号" style="width:280px" clearable @update:value="load" />
        <n-select v-model:value="year" :options="YEAR_OPTS" style="width:118px" @update:value="load" />
        <n-select v-model:value="bizType" :options="[{label:'全部业务类型',value:''},...BIZ_TYPES.map(t=>({label:t,value:t}))]" style="width:150px" @update:value="load" />
        <n-select v-model:value="ownerId" :options="[{label:'全部负责人',value:''},...members.map(m=>({label:m.name,value:m.id}))]" style="width:132px" @update:value="load" />
        <div style="flex:1"></div>
        <n-button type="primary" @click="showCreate = true">+ 新建登记</n-button>
      </div>
      <div style="display:flex;gap:6px;margin-top:12px">
        <n-button v-for="s in ['', ...STATUSES]" :key="s" size="small" :type="status === s ? 'primary' : 'default'" :ghost="status !== s" @click="status = s; load()">{{ s || '全部' }}</n-button>
      </div>
    </div>

    <div class="card sumstrip" style="padding:0">
      <div class="cell"><div class="lb">项目数（当前筛选）</div><div class="vl num">{{ sums.n }} <small>个</small></div></div>
      <div class="cell"><div class="lb">金额合计（控制价 / 结算价）</div><div class="vl num">{{ fmt(sums.price) }} <small>万元</small></div></div>
      <div class="cell"><div class="lb">咨询费应收</div><div class="vl num">{{ fmt(sums.fee) }} <small>万元</small></div></div>
      <div class="cell"><div class="lb">已收咨询费</div><div class="vl num">{{ fmt(sums.received) }} <small>万元</small></div></div>
    </div>

    <div class="card" style="padding:6px 10px;position:relative">
      <div style="position:absolute;top:13px;right:14px;z-index:6">
        <TableTools v-model="visibleKeys" :cols="ALL_COLUMNS" :rows="rows" filename="项目台账" storage-key="projects" />
      </div>
      <n-data-table :columns="columns" :data="rows" :loading="loading" :bordered="false"
        :pagination="{ pageSize: 15, showSizePicker: true, pageSizes: [15, 30, 50] }"
        :row-props="(row) => ({ style: 'cursor:pointer', onClick: () => router.push('/projects/' + row.id) })" />
      <div v-if="!loading && !rows.length" class="faint" style="text-align:center;padding:26px">没有符合条件的项目，试试调整筛选或新建登记</div>
    </div>

    <n-drawer v-model:show="showCreate" :width="520" placement="right">
      <n-drawer-content title="新建登记" closable>
        <n-form label-placement="top">
          <n-form-item label="项目名称 *"><n-input v-model:value="form.name" placeholder="如：翠屏区实验小学改扩建工程" /></n-form-item>
          <n-grid :cols="2" :x-gap="12">
            <n-form-item-gi label="委托单位 *"><n-input v-model:value="form.client" placeholder="建设单位 / 委托方" /></n-form-item-gi>
            <n-form-item-gi label="业务类型"><n-select v-model:value="form.bizType" :options="BIZ_TYPES.map(t=>({label:t,value:t}))" /></n-form-item-gi>
            <n-form-item-gi label="委托联系人"><n-input v-model:value="form.contactName" placeholder="联系人姓名" /></n-form-item-gi>
            <n-form-item-gi label="联系电话"><n-input v-model:value="form.contactPhone" placeholder="联系电话" /></n-form-item-gi>
            <n-form-item-gi label="项目负责人"><n-select v-model:value="form.ownerMemberId" clearable :options="members.map(m=>({label:m.name,value:m.id}))" placeholder="选择负责人" /></n-form-item-gi>
            <n-form-item-gi label="项目规模"><n-input v-model:value="form.scale" placeholder="如：建筑面积 12,000㎡" /></n-form-item-gi>
            <n-form-item-gi label="承接日期"><n-date-picker v-model:formatted-value="form.acceptedDate" value-format="yyyy-MM-dd" type="date" style="width:100%" /></n-form-item-gi>
            <n-form-item-gi label="要求完成日期"><n-date-picker v-model:formatted-value="form.dueDate" value-format="yyyy-MM-dd" type="date" clearable style="width:100%" /></n-form-item-gi>
          </n-grid>
          <n-form-item label="备注"><n-input v-model:value="form.description" type="textarea" placeholder="资料接收情况、特殊约定等" /></n-form-item>
        </n-form>
        <p class="small faint">送审金额、审定金额、合同金额等不在登记时填写：金额随项目推进在详情页维护，合同金额在合同管理中维护。</p>
        <template #footer>
          <div style="display:flex;justify-content:flex-end;gap:10px">
            <n-button @click="showCreate = false">取消</n-button>
            <n-button type="primary" @click="create">保存登记</n-button>
          </div>
        </template>
      </n-drawer-content>
    </n-drawer>
  </div>
</template>
