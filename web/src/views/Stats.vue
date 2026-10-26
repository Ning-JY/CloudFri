<script setup>
import { ref, onMounted, nextTick } from 'vue'
import * as echarts from 'echarts'
import api from '../api'
import { fmt } from '../utils'

const data = ref(null)
const elType = ref(null)
const elStatus = ref(null)
const elMember = ref(null)

onMounted(async () => {
  const { data: d } = await api.get('/stats/dashboard')
  data.value = d
  await nextTick()
  const base = { textStyle: { fontFamily: 'inherit' } }
  if (elType.value) {
    echarts.init(elType.value).setOption({
      ...base,
      tooltip: { trigger: 'axis' },
      grid: { left: 8, right: 12, top: 28, bottom: 0, containLabel: true },
      xAxis: { type: 'category', data: d.byType.map(x => x.type), axisLine: { lineStyle: { color: '#dde5df' } }, axisLabel: { color: '#5d6f66' } },
      yAxis: { type: 'value', splitLine: { lineStyle: { color: '#eef2ef' } }, axisLabel: { color: '#93a29a' } },
      series: [
        { name: '项目数', type: 'bar', data: d.byType.map(x => x.count), itemStyle: { color: '#186140', borderRadius: [6, 6, 0, 0] }, barWidth: 26, label: { show: true, position: 'top' } }
      ]
    })
  }
  if (elStatus.value) {
    echarts.init(elStatus.value).setOption({
      ...base,
      tooltip: {},
      legend: { bottom: 0, icon: 'circle', itemWidth: 9 },
      series: [{
        type: 'pie', radius: ['52%', '74%'], center: ['50%', '44%'],
        itemStyle: { borderColor: '#fff', borderWidth: 2, borderRadius: 5 },
        label: { formatter: '{b} {c}' },
        data: d.byStatus.map(x => ({ name: x.status, value: x.count })),
        color: ['#d9a441', '#7ea6f8', '#186140', '#b9c4bc']
      }]
    })
  }
  if (elMember.value) {
    const rows = [...d.byMember].reverse()
    echarts.init(elMember.value).setOption({
      ...base,
      tooltip: {},
      grid: { left: 8, right: 30, top: 12, bottom: 0, containLabel: true },
      xAxis: { type: 'value', splitLine: { lineStyle: { color: '#eef2ef' } }, axisLabel: { color: '#93a29a' } },
      yAxis: { type: 'category', data: rows.map(x => x.name), axisLine: { lineStyle: { color: '#dde5df' } }, axisLabel: { color: '#5d6f66' } },
      series: [{ type: 'bar', data: rows.map(x => x.projectCount), itemStyle: { color: '#7ea6f8', borderRadius: [0, 6, 6, 0] }, barWidth: 15, label: { show: true, position: 'right' } }]
    })
  }
})
</script>

<template>
  <div v-if="data">
    <div class="kpi-grid">
      <div class="kpi blue"><span class="ic"><svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"/></svg></span><div class="lb">在册项目</div><div class="vl">{{ data.kpi.projectTotal }} <small>个</small></div><small class="sub">进行中 {{ data.kpi.inProgress }} 个</small></div>
      <div class="kpi purple"><span class="ic"><svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><polyline points="9 15 11 17 15 13"/></svg></span><div class="lb">已出报告</div><div class="vl">{{ data.kpi.reported }} <small>个</small></div><small class="sub">成果交付</small></div>
      <div class="kpi green"><span class="ic"><svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M4 2v20l2-1 2 1 2-1 2 1 2-1 2 1 2-1 2 1V2l-2 1-2-1-2 1-2-1-2 1-2-1-2 1z"/><line x1="8" y1="8" x2="16" y2="8"/><line x1="8" y1="12" x2="16" y2="12"/></svg></span><div class="lb">咨询费应收</div><div class="vl">{{ fmt(data.kpi.feeTotal) }} <small>万元</small></div><small class="sub">累计</small></div>
      <div class="kpi orange"><span class="ic"><svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 12V7H5a2 2 0 0 1 0-4h14v4"/><path d="M3 5v14a2 2 0 0 0 2 2h16v-5"/><path d="M18 12a2 2 0 0 0 0 4h4v-4z"/></svg></span><div class="lb">已收 / 待收</div><div class="vl">{{ fmt(data.kpi.receivedTotal) }} <small>万元</small></div><small class="sub">待收 {{ fmt(data.kpi.unreceived) }} 万元</small></div>
    </div>
    <div style="display:grid;grid-template-columns:1fr 1fr;gap:14px" class="stats-grid">
      <div class="card"><div class="card-h"><h3>各业务类型项目数</h3></div><div ref="elType" style="height:300px"></div></div>
      <div class="card"><div class="card-h"><h3>项目状态分布</h3></div><div ref="elStatus" style="height:300px"></div></div>
    </div>
    <div class="card"><div class="card-h"><h3>负责人工作量（负责项目数）</h3></div><div ref="elMember" style="height:320px"></div></div>
    <div class="card">
      <div class="card-h"><h3>各业务类型咨询费（万元）</h3></div>
      <n-data-table :columns="[
        { title: '业务类型', key: 'type' },
        { title: '项目数', key: 'count', align: 'right' },
        { title: '咨询费应收（万元）', key: 'fee', align: 'right', render: (r) => fmt(r.fee) }
      ]" :data="data.byType" :bordered="false" size="small" />
    </div>
  </div>
  <div v-else class="card"><n-spin size="large" /></div>
</template>

<style scoped>
@media (max-width: 1080px) { .stats-grid { grid-template-columns: 1fr !important; } }
</style>
