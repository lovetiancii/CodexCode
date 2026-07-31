import { request } from './http'
import type {
  AuditLogDto, ContractDto, DepartmentDto, EmployeeDetailDto, EmployeeDto, FileDto, InterviewDto,
  LoginResponse, MenuDto, PagedResult, PositionDto, ResumeDto, RoleDto, UserDto,
} from '@/types/contracts'

export const authApi = {
  login: (data: { username: string; password: string }) => request.post<LoginResponse>('/auth/login', data),
}

export const employeeApi = {
  list: (params: Record<string, unknown>) => request.get<PagedResult<EmployeeDto>>('/employees', { params }),
  get: (id: string, includeSensitive = false) => request.get<EmployeeDetailDto>(`/employees/${id}`, { params: { includeSensitive } }),
  create: (data: Record<string, unknown>) => request.post<EmployeeDto>('/employees', data),
  update: (id: string, version: number, data: Record<string, unknown>) => request.put<EmployeeDto>(`/employees/${id}`, data, { params: { version } }),
  terminate: (id: string, data: { terminationDate: string; reason: string; version: number }) => request.post<void>(`/employees/${id}/terminate`, data),
  archive: (id: string, version: number) => request.post<void>(`/employees/${id}/archive`, undefined, { params: { version } }),
}

export const resumeApi = {
  list: (params: Record<string, unknown>) => request.get<PagedResult<ResumeDto>>('/resumes', { params }),
  get: (id: string) => request.get<ResumeDto>(`/resumes/${id}`),
  create: (data: Record<string, unknown>) => request.post<ResumeDto>('/resumes', data),
  update: (id: string, version: number, data: Record<string, unknown>) => request.put<ResumeDto>(`/resumes/${id}`, data, { params: { version } }),
  changeStatus: (id: string, data: { targetStatus: number; reason?: string; version: number }) => request.post<void>(`/resumes/${id}/status`, data),
  confirmOffer: (id: string, data: Record<string, unknown>) => request.post<void>(`/resumes/${id}/confirm-offer`, data),
  confirmEntry: (id: string, data: Record<string, unknown>) => request.post<{ employeeId: string }>(`/resumes/${id}/confirm-entry`, data),
}

export const interviewApi = {
  list: (resumeId: string) => request.get<InterviewDto[]>(`/resumes/${resumeId}/interviews`),
  schedule: (resumeId: string, data: Record<string, unknown>) => request.post<InterviewDto>(`/resumes/${resumeId}/interviews`, data),
  complete: (resumeId: string, interviewId: string, data: Record<string, unknown>) => request.post<InterviewDto>(`/resumes/${resumeId}/interviews/${interviewId}/complete`, data),
}

export const contractApi = {
  list: (params: Record<string, unknown>) => request.get<PagedResult<ContractDto>>('/contracts', { params }),
  get: (id: string) => request.get<ContractDto>(`/contracts/${id}`),
  expiring: (withinDays?: number) => request.get<ContractDto[]>('/contracts/expiring', { params: { withinDays } }),
  create: (data: Record<string, unknown>) => request.post<ContractDto>('/contracts', data),
  update: (id: string, version: number, data: Record<string, unknown>) => request.put<ContractDto>(`/contracts/${id}`, data, { params: { version } }),
  action: (id: string, action: 'activate' | 'terminate' | 'archive', version: number, reason?: string) => request.post<void>(`/contracts/${id}/${action}`, { version, reason }),
  renew: (id: string, version: number, data: Record<string, unknown>) => request.post<ContractDto>(`/contracts/${id}/renew`, data, { params: { version } }),
}

export const organizationApi = {
  departments: () => request.get<DepartmentDto[]>('/departments'),
  createDepartment: (data: Record<string, unknown>) => request.post<DepartmentDto>('/departments', data),
  updateDepartment: (id: string, data: Record<string, unknown>) => request.put<DepartmentDto>(`/departments/${id}`, data),
  deleteDepartment: (id: string) => request.delete<void>(`/departments/${id}`),
  positions: (departmentId?: string) => request.get<PositionDto[]>('/positions', { params: { departmentId } }),
  createPosition: (data: Record<string, unknown>) => request.post<PositionDto>('/positions', data),
  updatePosition: (id: string, data: Record<string, unknown>) => request.put<PositionDto>(`/positions/${id}`, data),
  deletePosition: (id: string) => request.delete<void>(`/positions/${id}`),
}

export const identityApi = {
  users: (params: Record<string, unknown>) => request.get<PagedResult<UserDto>>('/users', { params }),
  getUser: (id: string) => request.get<UserDto>(`/users/${id}`),
  createUser: (data: Record<string, unknown>) => request.post<UserDto>('/users', data),
  updateUser: (id: string, data: Record<string, unknown>) => request.put<UserDto>(`/users/${id}`, data),
  deleteUser: (id: string) => request.delete<void>(`/users/${id}`),
  resetPassword: (id: string, newPassword: string) => request.post<void>(`/users/${id}/reset-password`, { newPassword }),
  assignRoles: (id: string, ids: string[]) => request.put<void>(`/users/${id}/roles`, { ids }),
  roles: () => request.get<RoleDto[]>('/roles'),
  createRole: (data: Record<string, unknown>) => request.post<RoleDto>('/roles', data),
  updateRole: (id: string, data: Record<string, unknown>) => request.put<RoleDto>(`/roles/${id}`, data),
  deleteRole: (id: string) => request.delete<void>(`/roles/${id}`),
  assignMenus: (id: string, ids: string[]) => request.put<void>(`/roles/${id}/menus`, { ids }),
  menus: () => request.get<MenuDto[]>('/menus'),
  createMenu: (data: Record<string, unknown>) => request.post<MenuDto>('/menus', data),
  updateMenu: (id: string, data: Record<string, unknown>) => request.put<MenuDto>(`/menus/${id}`, data),
  deleteMenu: (id: string) => request.delete<void>(`/menus/${id}`),
  auditLogs: (params: Record<string, unknown>) => request.get<PagedResult<AuditLogDto>>('/audit-logs', { params }),
}

export const fileApi = {
  list: (businessType: string, businessId: string) => request.get<FileDto[]>('/files', { params: { businessType, businessId } }),
  upload: (form: FormData, onUploadProgress?: (percentage: number) => void) => request.post<FileDto>('/files', form, {
    headers: { 'Content-Type': 'multipart/form-data' },
    onUploadProgress: (event) => onUploadProgress?.(event.total ? Math.round(event.loaded * 100 / event.total) : 0),
  }),
  remove: (id: string) => request.delete<void>(`/files/${id}`),
  downloadUrl: (id: string) => `${import.meta.env.VITE_API_BASE_URL || '/api/v1'}/files/${id}/download`,
}
