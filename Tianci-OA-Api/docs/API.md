# Tianci OA API v1

基础路径为 `/api/v1`。除登录和一次性管理员初始化外，所有接口都要求 `Authorization: Bearer <token>`；受保护动作还要求对应 RBAC 权限。

| 模块 | 主要接口 |
|---|---|
| 认证 | `POST /auth/login`、`POST /auth/initialize-admin` |
| 用户 | `GET/POST /users`、`GET/PUT/DELETE /users/{id}`、`GET/PUT /users/{id}/roles`、`POST /users/{id}/reset-password` |
| 角色 | `GET/POST /roles`、`PUT/DELETE /roles/{id}`、`GET/PUT /roles/{id}/menus` |
| 菜单 | `GET/POST /menus`、`PUT/DELETE /menus/{id}` |
| 组织 | `GET/POST /departments`、`PUT/DELETE /departments/{id}`、`GET/POST /positions`、`PUT/DELETE /positions/{id}` |
| 员工 | `GET/POST /employees`、`GET/PUT /employees/{id}`、`POST /employees/{id}/regularize`、`POST /employees/{id}/terminate`、`POST /employees/{id}/archive` |
| 简历 | `GET/POST /resumes`、`GET/PUT /resumes/{id}`、`PUT /resumes/{id}/attachment`、`POST /resumes/{id}/status` |
| 面试 | `GET /resumes/{id}/interviewers`、`POST/GET /resumes/{id}/interviews`、`POST /resumes/{id}/interviews/{interviewId}/complete` |
| 录用 | `POST /resumes/{id}/confirm-offer`、`POST /resumes/{id}/confirm-entry` |
| 合同 | `GET/POST /contracts`、`GET/PUT /contracts/{id}`、`GET /contracts/expiring`、`POST /contracts/{id}/activate|terminate|renew|archive` |
| 文件 | `POST /files`（multipart）、`GET /files?businessType=&businessId=`、`GET /files/{id}/download`、`DELETE /files/{id}` |

## 权限规则

- 功能权限由角色绑定的菜单/操作权限码决定，接口以 `Permission` 特性强制校验，前端权限只负责隐藏无权操作入口。
- “安排面试”使用 `resume:schedule`，“提交面试评价”使用 `resume:evaluate`，面试官默认只授予评价权限。
- 多角色数据范围取最大授权：`All` 大于 `DepartmentAndChildren`，后者大于 `Self`。
- 员工“仅本人”按当前用户绑定的 `employee_id` 判断；合同按合同的 `employee_id` 判断。
- 简历“仅本人”按 `owner_user_id` 或本人作为面试官的面试记录判断。
- 本部门及下级通过当前用户 `department_id` 递归计算部门集合；员工、应聘岗位、合同及附件均继承该集合过滤。
- 文件上传、列表、下载和删除除功能权限外，还会校验所关联简历、员工、入职记录或合同的数据权限。

简历附件采用两阶段关联：先创建简历取得 ID，再调用 `POST /files` 上传，最后调用 `PUT /resumes/{id}/attachment` 将返回的文件 ID 设为当前附件。
| 工作流 | `POST /workflows`、`GET /workflows/{instanceId}`、`POST /workflows/{instanceId}/nodes/{nodeId}/decision` |
| 审计 | `GET /audit-logs` |

分页参数为 `pageNumber`（从 1 开始）和 `pageSize`（1～100）。时间使用 ISO 8601，时间点统一按 UTC 存储。状态动作均要求提交当前 `version`；并发冲突返回 HTTP 409。

文件上传字段：`businessType`（`resume|employee|entry|contract`）、`businessId`、`category`、`file`。允许 PDF、DOC、DOCX、JPG、JPEG、PNG，最大 20 MB；下载前会重新验证业务对象。
