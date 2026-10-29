<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { NButton, NDropdown, useMessage } from 'naive-ui'
import { useAuthStore } from '../stores/auth'
import { useThemeStore } from '../stores/theme'
import { useDicts } from '../stores/dicts'
import { CHANGELOG } from '../data/changelog'
import api, { errMsg } from '../api'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const theme = useThemeStore()
useDicts().load() // 全站字典预加载（各页下拉统一读字典 store）
onMounted(() => auth.fetchProfile()) // 拉个人资料：左下角头像用实际头像
const message = useMessage()
const collapsed = ref(false)

// 图标与样稿一致：内联 SVG（24 视窗、描边风格）
const NAV = [
  { group: '工作', items: [
    { name: 'home', label: '工作台', path: '/', icon: '<rect x="3" y="3" width="7.5" height="9" rx="1.5"/><rect x="13.5" y="3" width="7.5" height="5.5" rx="1.5"/><rect x="13.5" y="12" width="7.5" height="9" rx="1.5"/><rect x="3" y="15.5" width="7.5" height="5.5" rx="1.5"/>' },
    { name: 'projects', label: '项目台账', path: '/projects', icon: '<path d="M4 6h16M4 12h16M4 18h10"/>' },
    { name: 'contracts', label: '合同台账', path: '/contracts', icon: '<path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z"/><path d="M14 3v5h5M9 13h6M9 17h4"/>' },
    { name: 'fees', label: '收费台账', path: '/fees', icon: '<circle cx="12" cy="12" r="9"/><path d="M12 7v10M15 9.2c-.6-1-1.7-1.4-3-1.4-1.7 0-3 .9-3 2.2 0 2.9 6 1.5 6 4.1 0 1.3-1.3 2.1-3 2.1-1.4 0-2.6-.6-3.2-1.6"/>' }
  ] },
  { group: '办公', items: [
    { name: 'forms', label: '流程模板', path: '/forms', icon: '<path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z"/><path d="M14 3v5h5"/><path d="M9 13h6M9 17h4"/>' },
    { name: 'library', label: '资料库', path: '/library', icon: '<path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20V3H6.5A2.5 2.5 0 0 0 4 5.5z"/><path d="M4 19.5A2.5 2.5 0 0 0 6.5 22H20v-5"/>' }
  ] },
  { group: '分析', items: [
    { name: 'stats', label: '统计分析', path: '/stats', icon: '<path d="M4 20V10M10 20V4M16 20v-8M21 20H3"/>' },
    { name: 'fee-calc', label: '收费测算', path: '/fee-calc', icon: '<rect x="4" y="2" width="16" height="20" rx="2"/><line x1="8" y1="6" x2="16" y2="6"/><line x1="8" y1="11" x2="8.01" y2="11"/><line x1="12" y1="11" x2="12.01" y2="11"/><line x1="16" y1="11" x2="16.01" y2="11"/><line x1="8" y1="15" x2="8.01" y2="15"/><line x1="12" y1="15" x2="12.01" y2="15"/><line x1="16" y1="15" x2="16.01" y2="15"/>' }
  ] },
  { group: '系统', items: [
    { name: 'settings', label: '基础设置', path: '/settings', adminOnly: true, icon: '<circle cx="12" cy="12" r="3.2"/><path d="M19 12a7 7 0 0 0-.14-1.4l2-1.55-2-3.46-2.36.95a7 7 0 0 0-2.42-1.4L13.7 2.6h-3.4l-.38 2.54a7 7 0 0 0-2.42 1.4l-2.36-.95-2 3.46 2 1.55a7 7 0 0 0 0 2.8l-2 1.55 2 3.46 2.36-.95a7 7 0 0 0 2.42 1.4l.38 2.54h3.4l.38-2.54a7 7 0 0 0 2.42-1.4l2.36.95 2-3.46-2-1.55c.1-.45.14-.92.14-1.4z"/>' }
  ] }
]

const activeName = computed(() => {
  if (route.name === 'project-detail' || route.name === 'gcfx') return 'projects'
  return route.name
})
// 基础设置仅管理员：非管理员不渲染该菜单（路由层另有拦截）
const navGroups = computed(() => NAV
  .map(g => ({ ...g, items: g.items.filter(i => !i.adminOnly || auth.isAdmin) }))
  .filter(g => g.items.length))
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

