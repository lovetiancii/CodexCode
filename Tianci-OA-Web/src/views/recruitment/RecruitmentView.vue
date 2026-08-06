<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import {
  Calendar,
  Check,
  Close,
  Delete,
  Document,
  Download,
  Edit,
  Plus,
  Refresh,
  Search,
  TrendCharts,
  UploadFilled,
  UserFilled,
  View,
} from '@element-plus/icons-vue'
import {
  ElMessage,
  ElMessageBox,
  type FormInstance,
  type FormRules,
  type UploadRequestOptions,
} from 'element-plus'
import { fileApi, interviewApi, organizationApi, resumeApi } from '@/api/modules'
import { http } from '@/api/http'
import { useAuthStore } from '@/stores/auth'
import type {
  DepartmentDto,
  FileDto,
  InterviewDto,
  InterviewerOptionDto,
  PagedResult,
  PositionDto,
  ResumeDto,
} from '@/types/contracts'
import {
  GENDER,
  INTERVIEW_CONCLUSION,
  RESUME_STATUS,
  RESUME_STATUS_LABEL,
} from '@/types/contracts'

type RecruitmentMode = 'board' | 'resumes' | 'interviews' | 'entry'
type DialogKind = 'resume' | 'schedule' | 'complete' | 'offer' | 'entry'
type TagType = 'primary' | 'success' | 'warning' | 'danger' | 'info'

interface ResumeForm {
  name: string
  gender: number
  phone: string
  email: string
  education: string
  workExperience: string
  skills: string
  appliedPositionId: string
  source: string
  ownerUserId: string
  remark: string
}

interface ScheduleForm {
  roundNo: number
  interviewerUserId: string
  scheduledAt: string
  location: string
  remark: string
}

interface CompleteForm {
  interviewId: string
  score: number
  evaluation: string
  conclusion: number
  isFinalRound: boolean
  nextScheduledAt: string
  remark: string
}

interface OfferForm {
  plannedEntryDate: string
  departmentId: string
  positionId: string
  monthlySalary: string
  probationMonths: number
  remark: string
}

interface EntryForm {
  actualEntryDate: string
  employeeNo: string
  entryVersion: number
}

const route = useRoute()
const auth = useAuthStore()
const validModes: RecruitmentMode[] = ['board', 'resumes', 'interviews', 'entry']
const mode = computed<RecruitmentMode>(() => {
  const value = String(route.meta.mode ?? 'board') as RecruitmentMode
  return validModes.includes(value) ? value : 'board'
})

const loading = ref(false)
const actionLoading = ref(false)
const detailLoading = ref(false)
const errorMessage = ref('')
const resumeRows = ref<ResumeDto[]>([])
const departments = ref<DepartmentDto[]>([])
const positions = ref<PositionDto[]>([])
const total = ref(0)
const selectedResume = ref<ResumeDto | null>(null)
const selectedInterviews = ref<InterviewDto[]>([])
const resumeFiles = ref<FileDto[]>([])
const uploadProgress = ref(0)
const interviewerOptions = ref<InterviewerOptionDto[]>([])
const interviewerLoading = ref(false)
const sameDepartmentOnly = ref(true)
const drawerVisible = ref(false)
const dialogVisible = ref(false)
const dialogKind = ref<DialogKind>('resume')
const editingResume = ref<ResumeDto | null>(null)
const formRef = ref<FormInstance>()

const query = reactive({
  keyword: '',
  positionId: '',
  status: undefined as number | undefined,
  pageNumber: 1,
  pageSize: 10,
})

const emptyResumeForm = (): ResumeForm => ({
  name: '',
  gender: GENDER.Unknown,
  phone: '',
  email: '',
  education: '',
  workExperience: '',
  skills: '',
  appliedPositionId: '',
  source: '',
  ownerUserId: '',
  remark: '',
})
const resumeForm = reactive<ResumeForm>(emptyResumeForm())
const scheduleForm = reactive<ScheduleForm>({
  roundNo: 1,
  interviewerUserId: '',
  scheduledAt: '',
  location: '',
  remark: '',
})
const completeForm = reactive<CompleteForm>({
  interviewId: '',
  score: 80,
  evaluation: '',
  conclusion: INTERVIEW_CONCLUSION.Pass,
  isFinalRound: false,
  nextScheduledAt: '',
  remark: '',
})
const offerForm = reactive<OfferForm>({
  plannedEntryDate: '',
  departmentId: '',
  positionId: '',
  monthlySalary: '',
  probationMonths: 3,
  remark: '',
})
const entryForm = reactive<EntryForm>({
  actualEntryDate: '',
  employeeNo: '',
  entryVersion: 0,
})

const resumeRules: FormRules<ResumeForm> = {
  name: [{ required: true, message: '请输入候选人姓名', trigger: 'blur' }],
  phone: [
    { required: true, message: '请输入手机号', trigger: 'blur' },
    { pattern: /^1\d{10}$/, message: '请输入正确的 11 位手机号', trigger: 'blur' },
  ],
  email: [{ type: 'email', message: '邮箱格式不正确', trigger: 'blur' }],
  appliedPositionId: [{ required: true, message: '请选择应聘岗位', trigger: 'change' }],
}
const scheduleRules: FormRules<ScheduleForm> = {
  interviewerUserId: [{ required: true, message: '请选择面试官', trigger: 'change' }],
  scheduledAt: [{ required: true, message: '请选择面试时间', trigger: 'change' }],
}
const completeRules: FormRules<CompleteForm> = {
  evaluation: [{ required: true, message: '请填写面试评价', trigger: 'blur' }],
}
const offerRules: FormRules<OfferForm> = {
  plannedEntryDate: [{ required: true, message: '请选择计划入职日期', trigger: 'change' }],
  departmentId: [{ required: true, message: '请选择部门', trigger: 'change' }],
  positionId: [{ required: true, message: '请选择岗位', trigger: 'change' }],
  monthlySalary: [{ required: true, message: '请输入月薪', trigger: 'blur' }],
}
const entryRules: FormRules<EntryForm> = {
  actualEntryDate: [{ required: true, message: '请选择实际入职日期', trigger: 'change' }],
  employeeNo: [{ required: true, message: '请输入员工编号', trigger: 'blur' }],
}

const pageTitle = computed(() => ({
  board: '招聘流程看板',
  resumes: '简历管理',
  interviews: '面试管理',
  entry: '录用入职',
})[mode.value])

const pageDescription = computed(() => ({
  board: '掌握候选人从投递、面试到入职的完整进展',
  resumes: '维护候选人档案并推进简历筛选',
  interviews: '安排面试、记录评价并保留完整轮次',
  entry: '完成录用确认与候选人到岗转员工',
})[mode.value])

const dialogTitle = computed(() => {
  if (dialogKind.value === 'resume') return editingResume.value ? '编辑简历' : '新建简历'
  return {
    schedule: '安排面试',
    complete: '填写面试评价',
    offer: '确认录用',
    entry: '确认到岗',
  }[dialogKind.value]
})

const statusOptions = computed(() => Object.entries(RESUME_STATUS_LABEL).map(([value, label]) => ({
  value: Number(value),
  label,
})))

const positionMap = computed<Record<string, string>>(() => Object.fromEntries(
  positions.value.map((item) => [item.id, item.name]),
))
const interviewerMap = computed<Record<string, InterviewerOptionDto>>(() => Object.fromEntries(
  interviewerOptions.value.map((item) => [item.userId, item]),
))
const boardColumns = computed(() => [
  {
    key: 'delivery',
    title: '简历投递',
    tone: 'blue',
    statuses: [RESUME_STATUS.Submitted, RESUME_STATUS.Screening],
  },
  {
    key: 'interview',
    title: '面试阶段',
    tone: 'amber',
    statuses: [RESUME_STATUS.InterviewPending, RESUME_STATUS.Interviewing],
  },
  {
    key: 'offer',
    title: '录用入职',
    tone: 'violet',
    statuses: [RESUME_STATUS.OfferPending, RESUME_STATUS.EntryPending],
  },
  {
    key: 'closed',
    title: '流程归档',
    tone: 'green',
    statuses: [RESUME_STATUS.Hired, RESUME_STATUS.Rejected, RESUME_STATUS.OfferDeclined],
  },
].map((column) => ({
  ...column,
  items: resumeRows.value.filter((item) => containsNumber(column.statuses, item.status)),
})))

