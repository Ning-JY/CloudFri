<script setup>
import { watch } from 'vue'

// 全站共用的列表分页（各列表统一用它，不要各页各写一套）：左侧「共 X 条」+ 右侧分页器，可切换每页条数。
// 父页用 composables/usePager.js 管理 page/pageSize 与切片，本组件只负责呈现与翻页事件，并自动钳制越界页码。
const props = defineProps({
  total: { type: Number, default: 0 },
  page: { type: Number, default: 1 },
  pageSize: { type: Number, default: 15 },
  pageSizes: { type: Array, default: () => [15, 30, 50] }
})
const emit = defineEmits(['update:page', 'update:pageSize'])

watch(() => [props.total, props.pageSize], () => {
  const max = Math.max(1, Math.ceil(props.total / props.pageSize))
  if (props.page > max) emit('update:page', max)
})
function onPage(p) { emit('update:page', p) }
function onSize(s) { emit('update:pageSize', s); emit('update:page', 1) }
</script>

<template>
  <div style="display:flex;align-items:center;gap:10px;padding:10px 2px 2px">
    <span class="small mut">共 {{ total }} 条</span>
    <div style="flex:1"></div>
    <n-pagination :page="page" :page-size="pageSize" :item-count="total" :page-sizes="pageSizes"
      show-size-picker @update:page="onPage" @update:page-size="onSize" />
  </div>
</template>
