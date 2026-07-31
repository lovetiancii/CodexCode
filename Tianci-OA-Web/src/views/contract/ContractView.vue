<script setup lang="ts">
import { computed, onActivated, onMounted, reactive, ref } from 'vue'
import { useRoute } from 'vue-router'
import { ElMessage, ElMessageBox, type UploadRequestOptions } from 'element-plus'
import { Download, Edit, Plus, Refresh, Search, View } from '@element-plus/icons-vue'
import { contractApi, employeeApi, fileApi } from '@/api/modules'
import { http } from '@/api/http'
import {
  CONTRACT_STATUS, CONTRACT_STATUS_LABEL, CONTRACT_TYPE, CONTRACT_TYPE_LABEL, type ContractDto, type EmployeeDto, type FileDto,
} from '@/types/contracts'

const route = useRoute()
const loading = ref(false)
const error = ref('')
const rows = ref<ContractDto[]>([])
const employees = ref<EmployeeDto[]>([])
const total = ref(0)
const query = reactive({ keyword: '', employeeId: '', status: undefined as number | undefined, pageNumber: 1, pageSize: 20 })
const editorOpen = ref(false)
const detailOpen = ref(false)
const saving = ref(false)
const editing = ref<ContractDto | null>(null)
const renewing = ref<ContractDto | null>(null)
const detail = ref<ContractDto | null>(null)
const files = ref<FileDto[]>([])
const uploadProgress = ref(0)
const form = reactive({
  contractNo: '', employeeId: '', contractType: CONTRACT_TYPE.Labor as number, startDate: '', endDate: '',
  reminderDays: 30, attachmentFileId: '', previousContractId: '', remark: '',
})
const employeeMap = computed(() => Object.fromEntries(employees.value.map((item) => [item.id, item.name])))
const expiringOnly = computed(() => route.query.expiring === 'true')