const stats = computed(() => [
  {
    label: '简历总数',
    value: total.value,
    icon: Document,
    tone: 'blue',
  },
  {
    label: '待安排面试',
    value: resumeRows.value.filter((item) => item.status === RESUME_STATUS.InterviewPending).length,
    icon: Calendar,
    tone: 'amber',
  },
  {
    label: '面试中',
    value: resumeRows.value.filter((item) => item.status === RESUME_STATUS.Interviewing).length,
    icon: UserFilled,
    tone: 'violet',
  },
  {
    label: '已入职',
    value: resumeRows.value.filter((item) => item.status === RESUME_STATUS.Hired).length,
    icon: Check,
    tone: 'green',
  },
])

const interviewCandidates = computed(() => resumeRows.value.filter((item) => containsNumber([
  RESUME_STATUS.Screening,
  RESUME_STATUS.InterviewPending,
  RESUME_STATUS.Interviewing,
  RESUME_STATUS.OfferPending,
  RESUME_STATUS.Rejected,
], item.status)))

const entryCandidates = computed(() => resumeRows.value.filter((item) => containsNumber([
  RESUME_STATUS.OfferPending,
  RESUME_STATUS.EntryPending,
  RESUME_STATUS.Hired,
  RESUME_STATUS.OfferDeclined,
], item.status)))

const currentRules = computed<FormRules>(() => {
  if (dialogKind.value === 'resume') return resumeRules
  if (dialogKind.value === 'schedule') return scheduleRules
  if (dialogKind.value === 'complete') return completeRules
  if (dialogKind.value === 'offer') return offerRules
  return entryRules
})

const currentForm = computed(() => {
  if (dialogKind.value === 'resume') return resumeForm
  if (dialogKind.value === 'schedule') return scheduleForm
  if (dialogKind.value === 'complete') return completeForm
  if (dialogKind.value === 'offer') return offerForm
  return entryForm
})

function errorText(error: unknown): string {
  return error instanceof Error ? error.message : '请求失败，请稍后重试'
}

function containsNumber(values: readonly number[], value: number): boolean {
  return values.includes(value)
}

function formatDate(value: string | null | undefined, withTime = true): string {
  if (!value) return '—'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return new Intl.DateTimeFormat('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    ...(withTime ? { hour: '2-digit', minute: '2-digit' } : {}),
  }).format(date)
}

function genderLabel(value: number): string {
  return value === GENDER.Male ? '男' : value === GENDER.Female ? '女' : '未填写'
}

function interviewerLabel(userId: string): string {
  const option = interviewerMap.value[userId]
  if (!option && userId === auth.user?.id) {
    return `${auth.user.displayName} · 本人`
  }

  return option ? `${option.name} · ${option.positionName}` : `用户 ${userId}`
}

async function loadInterviewerOptions(keyword = ''): Promise<void> {
  if (!selectedResume.value) return
  interviewerLoading.value = true
  try {
    interviewerOptions.value = await interviewApi.options(
      selectedResume.value.id,
      keyword,
      sameDepartmentOnly.value,
    )
  } catch (error) {
    interviewerOptions.value = []
    ElMessage.error(errorText(error))
  } finally {
    interviewerLoading.value = false
  }
}

function statusTagType(status: number): TagType {
  if (status === RESUME_STATUS.Hired) return 'success'
  if (containsNumber([RESUME_STATUS.Rejected, RESUME_STATUS.OfferDeclined], status)) return 'danger'
  if (containsNumber([RESUME_STATUS.InterviewPending, RESUME_STATUS.Interviewing], status)) return 'warning'
  if (containsNumber([RESUME_STATUS.OfferPending, RESUME_STATUS.EntryPending], status)) return 'primary'
  return 'info'
}

function conclusionLabel(value: number): string {
  return {
    [INTERVIEW_CONCLUSION.Pending]: '待面试',
    [INTERVIEW_CONCLUSION.Pass]: '通过',
    [INTERVIEW_CONCLUSION.Fail]: '不通过',
    [INTERVIEW_CONCLUSION.Hold]: '待定',
    [INTERVIEW_CONCLUSION.Cancelled]: '已取消',
  }[value] ?? '未知'
}

function conclusionTagType(value: number): TagType {
  if (value === INTERVIEW_CONCLUSION.Pass) return 'success'
  if (containsNumber([INTERVIEW_CONCLUSION.Fail, INTERVIEW_CONCLUSION.Cancelled], value)) return 'danger'
  if (value === INTERVIEW_CONCLUSION.Hold) return 'warning'
  return 'info'
}

function resetResumeForm(): void {
  Object.assign(resumeForm, emptyResumeForm())
}

async function loadOptions(): Promise<void> {
  const [departmentResult, positionResult] = await Promise.all([
    organizationApi.departments(),
    organizationApi.positions(),
  ])
  departments.value = departmentResult
  positions.value = positionResult
}

async function loadResumes(): Promise<void> {
  loading.value = true
  errorMessage.value = ''
  try {
    const isOverview = mode.value !== 'resumes'
    const params: Record<string, unknown> = {
      keyword: query.keyword || undefined,
      positionId: query.positionId || undefined,
      status: query.status,
      pageNumber: isOverview ? 1 : query.pageNumber,
      pageSize: isOverview ? 200 : query.pageSize,
    }
    const result: PagedResult<ResumeDto> = await resumeApi.list(params)
    resumeRows.value = result.items
    total.value = result.total
    if (selectedResume.value) {
      selectedResume.value = result.items.find((item) => item.id === selectedResume.value?.id)
        ?? selectedResume.value
    }
  } catch (error) {
    errorMessage.value = errorText(error)
    resumeRows.value = []
    total.value = 0
  } finally {
    loading.value = false
  }
}

async function initialize(): Promise<void> {
  loading.value = true
  errorMessage.value = ''
  try {
    const routeStatus = Number(route.query.status)
    if (Number.isInteger(routeStatus) && routeStatus > 0) query.status = routeStatus
    await loadOptions()
    await loadResumes()
    if (route.query.action === 'create') openResumeDialog()
  } catch (error) {
    errorMessage.value = errorText(error)
    loading.value = false
  }
}

function search(): void {
  query.pageNumber = 1
  void loadResumes()
}

function resetSearch(): void {
  query.keyword = ''
  query.positionId = ''
  query.status = undefined
  query.pageNumber = 1
  void loadResumes()
}

async function showDetail(row: ResumeDto): Promise<void> {
  drawerVisible.value = true
  detailLoading.value = true
  selectedResume.value = row
  selectedInterviews.value = []
  resumeFiles.value = []
  uploadProgress.value = 0
  try {
    const [detail, interviews, interviewers, files] = await Promise.all([
      resumeApi.get(row.id),
      interviewApi.list(row.id),
      auth.has('resume:schedule')
        ? interviewApi.options(row.id, '', false)
        : Promise.resolve([]),
      auth.has('file:download')
        ? fileApi.list('resume', row.id)
        : Promise.resolve([]),
    ])
    selectedResume.value = detail
    selectedInterviews.value = interviews
    interviewerOptions.value = interviewers
    resumeFiles.value = files
  } catch (error) {
    ElMessage.error(errorText(error))
  } finally {
    detailLoading.value = false
  }
}

function openResumeDialog(row?: ResumeDto): void {
  editingResume.value = row ?? null
  resetResumeForm()
  if (row) {
    Object.assign(resumeForm, {
      name: row.name,
      gender: row.gender,
      phone: row.phone,
      email: row.email ?? '',
      education: row.education ?? '',
      workExperience: row.workExperience ?? '',
      skills: row.skills ?? '',
      appliedPositionId: row.appliedPositionId,
      source: row.source ?? '',
      ownerUserId: row.ownerUserId ?? '',
      remark: row.remark ?? '',
    })
  }
  dialogKind.value = 'resume'
  dialogVisible.value = true
}

