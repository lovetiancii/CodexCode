<script setup lang="ts">
import { computed, onActivated, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { Calendar, DocumentAdd, Plus, Refresh, Right, UserFilled } from '@element-plus/icons-vue'
import { contractApi, employeeApi, resumeApi } from '@/api/modules'
import { RESUME_STATUS } from '@/types/contracts'

const router = useRouter()
const loading = ref(true)
const sectionErrors = ref<string[]>([])
const state = reactive({
  resumes: 0, screening: 0, interviewing: 0, hired: 0, expiring: 0,
  employeeTotal: 0, recentEntries: [] as { month: string; count: number }[],
})

const metrics = computed(() => [
  { label: '员工总数', value: state.employeeTotal, note: '当前人员档案', color: '#2563eb', bg: '#eaf2ff', path: '/employees' },
  { label: '简历总数', value: state.resumes, note: '招聘人才库', color: '#7c3aed', bg: '#f1ebff', path: '/recruitment/resumes' },
  { label: '待筛选', value: state.screening, note: '需要及时处理', color: '#e38a13', bg: '#fff5df', path: '/recruitment/resumes?status=1' },
  { label: '面试中', value: state.interviewing, note: '含待安排场次', color: '#16a269', bg: '#e7f8f0', path: '/recruitment/interviews' },
  { label: '合同即将到期', value: state.expiring, note: '未来 30 天', color: '#e34a4a', bg: '#ffeded', path: '/contracts?expiring=true' },
])
const funnel = computed(() => [
  { label: '简历投递', value: state.resumes, width: 100, color: '#4e8df5' },
  { label: '待筛选', value: state.screening, width: 82, color: '#42c99a' },
  { label: '面试中', value: state.interviewing, width: 64, color: '#f5c45b' },
  { label: '已入职', value: state.hired, width: 46, color: '#fa8657' },
])
const maxTrend = computed(() => Math.max(...state.recentEntries.map((item) => item.count), 1))
const trendPoints = computed(() => state.recentEntries.map((item, index) => {
  const x = 12 + index * (276 / Math.max(state.recentEntries.length - 1, 1))
  const y = 112 - item.count / maxTrend.value * 82
  return `${x},${y}`
}).join(' '))
const todos = computed(() => [
  { title: `${state.screening} 份简历等待筛选`, category: '招聘', path: '/recruitment/resumes?status=1', danger: state.screening > 0 },
  { title: `${state.interviewing} 位候选人处于面试流程`, category: '面试', path: '/recruitment/interviews', danger: false },
  { title: `${state.expiring} 份合同将在 30 天内到期`, category: '合同', path: '/contracts?expiring=true', danger: state.expiring > 0 },
])

function monthKeys() {
  const formatter = new Intl.DateTimeFormat('zh-CN', { month: 'numeric' })
  return Array.from({ length: 6 }, (_, index) => {
    const date = new Date(); date.setMonth(date.getMonth() - (5 - index))
    return { key: `${date.getFullYear()}-${date.getMonth()}`, label: formatter.format(date) }
  })
}

async function load() {
  loading.value = true; sectionErrors.value = []
  const calls = await Promise.allSettled([
    employeeApi.list({ pageNumber: 1, pageSize: 100 }),
    resumeApi.list({ pageNumber: 1, pageSize: 1 }),
    resumeApi.list({ status: RESUME_STATUS.Submitted, pageNumber: 1, pageSize: 1 }),
    resumeApi.list({ status: RESUME_STATUS.InterviewPending, pageNumber: 1, pageSize: 1 }),
    resumeApi.list({ status: RESUME_STATUS.Interviewing, pageNumber: 1, pageSize: 1 }),
    resumeApi.list({ status: RESUME_STATUS.Hired, pageNumber: 1, pageSize: 1 }),
    contractApi.expiring(30),
  ])
  const get = <T,>(index: number): T | undefined => calls[index]?.status === 'fulfilled' ? (calls[index] as PromiseFulfilledResult<T>).value : undefined
  const employees = get<Awaited<ReturnType<typeof employeeApi.list>>>(0)
  state.employeeTotal = employees?.total || 0
  state.resumes = get<Awaited<ReturnType<typeof resumeApi.list>>>(1)?.total || 0
  state.screening = get<Awaited<ReturnType<typeof resumeApi.list>>>(2)?.total || 0
  state.interviewing = (get<Awaited<ReturnType<typeof resumeApi.list>>>(3)?.total || 0) + (get<Awaited<ReturnType<typeof resumeApi.list>>>(4)?.total || 0)
  state.hired = get<Awaited<ReturnType<typeof resumeApi.list>>>(5)?.total || 0
  state.expiring = get<Awaited<ReturnType<typeof contractApi.expiring>>>(6)?.length || 0
  const months = monthKeys()
  state.recentEntries = months.map((month) => ({
    month: month.label,
    count: employees?.items.filter((employee) => {
      const date = new Date(employee.entryDate)
      return `${date.getFullYear()}-${date.getMonth()}` === month.key
    }).length || 0,
  }))
  calls.forEach((result, index) => { if (result.status === 'rejected') sectionErrors.value.push(`数据区块 ${index + 1} 加载失败`) })
  loading.value = false
}
onMounted(load)
onActivated(() => { if (!loading.value) load() })
</script>

<template>
  <div class="page dashboard-page" v-loading="loading">
    <div class="page-heading">
      <div><p class="eyebrow">WORKSPACE OVERVIEW</p><h1>工作台</h1><p>实时掌握人员、招聘与合同进展。</p></div>
      <el-button :icon="Refresh" @click="load">刷新数据</el-button>
    </div>
    <el-alert v-if="sectionErrors.length" :title="`${sectionErrors.length} 个数据区块暂时不可用，其余内容已正常展示`" type="warning" show-icon :closable="false" />
    <div class="metric-grid">
      <button v-for="metric in metrics" :key="metric.label" class="metric-card" @click="router.push(metric.path)">
        <span class="metric-icon" :style="{ color: metric.color, background: metric.bg }"><UserFilled /></span>
        <span class="metric-copy"><small>{{ metric.label }}</small><strong>{{ metric.value }}</strong><em>{{ metric.note }}</em></span>
        <el-icon class="metric-arrow"><Right /></el-icon>
      </button>
    </div>
    <div class="dashboard-grid">
      <section class="panel funnel-panel">
        <div class="panel-heading"><div><h2>招聘流程概览</h2><p>从简历到入职的转化情况</p></div><el-button link type="primary" @click="router.push('/recruitment/board')">查看看板</el-button></div>
        <el-empty v-if="!state.resumes" description="暂无招聘数据" :image-size="72" />
        <div v-else class="funnel">
          <button v-for="item in funnel" :key="item.label" class="funnel-row" :style="{ width: `${item.width}%`, background: item.color }" @click="router.push('/recruitment/resumes')">
            <span>{{ item.label }}</span><strong>{{ item.value }}</strong>
          </button>
        </div>
      </section>
      <section class="panel trend-panel">
        <div class="panel-heading"><div><h2>入职趋势</h2><p>近 6 个月入职人数</p></div></div>
        <div class="trend-chart">
          <svg viewBox="0 0 300 130" role="img" aria-label="近六个月入职趋势">
            <defs><linearGradient id="chartFill" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#4b87f6" stop-opacity=".28"/><stop offset="1" stop-color="#4b87f6" stop-opacity="0"/></linearGradient></defs>
            <line v-for="y in [30, 58, 86, 114]" :key="y" x1="10" :y1="y" x2="290" :y2="y" stroke="#e9edf4" />
            <polygon v-if="trendPoints" :points="`12,114 ${trendPoints} 288,114`" fill="url(#chartFill)" />
            <polyline :points="trendPoints" fill="none" stroke="#3478ed" stroke-width="3" stroke-linecap="round" stroke-linejoin="round" />
          </svg>
          <div class="trend-labels"><span v-for="item in state.recentEntries" :key="item.month"><strong>{{ item.count }}</strong>{{ item.month }}</span></div>
        </div>
      </section>
      <section class="panel todo-panel">
        <div class="panel-heading"><div><h2>待办事项</h2><p>按紧急程度及时跟进</p></div></div>
        <div class="todo-list">
          <button v-for="todo in todos" :key="todo.title" @click="router.push(todo.path)">
            <span class="todo-dot" :class="{ danger: todo.danger }" /><span class="todo-text">{{ todo.title }}</span><el-tag size="small" effect="plain">{{ todo.category }}</el-tag><el-icon><Right /></el-icon>
          </button>
        </div>
      </section>
      <section class="panel quick-panel">
        <div class="panel-heading"><div><h2>快捷入口</h2><p>常用人事操作</p></div></div>
        <div class="quick-grid">
          <button v-permission="'resume:create'" @click="router.push('/recruitment/resumes?action=create')"><el-icon><Plus /></el-icon><span>新增简历</span></button>
          <button v-permission="'resume:interview'" @click="router.push('/recruitment/interviews?action=schedule')"><el-icon><Calendar /></el-icon><span>安排面试</span></button>
          <button v-permission="'resume:hire'" @click="router.push('/recruitment/entry')"><el-icon><UserFilled /></el-icon><span>确认入职</span></button>
          <button v-permission="'contract:manage'" @click="router.push('/contracts?action=create')"><el-icon><DocumentAdd /></el-icon><span>新增合同</span></button>
        </div>
      </section>
    </div>
  </div>
</template>
