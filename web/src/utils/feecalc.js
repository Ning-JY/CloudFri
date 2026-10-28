// 工程建设其他费计算（口径与独立版 feiyong.9996655.xyz / 观音镇测算表一致，验证基准：
// 建安 559.108 + 建筑面积 1743.10 + 占地 5033.09 → 其他费 84.7558、预备费 12.8773、总投资 656.7411 万元）。
// 纯函数、无 DOM 依赖，页面与校验脚本共用，不要在页面里另写一份算法。

// 差额累进：tiers=[[上限,费率],...]，费率为小数
export function tiered(amount, tiers) {
  let fee = 0, prev = 0
  const parts = []
  for (const [cap, rate] of tiers) {
    if (amount <= prev) break
    const seg = Math.min(amount, cap) - prev
    if (seg > 0) { fee += seg * rate; parts.push(seg.toFixed(2) + '×' + (rate * 100).toFixed(2) + '%') }
    prev = cap
  }
  return { fee, proc: parts.join(' + ') }
}

// 基价表直线内插：table=[[计费额,基价],...]，两端外按首末值钳制
export function interp(x, table) {
  if (x <= table[0][0]) return table[0][1]
  for (let i = 1; i < table.length; i++) {
    if (x <= table[i][0]) {
      const [x0, y0] = table[i - 1]
      const [x1, y1] = table[i]
      return y0 + (y1 - y0) * (x - x0) / (x1 - x0)
    }
  }
  return table[table.length - 1][1]
}

const SUPERVISE_BASE = [[500, 16.5], [1000, 30.1], [3000, 78.1], [5000, 120.8], [8000, 181.0], [10000, 218.6], [20000, 393.4], [40000, 708.2], [60000, 991.4], [80000, 1255.8], [100000, 1507.0], [200000, 2712.5], [400000, 4882.6], [600000, 6835.6], [800000, 8658.4], [1000000, 10390.1]]
const DESIGN_BASE = [[200, 9.0], [500, 20.9], [1000, 38.8], [3000, 103.8], [5000, 163.9], [8000, 249.6], [10000, 304.8], [20000, 566.8], [40000, 1054.0], [60000, 1515.2], [80000, 1960.1], [100000, 2393.4], [200000, 4450.8], [400000, 8276.7], [600000, 11897.5], [800000, 15391.4], [1000000, 18793.8], [2000000, 34948.9]]
// 环评 计价格[2002]125号：按估算投资额(亿元)分档 [下限,上限] 万元
const EIA_TIERS = [0.3, 1, 5, 10, Infinity]
const EIA = {
  book: [[6, 15], [15, 35], [35, 75], [75, 110], [110, 150]],
  table: [[1, 2], [2, 4], [4, 7], [7, 9], [9, 11]],
  bookEval: [[1.5, 3], [3, 6], [6, 10], [10, 14], [14, 18]],
  tableEval: [[0.5, 1.5], [1.5, 2.5], [2.5, 4], [4, 6], [6, 8]]
}
const eiaPick = (range, mode) => mode === 'low' ? range[0] : mode === 'high' ? range[1] : (range[0] + range[1]) / 2

export function amountCn(n) {
  const digits = ['零', '壹', '贰', '叁', '肆', '伍', '陆', '柒', '捌', '玖'], units = ['', '拾', '佰', '仟'], big = ['', '万', '亿', '兆']
  if (n === 0) return '零元整'
  let yuan = Math.round(n * 100)
  const jiao = Math.floor(yuan / 10) % 10, fen = yuan % 10
  yuan = Math.floor(yuan / 100)
  let str = '', group = 0, zero = false
  while (yuan > 0) {
    let g = yuan % 10000, gstr = '', gzero = true
    for (let i = 0; i < 4; i++) {
      const d = g % 10
      if (d === 0) { if (!gzero) zero = true }
      else { gstr = digits[d] + units[i] + (zero ? '零' : '') + gstr; zero = false; gzero = false }
      g = Math.floor(g / 10)
    }
    if (gstr) str = gstr + big[group] + str
    else if (str && str.indexOf('零') !== 0) str = '零' + str
    yuan = Math.floor(yuan / 10000); group++
  }
  str = str.replace(/零+/g, '零').replace(/零$/, '') + '元'
  if (jiao === 0 && fen === 0) return str + '整'
  if (jiao > 0) str += digits[jiao] + '角'
  if (fen > 0) str += (jiao === 0 ? '零' : '') + digits[fen] + '分'
  return str
}