async function openScheduleDialog(row: ResumeDto): Promise<void> {
  selectedResume.value = row
  sameDepartmentOnly.value = true
  interviewerOptions.value = []
  Object.assign(scheduleForm, {
    roundNo: Math.max(1, row.currentRound + 1),
    interviewerUserId: '',
    scheduledAt: '',
    location: '',
    remark: '',
  })
  dialogKind.value = 'schedule'
  dialogVisible.value = true
  await loadInterviewerOptions()
}

function openCompleteDialog(row: ResumeDto, interview?: InterviewDto): void {
  selectedResume.value = row
  const pending = interview ?? selectedInterviews.value.find((item) => (
    item.conclusion === INTERVIEW_CONCLUSION.Pending || item.conclusion === INTERVIEW_CONCLUSION.Hold
  ))
  if (!pending) {
    ElMessage.warning('没有可评价的面试记录')
    return
  }
  Object.assign(completeForm, {
    interviewId: pending.id,
    score: pending.score ?? 80,
    evaluation: pending.evaluation ?? '',
    conclusion: INTERVIEW_CONCLUSION.Pass,
    isFinalRound: false,
    nextScheduledAt: '',
    remark: pending.remark ?? '',
  })
  dialogKind.value = 'complete'
  dialogVisible.value = true
}

function openOfferDialog(row: ResumeDto): void {
  selectedResume.value = row
  Object.assign(offerForm, {
    plannedEntryDate: '',
    departmentId: '',
    positionId: row.appliedPositionId,
    monthlySalary: '',
    probationMonths: 3,
    remark: '',
  })
  dialogKind.value = 'offer'
  dialogVisible.value = true
}

function openEntryDialog(row: ResumeDto): void {
  selectedResume.value = row
  Object.assign(entryForm, {
    actualEntryDate: '',
    employeeNo: '',
    entryVersion: 0,
  })
  dialogKind.value = 'entry'
  dialogVisible.value = true
}

async function submitDialog(): Promise<void> {
  if (!formRef.value) return
  const valid = await formRef.value.validate().catch(() => false)
  if (!valid) return
  actionLoading.value = true
  try {
    if (dialogKind.value === 'resume') {
      const payload: Record<string, unknown> = {
        ...resumeForm,
        email: resumeForm.email || null,
        education: resumeForm.education || null,
        workExperience: resumeForm.workExperience || null,
        skills: resumeForm.skills || null,
        source: resumeForm.source || null,
        attachmentFileId: editingResume.value?.attachmentFileId || null,
        ownerUserId: resumeForm.ownerUserId || null,
        remark: resumeForm.remark || null,
      }
      if (editingResume.value) {
        await resumeApi.update(editingResume.value.id, editingResume.value.version, payload)
      } else {
        await resumeApi.create(payload)
      }
    } else if (dialogKind.value === 'schedule' && selectedResume.value) {
      await interviewApi.schedule(selectedResume.value.id, {
        ...scheduleForm,
        scheduledAt: new Date(scheduleForm.scheduledAt).toISOString(),
        remark: scheduleForm.remark || null,
        location: scheduleForm.location || null,
        resumeVersion: selectedResume.value.version,
      })
    } else if (dialogKind.value === 'complete' && selectedResume.value) {
      await interviewApi.complete(selectedResume.value.id, completeForm.interviewId, {
        score: completeForm.score,
        evaluation: completeForm.evaluation,
        conclusion: completeForm.conclusion,
        isFinalRound: completeForm.isFinalRound,
        nextScheduledAt: completeForm.nextScheduledAt
          ? new Date(completeForm.nextScheduledAt).toISOString()
          : null,
        remark: completeForm.remark || null,
        resumeVersion: selectedResume.value.version,
      })
    } else if (dialogKind.value === 'offer' && selectedResume.value) {
      await resumeApi.confirmOffer(selectedResume.value.id, {
        ...offerForm,
        plannedEntryDate: offerForm.plannedEntryDate,
        remark: offerForm.remark || null,
        resumeVersion: selectedResume.value.version,
      })
    } else if (dialogKind.value === 'entry' && selectedResume.value) {
      const result = await resumeApi.confirmEntry(selectedResume.value.id, {
        actualEntryDate: entryForm.actualEntryDate,
        employeeNo: entryForm.employeeNo,
        resumeVersion: selectedResume.value.version,
        entryVersion: entryForm.entryVersion,
      })
      ElMessage.success(`入职成功，员工 ID：${result.employeeId}`)
    }
    if (dialogKind.value !== 'entry') ElMessage.success('操作成功')
    dialogVisible.value = false
    await loadResumes()
    if (drawerVisible.value && selectedResume.value) await showDetail(selectedResume.value)
  } catch (error) {
    ElMessage.error(errorText(error))
  } finally {
    actionLoading.value = false
  }
}

function replaceResume(updated: ResumeDto): void {
  selectedResume.value = updated
  const index = resumeRows.value.findIndex((item) => item.id === updated.id)
  if (index >= 0) resumeRows.value[index] = updated
}

async function setPrimaryResumeFile(fileId: string | null): Promise<void> {
  if (!selectedResume.value) return

  const updated = await resumeApi.setAttachment(
    selectedResume.value.id,
    selectedResume.value.version,
    fileId,
  )
  replaceResume(updated)
}

async function uploadResumeFile(options: UploadRequestOptions): Promise<void> {
  if (!selectedResume.value) return

  const formData = new FormData()
  formData.append('businessType', 'resume')
  formData.append('businessId', selectedResume.value.id)
  formData.append('category', 'resume-original')
  formData.append('file', options.file)
  uploadProgress.value = 0

  const uploaded = await fileApi.upload(formData, (percentage) => {
    uploadProgress.value = percentage
  })
  resumeFiles.value.unshift(uploaded)
  await setPrimaryResumeFile(uploaded.id)
  options.onSuccess(uploaded)
  uploadProgress.value = 100
  ElMessage.success('简历已上传并自动关联')
}

async function downloadResumeFile(file: FileDto): Promise<void> {
  const response = await http.get(`/files/${file.id}/download`, {
    responseType: 'blob',
  })
  const url = URL.createObjectURL(response.data as Blob)
  const anchor = document.createElement('a')

  anchor.href = url
  anchor.download = file.originalName
  anchor.click()
  URL.revokeObjectURL(url)
}

async function removeResumeFile(file: FileDto): Promise<void> {
  if (!selectedResume.value) return

  await ElMessageBox.confirm(
    `确认删除简历附件“${file.originalName}”？`,
    '删除附件',
    { type: 'warning' },
  )

  if (selectedResume.value.attachmentFileId === file.id) {
    await setPrimaryResumeFile(null)
  }

  await fileApi.remove(file.id)
  resumeFiles.value = resumeFiles.value.filter((item) => item.id !== file.id)
  ElMessage.success('简历附件已删除')
}

function formatFileSize(sizeBytes: number): string {
  if (sizeBytes < 1024) return `${sizeBytes} B`
  if (sizeBytes < 1024 * 1024) return `${(sizeBytes / 1024).toFixed(1)} KB`
  return `${(sizeBytes / 1024 / 1024).toFixed(1)} MB`
}

async function advanceStatus(row: ResumeDto, targetStatus: number, label: string): Promise<void> {
  try {
    await ElMessageBox.confirm(`确定将「${row.name}」推进至“${label}”吗？`, '流程确认', {
      type: 'warning',
      confirmButtonText: '确定',
      cancelButtonText: '取消',
    })
    actionLoading.value = true
    await resumeApi.changeStatus(row.id, {
      targetStatus,
      version: row.version,
    })
    ElMessage.success('状态已更新')
    await loadResumes()
  } catch (error) {
    if (error === 'cancel' || error === 'close') return
    ElMessage.error(errorText(error))
  } finally {
    actionLoading.value = false
  }
}

