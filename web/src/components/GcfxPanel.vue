<script setup>
import { ref, computed } from 'vue'
import { fmt, fmtMoney } from '../utils'

// 造价解析卡片（原独立解析页的全部内容，改为在项目详情页卡片内展示）：
// 头部金额带 + 六页签（概览/报价汇总/分部分项清单/工料机汇总/费用汇总/工程信息）。
const props = defineProps({ parse: { type: Object, default: null }, project: { type: Object, default: null }, fileName: { type: String, default: '' } })
// 标题：优先解析出的工程名；提不到时用源文件名（去扩展名），避免看起来像没刷新
const fileTitle = computed(() => props.fileName ? props.fileName.replace(/\.[^.]+$/, '') : '')
const tab = ref('ov')
const units = computed(() => {
  try { return props.parse?.unitsJson ? JSON.parse(props.parse.unitsJson) : [] } catch { return [] }
})
// 清单明细（逐行提取：编码/名称/单位/工程量），按单位工程分组
const items = computed(() => {
  try { return props.parse?.itemsJson ? JSON.parse(props.parse.itemsJson) : [] } catch { return [] }
})
const itemGroups = computed(() => {
  const map = new Map()
  for (const it of items.value) {
    if (!map.has(it.unit)) map.set(it.unit, [])
    map.get(it.unit).push(it)
  }
  return [...map.entries()].map(([unit, rows]) => ({ unit, rows }))
})
function fmtQty(q) {
  if (q === null || q === undefined) return '—'
  return Number(q).toLocaleString('zh-CN', { minimumFractionDigits: 0, maximumFractionDigits: 4 })
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

    <div class="d-amounts">
      <div class="a"><div class="lb">工程总造价</div><div class="vl num" style="font-size:20px">¥{{ fmtMoney(parse.totalAmount) }}</div><div class="sb">与文件大写金额核对一致</div></div>
      <div class="a"><div class="lb">单位工程</div><div class="vl num">{{ units.length }} <small style="font-size:13px">个</small></div><div class="sb">自工程树提取</div></div>
      <div class="a"><div class="lb">计价依据</div><div class="vl" style="font-size:14px;line-height:1.45">{{ parse.basis || '—' }}</div><div class="sb">计价方式</div></div>
      <div class="a"><div class="lb">所在地区</div><div class="vl" style="font-size:16px">{{ parse.region || '—' }}</div><div class="sb">建设地点</div></div>
    </div>
    <div class="small faint" style="margin-top:12px">
      大写：{{ parse.amountCn || '—' }} · 计价依据：{{ parse.basis || '—' }} · 地区：{{ parse.region || '—' }} · 总造价按文件内大写金额归集核对
    </div>

    <div style="margin:14px 0 4px">
      <div class="gtabs">
        <button v-for="t in TABS" :key="t.key" :class="{ on: tab === t.key }" @click="tab = t.key">{{ t.label }}</button>
      </div>
    </div>

    <!-- 概览 -->
    <div v-if="tab === 'ov'">
      <div style="display:flex;align-items:center;margin:10px 0 4px"><b style="font-size:13.5px">单位工程构成</b><span class="small faint" style="margin-left:auto">自计价文件工程树提取，共 {{ units.length }} 个</span></div>
      <n-data-table size="small" :bordered="false" :columns="[
        { title: '序号', key: 'i', width: 64, render: (r) => r.i },
        { title: '单位工程', key: 'name' },
        { title: '数据来源', key: 'src', width: 180, render: () => '工程树字符串提取' }
      ]" :data="units.map((u, i) => ({ i: i + 1, name: u }))" />
      <div v-if="!units.length" class="faint small" style="padding:10px 0">未提取到单位工程构成</div>
      <div style="display:flex;align-items:center;margin:16px 0 4px"><b style="font-size:13.5px">提取进度</b></div>
      <p class="small mut" style="line-height:1.9;margin:0">
        已完成：工程名称、总造价（与文件大写金额核对一致）、计价依据、所在地区、单位工程构成，以及清单明细逐行提取
        （编码 / 名称 / 单位 / 工程量，共 {{ items.length }} 条，见「分部分项清单」页签）。
        尚未完成：综合单价与合价在文件内为二进制字段，勾稽核对（明细合价之和与费用汇总比对）需待其可靠提取后进行，当前不显示单价与合价以免误导。
      </p>
    </div>

    <!-- 报价汇总 -->
    <div v-if="tab === 'sum'">
      <div style="display:flex;align-items:center;margin:10px 0 4px"><b style="font-size:13.5px">报价汇总</b><span class="small faint" style="margin-left:auto">单位工程金额逐项拆分属二期解析</span></div>
      <n-data-table size="small" :bordered="false" :columns="[
        { title: '序号', key: 'i', width: 64, render: (r) => r.i },
        { title: '单位工程', key: 'name' },
        { title: '金额（元）', key: 'amt', align: 'right', width: 220, render: (r) => r.amt }
      ]" :data="[...units.map((u, i) => ({ i: i + 1, name: u, amt: '—' })), { i: '', name: '工程总造价（归集，与大写金额核对一致）', amt: '¥' + fmtMoney(parse.totalAmount) }]" />
    </div>

    <!-- 分部分项清单：逐行提取（编码/名称/单位/工程量） -->
    <div v-if="tab === 'boq'">
      <div style="display:flex;align-items:center;margin:10px 0 4px"><b style="font-size:13.5px">分部分项清单</b><span class="small faint" style="margin-left:auto">共 {{ items.length }} 条 · 综合单价/合价为二进制字段，尚未可靠提取，暂不显示</span></div>
      <div v-if="!items.length" class="faint small" style="padding:10px 0">该文件未提取到清单明细（点右上角「重新解析」可按最新解析器重试）。</div>
      <div v-for="g in itemGroups" :key="g.unit" style="margin-top:12px">
        <div style="font-size:13px;font-weight:650;margin-bottom:4px">{{ g.unit }}<span class="small faint" style="font-weight:400"> · {{ g.rows.length }} 条</span></div>
        <n-data-table size="small" :bordered="false" :max-height="420" :columns="[
          { title: '清单编码', key: 'code', width: 130, render: (r) => r.code },
          { title: '名称', key: 'name' },
          { title: '单位', key: 'unitName', width: 80 },
          { title: '工程量', key: 'qty', width: 120, align: 'right', render: (r) => fmtQty(r.qty) }
        ]" :data="g.rows" />
      </div>
    </div>
    <div v-if="tab === 'mat'">
      <p class="small mut" style="line-height:1.9;margin:10px 0 0">人工 / 材料 / 机械的消耗量与价格汇总：相关数据在文件内为定额子目二进制记录，尚未可靠提取，提取完成后按此页呈现。</p>
    </div>
    <div v-if="tab === 'fee'">
      <p class="small mut" style="line-height:1.9;margin:10px 0 0">各单位工程的费用构成（分部分项 / 措施 / 其他项目 / 规费 / 税金）依赖逐行合价提取，尚未完成；当前总造价见「概览」与「报价汇总」，清单行见「分部分项清单」。</p>
    </div>

    <!-- 工程信息 -->
    <div v-if="tab === 'info'" style="margin-top:6px">
      <div class="kv-row"><span class="k">工程名称</span><span>{{ parse.projectName || '—' }}</span></div>
      <div class="kv-row"><span class="k">总造价</span><span class="num">¥{{ fmtMoney(parse.totalAmount) }}</span></div>
      <div class="kv-row"><span class="k">大写金额</span><span>{{ parse.amountCn || '—' }}</span></div>
      <div class="kv-row"><span class="k">计价依据</span><span>{{ parse.basis || '—' }}</span></div>
      <div class="kv-row"><span class="k">所在地区</span><span>{{ parse.region || '—' }}</span></div>
      <div class="kv-row"><span class="k">单位工程</span><span>{{ units.length }} 个：{{ units.join('、') }}</span></div>
      <div class="kv-row"><span class="k">源文件</span><span>{{ fileName || '—' }}</span></div>
      <div class="kv-row"><span class="k">台账送审金额</span><span class="num">{{ project?.submitAmount != null ? fmt(project.submitAmount) + ' 万元' : '—' }}</span></div>
      <div class="kv-row"><span class="k">台账审定金额</span><span class="num">{{ project?.approvedAmount != null ? fmt(project.approvedAmount) + ' 万元' : '—' }}</span></div>
      <div class="kv-row"><span class="k">解析时间</span><span class="num">{{ String(parse.parsedAt).slice(0, 16).replace('T', ' ') }}</span></div>
      <div class="kv-row"><span class="k">解析说明</span><span class="mut">{{ parse.note }}</span></div>
      <div class="kv-row"><span class="k">回填规则</span><span class="mut">GCFX 文件本身只有一个总造价，没有送审/审定两个金额；上传时按业务类型自动回填台账对应栏位（审核类 → 审定金额，其余 → 送审/预算/控制价栏），仅回填空栏，不覆盖手填值。</span></div>
    </div>
  </div>
</template>
