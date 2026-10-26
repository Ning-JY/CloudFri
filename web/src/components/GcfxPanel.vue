<script setup>
import { ref, computed } from 'vue'
import { fmt, fmtMoney } from '../utils'

// 造价解析卡片：头部金额带 + 六页签（概览/报价汇总/分部分项清单/工料机汇总/费用汇总/工程信息）。
// 金额口径（第九轮起）：清单逐行含综合单价/合价（3003 锚定解析）；单位工程费用来自 3010 费用表
// （分部分项/措施/安全生产/税前/增值税/总价，自洽校验通过才采信）；旧解析记录无费用数据时显示「—」并提示重新解析。
const props = defineProps({ parse: { type: Object, default: null }, project: { type: Object, default: null }, fileName: { type: String, default: '' } })
const fileTitle = computed(() => props.fileName ? props.fileName.replace(/\.[^.]+$/, '') : '')
const tab = ref('ov')

function parseJson(s) { try { return s ? JSON.parse(s) : [] } catch { return [] } }
const pick = (o, a, b) => o?.[a] ?? o?.[b] ?? null

// 单位工程：兼容新版对象数组（含费用）与旧版字符串数组
const units = computed(() => parseJson(props.parse?.unitsJson).map(u => typeof u === 'string'
  ? { name: u, boq: null, measures: null, safety: null, taxBase: null, vat: null, total: null }
  : {
      name: pick(u, 'Name', 'name'), boq: pick(u, 'Boq', 'boq'), measures: pick(u, 'Measures', 'measures'),
      safety: pick(u, 'Safety', 'safety'), taxBase: pick(u, 'TaxBase', 'taxBase'), vat: pick(u, 'Vat', 'vat'), total: pick(u, 'Total', 'total')
    }))
const hasFee = computed(() => units.value.some(u => u.total != null))
const feeSum = computed(() => {
  const s = { boq: 0, measures: 0, safety: 0, taxBase: 0, vat: 0, total: 0 }
  for (const u of units.value) {
    if (u.total == null) continue
    s.boq += u.boq || 0; s.measures += u.measures || 0; s.safety += u.safety || 0
    s.taxBase += u.taxBase || 0; s.vat += u.vat || 0; s.total += u.total || 0
  }
  return s
})

// 清单明细：兼容键名大小写，统一为 {unit, code, name, unitName, qty, price, amount}
const items = computed(() => parseJson(props.parse?.itemsJson).map(it => ({
  unit: pick(it, 'Unit', 'unit'), code: pick(it, 'Code', 'code'), name: pick(it, 'Name', 'name'),
  unitName: pick(it, 'UnitName', 'unitName'), qty: pick(it, 'Qty', 'qty'),
  price: pick(it, 'Price', 'price'), amount: pick(it, 'Amount', 'amount')
})))
const itemGroups = computed(() => {
  const map = new Map()
  for (const it of items.value) {
    if (!map.has(it.unit)) map.set(it.unit, [])
    map.get(it.unit).push(it)
  }
  return [...map.entries()].map(([unit, rows]) => ({
    unit, rows,
    amountSum: rows.reduce((a, r) => a + (r.amount || 0), 0),
    hasAmount: rows.some(r => r.amount != null)
  }))
})
const itemsAmountSum = computed(() => items.value.reduce((a, r) => a + (r.amount || 0), 0))
const itemsHaveAmount = computed(() => items.value.some(r => r.amount != null))
const itemCountByUnit = computed(() => {
  const m = {}
  for (const it of items.value) m[it.unit] = (m[it.unit] || 0) + 1
  return m
})
function fmtQty(q) {
  if (q === null || q === undefined) return '—'
  return Number(q).toLocaleString('zh-CN', { minimumFractionDigits: 0, maximumFractionDigits: 4 })
}
function fmtPrice(v) {
  if (v === null || v === undefined) return '—'
  return Number(v).toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 4 })
}
function money(v) { return v === null || v === undefined ? '—' : '¥' + fmtMoney(v) }
function pct(v, total) {
  if (v == null || !total) return '—'
  return (v / total * 100).toFixed(1) + '%'
}
function diffText(a, b) {
  if (a == null || b == null) return ''
  const d = Math.round((a - b) * 100) / 100
  return d === 0 ? '勾稽一致（差 0.00）' : `差 ${fmtMoney(d)} 元`
}
const TABS = [
  { key: 'ov', label: '概览' },
  { key: 'sum', label: '报价汇总' },
  { key: 'boq', label: '分部分项清单' },
  { key: 'mat', label: '工料机汇总' },
  { key: 'fee', label: '费用汇总' },
  { key: 'info', label: '工程信息' }
]
</script>

