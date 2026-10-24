<script setup>
import { ref } from 'vue'
import api from '../api'
import { fileKind } from '../utils'

const show = ref(false)
const title = ref('')
const kind = ref('other')
const url = ref('')

// 拉取带鉴权的文件流并按类型预览；GCFX 等不可预览格式由调用方自行处理
async function open(downloadUrl, name) {
  const { data } = await api.get(downloadUrl, { responseType: 'blob' })
  if (url.value) URL.revokeObjectURL(url.value)
  url.value = URL.createObjectURL(data)
  title.value = name || ''
  kind.value = fileKind(name)
  show.value = true
}
defineExpose({ open })
</script>

<template>
  <n-modal v-model:show="show" preset="card" :title="title" style="width:920px;border-radius:16px">
    <iframe v-if="kind === 'pdf'" :src="url" style="width:100%;height:66vh;border:none;border-radius:8px;background:#f4f6f5"></iframe>
    <div v-else-if="kind === 'img'" style="text-align:center;background:#f7f9f7;padding:16px;border-radius:8px">
      <img :src="url" style="max-width:100%;max-height:66vh;border-radius:8px" />
    </div>
    <div v-else class="mut" style="padding:12px 4px">该格式暂不支持在线预览，请下载后查看。（支持预览：PDF、图片）</div>
    <template #footer>
      <div style="display:flex;justify-content:flex-end"><n-button @click="show = false">关闭</n-button></div>
    </template>
  </n-modal>
</template>
