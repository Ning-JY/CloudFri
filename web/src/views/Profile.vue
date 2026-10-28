<script setup>
import { ref, computed, onMounted } from 'vue'
import { useMessage } from 'naive-ui'
import api, { errMsg } from '../api'
import { useAuthStore } from '../stores/auth'
import { avatarFor, AVATAR_PRESETS } from '../utils'

const auth = useAuthStore()
const message = useMessage()
const profile = ref(null)
const member = computed(() => profile.value?.member || null)

const form = ref({ displayName: '', phone: '', remark: '', avatarPath: null })
const avatarTab = ref('M')
const previewAvatar = computed(() => member.value
  ? avatarFor({ id: member.value.id, gender: member.value.gender, avatarPath: form.value.avatarPath })
  : auth.avatarUrl)

async function load() {
  const data = await auth.fetchProfile()
  if (!data) return
  profile.value = data
  form.value = {
    displayName: data.displayName || '',
    phone: data.member?.phone || '',
    remark: data.member?.remark || '',
    avatarPath: data.member?.avatarPath || null
  }
  avatarTab.value = data.member?.gender || 'M'
}
async function save() {
  try {
    const { data } = await api.put('/auth/profile', form.value)
    auth.applyProfile(data)
    profile.value = data
    message.success('已保存')
  } catch (e) { message.error(errMsg(e)) }
}

const pwd = ref({ oldPassword: '', newPassword: '', confirm: '' })
async function changePwd() {
  if (!pwd.value.oldPassword || !pwd.value.newPassword) { message.warning('请填写原密码和新密码'); return }
  if (pwd.value.newPassword !== pwd.value.confirm) { message.warning('两次输入的新密码不一致'); return }
  try {
    await api.post('/auth/change-password', { oldPassword: pwd.value.oldPassword, newPassword: pwd.value.newPassword })
    message.success('密码已修改')
    pwd.value = { oldPassword: '', newPassword: '', confirm: '' }
  } catch (e) { message.error(errMsg(e)) }
}

function fmtTime(s) { return s ? String(s).replace('T', ' ').slice(0, 19) : '—' }
onMounted(load)
</script>

<template>
  <div style="display:grid;grid-template-columns:5fr 7fr;gap:14px;align-items:start">
    <!-- 详细信息 -->
    <div class="card">
      <div class="card-h"><h3>个人信息</h3></div>
      <div style="display:flex;align-items:center;gap:14px;margin-bottom:16px">
        <img :src="auth.avatarUrl" style="width:64px;height:64px;border-radius:50%;object-fit:cover;border:2px solid var(--line-soft)" alt="" />
        <div>
          <div style="font-size:16px;font-weight:600">{{ profile?.displayName || '—' }}</div>
          <div class="small faint" style="margin-top:3px">{{ profile?.userName }} · {{ profile?.role === 'Admin' ? '管理员' : '成员' }}</div>
        </div>
      </div>
      <div class="prow"><span>用户名</span><b>{{ profile?.userName || '—' }}</b></div>
      <div class="prow"><span>角色</span><b>{{ profile?.role === 'Admin' ? '管理员' : '成员' }}</b></div>
      <div class="prow"><span>账号创建时间</span><b class="num">{{ fmtTime(profile?.createdAt) }}</b></div>
      <div class="prow"><span>最后登录</span><b class="num">{{ fmtTime(profile?.lastLoginAt) }}</b></div>
      <div class="prow"><span>最后登录 IP</span><b class="num">{{ profile?.lastLoginIp || '—' }}</b></div>
      <div style="border-top:1px solid var(--line-soft);margin:14px 0"></div>
      <div class="card-h"><h3>成员信息</h3><div class="sp"></div><span class="small faint">姓名/性别/部门由管理员维护</span></div>
      <template v-if="member">
        <div class="prow"><span>姓名</span><b>{{ member.name }}</b></div>
        <div class="prow"><span>性别</span><b>{{ member.gender === 'F' ? '女' : '男' }}</b></div>
        <div class="prow"><span>部门</span><b>{{ member.department || '—' }}</b></div>
        <div class="prow"><span>电话</span><b class="num">{{ member.phone || '—' }}</b></div>
        <div class="prow"><span>备注</span><b>{{ member.remark || '—' }}</b></div>
      </template>
      <div v-else class="small faint" style="line-height:1.7">该账号未关联成员名单，暂无成员信息；头像、电话、备注需关联后才能编辑，请联系管理员在「基础设置 → 账号管理」里关联。</div>
    </div>

    <div>
      <!-- 编辑基本信息 -->
      <div class="card">
        <div class="card-h"><h3>编辑基本信息</h3></div>
        <n-form label-placement="top">
          <n-form-item label="显示名"><n-input v-model:value="form.displayName" placeholder="系统内显示的名字" /></n-form-item>
          <n-form-item v-if="member" label="头像">
            <div style="width:100%">
              <div style="display:flex;align-items:center;gap:12px;margin-bottom:10px">
                <img :src="previewAvatar" style="width:52px;height:52px;border-radius:50%;object-fit:cover;border:2px solid var(--line-soft)" alt="" />
                <div>
                  <div class="small">{{ form.avatarPath ? '已选择预设头像' : '默认头像（按名单顺序自动分配）' }}</div>
                  <n-button size="tiny" quaternary @click="form.avatarPath = null">恢复默认</n-button>
                </div>
                <div style="flex:1"></div>
                <n-tabs v-model:value="avatarTab" type="segment" size="small" style="width:150px">
                  <n-tab name="M">男</n-tab>
                  <n-tab name="F">女</n-tab>
                </n-tabs>
              </div>
              <div class="avatar-grid">
                <img v-for="url in AVATAR_PRESETS[avatarTab]" :key="url" :src="url" loading="lazy"
                  :class="{ on: form.avatarPath === url }" @click="form.avatarPath = url" />
              </div>
            </div>
          </n-form-item>
          <n-grid v-if="member" :cols="2" :x-gap="12">
            <n-form-item-gi label="电话"><n-input v-model:value="form.phone" /></n-form-item-gi>
            <n-form-item-gi label="备注"><n-input v-model:value="form.remark" placeholder="如：外聘专家 / 兼职配合等" /></n-form-item-gi>
          </n-grid>
        </n-form>
        <div style="display:flex;justify-content:flex-end"><n-button type="primary" @click="save">保存</n-button></div>
      </div>

      <!-- 更改密码 -->
      <div class="card">
        <div class="card-h"><h3>更改密码</h3></div>
        <n-form label-placement="top">
          <n-form-item label="原密码"><n-input v-model:value="pwd.oldPassword" type="password" show-password-on="click" /></n-form-item>
          <n-grid :cols="2" :x-gap="12">
            <n-form-item-gi label="新密码"><n-input v-model:value="pwd.newPassword" type="password" show-password-on="click" placeholder="至少 6 位" /></n-form-item-gi>
            <n-form-item-gi label="确认新密码"><n-input v-model:value="pwd.confirm" type="password" show-password-on="click" /></n-form-item-gi>
          </n-grid>
        </n-form>
        <div style="display:flex;justify-content:flex-end"><n-button type="primary" @click="changePwd">确认修改</n-button></div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.prow { display: flex; align-items: center; gap: 10px; padding: 7px 0; font-size: 13px; }
.prow span { width: 92px; flex: none; color: var(--sub); }
.prow b { font-weight: 500; }
</style>
