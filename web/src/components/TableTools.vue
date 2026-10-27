<script setup>
import { onMounted, watch } from 'vue'
import { useMessage } from 'naive-ui'
import * as XLSX from 'xlsx'

// 全站共用的表格工具条（各列表统一用它，不要各页各写一套）：
// 两个图标按钮——列显隐（勾选控制哪些列显示，选择按 storageKey 存本地）+ 导出 Excel（导出当前显示列、当前数据行）。
// 列定义直接用表格的 columns 数组（key/title 必备，可选 exportValue(row) 自定义导出值，缺省取 row[key]）。
// title 可以是渲染函数（如金额表头的 thUnit），这种列必须另给 titleText 纯文本，供勾选名单与导出表头用。
const titleOf = (c) => c.titleText ?? (typeof c.title === 'string' ? c.title : c.key)
const props = defineProps({
  cols: { type: Array, required: true },
  modelValue: { type: Array, required: true },
  rows: { type: Array, default: () => [] },
  filename: { type: String, default: '导出' },
  storageKey: { type: String, default: '' }
})
const emit = defineEmits(['update:modelValue'])
const message = useMessage()

onMounted(() => {
  if (!props.storageKey) return
  try {
    const saved = JSON.parse(localStorage.getItem('cf-cols-' + props.storageKey) || 'null')
    if (Array.isArray(saved)) {
      const valid = props.cols.map(c => c.key).filter(k => saved.includes(k))
      if (valid.length && valid.join() !== props.modelValue.join()) emit('update:modelValue', valid)
    }
  } catch { /* 本地存储不可用时用默认全显 */ }
})
watch(() => props.modelValue, (v) => {
  if (props.storageKey) { try { localStorage.setItem('cf-cols-' + props.storageKey, JSON.stringify(v)) } catch { } }
}, { deep: true })

function toggle(key, on) {
  const set = new Set(props.modelValue)
  if (on) set.add(key); else set.delete(key)
  if (!set.size) { message.warning('至少保留一列'); return }
  emit('update:modelValue', props.cols.map(c => c.key).filter(k => set.has(k)))
}

function exportExcel() {
  if (!props.rows.length) { message.warning('没有可导出的数据'); return }
  const defs = props.cols.filter(c => props.modelValue.includes(c.key))
  const header = defs.map(c => String(titleOf(c)))
  const body = props.rows.map(r => defs.map(c => {
    const v = c.exportValue ? c.exportValue(r) : r[c.key]
    return v == null ? '' : v
  }))
  const ws = XLSX.utils.aoa_to_sheet([header, ...body])
  ws['!cols'] = defs.map((c, i) => {
    const maxLen = Math.max(header[i].length * 2, ...body.map(row => String(row[i] ?? '').length))
    return { wch: Math.min(44, Math.max(10, Math.ceil(maxLen * 1.15))) }
  })
  const wb = XLSX.utils.book_new()
  XLSX.utils.book_append_sheet(wb, ws, 'Sheet1')
  XLSX.writeFile(wb, props.filename + '.xlsx')
  message.success(`已导出 ${props.rows.length} 行`)
}
</script>

<template>
  <span style="display:inline-flex;gap:6px;align-items:center">
    <n-popover trigger="click" placement="bottom-end" :show-arrow="false" raw>
      <template #trigger>
        <button class="icbtn b-slate" title="选择显示的列"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="4" width="4.6" height="16" rx="1.2"/><rect x="9.7" y="4" width="4.6" height="16" rx="1.2"/><rect x="16.4" y="4" width="4.6" height="16" rx="1.2"/></svg></button>
      </template>
      <div class="tt-pop">
        <div class="small faint" style="margin-bottom:6px">显示列</div>
        <div v-for="c in cols" :key="c.key" class="tt-row">
          <n-checkbox :checked="modelValue.includes(c.key)" @update:checked="(v) => toggle(c.key, v)">{{ titleOf(c) }}</n-checkbox>
        </div>
      </div>
    </n-popover>
    <button class="icbtn b-green" title="导出 Excel（当前显示列）" @click="exportExcel"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><path d="M12 11v6"/><polyline points="8.6 14.2 12 17.6 15.4 14.2"/></svg></button>
  </span>
</template>

<style scoped>
.tt-pop { background: #fff; border-radius: 12px; box-shadow: 0 10px 34px rgba(23, 43, 33, .18); border: 1px solid var(--line-soft); padding: 10px 12px; min-width: 168px; max-height: 320px; overflow: auto; }
.tt-row { padding: 3px 0; }
</style>
