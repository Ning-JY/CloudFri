<script setup>
import { ref, computed, onMounted, h, nextTick } from 'vue'
import { NButton, NTag } from 'naive-ui'
import { useRoute, useRouter } from 'vue-router'
import { useMessage, useDialog } from 'naive-ui'
import api, { errMsg } from '../api'
import { fmt, fmtSize, avatarFor, cutRate, reviewCutRate, fileKind, todayStr, iconBtn, thUnit } from '../utils'
import PreviewModal from '../components/PreviewModal.vue'
import ReceiptEditModal from '../components/ReceiptEditModal.vue'
import GcfxPanel from '../components/GcfxPanel.vue'
import { useDicts } from '../stores/dicts'
import TableTools from '../components/TableTools.vue'
import Pager from '../components/Pager.vue'
import UploadBtn from '../components/UploadBtn.vue'
import { usePager } from '../composables/usePager'

const route = useRoute()
const router = useRouter()
function goBack() { router.push('/projects') }
const message = useMessage()
const dialog = useDialog()
const id = Number(route.params.id)

const d = ref(null)
const members = ref([])
const allContracts = ref([])
const previewRef = ref(null)
// 业务类型/状态/团队角色/资料分类统一走全站字典（设置里维护后即生效），store 有兜底值
const dicts = useDicts()
const BIZ_TYPES = computed(() => dicts.names('BizType'))
const STATUSES = computed(() => dicts.names('ProjectStatus'))
const ROLES = computed(() => dicts.names('TeamRole'))
const DOC_CATS = computed(() => dicts.names('DocCategory'))
const FILE_CATS = [
  { key: 'Software', label: '软件版', icon: 'GCFX', cls: 'cg' },
  { key: 'Document', label: '文档版', icon: 'XLS', cls: 'cb' },
  { key: 'Report', label: '报告', icon: 'PDF', cls: 'co' }
]
const catIcon = (key) => FILE_CATS.find(c => c.key === key)?.icon || 'FILE'
const catCls = (key) => FILE_CATS.find(c => c.key === key)?.cls || 'cg'
const showFeeDetail = ref(false)

async function load() {
  // 带时间戳参数，避免浏览器缓存旧详情（重传 GCFX 后解析不刷新的投诉对策）
  const { data } = await api.get('/projects/' + id, { params: { t: Date.now() } })
  d.value = data
}
onMounted(async () => {
  const [m, c] = await Promise.all([api.get('/members'), api.get('/contracts')])
  members.value = m.data
  allContracts.value = c.data
  await load()
})

const p = computed(() => d.value?.project)
const isAudit = computed(() => p.value && (p.value.bizType === '结算审核' || p.value.bizType === '竣工决算'))
const isProcess = computed(() => p.value && (p.value.bizType === '跟踪审计' || p.value.bizType === '全过程咨询'))
const isReview = computed(() => p.value && p.value.bizType === '预算评审')
const contractAmount = computed(() => {
  if (!d.value) return null
  const cs = d.value.contracts
  if (!cs.length) return null
  if (cs.some(c => c.amount == null)) return '按实结算'
  return cs.reduce((a, c) => a + (c.amount || 0), 0)
})
const received = computed(() => d.value?.receivedTotal || 0)
const invoiced = computed(() => d.value?.invoicedTotal || 0)
// 收费圆环双段（v1.1 两段式）：深绿=已收、橙=已开票未收，占比均以应收咨询费为分母
const RING_C = 251.3
const feePct = computed(() => (p.value && p.value.fee > 0) ? Math.min(100, Math.round(received.value / p.value.fee * 100)) : 0)
const recvArc = computed(() => (RING_C * Math.min(received.value, p.value?.fee || 0) / (p.value?.fee || 1)).toFixed(1))
const invArc = computed(() => (RING_C * Math.max(Math.min(invoiced.value, p.value?.fee || 0) - Math.min(received.value, p.value?.fee || 0), 0) / (p.value?.fee || 1)).toFixed(1))
const lastReceipt = computed(() => {
  const rs = d.value?.receipts || []
  return rs.length ? rs[rs.length - 1] : null
})
const stepIdx = computed(() => p.value ? STATUSES.value.indexOf(p.value.status) : 0)
const shareTotal = computed(() => (d.value?.team || []).reduce((a, t) => a + (Number(t.share) || 0), 0))

const amounts = computed(() => {
  if (!p.value) return []
  const cr = cutRate(p.value)
  if (isAudit.value) return [
    { lb: '送审金额', vl: fmt(p.value.submitAmount) + ' 万', sb: '委托方送审', field: 'submitAmount', num: p.value.submitAmount },
    { lb: '审定金额', vl: fmt(p.value.approvedAmount) + ' 万', sb: p.value.reportNo ? '报告 ' + p.value.reportNo : '审定结果', field: 'approvedAmount', num: p.value.approvedAmount },
    { lb: '核减额 / 核减率', vl: (p.value.submitAmount != null && p.value.approvedAmount != null) ? fmt(p.value.submitAmount - p.value.approvedAmount) + ' 万' : '—', sb: cr != null ? '核减率 ' + cr.toFixed(2) + '%' : '—', field: null },
    { lb: '咨询费（应收 / 已收）', vl: fmt(p.value.fee) + ' 万', sb: '已收 ' + fmt(received.value) + ' 万', field: 'fee', num: p.value.fee }
  ]
  if (isProcess.value) return [
    { lb: '建安造价', vl: fmt(p.value.submitAmount) + ' 万', sb: '过程控制对象', field: 'submitAmount', num: p.value.submitAmount },
    { lb: '合同金额', vl: contractAmount.value === '按实结算' ? '按实结算' : fmt(contractAmount.value) + ' 万', sb: '关联合同合计', field: null },
    { lb: '咨询费应收', vl: fmt(p.value.fee) + ' 万', sb: '按合同约定', field: 'fee', num: p.value.fee },
    { lb: '已收咨询费', vl: fmt(received.value) + ' 万', sb: '累计收款', field: null }
  ]
  if (isReview.value) {
    const rcr = reviewCutRate(p.value)
    return [
      { lb: '送审金额', vl: fmt(p.value.reviewSubmitAmount) + ' 万', sb: '委托方送审', field: 'reviewSubmitAmount', num: p.value.reviewSubmitAmount },
      { lb: '预算金额', vl: fmt(p.value.submitAmount) + ' 万', sb: '编制成果', field: 'submitAmount', num: p.value.submitAmount },
      { lb: '审减额 / 审减率', vl: (p.value.reviewSubmitAmount != null && p.value.submitAmount != null) ? fmt(p.value.reviewSubmitAmount - p.value.submitAmount) + ' 万' : '—', sb: rcr != null ? '审减率 ' + rcr.toFixed(2) + '%' : '—', field: null },
      { lb: '咨询费（应收 / 已收）', vl: fmt(p.value.fee) + ' 万', sb: '已收 ' + fmt(received.value) + ' 万', field: 'fee', num: p.value.fee }
    ]
  }
  return [
    { lb: p.value.bizType === '清单控制价' ? '控制价金额' : '预算金额', vl: fmt(p.value.submitAmount) + ' 万', sb: '编制成果', field: 'submitAmount', num: p.value.submitAmount },
    { lb: '合同金额', vl: contractAmount.value === '按实结算' ? '按实结算' : (contractAmount.value == null ? '—' : fmt(contractAmount.value) + ' 万'), sb: '关联合同合计', field: null },
    { lb: '咨询费应收', vl: fmt(p.value.fee) + ' 万', sb: '按合同约定', field: 'fee', num: p.value.fee },
    { lb: '已收咨询费', vl: fmt(received.value) + ' 万', sb: '累计收款', field: null }
  ]
})

