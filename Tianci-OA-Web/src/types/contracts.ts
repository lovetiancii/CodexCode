export interface ApiResponse<T> {
  success: boolean
  code: string
  message: string
  data: T
  traceId: string
}

export interface PageQuery {
  pageNumber: number
  pageSize: number
}

export interface PagedResult<T> {
  items: T[]
  pageNumber: number
  pageSize: number
  total: number
}

export type Id = string
export type Nullable<T> = T | null

export const ENABLED_STATUS = { Disabled: 0, Enabled: 1 } as const
export const USER_STATUS = { Disabled: 0, Enabled: 1, Locked: 2 } as const
export const DATA_SCOPE = { All: 1, DepartmentAndChildren: 2, Self: 3 } as const
export const MENU_TYPE = { Directory: 1, Menu: 2, Action: 3 } as const
export const GENDER = { Unknown: 0, Male: 1, Female: 2 } as const
export const EMPLOYEE_STATUS = { Probation: 1, Active: 2, Terminated: 3, Archived: 4 } as const
export const RESUME_STATUS = {
  Submitted: 1, Screening: 2, InterviewPending: 3, Interviewing: 4,
  OfferPending: 5, EntryPending: 6, Hired: 7, Rejected: 8, OfferDeclined: 9,
} as const
export const INTERVIEW_CONCLUSION = { Pending: 0, Pass: 1, Fail: 2, Hold: 3, Cancelled: 4 } as const
export const CONTRACT_TYPE = { Labor: 1, Internship: 2, Confidentiality: 3, Other: 4 } as const
export const CONTRACT_STATUS = { Draft: 1, Active: 2, Terminated: 3, Renewed: 4, Archived: 5 } as const

export const EMPLOYEE_STATUS_LABEL: Record<number, string> = { 1: '试用', 2: '在职', 3: '离职', 4: '已归档' }
export const RESUME_STATUS_LABEL: Record<number, string> = {
  1: '简历投递', 2: '筛选中', 3: '待安排面试', 4: '面试中', 5: '待录用', 6: '待入职', 7: '已入职', 8: '已淘汰', 9: '录用拒绝',
}
export const CONTRACT_STATUS_LABEL: Record<number, string> = { 1: '草稿', 2: '生效中', 3: '已终止', 4: '已续签', 5: '已归档' }
export const CONTRACT_TYPE_LABEL: Record<number, string> = { 1: '劳动合同', 2: '实习协议', 3: '保密协议', 4: '其他' }

export interface UserDto {
  id: Id; username: string; displayName: string; phone: Nullable<string>; email: Nullable<string>
  employeeId: Nullable<Id>; departmentId: Nullable<Id>; status: number; requiresInitialization: boolean
}
export interface LoginResponse { accessToken: string; expiresAtUtc: string; user: UserDto; permissions: string[] }
export interface RoleDto { id: Id; name: string; code: string; dataScope: number; status: number; isSystem: boolean; remark: Nullable<string> }
export interface MenuDto {
  id: Id; parentId: Nullable<Id>; type: number; name: string; routePath: Nullable<string>; component: Nullable<string>
  permissionCode: Nullable<string>; icon: Nullable<string>; sortOrder: number; visible: boolean; status: number
}
export interface DepartmentDto { id: Id; parentId: Nullable<Id>; name: string; code: string; sortOrder: number; status: number; remark: Nullable<string> }
export interface PositionDto { id: Id; departmentId: Id; name: string; code: string; status: number; remark: Nullable<string> }
export interface EmployeeDto {
  id: Id; employeeNo: string; sourceResumeId: Nullable<Id>; name: string; gender: number; phone: string; email: Nullable<string>
  departmentId: Id; positionId: Id; status: number; entryDate: string; probationMonths: number; regularDate: Nullable<string>
  terminationDate: Nullable<string>; terminationReason: Nullable<string>; version: number
}
export interface EmployeeDetailDto { employee: EmployeeDto; idCard: Nullable<string>; monthlySalary: Nullable<string> }
export interface ResumeDto {
  id: Id; candidateNo: string; name: string; gender: number; phone: string; email: Nullable<string>; education: Nullable<string>
  workExperience: Nullable<string>; skills: Nullable<string>; appliedPositionId: Id; attachmentFileId: Nullable<Id>
  status: number; currentRound: number; rejectReason: Nullable<string>; remark: Nullable<string>; version: number
}
export interface InterviewDto {
  id: Id; resumeId: Id; roundNo: number; interviewerUserId: Id; scheduledAt: string; location: Nullable<string>
  score: Nullable<number>; evaluation: Nullable<string>; conclusion: number; nextScheduledAt: Nullable<string>
  completedAt: Nullable<string>; remark: Nullable<string>
}
export interface ContractDto {
  id: Id; contractNo: string; employeeId: Id; contractType: number; startDate: string; endDate: string; reminderDays: number
  attachmentFileId: Nullable<Id>; status: number; terminatedAt: Nullable<string>; remark: Nullable<string>; version: number
  isExpired: boolean; isExpiringSoon: boolean
}
export interface FileDto {
  id: Id; businessType: string; businessId: Id; category: string; originalName: string; contentType: string
  extension: string; sizeBytes: number; createdAt: string
}
export interface AuditLogDto {
  id: Id; traceId: Nullable<string>; operatorUserId: Nullable<Id>; operatorName: Nullable<string>; module: string; action: string
  businessType: Nullable<string>; businessId: Nullable<Id>; requestMethod: Nullable<string>; requestPath: Nullable<string>
  clientIp: Nullable<string>; succeeded: boolean; errorCode: Nullable<string>; durationMs: Nullable<number>; createdAt: string
}
