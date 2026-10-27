import { createRouter, createWebHistory } from 'vue-router'

const routes = [
  { path: '/login', name: 'login', component: () => import('./views/Login.vue'), meta: { public: true } },
  {
    path: '/',
    component: () => import('./views/Layout.vue'),
    children: [
      { path: '', name: 'home', component: () => import('./views/Dashboard.vue'), meta: { title: '工作台' } },
      { path: 'projects', name: 'projects', component: () => import('./views/Projects.vue'), meta: { title: '项目台账' } },
      { path: 'projects/:id', name: 'project-detail', component: () => import('./views/ProjectDetail.vue'), meta: { title: '项目详情' } },
      { path: 'contracts', name: 'contracts', component: () => import('./views/Contracts.vue'), meta: { title: '合同台账' } },
      { path: 'contracts/:id', name: 'contract-detail', component: () => import('./views/ContractDetail.vue'), meta: { title: '合同详情' } },
      { path: 'fees', name: 'fees', component: () => import('./views/Fees.vue'), meta: { title: '收费台账' } },
      { path: 'forms', name: 'forms', component: () => import('./views/Forms.vue'), meta: { title: '流程模板' } },
      { path: 'library', name: 'library', component: () => import('./views/Library.vue'), meta: { title: '资料库' } },
      { path: 'stats', name: 'stats', component: () => import('./views/Stats.vue'), meta: { title: '统计分析' } },
      { path: 'fee-calc', name: 'fee-calc', component: () => import('./views/FeeCalc.vue'), meta: { title: '其他费测算' } },
      { path: 'settings', name: 'settings', component: () => import('./views/Settings.vue'), meta: { title: '基础设置' } }
    ]
  },
  { path: '/:pathMatch(.*)*', redirect: '/' }
]

const router = createRouter({ history: createWebHistory(), routes })

router.beforeEach((to) => {
  const token = localStorage.getItem('cloudfri_token')
  if (!to.meta.public && !token) return { name: 'login' }
  if (to.name === 'login' && token) return { name: 'home' }
  return true
})

export default router