async function rejectResume(row: ResumeDto): Promise<void> {
  try {
    const result = await ElMessageBox.prompt('请填写淘汰原因，操作后将保留流程记录。', '淘汰候选人', {
      inputPattern: /\S+/,
      inputErrorMessage: '淘汰原因不能为空',
      confirmButtonText: '确认淘汰',
      cancelButtonText: '取消',
      type: 'warning',
    })
    actionLoading.value = true
    await resumeApi.changeStatus(row.id, {
      targetStatus: RESUME_STATUS.Rejected,
      reason: result.value,
      version: row.version,
    })
    ElMessage.success('候选人已淘汰')
    await loadResumes()
  } catch (error) {
    if (error === 'cancel' || error === 'close') return
    ElMessage.error(errorText(error))
  } finally {
    actionLoading.value = false
  }
}

async function loadCandidateInterviews(row: ResumeDto): Promise<void> {
  selectedResume.value = row
  detailLoading.value = true
  try {
    selectedInterviews.value = await interviewApi.list(row.id)
  } catch (error) {
    selectedInterviews.value = []
    ElMessage.error(errorText(error))
  } finally {
    detailLoading.value = false
  }
}

watch(mode, () => {
  query.pageNumber = 1
  selectedResume.value = null
  selectedInterviews.value = []
  void loadResumes()
})

watch(() => offerForm.departmentId, (departmentId) => {
  if (!departmentId) return
  const currentPosition = positions.value.find((item) => item.id === offerForm.positionId)
  if (currentPosition?.departmentId !== departmentId) offerForm.positionId = ''
})

onMounted(() => {
  void initialize()
})
</script>

