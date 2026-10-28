<script setup>
import { NConfigProvider, NMessageProvider, NDialogProvider, zhCN, dateZhCN, darkTheme } from 'naive-ui'
import { useThemeStore } from './stores/theme'

const theme = useThemeStore()
theme.init() // 挂载前先把深色类落到 body，避免首屏闪烁

const themeOverrides = {
  common: {
    primaryColor: '#186140',
    primaryColorHover: '#1f7a52',
    primaryColorPressed: '#0f4a30',
    primaryColorSuppl: '#186140',
    borderRadius: '10px',
    fontFamily: '"PingFang SC","Microsoft YaHei",system-ui,-apple-system,"Segoe UI",sans-serif'
  },
  Card: { borderRadius: '16px' },
  Button: { borderRadiusMedium: '10px' }
}
</script>

<template>
  <NConfigProvider :locale="zhCN" :date-locale="dateZhCN" :theme="theme.dark ? darkTheme : null" :theme-overrides="themeOverrides">
    <NMessageProvider>
      <NDialogProvider>
        <router-view />
      </NDialogProvider>
    </NMessageProvider>
  </NConfigProvider>
</template>