// 头部金额直接修改：点金额卡的数值就地编辑（送审类/审定/咨询费应收），保存走整表更新接口
const editingAmt = ref(null)
const editAmtVal = ref(null)
function projectPayload(v, overrides = {}) {
  return {
    name: v.name, client: v.client, contactName: v.contactName, contactPhone: v.contactPhone,
    bizType: v.bizType, scale: v.scale, status: v.status, ownerMemberId: v.ownerMemberId,
    acceptedDate: v.acceptedDate, dueDate: v.dueDate, reportDate: v.reportDate, reportNo: v.reportNo,
    submitAmount: v.submitAmount, approvedAmount: v.approvedAmount, fee: v.fee || 0, description: v.description,
    reviewSubmitAmount: v.reviewSubmitAmount ?? null,
    ...overrides
  }
}
function startAmtEdit(a) { editingAmt.value = a.field; editAmtVal.value = a.num ?? null }
async function saveAmtEdit(a) {
  try {
    await api.put('/projects/' + id, projectPayload(p.value, { [a.field]: a.field === 'fee' ? (editAmtVal.value || 0) : editAmtVal.value }))
    message.success(a.lb + '已更新')
    editingAmt.value = null
    await load()
  } catch (e) { message.error(errMsg(e)) }
}

// 成果文件整体版本：三类文件同属一个版本集合；SetId 为空的旧文件按最早集合（V1）处理
// 当前版本：被设为最终版的集合优先，否则版本号最大者
const sets = computed(() => d.value?.deliverableSets || [])
const currentSet = computed(() => sets.value.length ? (sets.value.find(s => s.isFinal) || sets.value[sets.value.length - 1]) : null)
const currentVersionNo = computed(() => currentSet.value?.version ?? 1)
const effSetId = (f) => f.setId ?? (sets.value[0]?.id ?? 0)
const currentSetKey = computed(() => currentSet.value?.id ?? (sets.value[0]?.id ?? 0))
const curFiles = computed(() => (d.value?.files || []).filter(f => effSetId(f) === currentSetKey.value))
const fileGroups = computed(() => {
  if (!d.value) return []
  return FILE_CATS.map(c => {
    const fs = curFiles.value.filter(f => f.category === c.key)
    return { ...c, cur: fs[0] || null, extra: fs.slice(1) }
  })
})
const gotCount = computed(() => fileGroups.value.filter(g => g.cur).length)
const catLabel = (key) => FILE_CATS.find(c => c.key === key)?.label || key
const historyGroups = computed(() => {
  if (!d.value) return []
  const groups = []
  const replaced = fileGroups.value.flatMap(g => g.extra.map(f => ({ ...f, catLabel: g.label })))
  if (replaced.length) groups.push({ key: 'rep', label: `当前版本 V${currentVersionNo.value} · 已替换的旧文件`, files: replaced })
  const older = sets.value.filter(s => s.id !== currentSet.value?.id).sort((a, b) => b.version - a.version)
  for (const s of older) {
    const fs = (d.value.files || []).filter(f => effSetId(f) === s.id).map(f => ({ ...f, catLabel: catLabel(f.category) }))
    if (fs.length) groups.push({ key: 'set' + s.id, setId: s.id, label: `历史版本 V${s.version}`, files: fs })
  }
  return groups
})
// 把历史版本设为最终版（重新成为当前版本，承接后续上传）
function setFinal(hg) {
  dialog.warning({
    title: '设为最终版', content: `把「${hg.label}」设为最终版？它将重新成为当前版本，后续上传的成果文件会进入该版本；现在的当前版本转入历史。`,
    positiveText: '设为最终版', negativeText: '取消',
    onPositiveClick: async () => {
      try {
        await api.post(`/projects/${id}/files/sets/${hg.setId}/set-final`)
        message.success('已设为最终版')
        await load()
      } catch (e) { message.error(errMsg(e)) }
    }
  })
}
// 删除项目（连同团队、合同关联、收款、成果与资料一并删除，不可恢复）
function delProject() {
  dialog.error({
    title: '删除项目', content: `确定删除项目「${p.value.name}」（${p.value.code}）？其团队分工、合同关联、收款记录、成果文件与项目资料将一并删除，不可恢复。`,
    positiveText: '删除', negativeText: '取消',
    onPositiveClick: async () => {
      try {
        await api.delete('/projects/' + id)
        message.success('项目已删除')
        router.push('/projects')
      } catch (e) { message.error(errMsg(e)) }
    }
  })
}
const parseOf = (fileId) => (d.value?.gcfxParses || []).find(g => g.projectFileId === fileId)
const parseOfDoc = (docId) => (d.value?.gcfxParses || []).find(g => g.projectDocId === docId)
const isGcfxName = (name) => /\.(gcfx|sgcfx)$/i.test(String(name || ''))
// 解析卡当前选中的文件（点某个文件的「解析」按钮时锁定；为空时按默认规则：当前版本软件版 → 最近一次解析）
const gcfxSel = ref(null)
// 造价解析卡片展示对象：优先用户选中的文件，否则当前版本软件版文件的解析，再否则取最近一次解析（成果与资料都算）
const gcfxTarget = computed(() => {
  if (!d.value) return null
  if (gcfxSel.value) {
    const pr = (d.value.gcfxParses || []).find(g => gcfxSel.value.kind === 'file' ? g.projectFileId === gcfxSel.value.id : g.projectDocId === gcfxSel.value.id)
    if (pr) {
      const fname = gcfxSel.value.kind === 'file'
        ? d.value.files.find(x => x.id === gcfxSel.value.id)?.fileName
        : d.value.docs.find(x => x.id === gcfxSel.value.id)?.fileName
      return { parse: pr, fileName: fname || '', kind: gcfxSel.value.kind, refId: gcfxSel.value.id }
    }
  }
  const sw = fileGroups.value.find(g => g.key === 'Software')?.cur
  if (sw) { const pr = parseOf(sw.id); if (pr) return { parse: pr, fileName: sw.fileName, kind: 'file', refId: sw.id } }
  const all = [...(d.value.gcfxParses || [])].sort((a, b) => String(b.parsedAt).localeCompare(String(a.parsedAt)))
  if (all.length) {
    const pr = all[0]
    const isFile = pr.projectFileId != null
    const f = isFile ? d.value.files.find(x => x.id === pr.projectFileId) : d.value.docs.find(x => x.id === pr.projectDocId)
    return { parse: pr, fileName: f?.fileName || '', kind: isFile ? 'file' : 'doc', refId: isFile ? pr.projectFileId : pr.projectDocId }
  }
  return null
})
// 项目内全部 GCFX/SGCFX 文件（成果 + 资料），供解析卡空状态列出待解析文件
const gcfxFilesAll = computed(() => {
  if (!d.value) return []
  const fs = d.value.files.filter(f => isGcfxName(f.fileName)).map(f => ({ kind: 'file', id: f.id, name: f.fileName, parsed: !!parseOf(f.id), from: '成果文件' }))
  const ds = d.value.docs.filter(x => isGcfxName(x.fileName)).map(x => ({ kind: 'doc', id: x.id, name: x.fileName, parsed: !!parseOfDoc(x.id), from: '项目资料' }))
  return [...fs, ...ds]
})
const docGroups = computed(() => {
  if (!d.value) return []
  return DOC_CATS.value.map(cat => ({ cat, files: d.value.docs.filter(x => x.category === cat) }))
})
// 更改资料分类（传错分类不用删了重传）
const showDocCat = ref(false)
const dcFile = ref(null)
const dcCat = ref('')
function openDocCatEdit(f) { dcFile.value = f; dcCat.value = f.category; showDocCat.value = true }
async function saveDocCat() {
  try {
    await api.put(`/projects/${id}/files/docs/${dcFile.value.id}/category`, { category: dcCat.value })
    message.success('分类已改为「' + dcCat.value + '」')
    openDocCat.value = dcCat.value
    showDocCat.value = false
    await load()
  } catch (e) { message.error(errMsg(e)) }
}
const openDocCat = ref('')
const histOpen = ref({})
function toggleHist(key) { histOpen.value = { ...histOpen.value, [key]: !histOpen.value[key] } }

