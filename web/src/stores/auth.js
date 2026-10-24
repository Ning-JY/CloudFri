import { defineStore } from 'pinia'
import api from '../api'

export const useAuthStore = defineStore('auth', {
  state: () => ({
    token: localStorage.getItem('cloudfri_token') || '',
    user: JSON.parse(localStorage.getItem('cloudfri_user') || 'null')
  }),
  getters: {
    isAdmin: (s) => s.user?.role === 'Admin',
    displayName: (s) => s.user?.displayName || s.user?.userName || ''
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
    logout() {
      this.token = ''
      this.user = null
      localStorage.removeItem('cloudfri_token')
      localStorage.removeItem('cloudfri_user')
    }
  }
})
