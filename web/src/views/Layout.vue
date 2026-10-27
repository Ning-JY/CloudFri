<script setup>
import { ref, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { NButton, NDropdown, useMessage } from 'naive-ui'
import { useAuthStore } from '../stores/auth'
import { useDicts } from '../stores/dicts'
import { CHANGELOG } from '../data/changelog'
import api, { errMsg } from '../api'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
useDicts().load() // 全站字典预加载（各页下拉统一读字典 store）
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
    { name: 'stats', label: '统计分析', path: '/stats', icon: '<path d="M4 20V10M10 20V4M16 20v-8M21 20H3"/>' }
  ] },
  { group: '系统', items: [
    { name: 'settings', label: '基础设置', path: '/settings', icon: '<circle cx="12" cy="12" r="3.2"/><path d="M19 12a7 7 0 0 0-.14-1.4l2-1.55-2-3.46-2.36.95a7 7 0 0 0-2.42-1.4L13.7 2.6h-3.4l-.38 2.54a7 7 0 0 0-2.42 1.4l-2.36-.95-2 3.46 2 1.55a7 7 0 0 0 0 2.8l-2 1.55 2 3.46 2.36-.95a7 7 0 0 0 2.42 1.4l.38 2.54h3.4l.38-2.54a7 7 0 0 0 2.42-1.4l2.36.95 2-3.46-2-1.55c.1-.45.14-.92.14-1.4z"/>' }
  ] }
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
        <template v-for="g in NAV" :key="g.group || 'root'">
          <div v-if="g.group" class="nav-t">{{ g.group }}</div>
          <a v-for="it in g.items" :key="it.name" class="nav-a" :class="{ on: activeName === it.name }"
             @click="router.push(it.path)">
            <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="flex:none" v-html="it.icon"></svg>
            <span class="nav-label">{{ it.label }}</span>
          </a>
        </template>
      </nav>
      <div class="side-foot">
        <div class="side-ver"><span class="cl ver-link" @click="showLog = true" title="查看历史版本更新记录">造价管理系统 v1.1</span></div>
        <button class="collapse-btn" @click="collapsed = !collapsed" :title="collapsed ? '展开侧边栏' : '收起侧边栏'">
          <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path :d="collapsed ? 'M9.5 5.5L16 12l-6.5 6.5' : 'M14.5 5.5L8 12l6.5 6.5'" /></svg>
          <span class="cl">{{ collapsed ? '' : '收起侧边栏' }}</span>
        </button>
      </div>
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
        <h1>{{ title }}</h1>
        <div style="flex:1"></div>
        <span class="faint small">{{ auth.displayName }} · {{ auth.isAdmin ? '管理员' : '成员' }}</span>
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
