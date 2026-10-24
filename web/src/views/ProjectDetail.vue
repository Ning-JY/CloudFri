<script setup>
import { ref, computed, onMounted, h } from 'vue'
import { NButton } from 'naive-ui'
import { useRoute, useRouter } from 'vue-router'
import { useMessage, useDialog } from 'naive-ui'
import api, { errMsg } from '../api'
import { fmt, fmtSize, avatarFor, cutRate, fileKind } from '../utils'
import PreviewModal from '../components/PreviewModal.vue'

const route = useRoute()
const router = useRouter()
const message = useMessage()
const dialog = useDialog()
const id = Number(route.params.id)

const d = ref(null)
const members = ref([])
const allContracts = ref([])
const previewRef = ref(null)
const BIZ_TYPES = ['结算审核', '预算编制', '清单控制价', '竣工决算', '跟踪审计', '全过程咨询']
const STATUSES = ['编制中', '审核中', '已出报告', '已归档']
const ROLES = ['项目负责人', '编制人', '审核人', '审定人', '配合人员']
const DOC_CATS = ['设计图纸', '隐蔽工程资料', '工作联系函', '会议纪要', '其他资料']
const FILE_CATS = [
  { key: 'Software', label: '软件版', icon: 'GCFX' },
  { key: 'Document', label: '文档版', icon: 'XLS' },
  { key: 'Report', label: '报告', icon: 'PDF' }
]

