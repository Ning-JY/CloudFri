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
const parse = computed(() => d.value?.gcfxParses?.find(g => g.projectFileId === fileId) || null)
const units = computed(() => {
  try { return parse.value?.unitsJson ? JSON.parse(parse.value.unitsJson) : [] } catch { return [] }
})
const file = computed(() => d.value?.files?.find(f => f.id === fileId))

onMounted(async () => {
  const { data } = await api.get('/projects/' + projectId)
  d.value = data
})
</script>

<template>
  <div v-if="d">
    <div class="card d-head">
      <div style="display:flex;gap:10px;align-items:flex-start;flex-wrap:wrap">
        <div style="flex:1;min-width:260px">
          <div class="small faint" style="margin-bottom:6px">造价解析 · {{ file?.fileName }}</div>
          <div class="d-title" style="font-size:18px">{{ parse?.projectName || d.project.name }}</div>
          <div class="small mut" style="margin-top:6px">上传成果文件后自动解析 · 数据来自 GCFX 文件字符串级提取</div>
        </div>
        <n-button @click="router.push('/projects/' + projectId)">返回项目详情</n-button>
      </div>
      <div class="d-amounts" v-if="parse">
        <div class="a"><div class="lb">工程总造价</div><div class="vl num">¥{{ fmtMoney(parse.totalAmount) }}</div><div class="sb">与文件大写金额核对</div></div>
        <div class="a"><div class="lb">计价依据</div><div class="vl" style="font-size:14.5px;line-height:1.45">{{ parse.basis || '—' }}</div><div class="sb">计价方式</div></div>
        <div class="a"><div class="lb">所在地区</div><div class="vl" style="font-size:16px">{{ parse.region || '—' }}</div><div class="sb">地区类别</div></div>
        <div class="a"><div class="lb">单位工程构成</div><div class="vl num">{{ units.length }} <small style="font-size:13px">类</small></div><div class="sb">按专业构成提取</div></div>
      </div>
    </div>

    <div class="card" v-if="parse">
      <div class="card-h"><h3>工程信息</h3></div>
      <div class="kv-row"><span class="k">工程名称</span><span>{{ parse.projectName || '—' }}</span></div>
      <div class="kv-row"><span class="k">计价依据</span><span>{{ parse.basis || '—' }}</span></div>
      <div class="kv-row"><span class="k">所在地区</span><span>{{ parse.region || '—' }}</span></div>
      <div class="kv-row"><span class="k">总造价</span><span class="num">¥{{ fmtMoney(parse.totalAmount) }}</span></div>
      <div class="kv-row"><span class="k">解析时间</span><span class="num">{{ String(parse.parsedAt).slice(0, 16).replace('T', ' ') }}</span></div>
      <div class="kv-row"><span class="k">解析说明</span><span class="mut">{{ parse.note }}</span></div>
    </div>

    <div class="card" v-if="parse">
      <div class="card-h"><h3>单位工程构成</h3><div class="sp"></div><span class="small faint">自计价文件工程树提取</span></div>
      <div style="display:flex;flex-wrap:wrap;gap:9px">
        <n-tag v-for="u in units" :key="u" size="medium" :bordered="false" style="border-radius:999px">{{ u }}</n-tag>
      </div>
      <p class="small faint" style="margin-top:14px">说明：当前为一期解析（工程信息、总造价、计价依据、单位工程构成）。分部分项清单逐行、定额组价、工料机汇总等深度结构化解析为二期攻关项，GCFX 二进制记录格式逆向完成后上线。</p>
    </div>

    <div class="card" v-if="!parse">
      <p class="mut">该文件暂无解析结果。GCFX / SGCFX 文件上传后会自动解析；其他格式不解析。</p>
    </div>
  </div>
  <div v-else class="card"><n-spin size="large" /></div>
</template>
