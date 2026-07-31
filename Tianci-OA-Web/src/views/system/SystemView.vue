<script setup lang="ts">
import { computed, onActivated, onMounted, reactive, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Delete, Edit, Key, Plus, Refresh, Search, Setting } from '@element-plus/icons-vue'
import { employeeApi, identityApi, organizationApi } from '@/api/modules'
import type { AuditLogDto, DepartmentDto, EmployeeDto, MenuDto, RoleDto, UserDto } from '@/types/contracts'

const route = useRoute()
const mode = computed(() => String(route.meta.mode || 'users'))
const loading = ref(false)
const error = ref('')
const users = ref<UserDto[]>([])
const roles = ref<RoleDto[]>([])
const menus = ref<MenuDto[]>([])
const audits = ref<AuditLogDto[]>([])
const departments = ref<DepartmentDto[]>([])
const employeeOptions = ref<EmployeeDto[]>([])
const employeeLoading = ref(false)
const total = ref(0)
const page = reactive({ keyword: '', module: '', operatorUserId: '', pageNumber: 1, pageSize: 20 })
const editorOpen = ref(false)
const roleAssignOpen = ref(false)
const menuAssignOpen = ref(false)
const editingId = ref('')
const assignedIds = ref<string[]>([])
const userForm = reactive({ username: '', displayName: '', password: '', phone: '', email: '', employeeId: '', departmentId: '', status: 1, roleIds: [] as string[] })
const roleForm = reactive({ name: '', code: '', dataScope: 3, status: 1, remark: '' })
const menuForm = reactive({ parentId: '', type: 2, name: '', routePath: '', component: '', permissionCode: '', icon: '', sortOrder: 0, visible: true, status: 1 })
const title = computed(() => ({ users: '用户管理', roles: '角色管理', menus: '菜单权限', audit: '操作日志' })[mode.value] || '系统管理')
const departmentMap = computed(() => Object.fromEntries(departments.value.map((item) => [item.id, item.name])))

