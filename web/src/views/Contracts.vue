<script setup>
import { ref, onMounted, h } from 'vue'
import { useRouter } from 'vue-router'
import { NTag, NButton, useMessage, useDialog } from 'naive-ui'
import api, { errMsg } from '../api'
import { fmt } from '../utils'

const router = useRouter()
const message = useMessage()
const dialog = useDialog()
const rows = ref([])
const projects = ref([])
const q = ref('')
const type = ref('')
const TYPES = ['框架合同', '单项合同', '补充协议']

async function load() {
  const params = {}
  if (q.value) params.q = q.value
  if (type.value) params.type = type.value
  const { data } = await api.get('/contracts', { params })
  rows.value = data
}
onMounted(async () => {
  const { data } = await api.get('/projects')
  projects.value = data
  load()
})

const showForm = ref(false)
const cf = ref({})
function openCreate() {
  cf.value = { no: '', name: '', type: '单项合同', partyA: '', amount: null, amountNote: '', signDate: null, status: '履行中' }
  showForm.value = true
}
function openEdit(r) { cf.value = { ...r }; showForm.value = true }
async function save() {
  if (!cf.value.no || !cf.value.name) { message.warning('请填写合同编号和名称'); return }
  try {
    if (cf.value.id) await api.put('/contracts/' + cf.value.id, cf.value)
    else await api.post('/contracts', cf.value)
    message.success('已保存')
    showForm.value = false
    load()
  } catch (e) { message.error(errMsg(e)) }
}
function del(r) {
  dialog.warning({ title: '删除合同', content: `确定删除合同「${r.name}」？其与项目的关联将一并解除。`, positiveText: '删除', negativeText: '取消',
    onPositiveClick: async () => { await api.delete('/contracts/' + r.id); message.success('已删除'); load() } })
}

// 关联项目
const showLink = ref(false)
const linkContract = ref(null)
const linkProjectId = ref(null)
const linkPrimary = ref(false)
function openLink(r) { linkContract.value = r; linkProjectId.value = null; linkPrimary.value = false; showLink.value = true }
async function doLink() {
  try {
    await api.post(`/contracts/${linkContract.value.id}/link`, { projectId: linkProjectId.value, isPrimary: linkPrimary.value })
    message.success('已关联')
    showLink.value = false
    load()
  } catch (e) { message.error(errMsg(e)) }
}

const columns = [
  { title: '合同编号', key: 'no', width: 130, render: (r) => h('span', { class: 'num faint' }, r.no) },
  { title: '合同名称', key: 'name', ellipsis: { tooltip: true }, render: (r) => h('div', [h('div', { class: 'pname' }, r.name), h('div', { class: 'psub' }, r.partyA || '')]) },
  { title: '类型', key: 'type', width: 100, render: (r) => h(NTag, { size: 'small', bordered: false, type: r.type === '框架合同' ? 'info' : r.type === '补充协议' ? 'warning' : 'default' }, () => r.type) },
  { title: '合同金额（万元）', key: 'amount', width: 130, align: 'right', render: (r) => h('span', { class: 'num', style: 'font-weight:650' }, r.amount == null ? '按实结算' : fmt(r.amount)) },
  { title: '签订日期', key: 'signDate', width: 110, render: (r) => r.signDate || '—' },
  { title: '关联项目', key: 'projects', ellipsis: { tooltip: true }, render: (r) => r.projectCount ? h('span', { class: 'small' }, r.projects.join('、')) : h('span', { class: 'faint small' }, '未关联') },
  { title: '状态', key: 'status', width: 90, render: (r) => r.status || '—' },
  { title: '操作', key: 'act', width: 190, render: (r) => h('div', { style: 'display:flex;gap:6px' }, [
    h(NButton, { size: 'small', onClick: () => openLink(r) }, () => '关联项目'),
    h(NButton, { size: 'small', onClick: () => openEdit(r) }, () => '编辑'),
    h(NButton, { size: 'small', quaternary: true, onClick: () => del(r) }, () => '删除')
  ]) }
]
</script>

<template>
  <div>
    <div class="card" style="padding:14px 18px">
      <div style="display:flex;gap:10px;align-items:center;flex-wrap:wrap">
        <n-input v-model:value="q" placeholder="搜索合同名称 / 编号 / 甲方" style="width:280px" clearable @update:value="load" />
        <n-select v-model:value="type" :options="[{label:'全部类型',value:''},...TYPES.map(t=>({label:t,value:t}))]" style="width:140px" @update:value="load" />
        <div style="flex:1"></div>
        <span class="small faint">合同与项目为多对多：框架合同可挂多个项目，一个项目可挂主合同 + 补充协议</span>
        <n-button type="primary" @click="openCreate">+ 新建合同</n-button>
      </div>
    </div>
    <div class="card" style="padding:6px 10px">
      <n-data-table :columns="columns" :data="rows" :bordered="false" />
      <div v-if="!rows.length" class="faint" style="text-align:center;padding:26px">暂无合同</div>
    </div>

    <n-modal v-model:show="showForm" preset="card" :title="cf.id ? '编辑合同' : '新建合同'" style="width:560px;border-radius:16px">
      <n-form label-placement="top">
        <n-grid :cols="2" :x-gap="12">
          <n-form-item-gi label="合同编号 *"><n-input v-model:value="cf.no" placeholder="如 HT-2026-001" /></n-form-item-gi>
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
    </n-modal>

    <n-modal v-model:show="showLink" preset="card" title="关联到项目" style="width:480px;border-radius:16px">
      <n-form label-placement="top">
        <n-form-item :label="'合同：' + (linkContract?.name || '')">
          <n-select v-model:value="linkProjectId" filterable :options="projects.map(p=>({label:p.code + ' · ' + p.name, value:p.id}))" placeholder="选择项目" />
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
  </div>
</template>
