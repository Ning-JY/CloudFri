<script setup>
import { ref, nextTick } from 'vue'
import api from '../api'
import { fileKind } from '../utils'
import * as XLSX from 'xlsx'
import { renderAsync as renderDocx } from 'docx-preview'

const show = ref(false)
const title = ref('')
const kind = ref('other')
const url = ref('')
const html = ref('')
const text = ref('')
const loading = ref(false)
const failed = ref('')
const docxHost = ref(null)

// 全站统一的文件预览组件（项目成果/资料、合同附件、资料库都用它，别再各写一套）：
// PDF/图片走浏览器原生渲染（显式指定 Blob 类型，避免个别浏览器把预览当下载）；
// Word(docx) 用 docx-preview 按原版式渲染（页边距/字体/表格/页眉页脚尽量保真）；
// Excel 用 SheetJS 读结构后自绘表格（保留合并单元格、列宽、数字格式与右对齐）；
// txt 直接读文本。旧版 .doc/.xls、GCFX 等仍提示下载查看。
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
      const type = kind.value === 'pdf' ? 'application/pdf' : (data.type || 'image/png')
      url.value = URL.createObjectURL(new Blob([data], { type }))
    } else if (kind.value === 'docx') {
      loading.value = false
      await nextTick()
      if (docxHost.value) {
        docxHost.value.innerHTML = ''
        await renderDocx(data, docxHost.value, null, {
          className: 'docx', inWrapper: true, breakPages: true,
          renderHeaders: true, renderFooters: true, useBase64URL: true
        })
      }
      return
    } else if (kind.value === 'sheet') {
      const buf = await data.arrayBuffer()
      const wb = XLSX.read(buf, { type: 'array' })
      const parts = []
      for (const sn of wb.SheetNames) {
        parts.push((wb.SheetNames.length > 1 ? `<div class="sheet-name">${escapeHtml(sn)}</div>` : '') + sheetToHtml(wb.Sheets[sn]))
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

function escapeHtml(s) {
  return String(s ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
}

// 把工作表渲染成带结构的表格：合并单元格、列宽、单元格的格式化文本（数字格式）、数字右对齐
function sheetToHtml(ws) {
  if (!ws || !ws['!ref']) return '<p>（空表）</p>'
  const range = XLSX.utils.decode_range(ws['!ref'])
  const skip = new Set()
  const span = {}
  for (const m of ws['!merges'] || []) {
    span[m.s.r + ',' + m.s.c] = { rs: m.e.r - m.s.r + 1, cs: m.e.c - m.s.c + 1 }
    for (let r = m.s.r; r <= m.e.r; r++)
      for (let c = m.s.c; c <= m.e.c; c++)
        if (r !== m.s.r || c !== m.s.c) skip.add(r + ',' + c)
  }
  let out = '<table><colgroup>'
  for (let c = range.s.c; c <= range.e.c; c++) {
    const col = (ws['!cols'] || [])[c]
    const w = col?.wpx || (col?.wch ? Math.round(col.wch * 7 + 5) : 96)
    out += `<col style="width:${w}px">`
  }
  out += '</colgroup>'
  for (let r = range.s.r; r <= range.e.r; r++) {
    out += '<tr>'
    for (let c = range.s.c; c <= range.e.c; c++) {
      if (skip.has(r + ',' + c)) continue
      const cell = ws[XLSX.utils.encode_cell({ r, c })]
      const sp = span[r + ',' + c]
      const v = cell ? (cell.w ?? (cell.v != null ? String(cell.v) : '')) : ''
      const num = cell && cell.t === 'n' ? ' class="num"' : ''
      out += `<td${sp ? ` rowspan="${sp.rs}" colspan="${sp.cs}"` : ''}${num}>${escapeHtml(v)}</td>`
    }
    out += '</tr>'
  }
  return out + '</table>'
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
    <div v-else-if="kind === 'docx'" class="docx-wrap"><div ref="docxHost"></div></div>
    <div v-else-if="kind === 'sheet' && html" class="doc-preview" v-html="html"></div>
    <pre v-else-if="kind === 'text'" class="text-preview">{{ text }}</pre>
    <div v-else class="mut" style="padding:12px 4px">{{ failed || '该格式暂不支持在线预览，请下载后查看。（支持预览：PDF、图片、Word .docx、Excel .xlsx/.csv、.txt）' }}</div>
    <template #footer>
      <div style="display:flex;justify-content:flex-end"><n-button @click="show = false">关闭</n-button></div>
    </template>
  </n-modal>
</template>

<style scoped>
.docx-wrap { max-height: 68vh; overflow: auto; background: #e8ece9; border-radius: 8px; padding: 14px 6px; }
.docx-wrap :deep(.docx-wrapper) { background: transparent; padding: 0; }
.docx-wrap :deep(.docx) { box-shadow: 0 2px 14px rgba(30, 50, 40, .16); margin: 0 auto 12px; }
.doc-preview { max-height: 66vh; overflow: auto; padding: 14px 16px; background: #fff; border: 1px solid var(--line-soft); border-radius: 8px; font-size: 13px; line-height: 1.6; color: #26312b; }
.doc-preview table { border-collapse: collapse; margin: 6px 0; table-layout: fixed; }
.doc-preview td, .doc-preview th { border: 1px solid #cfd6d1; padding: 4px 8px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.doc-preview td.num { text-align: right; font-variant-numeric: tabular-nums; }
.doc-preview .sheet-name { font-weight: 700; margin: 4px 0 6px; color: var(--pri-d); }
.text-preview { max-height: 66vh; overflow: auto; padding: 14px 16px; background: #f7f9f7; border-radius: 8px; font-size: 13px; line-height: 1.7; white-space: pre-wrap; word-break: break-all; }
</style>
