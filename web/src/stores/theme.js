import { defineStore } from 'pinia'

// 深色/浅色模式：状态存 localStorage（cloudfri_theme），body.dark 驱动 style.css 的深色变量与组件深色样式，
// App.vue 的 NConfigProvider 据此切换 Naive 的 darkTheme。
export const useThemeStore = defineStore('theme', {
  state: () => ({ dark: localStorage.getItem('cloudfri_theme') === 'dark' }),
  actions: {
    apply() { document.body.classList.toggle('dark', this.dark) },
    init() { this.apply() },
    toggle() {
      this.dark = !this.dark
      localStorage.setItem('cloudfri_theme', this.dark ? 'dark' : 'light')
      this.apply()
    }
  }
})
