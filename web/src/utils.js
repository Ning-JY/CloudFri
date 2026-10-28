// 图标操作按钮（列表/卡片行内操作）：图形 SVG + 按动作区分颜色，渲染函数里用 h 构造。
import { h } from 'vue'
import { NEllipsis } from 'naive-ui'

// 金额列表头：名称 + 单位分开渲染——单位不用括号，用 .th-unit 橙色小字显示（如「造价 万元」）。
// 用了函数标题的列必须同时给 titleText 纯文本，供 TableTools 的列勾选名单与 Excel 导出表头使用。
export function thUnit(label, unit = '万元') {
  return () => h('span', [label, h('span', { class: 'th-unit' }, unit)])
}

// 名称单元格：单行省略，悬停显示完整名称（tooltip）。长名称列统一用它，别再用裸文本或列级 ellipsis。
export function nameCell(text) {
  return h(NEllipsis, { style: { maxWidth: '100%' }, tooltip: { width: 360, scrollable: true, maxHeight: 240 } }, { default: () => text ?? '' })
}
export const ICONS = {
  eye: '<path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/>',
  pencil: '<path d="M17 3a2.83 2.83 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5Z"/><path d="m15 5 4 4"/>',
  trash: '<polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/><line x1="10" y1="11" x2="10" y2="17"/><line x1="14" y1="11" x2="14" y2="17"/>',
  download: '<path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/>',
  upload: '<path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="17 8 12 3 7 8"/><line x1="12" y1="3" x2="12" y2="15"/>',
  clip: '<path d="M21.44 11.05l-9.19 9.19a6 6 0 0 1-8.49-8.49l8.57-8.57A4 4 0 1 1 18 8.84l-8.59 8.57a2 2 0 0 1-2.83-2.83l8.49-8.48"/>',
  unlink: '<path d="M9 17H7A5 5 0 0 1 7 7h2"/><path d="M15 7h2a5 5 0 1 1 0 10h-2"/><line x1="8" y1="12" x2="16" y2="12"/><line x1="4" y1="4" x2="20" y2="20"/>',
  link: '<path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71"/><path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71"/>',
  key: '<path d="M21 2l-2 2m-7.61 7.61a5.5 5.5 0 1 1-7.778 7.778 5.5 5.5 0 0 1 7.777-7.777zm0 0L15.5 7.5m0 0 3 3L22 7l-3-3m-3.5 3.5L19 4"/>',
  power: '<path d="M18.36 6.64a9 9 0 1 1-12.73 0"/><line x1="12" y1="2" x2="12" y2="12"/>',
  printer: '<polyline points="6 9 6 2 18 2 18 9"/><path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"/><rect x="6" y="14" width="12" height="8"/>',
  copy: '<rect x="9" y="9" width="13" height="13" rx="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/>',
  file: '<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/>',
  check: '<polyline points="20 6 9 17 4 12"/>',
  plus: '<line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/>'
}
export function iconBtn(kind, color, title, onClick) {
  return h('button', {
    class: `icbtn b-${color}`, title,
    onClick: (e) => { e.stopPropagation(); onClick() }
  }, [h('svg', {
    viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor',
    'stroke-width': 2, 'stroke-linecap': 'round', 'stroke-linejoin': 'round', innerHTML: ICONS[kind] || ''
  })])
}

// 金额格式化（万元）：千分位，最多 2 位小数、去掉尾随 0
export function fmt(v) {
  if (v === null || v === undefined || v === '') return '—'
  const n = Number(v)
  if (Number.isNaN(n)) return '—'
  return n.toLocaleString('zh-CN', { minimumFractionDigits: 0, maximumFractionDigits: 2 })
}

export function fmtMoney(v) {
  if (v === null || v === undefined) return '—'
  return Number(v).toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

export function fmtSize(bytes) {
  if (!bytes && bytes !== 0) return '—'
  if (bytes < 1024) return bytes + ' B'
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB'
  return (bytes / 1024 / 1024).toFixed(1) + ' MB'
}

// 台账单一价格口径：与原型一致
export function priceOf(p) {
  if (p.bizType === '结算审核' || p.bizType === '竣工决算')
    return p.approvedAmount != null ? { v: p.approvedAmount, lb: '结算价' } : { v: p.submitAmount, lb: '送审价' }
  if (p.bizType === '清单控制价') return { v: p.submitAmount, lb: '控制价' }
  if (p.bizType === '预算编制') return { v: p.submitAmount, lb: '预算价' }
  return { v: p.submitAmount, lb: '建安造价' }
}

export function cutRate(p) {
  if (p.submitAmount && p.approvedAmount != null && p.submitAmount > 0)
    return (p.submitAmount - p.approvedAmount) / p.submitAmount * 100
  return null
}

// 预设头像库（randomuser 照片，按性别分组供成员表单挑选；选中后存 Member.AvatarPath）
export const AVATAR_PRESETS = {
  M: Array.from({ length: 36 }, (_, i) => `https://randomuser.me/api/portraits/men/${i}.jpg`),
  F: Array.from({ length: 36 }, (_, i) => `https://randomuser.me/api/portraits/women/${i}.jpg`)
}

// 头像：优先成员自选头像（avatarPath），否则按成员 id 与性别稳定映射到 randomuser 照片（正式版可换本地头像）
const AVATAR_N = { 1: ['men', 52], 2: ['men', 32], 3: ['women', 44], 4: ['men', 22], 5: ['men', 41], 6: ['women', 68], 7: ['women', 12], 8: ['men', 15], 9: ['women', 33], 10: ['men', 54], 11: ['women', 26] }
export function avatarFor(member) {
  if (!member) return ''
  if (member.avatarPath) return member.avatarPath
  const known = AVATAR_N[member.id]
  const g = member.gender === 'F' ? 'women' : 'men'
  const n = known ? known[1] : (member.id * 7) % 90
  const gg = known ? known[0] : g
  return `https://randomuser.me/api/portraits/${gg}/${n}.jpg`
}

export function statusType(status) {
  return { '编制中': 'warning', '审核中': 'info', '已出报告': 'success', '已归档': 'default' }[status] || 'default'
}

// 文件预览类型（按扩展名）：pdf / img / docx(Word) / sheet(Excel·CSV) / text(纯文本) / other
// 注：旧版 .doc / .xls 二进制格式不在 docx/sheet 解析范围内，归 other（下载查看）
export function fileKind(name) {
  const ext = String(name || '').split('.').pop().toUpperCase()
  if (ext === 'PDF') return 'pdf'
  if (['PNG', 'JPG', 'JPEG', 'GIF', 'WEBP', 'BMP', 'SVG'].includes(ext)) return 'img'
  if (ext === 'DOCX') return 'docx'
  if (['XLSX', 'XLS', 'CSV'].includes(ext)) return 'sheet'
  if (ext === 'PPTX') return 'pptx'
  if (['TIF', 'TIFF'].includes(ext)) return 'tif'
  if (ext === 'MD') return 'md'
  if (['TXT', 'LOG'].includes(ext)) return 'text'
  if (['ZIP', 'RAR', '7Z', 'TAR', 'GZ', 'TGZ', 'BZ2', 'XZ'].includes(ext)) return 'archive'
  if (['JSON', 'JS', 'TS', 'CSS', 'HTML', 'XML', 'PY', 'CS', 'JAVA', 'SQL', 'SH', 'YAML', 'YML', 'INI', 'CONF'].includes(ext)) return 'code'
  return 'other'
}

export function todayStr() {
  const d = new Date()
  const p = (x) => String(x).padStart(2, '0')
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`
}