<template>
  <section class="recruitment-page">
    <header class="page-heading">
      <div>
        <p class="eyebrow">RECRUITMENT</p>
        <h1>{{ pageTitle }}</h1>
        <p>{{ pageDescription }}</p>
      </div>
      <div class="heading-actions">
        <el-button :icon="Refresh" :loading="loading" @click="loadResumes">刷新</el-button>
        <el-button
          v-if="mode === 'resumes'"
          v-permission="'resume:create'"
          type="primary"
          :icon="Plus"
          @click="openResumeDialog()"
        >
          新建简历
        </el-button>
      </div>
    </header>

    <el-alert
      v-if="errorMessage"
      class="error-alert"
      type="error"
      :title="errorMessage"
      show-icon
      :closable="false"
    >
      <template #default>
        <el-button link type="primary" @click="initialize">重新加载</el-button>
      </template>
    </el-alert>

    <template v-if="mode === 'board'">
      <div v-loading="loading" class="overview-content">
        <div class="stat-grid">
          <article v-for="item in stats" :key="item.label" class="stat-card">
            <span class="stat-icon" :class="`tone-${item.tone}`">
              <el-icon><component :is="item.icon" /></el-icon>
            </span>
            <div>
              <p>{{ item.label }}</p>
              <strong>{{ item.value }}</strong>
            </div>
          </article>
        </div>

        <div class="content-card board-card">
          <div class="card-title">
            <div>
              <h2><el-icon><TrendCharts /></el-icon> 招聘流程</h2>
              <p>点击候选人卡片查看档案与面试记录</p>
            </div>
          </div>
          <div v-if="resumeRows.length" class="kanban">
            <section v-for="column in boardColumns" :key="column.key" class="kanban-column">
              <header :class="`column-${column.tone}`">
                <span>{{ column.title }}</span>
                <el-tag size="small" round>{{ column.items.length }}</el-tag>
              </header>
              <div class="candidate-stack">
                <button
                  v-for="candidate in column.items"
                  :key="candidate.id"
                  class="candidate-card"
                  type="button"
                  @click="showDetail(candidate)"
                >
                  <span class="candidate-avatar">{{ candidate.name.slice(0, 1) }}</span>
                  <span class="candidate-main">
                    <strong>{{ candidate.name }}</strong>
                    <small>{{ positionMap[candidate.appliedPositionId] ?? '未知岗位' }}</small>
                    <small v-if="candidate.currentRound">第 {{ candidate.currentRound }} 轮</small>
                  </span>
                  <el-tag :type="statusTagType(candidate.status)" size="small">
                    {{ RESUME_STATUS_LABEL[candidate.status] }}
                  </el-tag>
                </button>
                <el-empty v-if="!column.items.length" description="暂无候选人" :image-size="56" />
              </div>
            </section>
          </div>
          <el-empty v-else-if="!loading" description="暂无招聘数据，先录入一份简历吧" />
        </div>
      </div>
    </template>

    <template v-else-if="mode === 'resumes'">
      <div class="content-card filter-card">
        <el-form :inline="true" class="search-form" @submit.prevent="search">
          <el-form-item label="搜索">
            <el-input
              v-model.trim="query.keyword"
              clearable
              :prefix-icon="Search"
              placeholder="姓名 / 手机 / 邮箱"
              @keyup.enter="search"
            />
          </el-form-item>
          <el-form-item label="应聘岗位">
            <el-select v-model="query.positionId" clearable filterable placeholder="全部岗位">
              <el-option v-for="item in positions" :key="item.id" :label="item.name" :value="item.id" />
            </el-select>
          </el-form-item>
          <el-form-item label="招聘状态">
            <el-select v-model="query.status" clearable placeholder="全部状态">
              <el-option v-for="item in statusOptions" :key="item.value" :label="item.label" :value="item.value" />
            </el-select>
          </el-form-item>
          <el-form-item>
            <el-button type="primary" :icon="Search" @click="search">查询</el-button>
            <el-button @click="resetSearch">重置</el-button>
          </el-form-item>
        </el-form>
      </div>

      <div class="content-card table-card">
        <el-table v-loading="loading" :data="resumeRows" stripe>
          <el-table-column prop="candidateNo" label="候选人编号" min-width="145" />
          <el-table-column label="候选人" min-width="150">
            <template #default="{ row }: { row: ResumeDto }">
              <div class="person-cell">
                <span class="mini-avatar">{{ row.name.slice(0, 1) }}</span>
                <span><strong>{{ row.name }}</strong><small>{{ row.phone }}</small></span>
              </div>
            </template>
          </el-table-column>
          <el-table-column label="应聘岗位" min-width="130">
            <template #default="{ row }: { row: ResumeDto }">
              {{ positionMap[row.appliedPositionId] ?? '—' }}
            </template>
          </el-table-column>
          <el-table-column label="状态" width="120">
            <template #default="{ row }: { row: ResumeDto }">
              <el-tag :type="statusTagType(row.status)">
                {{ RESUME_STATUS_LABEL[row.status] }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column label="当前轮次" width="100" align="center">
            <template #default="{ row }: { row: ResumeDto }">
              {{ row.currentRound ? `第 ${row.currentRound} 轮` : '—' }}
            </template>
          </el-table-column>
          <el-table-column prop="education" label="学历" width="100" />
          <el-table-column label="操作" fixed="right" width="330">
            <template #default="{ row }: { row: ResumeDto }">
              <el-button link type="primary" :icon="View" @click="showDetail(row)">详情</el-button>
              <el-button
                v-permission="'resume:edit'"
                link
                type="primary"
                :icon="Edit"
                @click="openResumeDialog(row)"
              >
                编辑
              </el-button>
              <el-button
                v-if="row.status === RESUME_STATUS.Submitted"
                v-permission="'resume:manage'"
                link
                type="primary"
                @click="advanceStatus(row, RESUME_STATUS.Screening, '筛选中')"
              >
                开始筛选
              </el-button>
              <el-button
                v-if="containsNumber([RESUME_STATUS.Screening, RESUME_STATUS.InterviewPending], row.status)"
                v-permission="'resume:schedule'"
                link
                type="primary"
                @click="openScheduleDialog(row)"
              >
                安排面试
              </el-button>
              <el-button
                v-if="!containsNumber([RESUME_STATUS.Hired, RESUME_STATUS.Rejected, RESUME_STATUS.OfferDeclined], row.status)"
                v-permission="'resume:manage'"
                link
                type="danger"
                :icon="Close"
                @click="rejectResume(row)"
              >
                淘汰
              </el-button>
            </template>
          </el-table-column>
          <template #empty>
            <el-empty description="没有找到符合条件的简历" />
          </template>
        </el-table>
        <div class="pagination-bar">
          <span>共 {{ total }} 条</span>
          <el-pagination
            v-model:current-page="query.pageNumber"
            v-model:page-size="query.pageSize"
            background
            layout="sizes, prev, pager, next"
            :page-sizes="[10, 20, 50]"
            :total="total"
            @change="loadResumes"
          />
        </div>
      </div>
    </template>

    <template v-else-if="mode === 'interviews'">
      <div v-loading="loading" class="interview-layout">
        <aside class="content-card candidate-panel">
          <div class="card-title">
            <div>
              <h2>面试候选人</h2>
              <p>选择候选人查看轮次</p>
            </div>
            <el-tag>{{ interviewCandidates.length }}</el-tag>
          </div>
          <el-input
            v-model.trim="query.keyword"
            class="candidate-search"
            clearable
            :prefix-icon="Search"
            placeholder="搜索候选人"
            @keyup.enter="search"
            @clear="search"
          />
          <div v-if="interviewCandidates.length" class="candidate-list">
            <button
              v-for="candidate in interviewCandidates"
              :key="candidate.id"
              type="button"
              class="candidate-list-item"
              :class="{ active: selectedResume?.id === candidate.id }"
              @click="loadCandidateInterviews(candidate)"
            >
              <span class="candidate-avatar">{{ candidate.name.slice(0, 1) }}</span>
              <span>
                <strong>{{ candidate.name }}</strong>
                <small>{{ positionMap[candidate.appliedPositionId] ?? '未知岗位' }}</small>
              </span>
              <el-tag :type="statusTagType(candidate.status)" size="small">
                {{ RESUME_STATUS_LABEL[candidate.status] }}
              </el-tag>
            </button>
          </div>
          <el-empty v-else description="暂无面试候选人" :image-size="80" />
        </aside>

        <main class="content-card interview-panel">
          <template v-if="selectedResume">
            <div class="card-title candidate-summary">
              <div>
                <h2>{{ selectedResume.name }} · 面试记录</h2>
                <p>{{ positionMap[selectedResume.appliedPositionId] ?? '未知岗位' }}，当前第 {{ selectedResume.currentRound || 0 }} 轮</p>
              </div>
              <div>
                <el-button
                  v-if="containsNumber([RESUME_STATUS.Screening, RESUME_STATUS.InterviewPending], selectedResume.status)"
                  v-permission="'resume:schedule'"
                  type="primary"
                  :icon="Calendar"
                  @click="openScheduleDialog(selectedResume)"
                >
                  安排面试
                </el-button>
              </div>
            </div>
            <div v-loading="detailLoading">
              <el-timeline v-if="selectedInterviews.length" class="interview-timeline">
                <el-timeline-item
                  v-for="interview in selectedInterviews"
                  :key="interview.id"
                  :timestamp="formatDate(interview.scheduledAt)"
                  placement="top"
                  :type="conclusionTagType(interview.conclusion)"
                  :hollow="interview.conclusion === INTERVIEW_CONCLUSION.Pending"
                >
                  <article class="interview-record">
                    <header>
                      <div>
                        <strong>第 {{ interview.roundNo }} 轮面试</strong>
                        <el-tag :type="conclusionTagType(interview.conclusion)" size="small">
                          {{ conclusionLabel(interview.conclusion) }}
                        </el-tag>
                      </div>
                      <el-button
                        v-if="containsNumber([INTERVIEW_CONCLUSION.Pending, INTERVIEW_CONCLUSION.Hold], interview.conclusion)"
                        v-permission="'resume:evaluate'"
                        link
                        type="primary"
                        @click="openCompleteDialog(selectedResume, interview)"
                      >
                        填写评价
                      </el-button>
                    </header>
                    <dl>
                      <div><dt>面试官</dt><dd>{{ interviewerLabel(interview.interviewerUserId) }}</dd></div>
                      <div><dt>地点</dt><dd>{{ interview.location || '—' }}</dd></div>
                      <div><dt>评分</dt><dd>{{ interview.score ?? '—' }}</dd></div>
                    </dl>
                    <p v-if="interview.evaluation" class="evaluation">{{ interview.evaluation }}</p>
                  </article>
                </el-timeline-item>
              </el-timeline>
              <el-empty v-else description="尚未安排面试">
                <el-button
                  v-if="containsNumber([RESUME_STATUS.Screening, RESUME_STATUS.InterviewPending], selectedResume.status)"
                  v-permission="'resume:schedule'"
                  type="primary"
                  @click="openScheduleDialog(selectedResume)"
                >
                  立即安排
                </el-button>
              </el-empty>
            </div>
          </template>
          <el-empty v-else description="请从左侧选择一位候选人" />
        </main>
      </div>
    </template>

    <template v-else>
      <div class="content-card table-card">
        <div class="card-title">
          <div>
            <h2>录用与到岗</h2>
            <p>待录用候选人确认薪资与入职信息，到岗后自动创建员工档案</p>
          </div>
        </div>
        <el-table v-loading="loading" :data="entryCandidates" stripe>
          <el-table-column label="候选人" min-width="170">
            <template #default="{ row }: { row: ResumeDto }">
              <div class="person-cell">
                <span class="mini-avatar">{{ row.name.slice(0, 1) }}</span>
                <span><strong>{{ row.name }}</strong><small>{{ row.candidateNo }}</small></span>
              </div>
            </template>
          </el-table-column>
          <el-table-column label="应聘岗位" min-width="140">
            <template #default="{ row }: { row: ResumeDto }">
              {{ positionMap[row.appliedPositionId] ?? '—' }}
            </template>
          </el-table-column>
          <el-table-column prop="phone" label="手机号" width="135" />
          <el-table-column label="状态" width="120">
            <template #default="{ row }: { row: ResumeDto }">
              <el-tag :type="statusTagType(row.status)">
                {{ RESUME_STATUS_LABEL[row.status] }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column label="流程提示" min-width="210">
            <template #default="{ row }: { row: ResumeDto }">
              <span v-if="row.status === RESUME_STATUS.OfferPending">完善薪资、部门和计划入职日期</span>
              <span v-else-if="row.status === RESUME_STATUS.EntryPending">候选人到岗后确认实际入职信息</span>
              <span v-else-if="row.status === RESUME_STATUS.Hired">已生成员工档案</span>
              <span v-else>录用流程已结束</span>
            </template>
          </el-table-column>
          <el-table-column label="操作" width="210" fixed="right">
            <template #default="{ row }: { row: ResumeDto }">
              <el-button link type="primary" :icon="View" @click="showDetail(row)">详情</el-button>
              <el-button
                v-if="row.status === RESUME_STATUS.OfferPending"
                v-permission="'resume:hire'"
                type="primary"
                link
                @click="openOfferDialog(row)"
              >
                确认录用
              </el-button>
              <el-button
                v-if="row.status === RESUME_STATUS.EntryPending"
                v-permission="'resume:hire'"
                type="success"
                link
                @click="openEntryDialog(row)"
              >
                确认到岗
              </el-button>
            </template>
          </el-table-column>
          <template #empty>
            <el-empty description="暂无录用或入职记录" />
          </template>
        </el-table>
      </div>
    </template>

    <el-drawer
      v-model="drawerVisible"
      class="candidate-drawer"
      size="min(520px, 94vw)"
      destroy-on-close
    >
      <template #header>
        <div v-if="selectedResume" class="drawer-heading">
          <span class="drawer-avatar">{{ selectedResume.name.slice(0, 1) }}</span>
          <div>
            <h2>{{ selectedResume.name }}</h2>
            <p>{{ selectedResume.candidateNo }} · {{ positionMap[selectedResume.appliedPositionId] ?? '未知岗位' }}</p>
          </div>
        </div>
      </template>
      <div v-if="selectedResume" v-loading="detailLoading">
        <el-tag :type="statusTagType(selectedResume.status)" effect="light">
          {{ RESUME_STATUS_LABEL[selectedResume.status] }}
        </el-tag>
        <el-descriptions class="detail-descriptions" :column="2" border>
          <el-descriptions-item label="性别">{{ genderLabel(selectedResume.gender) }}</el-descriptions-item>
          <el-descriptions-item label="当前轮次">{{ selectedResume.currentRound || '—' }}</el-descriptions-item>
          <el-descriptions-item label="手机号">{{ selectedResume.phone }}</el-descriptions-item>
          <el-descriptions-item label="邮箱">{{ selectedResume.email || '—' }}</el-descriptions-item>
          <el-descriptions-item label="学历">{{ selectedResume.education || '—' }}</el-descriptions-item>
          <el-descriptions-item label="简历附件">{{ selectedResume.attachmentFileId ? '已上传' : '未上传' }}</el-descriptions-item>
        </el-descriptions>
        <section class="detail-section">
          <h3>工作经历</h3>
          <p>{{ selectedResume.workExperience || '暂无工作经历' }}</p>
        </section>
        <section class="detail-section">
          <h3>技能特长</h3>
          <p>{{ selectedResume.skills || '暂无技能信息' }}</p>
        </section>
        <section v-if="selectedResume.rejectReason" class="detail-section reject-reason">
          <h3>淘汰原因</h3>
          <p>{{ selectedResume.rejectReason }}</p>
        </section>
        <section class="detail-section resume-attachment-section">
          <div class="section-heading">
            <div>
              <h3>简历附件</h3>
              <small>先保存候选人，再上传 PDF、DOC、DOCX、JPG 或 PNG，最大 20 MB</small>
            </div>
          </div>
          <el-upload
            v-if="auth.has('file:upload') && auth.has('resume:attachment')"
            drag
            :show-file-list="false"
            :http-request="uploadResumeFile"
            accept=".pdf,.doc,.docx,.jpg,.jpeg,.png"
          >
            <el-icon class="el-icon--upload"><UploadFilled /></el-icon>
            <div class="el-upload__text">拖放简历或<em>点击上传</em></div>
          </el-upload>
          <el-progress
            v-if="uploadProgress > 0 && uploadProgress < 100"
            :percentage="uploadProgress"
          />
          <div v-if="resumeFiles.length" class="resume-file-list">
            <article v-for="file in resumeFiles" :key="file.id">
              <div class="resume-file-meta">
                <el-icon><Document /></el-icon>
                <div>
                  <strong>{{ file.originalName }}</strong>
                  <small>{{ formatFileSize(file.sizeBytes) }}</small>
                </div>
                <el-tag
                  v-if="selectedResume.attachmentFileId === file.id"
                  type="success"
                  size="small"
                >
                  当前附件
                </el-tag>
              </div>
              <div class="resume-file-actions">
                <el-button
                  v-if="auth.has('file:download')"
                  link
                  type="primary"
                  :icon="Download"
                  @click="downloadResumeFile(file)"
                >
                  下载
                </el-button>
                <el-button
                  v-if="auth.has('resume:attachment') && selectedResume.attachmentFileId !== file.id"
                  link
                  type="success"
                  @click="setPrimaryResumeFile(file.id)"
                >
                  设为当前
                </el-button>
                <el-button
                  v-if="auth.has('file:delete') && auth.has('resume:attachment')"
                  link
                  type="danger"
                  :icon="Delete"
                  @click="removeResumeFile(file)"
                >
                  删除
                </el-button>
              </div>
            </article>
          </div>
          <el-empty
            v-else-if="auth.has('file:download')"
            description="暂无简历附件"
            :image-size="54"
          />
          <el-alert
            v-else
            title="当前角色没有附件查看权限"
            type="info"
            :closable="false"
          />
        </section>
        <section class="detail-section">
          <div class="section-heading">
            <h3>面试记录</h3>
            <el-button
              v-if="containsNumber([RESUME_STATUS.Screening, RESUME_STATUS.InterviewPending], selectedResume.status)"
              v-permission="'resume:schedule'"
              link
              type="primary"
              @click="openScheduleDialog(selectedResume)"
            >
              安排面试
            </el-button>
          </div>
          <div v-if="selectedInterviews.length" class="drawer-interviews">
            <article v-for="interview in selectedInterviews" :key="interview.id">
              <span class="round-badge">{{ interview.roundNo }}</span>
              <div>
                <strong>第 {{ interview.roundNo }} 轮 · {{ conclusionLabel(interview.conclusion) }}</strong>
                <p>{{ formatDate(interview.scheduledAt) }} · {{ interview.location || '地点未定' }}</p>
                <small>{{ interview.evaluation || '暂无评价' }}</small>
              </div>
            </article>
          </div>
          <el-empty v-else description="暂无面试记录" :image-size="64" />
        </section>
      </div>
      <template #footer>
        <div v-if="selectedResume" class="drawer-actions">
          <el-button
            v-permission="'resume:edit'"
            :icon="Edit"
            @click="openResumeDialog(selectedResume)"
          >
            编辑简历
          </el-button>
          <el-button
            v-if="selectedResume.status === RESUME_STATUS.OfferPending"
            v-permission="'resume:hire'"
            type="primary"
            @click="openOfferDialog(selectedResume)"
          >
            确认录用
          </el-button>
          <el-button
            v-if="selectedResume.status === RESUME_STATUS.EntryPending"
            v-permission="'resume:hire'"
            type="success"
            @click="openEntryDialog(selectedResume)"
          >
            确认到岗
          </el-button>
        </div>
      </template>
    </el-drawer>

    <el-dialog
      v-model="dialogVisible"
      :title="dialogTitle"
      width="min(680px, 94vw)"
      destroy-on-close
      @closed="formRef?.clearValidate()"
    >
      <el-form
        ref="formRef"
        :model="currentForm"
        :rules="currentRules"
        label-position="top"
        status-icon
      >
        <template v-if="dialogKind === 'resume'">
          <div class="form-grid">
            <el-form-item label="候选人姓名" prop="name">
              <el-input v-model.trim="resumeForm.name" maxlength="100" />
            </el-form-item>
            <el-form-item label="性别" prop="gender">
              <el-select v-model="resumeForm.gender">
                <el-option label="未知" :value="GENDER.Unknown" />
                <el-option label="男" :value="GENDER.Male" />
                <el-option label="女" :value="GENDER.Female" />
              </el-select>
            </el-form-item>
            <el-form-item label="手机号" prop="phone">
              <el-input v-model.trim="resumeForm.phone" maxlength="11" />
            </el-form-item>
            <el-form-item label="邮箱" prop="email">
              <el-input v-model.trim="resumeForm.email" />
            </el-form-item>
            <el-form-item label="应聘岗位" prop="appliedPositionId">
              <el-select v-model="resumeForm.appliedPositionId" filterable>
                <el-option v-for="item in positions" :key="item.id" :label="item.name" :value="item.id" />
              </el-select>
            </el-form-item>
            <el-form-item label="学历" prop="education">
              <el-input v-model.trim="resumeForm.education" maxlength="64" />
            </el-form-item>
            <el-form-item label="招聘来源" prop="source">
              <el-input v-model.trim="resumeForm.source" maxlength="64" placeholder="如：招聘网站、内推" />
            </el-form-item>
          </div>
          <el-alert
            title="简历保存后，请在候选人详情的“简历附件”区域上传文件。"
            type="info"
            show-icon
            :closable="false"
          />
          <el-form-item label="工作经历" prop="workExperience">
            <el-input v-model="resumeForm.workExperience" type="textarea" :rows="3" maxlength="2000" show-word-limit />
          </el-form-item>
          <el-form-item label="技能特长" prop="skills">
            <el-input v-model="resumeForm.skills" type="textarea" :rows="2" maxlength="1000" show-word-limit />
          </el-form-item>
          <el-form-item label="备注" prop="remark">
            <el-input v-model="resumeForm.remark" type="textarea" :rows="2" maxlength="1000" show-word-limit />
          </el-form-item>
        </template>

        <template v-else-if="dialogKind === 'schedule'">
          <el-alert
            v-if="selectedResume"
            :title="`${selectedResume.name} · ${positionMap[selectedResume.appliedPositionId] ?? '未知岗位'}`"
            type="info"
            :closable="false"
          />
          <div class="form-grid dialog-form-grid">
            <el-form-item label="面试轮次" prop="roundNo">
              <el-input-number v-model="scheduleForm.roundNo" :min="1" :max="5" />
            </el-form-item>
            <el-form-item label="面试官" prop="interviewerUserId">
              <el-select
                v-model="scheduleForm.interviewerUserId"
                filterable
                remote
                clearable
                reserve-keyword
                :remote-method="loadInterviewerOptions"
                :loading="interviewerLoading"
                placeholder="按姓名、工号、账号或岗位搜索"
                no-data-text="暂无可用面试官，请先关联并启用员工账号"
              >
                <el-option
                  v-for="item in interviewerOptions"
                  :key="item.userId"
                  :label="`${item.name}（${item.employeeNo}）· ${item.positionName}`"
                  :value="item.userId"
                >
                  <div class="interviewer-option">
                    <strong>{{ item.name }}（{{ item.employeeNo }}）</strong>
                    <span>{{ item.departmentName }} / {{ item.positionName }}</span>
                  </div>
                </el-option>
              </el-select>
            </el-form-item>
            <el-form-item label="面试时间" prop="scheduledAt">
              <el-date-picker
                v-model="scheduleForm.scheduledAt"
                type="datetime"
                value-format="YYYY-MM-DDTHH:mm:ss"
                placeholder="选择日期和时间"
              />
            </el-form-item>
            <el-form-item label="面试地点" prop="location">
              <el-input v-model.trim="scheduleForm.location" maxlength="255" placeholder="会议室或线上会议地址" />
            </el-form-item>
          </div>
          <div class="interviewer-filter">
            <el-switch
              v-model="sameDepartmentOnly"
              active-text="优先显示应聘岗位所属部门"
              inactive-text="搜索全公司"
              @change="scheduleForm.interviewerUserId = ''; loadInterviewerOptions()"
            />
            <small>只有已关联员工且处于启用状态的用户账号可以被选为面试官。</small>
          </div>
          <el-form-item label="备注" prop="remark">
            <el-input v-model="scheduleForm.remark" type="textarea" :rows="3" maxlength="1000" show-word-limit />
          </el-form-item>
        </template>

        <template v-else-if="dialogKind === 'complete'">
          <div class="form-grid dialog-form-grid">
            <el-form-item label="面试评分" prop="score">
              <el-input-number v-model="completeForm.score" :min="0" :max="100" />
            </el-form-item>
            <el-form-item label="面试结论" prop="conclusion">
              <el-select v-model="completeForm.conclusion">
                <el-option label="通过" :value="INTERVIEW_CONCLUSION.Pass" />
                <el-option label="不通过" :value="INTERVIEW_CONCLUSION.Fail" />
                <el-option label="待定" :value="INTERVIEW_CONCLUSION.Hold" />
                <el-option label="取消" :value="INTERVIEW_CONCLUSION.Cancelled" />
              </el-select>
            </el-form-item>
          </div>
          <el-form-item label="面试评价" prop="evaluation">
            <el-input v-model="completeForm.evaluation" type="textarea" :rows="5" maxlength="2000" show-word-limit />
          </el-form-item>
          <div class="form-grid dialog-form-grid">
            <el-form-item label="是否最终轮">
              <el-switch v-model="completeForm.isFinalRound" inline-prompt active-text="是" inactive-text="否" />
            </el-form-item>
            <el-form-item
              v-if="completeForm.conclusion === INTERVIEW_CONCLUSION.Pass && !completeForm.isFinalRound"
              label="下一轮建议时间"
            >
              <el-date-picker
                v-model="completeForm.nextScheduledAt"
                type="datetime"
                value-format="YYYY-MM-DDTHH:mm:ss"
                placeholder="可选"
              />
            </el-form-item>
          </div>
          <el-form-item label="备注" prop="remark">
            <el-input v-model="completeForm.remark" type="textarea" :rows="2" maxlength="1000" />
          </el-form-item>
        </template>

        <template v-else-if="dialogKind === 'offer'">
          <el-alert
            v-if="selectedResume"
            :title="`为 ${selectedResume.name} 确认录用信息`"
            type="success"
            :closable="false"
          />
          <div class="form-grid dialog-form-grid">
            <el-form-item label="计划入职日期" prop="plannedEntryDate">
              <el-date-picker
                v-model="offerForm.plannedEntryDate"
                type="date"
                value-format="YYYY-MM-DD"
                placeholder="选择日期"
              />
            </el-form-item>
            <el-form-item label="试用期（月）" prop="probationMonths">
              <el-input-number v-model="offerForm.probationMonths" :min="0" :max="12" />
            </el-form-item>
            <el-form-item label="入职部门" prop="departmentId">
              <el-select v-model="offerForm.departmentId" filterable>
                <el-option v-for="item in departments" :key="item.id" :label="item.name" :value="item.id" />
              </el-select>
            </el-form-item>
            <el-form-item label="入职岗位" prop="positionId">
              <el-select v-model="offerForm.positionId" filterable>
                <el-option
                  v-for="item in positions.filter((position) => !offerForm.departmentId || position.departmentId === offerForm.departmentId)"
                  :key="item.id"
                  :label="item.name"
                  :value="item.id"
                />
              </el-select>
            </el-form-item>
            <el-form-item label="月薪" prop="monthlySalary">
              <el-input v-model.trim="offerForm.monthlySalary" placeholder="仅授权人员可查看">
                <template #prepend>¥</template>
              </el-input>
            </el-form-item>
          </div>
          <el-form-item label="录用备注" prop="remark">
            <el-input v-model="offerForm.remark" type="textarea" :rows="3" maxlength="1000" show-word-limit />
          </el-form-item>
        </template>

        <template v-else>
          <el-alert
            v-if="selectedResume"
            :title="`${selectedResume.name} 到岗后将自动生成员工档案`"
            type="warning"
            :closable="false"
          />
          <div class="form-grid dialog-form-grid">
            <el-form-item label="实际入职日期" prop="actualEntryDate">
              <el-date-picker
                v-model="entryForm.actualEntryDate"
                type="date"
                value-format="YYYY-MM-DD"
                placeholder="选择日期"
              />
            </el-form-item>
            <el-form-item label="员工编号" prop="employeeNo">
              <el-input v-model.trim="entryForm.employeeNo" placeholder="例如：TC20260001" />
            </el-form-item>
          </div>
        </template>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="actionLoading" @click="submitDialog">
          {{ dialogKind === 'entry' ? '确认到岗' : '保存' }}
        </el-button>
      </template>
    </el-dialog>
  </section>
</template>

<style scoped>
.recruitment-page {
  min-height: 100%;
  color: #1f2a3d;
}

.page-heading {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 24px;
  margin-bottom: 20px;
}

.eyebrow {
  margin: 0 0 6px;
  color: #3b82f6;
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.14em;
}

.page-heading h1 {
  margin: 0;
  font-size: 26px;
  line-height: 1.35;
}

.page-heading p:not(.eyebrow) {
  margin: 6px 0 0;
  color: #8491a5;
  font-size: 14px;
}

.heading-actions {
  display: flex;
  flex-shrink: 0;
}

.error-alert {
  margin-bottom: 16px;
}

.overview-content {
  min-height: 360px;
}

.stat-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 16px;
  margin-bottom: 16px;
}

.stat-card,
.content-card {
  border: 1px solid #eaf0f7;
  border-radius: 12px;
  background: #fff;
  box-shadow: 0 7px 22px rgb(31 42 61 / 4%);
}

.stat-card {
  display: flex;
  align-items: center;
  gap: 14px;
  padding: 20px;
}

.stat-card p {
  margin: 0 0 4px;
  color: #8793a6;
  font-size: 13px;
}

.stat-card strong {
  font-size: 27px;
  line-height: 1;
}

.stat-icon {
  display: grid;
  width: 46px;
  height: 46px;
  place-items: center;
  border-radius: 12px;
  font-size: 22px;
}

.tone-blue {
  color: #3276e8;
  background: #eaf2ff;
}

.tone-amber {
  color: #e7981b;
  background: #fff6df;
}

.tone-violet {
  color: #7b61d9;
  background: #f1edff;
}

.tone-green {
  color: #15945e;
  background: #e7f8f0;
}

.content-card {
  padding: 20px;
}

.board-card {
  min-height: 420px;
}

.card-title {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  margin-bottom: 18px;
}

.card-title h2 {
  display: flex;
  align-items: center;
  gap: 7px;
  margin: 0;
  font-size: 17px;
}

.card-title p {
  margin: 5px 0 0;
  color: #929daf;
  font-size: 13px;
}

.kanban {
  display: grid;
  grid-template-columns: repeat(4, minmax(220px, 1fr));
  gap: 14px;
  overflow-x: auto;
}

.kanban-column {
  min-height: 315px;
  overflow: hidden;
  border: 1px solid #edf1f7;
  border-radius: 10px;
  background: #f8fafc;
}

.kanban-column > header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 13px 14px;
  border-top: 3px solid;
  background: #fff;
  font-size: 14px;
  font-weight: 700;
}

