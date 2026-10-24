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
      <div class="kpi blue"><div class="lb">在册项目</div><div class="vl">{{ data.kpi.projectTotal }} <small>个</small></div><small class="sub">进行中 {{ data.kpi.inProgress }} 个</small></div>
      <div class="kpi purple"><div class="lb">已出报告</div><div class="vl">{{ data.kpi.reported }} <small>个</small></div><small class="sub">成果交付</small></div>
      <div class="kpi green"><div class="lb">咨询费应收</div><div class="vl">{{ fmt(data.kpi.feeTotal) }} <small>万元</small></div><small class="sub">累计</small></div>
      <div class="kpi orange"><div class="lb">已收 / 待收</div><div class="vl">{{ fmt(data.kpi.receivedTotal) }} <small>万元</small></div><small class="sub">待收 {{ fmt(data.kpi.unreceived) }} 万元</small></div>
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
