<script setup>
import { ref, onMounted, h } from 'vue'
import { NTag, NButton, useMessage, useDialog } from 'naive-ui'
import api, { errMsg } from '../api'
import { fmtSize } from '../utils'
import { useAuthStore } from '../stores/auth'

const message = useMessage()
const dialog = useDialog()
const auth = useAuthStore()
const rows = ref([])
const cat = ref('')
const q = ref('')
const CATS = ['规章制度', '规范文件', '标准图集', '其他资料']

async function load() {
  const params = {}
  if (cat.value) params.category = cat.value
  if (q.value) params.q = q.value
  const { data } = await api.get('/library', { params })
  rows.value = data
}
onMounted(load)

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
    load()
  } catch (e) { message.error(errMsg(e, '上传失败')) }
}

// 在线预览：取 blob 后按类型渲染
const showPreview = ref(false)
const preview = ref({ title: '', kind: '', url: '' })
async function openPreview(r) {
  const { data } = await api.get(`/library/${r.id}/download`, { responseType: 'blob' })
  const url = URL.createObjectURL(data)
  const fmtUp = (r.format || '').toUpperCase()
  const kind = fmtUp === 'PDF' ? 'pdf' : ['PNG', 'JPG', 'JPEG', 'GIF', 'WEBP'].includes(fmtUp) ? 'img' : 'other'
  preview.value = { title: r.name, kind, url }
  showPreview.value = true
}
async function download(r) {
  const { data } = await api.get(`/library/${r.id}/download`, { responseType: 'blob' })
  const a = document.createElement('a')
  a.href = URL.createObjectURL(data)
  a.download = r.name
  a.click()
  setTimeout(() => URL.revokeObjectURL(a.href), 800)
}
function del(r) {
  dialog.warning({ title: '删除资料', content: `确定删除「${r.name}」？`, positiveText: '删除', negativeText: '取消',
    onPositiveClick: async () => { await api.delete('/library/' + r.id); message.success('已删除'); load() } })
}

const columns = [
  { title: '资料名称', key: 'name', ellipsis: { tooltip: true }, render: (r) => h('span', { class: 'pname' }, r.name) },
  { title: '分类', key: 'category', width: 100, render: (r) => h(NTag, { size: 'small', bordered: false }, () => r.category) },
  { title: '格式', key: 'format', width: 70 },
  { title: '大小', key: 'sizeBytes', width: 90, align: 'right', render: (r) => fmtSize(r.sizeBytes) },
  { title: '上传日期', key: 'uploadedAt', width: 110, render: (r) => String(r.uploadedAt).slice(0, 10) },
  { title: '上传人', key: 'uploadedBy', width: 90 },
  { title: '操作', key: 'act', width: 210, render: (r) => h('div', { style: 'display:flex;gap:6px' }, [
    h(NButton, { size: 'small', type: 'primary', onClick: () => openPreview(r) }, () => '在线预览'),
    h(NButton, { size: 'small', onClick: () => download(r) }, () => '下载'),
    ...(auth.isAdmin ? [h(NButton, { size: 'small', quaternary: true, onClick: () => del(r) }, () => '删除')] : [])
  ]) }
]
</script>

<template>
  <div>
    <div class="card" style="padding:14px 18px">
      <div style="display:flex;gap:10px;align-items:center;flex-wrap:wrap">
        <div style="display:flex;gap:6px">
          <n-button v-for="c in ['', ...CATS]" :key="c" size="small" :type="cat === c ? 'primary' : 'default'" :ghost="cat !== c" @click="cat = c; load()">{{ c || '全部' }}</n-button>
        </div>
        <div style="flex:1"></div>
        <n-input v-model:value="q" placeholder="搜索资料名称" style="width:220px" clearable @update:value="load" />
        <n-button type="primary" @click="showUpload = true">上传资料</n-button>
      </div>
    </div>
    <div class="card" style="padding:6px 10px">
      <n-data-table :columns="columns" :data="rows" :bordered="false" />
      <div v-if="!rows.length" class="faint" style="text-align:center;padding:26px">暂无资料，点右上角上传第一份</div>
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

    <n-modal v-model:show="showPreview" preset="card" :title="preview.title" style="width:920px;border-radius:16px">
      <iframe v-if="preview.kind === 'pdf'" :src="preview.url" style="width:100%;height:66vh;border:none;border-radius:8px;background:#f4f6f5"></iframe>
      <div v-else-if="preview.kind === 'img'" style="text-align:center;background:#f7f9f7;padding:16px;border-radius:8px">
        <img :src="preview.url" style="max-width:100%;max-height:66vh;border-radius:8px" />
      </div>
      <div v-else class="mut" style="padding:12px 4px">该格式暂不支持在线预览，请下载后查看。（支持预览：PDF、图片）</div>
      <template #footer>
        <div style="display:flex;justify-content:flex-end"><n-button @click="showPreview = false">关闭</n-button></div>
      </template>
    </n-modal>
  </div>
</template>