async function downloadFile(kind, fid, name) {
  const url = kind === 'lib' ? `/library/${fid}/download` : `/projects/${id}/files/${kind}/${fid}/download`
  const { data } = await api.get(url, { responseType: 'blob' })
  const a = document.createElement('a')
  a.href = URL.createObjectURL(data)
  a.download = name
  a.click()
  setTimeout(() => URL.revokeObjectURL(a.href), 800)
}
function previewFile(kind, f) {
  previewRef.value.open(`/projects/${id}/files/${kind}/${f.id}/download`, f.fileName)
}

// 上传（带进度，走全站共用 UploadBtn）：函数只负责发请求并返回结果，成功提示在 @done 里按结果给
async function uploadDeliverable(cat, file, onProgress) {
  const fd = new FormData()
  fd.append('category', cat)
  fd.append('file', file)
  const { data } = await api.post(`/projects/${id}/files/deliverables`, fd, { onUploadProgress: (e) => { if (e.total) onProgress(Math.round(e.loaded / e.total * 100)) } })
  return data
}
async function onDeliverableUploaded(results) {
  const data = results?.[0]
  if (data?.filledField) message.success(`上传成功，已自动解析 GCFX，${data.filledField}已回填 ${fmt(data.filledAmountWan)} 万`)
  else message.success(data?.parseId ? '上传成功，已自动解析 GCFX' : '上传成功，已存入当前版本')
  await load()
}
async function uploadDoc(cat, file, onProgress) {
  const fd = new FormData()
  fd.append('category', cat)
  fd.append('file', file)
  const { data } = await api.post(`/projects/${id}/files/docs`, fd, { onUploadProgress: (e) => { if (e.total) onProgress(Math.round(e.loaded / e.total * 100)) } })
  return data
}
async function onDocUploaded(cat, results) {
  message.success(results?.[0]?.parseId ? '已归档到「' + cat + '」，GCFX 已自动解析' : '已归档到「' + cat + '」')
  openDocCat.value = cat
  await load()
}
// 新增成果版本：当前三类成果整体转历史，开启新版本
function newVersion() {
  if (!fileGroups.value.some(g => g.cur)) { message.info('当前版本还没有成果文件，先上传文件再新增版本'); return }
  dialog.warning({
    title: '新增版本', content: `把当前成果（软件版 / 文档版 / 报告）整体转入历史版本，并开始 V${currentVersionNo.value + 1}？`,
    positiveText: '新增版本', negativeText: '取消',
    onPositiveClick: async () => {
      try {
        await api.post(`/projects/${id}/files/new-version`)
        message.success(`已开启 V${currentVersionNo.value + 1}，原成果整体转入历史版本`)
        await load()
      } catch (e) { message.error(errMsg(e)) }
    }
  })
}
function scrollToParse() {
  document.getElementById('gcfxPanel')?.scrollIntoView({ behavior: 'smooth', block: 'start' })
}
// 选中某个文件的解析并滚动到解析卡（成果/资料文件行上的「解析」按钮：已解析直接看，未解析先解析）
async function selectParse(kind, refId) {
  gcfxSel.value = { kind, id: refId }
  await nextTick()
  scrollToParse()
}
async function parseFile(kind, refId) {
  try {
    const url = kind === 'file' ? `/projects/${id}/files/deliverables/${refId}/reparse` : `/projects/${id}/files/docs/${refId}/parse`
    const { data } = await api.post(url)
    message.success(`解析完成，工程总造价 ¥${fmt(data.totalAmount)}` + (data.filledField ? `，${data.filledField}已回填 ${fmt(data.filledAmountWan)} 万` : ''))
    await load()
    await selectParse(kind, refId)
  } catch (e) { message.error(errMsg(e, '解析失败')) }
}
function onParseBtn(kind, refId) {
  const parsed = kind === 'file' ? parseOf(refId) : parseOfDoc(refId)
  if (parsed) selectParse(kind, refId)
  else parseFile(kind, refId)
}
// 重新解析当前展示的 GCFX（解析器升级后刷新旧结果；成果与资料走各自接口，共用上面的 parseFile）
async function reparse() {
  const t = gcfxTarget.value
  if (!t) return
  await parseFile(t.kind, t.refId)
}
async function downloadContractFile(c, f) {
  const { data } = await api.get(`/contracts/${c.id}/files/${f.id}/download`, { responseType: 'blob' })
  const a = document.createElement('a')
  a.href = URL.createObjectURL(data)
  a.download = f.fileName
  a.click()
  setTimeout(() => URL.revokeObjectURL(a.href), 800)
}
function confirmDelete(text, fn) {
  dialog.warning({ title: '确认删除', content: text, positiveText: '删除', negativeText: '取消', onPositiveClick: fn })
}

// 更改状态（头部按钮下拉直接切换，不必打开编辑弹窗）
const statusOptions = computed(() => STATUSES.value.map(s => ({ label: s, key: s, disabled: p.value?.status === s })))
async function changeStatus(key) {
  try {
    await api.put('/projects/' + id, projectPayload(p.value, { status: key }))
    message.success('状态已改为「' + key + '」')
    await load()
  } catch (e) { message.error(errMsg(e)) }
}

// 编辑项目
const showEdit = ref(false)
const ef = ref({})
function openEdit() {
  ef.value = { ...p.value }
  showEdit.value = true
}
async function saveEdit() {
  try {
    await api.put('/projects/' + id, projectPayload(ef.value))
    message.success('已保存')
    showEdit.value = false
    await load()
  } catch (e) { message.error(errMsg(e)) }
}

// 团队编辑
const showTeam = ref(false)
const teamRows = ref([])
function openTeam() {
  teamRows.value = d.value.team.map(t => ({ memberId: t.memberId, role: t.role, duty: t.duty || '', share: t.share }))
  if (!teamRows.value.length) teamRows.value = [{ memberId: null, role: '编制人', duty: '', share: 40 }]
  showTeam.value = true
}
async function saveTeam() {
  const items = teamRows.value.filter(r => r.memberId)
  try {
    await api.put(`/projects/${id}/team`, items)
    message.success('团队已更新')
    showTeam.value = false
    await load()
  } catch (e) { message.error(errMsg(e)) }
}