<template>
  <div v-if="parse">
    <div style="display:flex;gap:10px;align-items:center;flex-wrap:wrap">
      <div style="flex:1;min-width:240px">
        <div style="font-size:15px;font-weight:700">{{ parse.projectName || fileTitle || project?.name }}</div>
        <div class="small mut" style="margin-top:4px">{{ fileName }} · 上传后自动解析 · 宏业清单计价文件（GCFX）</div>
      </div>
      <n-tag type="success" :bordered="false">已自动解析</n-tag>
    </div>

    <div class="gx-amounts">
      <div class="a"><div class="lb">工程总造价</div><div class="vl num">¥{{ fmtMoney(parse.totalAmount) }}</div><div class="sb">约 {{ fmt((parse.totalAmount || 0) / 10000) }} 万元 · 与文件大写金额核对一致</div></div>
      <div class="a"><div class="lb">单位工程</div><div class="vl num">{{ units.length }} <small>个</small></div><div class="sb">{{ hasFee ? '总价合计 ¥' + fmtMoney(feeSum.total) : '自工程树提取' }}</div></div>
      <div class="a"><div class="lb">分部分项工程费</div><div class="vl num">{{ hasFee ? '¥' + fmtMoney(feeSum.boq) : '—' }}</div><div class="sb">{{ itemsHaveAmount ? '清单合价合计 ¥' + fmtMoney(itemsAmountSum) + ' · ' + diffText(itemsAmountSum, hasFee ? feeSum.boq : null) : '点「重新解析」获取金额' }}</div></div>
      <div class="a"><div class="lb">增值税</div><div class="vl num">{{ hasFee ? '¥' + fmtMoney(feeSum.vat) : '—' }}</div><div class="sb">{{ hasFee ? '税前合计 ¥' + fmtMoney(feeSum.taxBase) : '点「重新解析」获取金额' }}</div></div>
    </div>
    <div class="small faint" style="margin-top:12px">
      大写：{{ parse.amountCn || '—' }} · 计价依据：{{ parse.basis || '—' }} · 地区：{{ parse.region || '—' }}
    </div>

    <div style="margin:14px 0 4px">
      <div class="gtabs">
        <button v-for="t in TABS" :key="t.key" :class="{ on: tab === t.key }" @click="tab = t.key">{{ t.label }}</button>
      </div>
    </div>

    <!-- 概览 -->
    <div v-if="tab === 'ov'">
      <div style="display:flex;align-items:center;margin:10px 0 4px"><b style="font-size:13.5px">单位工程构成</b><span class="small faint" style="margin-left:auto">共 {{ units.length }} 个{{ hasFee ? ' · 金额自费用表提取' : '' }}</span></div>
      <n-data-table size="small" :bordered="false" :columns="[
        { title: '序号', key: 'i', width: 56, render: (r) => r.i },
        { title: '单位工程', key: 'name' },
        { title: '清单（条）', key: 'cnt', width: 92, align: 'right', render: (r) => r.cnt ?? '—' },
        { title: '总造价（元）', key: 'total', width: 150, align: 'right', render: (r) => r.total != null ? fmtMoney(r.total) : '—' },
        { title: '占比', key: 'share', width: 76, align: 'right', render: (r) => pct(r.total, feeSum.total) }
      ]" :data="units.map((u, i) => ({ i: i + 1, name: u.name, cnt: itemCountByUnit[u.name], total: u.total }))" />
      <div v-if="!units.length" class="faint small" style="padding:10px 0">未提取到单位工程构成</div>
      <template v-if="hasFee">
        <div style="display:flex;align-items:center;margin:16px 0 4px"><b style="font-size:13.5px">费用构成</b></div>
        <div class="kv-row"><span class="k">分部分项工程费</span><span class="num">{{ money(feeSum.boq) }}</span></div>
        <div class="kv-row"><span class="k">措施项目费</span><span class="num">{{ money(feeSum.measures) }}</span><span class="small faint">其中安全生产措施费 {{ money(feeSum.safety) }}</span></div>
        <div class="kv-row"><span class="k">税前合计</span><span class="num">{{ money(feeSum.taxBase) }}</span></div>
        <div class="kv-row"><span class="k">增值税</span><span class="num">{{ money(feeSum.vat) }}</span></div>
        <div class="kv-row"><span class="k">工程总造价</span><span class="num" style="font-weight:700">{{ money(feeSum.total) }}</span><span class="small faint">{{ diffText(feeSum.total, parse.totalAmount) }}（与大写金额比对）</span></div>
      </template>
      <div style="display:flex;align-items:center;margin:16px 0 4px"><b style="font-size:13.5px">提取进度</b></div>
      <p class="small mut" style="line-height:1.9;margin:0">
        已完成：工程名称、总造价（与文件大写金额核对一致）、计价依据、所在地区、单位工程构成、清单明细逐行提取
        （编码 / 名称 / 单位 / 工程量 / 综合单价 / 合价，共 {{ items.length }} 条{{ hasFee ? '，及各单位工程费用汇总（分部分项 / 措施 / 税前 / 增值税 / 总价）' : '' }}）。
        尚未完成：工料机汇总（人工 / 材料 / 机械消耗量）在文件内为定额子目二进制记录，尚未稳定提取，暂不显示。
      </p>
    </div>

    <!-- 报价汇总 -->
    <div v-if="tab === 'sum'">
      <div style="display:flex;align-items:center;margin:10px 0 4px"><b style="font-size:13.5px">报价汇总</b><span class="small faint" style="margin-left:auto">{{ hasFee ? '各单位工程金额自费用表提取' : '旧解析暂无金额，点右上角「重新解析」后显示' }}</span></div>
      <n-data-table size="small" :bordered="false" :columns="[
        { title: '序号', key: 'i', width: 56, render: (r) => r.i },
        { title: '单位工程', key: 'name' },
        { title: '分部分项（元）', key: 'boq', width: 138, align: 'right', render: (r) => r.boq != null ? fmtMoney(r.boq) : '—' },
        { title: '措施项目（元）', key: 'measures', width: 128, align: 'right', render: (r) => r.measures != null ? fmtMoney(r.measures) : '—' },
        { title: '增值税（元）', key: 'vat', width: 118, align: 'right', render: (r) => r.vat != null ? fmtMoney(r.vat) : '—' },
        { title: '总价（元）', key: 'total', width: 148, align: 'right', render: (r) => r.total != null ? fmtMoney(r.total) : '—' }
      ]" :data="[...units.map((u, i) => ({ i: i + 1, ...u })), ...(hasFee ? [{ i: '', name: '合计', boq: feeSum.boq, measures: feeSum.measures, vat: feeSum.vat, total: feeSum.total }] : [{ i: '', name: '工程总造价（归集，与大写金额核对一致）', boq: null, measures: null, vat: null, total: parse.totalAmount }])]" />
      <p v-if="hasFee" class="small mut" style="line-height:1.9;margin:10px 0 0">
        勾稽核对：单位工程总价合计 {{ money(feeSum.total) }} 与文件大写总造价 {{ money(parse.totalAmount) }} —— {{ diffText(feeSum.total, parse.totalAmount) }}；
        清单逐行合价合计 {{ money(itemsAmountSum) }} 与分部分项工程费 {{ money(feeSum.boq) }} —— {{ diffText(itemsAmountSum, feeSum.boq) }}。
      </p>
    </div>

    <!-- 分部分项清单：逐行提取（编码/名称/单位/工程量/综合单价/合价） -->
    <div v-if="tab === 'boq'">
      <div style="display:flex;align-items:center;margin:10px 0 4px"><b style="font-size:13.5px">分部分项清单</b><span class="small faint" style="margin-left:auto">共 {{ items.length }} 条{{ itemsHaveAmount ? ' · 含综合单价与合价' : ' · 点「重新解析」可补单价/合价' }}</span></div>
      <div v-if="!items.length" class="faint small" style="padding:10px 0">该文件未提取到清单明细（点右上角「重新解析」可按最新解析器重试）。</div>
      <div v-for="g in itemGroups" :key="g.unit" style="margin-top:12px">
        <div style="font-size:13px;font-weight:650;margin-bottom:4px">{{ g.unit }}<span class="small faint" style="font-weight:400"> · {{ g.rows.length }} 条{{ g.hasAmount ? ' · 合价小计 ¥' + fmtMoney(g.amountSum) : '' }}</span></div>
        <n-data-table size="small" :bordered="false" :max-height="420" :columns="[
          { title: '清单编码', key: 'code', width: 128, render: (r) => r.code },
          { title: '名称', key: 'name' },
          { title: '单位', key: 'unitName', width: 64 },
          { title: '工程量', key: 'qty', width: 108, align: 'right', render: (r) => fmtQty(r.qty) },
          { title: '综合单价', key: 'price', width: 108, align: 'right', render: (r) => fmtPrice(r.price) },
          { title: '合价（元）', key: 'amount', width: 128, align: 'right', render: (r) => r.amount != null ? fmtMoney(r.amount) : '—' }
        ]" :data="g.rows" />
      </div>
    </div>
    <div v-if="tab === 'mat'">
      <p class="small mut" style="line-height:1.9;margin:10px 0 0">人工 / 材料 / 机械的消耗量与价格汇总：相关数据在文件内为定额子目二进制记录，尚未稳定提取，提取完成后按此页呈现；当前可查看「分部分项清单」的逐行单价与合价。</p>
    </div>

    <!-- 费用汇总 -->
    <div v-if="tab === 'fee'">
      <div style="display:flex;align-items:center;margin:10px 0 4px"><b style="font-size:13.5px">费用汇总</b><span class="small faint" style="margin-left:auto">{{ hasFee ? '自各单位工程费用表归集' : '旧解析暂无金额，点右上角「重新解析」后显示' }}</span></div>
      <template v-if="hasFee">
        <div class="kv-row"><span class="k">分部分项工程费</span><span class="num">{{ money(feeSum.boq) }}</span><span class="small faint">清单逐行合价合计 {{ money(itemsAmountSum) }} · {{ diffText(itemsAmountSum, feeSum.boq) }}</span></div>
        <div class="kv-row"><span class="k">措施项目费</span><span class="num">{{ money(feeSum.measures) }}</span></div>
        <div class="kv-row"><span class="k">其中：安全费</span><span class="num">{{ money(feeSum.safety) }}</span><span class="small faint">安全生产措施费，已包含在措施项目费内，不重复计入</span></div>
        <div class="kv-row"><span class="k">税前合计</span><span class="num">{{ money(feeSum.taxBase) }}</span><span class="small faint">分部分项 + 措施项目</span></div>
        <div class="kv-row"><span class="k">增值税</span><span class="num">{{ money(feeSum.vat) }}</span><span class="small faint">一般计税，税率 9%</span></div>
        <div class="kv-row"><span class="k">工程总造价</span><span class="num" style="font-weight:700">{{ money(feeSum.total) }}</span><span class="small faint">与文件大写金额 {{ diffText(feeSum.total, parse.totalAmount) }}</span></div>
        <div style="display:flex;align-items:center;margin:16px 0 4px"><b style="font-size:13.5px">各单位工程费用明细</b></div>
        <n-data-table size="small" :bordered="false" :columns="[
          { title: '单位工程', key: 'name' },
          { title: '分部分项（元）', key: 'boq', width: 138, align: 'right', render: (r) => r.boq != null ? fmtMoney(r.boq) : '—' },
          { title: '措施（元）', key: 'measures', width: 118, align: 'right', render: (r) => r.measures != null ? fmtMoney(r.measures) : '—' },
          { title: '税前（元）', key: 'taxBase', width: 128, align: 'right', render: (r) => r.taxBase != null ? fmtMoney(r.taxBase) : '—' },
          { title: '增值税（元）', key: 'vat', width: 118, align: 'right', render: (r) => r.vat != null ? fmtMoney(r.vat) : '—' },
          { title: '总价（元）', key: 'total', width: 138, align: 'right', render: (r) => r.total != null ? fmtMoney(r.total) : '—' }
        ]" :data="units" />
      </template>
      <p v-else class="small mut" style="line-height:1.9;margin:10px 0 0">该解析记录生成于费用提取上线之前，暂无费用金额；点卡片右上角「重新解析」即可按最新解析器补齐（总造价见上方金额带）。</p>
    </div>

    <!-- 工程信息 -->
    <div v-if="tab === 'info'" style="margin-top:6px">
      <div class="kv-row"><span class="k">工程名称</span><span>{{ parse.projectName || '—' }}</span></div>
      <div class="kv-row"><span class="k">总造价</span><span class="num">¥{{ fmtMoney(parse.totalAmount) }}</span></div>
      <div class="kv-row"><span class="k">大写金额</span><span>{{ parse.amountCn || '—' }}</span></div>
      <div class="kv-row"><span class="k">计价依据</span><span>{{ parse.basis || '—' }}</span></div>
      <div class="kv-row"><span class="k">所在地区</span><span>{{ parse.region || '—' }}</span></div>
      <div class="kv-row"><span class="k">单位工程</span><span>{{ units.length }} 个：{{ units.map(u => u.name).join('、') }}</span></div>
      <div v-if="hasFee" class="kv-row"><span class="k">费用构成</span><span class="num">分部分项 {{ money(feeSum.boq) }} · 措施 {{ money(feeSum.measures) }} · 税前 {{ money(feeSum.taxBase) }} · 增值税 {{ money(feeSum.vat) }}</span></div>
      <div class="kv-row"><span class="k">源文件</span><span>{{ fileName || '—' }}</span></div>
      <div class="kv-row"><span class="k">台账送审金额</span><span class="num">{{ project?.submitAmount != null ? fmt(project.submitAmount) + ' 万元' : '—' }}</span></div>
      <div class="kv-row"><span class="k">台账审定金额</span><span class="num">{{ project?.approvedAmount != null ? fmt(project.approvedAmount) + ' 万元' : '—' }}</span></div>
      <div class="kv-row"><span class="k">解析时间</span><span class="num">{{ String(parse.parsedAt).slice(0, 16).replace('T', ' ') }}</span></div>
      <div class="kv-row"><span class="k">解析说明</span><span class="mut">{{ parse.note }}</span></div>
      <div class="kv-row"><span class="k">回填规则</span><span class="mut">GCFX 文件本身只有一个总造价，没有送审/审定两个金额；上传时按业务类型自动回填台账对应栏位（审核类 → 审定金额，其余 → 送审/预算/控制价栏），仅回填空栏，不覆盖手填值。</span></div>
    </div>
  </div>
</template>

<style scoped>
/* 解析卡专属金额带：自适应列数，避免在详情页右栏窄宽度下挤压（工程总造价/单位工程等显示问题） */
.gx-amounts { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: 12px; margin-top: 14px; }
.gx-amounts .a { border: 1px solid var(--line-soft); border-radius: 14px; padding: 12px 14px; min-width: 0; }
.gx-amounts .lb { font-size: 12px; color: var(--sub); margin-bottom: 5px; }
.gx-amounts .vl { font-size: 19px; font-weight: 750; white-space: nowrap; }
.gx-amounts .vl small { font-size: 13px; font-weight: 600; }
.gx-amounts .sb { font-size: 12px; color: var(--faint); margin-top: 3px; line-height: 1.5; }
.kv-row .k { width: 108px; flex-basis: 108px; }
</style>
