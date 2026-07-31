# Tianci OA API v1

基础路径为 `/api/v1`。除登录和一次性管理员初始化外，所有接口都要求 `Authorization: Bearer <token>`；受保护动作还要求对应 RBAC 权限。

| 模块 | 主要接口 |
|---|---|
| 认证 | `POST /auth/login`、`POST /auth/initialize-admin` |
| 用户 | `GET/POST /users`、`GET/PUT/DELETE /users/{id}`、`PUT /users/{id}/roles`、`POST /users/{id}/reset-password` |
| 角色 | `GET/POST /roles`、`PUT/DELETE /roles/{id}`、`PUT /roles/{id}/menus` |
| 菜单 | `GET/POST /menus`、`PUT/DELETE /menus/{id}` |
| 组织 | `GET/POST /departments`、`PUT/DELETE /departments/{id}`、`GET/POST /positions`、`PUT/DELETE /positions/{id}` |
| 员工 | `GET/POST /employees`、`GET/PUT /employees/{id}`、`POST /employees/{id}/terminate`、`POST /employees/{id}/archive` |
| 简历 | `GET/POST /resumes`、`GET/PUT /resumes/{id}`、`POST /resumes/{id}/status` |
| 面试 | `POST/GET /resumes/{id}/interviews`、`POST /resumes/{id}/interviews/{interviewId}/complete` |
| 录用 | `POST /resumes/{id}/confirm-offer`、`POST /resumes/{id}/confirm-entry` |
| 合同 | `GET/POST /contracts`、`GET/PUT /contracts/{id}`、`GET /contracts/expiring`、`POST /contracts/{id}/activate|terminate|renew|archive` |
| 文件 | `POST /files`（multipart）、`GET /files?businessType=&businessId=`、`GET /files/{id}/download`、`DELETE /files/{id}` |
| 工作流 | `POST /workflows`、`GET /workflows/{instanceId}`、`POST /workflows/{instanceId}/nodes/{nodeId}/decision` |
| 审计 | `GET /audit-logs` |

分页参数为 `pageNumber`（从 1 开始）和 `pageSize`（1～100）。时间使用 ISO 8601，时间点统一按 UTC 存储。状态动作均要求提交当前 `version`；并发冲突返回 HTTP 409。

文件上传字段：`businessType`（`resume|employee|entry|contract`）、`businessId`、`category`、`file`。允许 PDF、DOC、DOCX、JPG、JPEG、PNG，最大 20 MB；下载前会重新验证业务对象。
