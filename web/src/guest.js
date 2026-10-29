// 访客模式（免登录演示）：开启后所有 /api 请求由本模块的自定义 axios adapter 在浏览器内拦截处理，
// 不发任何网络请求、不加载真实项目/合同/台账数据；访客新增/修改的数据只存内存（刷新或退出即清空），不落库。
// 返回结构逐项对齐后端各 Controller 的 DTO（camelCase），页面代码零改动即可在访客模式下运行。
import { DICT_FALLBACK } from './stores/dicts'

export const GUEST_KEY = 'cloudfri_guest'
export const isGuest = () => sessionStorage.getItem(GUEST_KEY) === '1'

// 与后端种子一致的三张流程模板（SeedData.cs）
const FORM_TEMPLATES = [
  { id: 1, key: 'seal', name: '盖章申请表', code: 'YZ', desc: '对外报送、合同文件等用印申请，审批意见栏打印后手签。', schemaJson: '{"byKey":"applicant","fields":[{"k":"dept","lb":"申请部门","ph":"如：造价咨询一部"},{"k":"applicant","lb":"申请人","ph":"姓名"},{"k":"reason","lb":"用印事由","ph":"说明用印事由","w":2},{"k":"sealType","lb":"印章类别","type":"sel","opts":["公司公章","合同专用章","财务专用章","法人章"]},{"k":"copies","lb":"用印文件份数","ph":"如：3 份"},{"k":"docName","lb":"用印文件名称","ph":"文件名称","w":2},{"k":"sendTo","lb":"文件报送单位","ph":"报送单位"},{"k":"date","lb":"申请日期","type":"date"}],"opinions":["部门负责人意见","分管领导意见","总经理意见","印章保管员存档确认"]}' },
  { id: 2, key: 'expense', name: '费用报销单', code: 'BX', desc: '差旅、办公、招待等费用报销，含费用类别、票据张数与收款方式。', schemaJson: '{"byKey":"person","fields":[{"k":"person","lb":"报销人","ph":"姓名"},{"k":"dept","lb":"所属部门","ph":"如：造价咨询二部"},{"k":"reason","lb":"报销事由","ph":"说明报销事由","w":2},{"k":"cat","lb":"费用类别","type":"sel","opts":["差旅费","办公费","业务招待费","交通费","其他"]},{"k":"amount","lb":"报销金额（元）","ph":"0.00"},{"k":"bills","lb":"票据张数","ph":"如：5 张"},{"k":"payway","lb":"收款方式","type":"sel","opts":["银行转账","现金","冲抵借款"]},{"k":"date","lb":"报销日期","type":"date"}],"opinions":["部门负责人意见","财务审核意见","总经理意见"]}' },
  { id: 3, key: 'leave', name: '请假申请表', code: 'QJ', desc: '事假、病假、年休假、调休等请假申请。', schemaJson: '{"byKey":"name","fields":[{"k":"name","lb":"申请人","ph":"姓名"},{"k":"dept","lb":"所属部门","ph":"部门"},{"k":"kind","lb":"请假类别","type":"sel","opts":["事假","病假","年休假","调休","其他"]},{"k":"days","lb":"请假天数","ph":"如：2 天"},{"k":"range","lb":"请假时间","ph":"如：10月28日—10月29日","w":2},{"k":"reason","lb":"请假事由","ph":"说明事由","w":2},{"k":"date","lb":"申请日期","type":"date"}],"opinions":["部门负责人意见","分管领导意见"]}' }
]

let seq = 1000
const nid = () => ++seq
const nowIso = () => new Date().toISOString()
const today = () => new Date().toISOString().slice(0, 10)

let db = null
export function resetGuestDb() {
  seq = 1000
  db = {
    displayName: '访客',
    members: [
      { id: 1, name: '演示负责人', gender: 'M', phone: '', department: '工程咨询事业部', remark: '', avatarPath: null, isActive: true, sort: 1 },
      { id: 2, name: '演示成员甲', gender: 'F', phone: '', department: '工程咨询事业部', remark: '', avatarPath: null, isActive: true, sort: 2 },
      { id: 3, name: '演示成员乙', gender: 'M', phone: '', department: '协作人员', remark: '', avatarPath: null, isActive: true, sort: 3 }
    ],
    dicts: Object.entries(DICT_FALLBACK).flatMap(([group, names]) => names.map((name, i) => ({ id: nid(), group, name, sort: i + 1, isActive: true }))),
    projects: [],       // { ..., team: [{memberId, role, duty, share}] }
    contracts: [],
    links: [],          // { projectId, contractId, isPrimary }
    receipts: [],       // { id, projectId, contractId, date, amount, method, handler, note, invoiceNo, isReceived, receivedDate }
    files: [],          // 成果文件 { id, projectId, setId, category, fileName, sizeBytes, uploadedBy, uploadedAt, version }
    sets: [],           // { id, projectId, version, isFinal, createdAt }
    docs: [],           // { id, projectId, category, fileName, sizeBytes, uploadedBy, uploadedAt }
    contractFiles: [],  // { id, contractId, fileName, sizeBytes, uploadedBy, uploadedAt }
    library: [],        // { id, name, category, format, sizeBytes, uploadedBy, uploadedAt, isPinned, description }
    records: [],        // 流程表单记录
    blobs: new Map()    // 文件内容（内存）：key = kind:id，下载/预览时原样返回
  }
}

