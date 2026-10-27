<script setup>
import { ref } from 'vue'
import { useMessage } from 'naive-ui'

// 全站共用的上传按钮（各处上传入口统一用它，不要各页各写一套）：
// 点按钮选文件（可多选）→ 逐个调父页给的 upload(file, onProgress) 上传（父页在自己的 api.post 里接
// onUploadProgress: onProgress）→ 按钮旁显示进度条与百分比，全部完成 emit('done', results)，父页刷新列表。
// silent=true 时不弹自带的成功提示（父页要按上传结果给专门提示时用，如 GCFX 回填提示）。
const props = defineProps({
  upload: { type: Function, required: true },
  label: { type: String, default: '上传' },
  multiple: { type: Boolean, default: false },
  size: { type: String, default: 'small' },
  type: { type: String, default: 'default' },
  ghost: { type: Boolean, default: false },
  disabled: { type: Boolean, default: false },
  silent: { type: Boolean, default: false }
})
const emit = defineEmits(['done'])
const message = useMessage()
const inputRef = ref(null)
const busy = ref(false)
const pct = ref(0)
const curName = ref('')

function pick() { if (!busy.value) inputRef.value?.click() }
async function onChange(e) {
  const files = [...(e.target.files || [])]
  e.target.value = ''
  if (!files.length) return
  busy.value = true
  const results = []
  try {
    for (const f of files) {
      curName.value = f.name
      pct.value = 0
      results.push(await props.upload(f, (p) => { pct.value = p }))
    }
    if (results.length && !props.silent) message.success(files.length > 1 ? `已上传 ${results.length} 个文件` : '上传成功')
    if (results.length) emit('done', results)
  } catch (err) {
    message.error(err?.response?.data?.message || err?.message || '上传失败')
    if (results.length) emit('done', results)
  } finally {
    busy.value = false
    pct.value = 0
    curName.value = ''
  }
}
</script>

<template>
  <span style="display:inline-flex;align-items:center;gap:8px">
    <input ref="inputRef" type="file" :multiple="multiple" style="display:none" @change="onChange" />
    <n-button :size="size" :type="type" :ghost="ghost" :disabled="disabled || busy" :loading="busy" @click="pick">
      <svg v-if="!busy" viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="margin-right:4px;vertical-align:-2px"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="17 8 12 3 7 8"/><line x1="12" y1="3" x2="12" y2="15"/></svg>
      {{ label }}
    </n-button>
    <span v-if="busy" style="display:inline-flex;align-items:center;gap:7px;min-width:170px">
      <n-progress type="line" :percentage="pct" :height="6" :show-indicator="false" style="width:110px" />
      <span class="small mut num">{{ pct }}%</span>
      <span class="small faint" style="max-width:170px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap">{{ curName }}</span>
    </span>
  </span>
</template>
