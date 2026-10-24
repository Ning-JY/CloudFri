<script setup>
import { ref, onMounted, nextTick } from 'vue'
import { useRouter } from 'vue-router'
import * as echarts from 'echarts'
import api from '../api'
import { fmt } from '../utils'

const router = useRouter()
const data = ref(null)
const chartEl = ref(null)

onMounted(async () => {
  const { data: d } = await api.get('/stats/dashboard')
  data.value = d
  await nextTick()
  if (chartEl.value) {
    const chart = echarts.init(chartEl.value)
    chart.setOption({
      tooltip: { trigger: 'axis' },
      legend: { data: ['新承接项目', '收款（万元）'], right: 0, icon: 'roundRect', itemWidth: 14 },
      grid: { left: 8, right: 8, top: 36, bottom: 0, containLabel: true },
      xAxis: { type: 'category', data: d.monthly.map(m => m.month.slice(5) + '月'), axisLine: { lineStyle: { color: '#dde5df' } }, axisLabel: { color: '#93a29a' } },
      yAxis: [{ type: 'value', splitLine: { lineStyle: { color: '#eef2ef' } }, axisLabel: { color: '#93a29a' } },
              { type: 'value', splitLine: { show: false }, axisLabel: { color: '#93a29a' } }],
      series: [
        { name: '新承接项目', type: 'bar', data: d.monthly.map(m => m.newProjects), itemStyle: { color: '#186140', borderRadius: [6, 6, 0, 0] }, barWidth: 22 },
        { name: '收款（万元）', type: 'line', yAxisIndex: 1, data: d.monthly.map(m => m.received), smooth: true, itemStyle: { color: '#7ea6f8' }, lineStyle: { width: 2.5 } }
      ]
    })
  }
})
</script>

<template>
  <div v-if="data">
    <div class="kpi-grid">
      <div class="kpi blue"><div class="lb">在册项目</div><div class="vl">{{ data.kpi.projectTotal }} <small>个</small></div><small class="sub">进行中 {{ data.kpi.inProgress }} 个</small></div>
      <div class="kpi purple"><div class="lb">已出报告</div><div class="vl">{{ data.kpi.reported }} <small>个</small></div><small class="sub">含已归档项目</small></div>
      <div class="kpi green"><div class="lb">咨询费应收</div><div class="vl">{{ fmt(data.kpi.feeTotal) }} <small>万元</small></div><small class="sub">累计合同应收</small></div>
      <div class="kpi orange"><div class="lb">已收咨询费</div><div class="vl">{{ fmt(data.kpi.receivedTotal) }} <small>万元</small></div><small class="sub">待收 {{ fmt(data.kpi.unreceived) }} 万元</small></div>
    </div>

    <div style="display:grid;grid-template-columns:1fr 360px;gap:14px;align-items:start" class="dash-grid">
      <div class="card">
        <div class="card-h"><h3>近 6 个月承接与收款</h3></div>
        <div ref="chartEl" style="height:280px"></div>
      </div>
      <div class="card">
        <div class="card-h"><h3>待处理项目</h3><div class="sp"></div><span class="small faint">编制中 / 审核中</span></div>
        <div v-if="!data.pending.length" class="faint small">暂无待处理项目</div>
        <div v-for="p in data.pending" :key="p.id" style="padding:8px 0;border-top:1px solid var(--line-soft);cursor:pointer" @click="router.push('/projects/' + p.id)">
          <div style="font-weight:600;font-size:13.5px">{{ p.name }}</div>
          <div class="psub">{{ p.code }} · {{ p.status }} · 承接 {{ p.acceptedDate || '—' }}</div>
        </div>
      </div>
    </div>

    <div class="card">
      <div class="card-h"><h3>最近登记</h3><div class="sp"></div><n-button size="small" @click="router.push('/projects')">查看台账</n-button></div>
      <n-data-table :columns="[
        { title: '编号', key: 'code', width: 120 },
        { title: '项目名称', key: 'name', ellipsis: { tooltip: true } },
        { title: '业务类型', key: 'bizType', width: 110 },
        { title: '状态', key: 'status', width: 100 },
        { title: '承接日期', key: 'acceptedDate', width: 110 }
      ]" :data="data.recent" :bordered="false" size="small"
        :row-props="(row) => ({ style: 'cursor:pointer', onClick: () => router.push('/projects/' + row.id) })" />
    </div>
  </div>
  <div v-else class="card"><n-spin size="large" /></div>
</template>

<style scoped>
@media (max-width: 1080px) { .dash-grid { grid-template-columns: 1fr !important; } }
</style>
