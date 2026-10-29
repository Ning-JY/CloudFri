<script setup>
import { ref, computed, onMounted, h } from 'vue'
import { NTag, NButton, useMessage, useDialog } from 'naive-ui'
import api, { errMsg } from '../api'
import { fmtSize, iconBtn, nameCell } from '../utils'
import { useAuthStore } from '../stores/auth'
import PreviewModal from '../components/PreviewModal.vue'
import { useDicts } from '../stores/dicts'
import TableTools from '../components/TableTools.vue'
import Pager from '../components/Pager.vue'
import UploadBtn from '../components/UploadBtn.vue'
import { usePager } from '../composables/usePager'

const pager = usePager()
const pagedRows = computed(() => pager.slice(rows.value))

const message = useMessage()
const dialog = useDialog()
const auth = useAuthStore()
const rows = ref([])
const allRows = ref([])
const cat = ref('')
const q = ref('')
// 资料分类走全站字典（LibCategory）
const dicts = useDicts()
const CATS = computed(() => dicts.names('LibCategory'))
const previewRef = ref(null)

async function load() {
  pager.reset()
  const params = {}
  if (cat.value) params.category = cat.value
  if (q.value) params.q = q.value
  const { data } = await api.get('/library', { params })
  rows.value = data
}
async function loadCounts() {
  const { data } = await api.get('/library')
  allRows.value = data
}
function countOf(c) { return c ? allRows.value.filter(r => r.category === c).length : allRows.value.length }
onMounted(async () => { await loadCounts(); load() })

// 上传（带进度，走全站共用 UploadBtn）：分类先在弹窗里选好，点按钮选文件即上传，可多选批量上传；
// 显示名称只在单个文件时生效（批量时各文件用自己的文件名，避免同名）
const showUpload = ref(false)
const upCat = ref('规章制度')
const upName = ref('')
async function doUpload(file, onProgress, meta) {
  const fd = new FormData()
  fd.append('category', upCat.value)
  if (upName.value && (!meta || meta.total <= 1)) fd.append('name', upName.value)
  fd.append('file', file)
  await api.post('/library', fd, { onUploadProgress: (e) => { if (e.total) onProgress(Math.round(e.loaded / e.total * 100)) } })
}
async function onUploaded() {
  showUpload.value = false
  upName.value = ''
  await loadCounts()
  load()
}

// 在线预览：统一走全局 PreviewModal（与项目/合同文件同一组件，支持 docx/xlsx 保格式预览）
// 资料 Name 不带扩展名，传给预览组件时补上，组件才能按类型渲染
const fileNameOf = (r) => r.format ? (r.name.toLowerCase().endsWith('.' + r.format.toLowerCase()) ? r.name : r.name + '.' + r.format.toLowerCase()) : r.name
function openPreview(r) { previewRef.value.open(`/library/${r.id}/download`, fileNameOf(r)) }
async function download(r) {
  const { data } = await api.get(`/library/${r.id}/download`, { responseType: 'blob' })
  const a = document.createElement('a')
  a.href = URL.createObjectURL(data)
  a.download = fileNameOf(r) // 下载文件名补扩展名（后端 Name 是不带扩展名的显示名）
  a.click()
  setTimeout(() => URL.revokeObjectURL(a.href), 800)
}
// 编辑资料信息（名称/分类/说明/置顶）
const showEdit = ref(false)
const ef = ref({})
function openEdit(r) { ef.value = { id: r.id, name: r.name, category: r.category, description: r.description || '', isPinned: !!r.isPinned }; showEdit.value = true }
async function saveEdit() {
  if (!ef.value.name) { message.warning('请填写资料名称'); return }
  try {
    await api.put('/library/' + ef.value.id, { name: ef.value.name, category: ef.value.category, description: ef.value.description || null, isPinned: ef.value.isPinned })
    message.success('已保存')
    showEdit.value = false
    await loadCounts()
    load()
  } catch (e) { message.error(errMsg(e)) }
}
function del(r) {
  dialog.warning({ title: '删除资料', content: `确定删除「${r.name}」？`, positiveText: '删除', negativeText: '取消',
    onPositiveClick: async () => { await api.delete('/library/' + r.id); message.success('已删除'); await loadCounts(); load() } })
}

