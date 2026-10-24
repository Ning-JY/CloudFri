<script setup>
import { ref, onMounted, h } from 'vue'
import { useRouter } from 'vue-router'
import { NTag } from 'naive-ui'
import api from '../api'
import { fmt, statusType } from '../utils'

const router = useRouter()
const rows = ref([])
const totals = ref({ feeTotal: 0, receivedTotal: 0, projectCount: 0 })

onMounted(async () => {
  const { data } = await api.get('/fees')
  rows.value = data.rows
  totals.value = data.totals
})

const columns = [
  { title: '编号', key: 'code', width: 118, render: (r) => h('span', { class: 'num faint' }, r.code) },
  { title: '项目名称', key: 'name', ellipsis: { tooltip: true }, render: (r) => h('div', [h('div', { class: 'pname' }, r.name), h('div', { class: 'psub' }, r.client)]) },
  { title: '状态', key: 'status', width: 96, render: (r) => h(NTag, { size: 'small', type: statusType(r.status), bordered: false }, () => r.status) },
  { title: '应收（万元）', key: 'fee', width: 110, align: 'right', render: (r) => h('span', { class: 'num' }, fmt(r.fee)) },
  { title: '已收（万元）', key: 'received', width: 110, align: 'right', render: (r) => h('span', { class: 'num' }, fmt(r.received)) },
  { title: '待收（万元）', key: 'left', width: 110, align: 'right', render: (r) => h('span', { class: 'num' + (r.fee - r.received > 0 ? ' neg' : '') }, fmt(Math.max(r.fee - r.received, 0))) },
  { title: '收款进度', key: 'pct', width: 150, render: (r) => {
      const pct = r.fee > 0 ? Math.min(100, Math.round(r.received / r.fee * 100)) : 0
      return h('div', { style: 'display:flex;align-items:center;gap:8px' }, [
        h('div', { style: 'flex:1;height:7px;border-radius:99px;background:#e9efeb;overflow:hidden' }, [
          h('div', { style: `height:100%;width:${pct}%;background:var(--pri);border-radius:99px` })
        ]),
        h('span', { class: 'small num' }, pct + '%')
      ])
    } },
  { title: '最近收款', key: 'lastReceiptDate', width: 110, render: (r) => r.lastReceiptDate || '—' },
  { title: '笔数', key: 'receiptCount', width: 70, align: 'right' }
]
</script>

<template>
  <div>
    <div class="card sumstrip" style="padding:0">
      <div class="cell"><div class="lb">项目数</div><div class="vl num">{{ totals.projectCount }} <small>个</small></div></div>
      <div class="cell"><div class="lb">咨询费应收合计</div><div class="vl num">{{ fmt(totals.feeTotal) }} <small>万元</small></div></div>
      <div class="cell"><div class="lb">已收合计</div><div class="vl num">{{ fmt(totals.receivedTotal) }} <small>万元</small></div></div>
      <div class="cell"><div class="lb">待收合计</div><div class="vl num neg">{{ fmt(Math.max(totals.feeTotal - totals.receivedTotal, 0)) }} <small>万元</small></div></div>
    </div>
    <div class="card" style="padding:6px 10px">
      <n-data-table :columns="columns" :data="rows" :bordered="false"
        :row-props="(row) => ({ style: 'cursor:pointer', onClick: () => router.push('/projects/' + row.id) })" />
      <div v-if="!rows.length" class="faint" style="text-align:center;padding:26px">暂无项目收费数据</div>
      <p class="small faint" style="padding:6px 10px">点击任意行进入项目详情，在「合同与收费」中登记或删除收款。</p>
    </div>
  </div>
</template>
