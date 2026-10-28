<script setup>
import { ref, reactive, computed, watch } from 'vue'
import { useMessage } from 'naive-ui'
import { calcFees, FEE_DEFAULTS } from '../utils/feecalc'
import * as F from '../utils/fees'
import FeeResult from '../components/FeeResult.vue'

// 收费测算：造价咨询（四川双标准）/ 招标代理 / 工程监理 / 工程设计 / 项目咨询管理 五个计算器
// 与独立站同源（utils/fees.js 为唯一实现，数据按各依据文件原文）；其他费测算沿用 utils/feecalc.js。
// 各模块算完可「添加至统计表」，底部统计表统一汇总、删除、打印。
const message = useMessage()
const tab = ref('zj')
const R = reactive({ zj141: null, zj56: null, bid: null, jl: null, sj: null, pcm: null })
const rngY = (a) => a[0] === a[1] ? F.fmtYuan(a[0]) : F.fmtYuan(a[0]) + ' 至 ' + F.fmtYuan(a[1])

/* ---------- 统计表（全模块共用） ---------- */
const stats = ref([])
function pushStat(res, disc) {
  if (!res || res.warn) return
  stats.value.push({ name: res.title, std: res.std, disc: [res.std[0] * disc / 100, res.std[1] * disc / 100] })
  message.success('已添加至统计表')
}
const statsTotal = computed(() => {
  const t = { std: [0, 0], disc: [0, 0] }
  stats.value.forEach(s => { t.std[0] += s.std[0]; t.std[1] += s.std[1]; t.disc[0] += s.disc[0]; t.disc[1] += s.disc[1] })
  return t
})
const statsColumns = [
  { title: '序号', key: 'i', width: 56, render: (r, i) => i + 1 },
  { title: '收费项目', key: 'name' },
  { title: '标准收费（元）', key: 'std', width: 230, align: 'right', render: (r) => rngY(r.std) },
  { title: '折扣后（元）', key: 'disc', width: 230, align: 'right', render: (r) => rngY(r.disc) },
  { title: '', key: 'act', width: 70, render: (r, i) => hDel(i) }
]
import { h } from 'vue'
import { NButton } from 'naive-ui'
function hDel(i) {
  return h(NButton, { size: 'tiny', tertiary: true, type: 'error', onClick: () => stats.value.splice(i, 1) }, () => '删除')
}

/* ---------- 造价咨询 ---------- */
const zjBasis = ref('141')
// 141 号
const z1 = reactive({ item: 3, base: null, qty: null, civil: false, withBp: false, cut: null, cutRate: 4, disc: 100 })
const z1Item = computed(() => F.ZJ_ITEMS[z1.item])
const z1Opts = F.ZJ_ITEMS.map((it, i) => ({ label: it.n, value: i }))
function doZ1() {
  R.zj141 = F.calcZJ141({ item: z1.item, base: z1.base || 0, qty: z1.qty || 0, civil: z1.civil, withBp: z1.withBp, cut: z1.cut || 0, cutRate: z1.cutRate || 0 })
}
// 56 号
const z5 = reactive({ method: 'rate', item: 38, base: null, qty: null, mode: 'area', specIdx: 0, cplx: 1, dayPrice: 3985, withBp: false, cut: null, cutRate: 4, disc: 100 })
const z5Item = computed(() => F.ZJ56_ITEMS[z5.item])
const z5Spec = computed(() => F.ZJ56_SPEC[z5.specIdx][1])
const z5Opts = computed(() => {
  const groups = {}
  F.ZJ56_ITEMS.forEach((it, i) => {
    if (z5.method === 'days' && !it.d) return
    ;(groups[it.g] = groups[it.g] || []).push({ label: it.code + ' ' + it.n, value: i })
  })
  return Object.entries(groups).map(([g, children]) => ({ type: 'group', label: g, key: g, children }))
})
watch(() => z5.method, () => {
  const it = F.ZJ56_ITEMS[z5.item]
  if (z5.method === 'days' && !(it && it.d)) z5.item = 38
})
const z5SpecOpts = F.ZJ56_SPEC.map(([n, v], i) => ({ label: n + '（' + v + '）', value: i }))
const z5Special = computed(() => { const it = z5Item.value; return !!(it && (it.unit || it.bim || it.na)) })
function doZ5() {
  R.zj56 = F.calcZJ56({ item: z5.item, method: z5.method, base: z5.base || 0, qty: z5.qty || 0, mode: z5.mode, spec: z5Spec.value, cplx: z5.cplx || 0, dayPrice: z5.dayPrice, withBp: z5.withBp, cut: z5.cut || 0, cutRate: z5.cutRate || 0 })
}

/* ---------- 招标代理 ---------- */
const bd = reactive({ type: 'eng', base: null, disc: 100 })
const bdOpts = Object.entries(F.BID_TYPES).map(([k, v]) => ({ label: v.n + '（上限 ' + v.cap + ' 万元）', value: k }))
function doBD() { R.bid = F.calcBID({ type: bd.type, base: bd.base || 0 }) }

