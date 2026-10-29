<script setup>
import { ref, shallowRef, defineAsyncComponent } from 'vue'
import api from '../api'
import { fileKind, fmtSize } from '../utils'

// 全站统一的文件预览组件（项目成果/资料、合同附件、资料库都用它，别再各写一套）：
// Office/PDF 走 @vue-office 套件（docx/excel/pdf/pptx，Excel 带样式渲染）；图片浏览器原生；
// tif 用 UTIF 解码成位图（支持多页翻页）；Markdown 用 marked + DOMPurify 渲染；
// 代码/文本用 highlight.js 高亮；压缩包（zip/7z/rar/tar…）用 libarchive.js 只列文件结构、不解压内容。
// .doc/.ppt 等老格式、GCFX 等仍提示下载查看。重型库全部懒加载，不进首屏包。
const VueOfficeDocx = defineAsyncComponent(() => Promise.all([import('@vue-office/docx'), import('@vue-office/docx/lib/index.css')]).then(([m]) => m))
const VueOfficeExcel = defineAsyncComponent(() => Promise.all([import('@vue-office/excel'), import('@vue-office/excel/lib/index.css')]).then(([m]) => m))
const VueOfficePdf = defineAsyncComponent(() => import('@vue-office/pdf'))
const VueOfficePptx = defineAsyncComponent(() => import('@vue-office/pptx'))

const show = ref(false)
const title = ref('')
const kind = ref('other')
// PDF 缩放：调 @vue-office/pdf 自带的 setScale（内部按新比例整册重渲染）；每次打开新文件重置为 100% 并换 key 重挂载
const pdfRef = ref(null)
const pdfScale = ref(1)
const pdfKey = ref(0)
function zoomPdf(d) {
  const v = Math.min(3, Math.max(0.5, Math.round((pdfScale.value + d) * 100) / 100))
  pdfScale.value = v
  pdfRef.value?.setScale?.(v)
}
const url = ref('')
const buf = shallowRef(null)
const mdHtml = ref('')
const codeHtml = ref('')
const text = ref('')
const tifPages = ref([])
const tifPage = ref(0)
const arcEntries = ref([])
const arcNote = ref('')
const loading = ref(false)
const failed = ref('')

const CODE_LANG = { JSON: 'json', JS: 'javascript', TS: 'typescript', CSS: 'css', HTML: 'xml', XML: 'xml', PY: 'python', CS: 'csharp', JAVA: 'java', SQL: 'sql', SH: 'bash', YAML: 'yaml', YML: 'yaml', INI: 'ini', CONF: 'ini' }

function reset() {
  mdHtml.value = ''; codeHtml.value = ''; text.value = ''
  tifPages.value = []; tifPage.value = 0
  arcEntries.value = []; arcNote.value = ''
  buf.value = null; failed.value = ''
  if (url.value) URL.revokeObjectURL(url.value)
  url.value = ''
}

async function open(downloadUrl, name) {
  title.value = name || ''
  kind.value = fileKind(name)
  pdfScale.value = 1
  pdfKey.value++
  reset()
  show.value = true
  loading.value = true
  try {
    const { data } = await api.get(downloadUrl, { responseType: 'blob' })
    const k = kind.value
    if (k === 'img') {
      url.value = URL.createObjectURL(data)
    } else if (k === 'pdf' || k === 'docx' || k === 'sheet' || k === 'pptx') {
      buf.value = await data.arrayBuffer()
    } else if (k === 'tif') {
      await renderTif(await data.arrayBuffer())
    } else if (k === 'md') {
      const [{ marked }, DOMPurify] = await Promise.all([import('marked'), import('dompurify')])
      mdHtml.value = DOMPurify.default.sanitize(marked.parse(await data.text()))
    } else if (k === 'code' || k === 'text') {
      const raw = await data.text()
      if (k === 'code') {
        const hljs = (await import('highlight.js')).default
        await import('highlight.js/styles/github.css')
        const lang = CODE_LANG[String(name).split('.').pop().toUpperCase()]
        codeHtml.value = lang && hljs.getLanguage(lang) ? hljs.highlight(raw, { language: lang }).value : hljs.highlightAuto(raw).value
      } else text.value = raw
    } else if (k === 'archive') {
      await listArchive(data, name)
    } else {
      failed.value = '该格式暂不支持在线预览，请下载后查看。（支持预览：PDF、Word .docx、Excel、PPT .pptx、图片（含 tif）、Markdown、文本/代码、压缩包结构）'
    }
  } catch (e) {
    failed.value = '预览生成失败，请下载后查看。'
  } finally {
    loading.value = false
  }
}

