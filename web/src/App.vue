<script setup>
import { NConfigProvider, NMessageProvider, NDialogProvider, zhCN, dateZhCN } from 'naive-ui'

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
  <NConfigProvider :locale="zhCN" :date-locale="dateZhCN" :theme-overrides="themeOverrides">
    <NMessageProvider>
      <NDialogProvider>
        <router-view />
      </NDialogProvider>
    </NMessageProvider>
  </NConfigProvider>
</template>