/* ---------- 工程监理 / 工程设计（专业系数选项值用「组-项」键，避免同系数多选项在下拉里串名） ---------- */
function profOpts(PROF) {
  return PROF.map(([g, items], gi) => ({
    type: 'group', label: g, key: g,
    children: items.map(([n, v], ii) => ({ label: n + ' ×' + v, value: gi + '-' + ii }))
  }))
}
function profVal(PROF, key) {
  const [gi, ii] = String(key).split('-').map(Number)
  return PROF[gi][1][ii][1]
}
const jl = reactive({ base: null, profKey: '5-1', cplx: 1, alt: 1, disc: 100 })
const jlProfOpts = profOpts(F.ESF_PROF)
function doJL() { R.jl = F.calcJL({ base: jl.base || 0, prof: profVal(F.ESF_PROF, jl.profKey), cplx: jl.cplx, alt: jl.alt }) }
const sj = reactive({ base: null, profKey: '5-1', cplx: 1, extra: 1, disc: 100 })
const sjProfOpts = profOpts(F.ED_PROF)
function doSJ() { R.sj = F.calcSJ({ base: sj.base || 0, prof: profVal(F.ED_PROF, sj.profKey), cplx: sj.cplx, extra: sj.extra || 0 }) }

/* ---------- 项目咨询管理 ---------- */
const pm = reactive({ kind: 'glf', base: null, qqKind: 1, qqInd: 1, qqCplx: 1, hpKind: 0, hpInd: 1, sens: 0.8, topics: 0, down534: false, sbKind: 0, ter: 1.2, disc: 100 })
const pmKindOpts = [
  { label: '项目建设管理费（财建〔2016〕504号）', value: 'glf' },
  { label: '前期工作咨询费（计价格〔1999〕1283号）', value: 'qq' },
  { label: '环境影响咨询费（计价格〔2002〕125号）', value: 'hp' },
  { label: '水土保持相关费（保监〔2005〕22号）', value: 'sb' }
]
const qqKindOpts = F.QQ_NAMES.map((n, i) => ({ label: n, value: i }))
const hpKindOpts = F.HP_NAMES.map((n, i) => ({ label: n, value: i }))
const sbKindOpts = F.SB_NAMES.map((n, i) => ({ label: n, value: i }))
function doPM() {
  const s = { kind: pm.kind, base: pm.base || 0 }
  if (pm.kind === 'qq') Object.assign(s, { qqKind: pm.qqKind, ind: pm.qqInd, cplx: pm.qqCplx })
  if (pm.kind === 'hp') Object.assign(s, { hpKind: pm.hpKind, ind: pm.hpInd, sens: pm.sens, topics: pm.topics || 0, down534: pm.down534 })
  if (pm.kind === 'sb') Object.assign(s, { sbKind: pm.sbKind, ter: pm.ter })
  R.pcm = F.calcPCM(s)
}

/* ---------- 工程建设其他费（原实现，口径走 utils/feecalc.js） ---------- */
const ofInp = reactive({ ...FEE_DEFAULTS })
const ofRes = computed(() => calcFees(ofInp))
const fmt4 = (x) => Number(x).toFixed(4)
const HP_OPTS = [
  { label: '区间中值', value: 'mid' },
  { label: '区间下限', value: 'low' },
  { label: '区间上限', value: 'high' }
]
const ofRows = computed(() => {
  if (!ofRes.value) return []
  const r = ofRes.value
  return [
    { no: '1', name: '工程直接费用（建安工程费）', proc: '输入值', amt: r.jianan, cls: 'sub' },
    ...r.rows.map(x => ({ no: x.no, name: x.name, proc: x.proc, amt: x.amt, basis: x.basis, cls: x.sub ? 'sub' : '' })),
    { no: '2', name: '工程其他费用合计', proc: '2.1 + … + 2.13', amt: r.other, cls: 'sub' },
    { no: '3.1', name: '基本预备费', proc: '（建安费 + 其他费）× ' + (ofInp.yubeiRate || 0) + '%', amt: r.yubei, cls: '' },
    { no: '4', name: '工程建设概算总投资', proc: '1 + 2 + 3', amt: r.total, cls: 'total' }
  ]
})
const ofColumns = [
  { title: '序号', key: 'no', width: 62 },
  { title: '费用名称', key: 'name', render: (r) => r.name },
  { title: '计算过程', key: 'proc', render: (r) => r.proc },
  { title: '金额（万元）', key: 'amt', width: 138, align: 'right', render: (r) => fmt4(r.amt) }
]
function ofRowProps(r) { return r.cls ? { class: 'fee-' + r.cls } : {} }
function doPrint() { window.print() }
</script>

