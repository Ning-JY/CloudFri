<script setup>
import { ref, computed, onMounted, h } from 'vue'
import { NTag, NButton, useMessage, useDialog } from 'naive-ui'
import api, { errMsg } from '../api'
import { fmtSize, iconBtn } from '../utils'
import { useAuthStore } from '../stores/auth'
import PreviewModal from '../components/PreviewModal.vue'
import { useDicts } from '../stores/dicts'

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

// 上传
const showUpload = ref(false)
const upCat = ref('规章制度')
const upName = ref('')
const upFile = ref(null)
function pickUploadFile() {
  const inp = document.createElement('input')
  inp.type = 'file'
  inp.onchange = () => { upFile.value = inp.files[0] || null }
  inp.click()
}
async function doUpload() {
  if (!upFile.value) { message.warning('请选择文件'); return }
  const fd = new FormData()
  fd.append('category', upCat.value)
  if (upName.value) fd.append('name', upName.value)
  fd.append('file', upFile.value)
  try {
    await api.post('/library', fd)
    message.success('上传成功')
    showUpload.value = false
    upFile.value = null; upName.value = ''
    await loadCounts()
    load()
  } catch (e) { message.error(errMsg(e, '上传失败')) }
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

const columns = [
  { title: '资料名称', key: 'name', ellipsis: { tooltip: true }, render: (r) => h('div', [
      h('div', { style: 'display:flex;align-items:center;gap:6px' }, [
        r.isPinned ? h(NTag, { size: 'tiny', type: 'warning', bordered: false }, () => '置顶') : null,
        h('span', { class: 'pname' }, r.name)
      ]),
      r.description ? h('div', { class: 'psub' }, r.description) : null
    ]) },
  { title: '分类', key: 'category', width: 100, render: (r) => h(NTag, { size: 'small', bordered: false }, () => r.category) },
  { title: '格式', key: 'format', width: 70 },
  { title: '大小', key: 'sizeBytes', width: 90, align: 'right', render: (r) => fmtSize(r.sizeBytes) },
  { title: '上传日期', key: 'uploadedAt', width: 110, render: (r) => String(r.uploadedAt).slice(0, 10) },
  { title: '上传人', key: 'uploadedBy', width: 90 },
  { title: '操作', key: 'act', width: 158, render: (r) => h('div', { style: 'display:flex' }, [
    iconBtn('eye', 'blue', '在线预览', () => openPreview(r)),
    iconBtn('pencil', 'green', '编辑（名称/分类/说明/置顶）', () => openEdit(r)),
    iconBtn('download', 'slate', '下载', () => download(r)),
    ...(auth.isAdmin ? [iconBtn('trash', 'red', '删除', () => del(r))] : [])
  ]) }
]
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
        <n-button type="primary" @click="upCat = cat || CATS[0]; showUpload = true">上传资料</n-button>
      </div>
    </div>
    <div class="card" style="padding:6px 10px">
      <n-data-table :columns="columns" :data="rows" :bordered="false" />
      <div v-if="!rows.length" class="faint" style="text-align:center;padding:26px">该分类暂无资料，点右上角上传</div>
    </div>
    </div>

    <n-modal v-model:show="showUpload" preset="card" title="上传资料" style="width:480px;border-radius:16px">
      <n-form label-placement="top">
        <n-form-item label="分类"><n-select v-model:value="upCat" :options="CATS.map(c => ({ label: c, value: c }))" /></n-form-item>
        <n-form-item label="显示名称（可空，默认取文件名）"><n-input v-model:value="upName" /></n-form-item>
        <n-form-item label="文件">
          <n-button @click="pickUploadFile">选择文件</n-button>
          <span class="small mut" style="margin-left:10px">{{ upFile ? upFile.name : '未选择' }}</span>
        </n-form-item>
      </n-form>
      <template #footer>
        <div style="display:flex;justify-content:flex-end;gap:10px">
          <n-button @click="showUpload = false">取消</n-button>
          <n-button type="primary" @click="doUpload">上传</n-button>
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
