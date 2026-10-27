<script setup>
import { reactive, computed } from 'vue'
import { calcFees, FEE_DEFAULTS } from '../utils/feecalc'

// 工程建设其他费测算：由独立版 feiyong 页面并入系统，样式与全站统一；
// 计算口径走 utils/feecalc.js（全站唯一实现，已按观音镇数据校验）。实时计算，改参数即刷新。
const inp = reactive({ ...FEE_DEFAULTS })
const res = computed(() => calcFees(inp))
const fmt4 = (x) => Number(x).toFixed(4)
const HP_OPTS = [
  { label: '区间中值', value: 'mid' },
  { label: '区间下限', value: 'low' },
  { label: '区间上限', value: 'high' }
]
const tableRows = computed(() => {
  if (!res.value) return []
  const r = res.value
  const rows = [
    { no: '1', name: '工程直接费用（建安工程费）', proc: '输入值', amt: r.jianan, cls: 'sub' },
    ...r.rows.map(x => ({ no: x.no, name: x.name, proc: x.proc, amt: x.amt, basis: x.basis, cls: x.sub ? 'sub' : '' })),
    { no: '2', name: '工程其他费用合计', proc: '2.1 + … + 2.13', amt: r.other, cls: 'sub' },
    { no: '3.1', name: '基本预备费', proc: '（建安费 + 其他费）× ' + (inp.yubeiRate || 0) + '%', amt: r.yubei, cls: '' },
    { no: '4', name: '工程建设概算总投资', proc: '1 + 2 + 3', amt: r.total, cls: 'total' }
  ]
  return rows
})
const columns = [
  { title: '序号', key: 'no', width: 62 },
  { title: '费用名称', key: 'name', render: (r) => r.name },
  { title: '计算过程', key: 'proc', render: (r) => r.proc },
  { title: '金额（万元）', key: 'amt', width: 138, align: 'right', render: (r) => fmt4(r.amt) }
]
function rowProps(r) { return r.cls ? { class: 'fee-' + r.cls } : {} }
function doPrint() { window.print() }
</script>