.column-blue {
  border-color: #4a89f3 !important;
}

.column-amber {
  border-color: #f1aa35 !important;
}

.column-violet {
  border-color: #8269db !important;
}

.column-green {
  border-color: #2cac77 !important;
}

.candidate-stack {
  display: grid;
  gap: 9px;
  padding: 10px;
}

.candidate-card,
.candidate-list-item {
  width: 100%;
  border: 1px solid #e9eef5;
  background: #fff;
  color: inherit;
  font: inherit;
  text-align: left;
  cursor: pointer;
  transition: 0.18s ease;
}

.candidate-card {
  display: flex;
  align-items: center;
  gap: 9px;
  padding: 12px;
  border-radius: 8px;
}

.candidate-card:hover,
.candidate-list-item:hover,
.candidate-list-item.active {
  border-color: #9fc4fa;
  box-shadow: 0 5px 16px rgb(59 130 246 / 10%);
  transform: translateY(-1px);
}

.candidate-avatar,
.mini-avatar,
.drawer-avatar {
  display: grid;
  flex: none;
  place-items: center;
  border-radius: 50%;
  color: #3276e8;
  background: #eaf2ff;
  font-weight: 700;
}

.candidate-avatar {
  width: 32px;
  height: 32px;
}

.candidate-main {
  display: grid;
  min-width: 0;
  flex: 1;
}