// 合同关联
const showLink = ref(false)
const linkForm = ref({ contractId: null, isPrimary: false })
async function doLink() {
  try {
    await api.post(`/contracts/${linkForm.value.contractId}/link`, { projectId: id, isPrimary: linkForm.value.isPrimary })
    message.success('已关联')
    showLink.value = false
    await load()
  } catch (e) { message.error(errMsg(e)) }
}
async function unlink(contractId) {
  await api.delete(`/contracts/${contractId}/link/${id}`)
  message.success('已取消关联')
  await load()
}

// 开票 / 确认收款两段式（v1.1）：先登记开票（发票号+开票日期），钱到账后再确认收款
const showReceipt = ref(false)
const rf = ref({ date: todayStr(), amount: null, method: '银行转账', handler: '', note: '', contractId: null, invoiceNo: '' })
function openReceipt() {
  rf.value = { date: todayStr(), amount: null, method: '银行转账', handler: '', note: '', contractId: null, invoiceNo: '' }
  showReceipt.value = true
}
async function addReceipt() {
  if (!rf.value.date || !rf.value.amount) { message.warning('请填写开票日期和金额'); return }
  try {
    await api.post(`/projects/${id}/receipts`, rf.value)
    message.success('开票已登记（待收款）')
    showReceipt.value = false
    await load()
  } catch (e) { message.error(errMsg(e)) }
}
// 确认收款：选到账日期 → 该笔变为已收款
const showConfirm = ref(false)
const cfRow = ref(null)
const cfDate = ref(todayStr())
function openConfirm(r) { cfRow.value = r; cfDate.value = todayStr(); showConfirm.value = true }
async function doConfirm() {
  try {
    await api.post(`/projects/${id}/receipts/${cfRow.value.id}/confirm`, { receivedDate: cfDate.value })
    message.success('已确认收款')
    showConfirm.value = false
    await load()
  } catch (e) { message.error(errMsg(e)) }
}
async function delReceipt(rid) {
  await api.delete(`/projects/${id}/receipts/${rid}`)
  message.success('已删除')
  await load()
}
// 编辑开票（与收费台账共用 ReceiptEditModal 组件）
const showRecEdit = ref(false)
const editRecRow = ref(null)
function openRecEdit(r) { editRecRow.value = r; showRecEdit.value = true }

const RECEIPT_COLUMNS = [
  { title: '开票日期', key: 'date', width: 102 },
  { title: '发票号', key: 'invoiceNo', width: 128, ellipsis: { tooltip: true }, render: (r) => r.invoiceNo || h('span', { class: 'faint' }, '—') },
  { title: thUnit('金额'), titleText: '金额（万元）', key: 'amount', align: 'right', render: (r) => fmt(r.amount) },
  { title: '所属合同', key: 'contractNo', width: 120, render: (r) => r.contractNo ? h('span', { title: r.contractName || '' }, r.contractNo) : h('span', { class: 'faint' }, '未指定') },
  { title: '状态', key: 'isReceived', width: 148, exportValue: (r) => r.isReceived ? '已收款 · ' + (r.receivedDate || '') : '已开票 · 待收款', render: (r) => r.isReceived
      ? h(NTag, { size: 'small', type: 'success', bordered: false }, () => '已收款 · ' + (r.receivedDate || ''))
      : h(NTag, { size: 'small', type: 'warning', bordered: false }, () => '已开票 · 待收款') },
  { title: '备注', key: 'note', ellipsis: { tooltip: true } },
  { title: '', key: 'act', width: 106, exportValue: () => '', render: (r) => h('span', { style: 'display:inline-flex;gap:2px' }, [
      iconBtn('pencil', 'blue', '编辑', () => openRecEdit(r)),
      r.isReceived ? null : iconBtn('check', 'green', '确认收款', () => openConfirm(r)),
      iconBtn('trash', 'red', '删除', () => delReceipt(r.id))
    ]) }
]
const receiptVisibleKeys = ref(RECEIPT_COLUMNS.map(c => c.key))
const receiptColumns = computed(() => RECEIPT_COLUMNS.filter(c => receiptVisibleKeys.value.includes(c.key)))
const pagerRec = usePager()
const pagedReceipts = computed(() => d.value ? pagerRec.slice(d.value.receipts) : [])
</script>

