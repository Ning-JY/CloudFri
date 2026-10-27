import { ref } from 'vue'

// 列表分页状态（与 components/Pager.vue 配套，全站列表统一用这一套）：
// const pager = usePager() → const pagedRows = computed(() => pager.slice(rows.value))，
// 表格 :data="pagedRows"，表下放 <Pager v-model:page="pager.page.value" v-model:page-size="pager.pageSize.value" :total="rows.length" />。
// 筛选条件变化时调 pager.reset() 回到第一页。
export function usePager(defaultSize = 15) {
  const page = ref(1)
  const pageSize = ref(defaultSize)
  function slice(list) {
    const start = (page.value - 1) * pageSize.value
    return list.slice(start, start + pageSize.value)
  }
  function reset() { page.value = 1 }
  return { page, pageSize, slice, reset }
}
