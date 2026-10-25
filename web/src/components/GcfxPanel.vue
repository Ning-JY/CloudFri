<script setup>
import { ref, computed } from 'vue'
import { fmt, fmtMoney } from '../utils'

// 造价解析卡片（原独立解析页的全部内容，改为在项目详情页卡片内展示）：
// 头部金额带 + 六页签（概览/报价汇总/分部分项清单/工料机汇总/费用汇总/工程信息）。
const props = defineProps({ parse: { type: Object, default: null }, project: { type: Object, default: null }, fileName: { type: String, default: '' } })
const tab = ref('ov')
const units = computed(() => {
  try { return props.parse?.unitsJson ? JSON.parse(props.parse.unitsJson) : [] } catch { return [] }
})
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
        <div style="font-size:15px;font-weight:700">{{ parse.projectName || project?.name }}</div>
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
      <div style="display:flex;align-items:center;margin:16px 0 4px"><b style="font-size:13.5px">勾稽核对</b></div>
      <p class="small mut" style="line-height:1.9;margin:0">
        勾稽核对（明细合计 = 分部分项 + 单价措施清单行合价之和，与费用汇总逐单位工程比对）需要逐行结构化解析，属于二期解析范围。
        当前一期已完成：工程名称、总造价（与文件大写金额核对一致）、计价依据、所在地区、单位工程构成的结构化提取与展示。
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

    <!-- 二期页签 -->
    <div v-if="tab === 'boq'">
      <p class="small mut" style="line-height:1.9;margin:10px 0 0">清单逐行数据（编码 / 项目特征 / 工程量 / 综合单价 / 合价）将在二期由解析器逐行提取后按此页呈现；一期先完成工程信息与总造价的结构化提取。</p>
    </div>
    <div v-if="tab === 'mat'">
      <p class="small mut" style="line-height:1.9;margin:10px 0 0">人工 / 材料 / 机械的消耗量与价格汇总将在二期逐行解析完成后按此页呈现。</p>
    </div>
    <div v-if="tab === 'fee'">
      <p class="small mut" style="line-height:1.9;margin:10px 0 0">各单位工程的费用构成（分部分项 / 措施 / 其他项目 / 规费 / 税金）将在二期逐行解析完成后按此页呈现；当前总造价见「概览」与「报价汇总」。</p>
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