function clean(record: Record<string, unknown>) {
  return Object.fromEntries(Object.entries(record).filter(([, value]) => value !== '' && value !== undefined))
}
async function load() {
  loading.value = true; error.value = ''
  try {
    const [contracts, employeeResult] = await Promise.all([
      expiringOnly.value
        ? contractApi.expiring(30).then((items) => ({ items, total: items.length }))
        : contractApi.list(clean(query)),
      employeeApi.list({ pageNumber: 1, pageSize: 100 }),
    ])
    rows.value = contracts.items; total.value = contracts.total; employees.value = employeeResult.items
  } catch (cause) { error.value = cause instanceof Error ? cause.message : '合同数据加载失败' }
  finally { loading.value = false }
}
function reset() { Object.assign(query, { keyword: '', employeeId: '', status: undefined, pageNumber: 1 }); load() }
function resetForm() {
  const today = new Date()
  const nextYear = new Date(today); nextYear.setFullYear(today.getFullYear() + 1)
  Object.assign(form, {
    contractNo: `HT-${today.getFullYear()}-`, employeeId: '', contractType: CONTRACT_TYPE.Labor,
    startDate: today.toISOString().slice(0, 10), endDate: nextYear.toISOString().slice(0, 10),
    reminderDays: 30, attachmentFileId: '', previousContractId: '', remark: '',
  })
}
function openCreate() { editing.value = null; renewing.value = null; resetForm(); editorOpen.value = true }
function openEdit(row: ContractDto) {
  editing.value = row; renewing.value = null
  Object.assign(form, {
    contractNo: row.contractNo, employeeId: row.employeeId, contractType: row.contractType,
    startDate: row.startDate.slice(0, 10), endDate: row.endDate.slice(0, 10), reminderDays: row.reminderDays,
    attachmentFileId: row.attachmentFileId || '', previousContractId: '', remark: row.remark || '',
  })
  editorOpen.value = true
}
function openRenew(row: ContractDto) {
  editing.value = null; renewing.value = row; resetForm()
  form.employeeId = row.employeeId; form.contractType = row.contractType; form.previousContractId = row.id
  editorOpen.value = true
}
async function save() {
  if (!form.contractNo || !form.employeeId || !form.startDate || !form.endDate) { ElMessage.warning('请完整填写合同必填项'); return }
  if (new Date(form.startDate) > new Date(form.endDate)) { ElMessage.warning('开始日期不得晚于结束日期'); return }
  saving.value = true
  try {
    if (editing.value) await contractApi.update(editing.value.id, editing.value.version, clean(form))
    else if (renewing.value) await contractApi.renew(renewing.value.id, renewing.value.version, clean(form))
    else await contractApi.create(clean(form))
    ElMessage.success(renewing.value ? '续签合同草稿已创建' : '合同已保存为草稿')
    editorOpen.value = false; await load()
  } finally { saving.value = false }
}
async function showDetail(row: ContractDto) {
  detailOpen.value = true; detail.value = null; files.value = []
  try {
    detail.value = await contractApi.get(row.id)
    files.value = await fileApi.list('contract', row.id)
  } catch { detailOpen.value = false }
}
async function action(row: ContractDto, type: 'activate' | 'terminate' | 'archive') {
  let reason = ''
  const labels = { activate: '生效', terminate: '终止', archive: '归档' }
  if (type === 'terminate') {
    const result = await ElMessageBox.prompt('终止原因将写入合同备注', '终止合同', { inputValidator: (value) => Boolean(value?.trim()) || '终止原因不能为空' })
    reason = result.value
  } else await ElMessageBox.confirm(`确认${labels[type]}合同“${row.contractNo}”？`, `${labels[type]}确认`, { type: type === 'activate' ? 'info' : 'warning' })
  await contractApi.action(row.id, type, row.version, reason)
  ElMessage.success(`合同已${labels[type]}`); await load()
  if (detailOpen.value) detailOpen.value = false
}
async function upload(options: UploadRequestOptions) {
  if (!detail.value) return
  const formData = new FormData()
  formData.append('businessType', 'contract'); formData.append('businessId', detail.value.id)
  formData.append('category', 'contract-scan'); formData.append('file', options.file)
  uploadProgress.value = 0
  const uploaded = await fileApi.upload(formData, (value) => { uploadProgress.value = value })
  files.value.push(uploaded)
  if (!detail.value.attachmentFileId) {
    const payload = {
      contractNo: detail.value.contractNo, employeeId: detail.value.employeeId, contractType: detail.value.contractType,
      startDate: detail.value.startDate, endDate: detail.value.endDate, reminderDays: detail.value.reminderDays,
      attachmentFileId: uploaded.id, remark: detail.value.remark,
    }
    detail.value = await contractApi.update(detail.value.id, detail.value.version, payload)
  }
  options.onSuccess(uploaded); ElMessage.success('合同附件已上传并关联')
  await load()
}
async function download(file: FileDto) {
  const response = await http.get(`/files/${file.id}/download`, { responseType: 'blob' })
  const url = URL.createObjectURL(response.data as Blob)
  const anchor = document.createElement('a'); anchor.href = url; anchor.download = file.originalName; anchor.click()
  URL.revokeObjectURL(url)
}
async function removeFile(file: FileDto) {
  await ElMessageBox.confirm(`确认删除附件“${file.originalName}”？`, '删除附件', { type: 'warning' })
  await fileApi.remove(file.id); files.value = files.value.filter((item) => item.id !== file.id); ElMessage.success('附件已删除')
}
onMounted(() => { load(); if (route.query.action === 'create') openCreate() })
onActivated(load)
</script>