// tif/tiff：UTIF 解码每一页为 PNG dataURL，模板里翻页切换
async function renderTif(ab) {
  const UTIF = (await import('utif')).default
  const ifds = UTIF.decode(ab)
  if (!ifds?.length) { failed.value = '该 tif 文件未能解码，请下载后查看。'; return }
  const pages = []
  for (const ifd of ifds) {
    UTIF.decodeImage(ab, ifd)
    const rgba = UTIF.toRGBA8(ifd)
    const cv = document.createElement('canvas')
    cv.width = ifd.width; cv.height = ifd.height
    const ctx = cv.getContext('2d')
    const img = ctx.createImageData(ifd.width, ifd.height)
    img.data.set(rgba)
    ctx.putImageData(img, 0, 0)
    pages.push(cv.toDataURL('image/png'))
  }
  tifPages.value = pages
}

// 压缩包：libarchive.js（WASM 在 public/libarchive 下自托管）只读目录结构，不解压文件内容
async function listArchive(blob, name) {
  try {
    const { Archive } = await import('libarchive.js')
    Archive.init({ workerUrl: '/libarchive/worker-bundle.js' })
    const file = new File([blob], name || 'archive')
    const archive = await Archive.open(file)
    const arr = await archive.getFilesArray()
    arcEntries.value = arr.map(e => {
      const path = e.path || e.file?.name || e.name || ''
      return { path, size: e.file?.size ?? e.size ?? 0, dir: String(path).endsWith('/') }
    }).filter(e => e.path).sort((a, b) => a.path.localeCompare(b.path))
    arcNote.value = `共 ${arcEntries.value.length} 项（只展示结构，不解压内容；需要里面的文件请下载后解压）`
  } catch (e) {
    failed.value = '压缩包结构读取失败（格式不受支持或文件损坏），请下载后查看。'
  }
}

function onRenderError() { failed.value = '预览渲染失败，请下载后查看。' }
defineExpose({ open })
</script>

<template>
  <n-modal v-model:show="show" preset="card" :title="title" style="width:920px;max-width:94vw;border-radius:16px">
    <div v-if="loading" style="text-align:center;padding:30px"><n-spin size="medium" /><div class="small mut" style="margin-top:8px">正在生成预览…</div></div>
    <template v-else-if="!failed">
      <div v-if="kind === 'pdf'">
        <div class="pdf-tools">
          <n-button size="small" :disabled="pdfScale <= 0.5" @click="zoomPdf(-0.25)" title="缩小">−</n-button>
          <span class="num small" style="min-width:46px;text-align:center">{{ Math.round(pdfScale * 100) }}%</span>
          <n-button size="small" :disabled="pdfScale >= 3" @click="zoomPdf(0.25)" title="放大">+</n-button>
        </div>
        <div class="office-wrap pdf-wrap"><VueOfficePdf ref="pdfRef" :key="pdfKey" :src="buf" @error="onRenderError" /></div>
      </div>
      <div v-else-if="kind === 'docx'" class="office-wrap"><VueOfficeDocx :src="buf" @error="onRenderError" /></div>
      <div v-else-if="kind === 'sheet'" class="office-wrap"><VueOfficeExcel :src="buf" :options="{ xls: true }" @error="onRenderError" /></div>
      <div v-else-if="kind === 'pptx'" class="office-wrap"><VueOfficePptx :src="buf" @error="onRenderError" /></div>
      <div v-else-if="kind === 'img'" style="text-align:center;background:#f7f9f7;padding:16px;border-radius:8px">
        <img :src="url" style="max-width:100%;max-height:66vh;border-radius:8px" />
      </div>
      <div v-else-if="kind === 'tif'" style="text-align:center;background:#f7f9f7;padding:16px;border-radius:8px">
        <img v-if="tifPages.length" :src="tifPages[tifPage]" style="max-width:100%;max-height:62vh;border-radius:8px" />
        <div v-if="tifPages.length > 1" style="margin-top:10px;display:flex;gap:8px;justify-content:center;align-items:center">
          <n-button size="small" :disabled="tifPage <= 0" @click="tifPage--">上一页</n-button>
          <span class="small mut num">第 {{ tifPage + 1 }} / {{ tifPages.length }} 页</span>
          <n-button size="small" :disabled="tifPage >= tifPages.length - 1" @click="tifPage++">下一页</n-button>
        </div>
      </div>
      <div v-else-if="kind === 'md'" class="doc-preview md-body" v-html="mdHtml"></div>
      <pre v-else-if="kind === 'code'" class="text-preview code-preview" v-html="codeHtml"></pre>
      <pre v-else-if="kind === 'text'" class="text-preview">{{ text }}</pre>
      <div v-else-if="kind === 'archive'" class="doc-preview">
        <div class="small mut" style="margin-bottom:8px">{{ arcNote }}</div>
        <table>
          <tbody>
            <tr v-for="e in arcEntries" :key="e.path">
              <td>
                <svg v-if="e.dir" viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="#c26a12" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:5px"><path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"/></svg>
                <svg v-else viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="#68766d" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px;margin-right:5px"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/></svg>
                {{ e.path }}
              </td>
              <td class="num" style="text-align:right;width:110px">{{ e.dir ? '' : fmtSize(e.size) }}</td>
            </tr>
          </tbody>
        </table>
        <p v-if="!arcEntries.length" class="mut">（包内没有文件）</p>
      </div>
      <div v-else class="mut" style="padding:12px 4px">该格式暂不支持在线预览，请下载后查看。</div>
    </template>
    <div v-else class="mut" style="padding:12px 4px">{{ failed }}</div>
    <template #footer>
      <div style="display:flex;justify-content:flex-end"><n-button @click="show = false">关闭</n-button></div>
    </template>
  </n-modal>