async function load() {
  const { data } = await api.get('/projects/' + id)
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
const contractAmount = computed(() => {
  if (!d.value) return null
  const cs = d.value.contracts
  if (!cs.length) return null
  if (cs.some(c => c.amount == null)) return '按实结算'
  return cs.reduce((a, c) => a + (c.amount || 0), 0)
})
const received = computed(() => d.value?.receivedTotal || 0)
const feePct = computed(() => (p.value && p.value.fee > 0) ? Math.min(100, Math.round(received.value / p.value.fee * 100)) : 0)
const ringOffset = computed(() => (251.3 * (1 - feePct.value / 100)).toFixed(1))
const lastReceipt = computed(() => {
  const rs = d.value?.receipts || []
  return rs.length ? rs[rs.length - 1] : null
})
const stepIdx = computed(() => p.value ? STATUSES.indexOf(p.value.status) : 0)

const amounts = computed(() => {
  if (!p.value) return []
  const cr = cutRate(p.value)
  if (isAudit.value) return [
    { lb: '送审金额', vl: fmt(p.value.submitAmount) + ' 万', sb: '委托方送审' },
    { lb: '审定金额', vl: fmt(p.value.approvedAmount) + ' 万', sb: p.value.reportNo ? '报告 ' + p.value.reportNo : '审定结果' },
    { lb: '核减额 / 核减率', vl: (p.value.submitAmount != null && p.value.approvedAmount != null) ? fmt(p.value.submitAmount - p.value.approvedAmount) + ' 万' : '—', sb: cr != null ? '核减率 ' + cr.toFixed(2) + '%' : '—' },
    { lb: '咨询费（应收 / 已收）', vl: fmt(p.value.fee) + ' 万', sb: '已收 ' + fmt(received.value) + ' 万' }
  ]
  if (isProcess.value) return [
    { lb: '建安造价', vl: fmt(p.value.submitAmount) + ' 万', sb: '过程控制对象' },
    { lb: '合同金额', vl: contractAmount.value === '按实结算' ? '按实结算' : fmt(contractAmount.value) + ' 万', sb: '关联合同合计' },
    { lb: '咨询费应收', vl: fmt(p.value.fee) + ' 万', sb: '按合同约定' },
    { lb: '已收咨询费', vl: fmt(received.value) + ' 万', sb: '累计收款' }
  ]
  return [
    { lb: p.value.bizType === '清单控制价' ? '控制价金额' : '预算金额', vl: fmt(p.value.submitAmount) + ' 万', sb: '编制成果' },
    { lb: '合同金额', vl: contractAmount.value === '按实结算' ? '按实结算' : (contractAmount.value == null ? '—' : fmt(contractAmount.value) + ' 万'), sb: '关联合同合计' },
    { lb: '咨询费应收', vl: fmt(p.value.fee) + ' 万', sb: '按合同约定' },
    { lb: '已收咨询费', vl: fmt(received.value) + ' 万', sb: '累计收款' }
  ]
})

const fileGroups = computed(() => {
  if (!d.value) return []
  return FILE_CATS.map(c => {
    const fs = d.value.files.filter(f => f.category === c.key)
    return { ...c, cur: fs[0] || null, hist: fs.slice(1) }
  })
})
const gotCount = computed(() => fileGroups.value.filter(g => g.cur).length)
const parseOf = (fileId) => (d.value?.gcfxParses || []).find(g => g.projectFileId === fileId)
const docGroups = computed(() => {
  if (!d.value) return []
  return DOC_CATS.map(cat => ({ cat, files: d.value.docs.filter(x => x.category === cat) }))
})
const openDocCat = ref('设计图纸')
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

async function uploadDeliverable(cat, file) {
  const fd = new FormData()
  fd.append('category', cat)
  fd.append('file', file)
  try {
    const { data } = await api.post(`/projects/${id}/files/deliverables`, fd)
    message.success(data.parseId ? '上传成功，已自动解析 GCFX' : '上传成功，新版本已生效，旧版转历史')
    await load()
  } catch (e) { message.error(errMsg(e, '上传失败')) }
}
async function uploadDoc(cat, file) {
  const fd = new FormData()
  fd.append('category', cat)
  fd.append('file', file)
  try {
    await api.post(`/projects/${id}/files/docs`, fd)
    message.success('已归档到「' + cat + '」')
    await load()
  } catch (e) { message.error(errMsg(e, '上传失败')) }
}
function pickFile(cb) {
  const inp = document.createElement('input')
  inp.type = 'file'
  inp.onchange = () => { if (inp.files[0]) cb(inp.files[0]) }
  inp.click()
}
function confirmDelete(text, fn) {
  dialog.warning({ title: '确认删除', content: text, positiveText: '删除', negativeText: '取消', onPositiveClick: fn })
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
    await api.put('/projects/' + id, {
      name: ef.value.name, client: ef.value.client, contactName: ef.value.contactName, contactPhone: ef.value.contactPhone,
      bizType: ef.value.bizType, scale: ef.value.scale, status: ef.value.status, ownerMemberId: ef.value.ownerMemberId,
      acceptedDate: ef.value.acceptedDate, dueDate: ef.value.dueDate, reportDate: ef.value.reportDate, reportNo: ef.value.reportNo,
      submitAmount: ef.value.submitAmount, approvedAmount: ef.value.approvedAmount, fee: ef.value.fee || 0, description: ef.value.description
    })
    message.success('已保存')
    showEdit.value = false
    await load()
  } catch (e) { message.error(errMsg(e)) }
}