function clean(record: Record<string, unknown>) {
  return Object.fromEntries(Object.entries(record).filter(([, value]) => value !== '' && value !== undefined))
}
async function load() {
  loading.value = true; error.value = ''
  try {
    const [roleData, menuData, departmentData] = await Promise.all([identityApi.roles(), identityApi.menus(), organizationApi.departments()])
    roles.value = roleData; menus.value = menuData; departments.value = departmentData
    if (mode.value === 'users') {
      const result = await identityApi.users(clean({ pageNumber: page.pageNumber, pageSize: page.pageSize, keyword: page.keyword }))
      users.value = result.items; total.value = result.total
    } else if (mode.value === 'audit') {
      const result = await identityApi.auditLogs(clean({ module: page.module, operatorUserId: page.operatorUserId, pageNumber: page.pageNumber, pageSize: page.pageSize }))
      audits.value = result.items; total.value = result.total
    }
  } catch (cause) { error.value = cause instanceof Error ? cause.message : '系统数据加载失败' }
  finally { loading.value = false }
}
async function searchEmployees(keyword = '') {
  employeeLoading.value = true
  try {
    const result = await employeeApi.list({ keyword: keyword.trim(), pageNumber: 1, pageSize: 50 })
    employeeOptions.value = result.items.filter((item) => item.status === 1 || item.status === 2)
  } finally {
    employeeLoading.value = false
  }
}
function selectEmployee(employeeId: string) {
  const employee = employeeOptions.value.find((item) => item.id === employeeId)
  if (!employee) return
  userForm.displayName ||= employee.name
  userForm.phone ||= employee.phone
  userForm.email ||= employee.email || ''
  userForm.departmentId = employee.departmentId
}
function resetQuery() { Object.assign(page, { keyword: '', module: '', operatorUserId: '', pageNumber: 1 }); load() }
async function openUser(row?: UserDto) {
  editingId.value = row?.id || ''
  Object.assign(userForm, row ? {
    username: row.username, displayName: row.displayName, password: '', phone: row.phone || '', email: row.email || '',
    employeeId: row.employeeId || '', departmentId: row.departmentId || '', status: row.status, roleIds: [],
  } : { username: '', displayName: '', password: '', phone: '', email: '', employeeId: '', departmentId: '', status: 1, roleIds: [] })
  editorOpen.value = true
  await searchEmployees(row?.displayName || '')
}
function openRole(row?: RoleDto) {
  editingId.value = row?.id || ''
  Object.assign(roleForm, row ? { name: row.name, code: row.code, dataScope: row.dataScope, status: row.status, remark: row.remark || '' } : { name: '', code: '', dataScope: 3, status: 1, remark: '' })
  editorOpen.value = true
}
function openMenu(row?: MenuDto) {
  editingId.value = row?.id || ''
  Object.assign(menuForm, row ? {
    parentId: row.parentId || '', type: row.type, name: row.name, routePath: row.routePath || '', component: row.component || '',
    permissionCode: row.permissionCode || '', icon: row.icon || '', sortOrder: row.sortOrder, visible: row.visible, status: row.status,
  } : { parentId: '', type: 2, name: '', routePath: '', component: '', permissionCode: '', icon: '', sortOrder: 0, visible: true, status: 1 })
  editorOpen.value = true
}
async function save() {
  if (mode.value === 'users') {
    if (!userForm.displayName || (!editingId.value && (!userForm.username || userForm.password.length < 8))) { ElMessage.warning('请填写完整用户信息，初始密码至少 8 位'); return }
    if (editingId.value) {
      const { username: _username, password: _password, roleIds: _roleIds, ...payload } = userForm
      void _username; void _password; void _roleIds
      await identityApi.updateUser(editingId.value, clean(payload))
    } else await identityApi.createUser(clean(userForm))
  } else if (mode.value === 'roles') {
    if (!roleForm.name || !/^[A-Z][A-Z0-9_]{1,63}$/.test(roleForm.code)) { ElMessage.warning('角色编码需为大写字母开头的字母数字下划线'); return }
    if (editingId.value) await identityApi.updateRole(editingId.value, roleForm)
    else await identityApi.createRole(roleForm)
  } else {
    if (!menuForm.name || !menuForm.type) { ElMessage.warning('请填写菜单名称和类型'); return }
    if (editingId.value) await identityApi.updateMenu(editingId.value, clean(menuForm))
    else await identityApi.createMenu(clean(menuForm))
  }
  ElMessage.success('保存成功'); editorOpen.value = false; await load()
}
async function remove(id: string, name: string) {
  await ElMessageBox.confirm(`确认删除“${name}”？被业务引用时后端将拒绝此操作。`, '删除确认', { type: 'warning' })
  if (mode.value === 'users') await identityApi.deleteUser(id)
  else if (mode.value === 'roles') await identityApi.deleteRole(id)
  else await identityApi.deleteMenu(id)
  ElMessage.success('删除成功'); await load()
}
async function resetPassword(row: UserDto) {
  const result = await ElMessageBox.prompt(`为“${row.displayName}”设置新密码`, '重置密码', {
    inputType: 'password', inputValidator: (value) => (value?.length || 0) >= 8 || '密码至少 8 位',
  })
  await identityApi.resetPassword(row.id, result.value); ElMessage.success('密码已重置，旧会话将失效')
}
function openAssignRoles(row: UserDto) { editingId.value = row.id; assignedIds.value = []; roleAssignOpen.value = true }
async function assignRoles() {
  await identityApi.assignRoles(editingId.value, assignedIds.value)
  ElMessage.success('用户角色已更新'); roleAssignOpen.value = false
}
function openAssignMenus(row: RoleDto) { editingId.value = row.id; assignedIds.value = []; menuAssignOpen.value = true }
async function assignMenus() {
  await identityApi.assignMenus(editingId.value, assignedIds.value)
  ElMessage.success('角色菜单权限已更新'); menuAssignOpen.value = false
}
watch(mode, () => { page.pageNumber = 1; load() })
onMounted(load)
onActivated(load)
</script>

