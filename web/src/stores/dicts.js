import { defineStore } from 'pinia'
import api from '../api'

// 字典兜底值（与后端种子一致）：字典接口未加载/为空时用这份，保证下拉永远有选项
export const DICT_FALLBACK = {
  BizType: ['结算审核', '预算编制', '清单控制价', '竣工决算', '跟踪审计', '全过程咨询'],
  ProjectStatus: ['编制中', '审核中', '已出报告', '已归档'],
  ContractType: ['框架合同', '单项合同', '补充协议'],
  TeamRole: ['项目负责人', '编制人', '审核人', '审定人', '配合人员'],
  DocCategory: ['设计图纸', '隐蔽工程资料', '工作联系函', '会议纪要', '其他资料'],
  LibCategory: ['规章制度', '规范文件', '标准图集', '其他资料']
}

// 全站字典：登录后由 Layout 加载一次，各页下拉统一从这里取（字典维护增删/排序后刷新即全站生效）
export const useDicts = defineStore('dicts', {
  state: () => ({ items: [], loaded: false }),
  actions: {
    async load(force = false) {
      if (this.loaded && !force) return
      try {
        const { data } = await api.get('/dicts')
        this.items = data
        this.loaded = true
      } catch { /* 加载失败时用兜底值，不阻塞页面 */ }
    }
  },
  getters: {
    // 某分组的有序名称数组（按 Sort）
    names: (s) => (group) => {
      const rows = s.items.filter(x => x.group === group).sort((a, b) => a.sort - b.sort).map(x => x.name)
      return rows.length ? rows : (DICT_FALLBACK[group] || [])
    },
    // 某分组的 n-select 选项
    options: (s) => (group) => s.names(group).map(t => ({ label: t, value: t })),
    // 某分组的原始字典项（设置页维护用，含 id/sort）
    rows: (s) => (group) => s.items.filter(x => x.group === group).sort((a, b) => a.sort - b.sort)
  }
})