.candidate-main strong,
.candidate-main small {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.candidate-main small {
  margin-top: 2px;
  color: #8d98aa;
  font-size: 11px;
}

.filter-card {
  margin-bottom: 16px;
  padding-bottom: 2px;
}

.search-form :deep(.el-input) {
  width: 210px;
}

.search-form :deep(.el-select) {
  width: 180px;
}

.table-card {
  overflow: hidden;
}

.person-cell {
  display: flex;
  align-items: center;
  gap: 10px;
}

.mini-avatar {
  width: 34px;
  height: 34px;
}

.person-cell span:last-child {
  display: grid;
}

.person-cell small {
  margin-top: 2px;
  color: #909aad;
}

.pagination-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding-top: 18px;
  color: #8b96a8;
  font-size: 13px;
}

.interview-layout {
  display: grid;
  grid-template-columns: 340px minmax(0, 1fr);
  gap: 16px;
  min-height: 560px;
}

.candidate-panel,
.interview-panel {
  min-height: 520px;
}

.candidate-search {
  margin-bottom: 12px;
}

.candidate-list {
  display: grid;
  gap: 8px;
  max-height: 600px;
  overflow-y: auto;
}

.candidate-list-item {
  display: grid;
  grid-template-columns: 36px 1fr auto;
  align-items: center;
  gap: 10px;
  padding: 11px;
  border-radius: 9px;
}