export const FEE_DEFAULTS = {
  pname: '', jianan: null, jianzhu: null, zhandi: null, yongdi: 0, shuibaoFangan: 0,
  xiafu: 20, xiafuDl: 40, jianliCoef: 1, shejiCoef: 1, yubeiRate: 2, huanpingPick: 'mid',
  kyBianzhi: 28, kyPinggu: 10, qingdanCoef: 1.25, shenjianRate: 5, xiaoyiRate: 3,
  kanchaRate: 1.5, tushenPrice: 1.8, buchangPrice: 1.3, changdiRate: 5, baoxianRate: 2
}

const fmt4 = (x) => x.toFixed(4)

// 计算全部费用行：入参为 FEE_DEFAULTS 形状的对象（数字可为 null/NaN，按 0 处理）；
// 返回 null 表示建安费未填。rows 每行 {no,name,proc,amt,basis,sub}，sub 为分组小计行（不重复计入合计）。
export function calcFees(inp) {
  const num = (k) => { const v = parseFloat(inp[k]); return isNaN(v) ? 0 : v }
  const E4 = num('jianan')
  if (E4 <= 0) return null
  const f = 1 - num('xiafu') / 100
  const fDl = 1 - num('xiafuDl') / 100
  const rows = []
  const push = (no, name, proc, amt, basis, sub) => rows.push({ no, name, proc, amt, basis: basis || '', sub: !!sub })

  push('2.1', '建设用地费', '按实际金额计列', num('yongdi'), '以实际发生额为准')

  const mgmt = tiered(E4, [[1000, 0.02], [5000, 0.015], [10000, 0.012], [50000, 0.01], [100000, 0.008], [Infinity, 0.004]])
  push('2.2', '项目建设管理费', mgmt.proc, mgmt.fee, '财建〔2016〕504号，差额累进（基数暂按建安费）')

  const supBase = interp(E4, SUPERVISE_BASE)
  const sup = supBase * num('jianliCoef') * f
  push('2.3', '施工监理费', '基价内插 ' + fmt4(supBase) + ' × 系数 ' + num('jianliCoef') + ' × ' + f.toFixed(2), sup, '发改价格〔2007〕670号基价表内插，下浮后计')

  const ky1 = E4 * num('kyBianzhi') / 10000, ky2 = E4 * num('kyPinggu') / 10000
  push('2.4', '建设工程前期工作咨询费', '编制可研 + 评估可研', ky1 + ky2, '计价格〔1999〕1283号 / 川价字费〔2000〕35号', true)
  push('2.4.1', '编制可行性研究报告', '建安费 × ' + num('kyBianzhi') + '/10000', ky1, '3000万以下可研费可协商，暂按比例法')
  push('2.4.2', '评估可行性研究报告', '建安费 × ' + num('kyPinggu') + '/10000', ky2, '同上')

  const dl = tiered(E4, [[100, 0.01], [500, 0.007], [1000, 0.0055], [5000, 0.0035], [10000, 0.002], [100000, 0.0005], [Infinity, 0.0001]])
  push('2.5', '招投标代理费', '(' + dl.proc + ') × ' + fDl.toFixed(2), dl.fee * fDl, '计价格〔2002〕1980号工程招标费率差额累进，下浮后计')

  const zj = (tiers, extra) => { const r = tiered(E4, tiers); return { fee: r.fee * f * (extra || 1), proc: '(' + r.proc + ') × ' + f.toFixed(2) + (extra ? ' × ' + extra : '') } }
  const z1 = zj([[100, 0.0015], [500, 0.0013], [Infinity, 0.0012]])
  const z2 = zj([[100, 0.004], [500, 0.0038], [Infinity, 0.0036]], num('qingdanCoef'))
  const z3 = zj([[100, 0.0036], [500, 0.0034], [Infinity, 0.0032]])
  const z4 = zj([[100, 0.013], [500, 0.012], [Infinity, 0.011]])
  const z5base = tiered(E4, [[100, 0.005], [500, 0.0048], [Infinity, 0.0046]])
  const z5 = z5base.fee * f + E4 * num('shenjianRate') / 100 * num('xiaoyiRate') / 100 * f
  push('2.6', '工程造价咨询服务费', '2.6.1 + … + 2.6.5', z1.fee + z2.fee + z3.fee + z4.fee + z5, '川价发〔2008〕141号，下浮后计', true)
  push('2.6.1', '编制工程设计概算', z1.proc, z1.fee, '')
  push('2.6.2', '工程量清单编制（含预算控制价）', z2.proc, z2.fee, '含清单附加系数')
  push('2.6.3', '招标控制价（预算）审核', z3.proc, z3.fee, '')
  push('2.6.4', '施工阶段全过程造价控制', z4.proc, z4.fee, '')
  push('2.6.5', '竣工结算审核费', '(' + z5base.proc + ') × ' + f.toFixed(2) + ' + 效益费（审减 ' + num('shenjianRate') + '% × ' + num('xiaoyiRate') + '%）', z5, '基本费差额累进 + 效益费')

  const kc = E4 * num('kanchaRate') / 100 * f
  const desBase = interp(E4, DESIGN_BASE)
  const des = desBase * num('shejiCoef') * f
  push('2.7', '工程勘察设计费', '2.7.1 + 2.7.2', kc + des, '', true)
  push('2.7.1', '工程勘察费', '建安费 × ' + num('kanchaRate') + '% × ' + f.toFixed(2), kc, '按工程费用比例计')
  push('2.7.2', '工程设计费', '基价内插 ' + fmt4(desBase) + ' × 系数 ' + num('shejiCoef') + ' × ' + f.toFixed(2), des, '计价格〔2002〕10号基价表内插，下浮后计')

  const ts = num('jianzhu') * num('tushenPrice') * f / 10000
  push('2.8', '施工图审查费', '建筑面积 ' + num('jianzhu') + '㎡ × ' + num('tushenPrice') + '元/㎡ × ' + f.toFixed(2), ts, '川发改价格〔2011〕323号')

  const yi = E4 / 10000
  let ti = 0
  while (ti < EIA_TIERS.length - 1 && yi >= EIA_TIERS[ti]) ti++
  const mode = inp.huanpingPick || 'mid'
  const e1 = eiaPick(EIA.book[ti], mode) * f, e2 = eiaPick(EIA.table[ti], mode) * f,
    e3 = eiaPick(EIA.bookEval[ti], mode) * f, e4 = eiaPick(EIA.tableEval[ti], mode) * f
  push('2.9', '环境影响咨询服务费', '2.9.1 + … + 2.9.4', e1 + e2 + e3 + e4, '计价格〔2002〕125号，按估算投资额分档取区间' + (mode === 'mid' ? '中值' : mode === 'low' ? '下限' : '上限'), true)
  push('2.9.1', '编制环境影响报告书(含大纲)', '区间 ' + EIA.book[ti][0] + '–' + EIA.book[ti][1] + ' 万 × ' + f.toFixed(2), e1, '')
  push('2.9.2', '编制环境影响报告表', '区间 ' + EIA.table[ti][0] + '–' + EIA.table[ti][1] + ' 万 × ' + f.toFixed(2), e2, '')
  push('2.9.3', '评估环境影响报告书(含大纲)', '区间 ' + EIA.bookEval[ti][0] + '–' + EIA.bookEval[ti][1] + ' 万 × ' + f.toFixed(2), e3, '')
  push('2.9.4', '评估环境影响报告表', '区间 ' + EIA.tableEval[ti][0] + '–' + EIA.tableEval[ti][1] + ' 万 × ' + f.toFixed(2), e4, '')

  push('2.10', '场地准备及临时设施费', '建安费 × ' + num('changdiRate') + '‰', E4 * num('changdiRate') / 1000, '按工程费用比例计')
  push('2.11', '工程保险费', '建安费 × ' + num('baoxianRate') + '‰', E4 * num('baoxianRate') / 1000, '按工程费用比例计')
  push('2.12', '水土保持方案编制和监测费', '按核定金额计列', num('shuibaoFangan'), '水利部保监〔2005〕22号，需结合土建投资与监测期核定')
  push('2.13', '水土保持补偿费', '征占地面积 ' + num('zhandi') + '㎡ × ' + num('buchangPrice') + '元/㎡ × ' + f.toFixed(2), num('zhandi') * num('buchangPrice') * f / 10000, '川发改价格〔2017〕347号')

  const other = rows.reduce((s, r) => r.sub ? s : s + r.amt, 0)
  const yubei = (E4 + other) * num('yubeiRate') / 100
  const total = E4 + other + yubei
  return { rows, jianan: E4, other, yubei, total, totalCn: amountCn(total * 10000) }
}
