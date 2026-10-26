<script setup>
import { ref, onMounted, h } from 'vue'
import { NButton, NTag, useMessage, useDialog } from 'naive-ui'
import api, { errMsg } from '../api'
import { avatarFor, iconBtn } from '../utils'
import { useAuthStore } from '../stores/auth'

const message = useMessage()
const dialog = useDialog()
const auth = useAuthStore()
const tab = ref('members')
const members = ref([])
const dicts = ref([])
const users = ref([])

async function loadAll() {
  const [m, d] = await Promise.all([api.get('/members?includeInactive=true'), api.get('/dicts')])
  members.value = m.data
  dicts.value = d.data
  if (auth.isAdmin) {
    const u = await api.get('/users')
    users.value = u.data
  }
}
onMounted(loadAll)

// 成员
const showMember = ref(false)
const mf = ref({})
const DEPARTMENTS = ['工程咨询事业部', '协作人员']
function openMember(m) { mf.value = m ? { ...m } : { name: '', gender: 'M', phone: '', department: '工程咨询事业部', remark: '', sort: members.value.length + 1 }; showMember.value = true }
async function saveMember() {
  try {
    if (mf.value.id) await api.put('/members/' + mf.value.id, mf.value)
    else await api.post('/members', mf.value)
    message.success('已保存'); showMember.value = false; loadAll()
  } catch (e) { message.error(errMsg(e)) }
}
async function deactivateMember(m) {
  await api.delete('/members/' + m.id)
  message.success('已停用'); loadAll()
}

// 字典
const GROUP_NAMES = { BizType: '业务类型', ProjectStatus: '项目状态', ContractType: '合同类型', TeamRole: '团队角色', DocCategory: '项目资料分类', LibCategory: '资料库分类' }
const showDict = ref(false)
const df = ref({})
function openDict(group) { df.value = { group, name: '', sort: dicts.value.filter(x => x.group === group).length + 1 }; showDict.value = true }
async function saveDict() {
  try {
    await api.post('/dicts', df.value)
    message.success('已添加'); showDict.value = false; loadAll()
  } catch (e) { message.error(errMsg(e)) }
}
async function delDict(d) {
  await api.delete('/dicts/' + d.id)
  message.success('已停用'); loadAll()
}

// 用户（管理员）
const showUser = ref(false)
const uf = ref({})
function openUser() { uf.value = { userName: '', password: '', displayName: '', role: 'Member', memberId: null }; showUser.value = true }
async function saveUser() {
  try {
    await api.post('/users', uf.value)
    message.success('账号已创建'); showUser.value = false; loadAll()
  } catch (e) { message.error(errMsg(e)) }
}
async function resetPwd(u) {
  const pwd = 'cloudfri123'
  dialog.info({ title: '重置密码', content: `将 ${u.userName} 的密码重置为 ${pwd}（请提醒其登录后立即修改）？`, positiveText: '重置', negativeText: '取消',
    onPositiveClick: async () => { await api.post(`/users/${u.id}/reset-password`, { password: pwd }); message.success('已重置') } })
}
async function toggleUser(u) { await api.post(`/users/${u.id}/toggle-active`); loadAll() }

const memberColumns = [
  { title: '头像', key: 'avatar', width: 70, render: (r) => h('img', { src: avatarFor(r), style: 'width:36px;height:36px;border-radius:50%;object-fit:cover' }) },
  { title: '姓名', key: 'name', render: (r) => h('b', r.name) },
  { title: '性别', key: 'gender', width: 70, render: (r) => r.gender === 'F' ? '女' : '男' },
  { title: '电话', key: 'phone', render: (r) => r.phone || '—' },
  { title: '部门', key: 'department', width: 130, render: (r) => r.department ? h(NTag, { size: 'small', bordered: false, type: r.department === '协作人员' ? 'warning' : 'info' }, () => r.department) : '—' },
  { title: '备注', key: 'remark', ellipsis: { tooltip: true }, render: (r) => r.remark || '—' },
  { title: '状态', key: 'isActive', width: 90, render: (r) => h(NTag, { size: 'small', type: r.isActive ? 'success' : 'default', bordered: false }, () => r.isActive ? '在职' : '已停用') },
  ...(auth.isAdmin ? [{ title: '操作', key: 'act', width: 110, render: (r) => h('div', { style: 'display:flex' }, [
    iconBtn('pencil', 'green', '编辑', () => openMember(r)),
    ...(r.isActive ? [iconBtn('power', 'orange', '停用', () => deactivateMember(r))] : [])
  ]) }] : [])
]
const userColumns = [
  { title: '用户名', key: 'userName' },
  { title: '显示名', key: 'displayName' },
  { title: '角色', key: 'role', width: 100, render: (r) => h(NTag, { size: 'small', bordered: false, type: r.role === 'Admin' ? 'success' : 'default' }, () => r.role === 'Admin' ? '管理员' : '成员') },
  { title: '状态', key: 'isActive', width: 90, render: (r) => r.isActive ? '启用' : '停用' },
  { title: '操作', key: 'act', width: 110, render: (r) => h('div', { style: 'display:flex' }, [
    iconBtn('key', 'violet', '重置密码', () => resetPwd(r)),
    iconBtn('power', r.isActive ? 'orange' : 'green', r.isActive ? '停用' : '启用', () => toggleUser(r))
  ]) }
]
</script>

