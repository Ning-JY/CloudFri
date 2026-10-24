<script setup>
import { ref, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { NButton, NDropdown, useMessage } from 'naive-ui'
import { useAuthStore } from '../stores/auth'
import api, { errMsg } from '../api'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const message = useMessage()
const collapsed = ref(false)

const NAV = [
  { group: null, items: [{ name: 'home', label: '工作台', path: '/', icon: '▦' }] },
  { group: '台账', items: [
    { name: 'projects', label: '项目台账', path: '/projects', icon: '▤' },
    { name: 'contracts', label: '合同台账', path: '/contracts', icon: '▧' },
    { name: 'fees', label: '收费台账', path: '/fees', icon: '◉' }
  ] },
  { group: '办公', items: [
    { name: 'forms', label: '流程模板', path: '/forms', icon: '▨' },
    { name: 'library', label: '资料库', path: '/library', icon: '▩' }
  ] },
  { group: '分析', items: [{ name: 'stats', label: '统计分析', path: '/stats', icon: '◪' }] },
  { group: '系统', items: [{ name: 'settings', label: '基础设置', path: '/settings', icon: '⚙' }] }
]

const activeName = computed(() => {
  if (route.name === 'project-detail' || route.name === 'gcfx') return 'projects'
  return route.name
})
const title = computed(() => route.meta.title || '工作台')

const showPwd = ref(false)
const pwdForm = ref({ oldPassword: '', newPassword: '' })
async function changePwd() {
  try {
    await api.post('/auth/change-password', pwdForm.value)
    message.success('密码已修改')
    showPwd.value = false
    pwdForm.value = { oldPassword: '', newPassword: '' }
  } catch (e) { message.error(errMsg(e)) }
}
function logout() { auth.logout(); router.push('/login') }

const userOptions = [
  { label: '修改密码', key: 'pwd' },
  { label: '退出登录', key: 'logout' }
]
function onUserSelect(key) { if (key === 'pwd') showPwd.value = true; else logout() }
</script>

<template>
  <div class="app-shell">
    <aside class="side" :class="{ collapsed }">
      <div class="brand">
        <div class="brand-logo">C</div>
        <div class="brand-t"><b>CloudFri</b><span>造价咨询台账</span></div>
      </div>
      <nav>
        <template v-for="g in NAV" :key="g.group || 'root'">
          <div v-if="g.group" class="nav-t">{{ g.group }}</div>
          <a v-for="it in g.items" :key="it.name" class="nav-a" :class="{ on: activeName === it.name }"
             @click="router.push(it.path)">
            <span style="font-size:15px;width:18px;text-align:center">{{ it.icon }}</span>
            <span class="nav-label">{{ it.label }}</span>
          </a>
        </template>
      </nav>
      <div class="side-user">
        <img src="https://randomuser.me/api/portraits/men/52.jpg" alt="" />
        <div class="user-meta">
          <b>{{ auth.displayName }}</b>
          <span>{{ auth.isAdmin ? '管理员' : '成员' }}</span>
        </div>
        <NDropdown :options="userOptions" @select="onUserSelect" trigger="click">
          <NButton quaternary circle size="small" style="margin-left:auto">···</NButton>
        </NDropdown>
      </div>
    </aside>

    <div class="main">
      <div class="topbar">
        <NButton quaternary circle @click="collapsed = !collapsed">{{ collapsed ? '»' : '«' }}</NButton>
        <h1>{{ title }}</h1>
        <div style="flex:1"></div>
        <span class="faint small">{{ auth.displayName }} · {{ auth.isAdmin ? '管理员' : '成员' }}</span>
      </div>
      <router-view />
    </div>

    <n-modal v-model:show="showPwd" preset="card" title="修改密码" style="width:420px;border-radius:16px">
      <n-form>
        <n-form-item label="原密码"><n-input v-model:value="pwdForm.oldPassword" type="password" show-password-on="click" /></n-form-item>
        <n-form-item label="新密码"><n-input v-model:value="pwdForm.newPassword" type="password" show-password-on="click" placeholder="至少 6 位" /></n-form-item>
      </n-form>
      <template #footer>
        <div style="display:flex;justify-content:flex-end;gap:10px">
          <n-button @click="showPwd = false">取消</n-button>
          <n-button type="primary" @click="changePwd">确认修改</n-button>
        </div>
      </template>
    </n-modal>
  </div>
</template>
