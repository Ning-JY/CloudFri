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

// 头像：按成员 id 与性别稳定映射到 randomuser 照片（正式版可换本地头像）
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

// 文件预览类型（按扩展名）：pdf / img / other
export function fileKind(name) {
  const ext = String(name || '').split('.').pop().toUpperCase()
  if (ext === 'PDF') return 'pdf'
  if (['PNG', 'JPG', 'JPEG', 'GIF', 'WEBP', 'BMP'].includes(ext)) return 'img'
  return 'other'
}

export function todayStr() {
  const d = new Date()
  const p = (x) => String(x).padStart(2, '0')
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`
}