const memberOf = (id) => db.members.find(m => m.id === id)
const projectOf = (id) => db.projects.find(p => p.id === id)
const contractOf = (id) => db.contracts.find(c => c.id === id)
const sum = (arr, f) => arr.reduce((a, x) => a + (f(x) || 0), 0)

function respond(config, data, status = 200) {
  return Promise.resolve({ data, status, statusText: 'OK', headers: {}, config })
}
function fail(config, status, message) {
  const err = new Error(message)
  err.config = config
  err.response = { status, statusText: '', headers: {}, config, data: { message } }
  return Promise.reject(err)
}
const notFound = (config, what = '记录') => fail(config, 404, what + '不存在')

// ---------- 各域的读模型（对齐后端投影） ----------
function projectListRow(p) {
  const recs = db.receipts.filter(r => r.projectId === p.id)
  return {
    id: p.id, code: p.code, name: p.name, client: p.client, bizType: p.bizType, status: p.status,
    ownerMemberId: p.ownerMemberId, acceptedDate: p.acceptedDate, submitAmount: p.submitAmount,
    approvedAmount: p.approvedAmount, fee: p.fee, createdBy: p.createdBy, createdAt: p.createdAt,
    received: sum(recs.filter(r => r.isReceived), r => r.amount),
    ownerName: p.ownerMemberId ? (memberOf(p.ownerMemberId)?.name ?? null) : null
  }
}
function projectDetail(p) {
  const team = (p.team || []).map((t, i) => {
    const m = memberOf(t.memberId)
    return { id: i + 1, memberId: t.memberId, name: m?.name ?? '', gender: m?.gender ?? 'M', avatarPath: m?.avatarPath ?? null, role: t.role, duty: t.duty, share: t.share }
  })
  const contracts = db.links.filter(l => l.projectId === p.id).map(l => {
    const c = contractOf(l.contractId)
    if (!c) return null
    return {
      id: c.id, no: c.no, name: c.name, type: c.type, partyA: c.partyA, amount: c.amount, amountNote: c.amountNote,
      signDate: c.signDate, status: c.status, isPrimary: l.isPrimary,
      files: db.contractFiles.filter(f => f.contractId === c.id).map(f => ({ id: f.id, fileName: f.fileName, sizeBytes: f.sizeBytes, uploadedAt: f.uploadedAt }))
    }
  }).filter(Boolean)
  const receipts = db.receipts.filter(r => r.projectId === p.id).map(r => {
    const c = r.contractId ? contractOf(r.contractId) : null
    return { id: r.id, date: r.date, amount: r.amount, method: r.method, handler: r.handler, note: r.note, contractId: r.contractId, invoiceNo: r.invoiceNo, isReceived: r.isReceived, receivedDate: r.receivedDate, contractNo: c?.no ?? null, contractName: c?.name ?? null }
  })
  const files = db.files.filter(f => f.projectId === p.id)
  return {
    project: {
      id: p.id, code: p.code, name: p.name, client: p.client, contactName: p.contactName, contactPhone: p.contactPhone,
      bizType: p.bizType, scale: p.scale, status: p.status, ownerMemberId: p.ownerMemberId,
      ownerName: p.ownerMemberId ? (memberOf(p.ownerMemberId)?.name ?? null) : null,
      acceptedDate: p.acceptedDate, dueDate: p.dueDate, reportDate: p.reportDate, reportNo: p.reportNo,
      submitAmount: p.submitAmount, approvedAmount: p.approvedAmount, reviewSubmitAmount: p.reviewSubmitAmount,
      fee: p.fee, description: p.description, createdAt: p.createdAt, updatedAt: p.updatedAt, createdBy: p.createdBy
    },
    team, contracts, receipts,
    receivedTotal: sum(receipts.filter(r => r.isReceived), r => r.amount),
    invoicedTotal: sum(receipts, r => r.amount),
    files,
    deliverableSets: db.sets.filter(s => s.projectId === p.id).map(s => ({ id: s.id, version: s.version, isFinal: s.isFinal, createdAt: s.createdAt })),
    docs: db.docs.filter(d => d.projectId === p.id),
    gcfxParses: []
  }
}
function contractListRow(c) {
  const links = db.links.filter(l => l.contractId === c.id)
  return {
    id: c.id, no: c.no, name: c.name, type: c.type, partyA: c.partyA, amount: c.amount, amountNote: c.amountNote,
    signDate: c.signDate, status: c.status, createdBy: c.createdBy, createdAt: c.createdAt,
    fileCount: db.contractFiles.filter(f => f.contractId === c.id).length,
    projectCount: links.length,
    projects: links.map(l => projectOf(l.projectId)?.name).filter(Boolean)
  }
}
function feesReceiptRow(r) {
  const p = projectOf(r.projectId)
  const c = r.contractId ? contractOf(r.contractId) : null
  return {
    id: r.id, date: r.date, amount: r.amount, method: r.method, handler: r.handler, note: r.note,
    invoiceNo: r.invoiceNo, isReceived: r.isReceived, receivedDate: r.receivedDate,
    projectId: r.projectId, projectCode: p?.code ?? '', projectName: p?.name ?? '',
    contractId: r.contractId, contractNo: c?.no ?? null, contractName: c?.name ?? null
  }
}
function dashboard() {
  const projects = db.projects
  const receipts = db.receipts
  const feeTotal = sum(projects, p => p.fee)
  const receivedTotal = sum(receipts.filter(r => r.isReceived), r => r.amount)
  const byStatusMap = {}
  projects.forEach(p => { byStatusMap[p.status] = (byStatusMap[p.status] || 0) + 1 })
  const byTypeMap = {}
  projects.forEach(p => { const t = byTypeMap[p.bizType] ||= { type: p.bizType, count: 0, fee: 0 }; t.count++; t.fee += p.fee || 0 })
  const monthly = []
  const base = new Date(); base.setDate(1)
  for (let i = 0; i < 6; i++) {
    const d = new Date(base.getFullYear(), base.getMonth() - (5 - i), 1)
    const ym = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`
    monthly.push({
      month: ym,
      newProjects: projects.filter(p => p.acceptedDate && p.acceptedDate.startsWith(ym)).length,
      received: sum(receipts.filter(r => r.isReceived && (r.receivedDate || r.date || '').startsWith(ym)), r => r.amount)
    })
  }
  const receivedByProject = {}
  receipts.filter(r => r.isReceived).forEach(r => { receivedByProject[r.projectId] = (receivedByProject[r.projectId] || 0) + r.amount })
  return {
    kpi: {
      projectTotal: projects.length,
      inProgress: projects.filter(p => p.status === '编制中' || p.status === '审核中').length,
      reported: projects.filter(p => p.status === '已出报告').length,
      feeTotal, receivedTotal, invoicedTotal: sum(receipts, r => r.amount), unreceived: feeTotal - receivedTotal
    },
    byStatus: Object.entries(byStatusMap).map(([status, count]) => ({ status, count })),
    byType: Object.values(byTypeMap),
    monthly,
    byMember: db.members.filter(m => m.isActive).map(m => ({
      id: m.id, name: m.name,
      projectCount: projects.filter(p => p.ownerMemberId === m.id).length,
      fee: sum(projects.filter(p => p.ownerMemberId === m.id), p => p.fee)
    })).sort((a, b) => b.projectCount - a.projectCount),
    recent: [...projects].sort((a, b) => (b.acceptedDate || '').localeCompare(a.acceptedDate || '')).slice(0, 8)
      .map(p => ({ id: p.id, code: p.code, name: p.name, bizType: p.bizType, status: p.status, acceptedDate: p.acceptedDate })),
    pending: projects.filter(p => p.status === '编制中' || p.status === '审核中').slice(0, 8)
      .map(p => ({ id: p.id, code: p.code, name: p.name, status: p.status, acceptedDate: p.acceptedDate })),
    receivedByProject
  }
}

// ---------- adapter ----------
export function guestAdapter(config) {
  if (!db) resetGuestDb()
  const method = (config.method || 'get').toLowerCase()
  let path = (config.url || '').replace(/^\/api/, '')
  const params = { ...(config.params || {}) }
  const qi = path.indexOf('?')
  if (qi >= 0) {
    new URLSearchParams(path.slice(qi + 1)).forEach((v, k) => { params[k] = v })
    path = path.slice(0, qi)
  }
  if (!path.startsWith('/')) path = '/' + path
  let body = config.data
  if (typeof body === 'string') { try { body = JSON.parse(body) } catch { body = {} } }
  const isForm = typeof FormData !== 'undefined' && body instanceof FormData
  if (isForm && config.onUploadProgress) {
    try { config.onUploadProgress({ loaded: 1, total: 1 }) } catch { /* 进度回调失败不阻塞 */ }
  }
  const J = (data, status) => respond(config, data, status)
  const M = (re) => path.match(re)
  let m

  // ===== 账号 =====
  if (path === '/auth/profile' && method === 'get') {
    return J({ id: 0, userName: 'guest', displayName: db.displayName, role: 'Member', memberId: null, createdAt: nowIso(), lastLoginAt: nowIso(), lastLoginIp: '—', member: null })
  }
  if (path === '/auth/profile' && method === 'put') {
    if (body?.displayName) db.displayName = body.displayName
    return J({ id: 0, userName: 'guest', displayName: db.displayName, role: 'Member', memberId: null, createdAt: nowIso(), lastLoginAt: nowIso(), lastLoginIp: '—', member: null })
  }
  if (path === '/auth/change-password') return fail(config, 400, '访客模式下不能修改密码')
  if (path === '/reportgen/templates' && method === 'get') return J([{ id: 'cost-audit', name: '造价审核报告', category: '造价' }])
  if (path === '/reportgen/generate') return fail(config, 400, '访客模式下不支持导出 Word（报告渲染在服务器进行）；导入、映射与预览可正常使用')
  if (path === '/auth/login') return fail(config, 400, '当前为访客模式，请先退出访客模式再登录')

  // ===== 字典 / 成员 / 账号（设置页访客进不去，这里兜底保证不报错） =====
  if (path === '/dicts' && method === 'get') return J(db.dicts)
  if (path === '/dicts' && method === 'post') { const d = { id: nid(), group: body.group, name: body.name, sort: body.sort ?? 99, isActive: true }; db.dicts.push(d); return J({ id: d.id }) }
  if ((m = M(/^\/dicts\/(\d+)$/)) && method === 'put') { const d = db.dicts.find(x => x.id === +m[1]); if (d) Object.assign(d, { name: body.name, sort: body.sort }); return J({ id: +m[1] }) }
  if ((m = M(/^\/dicts\/(\d+)$/)) && method === 'delete') { db.dicts = db.dicts.filter(x => x.id !== +m[1]); return J({ id: +m[1] }) }
  if (path === '/dicts/reorder' && method === 'post') return J({ ok: true })
  if (path === '/members' && method === 'get') {
    const rows = params.includeInactive === 'true' || params.includeInactive === true ? db.members : db.members.filter(x => x.isActive)
    return J([...rows].sort((a, b) => a.sort - b.sort))
  }
  if (path === '/members' && method === 'post') { const mm = { id: nid(), isActive: true, sort: db.members.length + 1, avatarPath: null, ...body }; db.members.push(mm); return J({ id: mm.id }) }
  if ((m = M(/^\/members\/(\d+)$/)) && method === 'put') { const mm = memberOf(+m[1]); if (mm) Object.assign(mm, body); return J({ id: +m[1] }) }
  if ((m = M(/^\/members\/(\d+)\/toggle-active$/)) && method === 'post') { const mm = memberOf(+m[1]); if (mm) mm.isActive = !mm.isActive; return J({ id: +m[1] }) }
  if (path === '/users' && method === 'get') return J([{ id: 0, userName: 'guest', displayName: db.displayName, role: 'Member', memberId: null, isActive: true, createdAt: nowIso(), lastLoginAt: nowIso(), lastLoginIp: '—' }])
  if (path.startsWith('/users')) return J({ id: 0 })

  // ===== 工作台 / 统计 =====
  if (path === '/stats/dashboard' && method === 'get') return J(dashboard())

  // ===== 项目 =====
  if (path === '/projects' && method === 'get') {
    let rows = db.projects.map(projectListRow)
    if (params.q) { const q = String(params.q); rows = rows.filter(r => r.name.includes(q) || r.client.includes(q) || r.code.includes(q)) }
    if (params.status) rows = rows.filter(r => r.status === params.status)
    if (params.bizType) rows = rows.filter(r => r.bizType === params.bizType)
    if (params.ownerId) rows = rows.filter(r => r.ownerMemberId === +params.ownerId)
    if (params.year) rows = rows.filter(r => r.acceptedDate && r.acceptedDate.startsWith(String(params.year)))
    rows.sort((a, b) => (b.acceptedDate || '').localeCompare(a.acceptedDate || '') || b.id - a.id)
    return J(rows)
  }
  if (path === '/projects' && method === 'post') {
    const year = (body.acceptedDate || today()).slice(0, 4)
    const count = db.projects.filter(p => p.code.startsWith(`ZJ-${year}-`)).length
    const p = {
      id: nid(), code: `ZJ-${year}-${String(count + 1).padStart(3, '0')}`,
      name: body.name, client: body.client, contactName: body.contactName ?? null, contactPhone: body.contactPhone ?? null,
      bizType: body.bizType, scale: body.scale ?? null, status: body.status, ownerMemberId: body.ownerMemberId ?? null,
      acceptedDate: body.acceptedDate ?? null, dueDate: body.dueDate ?? null, reportDate: body.reportDate ?? null, reportNo: body.reportNo ?? null,
      submitAmount: body.submitAmount ?? null, approvedAmount: body.approvedAmount ?? null, reviewSubmitAmount: body.reviewSubmitAmount ?? null,
      fee: body.fee ?? 0, description: body.description ?? null,
      createdBy: '访客', createdAt: nowIso(), updatedAt: nowIso(),
      team: body.ownerMemberId ? [{ memberId: body.ownerMemberId, role: '项目负责人', duty: '整体项目管理', share: 100 }] : []
    }
    db.projects.push(p)
    return J({ id: p.id, code: p.code })
  }
  if ((m = M(/^\/projects\/(\d+)$/)) && method === 'get') {
    const p = projectOf(+m[1]); return p ? J(projectDetail(p)) : notFound(config, '项目')
  }
  if ((m = M(/^\/projects\/(\d+)$/)) && method === 'put') {
    const p = projectOf(+m[1]); if (!p) return notFound(config, '项目')
    Object.assign(p, {
      name: body.name, client: body.client, contactName: body.contactName ?? null, contactPhone: body.contactPhone ?? null,
      bizType: body.bizType, scale: body.scale ?? null, status: body.status, ownerMemberId: body.ownerMemberId ?? null,
      acceptedDate: body.acceptedDate ?? null, dueDate: body.dueDate ?? null, reportDate: body.reportDate ?? null, reportNo: body.reportNo ?? null,
      submitAmount: body.submitAmount ?? null, approvedAmount: body.approvedAmount ?? null, reviewSubmitAmount: body.reviewSubmitAmount ?? null,
      fee: body.fee ?? 0, description: body.description ?? null, updatedAt: nowIso()
    })
    return J({ id: p.id })
  }
  if ((m = M(/^\/projects\/(\d+)$/)) && method === 'delete') {
    const pid = +m[1]
    db.projects = db.projects.filter(p => p.id !== pid)
    db.receipts = db.receipts.filter(r => r.projectId !== pid)
    db.links = db.links.filter(l => l.projectId !== pid)
    db.files = db.files.filter(f => f.projectId !== pid)
    db.sets = db.sets.filter(s => s.projectId !== pid)
    db.docs = db.docs.filter(d => d.projectId !== pid)
    return J({ id: pid })
  }
  if ((m = M(/^\/projects\/(\d+)\/team$/)) && method === 'put') {
    const p = projectOf(+m[1]); if (!p) return notFound(config, '项目')
    p.team = (body || []).map(it => ({ memberId: it.memberId, role: it.role, duty: it.duty ?? null, share: it.share }))
    return J({ count: p.team.length })
  }

  // ===== 收款（开票/确认收款/编辑/删除） =====
  if ((m = M(/^\/projects\/(\d+)\/receipts$/)) && method === 'post') {
    const r = { id: nid(), projectId: +m[1], contractId: body.contractId ?? null, date: body.date, amount: body.amount, method: body.method ?? null, handler: body.handler ?? null, note: body.note ?? null, invoiceNo: body.invoiceNo ?? null, isReceived: false, receivedDate: null }
    db.receipts.push(r)
    return J({ id: r.id })
  }
  if ((m = M(/^\/projects\/(\d+)\/receipts\/(\d+)$/)) && method === 'put') {
    const r = db.receipts.find(x => x.id === +m[2] && x.projectId === +m[1]); if (!r) return notFound(config, '收款记录')
    Object.assign(r, { date: body.date, amount: body.amount, method: body.method ?? null, handler: body.handler ?? null, note: body.note ?? null, contractId: body.contractId ?? null, invoiceNo: body.invoiceNo ?? null, isReceived: !!body.isReceived, receivedDate: body.isReceived ? (body.receivedDate ?? r.receivedDate ?? today()) : null })
    return J({ id: r.id })
  }
  if ((m = M(/^\/projects\/(\d+)\/receipts\/(\d+)\/confirm$/)) && method === 'post') {
    const r = db.receipts.find(x => x.id === +m[2] && x.projectId === +m[1]); if (!r) return notFound(config, '收款记录')
    r.isReceived = true; r.receivedDate = body?.receivedDate ?? today()
    return J({ id: r.id, isReceived: true, receivedDate: r.receivedDate })
  }
  if ((m = M(/^\/projects\/(\d+)\/receipts\/(\d+)$/)) && method === 'delete') {
    db.receipts = db.receipts.filter(x => x.id !== +m[2])
    return J({ id: +m[2] })
  }

  // ===== 成果文件 / 项目资料 =====
  const ensureSet = (pid) => {
    let s = [...db.sets.filter(x => x.projectId === pid)].sort((a, b) => b.version - a.version)[0]
    if (!s) { s = { id: nid(), projectId: pid, version: 1, isFinal: false, createdAt: nowIso() }; db.sets.push(s) }
    return s
  }
  if ((m = M(/^\/projects\/(\d+)\/files\/deliverables$/)) && method === 'post') {
    const file = body.get('file'); const category = body.get('category')
    const set = ensureSet(+m[1])
    const ver = Math.max(0, ...db.files.filter(f => f.projectId === +m[1] && f.category === category && f.setId === set.id).map(f => f.version)) + 1
    const f = { id: nid(), projectId: +m[1], setId: set.id, category, fileName: file.name, sizeBytes: file.size, uploadedBy: '访客', uploadedAt: nowIso(), version: ver }
    db.files.push(f); db.blobs.set('deliverable:' + f.id, file)
    return J({ id: f.id })
  }
  if ((m = M(/^\/projects\/(\d+)\/files\/new-version$/)) && method === 'post') {
    const cur = ensureSet(+m[1])
    if (!db.files.some(f => f.projectId === +m[1] && f.setId === cur.id)) return J({ version: cur.version, created: false })
    db.sets.forEach(s => { if (s.projectId === +m[1]) s.isFinal = false })
    const next = { id: nid(), projectId: +m[1], version: cur.version + 1, isFinal: false, createdAt: nowIso() }
    db.sets.push(next)
    return J({ version: next.version, created: true })
  }
  if ((m = M(/^\/projects\/(\d+)\/files\/sets\/(\d+)\/set-final$/)) && method === 'post') {
    db.sets.forEach(s => { if (s.projectId === +m[1]) s.isFinal = s.id === +m[2] })
    return J({ id: +m[2] })
  }
  if ((m = M(/^\/projects\/(\d+)\/files\/deliverables\/(\d+)$/)) && method === 'delete') {
    db.files = db.files.filter(f => f.id !== +m[2]); db.blobs.delete('deliverable:' + m[2])
    return J({ id: +m[2] })
  }
  if ((m = M(/^\/projects\/(\d+)\/files\/deliverables\/(\d+)\/download$/)) && method === 'get') {
    const b = db.blobs.get('deliverable:' + m[2]); return b ? J(b) : notFound(config, '文件')
  }
  if ((m = M(/^\/projects\/(\d+)\/files\/docs$/)) && method === 'post') {
    const file = body.get('file')
    const d = { id: nid(), projectId: +m[1], category: body.get('category'), fileName: file.name, sizeBytes: file.size, uploadedBy: '访客', uploadedAt: nowIso() }
    db.docs.push(d); db.blobs.set('doc:' + d.id, file)
    return J({ id: d.id, parseId: null })
  }
  if ((m = M(/^\/projects\/(\d+)\/files\/docs\/(\d+)\/category$/)) && method === 'put') {
    const d = db.docs.find(x => x.id === +m[2]); if (d) d.category = body.category
    return J({ id: +m[2] })
  }
  if ((m = M(/^\/projects\/(\d+)\/files\/docs\/(\d+)$/)) && method === 'delete') {
    db.docs = db.docs.filter(x => x.id !== +m[2]); db.blobs.delete('doc:' + m[2])
    return J({ id: +m[2] })
  }
  if ((m = M(/^\/projects\/(\d+)\/files\/docs\/(\d+)\/download$/)) && method === 'get') {
    const b = db.blobs.get('doc:' + m[2]); return b ? J(b) : notFound(config, '文件')
  }
  if (/\/files\/(docs\/\d+|deliverables\/\d+)\/parse$/.test(path) || /\/reparse$/.test(path)) {
    return fail(config, 400, '访客模式下不支持造价解析')
  }

  // ===== 合同 =====
  if (path === '/contracts' && method === 'get') {
    let rows = db.contracts.map(contractListRow)
    if (params.q) { const q = String(params.q); rows = rows.filter(r => r.name.includes(q) || r.no.includes(q) || (r.partyA || '').includes(q)) }
    if (params.type) rows = rows.filter(r => r.type === params.type)
    rows.sort((a, b) => (b.signDate || '').localeCompare(a.signDate || '') || b.id - a.id)
    return J(rows)
  }
  if (path === '/contracts' && method === 'post') {
    const year = (body.signDate || today()).slice(0, 4)
    const c = {
      id: nid(), no: body.no || `HT-${year}-${String(db.contracts.length + 1).padStart(3, '0')}`,
      name: body.name, type: body.type, partyA: body.partyA ?? null, amount: body.amount ?? null,
      amountNote: body.amountNote ?? null, signDate: body.signDate ?? null, status: body.status,
      createdBy: '访客', createdAt: nowIso()
    }
    db.contracts.push(c)
    return J({ id: c.id })
  }
  if ((m = M(/^\/contracts\/(\d+)$/)) && method === 'get') {
    const c = contractOf(+m[1]); if (!c) return notFound(config, '合同')
    const projects = db.links.filter(l => l.contractId === c.id).map(l => {
      const p = projectOf(l.projectId); if (!p) return null
      const recs = db.receipts.filter(r => r.projectId === p.id)
      const crec = recs.filter(r => r.contractId === c.id)
      return { id: p.id, code: p.code, name: p.name, client: p.client, bizType: p.bizType, status: p.status, fee: p.fee, isPrimary: l.isPrimary, receivedTotal: sum(recs.filter(r => r.isReceived), r => r.amount), contractReceived: sum(crec.filter(r => r.isReceived), r => r.amount), contractInvoiced: sum(crec, r => r.amount) }
    }).filter(Boolean)
    const receipts = db.receipts.filter(r => r.contractId === c.id).map(r => {
      const p = projectOf(r.projectId)
      return { id: r.id, projectId: r.projectId, projectCode: p?.code ?? '', projectName: p?.name ?? '', date: r.date, amount: r.amount, method: r.method, handler: r.handler, note: r.note, invoiceNo: r.invoiceNo, isReceived: r.isReceived, receivedDate: r.receivedDate }
    })
    return J({
      contract: { id: c.id, no: c.no, name: c.name, type: c.type, partyA: c.partyA, amount: c.amount, amountNote: c.amountNote, signDate: c.signDate, status: c.status, createdAt: c.createdAt, createdBy: c.createdBy },
      files: db.contractFiles.filter(f => f.contractId === c.id),
      projects, receipts,
      receivedTotal: sum(receipts.filter(r => r.isReceived), r => r.amount),
      invoicedTotal: sum(receipts, r => r.amount)
    })
  }
  if ((m = M(/^\/contracts\/(\d+)$/)) && method === 'put') {
    const c = contractOf(+m[1]); if (!c) return notFound(config, '合同')
    Object.assign(c, { no: body.no, name: body.name, type: body.type, partyA: body.partyA ?? null, amount: body.amount ?? null, amountNote: body.amountNote ?? null, signDate: body.signDate ?? null, status: body.status })
    return J({ id: c.id })
  }
  if ((m = M(/^\/contracts\/(\d+)$/)) && method === 'delete') {
    const cid = +m[1]
    db.contracts = db.contracts.filter(c => c.id !== cid)
    db.links = db.links.filter(l => l.contractId !== cid)
    db.contractFiles = db.contractFiles.filter(f => f.contractId !== cid)
    return J({ id: cid })
  }
  if ((m = M(/^\/contracts\/(\d+)\/link$/)) && method === 'post') {
    if (db.links.some(l => l.contractId === +m[1] && l.projectId === body.projectId)) return fail(config, 400, '已关联该项目')
    db.links.push({ projectId: body.projectId, contractId: +m[1], isPrimary: !!body.isPrimary })
    return J({ ok: true })
  }
  if ((m = M(/^\/contracts\/(\d+)\/link\/(\d+)$/)) && method === 'delete') {
    db.links = db.links.filter(l => !(l.contractId === +m[1] && l.projectId === +m[2]))
    return J({ ok: true })
  }
  if ((m = M(/^\/contracts\/(\d+)\/files$/)) && method === 'get') {
    return J(db.contractFiles.filter(f => f.contractId === +m[1]))
  }
  if ((m = M(/^\/contracts\/(\d+)\/files$/)) && method === 'post') {
    const file = body.get('file')
    const f = { id: nid(), contractId: +m[1], fileName: file.name, sizeBytes: file.size, uploadedBy: '访客', uploadedAt: nowIso() }
    db.contractFiles.push(f); db.blobs.set('contractFile:' + f.id, file)
    return J({ id: f.id })
  }
  if ((m = M(/^\/contracts\/(\d+)\/files\/(\d+)$/)) && method === 'delete') {
    db.contractFiles = db.contractFiles.filter(f => f.id !== +m[2]); db.blobs.delete('contractFile:' + m[2])
    return J({ id: +m[2] })
  }
  if ((m = M(/^\/contracts\/(\d+)\/files\/(\d+)\/download$/)) && method === 'get') {
    const b = db.blobs.get('contractFile:' + m[2]); return b ? J(b) : notFound(config, '文件')
  }

  // ===== 收费台账 =====
  if (path === '/fees' && method === 'get') {
    const rows = db.projects.map(p => {
      const recs = db.receipts.filter(r => r.projectId === p.id)
      return {
        id: p.id, code: p.code, name: p.name, client: p.client, status: p.status, fee: p.fee,
        received: sum(recs.filter(r => r.isReceived), r => r.amount),
        invoiced: sum(recs, r => r.amount),
        lastReceiptDate: recs.length ? recs.map(r => r.date).sort().slice(-1)[0] : null,
        receiptCount: recs.length
      }
    })
    return J({ rows, totals: { feeTotal: sum(rows, r => r.fee), receivedTotal: sum(rows, r => r.received), projectCount: rows.length } })
  }
  if (path === '/fees/receipts' && method === 'get') {
    let rows = db.receipts.map(feesReceiptRow)
    if (params.projectId) rows = rows.filter(r => r.projectId === +params.projectId)
    if (params.contractId) rows = rows.filter(r => r.contractId === +params.contractId)
    if (params.q) { const q = String(params.q); rows = rows.filter(r => r.projectName.includes(q) || r.projectCode.includes(q) || (r.contractNo || '').includes(q) || (r.contractName || '').includes(q)) }
    rows.sort((a, b) => (b.date || '').localeCompare(a.date || '') || b.id - a.id)
    return J({ rows, total: sum(rows, r => r.amount), receivedTotal: sum(rows.filter(r => r.isReceived), r => r.amount), count: rows.length })
  }

  // ===== 资料库 =====
  if (path === '/library' && method === 'get') {
    let rows = [...db.library]
    if (params.category) rows = rows.filter(r => r.category === params.category)
    if (params.q) rows = rows.filter(r => r.name.includes(String(params.q)))
    rows.sort((a, b) => (b.isPinned - a.isPinned) || (b.uploadedAt || '').localeCompare(a.uploadedAt || ''))
    return J(rows)
  }
  if (path === '/library' && method === 'post') {
    const file = body.get('file')
    const d = {
      id: nid(), name: body.get('name') || file.name.replace(/\.[^.]+$/, ''), category: body.get('category'),
      format: (file.name.split('.').pop() || '').toUpperCase(), sizeBytes: file.size,
      uploadedBy: '访客', uploadedAt: nowIso(), isPinned: false, description: null
    }
    db.library.push(d); db.blobs.set('library:' + d.id, file)
    return J({ id: d.id })
  }
  if ((m = M(/^\/library\/(\d+)$/)) && method === 'put') {
    const d = db.library.find(x => x.id === +m[1]); if (d) Object.assign(d, { name: body.name, category: body.category, description: body.description || null, isPinned: !!body.isPinned })
    return J({ id: +m[1] })
  }
  if ((m = M(/^\/library\/(\d+)$/)) && method === 'delete') {
    db.library = db.library.filter(x => x.id !== +m[1]); db.blobs.delete('library:' + m[1])
    return J({ id: +m[1] })
  }
  if ((m = M(/^\/library\/(\d+)\/download$/)) && method === 'get') {
    const b = db.blobs.get('library:' + m[1]); return b ? J(b) : notFound(config, '文件')
  }

  // ===== 流程模板 =====
  if (path === '/forms/templates' && method === 'get') return J(FORM_TEMPLATES)
  if (path === '/forms/records' && method === 'get') {
    let rows = [...db.records]
    if (params.templateId) rows = rows.filter(r => r.templateId === +params.templateId)
    if (params.q) { const q = String(params.q); rows = rows.filter(r => r.no.includes(q) || (r.filledByName || '').includes(q)) }
    rows.sort((a, b) => b.id - a.id)
    return J(rows)
  }
  if (path === '/forms/records' && method === 'post') {
    const tpl = FORM_TEMPLATES.find(t => t.id === body.templateId); if (!tpl) return fail(config, 400, '模板不存在')
    const year = new Date().getFullYear()
    const count = db.records.filter(r => r.no.startsWith(`${tpl.code}-${year}-`)).length
    const byKey = JSON.parse(tpl.schemaJson).byKey
    const rec = {
      id: nid(), no: `${tpl.code}-${year}-${String(count + 1).padStart(3, '0')}`, templateId: tpl.id,
      templateName: tpl.name, templateKey: tpl.key,
      dataJson: JSON.stringify(body.data || {}),
      filledByName: byKey ? (body.data?.[byKey] ?? null) : null,
      createdAt: nowIso(), updatedAt: nowIso()
    }
    db.records.push(rec)
    return J({ id: rec.id, no: rec.no })
  }
  if ((m = M(/^\/forms\/records\/(\d+)$/)) && method === 'put') {
    const r = db.records.find(x => x.id === +m[1]); if (r) { r.dataJson = JSON.stringify(body.data || {}); r.updatedAt = nowIso() }
    return J({ id: +m[1] })
  }
  if ((m = M(/^\/forms\/records\/(\d+)$/)) && method === 'delete') {
    db.records = db.records.filter(x => x.id !== +m[1])
    return J({ id: +m[1] })
  }

  return fail(config, 404, '访客模式下该功能不可用')
}
