<script setup>
import { ref, computed, onMounted, h } from 'vue'
import { NButton, NTag, useMessage, useDialog } from 'naive-ui'
import api, { errMsg } from '../api'
import { avatarFor, iconBtn, AVATAR_PRESETS } from '../utils'
import { useAuthStore } from '../stores/auth'
import { useDicts } from '../stores/dicts'
import TableTools from '../components/TableTools.vue'
import Pager from '../components/Pager.vue'
import { usePager } from '../composables/usePager'

const pagerMembers = usePager()
const pagerUsers = usePager()
const pagedMembers = computed(() => pagerMembers.slice(members.value))
const pagedUsers = computed(() => pagerUsers.slice(users.value))

const message = useMessage()
const dialog = useDialog()
const auth = useAuthStore()
const tab = ref('members')
const members = ref([])
// 字典直接用全站 store：这里的增删/排序保存后刷新 store，全站下拉立即生效
const dictStore = useDicts()
const users = ref([])

async function loadAll() {
  const m = await api.get('/members?includeInactive=true')
  members.value = m.data
  await dictStore.load(true)
  if (auth.isAdmin) {
    const u = await api.get('/users')
    users.value = u.data
  }
}
onMounted(loadAll)

// 成员
const showMember = ref(false)
const mf = ref({})
const avatarTab = ref('M')
const DEPARTMENTS = ['工程咨询事业部', '协作人员']
function openMember(m) {
  mf.value = m ? { ...m } : { name: '', gender: 'M', phone: '', department: '工程咨询事业部', remark: '', avatarPath: null, sort: members.value.length + 1 }
  avatarTab.value = mf.value.gender === 'F' ? 'F' : 'M'
  showMember.value = true
}
async function saveMember() {
  try {
    if (mf.value.id) await api.put('/members/' + mf.value.id, mf.value)
    else await api.post('/members', mf.value)
    message.success('已保存'); showMember.value = false; loadAll()
  } catch (e) { message.error(errMsg(e)) }
}
async function toggleMember(m) {
  const { data } = await api.post(`/members/${m.id}/toggle-active`)
  message.success(data.isActive ? '已启用' : '已停用'); loadAll()
}