</template>

<style scoped>
.office-wrap { height: 68vh; overflow: auto; background: #f4f6f5; border-radius: 8px; }
/* PDF 专用：@vue-office/pdf 是窗口化懒渲染，靠它自身容器的 scroll 推进渲染窗口；
   外层不能代替它滚动（否则 scrollTop 恒为 0、只渲染开头几页），故把 68vh 高度交给组件根元素自己滚。 */
.pdf-wrap { overflow: hidden; }
.pdf-wrap :deep(.vue-office-pdf) { height: 64vh; border-radius: 8px; }
.pdf-tools { display: flex; align-items: center; justify-content: flex-end; gap: 6px; margin-bottom: 8px; }
.doc-preview { max-height: 66vh; overflow: auto; padding: 14px 16px; background: #fff; border: 1px solid var(--line-soft); border-radius: 8px; font-size: 13px; line-height: 1.6; color: #26312b; }
.doc-preview table { border-collapse: collapse; margin: 6px 0; width: 100%; }
.doc-preview td, .doc-preview th { border-bottom: 1px solid #e3e8e4; padding: 4px 8px; }
.text-preview { max-height: 66vh; overflow: auto; padding: 14px 16px; background: #f7f9f7; border-radius: 8px; font-size: 13px; line-height: 1.7; white-space: pre-wrap; word-break: break-all; }
.code-preview { white-space: pre; word-break: normal; background: #fff; border: 1px solid var(--line-soft); font-family: ui-monospace, Menlo, Consolas, monospace; font-size: 12.5px; }
.md-body :deep(h1), .md-body :deep(h2), .md-body :deep(h3) { color: var(--pri-d); margin: 14px 0 6px; line-height: 1.35; }
.md-body :deep(h1) { font-size: 20px; } .md-body :deep(h2) { font-size: 17px; } .md-body :deep(h3) { font-size: 15px; }
.md-body :deep(p) { margin: 6px 0; }
.md-body :deep(ul), .md-body :deep(ol) { padding-left: 22px; margin: 6px 0; }
.md-body :deep(code) { background: #eef3ef; border-radius: 4px; padding: 1px 5px; font-family: ui-monospace, Menlo, Consolas, monospace; font-size: 12.5px; }
.md-body :deep(pre) { background: #f7f9f7; border-radius: 8px; padding: 10px 12px; overflow: auto; }
.md-body :deep(pre code) { background: none; padding: 0; }
.md-body :deep(table) { width: auto; } .md-body :deep(th) { background: #f4f8f5; font-weight: 650; }
.md-body :deep(blockquote) { border-left: 3px solid var(--line-soft); margin: 8px 0; padding: 2px 12px; color: var(--sub); }
.md-body :deep(a) { color: var(--pri); }
.md-body :deep(img) { max-width: 100%; }
</style>