<template>
  <div>
    <div class="card" style="padding:10px 14px">
      <n-tabs v-model:value="tab" type="segment" style="max-width:860px">
        <n-tab name="zj">造价咨询</n-tab>
        <n-tab name="bid">招标代理</n-tab>
        <n-tab name="jl">工程监理</n-tab>
        <n-tab name="sj">工程设计</n-tab>
        <n-tab name="pcm">项目咨询管理</n-tab>
        <n-tab name="other">其他费测算</n-tab>
      </n-tabs>
    </div>

    <!-- ============ 造价咨询 ============ -->
    <div v-show="tab === 'zj'" class="card">
      <div class="card-h"><span class="card-ic g"><svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="4" y="2" width="16" height="20" rx="2"/><line x1="8" y1="6" x2="16" y2="6"/><line x1="8" y1="11" x2="8.01" y2="11"/><line x1="12" y1="11" x2="12.01" y2="11"/><line x1="16" y1="11" x2="16.01" y2="11"/><line x1="8" y1="15" x2="8.01" y2="15"/><line x1="12" y1="15" x2="12.01" y2="15"/><line x1="16" y1="15" x2="16.01" y2="15"/></svg></span><h3>造价咨询服务费（四川省）</h3></div>
      <n-form label-placement="top" :show-feedback="false">
        <n-grid :cols="2" :x-gap="14" :y-gap="4" responsive="screen">
          <n-form-item-gi label="计费依据">
            <n-select v-model:value="zjBasis" :options="[{ label: '川价发〔2008〕141号（省物价局、省建设厅）', value: '141' }, { label: '川建价师协〔2022〕56号 试行（省造价工程师协会）', value: '56' }]" />
          </n-form-item-gi>
        </n-grid>
      </n-form>

      <!-- 141 号 -->
      <div v-show="zjBasis === '141'">
        <p class="small mut" style="line-height:1.75;margin:2px 0 10px">川价发〔2008〕141号：分档费率按差额定率累进法计算；公路、铁路、水、坝等土木工程按标准降低 50%；编制工程量清单含招标控制价时收费系数乘 1.25。</p>
        <n-form label-placement="top" :show-feedback="false">
          <n-grid :cols="2" :x-gap="14" :y-gap="4" responsive="screen">
            <n-form-item-gi :span="2" label="收费项目"><n-select v-model:value="z1.item" :options="z1Opts" /></n-form-item-gi>
            <n-form-item-gi v-if="!z1Item.unit" :label="(z1Item.base || '计费基数') + '（万元）'"><n-input-number v-model:value="z1.base" :min="0" :precision="4" placeholder="如 559.108" style="width:100%" /></n-form-item-gi>
            <n-form-item-gi v-if="z1Item.unit" :label="z1Item.unit.qlabel"><n-input-number v-model:value="z1.qty" :min="0" style="width:100%" /><div class="small faint">单价 {{ z1Item.unit.price[0] === z1Item.unit.price[1] ? z1Item.unit.price[0] : z1Item.unit.price[0] + '–' + z1Item.unit.price[1] }} {{ z1Item.unit.qunit }}</div></n-form-item-gi>
            <n-form-item-gi v-if="z1Item.cut" label="审减（审增）额（万元，选填）"><n-input-number v-model:value="z1.cut" :min="0" :precision="4" style="width:100%" /><div class="small faint">审核竣工结算另按审减（审增）额加收 3%–5% 效益费</div></n-form-item-gi>
            <n-form-item-gi v-if="z1Item.cut" label="效益费加收费率（%）"><n-input-number v-model:value="z1.cutRate" :min="3" :max="5" :step="0.1" style="width:100%" /></n-form-item-gi>
            <n-form-item-gi label="折扣（%）"><n-input-number v-model:value="z1.disc" :min="0" :max="100" style="width:100%" /><div class="small faint">100 = 不打折，80 = 八折</div></n-form-item-gi>
            <n-form-item-gi v-if="!z1Item.unit"><n-checkbox v-model:checked="z1.civil">土木工程（公路/铁路/水/坝等，收费降 50%）</n-checkbox></n-form-item-gi>
            <n-form-item-gi v-if="z1Item.bp"><n-checkbox v-model:checked="z1.withBp">清单含招标控制价（系数 ×1.25）</n-checkbox></n-form-item-gi>
          </n-grid>
        </n-form>
        <div style="display:flex;gap:10px;margin-top:10px">
          <n-button type="primary" @click="doZ1">开始计算</n-button>
          <n-button v-if="R.zj141 && !R.zj141.warn" @click="pushStat(R.zj141, z1.disc)">添加至统计表</n-button>
        </div>
        <FeeResult :res="R.zj141" :disc="z1.disc" />
      </div>

      <!-- 56 号 -->
      <div v-show="zjBasis === '56'">
        <p class="small mut" style="line-height:1.75;margin:2px 0 10px">川建价师协〔2022〕56号（试行）：差额定率累进法（收费＝Σ各档收费×专业调整系数×工程复杂程度调整系数）或人工工日法（工日数分档双线性内插×工日单价）。清单编制含招标控制价编制时费率乘 1.5；竣工结算审核（或审计）可另按核减、核增额之和的 3%–5% 增收效益服务费；单项收费低于 5000 元按 5000 元计；计费基数包含甲供材料。</p>
        <n-form label-placement="top" :show-feedback="false">
          <n-grid :cols="2" :x-gap="14" :y-gap="4" responsive="screen">
            <n-form-item-gi label="计费方法"><n-select v-model:value="z5.method" :options="[{ label: '差额定率累进法', value: 'rate' }, { label: '人工工日法', value: 'days' }]" /></n-form-item-gi>
            <n-form-item-gi :span="2" label="收费项目"><n-select v-model:value="z5.item" :options="z5Opts" filterable /></n-form-item-gi>
            <n-form-item-gi v-if="!z5Special" :label="(z5Item.base || '计费基数') + '（万元）'"><n-input-number v-model:value="z5.base" :min="0" :precision="4" placeholder="如 559.108" style="width:100%" /></n-form-item-gi>
            <n-form-item-gi v-if="z5Item.unit" :label="z5Item.unit.qlabel"><n-input-number v-model:value="z5.qty" :min="0" style="width:100%" /><div class="small faint">单价 {{ z5Item.unit.price[0] === z5Item.unit.price[1] ? z5Item.unit.price[0] : z5Item.unit.price[0] + '–' + z5Item.unit.price[1] }} {{ z5Item.unit.qunit }}</div></n-form-item-gi>
            <n-form-item-gi v-if="z5Item.bim" label="BIM 咨询计费方式"><n-select v-model:value="z5.mode" :options="[{ label: '按建筑面积（元/㎡）', value: 'area' }, { label: '按工程造价（%）', value: 'pct' }]" /></n-form-item-gi>
            <n-form-item-gi v-if="z5Item.bim" :label="z5.mode === 'area' ? '建筑面积（㎡）' : '工程造价（万元）'"><n-input-number v-model:value="z5.qty" :min="0" style="width:100%" /><div class="small faint">{{ z5.mode === 'area' ? '单价 ' + z5Item.bim.area[0] + '–' + z5Item.bim.area[1] + ' 元/㎡' : '费率 ' + z5Item.bim.pct[0] + '%–' + z5Item.bim.pct[1] + '%' }}{{ z5Item.bim.note ? '（' + z5Item.bim.note + '）' : '' }}</div></n-form-item-gi>
            <n-form-item-gi v-if="!z5Special" label="专业调整系数"><n-select v-model:value="z5.specIdx" :options="z5SpecOpts" /></n-form-item-gi>
            <n-form-item-gi v-if="!z5Special" label="工程复杂程度调整系数"><n-input-number v-model:value="z5.cplx" :min="0" :step="0.01" style="width:100%" /><div class="small faint">其他项目 1.0；新工艺智能建造 1.1–1.2；超大体量公共建筑、非标设备、超高层 1.2–1.3</div></n-form-item-gi>
            <n-form-item-gi v-if="z5.method === 'days'" label="人工工日单价"><n-select v-model:value="z5.dayPrice" :options="[{ label: '一级注册造价工程师 3985 元/工日', value: 3985 }, { label: '二级注册造价工程师 2988 元/工日', value: 2988 }, { label: '其他工程造价技术人员 2391 元/工日', value: 2391 }]" /><div class="small faint">人工消耗量以一级注册造价工程师为标准测算</div></n-form-item-gi>
            <n-form-item-gi v-if="z5Item.bp && z5.method === 'rate'"><n-checkbox v-model:checked="z5.withBp">清单含编制招标控制价（最高投标限价、标底）（费率 ×1.5）</n-checkbox></n-form-item-gi>
            <n-form-item-gi v-if="z5Item.cut && z5.method === 'rate'" label="核减＋核增额（万元，选填）"><n-input-number v-model:value="z5.cut" :min="0" :precision="4" style="width:100%" /><div class="small faint">竣工结算审核（或审计）另按核减、核增额之和加收 3%–5% 效益服务费</div></n-form-item-gi>
            <n-form-item-gi v-if="z5Item.cut && z5.method === 'rate'" label="效益费率（%）"><n-input-number v-model:value="z5.cutRate" :min="3" :max="5" :step="0.1" style="width:100%" /></n-form-item-gi>
            <n-form-item-gi label="折扣（%）"><n-input-number v-model:value="z5.disc" :min="0" :max="100" style="width:100%" /><div class="small faint">100 = 不打折，80 = 八折</div></n-form-item-gi>
          </n-grid>
        </n-form>
        <div style="display:flex;gap:10px;margin-top:10px">
          <n-button type="primary" @click="doZ5">开始计算</n-button>
          <n-button v-if="R.zj56 && !R.zj56.warn" @click="pushStat(R.zj56, z5.disc)">添加至统计表</n-button>
        </div>
        <FeeResult :res="R.zj56" :disc="z5.disc" />
      </div>
    </div>

    <!-- ============ 招标代理 ============ -->
    <div v-show="tab === 'bid'" class="card">
      <div class="card-h"><span class="card-ic b"><svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z"/><path d="M14 3v5h5M9 13h6M9 17h4"/></svg></span><h3>招标代理服务费</h3></div>
      <p class="small mut" style="line-height:1.75;margin:2px 0 10px">依据：《招标代理服务收费管理暂行办法》计价格〔2002〕1980号费率表，按发改价格〔2011〕534号下调后的标准执行（下浮幅度与浮动另行约定，此处按标准费率计算）。按中标金额分档差额定率累进，工程/货物/服务分别设收费上限。</p>
      <n-form label-placement="top" :show-feedback="false">
        <n-grid :cols="2" :x-gap="14" :y-gap="4" responsive="screen">
          <n-form-item-gi label="招标类型"><n-select v-model:value="bd.type" :options="bdOpts" /></n-form-item-gi>
          <n-form-item-gi label="中标金额（万元）"><n-input-number v-model:value="bd.base" :min="0" :precision="4" placeholder="如 3000" style="width:100%" /></n-form-item-gi>
          <n-form-item-gi label="折扣（%）"><n-input-number v-model:value="bd.disc" :min="0" :max="100" style="width:100%" /><div class="small faint">100 = 不打折，80 = 八折</div></n-form-item-gi>
        </n-grid>
      </n-form>
      <div style="display:flex;gap:10px;margin-top:10px">
        <n-button type="primary" @click="doBD">开始计算</n-button>
        <n-button v-if="R.bid && !R.bid.warn" @click="pushStat(R.bid, bd.disc)">添加至统计表</n-button>
      </div>
      <FeeResult :res="R.bid" :disc="bd.disc" />
    </div>

    <!-- ============ 工程监理 ============ -->
    <div v-show="tab === 'jl'" class="card">
      <div class="card-h"><span class="card-ic tl"><svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7-10-7-10-7z"/><circle cx="12" cy="12" r="3"/></svg></span><h3>工程监理服务费（施工监理）</h3></div>
      <p class="small mut" style="line-height:1.75;margin:2px 0 10px">依据：《建设工程监理与相关服务收费管理规定》发改价格〔2007〕670号。施工监理服务收费＝收费基价×专业调整系数×工程复杂程度调整系数×高程调整系数；收费基价按计费额内插，计费额超过 1000000 万元（100 亿元）按 1.039% 计。注：该文件已由发改价格〔2015〕299号改为市场调节价，本表供参考。</p>
      <n-form label-placement="top" :show-feedback="false">
        <n-grid :cols="2" :x-gap="14" :y-gap="4" responsive="screen">
          <n-form-item-gi label="计费额（工程概预算，万元）"><n-input-number v-model:value="jl.base" :min="0" :precision="4" placeholder="如 5000" style="width:100%" /></n-form-item-gi>
          <n-form-item-gi label="专业调整系数"><n-select v-model:value="jl.profKey" :options="jlProfOpts" /></n-form-item-gi>
          <n-form-item-gi label="工程复杂程度调整系数"><n-select v-model:value="jl.cplx" :options="F.JL_CPLX" /></n-form-item-gi>
          <n-form-item-gi label="海拔高程调整系数"><n-select v-model:value="jl.alt" :options="F.JL_ALT" /></n-form-item-gi>
          <n-form-item-gi label="折扣（%）"><n-input-number v-model:value="jl.disc" :min="0" :max="100" style="width:100%" /><div class="small faint">100 = 基准价；政府指导价浮动幅度 ±20%</div></n-form-item-gi>
        </n-grid>
      </n-form>
      <div style="display:flex;gap:10px;margin-top:10px">
        <n-button type="primary" @click="doJL">开始计算</n-button>
        <n-button v-if="R.jl && !R.jl.warn" @click="pushStat(R.jl, jl.disc)">添加至统计表</n-button>
      </div>
      <FeeResult :res="R.jl" :disc="jl.disc" />
    </div>

    <!-- ============ 工程设计 ============ -->
    <div v-show="tab === 'sj'" class="card">
      <div class="card-h"><span class="card-ic p"><svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 19l7-7 3 3-7 7-3-3z"/><path d="M18 13l-1.5-7.5L2 2l3.5 14.5L13 18l5-5z"/><path d="M2 2l7.586 7.586"/><circle cx="11" cy="11" r="2"/></svg></span><h3>工程设计费（基本设计收费）</h3></div>
      <p class="small mut" style="line-height:1.75;margin:2px 0 10px">依据：《工程勘察设计收费管理规定》计价格〔2002〕10号。基本设计收费＝收费基价×专业调整系数×工程复杂程度调整系数×附加调整系数；收费基价按计费额内插，计费额超过 2000000 万元按 1.6% 计。</p>
      <n-form label-placement="top" :show-feedback="false">
        <n-grid :cols="2" :x-gap="14" :y-gap="4" responsive="screen">
          <n-form-item-gi label="计费额（工程概预算，万元）"><n-input-number v-model:value="sj.base" :min="0" :precision="4" placeholder="如 10000" style="width:100%" /></n-form-item-gi>
          <n-form-item-gi label="专业调整系数"><n-select v-model:value="sj.profKey" :options="sjProfOpts" /></n-form-item-gi>
          <n-form-item-gi label="工程复杂程度调整系数"><n-select v-model:value="sj.cplx" :options="F.SJ_CPLX" /></n-form-item-gi>
          <n-form-item-gi label="附加调整系数"><n-input-number v-model:value="sj.extra" :min="0" :step="0.05" style="width:100%" /><div class="small faint">一般取 1.0；改扩建、特殊工程等按文件取 1.1–1.4 等</div></n-form-item-gi>
          <n-form-item-gi label="折扣（%）"><n-input-number v-model:value="sj.disc" :min="0" :max="100" style="width:100%" /><div class="small faint">100 = 不打折，80 = 八折</div></n-form-item-gi>
        </n-grid>
      </n-form>
      <div style="display:flex;gap:10px;margin-top:10px">
        <n-button type="primary" @click="doSJ">开始计算</n-button>
        <n-button v-if="R.sj && !R.sj.warn" @click="pushStat(R.sj, sj.disc)">添加至统计表</n-button>
      </div>
      <FeeResult :res="R.sj" :disc="sj.disc" />
    </div>

    <!-- ============ 项目咨询管理 ============ -->
    <div v-show="tab === 'pcm'" class="card">
      <div class="card-h"><span class="card-ic o"><svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 12V7H5a2 2 0 0 1 0-4h14v4"/><path d="M3 5v14a2 2 0 0 0 2 2h16v-5"/><path d="M18 12a2 2 0 0 0 0 4h4v-4z"/></svg></span><h3>项目咨询管理费（国家标准）</h3></div>
      <n-form label-placement="top" :show-feedback="false">
        <n-grid :cols="2" :x-gap="14" :y-gap="4" responsive="screen">
          <n-form-item-gi :span="2" label="收费项目"><n-select v-model:value="pm.kind" :options="pmKindOpts" /></n-form-item-gi>
          <n-form-item-gi v-if="pm.kind === 'glf'" label="工程总概算（万元）"><n-input-number v-model:value="pm.base" :min="0" :precision="4" placeholder="如 5000" style="width:100%" /><div class="small faint">财建〔2016〕504号附件2：按工程总概算分档累进（2% / 1.5% / 1.2% / 1% / 0.8% / 0.4%）</div></n-form-item-gi>
          <template v-if="pm.kind === 'qq'">
            <n-form-item-gi label="咨询类型"><n-select v-model:value="pm.qqKind" :options="qqKindOpts" /></n-form-item-gi>
            <n-form-item-gi label="估算投资额（万元）"><n-input-number v-model:value="pm.base" :min="0" :precision="4" placeholder="如 50000" style="width:100%" /></n-form-item-gi>
            <n-form-item-gi label="行业调整系数"><n-select v-model:value="pm.qqInd" :options="F.QQ_IND" /></n-form-item-gi>
            <n-form-item-gi label="工程复杂程度调整系数"><n-input-number v-model:value="pm.qqCplx" :min="0.8" :max="1.2" :step="0.05" style="width:100%" /><div class="small faint">文件规定 0.8–1.2，由咨询机构与委托方协商确定</div></n-form-item-gi>
          </template>
          <template v-if="pm.kind === 'hp'">
            <n-form-item-gi label="咨询类型"><n-select v-model:value="pm.hpKind" :options="hpKindOpts" /></n-form-item-gi>
            <n-form-item-gi label="估算投资额（万元）"><n-input-number v-model:value="pm.base" :min="0" :precision="4" placeholder="如 50000" style="width:100%" /><div class="small faint">费率表按亿元分档，内部自动换算</div></n-form-item-gi>
            <n-form-item-gi label="行业调整系数"><n-select v-model:value="pm.hpInd" :options="F.HP_IND" /></n-form-item-gi>
            <n-form-item-gi label="环境敏感程度调整系数"><n-select v-model:value="pm.sens" :options="F.HP_SENS" /></n-form-item-gi>
            <n-form-item-gi v-if="pm.hpKind === 1" label="报告表评价专题数（个）"><n-input-number v-model:value="pm.topics" :min="0" :step="1" style="width:100%" /><div class="small faint">编制报告表每增加一个评价专题加收 50%</div></n-form-item-gi>
            <n-form-item-gi><n-checkbox v-model:checked="pm.down534">属发改价格〔2011〕534号下调 20% 行业（仅编制类）</n-checkbox></n-form-item-gi>
          </template>
          <template v-if="pm.kind === 'sb'">
            <n-form-item-gi label="费用类型"><n-select v-model:value="pm.sbKind" :options="sbKindOpts" /></n-form-item-gi>
            <n-form-item-gi label="主体工程土建投资（万元）"><n-input-number v-model:value="pm.base" :min="0" :precision="4" placeholder="如 30000" style="width:100%" /><div class="small faint">点值表按亿元内插，内部自动换算</div></n-form-item-gi>
            <n-form-item-gi v-if="pm.sbKind === 1" label="地貌类型调整系数（仅监测费）"><n-select v-model:value="pm.ter" :options="F.SB_TER" /></n-form-item-gi>
          </template>
          <n-form-item-gi label="折扣（%）"><n-input-number v-model:value="pm.disc" :min="0" :max="100" style="width:100%" /><div class="small faint">100 = 不打折，80 = 八折</div></n-form-item-gi>
        </n-grid>
      </n-form>
      <p v-if="pm.kind === 'qq'" class="small mut" style="margin-top:8px">注：计价格〔1999〕1283号只规定估算投资额 3000 万元以上的收费标准，3000 万元以下由各省制定；低于 3000 万元时按第一档下限给出参考值。</p>
      <div style="display:flex;gap:10px;margin-top:10px">
        <n-button type="primary" @click="doPM">开始计算</n-button>
        <n-button v-if="R.pcm && !R.pcm.warn" @click="pushStat(R.pcm, pm.disc)">添加至统计表</n-button>
      </div>
      <FeeResult :res="R.pcm" :disc="pm.disc" />
    </div>

    <!-- ============ 工程建设其他费（原页签） ============ -->
    <div v-show="tab === 'other'" class="fee-print">
      <div class="noprint" style="display:flex;gap:10px;align-items:center;margin-bottom:14px">
        <div class="small mut">按四川常用取费口径计算：填基本数据后实时出结果；费率与下浮在「参数调整」里改。金额单位除注明外均为万元，保留 4 位小数。</div>
        <div style="flex:1"></div>
        <n-button type="primary" @click="doPrint">
          <svg viewBox="0 0 24 24" width="15" height="15" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="margin-right:5px;vertical-align:-2px"><polyline points="6 9 6 2 18 2 18 9"/><path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"/><rect x="6" y="14" width="12" height="8"/></svg>
          打印结果
        </n-button>
      </div>

      <div class="card noprint">
        <div class="card-h"><span class="card-ic g"><svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/></svg></span><h3>基本数据</h3></div>
        <n-form label-placement="top" :show-feedback="false">
          <n-grid :cols="2" :x-gap="14" :y-gap="4" responsive="screen" :collapsed-rows="3">
            <n-form-item-gi :span="2" label="工程名称（可选，打印时显示）"><n-input v-model:value="ofInp.pname" placeholder="如：XX项目" /></n-form-item-gi>
            <n-form-item-gi label="建安工程费（工程直接费用）*"><n-input-number v-model:value="ofInp.jianan" :min="0" :precision="4" placeholder="如 559.108" style="width:100%" /><div class="small faint">万元；各项取费的主要基数</div></n-form-item-gi>
            <n-form-item-gi label="建筑面积"><n-input-number v-model:value="ofInp.jianzhu" :min="0" :precision="2" placeholder="如 1743.10" style="width:100%" /><div class="small faint">㎡；用于施工图审查费</div></n-form-item-gi>
            <n-form-item-gi label="征占地面积"><n-input-number v-model:value="ofInp.zhandi" :min="0" :precision="2" placeholder="如 5033.09" style="width:100%" /><div class="small faint">㎡；用于水土保持补偿费</div></n-form-item-gi>
            <n-form-item-gi label="建设用地费"><n-input-number v-model:value="ofInp.yongdi" :min="0" :precision="4" style="width:100%" /><div class="small faint">万元；按实际金额计列，没有填 0</div></n-form-item-gi>
            <n-form-item-gi label="水土保持方案编制和监测费"><n-input-number v-model:value="ofInp.shuibaoFangan" :min="0" :precision="4" style="width:100%" /><div class="small faint">万元；需另行核定的填实际数，没有填 0</div></n-form-item-gi>
          </n-grid>
        </n-form>
      </div>

      <div class="card noprint">
        <div class="card-h"><span class="card-ic b"><svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="4" y1="21" x2="4" y2="14"/><line x1="4" y1="10" x2="4" y2="3"/><line x1="12" y1="21" x2="12" y2="12"/><line x1="12" y1="8" x2="12" y2="3"/><line x1="20" y1="21" x2="20" y2="16"/><line x1="20" y1="12" x2="20" y2="3"/><line x1="1" y1="14" x2="7" y2="14"/><line x1="9" y1="8" x2="15" y2="8"/><line x1="17" y1="16" x2="23" y2="16"/></svg></span><h3>参数调整</h3></div>
        <n-form label-placement="top" :show-feedback="false">
          <n-grid :cols="2" :x-gap="14" :y-gap="4" responsive="screen">
            <n-form-item-gi label="服务费统一下浮率（监理/造价咨询/勘察/设计/环评）"><n-input-number v-model:value="ofInp.xiafu" :min="0" :max="100" style="width:100%" /><div class="small faint">%，默认 20（即 ×0.8）</div></n-form-item-gi>
            <n-form-item-gi label="招标代理下浮率"><n-input-number v-model:value="ofInp.xiafuDl" :min="0" :max="100" style="width:100%" /><div class="small faint">%，默认 40（即 ×0.6，发改价格〔2011〕534号）</div></n-form-item-gi>
            <n-form-item-gi label="监理专业/复杂/高程综合系数"><n-input-number v-model:value="ofInp.jianliCoef" :min="0" :step="0.05" style="width:100%" /></n-form-item-gi>
            <n-form-item-gi label="设计专业/复杂/附加综合系数"><n-input-number v-model:value="ofInp.shejiCoef" :min="0" :step="0.05" style="width:100%" /></n-form-item-gi>
            <n-form-item-gi label="基本预备费率"><n-input-number v-model:value="ofInp.yubeiRate" :min="0" :step="0.5" style="width:100%" /><div class="small faint">%，按（建安费+其他费）计</div></n-form-item-gi>
            <n-form-item-gi label="环评费区间取值"><n-select v-model:value="ofInp.huanpingPick" :options="HP_OPTS" /><div class="small faint">按计价格〔2002〕125号分档区间</div></n-form-item-gi>
          </n-grid>
        </n-form>
        <n-collapse style="margin-top:6px">
          <n-collapse-item title="更多参数（费率细项，一般不用改）" name="more">
            <n-form label-placement="top" :show-feedback="false">
              <n-grid :cols="2" :x-gap="14" :y-gap="4" responsive="screen">
                <n-form-item-gi label="前期咨询·编制可研费率（万分之）"><n-input-number v-model:value="ofInp.kyBianzhi" :min="0" style="width:100%" /></n-form-item-gi>
                <n-form-item-gi label="前期咨询·评估可研费率（万分之）"><n-input-number v-model:value="ofInp.kyPinggu" :min="0" style="width:100%" /></n-form-item-gi>
                <n-form-item-gi label="清单编制附加系数"><n-input-number v-model:value="ofInp.qingdanCoef" :min="0" :step="0.05" style="width:100%" /><div class="small faint">清单与控制价一体时 ×1.25</div></n-form-item-gi>
                <n-form-item-gi label="结算审核效益费·审减率（%）"><n-input-number v-model:value="ofInp.shenjianRate" :min="0" :step="0.5" style="width:100%" /></n-form-item-gi>
                <n-form-item-gi label="结算审核效益费·费率（%）"><n-input-number v-model:value="ofInp.xiaoyiRate" :min="0" :step="0.5" style="width:100%" /></n-form-item-gi>
                <n-form-item-gi label="勘察费率（%）"><n-input-number v-model:value="ofInp.kanchaRate" :min="0" :step="0.1" style="width:100%" /></n-form-item-gi>
                <n-form-item-gi label="施工图审查单价（元/㎡）"><n-input-number v-model:value="ofInp.tushenPrice" :min="0" :step="0.1" style="width:100%" /></n-form-item-gi>
                <n-form-item-gi label="水保补偿单价（元/㎡）"><n-input-number v-model:value="ofInp.buchangPrice" :min="0" :step="0.1" style="width:100%" /></n-form-item-gi>
                <n-form-item-gi label="场地准备费率（‰）"><n-input-number v-model:value="ofInp.changdiRate" :min="0" :step="0.5" style="width:100%" /></n-form-item-gi>
                <n-form-item-gi label="工程保险费率（‰）"><n-input-number v-model:value="ofInp.baoxianRate" :min="0" :step="0.5" style="width:100%" /></n-form-item-gi>
              </n-grid>
            </n-form>
          </n-collapse-item>
        </n-collapse>
      </div>

      <div class="card" v-if="ofRes">
        <div class="card-h"><span class="card-ic o"><svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="4" y="2" width="16" height="20" rx="2"/><line x1="8" y1="6" x2="16" y2="6"/><line x1="8" y1="11" x2="8.01" y2="11"/><line x1="12" y1="11" x2="12.01" y2="11"/><line x1="16" y1="11" x2="16.01" y2="11"/><line x1="8" y1="15" x2="8.01" y2="15"/><line x1="12" y1="15" x2="12.01" y2="15"/><line x1="16" y1="15" x2="16.01" y2="15"/><line x1="8" y1="19" x2="8.01" y2="19"/><line x1="12" y1="19" x2="12.01" y2="19"/><line x1="16" y1="19" x2="16.01" y2="19"/></svg></span><h3>计算结果</h3></div>
        <div v-if="ofInp.pname" style="font-size:15px;font-weight:650;margin-bottom:8px">工程名称：{{ ofInp.pname }}</div>
        <n-data-table size="small" :bordered="false" :columns="ofColumns" :data="ofRows" :row-props="ofRowProps" />
        <div style="margin-top:10px;color:var(--pri-d);font-weight:600;font-size:14px">
          概算总投资（大写）：{{ ofRes.totalCn }}（¥{{ (ofRes.total * 10000).toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) }}）
        </div>
      </div>
      <div class="card" v-else>
        <div class="faint small">填好建安工程费后，这里会实时显示计算结果。</div>
      </div>
    </div>

    <!-- ============ 统计表 ============ -->
    <div class="card" v-if="stats.length">
      <div class="card-h"><span class="card-ic yl"><svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M4 20V10M10 20V4M16 20v-8M21 20H3"/></svg></span><h3>多项收费统计表</h3><div class="sp"></div>
        <n-button size="small" @click="stats = []">清空</n-button>
        <n-button size="small" type="primary" @click="doPrint">打印</n-button>
      </div>
      <n-data-table table-layout="fixed" size="small" :bordered="false" :columns="statsColumns" :data="stats" :pagination="false" />
      <div style="display:flex;gap:26px;justify-content:flex-end;margin-top:10px;font-size:13.5px;flex-wrap:wrap">
        <span>标准收费合计：<b>¥ {{ rngY(statsTotal.std) }} 元</b></span>
        <span>折扣后合计：<b style="color:var(--pri-d)">¥ {{ rngY(statsTotal.disc) }} 元</b></span>
      </div>
    </div>
  </div>
</template>

<style scoped>
:deep(.fee-sub td) { background: #f4f8f5; font-weight: 600; }
:deep(.fee-total td) { background: #e7f0ea; font-weight: 700; color: var(--pri-d); }
@media print {
  .noprint { display: none !important; }
}
</style>
