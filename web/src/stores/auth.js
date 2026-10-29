import { defineStore } from 'pinia'
import api, { applyGuestAdapter } from '../api'
import { avatarFor } from '../utils'
import { GUEST_KEY, isGuest, resetGuestDb } from '../guest'

export const useAuthStore = defineStore('auth', {
  state: () => ({
    token: localStorage.getItem('cloudfri_token') || '',
    user: JSON.parse(localStorage.getItem('cloudfri_user') || 'null')
      || (isGuest() ? { id: 0, userName: 'guest', displayName: '访客', role: 'Member' } : null),
    profile: null, // 个人中心详情（GET /auth/profile）：含关联成员信息，左下角头像与个人中心共用
    guest: isGuest() // 访客模式（免登录演示）：数据只存浏览器内存，不落库
  }),
  getters: {
    isAdmin: (s) => s.user?.role === 'Admin',
    displayName: (s) => s.user?.displayName || s.user?.userName || '',
    // 实际头像：优先关联成员的头像（与成员名单/参与人员一致），未关联成员时按账号 id 取默认头像
    avatarUrl: (s) => {
      const m = s.profile?.member
      if (m) return avatarFor(m)
      return avatarFor({ id: s.user?.id || 1, gender: 'M' })
    }
  },
  actions: {
    async login(userName, password) {
      const { data } = await api.post('/auth/login', { userName, password })
      this.token = data.token
      this.user = data.user
      localStorage.setItem('cloudfri_token', data.token)
      localStorage.setItem('cloudfri_user', JSON.stringify(data.user))
      return data.user
    },
    async fetchProfile() {
      try {
        const { data } = await api.get('/auth/profile')
        this.applyProfile(data)
        return data
      } catch { return null }
    },
    // 保存个人资料后同步：profile + 顶部/左下角显示的 displayName
    applyProfile(data) {
      this.profile = data
      if (data && this.user && data.displayName) {
        this.user = { ...this.user, displayName: data.displayName }
        localStorage.setItem('cloudfri_user', JSON.stringify(this.user))
      }
    },
    // 进入访客模式：不登录、不加载真实数据，操作只存内存（guest.js 拦截全部接口）
    enterGuest() {
      sessionStorage.setItem(GUEST_KEY, '1')
      resetGuestDb()
      this.guest = true
      this.token = ''
      this.user = { id: 0, userName: 'guest', displayName: '访客', role: 'Member' }
      this.profile = null
      applyGuestAdapter()
    },
    exitGuest() {
      sessionStorage.removeItem(GUEST_KEY)
      this.guest = false
      this.user = null
      this.profile = null
      resetGuestDb()
      applyGuestAdapter()
    },
    logout() {
      if (this.guest) { this.exitGuest(); return }
      this.token = ''
      this.user = null
      this.profile = null
      localStorage.removeItem('cloudfri_token')
      localStorage.removeItem('cloudfri_user')
    }
  }
})
