<script setup>
import { computed } from 'vue'
import { fmtYuan, rmbUpper } from '../utils/fees'

// 收费计算结果展示（收费测算各模块共用同一组件）：
// res = { title, std:[下限元,上限元], proc } 或 { warn }；disc 为折扣 %（100 不打折）
const props = defineProps({
  res: { type: Object, default: null },
  disc: { type: Number, default: 100 }
})
const rng = (a) => a[0] === a[1] ? fmtYuan(a[0]) : fmtYuan(a[0]) + ' 至 ' + fmtYuan(a[1])
const upper = computed(() => {
  if (!props.res || props.res.warn) return ''
  const s = props.res.std
  return s[0] === s[1] ? rmbUpper(s[0]) : rmbUpper(s[0]) + ' 至 ' + rmbUpper(s[1])
})
const discAmt = computed(() => {
  if (!props.res || props.res.warn || props.disc === 100) return null
  return [props.res.std[0] * props.disc / 100, props.res.std[1] * props.disc / 100]
})
</script>

<template>
  <div v-if="res" style="margin-top:14px">
    <n-alert v-if="res.warn" type="warning" :show-icon="false" style="border-radius:12px">{{ res.warn }}</n-alert>
    <div v-else style="border-top:1px solid var(--line-soft);padding-top:12px">
      <div class="small mut" style="margin-bottom:4px">{{ res.title }}</div>
      <div style="display:flex;align-items:baseline;gap:14px;flex-wrap:wrap">
        <span class="small mut">标准收费</span>
        <b style="font-size:21px;color:var(--pri-d)">¥ {{ rng(res.std) }} 元</b>
        <span class="small mut">{{ upper }}</span>
      </div>
      <div v-if="discAmt" style="margin-top:4px;font-size:13.5px">
        折扣后（{{ disc }}%）：<b>¥ {{ rng(discAmt) }} 元</b>
      </div>
      <div v-if="res.proc" class="small mut" style="margin-top:8px;line-height:1.75;white-space:pre-wrap">计算过程：{{ res.proc }}</div>
    </div>
  </div>
</template>
