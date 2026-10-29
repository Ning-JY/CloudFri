<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { NForm, NFormItem, NInput, NButton, useMessage } from 'naive-ui'
import { useAuthStore } from '../stores/auth'
import { errMsg } from '../api'

const router = useRouter()
const auth = useAuthStore()
const message = useMessage()
const form = ref({ userName: '', password: '' })
const loading = ref(false)

function enterGuest() {
  auth.enterGuest()
  message.success('已进入访客模式：可自由体验，添加的数据不会保存')
  router.push('/')
}

async function submit() {
  if (!form.value.userName || !form.value.password) { message.warning('请输入用户名和密码'); return }
  loading.value = true
  try {
    await auth.login(form.value.userName, form.value.password)
    message.success('登录成功')
    router.push('/')
  } catch (e) {
    message.error(errMsg(e, '登录失败'))
  } finally { loading.value = false }
}
</script>

<template>
  <div class="login-wrap">
    <div class="login-card">
      <div style="display:flex;align-items:center;gap:12px;margin-bottom:8px">
        <div class="brand-logo" style="width:46px;height:46px;border-radius:14px"><img src="/logo.png" alt="宜宾规划设计咨询集团" /></div>
        <div>
          <div style="font-size:21px;font-weight:800;line-height:1.2">造价管理系统</div>
          <div class="faint small">宜宾规划设计咨询集团有限公司</div>
        </div>
      </div>
      <p class="mut small" style="margin:10px 0 22px">请使用公司账号登录。首次使用请联系管理员开通账号。</p>
      <NForm @submit.prevent="submit">
        <NFormItem label="用户名">
          <NInput v-model:value="form.userName" placeholder="请输入用户名" size="large" @keyup.enter="submit" />
        </NFormItem>
        <NFormItem label="密码">
          <NInput v-model:value="form.password" type="password" show-password-on="click" placeholder="请输入密码" size="large" @keyup.enter="submit" />
        </NFormItem>
        <NButton type="primary" size="large" block :loading="loading" @click="submit" style="margin-top:6px">登 录</NButton>
      </NForm>
      <div style="display:flex;align-items:center;gap:10px;margin:18px 0 12px">
        <span style="flex:1;height:1px;background:var(--line)"></span>
        <span class="faint" style="font-size:12px">或</span>
        <span style="flex:1;height:1px;background:var(--line)"></span>
      </div>
      <NButton size="large" block @click="enterGuest">访客模式进入（免登录 · 数据不保存）</NButton>
      <p class="faint" style="text-align:center;font-size:12px;margin:20px 0 0">造价管理系统 v1.2 · 宜宾规划设计咨询集团有限公司</p>
    </div>
  </div>
</template>