<template>
  <div class="page">
    <div class="page-heading compact"><div><p class="eyebrow">CONTRACTS</p><h1>合同档案</h1><p>管理合同草稿、生效、续签、终止与归档。</p></div><el-button :icon="Refresh" @click="load">刷新</el-button></div>
    <el-alert v-if="error" :title="error" type="error" show-icon :closable="false"><el-button link type="primary" @click="load">重新加载</el-button></el-alert>
    <section class="panel search-panel">
      <el-form inline>
        <el-form-item label="关键字"><el-input v-model="query.keyword" clearable placeholder="合同编号" :prefix-icon="Search" @keyup.enter="query.pageNumber = 1; load()" /></el-form-item>
        <el-form-item label="员工"><el-select v-model="query.employeeId" clearable filterable placeholder="全部员工"><el-option v-for="item in employees" :key="item.id" :label="`${item.name}（${item.employeeNo}）`" :value="item.id" /></el-select></el-form-item>
        <el-form-item label="状态"><el-select v-model="query.status" clearable placeholder="全部状态"><el-option v-for="(label, value) in CONTRACT_STATUS_LABEL" :key="value" :label="label" :value="Number(value)" /></el-select></el-form-item>
        <el-form-item><el-button type="primary" :icon="Search" @click="query.pageNumber = 1; load()">查询</el-button><el-button @click="reset">重置</el-button></el-form-item>
      </el-form>
    </section>
    <section class="panel table-panel" v-loading="loading">
      <div class="table-toolbar"><div><h2>合同列表</h2><span>共 {{ total }} 份合同</span></div><el-button v-permission="'contract:manage'" type="primary" :icon="Plus" @click="openCreate">新增合同</el-button></div>
      <el-table :data="rows" stripe empty-text="暂无合同">
        <el-table-column label="合同编号" min-width="170"><template #default="{ row }"><el-button link type="primary" @click="showDetail(row)">{{ row.contractNo }}</el-button></template></el-table-column>
        <el-table-column label="员工" min-width="130"><template #default="{ row }">{{ employeeMap[row.employeeId] || row.employeeId }}</template></el-table-column>
        <el-table-column label="类型" width="120"><template #default="{ row }">{{ CONTRACT_TYPE_LABEL[row.contractType] }}</template></el-table-column>
        <el-table-column label="合同期限" min-width="210"><template #default="{ row }">{{ row.startDate.slice(0,10) }} 至 {{ row.endDate.slice(0,10) }}</template></el-table-column>
        <el-table-column label="到期提醒" width="120"><template #default="{ row }"><el-tag v-if="row.isExpired" type="danger">已到期</el-tag><el-tag v-else-if="row.isExpiringSoon" type="warning">即将到期</el-tag><span v-else>{{ row.reminderDays }} 天</span></template></el-table-column>
        <el-table-column label="状态" width="110"><template #default="{ row }"><el-tag :type="row.status === 2 ? 'success' : row.status === 1 ? 'info' : 'warning'">{{ CONTRACT_STATUS_LABEL[row.status] }}</el-tag></template></el-table-column>
        <el-table-column label="操作" fixed="right" width="290"><template #default="{ row }">
          <el-button link type="primary" :icon="View" @click="showDetail(row)">详情</el-button>
          <el-button v-if="row.status === CONTRACT_STATUS.Draft" v-permission="'contract:manage'" link type="primary" :icon="Edit" @click="openEdit(row)">编辑</el-button>
          <el-button v-if="row.status === CONTRACT_STATUS.Draft" v-permission="'contract:manage'" link type="success" @click="action(row, 'activate')">生效</el-button>
          <el-button v-if="[CONTRACT_STATUS.Active, CONTRACT_STATUS.Terminated].includes(row.status)" v-permission="'contract:manage'" link type="primary" @click="openRenew(row)">续签</el-button>
          <el-button v-if="row.status === CONTRACT_STATUS.Active" v-permission="'contract:manage'" link type="danger" @click="action(row, 'terminate')">终止</el-button>
          <el-button v-if="[CONTRACT_STATUS.Terminated, CONTRACT_STATUS.Renewed].includes(row.status)" v-permission="'contract:manage'" link type="warning" @click="action(row, 'archive')">归档</el-button>
        </template></el-table-column>
      </el-table>
      <div class="pagination"><el-pagination v-model:current-page="query.pageNumber" v-model:page-size="query.pageSize" :total="total" :page-sizes="[10,20,50,100]" layout="total, sizes, prev, pager, next, jumper" @change="load" /></div>
    </section>

    <el-drawer v-model="editorOpen" :title="renewing ? '续签合同' : editing ? '编辑合同' : '新增合同'" size="600px">
      <el-alert title="合同先保存为草稿，保存后可在详情中上传附件，再执行生效。" type="info" show-icon :closable="false" />
      <el-form :model="form" label-position="top" class="drawer-form"><div class="form-grid">
        <el-form-item label="合同编号" required><el-input v-model="form.contractNo" /></el-form-item>
        <el-form-item label="员工" required><el-select v-model="form.employeeId" filterable><el-option v-for="item in employees" :key="item.id" :label="`${item.name}（${item.employeeNo}）`" :value="item.id" /></el-select></el-form-item>
        <el-form-item label="合同类型" required><el-select v-model="form.contractType"><el-option v-for="(label, value) in CONTRACT_TYPE_LABEL" :key="value" :label="label" :value="Number(value)" /></el-select></el-form-item>
        <el-form-item label="提醒天数"><el-input-number v-model="form.reminderDays" :min="0" :max="365" /></el-form-item>
        <el-form-item label="开始日期" required><el-date-picker v-model="form.startDate" value-format="YYYY-MM-DD" type="date" /></el-form-item>
        <el-form-item label="结束日期" required><el-date-picker v-model="form.endDate" value-format="YYYY-MM-DD" type="date" /></el-form-item>
      </div><el-form-item label="备注"><el-input v-model="form.remark" type="textarea" :rows="4" maxlength="1000" show-word-limit /></el-form-item></el-form>
      <template #footer><el-button @click="editorOpen = false">取消</el-button><el-button type="primary" :loading="saving" @click="save">保存草稿</el-button></template>
    </el-drawer>

    <el-drawer v-model="detailOpen" title="合同详情" size="620px">
      <div v-if="!detail" v-loading="true" class="drawer-loading" />
      <template v-else>
        <div class="detail-hero"><div class="document-avatar">合</div><div><h2>{{ detail.contractNo }}</h2><p>{{ employeeMap[detail.employeeId] }} · {{ CONTRACT_STATUS_LABEL[detail.status] }}</p></div></div>
        <el-descriptions :column="2" border><el-descriptions-item label="合同类型">{{ CONTRACT_TYPE_LABEL[detail.contractType] }}</el-descriptions-item><el-descriptions-item label="提醒天数">{{ detail.reminderDays }} 天</el-descriptions-item><el-descriptions-item label="开始日期">{{ detail.startDate.slice(0,10) }}</el-descriptions-item><el-descriptions-item label="结束日期">{{ detail.endDate.slice(0,10) }}</el-descriptions-item><el-descriptions-item label="备注" :span="2">{{ detail.remark || '—' }}</el-descriptions-item></el-descriptions>
        <div class="attachment-section"><div class="panel-heading"><div><h2>合同附件</h2><p>支持 PDF、DOC、DOCX、JPG、PNG，最大 20 MB</p></div></div>
          <el-upload v-if="detail.status === CONTRACT_STATUS.Draft" drag :show-file-list="false" :http-request="upload" accept=".pdf,.doc,.docx,.jpg,.jpeg,.png"><el-icon class="el-icon--upload"><UploadFilled /></el-icon><div class="el-upload__text">拖放文件或<em>点击上传</em></div></el-upload>
          <el-progress v-if="uploadProgress > 0 && uploadProgress < 100" :percentage="uploadProgress" />
          <div v-if="files.length" class="file-list"><div v-for="file in files" :key="file.id"><span><Document />{{ file.originalName }}</span><div><el-button link type="primary" :icon="Download" @click="download(file)">下载</el-button><el-button v-if="detail.status === CONTRACT_STATUS.Draft" v-permission="'file:delete'" link type="danger" @click="removeFile(file)">删除</el-button></div></div></div>
          <el-empty v-else description="暂无附件" :image-size="64" />
        </div>
      </template>
    </el-drawer>
  </div>
</template>