<template>
  <div>
    <div class="card" style="padding:10px 14px">
      <n-tabs v-model:value="tab" type="segment" style="max-width:520px">
        <n-tab name="members">成员名单</n-tab>
        <n-tab name="dicts">字典维护</n-tab>
        <n-tab name="users" v-if="auth.isAdmin">账号管理</n-tab>
      </n-tabs>
    </div>

    <div class="card" v-if="tab === 'members'">
      <div class="card-h"><h3>成员名单</h3><div class="sp"></div><span class="small faint">用于项目负责人与团队分工选择</span>
        <n-button v-if="auth.isAdmin" type="primary" size="small" @click="openMember(null)">+ 新增成员</n-button></div>
      <n-data-table :columns="memberColumns" :data="members" :bordered="false" />
    </div>

    <div class="card" v-if="tab === 'dicts'">
      <div class="card-h"><h3>字典维护</h3><div class="sp"></div><span class="small faint">业务类型、状态、分类等下拉选项</span></div>
      <div style="display:grid;grid-template-columns:repeat(auto-fill,minmax(300px,1fr));gap:16px">
        <div v-for="(label, g) in GROUP_NAMES" :key="g" style="border:1px solid var(--line-soft);border-radius:12px;padding:12px 14px">
          <div style="display:flex;align-items:center;margin-bottom:8px"><b style="font-size:13.5px">{{ label }}</b><div style="flex:1"></div>
            <n-button v-if="auth.isAdmin" size="tiny" @click="openDict(g)">+ 添加</n-button></div>
          <div style="display:flex;flex-wrap:wrap;gap:6px">
            <n-tag v-for="d in dicts.filter(x => x.group === g)" :key="d.id" :closable="auth.isAdmin" @close="delDict(d)">{{ d.name }}</n-tag>
            <span v-if="!dicts.filter(x => x.group === g).length" class="faint small">无</span>
          </div>
        </div>
      </div>
    </div>

    <div class="card" v-if="tab === 'users' && auth.isAdmin">
      <div class="card-h"><h3>账号管理</h3><div class="sp"></div><n-button type="primary" size="small" @click="openUser">+ 新建账号</n-button></div>
      <n-data-table :columns="userColumns" :data="users" :bordered="false" />
    </div>

    <n-modal v-model:show="showMember" preset="card" :title="mf.id ? '编辑成员' : '新增成员'" style="width:440px;border-radius:16px">
      <n-form label-placement="top">
        <n-form-item label="姓名"><n-input v-model:value="mf.name" /></n-form-item>
        <n-grid :cols="2" :x-gap="12">
          <n-form-item-gi label="性别"><n-select v-model:value="mf.gender" :options="[{label:'男',value:'M'},{label:'女',value:'F'}]" /></n-form-item-gi>
          <n-form-item-gi label="排序"><n-input-number v-model:value="mf.sort" style="width:100%" /></n-form-item-gi>
        </n-grid>
        <n-form-item label="电话"><n-input v-model:value="mf.phone" /></n-form-item>
        <n-form-item label="部门"><n-select v-model:value="mf.department" :options="DEPARTMENTS.map(t => ({ label: t, value: t }))" /></n-form-item>
        <n-form-item label="备注"><n-input v-model:value="mf.remark" placeholder="如：外聘专家 / 兼职配合等" /></n-form-item>
      </n-form>
      <template #footer><div style="display:flex;justify-content:flex-end;gap:10px">
        <n-button @click="showMember = false">取消</n-button><n-button type="primary" @click="saveMember">保存</n-button>
      </div></template>
    </n-modal>

    <n-modal v-model:show="showDict" preset="card" title="添加字典项" style="width:400px;border-radius:16px">
      <n-form label-placement="top">
        <n-form-item :label="GROUP_NAMES[df.group] || df.group"><n-input v-model:value="df.name" placeholder="名称" /></n-form-item>
      </n-form>
      <template #footer><div style="display:flex;justify-content:flex-end;gap:10px">
        <n-button @click="showDict = false">取消</n-button><n-button type="primary" @click="saveDict">添加</n-button>
      </div></template>
    </n-modal>

    <n-modal v-model:show="showUser" preset="card" title="新建账号" style="width:440px;border-radius:16px">
      <n-form label-placement="top">
        <n-form-item label="用户名"><n-input v-model:value="uf.userName" /></n-form-item>
        <n-form-item label="初始密码"><n-input v-model:value="uf.password" type="password" show-password-on="click" /></n-form-item>
        <n-grid :cols="2" :x-gap="12">
          <n-form-item-gi label="显示名"><n-input v-model:value="uf.displayName" /></n-form-item-gi>
          <n-form-item-gi label="角色"><n-select v-model:value="uf.role" :options="[{label:'成员',value:'Member'},{label:'管理员',value:'Admin'}]" /></n-form-item-gi>
        </n-grid>
        <n-form-item label="关联成员"><n-select v-model:value="uf.memberId" clearable :options="members.filter(m => m.isActive).map(m => ({ label: m.name, value: m.id }))" /></n-form-item>
      </n-form>
      <template #footer><div style="display:flex;justify-content:flex-end;gap:10px">
        <n-button @click="showUser = false">取消</n-button><n-button type="primary" @click="saveUser">创建</n-button>
      </div></template>
    </n-modal>
  </div>
</template>