// 字典
const GROUP_NAMES = { BizType: '业务类型', ProjectStatus: '项目状态', ContractType: '合同类型', TeamRole: '团队角色', DocCategory: '项目资料分类', LibCategory: '资料库分类' }
const showDict = ref(false)
const df = ref({})
function openDict(group) { df.value = { group, name: '', sort: dictStore.rows(group).length + 1 }; showDict.value = true }
async function saveDict() {
  try {
    await api.post('/dicts', df.value)
    message.success('已添加'); showDict.value = false; await dictStore.load(true)
  } catch (e) { message.error(errMsg(e)) }
}
async function delDict(d) {
  await api.delete('/dicts/' + d.id)
  message.success('已停用'); await dictStore.load(true)
}
// 拖动排序：拖到目标行放下 → 按新顺序调 reorder 保存（Sort 重写为 1..n，全站下拉顺序随之变）
const dragCtx = ref(null)
function dragStart(g, i) { dragCtx.value = { g, i } }
async function dragDrop(g, i) {
  if (!dragCtx.value || dragCtx.value.g !== g || dragCtx.value.i === i) { dragCtx.value = null; return }
  const list = [...dictStore.rows(g)]
  const [moved] = list.splice(dragCtx.value.i, 1)
  list.splice(i, 0, moved)
  dragCtx.value = null
  try {
    await api.post('/dicts/reorder', { group: g, ids: list.map(x => x.id) })
    await dictStore.load(true)
    message.success('顺序已保存')
  } catch (e) { message.error(errMsg(e)) }
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

const MEMBER_COLUMNS = [
  { title: '头像', key: 'avatar', width: 70, exportValue: () => '', render: (r) => h('img', { src: avatarFor(r), style: 'width:36px;height:36px;border-radius:50%;object-fit:cover' }) },
  { title: '姓名', key: 'name', render: (r) => h('b', r.name) },
  { title: '性别', key: 'gender', width: 70, exportValue: (r) => r.gender === 'F' ? '女' : '男', render: (r) => r.gender === 'F' ? '女' : '男' },
  { title: '电话', key: 'phone', render: (r) => r.phone || '—' },
  { title: '部门', key: 'department', width: 130, render: (r) => r.department ? h(NTag, { size: 'small', bordered: false, type: r.department === '协作人员' ? 'warning' : 'info' }, () => r.department) : '—' },
  { title: '备注', key: 'remark', ellipsis: { tooltip: true }, render: (r) => r.remark || '—' },
  { title: '状态', key: 'isActive', width: 90, exportValue: (r) => r.isActive ? '在职' : '已停用', render: (r) => h(NTag, { size: 'small', type: r.isActive ? 'success' : 'default', bordered: false }, () => r.isActive ? '在职' : '已停用') },
  ...(auth.isAdmin ? [{ title: '操作', key: 'act', width: 110, exportValue: () => '', render: (r) => h('div', { style: 'display:flex' }, [
    iconBtn('pencil', 'green', '编辑', () => openMember(r)),
    iconBtn('power', r.isActive ? 'orange' : 'green', r.isActive ? '停用' : '启用', () => toggleMember(r))
  ]) }] : [])
]
const memberVisibleKeys = ref(MEMBER_COLUMNS.map(c => c.key))
const memberColumns = computed(() => MEMBER_COLUMNS.filter(c => memberVisibleKeys.value.includes(c.key)))
const USER_COLUMNS = [
  { title: '用户名', key: 'userName' },
  { title: '显示名', key: 'displayName' },
  { title: '角色', key: 'role', width: 100, exportValue: (r) => r.role === 'Admin' ? '管理员' : '成员', render: (r) => h(NTag, { size: 'small', bordered: false, type: r.role === 'Admin' ? 'success' : 'default' }, () => r.role === 'Admin' ? '管理员' : '成员') },
  { title: '状态', key: 'isActive', width: 90, exportValue: (r) => r.isActive ? '启用' : '停用', render: (r) => r.isActive ? '启用' : '停用' },
  { title: '最后登录', key: 'lastLoginAt', width: 180, exportValue: (r) => r.lastLoginAt ? String(r.lastLoginAt).replace('T', ' ').slice(0, 16) + (r.lastLoginIp ? '（' + r.lastLoginIp + '）' : '') : '从未登录', render: (r) => r.lastLoginAt
      ? h('div', [h('div', { class: 'num mut' }, String(r.lastLoginAt).replace('T', ' ').slice(0, 16)), h('div', { class: 'psub' }, r.lastLoginIp || '')])
      : h('span', { class: 'faint' }, '从未登录') },
  { title: '操作', key: 'act', width: 110, exportValue: () => '', render: (r) => h('div', { style: 'display:flex' }, [
    iconBtn('key', 'violet', '重置密码', () => resetPwd(r)),
    iconBtn('power', r.isActive ? 'orange' : 'green', r.isActive ? '停用' : '启用', () => toggleUser(r))
  ]) }
]
const userVisibleKeys = ref(USER_COLUMNS.map(c => c.key))
const userColumns = computed(() => USER_COLUMNS.filter(c => userVisibleKeys.value.includes(c.key)))
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
        <TableTools v-model="memberVisibleKeys" :cols="MEMBER_COLUMNS" :rows="members" filename="成员名单" storage-key="members" />
        <n-button v-if="auth.isAdmin" type="primary" size="small" @click="openMember(null)">+ 新增成员</n-button></div>
      <n-data-table :columns="memberColumns" :data="pagedMembers" :bordered="false" :pagination="false" />
      <Pager v-model:page="pagerMembers.page.value" v-model:page-size="pagerMembers.pageSize.value" :total="members.length" />
    </div>

    <div class="card" v-if="tab === 'dicts'">
      <div class="card-h"><h3>字典维护</h3><div class="sp"></div><span class="small faint">下拉选项的顺序 = 拖动后的顺序，保存后全站生效</span></div>
      <div style="display:grid;grid-template-columns:repeat(auto-fill,minmax(300px,1fr));gap:16px">
        <div v-for="(label, g) in GROUP_NAMES" :key="g" style="border:1px solid var(--line-soft);border-radius:12px;padding:12px 14px">
          <div style="display:flex;align-items:center;margin-bottom:8px"><b style="font-size:13.5px">{{ label }}</b><div style="flex:1"></div>
            <n-button v-if="auth.isAdmin" size="tiny" @click="openDict(g)">+ 添加</n-button></div>
          <div>
            <div v-for="(d, i) in dictStore.rows(g)" :key="d.id" class="dict-row" :draggable="auth.isAdmin"
              @dragstart="dragStart(g, i)" @dragover.prevent @drop="dragDrop(g, i)">
              <svg class="grip" viewBox="0 0 24 24" fill="currentColor"><circle cx="9" cy="6" r="1.7"/><circle cx="15" cy="6" r="1.7"/><circle cx="9" cy="12" r="1.7"/><circle cx="15" cy="12" r="1.7"/><circle cx="9" cy="18" r="1.7"/><circle cx="15" cy="18" r="1.7"/></svg>
              <span style="flex:1">{{ d.name }}</span>
              <button v-if="auth.isAdmin" class="icbtn b-red" title="停用" @click="delDict(d)"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/></svg></button>
            </div>
            <span v-if="!dictStore.rows(g).length" class="faint small">无</span>
          </div>
        </div>
      </div>
    </div>

    <div class="card" v-if="tab === 'users' && auth.isAdmin">
      <div class="card-h"><h3>账号管理</h3><div class="sp"></div>
        <TableTools v-model="userVisibleKeys" :cols="USER_COLUMNS" :rows="users" filename="账号管理" storage-key="users" />
        <n-button type="primary" size="small" @click="openUser">+ 新建账号</n-button></div>
      <n-data-table :columns="userColumns" :data="pagedUsers" :bordered="false" :pagination="false" />
      <Pager v-model:page="pagerUsers.page.value" v-model:page-size="pagerUsers.pageSize.value" :total="users.length" />
    </div>

    <n-modal v-model:show="showMember" preset="card" :title="mf.id ? '编辑成员' : '新增成员'" style="width:560px;border-radius:16px">
      <n-form label-placement="top">
        <n-form-item label="姓名"><n-input v-model:value="mf.name" /></n-form-item>
        <n-grid :cols="2" :x-gap="12">
          <n-form-item-gi label="性别"><n-select v-model:value="mf.gender" :options="[{label:'男',value:'M'},{label:'女',value:'F'}]" @update:value="(v) => { avatarTab = v }" /></n-form-item-gi>
          <n-form-item-gi label="排序"><n-input-number v-model:value="mf.sort" style="width:100%" /></n-form-item-gi>
        </n-grid>
        <n-form-item label="头像">
          <div style="width:100%">
            <div style="display:flex;align-items:center;gap:12px;margin-bottom:10px">
              <img :src="avatarFor({ id: mf.id ?? 0, gender: mf.gender, avatarPath: mf.avatarPath })" style="width:52px;height:52px;border-radius:50%;object-fit:cover;border:2px solid var(--line-soft)" />
              <div>
                <div class="small">{{ mf.avatarPath ? '已选择预设头像' : '默认头像（按名单顺序自动分配）' }}</div>
                <n-button size="tiny" quaternary @click="mf.avatarPath = null">恢复默认</n-button>
              </div>
              <div style="flex:1"></div>
              <n-tabs v-model:value="avatarTab" type="segment" size="small" style="width:150px">
                <n-tab name="M">男</n-tab>
                <n-tab name="F">女</n-tab>
              </n-tabs>
            </div>
            <div class="avatar-grid">
              <img v-for="url in AVATAR_PRESETS[avatarTab]" :key="url" :src="url" loading="lazy"
                :class="{ on: mf.avatarPath === url }" @click="mf.avatarPath = url" />
            </div>
          </div>
        </n-form-item>
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

<style scoped>
.avatar-grid { display: grid; grid-template-columns: repeat(auto-fill, 44px); gap: 8px; max-height: 168px; overflow-y: auto; padding: 2px; }
.avatar-grid img { width: 44px; height: 44px; border-radius: 50%; object-fit: cover; cursor: pointer; border: 2px solid transparent; transition: transform .1s; }
.avatar-grid img:hover { transform: scale(1.08); }
.avatar-grid img.on { border-color: var(--pri); box-shadow: 0 0 0 3px rgba(24, 97, 64, .18); }
.dict-row { display: flex; align-items: center; gap: 8px; padding: 5px 8px; border-radius: 8px; font-size: 13px; }
.dict-row:hover { background: #f2f6f2; }
.dict-row .grip { width: 15px; height: 15px; color: #b3beb5; cursor: grab; flex: 0 0 15px; }
</style>