const ALL_COLUMNS = [
  { title: '资料名称', key: 'name', exportValue: (r) => r.name + (r.description ? '（' + r.description + '）' : ''), render: (r) => h('div', [
      h('div', { style: 'display:flex;align-items:center;gap:6px;min-width:0' }, [
        r.isPinned ? h(NTag, { size: 'tiny', type: 'warning', bordered: false, style: 'flex:none' }, () => '置顶') : null,
        h('div', { class: 'pname', style: 'flex:1;min-width:0' }, [nameCell(r.name)])
      ]),
      r.description ? h('div', { class: 'psub' }, r.description) : null
    ]) },
  { title: '分类', key: 'category', width: 100, render: (r) => h(NTag, { size: 'small', bordered: false }, () => r.category) },
  { title: '格式', key: 'format', width: 70 },
  { title: '大小', key: 'sizeBytes', width: 90, align: 'right', exportValue: (r) => fmtSize(r.sizeBytes), render: (r) => fmtSize(r.sizeBytes) },
  { title: '上传日期', key: 'uploadedAt', width: 110, exportValue: (r) => String(r.uploadedAt).slice(0, 10), render: (r) => String(r.uploadedAt).slice(0, 10) },
  { title: '上传人', key: 'uploadedBy', width: 90 },
  { title: '操作', key: 'act', width: 158, exportValue: () => '', render: (r) => h('div', { style: 'display:flex' }, [
    iconBtn('eye', 'blue', '在线预览', () => openPreview(r)),
    iconBtn('pencil', 'green', '编辑（名称/分类/说明/置顶）', () => openEdit(r)),
    iconBtn('download', 'slate', '下载', () => download(r)),
    ...(auth.isAdmin ? [iconBtn('trash', 'red', '删除', () => del(r))] : [])
  ]) }
]
const visibleKeys = ref(ALL_COLUMNS.map(c => c.key))
const columns = computed(() => ALL_COLUMNS.filter(c => visibleKeys.value.includes(c.key)))
</script>

<template>
  <div style="display:flex;gap:14px;align-items:flex-start">
    <!-- 左：分类 -->
    <div class="card lib-rail" style="padding:14px 10px">
      <div class="nav-t" style="padding-top:2px">资料分类</div>
      <button class="lib-cat" :class="{ on: cat === '' }" @click="cat = ''; load()">全部资料<span class="cnt">{{ countOf('') }}</span></button>
      <button class="lib-cat" v-for="c in CATS" :key="c" :class="{ on: cat === c }" @click="cat = c; load()">{{ c }}<span class="cnt">{{ countOf(c) }}</span></button>
    </div>
    <!-- 右：列表 -->
    <div style="flex:1;min-width:0">
    <div class="card" style="padding:14px 18px">
      <div style="display:flex;gap:10px;align-items:center;flex-wrap:wrap">
        <b style="font-size:14.5px">{{ cat || '全部资料' }}</b>
        <div style="flex:1"></div>
        <n-input v-model:value="q" placeholder="搜索资料名称" style="width:220px" clearable @update:value="load" />
        <TableTools v-model="visibleKeys" :cols="ALL_COLUMNS" :rows="rows" filename="资料库" storage-key="library" />
        <n-button type="primary" @click="upCat = cat || CATS[0]; showUpload = true">上传资料</n-button>
      </div>
    </div>
    <div class="card" style="padding:6px 10px">
      <n-data-table table-layout="fixed" :columns="columns" :data="pagedRows" :bordered="false" :pagination="false" />
      <Pager v-model:page="pager.page.value" v-model:page-size="pager.pageSize.value" :total="rows.length" />
      <div v-if="!rows.length" class="faint" style="text-align:center;padding:26px">该分类暂无资料，点右上角上传</div>
    </div>
    </div>

    <n-modal v-model:show="showUpload" preset="card" title="上传资料" style="width:480px;border-radius:16px">
      <n-form label-placement="top">
        <n-form-item label="分类"><n-select v-model:value="upCat" :options="CATS.map(c => ({ label: c, value: c }))" /></n-form-item>
        <n-form-item label="显示名称（可空，默认取文件名；仅单个文件时生效）"><n-input v-model:value="upName" /></n-form-item>
      </n-form>
      <p class="small faint" style="margin:0">可一次多选多个文件批量上传，同一批共用上面选的分类；批量时显示名称不生效，各文件用自己的文件名。</p>
      <template #footer>
        <div style="display:flex;justify-content:flex-end;gap:10px;align-items:center">
          <n-button @click="showUpload = false">取消</n-button>
          <UploadBtn label="选择文件并上传（可多选）" type="primary" multiple :upload="doUpload" @done="onUploaded" />
        </div>
      </template>
    </n-modal>

    <n-modal v-model:show="showEdit" preset="card" title="编辑资料" style="width:480px;border-radius:16px">
      <n-form label-placement="top">
        <n-form-item label="资料名称"><n-input v-model:value="ef.name" /></n-form-item>
        <n-form-item label="分类"><n-select v-model:value="ef.category" :options="CATS.map(c => ({ label: c, value: c }))" /></n-form-item>
        <n-form-item label="说明（这份资料是什么、用在哪）"><n-input v-model:value="ef.description" type="textarea" placeholder="如：2025 版计价规范电子版，清单编制时查条文用" /></n-form-item>
        <n-form-item label="置顶"><n-switch v-model:value="ef.isPinned" /><span class="small mut" style="margin-left:10px">置顶的资料排在列表最前面</span></n-form-item>
      </n-form>
      <template #footer>
        <div style="display:flex;justify-content:flex-end;gap:10px">
          <n-button @click="showEdit = false">取消</n-button>
          <n-button type="primary" @click="saveEdit">保存</n-button>
        </div>
      </template>
    </n-modal>

    <PreviewModal ref="previewRef" />
  </div>
</template>
