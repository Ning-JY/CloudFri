import axios from 'axios'
import { isGuest, guestAdapter } from './guest'

const api = axios.create({ baseURL: '/api', timeout: 30000 })

// 访客模式：全部请求走浏览器内模拟层（guest.js），不发网络请求、不碰真实数据
const defaultAdapter = api.defaults.adapter
export function applyGuestAdapter() {
  api.defaults.adapter = isGuest() ? guestAdapter : defaultAdapter
}
applyGuestAdapter()

api.interceptors.request.use((config) => {
  const token = localStorage.getItem('cloudfri_token')
  if (token) config.headers.Authorization = 'Bearer ' + token
  return config
})

api.interceptors.response.use(
  (res) => res,
  (err) => {
    if (err.response && err.response.status === 401) {
      localStorage.removeItem('cloudfri_token')
      localStorage.removeItem('cloudfri_user')
      if (location.pathname !== '/login') location.href = '/login'
    }
    return Promise.reject(err)
  }
)

export default api

export function errMsg(e, fallback = '操作失败') {
  return e?.response?.data?.message || e?.response?.data?.title || fallback
}