<template>
  <div class="page">
    <div class="page-heading compact"><div><p class="eyebrow">SYSTEM</p><h1>{{ title }}</h1><p>配置账号、角色、菜单权限并追踪关键操作。</p></div><el-button :icon="Refresh" @click="load">刷新</el-button></div>
    <el-alert v-if="error" :title="error" type="error" show-icon :closable="false"><el-button link type="primary" @click="load">重新加载</el-button></el-alert>
    <section v-if="mode === 'users' || mode === 'audit'" class="panel search-panel">
      <el-form inline>
        <template v-if="mode === 'users'"><el-form-item label="关键字"><el-input v-model="page.keyword" clearable placeholder="用户名 / 姓名" :prefix-icon="Search" @keyup.enter="page.pageNumber = 1; load()" /></el-form-item></template>
        <template v-else><el-form-item label="模块"><el-input v-model="page.module" clearable placeholder="例如 employees" /></el-form-item><el-form-item label="操作人 ID"><el-input v-model="page.operatorUserId" clearable /></el-form-item></template>
        <el-form-item><el-button type="primary" :icon="Search" @click="page.pageNumber = 1; load()">查询</el-button><el-button @click="resetQuery">重置</el-button></el-form-item>
      </el-form>
    </section>

    <section class="panel table-panel" v-loading="loading">
      <div class="table-toolbar"><div><h2>{{ title }}列表</h2><span v-if="mode === 'users' || mode === 'audit'">共 {{ total }} 条</span><span v-else>共 {{ mode === 'roles' ? roles.length : menus.length }} 条</span></div>
        <el-button v-if="mode !== 'audit'" type="primary" :icon="Plus" @click="mode === 'users' ? openUser() : mode === 'roles' ? openRole() : openMenu()">新增{{ mode === 'users' ? '用户' : mode === 'roles' ? '角色' : '菜单' }}</el-button>
      </div>
      <el-table v-if="mode === 'users'" :data="users" stripe empty-text="暂无用户">
        <el-table-column prop="username" label="用户名" min-width="130" /><el-table-column prop="displayName" label="姓名" min-width="120" />
        <el-table-column label="联系方式" min-width="190"><template #default="{ row }">{{ row.phone || '—' }}<small class="table-sub">{{ row.email || '—' }}</small></template></el-table-column>
        <el-table-column label="所属部门" min-width="150"><template #default="{ row }">{{ departmentMap[row.departmentId] || '—' }}</template></el-table-column>
        <el-table-column label="状态" width="100"><template #default="{ row }"><el-tag :type="row.status === 1 ? 'success' : row.status === 2 ? 'warning' : 'info'">{{ row.status === 1 ? '启用' : row.status === 2 ? '锁定' : '停用' }}</el-tag></template></el-table-column>
        <el-table-column label="操作" fixed="right" width="300"><template #default="{ row }"><el-button link type="primary" :icon="Edit" @click="openUser(row)">编辑</el-button><el-button link type="primary" :icon="Setting" @click="openAssignRoles(row)">分配角色</el-button><el-button link type="warning" :icon="Key" @click="resetPassword(row)">重置密码</el-button><el-button link type="danger" :icon="Delete" @click="remove(row.id, row.displayName)">删除</el-button></template></el-table-column>
      </el-table>
      <el-table v-else-if="mode === 'roles'" :data="roles" stripe empty-text="暂无角色">
        <el-table-column prop="name" label="角色名称" min-width="150" /><el-table-column prop="code" label="编码" min-width="160" />
        <el-table-column label="数据范围" min-width="150"><template #default="{ row }">{{ { 1: '全部数据', 2: '本部门及下级', 3: '仅本人' }[row.dataScope as 1|2|3] }}</template></el-table-column>
        <el-table-column label="状态" width="100"><template #default="{ row }"><el-tag :type="row.status === 1 ? 'success' : 'info'">{{ row.status === 1 ? '启用' : '停用' }}</el-tag></template></el-table-column>
        <el-table-column label="操作" width="270"><template #default="{ row }"><el-button link type="primary" :icon="Edit" @click="openRole(row)">编辑</el-button><el-button link type="primary" :icon="Setting" @click="openAssignMenus(row)">菜单授权</el-button><el-button v-if="!row.isSystem" link type="danger" :icon="Delete" @click="remove(row.id, row.name)">删除</el-button></template></el-table-column>
      </el-table>
      <el-table v-else-if="mode === 'menus'" :data="menus" row-key="id" stripe empty-text="暂无菜单">
        <el-table-column prop="name" label="名称" min-width="150" /><el-table-column label="类型" width="100"><template #default="{ row }">{{ { 1:'目录', 2:'菜单', 3:'按钮' }[row.type as 1|2|3] }}</template></el-table-column>
        <el-table-column prop="routePath" label="路由" min-width="180" show-overflow-tooltip /><el-table-column prop="permissionCode" label="权限标识" min-width="190" show-overflow-tooltip />
        <el-table-column prop="sortOrder" label="排序" width="80" /><el-table-column label="显示" width="80"><template #default="{ row }">{{ row.visible ? '是' : '否' }}</template></el-table-column>
        <el-table-column label="操作" width="150"><template #default="{ row }"><el-button link type="primary" :icon="Edit" @click="openMenu(row)">编辑</el-button><el-button link type="danger" :icon="Delete" @click="remove(row.id, row.name)">删除</el-button></template></el-table-column>
      </el-table>
      <el-table v-else :data="audits" stripe empty-text="暂无操作日志">
        <el-table-column label="时间" width="170"><template #default="{ row }">{{ new Date(row.createdAt).toLocaleString() }}</template></el-table-column><el-table-column prop="operatorName" label="操作人" width="120" />
        <el-table-column prop="module" label="模块" width="120" /><el-table-column label="操作" min-width="210"><template #default="{ row }"><el-tag size="small" effect="plain">{{ row.requestMethod || row.action }}</el-tag> {{ row.requestPath }}</template></el-table-column>
        <el-table-column label="结果" width="100"><template #default="{ row }"><el-tag :type="row.succeeded ? 'success' : 'danger'">{{ row.succeeded ? '成功' : '失败' }}</el-tag></template></el-table-column>
        <el-table-column prop="durationMs" label="耗时(ms)" width="100" /><el-table-column prop="traceId" label="Trace ID" min-width="220" show-overflow-tooltip />
      </el-table>
      <div v-if="mode === 'users' || mode === 'audit'" class="pagination"><el-pagination v-model:current-page="page.pageNumber" v-model:page-size="page.pageSize" :total="total" :page-sizes="[10,20,50,100]" layout="total, sizes, prev, pager, next, jumper" @change="load" /></div>
    </section>

    <el-drawer v-model="editorOpen" :title="editingId ? '编辑' + title.slice(0,2) : '新增' + title.slice(0,2)" size="560px">
      <el-form v-if="mode === 'users'" :model="userForm" label-position="top">
        <el-alert
          title="面试官需要先关联一名在职员工，并保持账号为启用状态。"
          type="info"
          show-icon
          :closable="false"
          class="editor-tip"
        />
        <div class="form-grid">
          <el-form-item label="关联员工">
            <el-select
              v-model="userForm.employeeId"
              filterable
              remote
              clearable
              reserve-keyword
              :remote-method="searchEmployees"
              :loading="employeeLoading"
              placeholder="按姓名或工号搜索在职员工"
              @change="selectEmployee"
            >
              <el-option
                v-for="item in employeeOptions"
                :key="item.id"
                :label="`${item.name}（${item.employeeNo}）`"
                :value="item.id"
              >
                <span>{{ item.name }}（{{ item.employeeNo }}）</span>
                <small class="employee-department">{{ departmentMap[item.departmentId] || '未分配部门' }}</small>
              </el-option>
            </el-select>
          </el-form-item>
          <el-form-item label="用户名" required><el-input v-model="userForm.username" :disabled="Boolean(editingId)" /></el-form-item>
          <el-form-item label="姓名" required><el-input v-model="userForm.displayName" /></el-form-item>
          <el-form-item v-if="!editingId" label="初始密码" required><el-input v-model="userForm.password" type="password" show-password /></el-form-item>
          <el-form-item label="手机"><el-input v-model="userForm.phone" /></el-form-item>
          <el-form-item label="邮箱"><el-input v-model="userForm.email" /></el-form-item>
          <el-form-item label="所属部门"><el-select v-model="userForm.departmentId" clearable :disabled="Boolean(userForm.employeeId)"><el-option v-for="item in departments" :key="item.id" :label="item.name" :value="item.id" /></el-select></el-form-item>
          <el-form-item v-if="editingId" label="状态"><el-select v-model="userForm.status"><el-option label="停用" :value="0" /><el-option label="启用" :value="1" /><el-option label="锁定" :value="2" /></el-select></el-form-item>
          <el-form-item v-else label="角色"><el-select v-model="userForm.roleIds" multiple><el-option v-for="item in roles" :key="item.id" :label="item.name" :value="item.id" /></el-select></el-form-item>
        </div>
      </el-form>
      <el-form v-else-if="mode === 'roles'" :model="roleForm" label-position="top"><el-form-item label="角色名称" required><el-input v-model="roleForm.name" /></el-form-item><el-form-item label="角色编码" required><el-input v-model="roleForm.code" placeholder="例如 HR_MANAGER" /></el-form-item><el-form-item label="数据范围"><el-select v-model="roleForm.dataScope"><el-option label="全部数据" :value="1" /><el-option label="本部门及下级" :value="2" /><el-option label="仅本人" :value="3" /></el-select></el-form-item><el-form-item label="状态"><el-switch v-model="roleForm.status" :active-value="1" :inactive-value="0" /></el-form-item><el-form-item label="备注"><el-input v-model="roleForm.remark" type="textarea" /></el-form-item></el-form>
      <el-form v-else :model="menuForm" label-position="top"><div class="form-grid"><el-form-item label="名称" required><el-input v-model="menuForm.name" /></el-form-item><el-form-item label="类型" required><el-select v-model="menuForm.type"><el-option label="目录" :value="1" /><el-option label="菜单" :value="2" /><el-option label="按钮" :value="3" /></el-select></el-form-item><el-form-item label="上级"><el-select v-model="menuForm.parentId" clearable><el-option v-for="item in menus.filter(i => i.id !== editingId && i.type !== 3)" :key="item.id" :label="item.name" :value="item.id" /></el-select></el-form-item><el-form-item label="排序"><el-input-number v-model="menuForm.sortOrder" /></el-form-item><el-form-item label="路由"><el-input v-model="menuForm.routePath" /></el-form-item><el-form-item label="组件"><el-input v-model="menuForm.component" /></el-form-item><el-form-item label="权限标识"><el-input v-model="menuForm.permissionCode" /></el-form-item><el-form-item label="图标"><el-input v-model="menuForm.icon" /></el-form-item><el-form-item label="可见"><el-switch v-model="menuForm.visible" /></el-form-item><el-form-item label="状态"><el-switch v-model="menuForm.status" :active-value="1" :inactive-value="0" /></el-form-item></div></el-form>
      <template #footer><el-button @click="editorOpen = false">取消</el-button><el-button type="primary" @click="save">保存</el-button></template>
    </el-drawer>

    <el-dialog v-model="roleAssignOpen" title="分配角色" width="480px"><el-alert title="当前后端未提供用户已绑定角色查询，本次保存将以所选角色替换原绑定。" type="warning" show-icon :closable="false" /><el-checkbox-group v-model="assignedIds" class="permission-checks"><el-checkbox v-for="item in roles" :key="item.id" :value="item.id">{{ item.name }}</el-checkbox></el-checkbox-group><template #footer><el-button @click="roleAssignOpen = false">取消</el-button><el-button type="primary" @click="assignRoles">保存</el-button></template></el-dialog>
    <el-dialog v-model="menuAssignOpen" title="菜单与操作授权" width="560px"><el-alert title="当前后端未提供角色已授权菜单查询，本次保存将以所选菜单替换原授权。" type="warning" show-icon :closable="false" /><el-checkbox-group v-model="assignedIds" class="permission-checks"><el-checkbox v-for="item in menus" :key="item.id" :value="item.id">{{ item.name }}<small>{{ item.permissionCode }}</small></el-checkbox></el-checkbox-group><template #footer><el-button @click="menuAssignOpen = false">取消</el-button><el-button type="primary" @click="assignMenus">保存授权</el-button></template></el-dialog>
  </div>
</template>

<style scoped>
.editor-tip {
  margin-bottom: 18px;
}

.employee-department {
  float: right;
  margin-left: 24px;
  color: var(--el-text-color-secondary);
}
</style>
