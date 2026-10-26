<script setup>
import { ref } from 'vue'
import api from '../api'
import { fileKind } from '../utils'
import * as mammoth from 'mammoth/mammoth.browser'
import * as XLSX from 'xlsx'

const show = ref(false)
const title = ref('')
const kind = ref('other')
const url = ref('')
const html = ref('')
const text = ref('')
const loading = ref(false)
const failed = ref('')

// 拉取带鉴权的文件流并按类型预览：PDF/图片走浏览器原生渲染；
// Word(docx) 用 mammoth 转 HTML；Excel(xlsx/csv) 用 SheetJS 转表格；txt 直接读文本。
// 旧版 .doc/.xls、GCFX 等格式仍提示下载查看。
async function open(downloadUrl, name) {
  title.value = name || ''
  kind.value = fileKind(name)
  html.value = ''
  text.value = ''
  failed.value = ''
  if (url.value) URL.revokeObjectURL(url.value)
  url.value = ''
  show.value = true
  loading.value = true
  try {
    const { data } = await api.get(downloadUrl, { responseType: 'blob' })
    if (kind.value === 'pdf' || kind.value === 'img') {
      url.value = URL.createObjectURL(data)
    } else if (kind.value === 'docx') {
      const buf = await data.arrayBuffer()
      const out = await mammoth.convertToHtml({ arrayBuffer: buf })
      html.value = out.value || '<p>（文档无正文内容）</p>'
    } else if (kind.value === 'sheet') {
      const buf = await data.arrayBuffer()
      const wb = XLSX.read(buf, { type: 'array' })
      const parts = []
      for (const sn of wb.SheetNames) {
        const sheetHtml = XLSX.utils.sheet_to_html(wb.Sheets[sn])
        parts.push((wb.SheetNames.length > 1 ? `<div class="sheet-name">${sn}</div>` : '') + sheetHtml)
      }
      html.value = parts.join('<div style="height:18px"></div>') || '<p>（工作簿无内容）</p>'
    } else if (kind.value === 'text') {
      text.value = await data.text()
    }
  } catch (e) {
    failed.value = '预览生成失败，请下载后查看。'
  } finally {
    loading.value = false
  }
}
defineExpose({ open })
</script>

<template>
  <n-modal v-model:show="show" preset="card" :title="title" style="width:920px;max-width:94vw;border-radius:16px">
    <div v-if="loading" style="text-align:center;padding:30px"><n-spin size="medium" /><div class="small mut" style="margin-top:8px">正在生成预览…</div></div>
    <iframe v-else-if="kind === 'pdf'" :src="url" style="width:100%;height:66vh;border:none;border-radius:8px;background:#f4f6f5"></iframe>
    <div v-else-if="kind === 'img'" style="text-align:center;background:#f7f9f7;padding:16px;border-radius:8px">
      <img :src="url" style="max-width:100%;max-height:66vh;border-radius:8px" />
    </div>
    <div v-else-if="(kind === 'docx' || kind === 'sheet') && html" class="doc-preview" v-html="html"></div>
    <pre v-else-if="kind === 'text'" class="text-preview">{{ text }}</pre>
    <div v-else class="mut" style="padding:12px 4px">{{ failed || '该格式暂不支持在线预览，请下载后查看。（支持预览：PDF、图片、Word .docx、Excel .xlsx/.csv、.txt）' }}</div>
    <template #footer>
      <div style="display:flex;justify-content:flex-end"><n-button @click="show = false">关闭</n-button></div>
    </template>
  </n-modal>
</template>

<style scoped>
.doc-preview { max-height: 66vh; overflow: auto; padding: 18px 20px; background: #fff; border: 1px solid var(--line-soft); border-radius: 8px; font-size: 14px; line-height: 1.75; color: #26312b; }
.doc-preview table { border-collapse: collapse; margin: 10px 0; }
.doc-preview td, .doc-preview th { border: 1px solid #cfd6d1; padding: 5px 9px; font-size: 13px; }
.doc-preview .sheet-name { font-weight: 700; margin: 4px 0 6px; color: var(--pri-d); }
.text-preview { max-height: 66vh; overflow: auto; padding: 14px 16px; background: #f7f9f7; border-radius: 8px; font-size: 13px; line-height: 1.7; white-space: pre-wrap; word-break: break-all; }
</style>