// 团队编辑
const showTeam = ref(false)
const teamRows = ref([])
function openTeam() {
  teamRows.value = d.value.team.map(t => ({ memberId: t.memberId, role: t.role, share: t.share }))
  if (!teamRows.value.length) teamRows.value = [{ memberId: null, role: '编制人', share: 40 }]
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

// 收款
const showReceipt = ref(false)
const rf = ref({ date: null, amount: null, method: '银行转账', handler: '', note: '' })
async function addReceipt() {
  if (!rf.value.date || !rf.value.amount) { message.warning('请填写日期和金额'); return }
  try {
    await api.post(`/projects/${id}/receipts`, rf.value)
    message.success('收款已登记')
    showReceipt.value = false
    rf.value = { date: null, amount: null, method: '银行转账', handler: '', note: '' }
    await load()
  } catch (e) { message.error(errMsg(e)) }
}
async function delReceipt(rid) {
  await api.delete(`/projects/${id}/receipts/${rid}`)
  message.success('已删除')
  await load()
}

const receiptColumns = [
  { title: '收款日期', key: 'date', width: 110 },
  { title: '金额（万元）', key: 'amount', align: 'right', render: (r) => fmt(r.amount) },
  { title: '方式', key: 'method', width: 100 },
  { title: '经办人', key: 'handler', width: 90 },
  { title: '备注', key: 'note' },
  { title: '', key: 'act', width: 60, render: (r) => h(NButton, { size: 'tiny', quaternary: true, onClick: () => delReceipt(r.id) }, () => '删除') }
]
</script>

<template>
  <div v-if="p">
    <!-- 头部 -->
    <div class="card d-head">
      <div style="display:flex;gap:10px;align-items:flex-start;flex-wrap:wrap">
        <div style="flex:1;min-width:260px">
          <div style="display:flex;gap:8px;align-items:center;margin-bottom:7px">
            <span class="num faint">{{ p.code }}</span>
            <n-tag size="small" :type="p.status === '已归档' ? 'default' : p.status === '已出报告' ? 'success' : 'warning'" :bordered="false">{{ p.status }}</n-tag>
            <n-tag size="small" :bordered="false">{{ p.bizType }}</n-tag>
            <span v-if="p.reportNo" class="small faint">报告 {{ p.reportNo }}</span>
          </div>
          <div class="d-title">{{ p.name }}</div>
          <div class="small mut" style="margin-top:6px">{{ p.client }} · 负责人 {{ p.ownerName || '—' }} · 承接 {{ p.acceptedDate || '—' }}</div>
        </div>
        <n-button @click="openEdit">编辑项目</n-button>
        <n-button type="primary" @click="showReceipt = true">登记收款</n-button>
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
        <div class="a" v-for="a in amounts" :key="a.lb">
          <div class="lb">{{ a.lb }}</div><div class="vl num">{{ a.vl }}</div><div class="sb">{{ a.sb }}</div>
        </div>
      </div>
    </div>

    <div class="d-grid">
      <!-- 左列（窄长）：基本信息 -->
      <div style="display:flex;flex-direction:column;gap:0;min-width:0">
        <div class="card">
          <div class="card-h"><h3>基本信息</h3></div>
          <p class="d-desc">{{ p.description || '暂无项目概况。' }}</p>
          <div class="kv-row"><span class="k">项目编号</span><span class="num">{{ p.code }}</span></div>
          <div class="kv-row"><span class="k">委托单位</span><span>{{ p.client }}</span></div>
          <div class="kv-row"><span class="k">联系人</span><span>{{ p.contactName || '—' }}</span></div>
          <div class="kv-row"><span class="k">联系电话</span><span>{{ p.contactPhone || '—' }}</span></div>
          <div class="kv-row"><span class="k">业务类型</span><span>{{ p.bizType }}</span></div>
          <div class="kv-row"><span class="k">项目规模</span><span>{{ p.scale || '—' }}</span></div>
          <div class="kv-row"><span class="k">负责人</span><span>{{ p.ownerName || '—' }}</span></div>
          <div class="kv-row"><span class="k">项目状态</span><span>{{ p.status }}</span></div>
          <div class="kv-row"><span class="k">承接日期</span><span class="num">{{ p.acceptedDate || '—' }}</span></div>
          <div class="kv-row"><span class="k">要求完成</span><span class="num">{{ p.dueDate || '—' }}</span></div>
          <div class="kv-row"><span class="k">出报告日期</span><span class="num">{{ p.reportDate || '—' }}</span></div>
          <div class="kv-row"><span class="k">报告编号</span><span>{{ p.reportNo || '—' }}</span></div>
        </div>
      </div>

      <!-- 右列（宽）：参与人员 + 合同与收费 + 成果文件 + 项目资料 -->
      <div style="display:flex;flex-direction:column;gap:0;min-width:0">
        <div class="card">
          <div class="card-h"><h3>参与人员</h3><div class="sp"></div><n-button size="small" @click="openTeam">编辑分工</n-button></div>
          <div v-if="!d.team.length" class="faint small">暂未设置团队，点「编辑分工」添加成员与占比。</div>
          <div class="team-it" v-for="t in d.team" :key="t.id">
            <img :src="avatarFor(t)" :alt="t.name" />
            <div><b style="font-size:13.5px">{{ t.name }}</b> <span class="role-tag" style="margin-left:4px">{{ t.role }}</span></div>
            <div class="share"><b class="num">{{ fmt(t.share) }}%</b><span>工作占比</span></div>
          </div>
        </div>

        <div class="card">
          <div class="card-h"><h3>合同与收费</h3><div class="sp"></div>
            <span class="small faint">{{ d.contracts.length }} 份关联合同</span>
            <n-button size="small" @click="showLink = true">关联合同</n-button>
          </div>
          <div class="head-fee" style="margin:2px 0 16px">
            <div class="fring">
              <svg width="96" height="96" viewBox="0 0 96 96">
                <circle class="bgc" cx="48" cy="48" r="40" />
                <circle class="vlc" cx="48" cy="48" r="40" stroke-dasharray="251.3" :stroke-dashoffset="ringOffset" />
              </svg>
              <div class="fring-t"><b class="num">{{ feePct }}%</b><span>收款进度</span></div>
            </div>
            <div class="fee-stats">
              <div><span>咨询费应收</span><b class="num">{{ fmt(p.fee) }} 万</b></div>
              <div><span>已收</span><b class="num">{{ fmt(received) }} 万</b></div>
              <div><span>待收</span><b class="num neg">{{ fmt(Math.max(p.fee - received, 0)) }} 万</b></div>
            </div>
            <div class="small mut" style="margin-left:auto">最近收款 {{ lastReceipt ? lastReceipt.date + ' · ' + fmt(lastReceipt.amount) + ' 万元' : '暂无收款' }}</div>
          </div>

          <div v-if="!d.contracts.length" class="faint small" style="padding:6px 0 12px">暂未关联合同，点右上角「关联合同」添加（合同金额在合同管理中维护）。</div>
          <div v-for="c in d.contracts" :key="c.id" style="display:flex;gap:10px;align-items:center;padding:9px 0;border-top:1px solid var(--line-soft)">
            <div style="flex:1;min-width:0">
              <b style="font-size:13.5px">{{ c.name }}</b>
              <n-tag v-if="c.isPrimary" size="tiny" type="success" :bordered="false" style="margin-left:6px">主合同</n-tag>
              <n-tag size="tiny" :bordered="false" style="margin-left:4px">{{ c.type }}</n-tag>
              <div class="psub">{{ c.no }} · {{ c.partyA || '—' }}</div>
            </div>
            <div class="num" style="font-weight:650">{{ c.amount == null ? '按实结算' : fmt(c.amount) + ' 万' }}</div>
            <n-button size="tiny" quaternary @click="unlink(c.id)">取消关联</n-button>
          </div>

          <div style="display:flex;align-items:center;margin:16px 0 4px"><b style="font-size:13.5px">收款记录</b></div>
          <n-data-table size="small" :bordered="false" :columns="receiptColumns" :data="d.receipts" />
          <div v-if="!d.receipts.length" class="faint small" style="padding:8px 0">暂无收款记录</div>
        </div>

        <div class="card">
          <div class="card-h"><h3>成果文件</h3><div class="sp"></div>
            <n-tag v-if="gotCount === 3" type="success" size="small" :bordered="false">已集齐 3/3</n-tag>
            <n-tag v-else type="warning" size="small" :bordered="false">已传 {{ gotCount }}/3 · 缺{{ fileGroups.filter(g => !g.cur).map(g => g.label).join('、') }}</n-tag>
          </div>
          <div class="small faint" style="margin-bottom:6px">出报告必传三类；当前上传的即为最终版，点「新增版本」更新后，旧版自动转入历史版本。</div>
          <div class="fcat" v-for="g in fileGroups" :key="g.key">
            <div class="fcat-h"><b style="font-size:13.5px">{{ g.label }}</b>
              <n-tag v-if="g.cur" size="tiny" type="success" :bordered="false">最终版 V{{ g.cur.version }}</n-tag>
              <span v-else class="small" style="color:#9a6a12">未上传</span>
              <div style="flex:1"></div>
              <n-button size="tiny" type="primary" ghost @click="pickFile(f => uploadDeliverable(g.key, f))">{{ g.cur ? '+ 新增版本' : '上传最终版' }}</n-button>
            </div>
            <div v-if="g.cur" class="file-it">
              <div class="file-ic">{{ g.icon }}</div>
              <div style="flex:1;min-width:0">
                <b style="font-size:13px">{{ g.cur.fileName }}</b>
                <div class="psub">{{ fmtSize(g.cur.sizeBytes) }} · {{ String(g.cur.uploadedAt).slice(0, 10) }}<span v-if="parseOf(g.cur.id)"> · 已自动解析 · 工程造价 ¥{{ fmt(parseOf(g.cur.id).totalAmount) }}</span></div>
              </div>
              <n-button v-if="parseOf(g.cur.id)" size="small" type="primary" @click="router.push(`/projects/${id}/gcfx/${g.cur.id}`)">查看解析</n-button>
              <n-button v-if="fileKind(g.cur.fileName) !== 'other'" size="small" @click="previewFile('deliverables', g.cur)">预览</n-button>
              <n-button size="small" @click="downloadFile('deliverables', g.cur.id, g.cur.fileName)">下载</n-button>
              <n-button size="small" quaternary @click="confirmDelete('删除最终版文件？', async () => { await api.delete(`/projects/${id}/files/deliverables/${g.cur.id}`); load() })">删除</n-button>
            </div>
            <div v-else class="small faint" style="padding:4px 0 8px">暂无文件</div>
            <div v-if="g.hist.length" style="margin:2px 0 8px">
              <div style="display:flex;align-items:center;gap:8px;cursor:pointer;padding:4px 0" @click="toggleHist(g.key)">
                <span class="small mut">历史版本（{{ g.hist.length }}）</span>
                <span class="faint" style="margin-left:auto">{{ histOpen[g.key] ? '▾ 收起' : '▸ 展开' }}</span>
              </div>
              <div v-if="histOpen[g.key]">
                <div class="fhist" v-for="hf in g.hist" :key="hf.id">
                  <n-tag size="tiny" :bordered="false">V{{ hf.version }} 历史版本</n-tag>
                  <span style="flex:1;min-width:0">{{ hf.fileName }}<span class="faint"> · {{ String(hf.uploadedAt).slice(0, 10) }}</span></span>
                  <n-button v-if="fileKind(hf.fileName) !== 'other'" size="tiny" quaternary @click="previewFile('deliverables', hf)">预览</n-button>
                  <n-button size="tiny" quaternary @click="downloadFile('deliverables', hf.id, hf.fileName)">下载</n-button>
                  <n-button size="tiny" quaternary @click="confirmDelete('删除该历史版本？', async () => { await api.delete(`/projects/${id}/files/deliverables/${hf.id}`); load() })">删除</n-button>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div class="card">
          <div class="card-h"><h3>项目资料</h3></div>
          <div class="small faint" style="margin-bottom:4px">图纸、隐蔽资料、联系函等过程文件按分类归档，与成果文件分开保存。</div>
          <div class="doc-cat" v-for="g in docGroups" :key="g.cat">
            <div style="display:flex;align-items:center;gap:9px;cursor:pointer;padding:3px 0" @click="openDocCat = openDocCat === g.cat ? '' : g.cat">
              <span class="role-tag" style="background:#eef4f0;font-size:11.5px">{{ g.cat.slice(0, 1) }}</span>
              <b style="font-size:13px">{{ g.cat }}</b>
              <span class="faint small">{{ g.files.length }} 个文件</span>
              <span class="faint" style="margin-left:auto">{{ openDocCat === g.cat ? '▾' : '▸' }}</span>
            </div>
            <div v-if="openDocCat === g.cat" style="padding:2px 0 6px 35px">
              <div class="doc-f" v-for="f in g.files" :key="f.id">
                <span style="flex:1;min-width:0">{{ f.fileName }}</span>
                <span class="faint">{{ String(f.uploadedAt).slice(0, 10) }}</span>
                <n-button v-if="fileKind(f.fileName) !== 'other'" size="tiny" quaternary @click="previewFile('docs', f)">预览</n-button>
                <n-button size="tiny" quaternary @click="downloadFile('docs', f.id, f.fileName)">下载</n-button>
                <n-button size="tiny" quaternary @click="confirmDelete('删除该资料文件？', async () => { await api.delete(`/projects/${id}/files/docs/${f.id}`); load() })">删除</n-button>
              </div>
              <div v-if="!g.files.length" class="doc-f faint">暂无文件</div>
              <n-button size="tiny" style="margin-top:4px" @click="pickFile(f => uploadDoc(g.cat, f))">+ 上传到此分类</n-button>
            </div>
          </div>
        </div>
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
    <n-modal v-model:show="showTeam" preset="card" title="编辑团队分工" style="width:560px;border-radius:16px">
      <div v-for="(r, i) in teamRows" :key="i" style="display:flex;gap:8px;margin-bottom:10px;align-items:center">
        <n-select v-model:value="r.memberId" :options="members.map(m => ({ label: m.name, value: m.id }))" placeholder="成员" style="flex:1.2" />
        <n-select v-model:value="r.role" :options="ROLES.map(t => ({ label: t, value: t }))" style="flex:1" />
        <n-input-number v-model:value="r.share" :min="0" :max="100" style="width:110px" />
        <span class="small mut">%</span>
        <n-button quaternary @click="teamRows.splice(i, 1)">✕</n-button>
      </div>
      <n-button dashed block @click="teamRows.push({ memberId: null, role: '编制人', share: 0 })">+ 添加成员</n-button>
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

    <!-- 登记收款 -->
    <n-modal v-model:show="showReceipt" preset="card" title="登记收款" style="width:480px;border-radius:16px">
      <n-form label-placement="top">
        <n-form-item label="收款日期"><n-date-picker v-model:formatted-value="rf.date" value-format="yyyy-MM-dd" type="date" style="width:100%" /></n-form-item>
        <n-form-item label="金额（万元）"><n-input-number v-model:value="rf.amount" :min="0" style="width:100%" /></n-form-item>
        <n-grid :cols="2" :x-gap="12">
          <n-form-item-gi label="方式"><n-select v-model:value="rf.method" :options="['银行转账', '现金', '承兑汇票', '其他'].map(t => ({ label: t, value: t }))" /></n-form-item-gi>
          <n-form-item-gi label="经办人"><n-input v-model:value="rf.handler" /></n-form-item-gi>
        </n-grid>
        <n-form-item label="备注"><n-input v-model:value="rf.note" /></n-form-item>
      </n-form>
      <template #footer>
        <div style="display:flex;justify-content:flex-end;gap:10px">
          <n-button @click="showReceipt = false">取消</n-button>
          <n-button type="primary" @click="addReceipt">保存收款</n-button>
        </div>
      </template>
    </n-modal>

    <PreviewModal ref="previewRef" />
  </div>
  <div v-else class="card"><n-spin size="large" /></div>
</template>
