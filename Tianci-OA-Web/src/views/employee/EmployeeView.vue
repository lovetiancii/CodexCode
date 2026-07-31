<script setup lang="ts">
import { computed, onActivated, onMounted, reactive, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { Delete, Edit, Plus, Refresh, Search, View } from '@element-plus/icons-vue'
import { employeeApi, organizationApi } from '@/api/modules'
import {
  EMPLOYEE_STATUS, EMPLOYEE_STATUS_LABEL, type DepartmentDto, type EmployeeDetailDto, type EmployeeDto, type PositionDto,
} from '@/types/contracts'

const route = useRoute()
const mode = computed(() => String(route.meta.mode || 'employees'))
const loading = ref(false)
const error = ref('')
const rows = ref<EmployeeDto[]>([])
const departments = ref<DepartmentDto[]>([])
const positions = ref<PositionDto[]>([])
const total = ref(0)
const query = reactive({ keyword: '', departmentId: '', positionId: '', status: undefined as number | undefined, pageNumber: 1, pageSize: 20 })
const editorOpen = ref(false)
const detailOpen = ref(false)
const regularizeOpen = ref(false)
const saving = ref(false)
const actionLoading = ref(false)
const editing = ref<EmployeeDto | null>(null)
const regularizingEmployee = ref<EmployeeDto | null>(null)
const regularizeDate = ref('')
const detail = ref<EmployeeDetailDto | null>(null)
const formRef = ref<FormInstance>()
const form = reactive({
  employeeNo: '', sourceResumeId: '', name: '', gender: 0, phone: '', email: '', idCard: '',
  departmentId: '', positionId: '', entryDate: '', probationMonths: 3, regularDate: '', monthlySalary: '',
})
const rules: FormRules = {
  employeeNo: [{ required: true, message: '请输入员工编号', trigger: 'blur' }],
  name: [{ required: true, message: '请输入姓名', trigger: 'blur' }],
  phone: [{ required: true, message: '请输入手机号', trigger: 'blur' }],
  departmentId: [{ required: true, message: '请选择部门', trigger: 'change' }],
  positionId: [{ required: true, message: '请选择岗位', trigger: 'change' }],
  entryDate: [{ required: true, message: '请选择入职日期', trigger: 'change' }],
}
const departmentDialog = ref(false)
const positionDialog = ref(false)
const orgEditingId = ref('')
const departmentForm = reactive({ parentId: '', name: '', code: '', leaderEmployeeId: '', sortOrder: 0, status: 1, remark: '' })
const positionForm = reactive({ departmentId: '', name: '', code: '', status: 1, remark: '' })

const departmentMap = computed(() => Object.fromEntries(departments.value.map((item) => [item.id, item.name])))
const positionMap = computed(() => Object.fromEntries(positions.value.map((item) => [item.id, item.name])))
const filteredPositions = computed(() => positions.value.filter((item) => !form.departmentId || item.departmentId === form.departmentId))
const pageTitle = computed(() => ({ employees: '员工档案', departments: '组织架构', positions: '岗位管理' })[mode.value] || '人员管理')
const overdueProbationCount = computed(() => rows.value.filter(isProbationOverdue).length)

function cleanParams(record: Record<string, unknown>) {
  return Object.fromEntries(Object.entries(record).filter(([, value]) => value !== '' && value !== undefined))
}
async function loadOptions() {
  const [departmentResult, positionResult] = await Promise.all([organizationApi.departments(), organizationApi.positions()])
  departments.value = departmentResult
  positions.value = positionResult
}
async function load() {
  loading.value = true; error.value = ''
  try {
    await loadOptions()
    if (mode.value === 'employees') {
      const result = await employeeApi.list(cleanParams(query))
      rows.value = result.items; total.value = result.total
    }
  } catch (cause) { error.value = cause instanceof Error ? cause.message : '数据加载失败' }
  finally { loading.value = false }
}
function resetQuery() {
  Object.assign(query, { keyword: '', departmentId: '', positionId: '', status: undefined, pageNumber: 1 })
  load()
}
function resetEmployeeForm() {
  Object.assign(form, {
    employeeNo: '', sourceResumeId: '', name: '', gender: 0, phone: '', email: '', idCard: '',
    departmentId: '', positionId: '', entryDate: new Date().toISOString().slice(0, 10), probationMonths: 3, regularDate: '', monthlySalary: '',
  })
}
function dateInputValue(date = new Date()) {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}
function plannedRegularDate(row: EmployeeDto) {
  if (row.regularDate) return row.regularDate.slice(0, 10)
  const date = new Date(`${row.entryDate.slice(0, 10)}T00:00:00`)
  const entryDay = date.getDate()
  date.setDate(1)
  date.setMonth(date.getMonth() + row.probationMonths)
  const lastDayOfMonth = new Date(date.getFullYear(), date.getMonth() + 1, 0).getDate()
  date.setDate(Math.min(entryDay, lastDayOfMonth))
  return dateInputValue(date)
}
function isProbationOverdue(row: EmployeeDto) {
  return row.status === EMPLOYEE_STATUS.Probation && plannedRegularDate(row) < dateInputValue()
}
function overdueDays(row: EmployeeDto) {
  if (!isProbationOverdue(row)) return 0
  const elapsed = new Date(dateInputValue()).getTime() - new Date(plannedRegularDate(row)).getTime()
  return Math.max(1, Math.floor(elapsed / 86_400_000))
}
function openRegularize(row: EmployeeDto) {
  regularizingEmployee.value = row
  regularizeDate.value = dateInputValue()
  regularizeOpen.value = true
}
function disableRegularizeDate(date: Date) {
  const row = regularizingEmployee.value
  if (!row) return true
  const value = dateInputValue(date)
  return value > dateInputValue() || value < row.entryDate.slice(0, 10)
}
async function confirmRegularize() {
  const row = regularizingEmployee.value
  if (!row || !regularizeDate.value) { ElMessage.warning('请选择实际转正日期'); return }
  const entryDate = row.entryDate.slice(0, 10)
  const today = dateInputValue()
  if (regularizeDate.value < entryDate || regularizeDate.value > today) {
    ElMessage.warning(`实际转正日期须在入职日期 ${entryDate} 与今天之间`)
    return
  }
  actionLoading.value = true
  try {
    await employeeApi.regularize(row.id, { regularDate: regularizeDate.value, version: row.version })
    ElMessage.success(`${row.name} 已确认转正`)
    regularizeOpen.value = false
    await load()
  } finally { actionLoading.value = false }
}
function openCreate() { editing.value = null; resetEmployeeForm(); editorOpen.value = true }
function openEdit(row: EmployeeDto) {
  editing.value = row
  Object.assign(form, {
    employeeNo: row.employeeNo, sourceResumeId: row.sourceResumeId || '', name: row.name, gender: row.gender, phone: row.phone,
    email: row.email || '', idCard: '', departmentId: row.departmentId, positionId: row.positionId, entryDate: row.entryDate.slice(0, 10),
    probationMonths: row.probationMonths, regularDate: row.regularDate?.slice(0, 10) || '', monthlySalary: '',
  })
  editorOpen.value = true
}
async function saveEmployee() {
  if (!await formRef.value?.validate()) return
  saving.value = true
  const payload = cleanParams({ ...form, regularDate: form.regularDate || null })
  try {
    if (editing.value) await employeeApi.update(editing.value.id, editing.value.version, payload)
    else await employeeApi.create(payload)
    ElMessage.success(editing.value ? '员工信息已更新' : '员工已新增')
    editorOpen.value = false; await load()
  } finally { saving.value = false }
}
async function showDetail(row: EmployeeDto) {
  detailOpen.value = true; detail.value = null
  try { detail.value = await employeeApi.get(row.id, false) } catch { detailOpen.value = false }
}
async function showSensitive() {
  if (!detail.value) return
  detail.value = await employeeApi.get(detail.value.employee.id, true)
}
async function terminate(row: EmployeeDto) {
  const { value: date } = await ElMessageBox.prompt('请输入离职日期（YYYY-MM-DD）', '办理离职', { inputPattern: /^\d{4}-\d{2}-\d{2}$/, inputErrorMessage: '日期格式不正确' })
  const { value: reason } = await ElMessageBox.prompt('请填写离职原因', '办理离职', { inputValidator: (value) => Boolean(value?.trim()) || '离职原因不能为空' })
  await employeeApi.terminate(row.id, { terminationDate: date, reason, version: row.version })
  ElMessage.success('已办理离职'); await load()
}
async function archive(row: EmployeeDto) {
  await ElMessageBox.confirm(`确认归档员工“${row.name}”的档案？归档后将只读。`, '归档确认', { type: 'warning' })
  await employeeApi.archive(row.id, row.version); ElMessage.success('档案已归档'); await load()
}

function openDepartment(row?: DepartmentDto) {
  orgEditingId.value = row?.id || ''
  Object.assign(departmentForm, row ? { parentId: row.parentId || '', name: row.name, code: row.code, sortOrder: row.sortOrder, status: row.status, remark: row.remark || '', leaderEmployeeId: '' } : { parentId: '', name: '', code: '', leaderEmployeeId: '', sortOrder: 0, status: 1, remark: '' })
  departmentDialog.value = true
}
async function saveDepartment() {
  if (!departmentForm.name || !departmentForm.code) { ElMessage.warning('请填写部门名称和编码'); return }
  const payload = cleanParams(departmentForm)
  if (orgEditingId.value) await organizationApi.updateDepartment(orgEditingId.value, payload)
  else await organizationApi.createDepartment(payload)
  ElMessage.success('部门已保存'); departmentDialog.value = false; await load()
}
async function deleteDepartment(row: DepartmentDto) {
  await ElMessageBox.confirm(`确认删除未被引用的部门“${row.name}”？`, '删除确认', { type: 'warning' })
  await organizationApi.deleteDepartment(row.id); ElMessage.success('部门已删除'); await load()
}
function openPosition(row?: PositionDto) {
  orgEditingId.value = row?.id || ''
  Object.assign(positionForm, row ? { departmentId: row.departmentId, name: row.name, code: row.code, status: row.status, remark: row.remark || '' } : { departmentId: departments.value[0]?.id || '', name: '', code: '', status: 1, remark: '' })
  positionDialog.value = true
}
async function savePosition() {
  if (!positionForm.departmentId || !positionForm.name || !positionForm.code) { ElMessage.warning('请完整填写岗位信息'); return }
  if (orgEditingId.value) await organizationApi.updatePosition(orgEditingId.value, positionForm)
  else await organizationApi.createPosition(positionForm)
  ElMessage.success('岗位已保存'); positionDialog.value = false; await load()
}
async function deletePosition(row: PositionDto) {
  await ElMessageBox.confirm(`确认删除未被引用的岗位“${row.name}”？`, '删除确认', { type: 'warning' })
  await organizationApi.deletePosition(row.id); ElMessage.success('岗位已删除'); await load()
}
watch(() => form.departmentId, () => { if (!filteredPositions.value.some((item) => item.id === form.positionId)) form.positionId = '' })
watch(mode, load)
onMounted(load)
onActivated(load)
</script>

<template>
  <div class="page">
    <div class="page-heading compact"><div><p class="eyebrow">PEOPLE</p><h1>{{ pageTitle }}</h1><p>维护组织与员工全生命周期档案。</p></div><el-button :icon="Refresh" @click="load">刷新</el-button></div>
    <el-alert v-if="error" :title="error" type="error" show-icon :closable="false"><template #default><el-button link type="primary" @click="load">重新加载</el-button></template></el-alert>

    <template v-if="mode === 'employees'">
      <section class="panel search-panel">
        <el-form inline>
          <el-form-item label="关键字"><el-input v-model="query.keyword" clearable placeholder="姓名 / 编号 / 手机" :prefix-icon="Search" @keyup.enter="query.pageNumber = 1; load()" /></el-form-item>
          <el-form-item label="部门"><el-select v-model="query.departmentId" clearable filterable placeholder="全部部门"><el-option v-for="item in departments" :key="item.id" :label="item.name" :value="item.id" /></el-select></el-form-item>
          <el-form-item label="岗位"><el-select v-model="query.positionId" clearable filterable placeholder="全部岗位"><el-option v-for="item in positions" :key="item.id" :label="item.name" :value="item.id" /></el-select></el-form-item>
          <el-form-item label="状态"><el-select v-model="query.status" clearable placeholder="全部状态"><el-option v-for="(label, value) in EMPLOYEE_STATUS_LABEL" :key="value" :label="label" :value="Number(value)" /></el-select></el-form-item>
          <el-form-item><el-button type="primary" :icon="Search" @click="query.pageNumber = 1; load()">查询</el-button><el-button @click="resetQuery">重置</el-button></el-form-item>
        </el-form>
      </section>
      <section class="panel table-panel" v-loading="loading">
        <div class="table-toolbar"><div><h2>员工列表</h2><span>共 {{ total }} 条档案</span></div><el-button v-permission="'employee:create'" type="primary" :icon="Plus" @click="openCreate">新增员工</el-button></div>
        <el-alert v-if="overdueProbationCount" :title="`${overdueProbationCount} 名试用员工已超过计划转正日期，请及时处理`" type="warning" show-icon :closable="false" class="probation-alert" />
        <el-table :data="rows" stripe empty-text="暂无员工档案">
          <el-table-column prop="employeeNo" label="员工编号" width="130" />
          <el-table-column label="姓名" min-width="130"><template #default="{ row }"><el-button link type="primary" @click="showDetail(row)">{{ row.name }}</el-button></template></el-table-column>
          <el-table-column label="部门 / 岗位" min-width="180"><template #default="{ row }"><strong>{{ departmentMap[row.departmentId] || '—' }}</strong><small class="table-sub">{{ positionMap[row.positionId] || '—' }}</small></template></el-table-column>
          <el-table-column prop="phone" label="手机" width="135" />
          <el-table-column label="入职日期" width="120"><template #default="{ row }">{{ row.entryDate.slice(0, 10) }}</template></el-table-column>
          <el-table-column label="计划转正" width="140"><template #default="{ row }">
            <template v-if="row.status === EMPLOYEE_STATUS.Probation">
              <el-tag v-if="isProbationOverdue(row)" type="danger" effect="light">逾期 {{ overdueDays(row) }} 天</el-tag>
              <span v-else>{{ plannedRegularDate(row) }}</span>
              <small v-if="isProbationOverdue(row)" class="table-sub">{{ plannedRegularDate(row) }}</small>
            </template>
            <span v-else>{{ row.regularDate?.slice(0, 10) || '—' }}</span>
          </template></el-table-column>
          <el-table-column label="状态" width="100"><template #default="{ row }"><el-tag :type="row.status === EMPLOYEE_STATUS.Active ? 'success' : row.status === EMPLOYEE_STATUS.Terminated ? 'danger' : 'warning'" effect="light">{{ EMPLOYEE_STATUS_LABEL[row.status] }}</el-tag></template></el-table-column>
          <el-table-column label="操作" fixed="right" width="320"><template #default="{ row }">
            <el-button link type="primary" :icon="View" @click="showDetail(row)">详情</el-button>
            <el-button v-if="[EMPLOYEE_STATUS.Probation, EMPLOYEE_STATUS.Active].includes(row.status)" v-permission="'employee:edit'" link type="primary" :icon="Edit" @click="openEdit(row)">编辑</el-button>
            <el-button v-if="row.status === EMPLOYEE_STATUS.Probation" v-permission="'employee:edit'" link type="success" @click="openRegularize(row)">确认转正</el-button>
            <el-button v-if="[EMPLOYEE_STATUS.Probation, EMPLOYEE_STATUS.Active].includes(row.status)" v-permission="'employee:terminate'" link type="danger" @click="terminate(row)">离职</el-button>
            <el-button v-if="row.status === EMPLOYEE_STATUS.Terminated" v-permission="'employee:archive'" link type="warning" @click="archive(row)">归档</el-button>
          </template></el-table-column>
        </el-table>
        <div class="pagination"><el-pagination v-model:current-page="query.pageNumber" v-model:page-size="query.pageSize" :total="total" :page-sizes="[10,20,50,100]" layout="total, sizes, prev, pager, next, jumper" @change="load" /></div>
      </section>
    </template>

    <section v-else-if="mode === 'departments'" class="panel table-panel" v-loading="loading">
      <div class="table-toolbar"><div><h2>部门树</h2><span>共 {{ departments.length }} 个部门</span></div><el-button type="primary" :icon="Plus" @click="openDepartment()">新增部门</el-button></div>
      <el-table :data="departments" row-key="id" :tree-props="{ children: 'children' }" empty-text="暂无部门">
        <el-table-column prop="name" label="部门名称" min-width="180" /><el-table-column prop="code" label="编码" min-width="140" />
        <el-table-column prop="sortOrder" label="排序" width="90" /><el-table-column label="状态" width="100"><template #default="{ row }"><el-tag :type="row.status === 1 ? 'success' : 'info'">{{ row.status === 1 ? '启用' : '停用' }}</el-tag></template></el-table-column>
        <el-table-column prop="remark" label="备注" min-width="180" show-overflow-tooltip /><el-table-column label="操作" width="150"><template #default="{ row }"><el-button link type="primary" :icon="Edit" @click="openDepartment(row)">编辑</el-button><el-button link type="danger" :icon="Delete" @click="deleteDepartment(row)">删除</el-button></template></el-table-column>
      </el-table>
      <el-empty v-if="!loading && !departments.length" description="暂无部门，点击右上角新增" />
    </section>

    <section v-else class="panel table-panel" v-loading="loading">
      <div class="table-toolbar"><div><h2>岗位列表</h2><span>共 {{ positions.length }} 个岗位</span></div><el-button type="primary" :icon="Plus" @click="openPosition()">新增岗位</el-button></div>
      <el-table :data="positions" stripe empty-text="暂无岗位">
        <el-table-column prop="name" label="岗位名称" min-width="170" /><el-table-column prop="code" label="编码" min-width="140" />
        <el-table-column label="所属部门" min-width="160"><template #default="{ row }">{{ departmentMap[row.departmentId] || '—' }}</template></el-table-column>
        <el-table-column label="状态" width="100"><template #default="{ row }"><el-tag :type="row.status === 1 ? 'success' : 'info'">{{ row.status === 1 ? '启用' : '停用' }}</el-tag></template></el-table-column>
        <el-table-column prop="remark" label="备注" min-width="180" show-overflow-tooltip /><el-table-column label="操作" width="150"><template #default="{ row }"><el-button link type="primary" :icon="Edit" @click="openPosition(row)">编辑</el-button><el-button link type="danger" :icon="Delete" @click="deletePosition(row)">删除</el-button></template></el-table-column>
      </el-table>
    </section>

    <el-drawer v-model="editorOpen" :title="editing ? '编辑员工' : '新增员工'" size="620px" destroy-on-close>
      <el-form ref="formRef" :model="form" :rules="rules" label-position="top">
        <div class="form-grid">
          <el-form-item label="员工编号" prop="employeeNo"><el-input v-model="form.employeeNo" /></el-form-item>
          <el-form-item label="姓名" prop="name"><el-input v-model="form.name" /></el-form-item>
          <el-form-item label="性别"><el-select v-model="form.gender"><el-option label="未知" :value="0" /><el-option label="男" :value="1" /><el-option label="女" :value="2" /></el-select></el-form-item>
          <el-form-item label="手机" prop="phone"><el-input v-model="form.phone" /></el-form-item>
          <el-form-item label="邮箱"><el-input v-model="form.email" /></el-form-item>
          <el-form-item label="身份证号"><el-input v-model="form.idCard" placeholder="敏感信息，留空则不修改" /></el-form-item>
          <el-form-item label="部门" prop="departmentId"><el-select v-model="form.departmentId" filterable><el-option v-for="item in departments" :key="item.id" :label="item.name" :value="item.id" /></el-select></el-form-item>
          <el-form-item label="岗位" prop="positionId"><el-select v-model="form.positionId" filterable><el-option v-for="item in filteredPositions" :key="item.id" :label="item.name" :value="item.id" /></el-select></el-form-item>
          <el-form-item label="入职日期" prop="entryDate"><el-date-picker v-model="form.entryDate" value-format="YYYY-MM-DD" type="date" /></el-form-item>
          <el-form-item label="试用期（月）"><el-input-number v-model="form.probationMonths" :min="0" :max="12" /></el-form-item>
          <el-form-item label="转正日期"><el-date-picker v-model="form.regularDate" value-format="YYYY-MM-DD" type="date" /></el-form-item>
          <el-form-item label="月薪"><el-input v-model="form.monthlySalary" placeholder="敏感信息" /></el-form-item>
        </div>
      </el-form>
      <template #footer><el-button @click="editorOpen = false">取消</el-button><el-button type="primary" :loading="saving" @click="saveEmployee">保存</el-button></template>
    </el-drawer>

    <el-drawer v-model="detailOpen" title="员工档案详情" size="560px">
      <div v-if="!detail" v-loading="true" class="drawer-loading" />
      <template v-else>
        <div class="detail-hero"><el-avatar :size="56">{{ detail.employee.name.slice(0, 1) }}</el-avatar><div><h2>{{ detail.employee.name }}</h2><p>{{ detail.employee.employeeNo }} · {{ EMPLOYEE_STATUS_LABEL[detail.employee.status] }}</p></div></div>
        <el-descriptions :column="2" border><el-descriptions-item label="部门">{{ departmentMap[detail.employee.departmentId] }}</el-descriptions-item><el-descriptions-item label="岗位">{{ positionMap[detail.employee.positionId] }}</el-descriptions-item><el-descriptions-item label="手机">{{ detail.employee.phone }}</el-descriptions-item><el-descriptions-item label="邮箱">{{ detail.employee.email || '—' }}</el-descriptions-item><el-descriptions-item label="入职日期">{{ detail.employee.entryDate.slice(0,10) }}</el-descriptions-item><el-descriptions-item label="试用期">{{ detail.employee.probationMonths }} 个月</el-descriptions-item><el-descriptions-item label="身份证">{{ detail.idCard || '—' }}</el-descriptions-item><el-descriptions-item label="月薪">{{ detail.monthlySalary || '—' }}</el-descriptions-item></el-descriptions>
        <el-button v-permission="'employee:sensitive'" class="sensitive-button" @click="showSensitive">查看完整敏感信息</el-button>
      </template>
    </el-drawer>

    <el-dialog v-model="regularizeOpen" title="确认员工转正" width="460px" destroy-on-close>
      <template v-if="regularizingEmployee">
        <el-alert
          :type="isProbationOverdue(regularizingEmployee) ? 'warning' : 'info'"
          :title="isProbationOverdue(regularizingEmployee)
            ? `${regularizingEmployee.name} 已超过计划转正日期 ${overdueDays(regularizingEmployee)} 天`
            : `${regularizingEmployee.name} 的计划转正日期为 ${plannedRegularDate(regularizingEmployee)}`"
          show-icon
          :closable="false"
        />
        <el-form label-position="top" class="regularize-form">
          <el-form-item label="实际转正日期" required>
            <el-date-picker
              v-model="regularizeDate"
              type="date"
              value-format="YYYY-MM-DD"
              :disabled-date="disableRegularizeDate"
              placeholder="选择实际转正日期"
            />
          </el-form-item>
          <p class="form-help">确认后员工状态将由“试用”变为“在职”，并记录实际转正日期。</p>
        </el-form>
      </template>
      <template #footer><el-button @click="regularizeOpen = false">取消</el-button><el-button type="primary" :loading="actionLoading" @click="confirmRegularize">确认转正</el-button></template>
    </el-dialog>

    <el-dialog v-model="departmentDialog" :title="orgEditingId ? '编辑部门' : '新增部门'" width="520px">
      <el-form :model="departmentForm" label-position="top"><div class="form-grid"><el-form-item label="部门名称" required><el-input v-model="departmentForm.name" /></el-form-item><el-form-item label="部门编码" required><el-input v-model="departmentForm.code" /></el-form-item><el-form-item label="上级部门"><el-select v-model="departmentForm.parentId" clearable><el-option v-for="item in departments.filter(i => i.id !== orgEditingId)" :key="item.id" :label="item.name" :value="item.id" /></el-select></el-form-item><el-form-item label="排序"><el-input-number v-model="departmentForm.sortOrder" /></el-form-item><el-form-item label="状态"><el-switch v-model="departmentForm.status" :active-value="1" :inactive-value="0" /></el-form-item></div><el-form-item label="备注"><el-input v-model="departmentForm.remark" type="textarea" /></el-form-item></el-form>
      <template #footer><el-button @click="departmentDialog = false">取消</el-button><el-button type="primary" @click="saveDepartment">保存</el-button></template>
    </el-dialog>
    <el-dialog v-model="positionDialog" :title="orgEditingId ? '编辑岗位' : '新增岗位'" width="520px">
      <el-form :model="positionForm" label-position="top"><div class="form-grid"><el-form-item label="岗位名称" required><el-input v-model="positionForm.name" /></el-form-item><el-form-item label="岗位编码" required><el-input v-model="positionForm.code" /></el-form-item><el-form-item label="所属部门" required><el-select v-model="positionForm.departmentId"><el-option v-for="item in departments" :key="item.id" :label="item.name" :value="item.id" /></el-select></el-form-item><el-form-item label="状态"><el-switch v-model="positionForm.status" :active-value="1" :inactive-value="0" /></el-form-item></div><el-form-item label="备注"><el-input v-model="positionForm.remark" type="textarea" /></el-form-item></el-form>
      <template #footer><el-button @click="positionDialog = false">取消</el-button><el-button type="primary" @click="savePosition">保存</el-button></template>
    </el-dialog>
  </div>
</template>

<style scoped>
.probation-alert { margin-bottom: 14px; }
.regularize-form { margin-top: 20px; }
.regularize-form :deep(.el-date-editor) { width: 100%; }
.form-help { margin: -4px 0 0; color: #728097; font-size: 12px; line-height: 1.7; }
</style>
