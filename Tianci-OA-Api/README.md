# Tianci OA API

Tianci OA 的 .NET 10 模块化单体后端。解决方案严格分为 Domain、Application、Infrastructure、WebApi 四层，使用 MySQL 8、SqlSugar、JWT、Redis、AutoMapper 与 Swagger。

## 代码组织约定

- C# 代码统一采用 Allman 大括号、4 空格缩进、一行一语句，并通过根目录 `.editorconfig` 固化格式。
- Controller、Service 接口、Service 实现分别独立文件；业务 DTO 单独放在对应模块的 `*Dtos.cs` 中。
- Controller 只负责路由、鉴权、参数接收和结果返回，业务逻辑由 Application 层 Service 实现。
- 超过单行可读范围的方法签名、条件、对象初始化和链式调用必须按语义分行。

## 功能

- RBAC：用户、角色、菜单 CRUD，角色授权、用户授权、接口权限校验与会话安全戳失效。
- 组织与员工：部门/岗位、员工分页查询、详情、新增、编辑、确认转正、离职、归档和敏感字段保护。
- 招聘入职：简历、状态流转、1～5 轮面试、评价、录用、确认入职并事务创建员工。
- 合同：草稿、生效、终止、续签、归档及到期提醒。
- 工作流：实例、节点、记录，启动、审批、拒绝、自动推进与幂等请求。
- 文件与审计：20 MB 白名单上传、MIME/文件头/路径校验、受控下载、只追加审计日志。
- 平台能力：统一响应、全局异常、TraceId、雪花 ID 字符串 JSON、健康检查、Swagger。

## 本地运行

1. 安装 .NET 10 SDK、MySQL 8、Redis。
2. 执行 `database/init.sql`。开发默认账号为 `admin`，默认密码为 `Tianci@OA2026!`；首次登录后必须立即修改。
3. 通过环境变量覆盖连接串、JWT 密钥和初始化令牌：

```powershell
$env:ConnectionStrings__MySql='Server=localhost;Database=tianci_oa;User=tianci;Password=...;CharSet=utf8mb4;Allow User Variables=true;'
$env:ConnectionStrings__Redis='localhost:6379,password=...'
$env:Jwt__Secret='至少32字节的随机密钥'
$env:Initialization__Token='一次性高强度随机令牌'
dotnet run --project src/Tianci.OA.WebApi
```

4. 使用默认账号登录后立即通过用户管理重置管理员密码。`POST /api/v1/auth/initialize-admin` 仅保留给采用“禁用种子 + 一次性令牌”策略的定制部署。
5. 登录后在 Swagger 的 Bearer 鉴权中使用 Access Token。

## 验证

```powershell
dotnet restore Tianci.OA.slnx
dotnet build Tianci.OA.slnx --no-restore
dotnet test Tianci.OA.slnx --no-build --no-restore
```

## Docker Compose

复制 `.env.example` 为 `.env` 并替换所有密钥，然后执行：

```bash
docker compose up --build -d
```

Web 管理端默认位于 `http://localhost`，API 也可通过 `http://localhost:8080` 访问。Web 容器由 Nginx 提供静态资源并将 `/api` 反向代理到 API；文件保存在命名卷中，MySQL 和 Redis 未映射到宿主公网端口。

默认管理员仅用于首次开发启动。任何共享、测试或生产环境都必须在开放网络访问前修改默认密码。

## 响应与 ID

普通 JSON 接口统一返回 `success/code/message/data/traceId`。所有 `long`/`long?` 在 JSON 中写为字符串，读取时兼容字符串和数字。文件下载保持二进制响应。业务异常映射为 400/401/403/404/409/413，生产环境不返回堆栈或数据库错误。

详细路由见 [API.md](API.md)。