<template>
  <div v-if="p">
    <!-- 头部 -->
    <div class="card d-head">
      <div style="display:flex;gap:10px;align-items:flex-start;flex-wrap:wrap">
        <div style="flex:1;min-width:260px">
          <div style="display:flex;gap:8px;align-items:center;margin-bottom:7px;flex-wrap:wrap">
            <button class="back-ic" @click="goBack" title="返回台账"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><line x1="19" y1="12" x2="5" y2="12"/><polyline points="12 19 5 12 12 5"/></svg></button>
            <span class="num faint">{{ p.code }}</span>
            <n-tag size="small" :type="p.status === '已归档' ? 'default' : p.status === '已出报告' ? 'success' : 'warning'" :bordered="false">{{ p.status }}</n-tag>
            <n-tag size="small" :bordered="false">{{ p.bizType }}</n-tag>
            <span class="rpt-chip">报告编号：{{ p.reportNo || '—' }}</span>
          </div>
          <div class="d-title">{{ p.name }}</div>
          <div class="small mut" style="margin-top:6px">{{ p.client }}<span v-if="p.contactName"> · 联系人 {{ p.contactName }}<span v-if="p.contactPhone"> {{ p.contactPhone }}</span></span> · 负责人 {{ p.ownerName || '—' }} · 承接 {{ p.acceptedDate || '—' }}<span v-if="p.createdBy"> · 创建 {{ p.createdBy }} · {{ (p.createdAt || '').slice(0, 10) }}</span></div>
        </div>
        <n-button @click="openEdit">编辑项目</n-button>
        <n-dropdown :options="statusOptions" @select="changeStatus"><n-button type="primary">更改状态</n-button></n-dropdown>
        <n-button type="error" ghost @click="delProject">删除项目</n-button>
      </div>
      <div class="d-steps">
        <template v-for="(s, i) in STATUSES" :key="s">
          <div class="d-step" :class="{ done: i < stepIdx, cur: i === stepIdx }">
            <span class="dot">{{ i < stepIdx ? '✓' : i + 1 }}</span>{{ s }}
          </div>
          <div v-if="i < STATUSES.length - 1" class="d-step-line" :class="{ done: i < stepIdx }"></div>
        </template>
      </div>
      <div class="d-amounts">
        <div class="a" :class="'t' + (i % 4)" v-for="(a, i) in amounts" :key="a.lb">
          <span class="a-ic" :class="'t' + (i % 4)">
            <svg v-if="i === 0" viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="12" y1="18" x2="12" y2="11"/><polyline points="8.5 14.5 12 11 15.5 14.5"/></svg>
            <svg v-else-if="i === 1" viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/></svg>
            <svg v-else-if="i === 2" viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="19" y1="5" x2="5" y2="19"/><circle cx="6.5" cy="6.5" r="2.5"/><circle cx="17.5" cy="17.5" r="2.5"/></svg>
            <svg v-else viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 12V7H5a2 2 0 0 1 0-4h14v4"/><path d="M3 5v14a2 2 0 0 0 2 2h16v-5"/><path d="M18 12a2 2 0 0 0 0 4h4v-4z"/></svg>
          </span>
          <div style="flex:1;min-width:0">
            <div class="lb">{{ a.lb }}</div>
            <div v-if="a.field && editingAmt === a.field" style="display:flex;gap:4px;align-items:center">
              <n-input-number v-model:value="editAmtVal" :min="0" :precision="2" size="small" style="flex:1;min-width:0" @keyup.enter="saveAmtEdit(a)" @keyup.esc="editingAmt = null" />
              <n-button size="tiny" type="primary" @click="saveAmtEdit(a)">保存</n-button>
              <n-button size="tiny" @click="editingAmt = null">取消</n-button>
            </div>
            <div v-else class="vl num" :class="{ 'vl-edit': a.field }" :title="a.field ? '点击直接修改' : null" @click="a.field && startAmtEdit(a)">{{ a.vl }}<svg v-if="a.field" class="amt-pen" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M17 3a2.83 2.83 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5Z"/><path d="m15 5 4 4"/></svg></div>
            <div class="sb">{{ a.sb }}</div>
          </div>
        </div>
      </div>
    </div>

    <!-- 第一行：参与人员 / 合同与收费（按比例分；基本信息已并入头部与编辑弹窗，不再单设卡片） -->
    <div class="d-top2">
      <div class="card">
        <div class="card-h"><span class="card-ic b"><svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/></svg></span><h3>参与人员</h3><div class="sp"></div><n-button size="small" @click="openTeam">编辑分工</n-button></div>
        <div v-if="!d.team.length" class="faint small">暂未设置团队，点「编辑分工」添加成员、职责与占比。</div>
        <div>
          <div class="team-it" v-for="t in d.team" :key="t.id">
            <img :src="avatarFor({ id: t.memberId, gender: t.gender, avatarPath: t.avatarPath })" :alt="t.name" />
            <div style="flex:1;min-width:0">
              <div style="display:flex;align-items:center;gap:6px;flex-wrap:wrap"><b style="font-size:13.5px">{{ t.name }}</b><span class="role-tag">{{ t.role }}</span></div>
              <div class="duty">{{ t.duty || '暂无职责说明' }}</div>
            </div>
            <div class="share">
              <b class="num">{{ fmt(t.share) }}%</b>
              <div class="pbar"><i :style="{ width: Math.min(Number(t.share) || 0, 100) + '%' }"></i></div>
              <span>工作占比</span>
            </div>
          </div>
        </div>
        <div v-if="d.team.length" style="display:flex;justify-content:flex-end;align-items:center;gap:7px;margin-top:9px;padding-top:8px;border-top:1px solid var(--line-soft)" class="small">
          <span class="mut">占比合计</span>
          <b class="num" :style="{ color: shareTotal === 100 ? '#1f7a4d' : '#c26a12' }">{{ fmt(shareTotal) }}%</b>
          <span v-if="shareTotal !== 100" class="faint">（合计应为 100%）</span>
        </div>
      </div>

      <div class="card">
        <div class="card-h"><span class="card-ic o"><svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/></svg></span><h3>合同与收费</h3><div class="sp"></div>
          <n-button size="small" :type="d.contracts.length ? 'default' : 'primary'" @click="showLink = true">关联合同</n-button>
        </div>
        <div style="display:flex;gap:14px;align-items:center;flex-wrap:wrap">
          <div class="fring sm">
            <svg width="76" height="76" viewBox="0 0 96 96">
              <circle class="bgc" cx="48" cy="48" r="40" />
              <circle cx="48" cy="48" r="40" fill="none" stroke="#e8862e" stroke-width="9" :stroke-dasharray="invArc + ' ' + RING_C" :stroke-dashoffset="-recvArc" />
              <circle class="vlc" cx="48" cy="48" r="40" :stroke-dasharray="recvArc + ' ' + RING_C" stroke-dashoffset="0" />
            </svg>
            <div class="fring-t"><b class="num" style="font-size:16px">{{ feePct }}%</b><span>已收进度</span></div>
          </div>
          <div class="fee-stats" style="gap:18px">
            <div><span>咨询费应收</span><b class="num">{{ fmt(p.fee) }} 万</b></div>
            <div><span>已开票</span><b class="num" style="color:#c26a12">{{ fmt(invoiced) }} 万</b></div>
            <div><span>已收</span><b class="num" style="color:#1f7a4d">{{ fmt(received) }} 万</b></div>
            <div><span>待收</span><b class="num neg">{{ fmt(Math.max(p.fee - received, 0)) }} 万</b></div>
          </div>
        </div>
        <div class="small mut" style="margin-top:10px">{{ d.contracts.length }} 份合同<span v-if="contractAmount != null"> · 合同金额 {{ contractAmount === '按实结算' ? '按实结算' : fmt(contractAmount) + ' 万' }}</span> · 最近开票 {{ lastReceipt ? lastReceipt.date + ' · ' + fmt(lastReceipt.amount) + ' 万元' + (lastReceipt.isReceived ? '（已收）' : '（待收）') : '暂无' }}</div>
        <div style="margin-top:6px">
          <div v-if="!d.contracts.length" class="faint small" style="padding:6px 0 12px">暂未关联合同，点右上角「关联合同」添加（合同金额在合同管理中维护）。</div>
          <div v-for="c in d.contracts" :key="c.id" style="padding:9px 0;border-top:1px solid var(--line-soft)">
            <div style="display:flex;gap:10px;align-items:center;flex-wrap:wrap">
              <div style="flex:1;min-width:0">
                <b style="font-size:13.5px">{{ c.name }}</b>
                <n-tag v-if="c.isPrimary" size="tiny" type="success" :bordered="false" style="margin-left:6px">主合同</n-tag>
                <n-tag size="tiny" :bordered="false" style="margin-left:4px">{{ c.type }}</n-tag>
                <div class="psub">{{ c.no }} · {{ c.partyA || '—' }}</div>
              </div>
              <div class="num" style="font-weight:650">{{ c.amount == null ? '按实结算' : fmt(c.amount) + ' 万' }}</div>
              <n-button size="tiny" quaternary @click="unlink(c.id)">取消关联</n-button>
            </div>
            <div v-if="c.files && c.files.length" style="margin-top:5px">
              <div v-for="f in c.files" :key="f.id" style="display:flex;gap:8px;align-items:center;font-size:12.5px;color:var(--sub);padding:3px 0;flex-wrap:wrap">
                <svg viewBox="0 0 24 24" width="13" height="13" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="flex:0 0 13px"><path d="M21.44 11.05l-9.19 9.19a6 6 0 0 1-8.49-8.49l8.57-8.57A4 4 0 1 1 18 8.84l-8.59 8.57a2 2 0 0 1-2.83-2.83l8.49-8.48"/></svg>
                <span class="fname" style="flex:1" :title="f.fileName">{{ f.fileName }}</span>
                <span class="faint">{{ fmtSize(f.sizeBytes) }}</span>
                <button class="icbtn b-blue" title="预览" @click="previewRef.open(`/contracts/${c.id}/files/${f.id}/download`, f.fileName)"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg></button>
                <button class="icbtn b-slate" title="下载" @click="downloadContractFile(c, f)"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg></button>
              </div>
            </div>
          </div>
          <div style="display:flex;align-items:center;margin:14px 0 4px"><b style="font-size:13.5px">开票与收款</b><div style="flex:1"></div><TableTools v-model="receiptVisibleKeys" :cols="RECEIPT_COLUMNS" :rows="receipts" filename="开票与收款" storage-key="pd-receipts" /><n-button size="small" type="primary" ghost @click="openReceipt" style="margin-left:8px">+ 新增开票</n-button></div>
          <n-data-table v-if="d.receipts.length" size="small" :bordered="false" :columns="receiptColumns" :data="pagedReceipts" :scroll-x="700" :pagination="false" />
          <Pager v-if="d.receipts.length" v-model:page="pagerRec.page.value" v-model:page-size="pagerRec.pageSize.value" :total="d.receipts.length" />
          <div v-if="!d.receipts.length" class="faint small" style="padding:8px 0">暂无开票记录</div>
        </div>
      </div>
    </div>

    <!-- 第二行：左列 成果文件 + 项目资料（上下放）；右列 造价解析（并排） -->
    <div class="d-row2">
      <div style="display:flex;flex-direction:column;min-width:0">
        <div class="card">
          <div class="card-h"><span class="card-ic g"><svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z"/><polyline points="3.27 6.96 12 12.01 20.73 6.96"/><line x1="12" y1="22.08" x2="12" y2="12"/></svg></span><h3>成果文件</h3><div class="sp"></div>
            <n-tag size="small" type="success" :bordered="false">{{ currentSet?.isFinal ? '最终版' : '当前版本' }} V{{ currentVersionNo }}</n-tag>
            <n-tag v-if="gotCount === 3" type="success" size="small" :bordered="false">已集齐 3/3</n-tag>
            <n-tag v-else type="warning" size="small" :bordered="false">已传 {{ gotCount }}/3</n-tag>
            <n-button size="small" type="primary" @click="newVersion">+ 新增版本</n-button>
          </div>
          <div class="small faint" style="margin-bottom:10px">软件版 / 文档版 / 报告是一个整体版本；点「新增版本」，三类一起转入历史版本。</div>
          <div class="ftiles">
            <div class="ftile" v-for="g in fileGroups" :key="g.key">
              <div class="ftile-ic" :class="g.cls">
                <svg v-if="g.key === 'Software'" viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="16 18 22 12 16 6"/><polyline points="8 6 2 12 8 18"/></svg>
                <svg v-else-if="g.key === 'Document'" viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="3" width="18" height="18" rx="2"/><line x1="3" y1="9" x2="21" y2="9"/><line x1="3" y1="15" x2="21" y2="15"/><line x1="9" y1="3" x2="9" y2="21"/><line x1="15" y1="3" x2="15" y2="21"/></svg>
                <svg v-else viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/><line x1="10" y1="9" x2="8" y2="9"/></svg>
              </div>
              <div style="font-size:13px;font-weight:650;margin-top:8px">{{ g.label }}
                <n-tag v-if="g.cur" size="tiny" type="success" :bordered="false">已上传</n-tag>
                <span v-else class="small" style="color:#9a6a12;font-weight:500">未上传</span>
              </div>
              <div class="psub" v-if="g.cur" :title="g.cur.fileName">{{ fmtSize(g.cur.sizeBytes) }} · {{ String(g.cur.uploadedAt).slice(0, 10) }}</div>
              <div class="psub" v-if="g.cur && parseOf(g.cur.id)" style="color:var(--pri);font-weight:600">解析造价 ¥{{ fmt(parseOf(g.cur.id).totalAmount) }}</div>
              <div class="psub" v-else-if="g.cur && isGcfxName(g.cur.fileName)" style="color:#9a6a12">GCFX 尚未解析，点下方「解析」</div>
              <div class="psub" v-else-if="!g.cur">暂无文件</div>
              <div class="psub" v-if="g.key === 'Report'" style="color:#6d4fc2;font-weight:600">报告编号：{{ p.reportNo || '—' }}</div>
              <div style="display:flex;gap:2px;margin-top:7px;flex-wrap:wrap;justify-content:center">
                <UploadBtn size="tiny" type="primary" ghost silent :label="g.cur ? '替换' : '上传'" :upload="(f, p) => uploadDeliverable(g.key, f, p)" @done="onDeliverableUploaded" />
                <n-button v-if="g.cur && isGcfxName(g.cur.fileName)" size="tiny" quaternary @click="onParseBtn('file', g.cur.id)">解析</n-button>
                <button v-if="g.cur && fileKind(g.cur.fileName) !== 'other'" class="icbtn b-blue" title="预览" @click="previewFile('deliverables', g.cur)"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg></button>
                <button v-if="g.cur" class="icbtn b-slate" title="下载" @click="downloadFile('deliverables', g.cur.id, g.cur.fileName)"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg></button>
                <button v-if="g.cur" class="icbtn b-red" title="删除" @click="confirmDelete('删除当前版本中的该文件？', async () => { await api.delete(`/projects/${id}/files/deliverables/${g.cur.id}`); load() })"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/></svg></button>
              </div>
            </div>
          </div>
          <div v-if="historyGroups.length" style="margin-top:10px;border-top:1px solid var(--line-soft);padding-top:8px">
            <div v-for="hg in historyGroups" :key="hg.key" style="margin-bottom:2px">
              <div style="display:flex;align-items:center;gap:8px;cursor:pointer;padding:4px 0" @click="toggleHist(hg.key)">
                <span class="small mut">{{ hg.label }}（{{ hg.files.length }}）</span>
                <n-button v-if="hg.setId" size="tiny" type="primary" ghost @click.stop="setFinal(hg)">设为最终版</n-button>
                <span class="faint" style="margin-left:auto">{{ histOpen[hg.key] ? '▾ 收起' : '▸ 展开' }}</span>
              </div>
              <div v-if="histOpen[hg.key]">
                <div class="fhist" v-for="hf in hg.files" :key="hf.id" style="padding-left:0">
                  <span class="fhist-ic" :class="catCls(hf.category)"><svg viewBox="0 0 24 24" width="12" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/></svg></span>
                  <n-tag size="tiny" :bordered="false">{{ hf.catLabel }}</n-tag>
                  <span class="faint">{{ fmtSize(hf.sizeBytes) }} · {{ String(hf.uploadedAt).slice(0, 10) }}</span>
                  <span style="flex:1"></span>
                  <button v-if="isGcfxName(hf.fileName)" class="icbtn b-violet" title="解析 / 查看解析" @click="onParseBtn('file', hf.id)"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21.21 15.89A10 10 0 1 1 8 2.83"/><path d="M22 12A10 10 0 0 0 12 2v10z"/></svg></button>
                  <button v-if="fileKind(hf.fileName) !== 'other'" class="icbtn b-blue" title="预览" @click="previewFile('deliverables', hf)"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg></button>
                  <button class="icbtn b-slate" title="下载" @click="downloadFile('deliverables', hf.id, hf.fileName)"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg></button>
                  <button class="icbtn b-red" title="删除" @click="confirmDelete('删除该历史文件？', async () => { await api.delete(`/projects/${id}/files/deliverables/${hf.id}`); load() })"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/></svg></button>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div class="card">
          <div class="card-h"><span class="card-ic tl"><svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"/></svg></span><h3>项目资料</h3></div>
          <div class="small faint" style="margin-bottom:4px">图纸、隐蔽资料、联系函等过程文件按分类归档，与成果文件分开保存。</div>
          <div class="doc-cat" v-for="g in docGroups" :key="g.cat">
            <div style="display:flex;align-items:center;gap:9px;cursor:pointer;padding:3px 0" @click="openDocCat = openDocCat === g.cat ? '' : g.cat">
              <span class="role-tag" style="background:#eef4f0;display:inline-flex;padding:4px 5px"><svg viewBox="0 0 24 24" width="13" height="13" fill="none" stroke="#186140" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"/></svg></span>
              <b style="font-size:13px">{{ g.cat }}</b>
              <span class="faint small">{{ g.files.length }} 个文件</span>
              <span class="faint" style="margin-left:auto">{{ openDocCat === g.cat ? '▾' : '▸' }}</span>
            </div>
            <div v-if="openDocCat === g.cat" style="padding:2px 0 6px 35px">
              <div class="doc-f" v-for="f in g.files" :key="f.id">
                <span class="fname" style="flex:1" :title="f.fileName">{{ f.fileName }}</span>
                <span v-if="parseOfDoc(f.id)" class="small" style="color:var(--pri);font-weight:600">解析 ¥{{ fmt(parseOfDoc(f.id).totalAmount) }}</span>
                <span class="faint">{{ String(f.uploadedAt).slice(0, 10) }}</span>
                <button class="icbtn b-violet" title="更改分类" @click="openDocCatEdit(f)"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M8 3 4 7l4 4"/><path d="M4 7h16"/><path d="m16 21 4-4-4-4"/><path d="M20 17H4"/></svg></button>
                <button v-if="isGcfxName(f.fileName)" class="icbtn b-violet" title="解析 / 查看解析" @click="onParseBtn('doc', f.id)"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21.21 15.89A10 10 0 1 1 8 2.83"/><path d="M22 12A10 10 0 0 0 12 2v10z"/></svg></button>
                <button v-if="fileKind(f.fileName) !== 'other'" class="icbtn b-blue" title="预览" @click="previewFile('docs', f)"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg></button>
                <button class="icbtn b-slate" title="下载" @click="downloadFile('docs', f.id, f.fileName)"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg></button>
                <button class="icbtn b-red" title="删除" @click="confirmDelete('删除该资料文件？', async () => { await api.delete(`/projects/${id}/files/docs/${f.id}`); load() })"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/></svg></button>
              </div>
              <div v-if="!g.files.length" class="doc-f faint">暂无文件</div>
              <UploadBtn size="tiny" silent label="+ 上传到此分类" style="margin-top:4px" :upload="(f, p) => uploadDoc(g.cat, f, p)" @done="(res) => onDocUploaded(g.cat, res)" />
            </div>
          </div>
        </div>
      </div>

      <!-- 造价解析（与成果文件并排） -->
      <div class="card" id="gcfxPanel" v-if="gcfxTarget">
        <div class="card-h"><span class="card-ic p"><svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21.21 15.89A10 10 0 1 1 8 2.83"/><path d="M22 12A10 10 0 0 0 12 2v10z"/></svg></span><h3>造价解析</h3><div class="sp"></div>
          <span class="small faint">{{ String(gcfxTarget.parse.parsedAt).slice(0, 16).replace('T', ' ') }} 解析</span>
          <n-button size="small" @click="reparse">重新解析</n-button>
        </div>
        <GcfxPanel :key="gcfxTarget.parse.id + '-' + gcfxTarget.parse.parsedAt" :parse="gcfxTarget.parse" :project="p" :file-name="gcfxTarget.fileName" />
      </div>
      <div class="card" v-else>
        <div class="card-h"><span class="card-ic p"><svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21.21 15.89A10 10 0 1 1 8 2.83"/><path d="M22 12A10 10 0 0 0 12 2v10z"/></svg></span><h3>造价解析</h3></div>
        <p class="small mut" style="line-height:1.9;margin:0 0 6px">
          解析宏业清单计价文件（GCFX / SGCFX）：提取工程名称、总造价（与文件大写金额核对）、计价依据、单位工程构成与分部分项清单明细（编码 / 工程量 / 综合单价 / 合价）及费用汇总。成果文件与项目资料里的计价文件都可以解析，上传时会自动解析一次，之后也可随时点文件旁的「解析」重跑。
        </p>
        <div v-if="gcfxFilesAll.length">
          <div v-for="gf in gcfxFilesAll" :key="gf.kind + gf.id" style="display:flex;gap:8px;align-items:center;padding:7px 0;border-top:1px solid var(--line-soft)">
            <span class="fname" style="flex:1;font-size:13px" :title="gf.name">{{ gf.name }}</span>
            <span class="faint small">{{ gf.from }}</span>
            <n-tag v-if="gf.parsed" size="tiny" type="success" :bordered="false">已解析</n-tag>
            <n-button size="tiny" type="primary" ghost @click="onParseBtn(gf.kind, gf.id)">{{ gf.parsed ? '查看解析' : '解析' }}</n-button>
          </div>
        </div>
        <div v-else class="faint small">本项目还没有 GCFX / SGCFX 文件。在左侧「成果文件」或「项目资料」里上传计价文件后，这里会展示解析结果。</div>
      </div>
    </div>

    <!-- 编辑项目 -->
    <n-modal v-model:show="showEdit" preset="card" title="编辑项目" style="width:640px;border-radius:16px">
      <n-form label-placement="top">
        <n-form-item label="项目名称"><n-input v-model:value="ef.name" /></n-form-item>
        <n-grid :cols="2" :x-gap="12">
          <n-form-item-gi label="委托单位"><n-input v-model:value="ef.client" /></n-form-item-gi>
          <n-form-item-gi label="业务类型"><n-select v-model:value="ef.bizType" :options="BIZ_TYPES.map(t => ({ label: t, value: t }))" /></n-form-item-gi>
          <n-form-item-gi label="联系人"><n-input v-model:value="ef.contactName" /></n-form-item-gi>
          <n-form-item-gi label="联系电话"><n-input v-model:value="ef.contactPhone" /></n-form-item-gi>
          <n-form-item-gi label="项目规模"><n-input v-model:value="ef.scale" /></n-form-item-gi>
          <n-form-item-gi label="状态"><n-select v-model:value="ef.status" :options="STATUSES.map(t => ({ label: t, value: t }))" /></n-form-item-gi>
          <n-form-item-gi label="负责人"><n-select v-model:value="ef.ownerMemberId" clearable :options="members.map(m => ({ label: m.name, value: m.id }))" /></n-form-item-gi>
          <n-form-item-gi label="报告编号"><n-input v-model:value="ef.reportNo" /></n-form-item-gi>
          <n-form-item-gi label="承接日期"><n-date-picker v-model:formatted-value="ef.acceptedDate" value-format="yyyy-MM-dd" type="date" style="width:100%" /></n-form-item-gi>
          <n-form-item-gi label="要求完成"><n-date-picker v-model:formatted-value="ef.dueDate" value-format="yyyy-MM-dd" type="date" clearable style="width:100%" /></n-form-item-gi>
          <n-form-item-gi label="出报告日期"><n-date-picker v-model:formatted-value="ef.reportDate" value-format="yyyy-MM-dd" type="date" clearable style="width:100%" /></n-form-item-gi>
          <n-form-item-gi label="咨询费应收（万元）"><n-input-number v-model:value="ef.fee" :min="0" style="width:100%" /></n-form-item-gi>
          <n-form-item-gi label="送审 / 控制价 / 预算 / 建安（万元）"><n-input-number v-model:value="ef.submitAmount" :min="0" style="width:100%" /></n-form-item-gi>
          <n-form-item-gi v-if="ef.bizType === '预算评审'" label="送审金额（万元，预算评审）"><n-input-number v-model:value="ef.reviewSubmitAmount" :min="0" style="width:100%" /></n-form-item-gi>
          <n-form-item-gi label="审定金额（万元，审核类）"><n-input-number v-model:value="ef.approvedAmount" :min="0" style="width:100%" /></n-form-item-gi>
        </n-grid>
        <n-form-item label="项目概况"><n-input v-model:value="ef.description" type="textarea" /></n-form-item>
      </n-form>
      <template #footer>
        <div style="display:flex;justify-content:flex-end;gap:10px">
          <n-button @click="showEdit = false">取消</n-button>
          <n-button type="primary" @click="saveEdit">保存</n-button>
        </div>
      </template>
    </n-modal>

    <!-- 编辑团队 -->
    <n-modal v-model:show="showTeam" preset="card" title="编辑团队分工" style="width:760px;max-width:94vw;border-radius:16px">
      <div style="display:flex;gap:8px;margin-bottom:6px;padding:0 2px" class="small faint">
        <span style="flex:1.1">成员</span><span style="flex:0.9">角色</span><span style="flex:1.6">人员职责</span><span style="width:104px">占比</span><span style="width:32px"></span>
      </div>
      <div v-for="(r, i) in teamRows" :key="i" style="display:flex;gap:8px;margin-bottom:10px;align-items:center">
        <n-select v-model:value="r.memberId" :options="members.map(m => ({ label: m.name, value: m.id }))" placeholder="成员" style="flex:1.1" />
        <n-select v-model:value="r.role" :options="ROLES.map(t => ({ label: t, value: t }))" style="flex:0.9" />
        <n-input v-model:value="r.duty" placeholder="如：土建清单编制与复核" style="flex:1.6" />
        <n-input-number v-model:value="r.share" :min="0" :max="100" style="width:104px" />
        <span class="small mut">%</span>
        <n-button quaternary @click="teamRows.splice(i, 1)">✕</n-button>
      </div>
      <n-button dashed block @click="teamRows.push({ memberId: null, role: '编制人', duty: '', share: 0 })">+ 添加成员</n-button>
      <p class="small faint" style="margin-top:10px">占比合计：{{ teamRows.reduce((a, r) => a + (r.share || 0), 0) }}%（建议合计 100%）</p>
      <template #footer>
        <div style="display:flex;justify-content:flex-end;gap:10px">
          <n-button @click="showTeam = false">取消</n-button>
          <n-button type="primary" @click="saveTeam">保存分工</n-button>
        </div>
      </template>
    </n-modal>

    <!-- 关联合同 -->
    <n-modal v-model:show="showLink" preset="card" title="关联合同" style="width:480px;border-radius:16px">
      <n-form label-placement="top">
        <n-form-item label="选择合同">
          <n-select v-model:value="linkForm.contractId" filterable :options="allContracts.map(c => ({ label: c.no + ' · ' + c.name, value: c.id }))" placeholder="在合同台账中选择" />
        </n-form-item>
        <n-checkbox v-model:checked="linkForm.isPrimary">设为主合同</n-checkbox>
      </n-form>
      <template #footer>
        <div style="display:flex;justify-content:flex-end;gap:10px">
          <n-button @click="showLink = false">取消</n-button>
          <n-button type="primary" :disabled="!linkForm.contractId" @click="doLink">确认关联</n-button>
        </div>
      </template>
    </n-modal>

    <!-- 新增开票（登记发票，确认收款是后续第二步） -->
    <n-modal v-model:show="showReceipt" preset="card" title="新增开票" style="width:480px;border-radius:16px">
      <n-form label-placement="top">
        <n-form-item label="开票日期"><n-date-picker v-model:formatted-value="rf.date" value-format="yyyy-MM-dd" type="date" style="width:100%" /></n-form-item>
        <n-form-item label="发票号"><n-input v-model:value="rf.invoiceNo" placeholder="发票号码（可后补）" /></n-form-item>
        <n-form-item label="金额（万元）"><n-input-number v-model:value="rf.amount" :min="0" style="width:100%" /></n-form-item>
        <n-form-item label="所属合同（可选）">
          <n-select v-model:value="rf.contractId" clearable :options="d.contracts.map(c => ({ label: c.no + ' · ' + c.name, value: c.id }))" placeholder="未指定合同" />
        </n-form-item>
        <n-grid :cols="2" :x-gap="12">
          <n-form-item-gi label="方式"><n-select v-model:value="rf.method" :options="['银行转账', '现金', '承兑汇票', '其他'].map(t => ({ label: t, value: t }))" /></n-form-item-gi>
          <n-form-item-gi label="经办人"><n-input v-model:value="rf.handler" /></n-form-item-gi>
        </n-grid>
        <n-form-item label="备注"><n-input v-model:value="rf.note" /></n-form-item>
      </n-form>
      <p class="small faint">登记后状态为「已开票 · 待收款」；钱到账后在列表里点 ✓「确认收款」。</p>
      <template #footer>
        <div style="display:flex;justify-content:flex-end;gap:10px">
          <n-button @click="showReceipt = false">取消</n-button>
          <n-button type="primary" @click="addReceipt">保存开票</n-button>
        </div>
      </template>
    </n-modal>

    <!-- 编辑开票（共用组件） -->
    <ReceiptEditModal v-model:show="showRecEdit" :receipt="editRecRow" :project-id="id" @saved="load" />

    <!-- 确认收款 -->
    <n-modal v-model:show="showConfirm" preset="card" title="确认收款" style="width:400px;border-radius:16px">
      <p class="small mut" style="margin:0 0 12px">确认这笔 {{ cfRow ? fmt(cfRow.amount) : '' }} 万元已经到账？</p>
      <n-form label-placement="top">
        <n-form-item label="到账日期"><n-date-picker v-model:formatted-value="cfDate" value-format="yyyy-MM-dd" type="date" style="width:100%" /></n-form-item>
      </n-form>
      <template #footer>
        <div style="display:flex;justify-content:flex-end;gap:10px">
          <n-button @click="showConfirm = false">取消</n-button>
          <n-button type="primary" @click="doConfirm">确认到账</n-button>
        </div>
      </template>
    </n-modal>

    <!-- 更改资料分类 -->
    <n-modal v-model:show="showDocCat" preset="card" title="更改分类" style="width:400px;border-radius:16px">
      <n-form label-placement="top">
        <n-form-item label="文件名"><span class="small">{{ dcFile?.fileName }}</span></n-form-item>
        <n-form-item label="改到分类"><n-select v-model:value="dcCat" :options="DOC_CATS.map(t => ({ label: t, value: t }))" /></n-form-item>
      </n-form>
      <template #footer>
        <div style="display:flex;justify-content:flex-end;gap:10px">
          <n-button @click="showDocCat = false">取消</n-button>
          <n-button type="primary" @click="saveDocCat">保存</n-button>
        </div>
      </template>
    </n-modal>

    <PreviewModal ref="previewRef" />
  </div>
  <div v-else class="card"><n-spin size="large" /></div>
</template>