const userOptions = computed(() => auth.guest
  ? [
      { label: '个人中心', key: 'profile' },
      { label: '退出访客模式', key: 'logout' }
    ]
  : [
      { label: '个人中心', key: 'profile' },
      { label: '修改密码', key: 'pwd' },
      { label: '退出登录', key: 'logout' }
    ])
function onUserSelect(key) {
  if (key === 'profile') router.push('/profile')
  else if (key === 'pwd') showPwd.value = true
  else logout()
}

// 历史版本更新记录（点侧边栏版本号打开；数据在 data/changelog.js，最新在最上面）
const showLog = ref(false)
</script>

<template>
  <div class="app-shell">
    <aside class="side" :class="{ collapsed }">
      <div class="brand">
        <div class="brand-logo"><img src="/logo.png" alt="宜宾规划设计咨询集团" /></div>
        <div class="brand-t"><b>造价管理系统</b><span>宜宾规划设计咨询集团</span></div>
      </div>
      <nav>
        <template v-for="g in navGroups" :key="g.group || 'root'">
          <div v-if="g.group" class="nav-t">{{ g.group }}</div>
          <a v-for="it in g.items" :key="it.name" class="nav-a" :class="{ on: activeName === it.name }"
             @click="router.push(it.path)">
            <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="flex:none" v-html="it.icon"></svg>
            <span class="nav-label">{{ it.label }}</span>
          </a>
        </template>
      </nav>
      <div class="side-foot">
        <div class="side-ver"><span class="cl ver-link" @click="showLog = true" title="查看历史版本更新记录">造价管理系统 v1.3</span></div>
        <button class="collapse-btn" @click="collapsed = !collapsed" :title="collapsed ? '展开侧边栏' : '收起侧边栏'">
          <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path :d="collapsed ? 'M9.5 5.5L16 12l-6.5 6.5' : 'M14.5 5.5L8 12l6.5 6.5'" /></svg>
          <span class="cl">{{ collapsed ? '' : '收起侧边栏' }}</span>
        </button>
      </div>
      <div class="side-user" @click="router.push('/profile')" title="个人中心" style="cursor:pointer">
        <img :src="auth.avatarUrl" alt="" />
        <div class="user-meta">
          <b>{{ auth.displayName }}</b>
          <span>{{ auth.isAdmin ? '管理员' : '成员' }}</span>
        </div>
        <NDropdown :options="userOptions" @select="onUserSelect" trigger="click" @click.stop>
          <NButton quaternary circle size="small" style="margin-left:auto" @click.stop>···</NButton>
        </NDropdown>
      </div>
    </aside>

    <div class="main">
      <div class="topbar">
        <h1>{{ title }}</h1>
        <div style="flex:1"></div>
        <button class="theme-btn" @click="theme.toggle()" :title="theme.dark ? '切换到浅色模式' : '切换到深色模式'">
          <svg v-if="theme.dark" viewBox="0 0 24 24" width="17" height="17" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4"/></svg>
          <svg v-else viewBox="0 0 24 24" width="17" height="17" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8z"/></svg>
        </button>
        <n-tag v-if="auth.guest" type="warning" size="small" :bordered="false">访客模式 · 数据不保存</n-tag>
        <span class="faint small">{{ auth.displayName }} · {{ auth.guest ? '访客' : (auth.isAdmin ? '管理员' : '成员') }}</span>
      </div>
      <router-view />
    </div>

    <n-modal v-model:show="showLog" preset="card" title="历史版本更新记录" style="width:620px;max-width:94vw;border-radius:16px">
      <div style="max-height:62vh;overflow:auto;padding-right:4px">
        <div v-for="(e, i) in CHANGELOG" :key="e.version" :style="i ? 'margin-top:20px;padding-top:16px;border-top:1px solid var(--line-soft)' : ''">
          <div style="display:flex;align-items:center;gap:8px;flex-wrap:wrap">
            <n-tag size="small" type="success" :bordered="false"><b>{{ e.version }}</b></n-tag>
            <b style="font-size:14px">{{ e.title }}</b>
            <span class="faint small num">{{ e.date }}</span>
            <n-tag v-if="i === 0" size="tiny" type="warning" :bordered="false">当前版本</n-tag>
          </div>
          <ul style="margin:8px 0 0;padding-left:18px;line-height:1.85;font-size:13px;color:var(--sub)">
            <li v-for="it in e.items" :key="it">{{ it }}</li>
          </ul>
        </div>
      </div>
      <template #footer>
        <div style="display:flex;justify-content:flex-end"><n-button @click="showLog = false">关闭</n-button></div>
      </template>
    </n-modal>

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