<template>
  <div class="fee-print">
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
          <n-form-item-gi :span="2" label="工程名称（可选，打印时显示）"><n-input v-model:value="inp.pname" placeholder="如：XX项目" /></n-form-item-gi>
          <n-form-item-gi label="建安工程费（工程直接费用）*"><n-input-number v-model:value="inp.jianan" :min="0" :precision="4" placeholder="如 559.108" style="width:100%" /><div class="small faint">万元；各项取费的主要基数</div></n-form-item-gi>
          <n-form-item-gi label="建筑面积"><n-input-number v-model:value="inp.jianzhu" :min="0" :precision="2" placeholder="如 1743.10" style="width:100%" /><div class="small faint">㎡；用于施工图审查费</div></n-form-item-gi>
          <n-form-item-gi label="征占地面积"><n-input-number v-model:value="inp.zhandi" :min="0" :precision="2" placeholder="如 5033.09" style="width:100%" /><div class="small faint">㎡；用于水土保持补偿费</div></n-form-item-gi>
          <n-form-item-gi label="建设用地费"><n-input-number v-model:value="inp.yongdi" :min="0" :precision="4" style="width:100%" /><div class="small faint">万元；按实际金额计列，没有填 0</div></n-form-item-gi>
          <n-form-item-gi label="水土保持方案编制和监测费"><n-input-number v-model:value="inp.shuibaoFangan" :min="0" :precision="4" style="width:100%" /><div class="small faint">万元；需另行核定的填实际数，没有填 0</div></n-form-item-gi>
        </n-grid>
      </n-form>
    </div>

    <div class="card noprint">
      <div class="card-h"><span class="card-ic b"><svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="4" y1="21" x2="4" y2="14"/><line x1="4" y1="10" x2="4" y2="3"/><line x1="12" y1="21" x2="12" y2="12"/><line x1="12" y1="8" x2="12" y2="3"/><line x1="20" y1="21" x2="20" y2="16"/><line x1="20" y1="12" x2="20" y2="3"/><line x1="1" y1="14" x2="7" y2="14"/><line x1="9" y1="8" x2="15" y2="8"/><line x1="17" y1="16" x2="23" y2="16"/></svg></span><h3>参数调整</h3></div>
      <n-form label-placement="top" :show-feedback="false">
        <n-grid :cols="2" :x-gap="14" :y-gap="4" responsive="screen">
          <n-form-item-gi label="服务费统一下浮率（监理/造价咨询/勘察/设计/环评）"><n-input-number v-model:value="inp.xiafu" :min="0" :max="100" style="width:100%" /><div class="small faint">%，默认 20（即 ×0.8）</div></n-form-item-gi>
          <n-form-item-gi label="招标代理下浮率"><n-input-number v-model:value="inp.xiafuDl" :min="0" :max="100" style="width:100%" /><div class="small faint">%，默认 40（即 ×0.6，发改价格〔2011〕534号）</div></n-form-item-gi>
          <n-form-item-gi label="监理专业/复杂/高程综合系数"><n-input-number v-model:value="inp.jianliCoef" :min="0" :step="0.05" style="width:100%" /></n-form-item-gi>
          <n-form-item-gi label="设计专业/复杂/附加综合系数"><n-input-number v-model:value="inp.shejiCoef" :min="0" :step="0.05" style="width:100%" /></n-form-item-gi>
          <n-form-item-gi label="基本预备费率"><n-input-number v-model:value="inp.yubeiRate" :min="0" :step="0.5" style="width:100%" /><div class="small faint">%，按（建安费+其他费）计</div></n-form-item-gi>
          <n-form-item-gi label="环评费区间取值"><n-select v-model:value="inp.huanpingPick" :options="HP_OPTS" /><div class="small faint">按计价格〔2002〕125号分档区间</div></n-form-item-gi>
        </n-grid>
      </n-form>
      <n-collapse style="margin-top:6px">
        <n-collapse-item title="更多参数（费率细项，一般不用改）" name="more">
          <n-form label-placement="top" :show-feedback="false">
            <n-grid :cols="2" :x-gap="14" :y-gap="4" responsive="screen">
              <n-form-item-gi label="前期咨询·编制可研费率（万分之）"><n-input-number v-model:value="inp.kyBianzhi" :min="0" style="width:100%" /></n-form-item-gi>
              <n-form-item-gi label="前期咨询·评估可研费率（万分之）"><n-input-number v-model:value="inp.kyPinggu" :min="0" style="width:100%" /></n-form-item-gi>
              <n-form-item-gi label="清单编制附加系数"><n-input-number v-model:value="inp.qingdanCoef" :min="0" :step="0.05" style="width:100%" /><div class="small faint">清单与控制价一体时 ×1.25</div></n-form-item-gi>
              <n-form-item-gi label="结算审核效益费·审减率（%）"><n-input-number v-model:value="inp.shenjianRate" :min="0" :step="0.5" style="width:100%" /></n-form-item-gi>
              <n-form-item-gi label="结算审核效益费·费率（%）"><n-input-number v-model:value="inp.xiaoyiRate" :min="0" :step="0.5" style="width:100%" /></n-form-item-gi>
              <n-form-item-gi label="勘察费率（%）"><n-input-number v-model:value="inp.kanchaRate" :min="0" :step="0.1" style="width:100%" /></n-form-item-gi>
              <n-form-item-gi label="施工图审查单价（元/㎡）"><n-input-number v-model:value="inp.tushenPrice" :min="0" :step="0.1" style="width:100%" /></n-form-item-gi>
              <n-form-item-gi label="水保补偿单价（元/㎡）"><n-input-number v-model:value="inp.buchangPrice" :min="0" :step="0.1" style="width:100%" /></n-form-item-gi>
              <n-form-item-gi label="场地准备费率（‰）"><n-input-number v-model:value="inp.changdiRate" :min="0" :step="0.5" style="width:100%" /></n-form-item-gi>
              <n-form-item-gi label="工程保险费率（‰）"><n-input-number v-model:value="inp.baoxianRate" :min="0" :step="0.5" style="width:100%" /></n-form-item-gi>
            </n-grid>
          </n-form>
        </n-collapse-item>
      </n-collapse>
    </div>

    <div class="card" v-if="res">
      <div class="card-h"><span class="card-ic o"><svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="4" y="2" width="16" height="20" rx="2"/><line x1="8" y1="6" x2="16" y2="6"/><line x1="8" y1="11" x2="8.01" y2="11"/><line x1="12" y1="11" x2="12.01" y2="11"/><line x1="16" y1="11" x2="16.01" y2="11"/><line x1="8" y1="15" x2="8.01" y2="15"/><line x1="12" y1="15" x2="12.01" y2="15"/><line x1="16" y1="15" x2="16.01" y2="15"/><line x1="8" y1="19" x2="8.01" y2="19"/><line x1="12" y1="19" x2="12.01" y2="19"/><line x1="16" y1="19" x2="16.01" y2="19"/></svg></span><h3>计算结果</h3></div>
      <div v-if="inp.pname" style="font-size:15px;font-weight:650;margin-bottom:8px">工程名称：{{ inp.pname }}</div>
      <n-data-table size="small" :bordered="false" :columns="columns" :data="tableRows" :row-props="rowProps" />
      <div style="margin-top:10px;color:var(--pri-d);font-weight:600;font-size:14px">
        概算总投资（大写）：{{ res.totalCn }}（¥{{ (res.total * 10000).toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) }}）
      </div>
    </div>
    <div class="card" v-else>
      <div class="faint small">填好建安工程费后，这里会实时显示计算结果。</div>
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
