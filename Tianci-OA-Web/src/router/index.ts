import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { useAppStore } from '@/stores/app'

declare module 'vue-router' {
  interface RouteMeta {
    title?: string
    permission?: string
    mode?: string
    public?: boolean
  }
}

const businessRoutes: RouteRecordRaw[] = [
  { path: 'dashboard', name: 'dashboard', component: () => import('@/views/dashboard/DashboardView.vue'), meta: { title: '首页' } },
  { path: 'employees', name: 'employees', component: () => import('@/views/employee/EmployeeView.vue'), meta: { title: '员工档案', permission: 'employee:view', mode: 'employees' } },
  { path: 'departments', name: 'departments', component: () => import('@/views/employee/EmployeeView.vue'), meta: { title: '组织架构', permission: 'organization:manage', mode: 'departments' } },
  { path: 'positions', name: 'positions', component: () => import('@/views/employee/EmployeeView.vue'), meta: { title: '岗位管理', permission: 'organization:manage', mode: 'positions' } },
  { path: 'recruitment/board', name: 'recruitment-board', component: () => import('@/views/recruitment/RecruitmentView.vue'), meta: { title: '招聘看板', permission: 'resume:view', mode: 'board' } },
  { path: 'recruitment/resumes', name: 'resumes', component: () => import('@/views/recruitment/RecruitmentView.vue'), meta: { title: '简历管理', permission: 'resume:view', mode: 'resumes' } },
  { path: 'recruitment/interviews', name: 'interviews', component: () => import('@/views/recruitment/RecruitmentView.vue'), meta: { title: '面试安排与记录', permission: 'resume:view', mode: 'interviews' } },
  { path: 'recruitment/entry', name: 'entry', component: () => import('@/views/recruitment/RecruitmentView.vue'), meta: { title: '入职办理', permission: 'resume:hire', mode: 'entry' } },
  { path: 'contracts', name: 'contracts', component: () => import('@/views/contract/ContractView.vue'), meta: { title: '合同档案', permission: 'contract:view' } },
  { path: 'system/users', name: 'users', component: () => import('@/views/system/SystemView.vue'), meta: { title: '用户管理', permission: 'system:user', mode: 'users' } },
  { path: 'system/roles', name: 'roles', component: () => import('@/views/system/SystemView.vue'), meta: { title: '角色管理', permission: 'system:role', mode: 'roles' } },
  { path: 'system/menus', name: 'menus', component: () => import('@/views/system/SystemView.vue'), meta: { title: '菜单权限', permission: 'system:menu', mode: 'menus' } },
  { path: 'system/audit', name: 'audit', component: () => import('@/views/system/SystemView.vue'), meta: { title: '操作日志', permission: 'audit:view', mode: 'audit' } },
]

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  scrollBehavior: () => ({ top: 0 }),
  routes: [
    { path: '/login', name: 'login', component: () => import('@/views/login/LoginView.vue'), meta: { title: '登录', public: true } },
    {
      path: '/', component: () => import('@/layouts/AppLayout.vue'),
      children: [{ path: '', redirect: '/dashboard' }, ...businessRoutes],
    },
    { path: '/403', name: 'forbidden', component: () => import('@/views/error/ForbiddenView.vue'), meta: { title: '无权访问' } },
    { path: '/:pathMatch(.*)*', redirect: '/dashboard' },
  ],
})

router.beforeEach((to) => {
  document.title = `${to.meta.title || '工作台'} - Tianci OA`
  const auth = useAuthStore()
  if (!to.meta.public && !auth.authenticated) return { name: 'login', query: { redirect: to.fullPath } }
  if (to.name === 'login' && auth.authenticated) return { name: 'dashboard' }
  if (to.meta.permission && !auth.has(to.meta.permission)) return { name: 'forbidden' }
})
router.afterEach((to) => {
  if (!to.meta.public && to.meta.title) useAppStore().addTab({ path: to.fullPath, title: to.meta.title, closable: to.path !== '/dashboard' })
})

export default router
