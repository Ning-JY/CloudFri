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
      <div class="kpi blue"><span class="ic"><svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"/></svg></span><div class="lb">在册项目</div><div class="vl">{{ data.kpi.projectTotal }} <small>个</small></div><small class="sub">进行中 {{ data.kpi.inProgress }} 个</small></div>
      <div class="kpi purple"><span class="ic"><svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><polyline points="9 15 11 17 15 13"/></svg></span><div class="lb">已出报告</div><div class="vl">{{ data.kpi.reported }} <small>个</small></div><small class="sub">含已归档项目</small></div>
      <div class="kpi green"><span class="ic"><svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M4 2v20l2-1 2 1 2-1 2 1 2-1 2 1 2-1 2 1V2l-2 1-2-1-2 1-2-1-2 1-2-1-2 1z"/><line x1="8" y1="8" x2="16" y2="8"/><line x1="8" y1="12" x2="16" y2="12"/></svg></span><div class="lb">咨询费应收</div><div class="vl">{{ fmt(data.kpi.feeTotal) }} <small>万元</small></div><small class="sub">累计合同应收</small></div>
      <div class="kpi orange"><span class="ic"><svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 12V7H5a2 2 0 0 1 0-4h14v4"/><path d="M3 5v14a2 2 0 0 0 2 2h16v-5"/><path d="M18 12a2 2 0 0 0 0 4h4v-4z"/></svg></span><div class="lb">已收咨询费</div><div class="vl">{{ fmt(data.kpi.receivedTotal) }} <small>万元</small></div><small class="sub">已开票 {{ fmt(data.kpi.invoicedTotal) }} 万元 · 待收 {{ fmt(data.kpi.unreceived) }} 万元</small></div>
    </div>

    <div style="display:grid;grid-template-columns:1fr 360px;gap:14px;align-items:start" class="dash-grid">
      <div class="card">
        <div class="card-h"><h3>近 6 个月承接与收款</h3></div>
        <div ref="chartEl" style="height:280px"></div>
      </div>
      <div class="card">
        <div class="card-h"><h3>待处理项目</h3><div class="sp"></div><span class="small faint">编制中 / 审核中</span></div>
        <div v-if="!data.pending.length" class="faint small">暂无待处理项目</div>
        <div v-for="p in data.pending" :key="p.id" class="todo-it" @click="router.push('/projects/' + p.id)">
          <span class="todo-ic" :class="p.status === '审核中' ? 'c-violet' : 'c-blue'">
            <svg v-if="p.status === '审核中'" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg>
            <svg v-else viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M17 3a2.83 2.83 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5Z"/><path d="m15 5 4 4"/></svg>
          </span>
          <div style="min-width:0">
            <div style="font-weight:600;font-size:13.5px">{{ p.name }}</div>
            <div class="psub">{{ p.code }} · {{ p.status }}</div>
          </div>
          <span class="when">{{ p.acceptedDate || '' }}</span>
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
