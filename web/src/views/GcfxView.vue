<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import api from '../api'
import { fmtMoney } from '../utils'

const route = useRoute()
const router = useRouter()
const projectId = Number(route.params.id)
const fileId = Number(route.params.fileId)
const d = ref(null)
const tab = ref('ov')
const parse = computed(() => d.value?.gcfxParses?.find(g => g.projectFileId === fileId) || null)
const units = computed(() => {
  try { return parse.value?.unitsJson ? JSON.parse(parse.value.unitsJson) : [] } catch { return [] }
})
const file = computed(() => d.value?.files?.find(f => f.id === fileId))
const TABS = [
  { key: 'ov', label: '概览' },
  { key: 'sum', label: '报价汇总' },
  { key: 'boq', label: '分部分项清单' },
  { key: 'mat', label: '工料机汇总' },
  { key: 'fee', label: '费用汇总' },
  { key: 'info', label: '工程信息' }
]

onMounted(async () => {
  const { data } = await api.get('/projects/' + projectId)
  d.value = data
})
</script>

<template>
  <div v-if="d">
    <n-button quaternary size="small" style="margin-bottom:10px" @click="router.push('/projects/' + projectId)">‹ 返回项目详情</n-button>

    <div class="card d-head">
      <div style="display:flex;gap:10px;align-items:flex-start;flex-wrap:wrap">
        <div style="flex:1;min-width:260px">
          <div class="d-title" style="font-size:18px">{{ parse?.projectName || d.project.name }}</div>
          <div class="small mut" style="margin-top:6px;display:flex;gap:14px;flex-wrap:wrap">
            <span>{{ file?.fileName }}</span><span>上传后自动解析</span><span>宏业清单计价文件（GCFX）</span>
          </div>
        </div>
        <n-tag v-if="parse" type="success" :bordered="false">已自动解析</n-tag>
      </div>
      <div class="d-amounts" v-if="parse">
        <div class="a"><div class="lb">工程总造价</div><div class="vl num" style="font-size:20px">¥{{ fmtMoney(parse.totalAmount) }}</div><div class="sb">与文件大写金额核对一致</div></div>
        <div class="a"><div class="lb">单位工程</div><div class="vl num">{{ units.length }} <small style="font-size:13px">个</small></div><div class="sb">自工程树提取</div></div>
        <div class="a"><div class="lb">计价依据</div><div class="vl" style="font-size:14px;line-height:1.45">{{ parse.basis || '—' }}</div><div class="sb">计价方式</div></div>
        <div class="a"><div class="lb">所在地区</div><div class="vl" style="font-size:16px">{{ parse.region || '—' }}</div><div class="sb">建设地点</div></div>
      </div>
      <div v-if="parse" class="small faint" style="border-top:1px solid var(--line-soft);margin-top:14px;padding-top:12px">
        大写：{{ parse.amountCn || '—' }} · 计价依据：{{ parse.basis || '—' }} · 地区：{{ parse.region || '—' }} · 总造价按文件内大写金额归集核对
      </div>
    </div>

    <div style="margin:2px 0 14px" v-if="parse">
      <div class="gtabs">
        <button v-for="t in TABS" :key="t.key" :class="{ on: tab === t.key }" @click="tab = t.key">{{ t.label }}</button>
      </div>
    </div>

    <!-- 概览 -->
    <div v-if="parse && tab === 'ov'">
      <div class="card">
        <div class="card-h"><h3>单位工程构成</h3><div class="sp"></div><span class="small faint">自计价文件工程树提取，共 {{ units.length }} 个</span></div>
        <n-data-table size="small" :bordered="false" :columns="[
          { title: '序号', key: 'i', width: 64, render: (r) => r.i },
          { title: '单位工程', key: 'name' },
          { title: '数据来源', key: 'src', width: 180, render: () => '工程树字符串提取' }
        ]" :data="units.map((u, i) => ({ i: i + 1, name: u }))" />
        <div v-if="!units.length" class="faint small" style="padding:10px 0">未提取到单位工程构成</div>
      </div>
      <div class="card">
        <div class="card-h"><h3>勾稽核对</h3></div>
        <p class="small mut" style="line-height:1.9;margin:0">
          勾稽核对（明细合计 = 分部分项 + 单价措施清单行合价之和，与费用汇总逐单位工程比对）需要逐行结构化解析，属于二期解析范围。
          当前一期已完成：工程名称、总造价（与文件大写金额核对一致）、计价依据、所在地区、单位工程构成的结构化提取与展示。
        </p>
      </div>
    </div>

    <!-- 报价汇总 -->
    <div v-if="parse && tab === 'sum'">
      <div class="card">
        <div class="card-h"><h3>报价汇总</h3><div class="sp"></div><span class="small faint">单位工程金额逐项拆分属二期解析</span></div>
        <n-data-table size="small" :bordered="false" :columns="[
          { title: '序号', key: 'i', width: 64, render: (r) => r.i },
          { title: '单位工程', key: 'name' },
          { title: '金额（元）', key: 'amt', align: 'right', width: 220, render: (r) => r.amt }
        ]" :data="[...units.map((u, i) => ({ i: i + 1, name: u, amt: '—' })), { i: '', name: '工程总造价（归集，与大写金额核对一致）', amt: '¥' + fmtMoney(parse.totalAmount) }]" />
      </div>
    </div>

    <!-- 二期页签 -->
    <div v-if="parse && tab === 'boq'">
      <div class="card">
        <div class="card-h"><h3>分部分项清单</h3></div>
        <p class="small mut" style="line-height:1.9;margin:0">清单逐行数据（编码 / 项目特征 / 工程量 / 综合单价 / 合价）将在二期由解析器逐行提取后按此页呈现；一期先完成工程信息与总造价的结构化提取。</p>
      </div>
    </div>
    <div v-if="parse && tab === 'mat'">
      <div class="card">
        <div class="card-h"><h3>工料机汇总</h3></div>
        <p class="small mut" style="line-height:1.9;margin:0">人工 / 材料 / 机械的消耗量与价格汇总将在二期逐行解析完成后按此页呈现。</p>
      </div>
    </div>
    <div v-if="parse && tab === 'fee'">
      <div class="card">
        <div class="card-h"><h3>费用汇总</h3></div>
        <p class="small mut" style="line-height:1.9;margin:0">各单位工程的费用构成（分部分项 / 措施 / 其他项目 / 规费 / 税金）将在二期逐行解析完成后按此页呈现；当前总造价见「概览」与「报价汇总」。</p>
      </div>
    </div>

    <!-- 工程信息 -->
    <div v-if="parse && tab === 'info'">
      <div class="card">
        <div class="card-h"><h3>工程信息</h3></div>
        <div class="kv-row"><span class="k">工程名称</span><span>{{ parse.projectName || '—' }}</span></div>
        <div class="kv-row"><span class="k">总造价</span><span class="num">¥{{ fmtMoney(parse.totalAmount) }}</span></div>
        <div class="kv-row"><span class="k">大写金额</span><span>{{ parse.amountCn || '—' }}</span></div>
        <div class="kv-row"><span class="k">计价依据</span><span>{{ parse.basis || '—' }}</span></div>
        <div class="kv-row"><span class="k">所在地区</span><span>{{ parse.region || '—' }}</span></div>
        <div class="kv-row"><span class="k">单位工程</span><span>{{ units.length }} 个：{{ units.join('、') }}</span></div>
        <div class="kv-row"><span class="k">源文件</span><span>{{ file?.fileName || '—' }}</span></div>
        <div class="kv-row"><span class="k">解析时间</span><span class="num">{{ String(parse.parsedAt).slice(0, 16).replace('T', ' ') }}</span></div>
        <div class="kv-row"><span class="k">解析说明</span><span class="mut">{{ parse.note }}</span></div>
      </div>
    </div>

    <div class="card" v-if="!parse">
      <p class="mut">该文件暂无解析结果。GCFX / SGCFX 文件上传后会自动解析；其他格式不解析。</p>
    </div>
  </div>
  <div v-else class="card"><n-spin size="large" /></div>
</template>