.candidate-list-item.active {
  background: #f2f7ff;
}

.candidate-list-item span:nth-child(2) {
  display: grid;
  min-width: 0;
}

.candidate-list-item small {
  overflow: hidden;
  margin-top: 3px;
  color: #8d98aa;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.interview-timeline {
  padding: 12px 6px 0;
}

.interview-record {
  padding: 15px;
  border: 1px solid #e9eef5;
  border-radius: 10px;
  background: #fff;
}

.interview-record header,
.interview-record header > div {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
}

.interview-record dl {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 14px;
  padding: 12px 0;
  margin: 12px 0 0;
  border-top: 1px solid #eff2f6;
}

.interview-record dl div {
  display: grid;
  gap: 4px;
}

.interview-record dt {
  color: #8c97a9;
  font-size: 12px;
}

.interview-record dd {
  margin: 0;
  font-size: 13px;
}

.evaluation {
  padding: 11px;
  margin: 0;
  border-radius: 7px;
  background: #f7f9fc;
  color: #536078;
  line-height: 1.6;
}

.drawer-heading {
  display: flex;
  align-items: center;
  gap: 12px;
}

.drawer-avatar {
  width: 44px;
  height: 44px;
  font-size: 18px;
}

.drawer-heading h2 {
  margin: 0;
  font-size: 19px;
}

.drawer-heading p {
  margin: 4px 0 0;
  color: #8d98aa;
  font-size: 12px;
}

.detail-descriptions {
  margin-top: 15px;
}

.detail-section {
  padding-top: 18px;
  margin-top: 18px;
  border-top: 1px solid #edf1f6;
}

.detail-section h3 {
  margin: 0 0 9px;
  font-size: 14px;
}

.detail-section > p {
  margin: 0;
  color: #667289;
  line-height: 1.75;
  white-space: pre-wrap;
}

.reject-reason {
  padding: 13px;
  border: 1px solid #fde1e1;
  border-radius: 8px;
  background: #fff6f6;
}

.reject-reason h3,
.reject-reason p {
  color: #c44d4d;
}

.section-heading {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.section-heading small {
  color: #8b96a8;
}

.resume-attachment-section :deep(.el-upload),
.resume-attachment-section :deep(.el-upload-dragger) {
  width: 100%;
}

.resume-attachment-section :deep(.el-upload-dragger) {
  padding: 18px;
  margin-top: 12px;
}

.resume-file-list {
  display: grid;
  gap: 9px;
  margin-top: 12px;
}

.resume-file-list article {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 10px 12px;
  border: 1px solid #e8edf5;
  border-radius: 8px;
  background: #f9fbfe;
}

.resume-file-meta,
.resume-file-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.resume-file-meta {
  min-width: 0;
}

.resume-file-meta > div {
  display: grid;
  min-width: 0;
}

.resume-file-meta strong {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.resume-file-meta small {
  color: #929cad;
}

.drawer-interviews {
  display: grid;
  gap: 12px;
}

.drawer-interviews article {
  display: flex;
  gap: 11px;
}

.drawer-interviews article > div {
  display: grid;
  gap: 4px;
}

.drawer-interviews p {
  margin: 0;
  color: #7f8a9d;
  font-size: 12px;
}

.drawer-interviews small {
  color: #9aa4b5;
}

.round-badge {
  display: grid;
  width: 26px;
  height: 26px;
  flex: none;
  place-items: center;
  border-radius: 50%;
  color: #fff;
  background: #4a89f3;
  font-size: 12px;
  font-weight: 700;
}

.drawer-actions {
  display: flex;
  justify-content: flex-end;
}

.form-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  column-gap: 18px;
}

.dialog-form-grid {
  margin-top: 18px;
}

.interviewer-option {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  width: 100%;
}

.interviewer-option span,
.interviewer-filter small {
  color: #8b96a8;
  font-size: 12px;
}

.interviewer-filter {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  margin: -4px 0 18px;
  padding: 10px 12px;
  border-radius: 8px;
  background: #f7f9fc;
}

.form-grid :deep(.el-select),
.form-grid :deep(.el-date-editor),
.form-grid :deep(.el-input-number) {
  width: 100%;
}

@media (max-width: 1100px) {
  .stat-grid {
    grid-template-columns: repeat(2, 1fr);
  }

  .kanban {
    grid-template-columns: repeat(4, 260px);
  }
}

@media (max-width: 760px) {
  .page-heading {
    align-items: flex-start;
    flex-direction: column;
  }

  .stat-grid,
  .form-grid {
    grid-template-columns: 1fr;
  }

  .interview-layout {
    grid-template-columns: 1fr;
  }

  .candidate-panel {
    min-height: auto;
  }

  .interview-record dl {
    grid-template-columns: 1fr;
  }

  .pagination-bar {
    align-items: flex-start;
    flex-direction: column;
    gap: 12px;
  }
}
</style>
