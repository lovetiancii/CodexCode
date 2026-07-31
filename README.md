# Tianci OA

Tianci OA 是面向中小企业的轻量人事 OA，覆盖 RBAC、组织与员工档案、招聘与多轮面试、入职、合同、文件归档和工作流。

## 项目

- `Tianci-OA-Api`：.NET 10、SqlSugar、MySQL、Redis、JWT、Swagger。
- `Tianci-OA-Web`：Vue 3、TypeScript、Vite、Element Plus、Pinia、Vue Router。
- `docs`：产品、架构、数据库和测试验收文档。
- `项目整体进度.md`：按 `.agents` 1～6 号提示词执行的总体进度。

## 本地启动

1. 复制 `Tianci-OA-Api/.env.example` 为 `.env`，替换全部示例密钥和密码。
2. 在 `Tianci-OA-Api` 中执行 `docker compose up --build`，或按其 README 分别启动 MySQL、Redis 和 API。
3. 在 `Tianci-OA-Web` 中安装依赖并执行 `pnpm dev`。
4. 浏览器访问前端开发地址；前端默认将 `/api` 代理到 `http://localhost:5224`。

开发默认账号为 `admin`，密码为 `Tianci@OA2026!`。首次登录后必须立即修改；共享或生产环境不得继续使用默认密码。

## 当前验收状态

- 后端构建通过，单元测试 15/15 通过。
- API 存活检查实际返回 HTTP 200。
- 前端 TypeScript 检查和生产构建通过。
- Docker Compose 配置解析通过。
- 真实 MySQL/Redis 全链路、五角色越权矩阵、性能与渗透测试需在部署环境继续执行。
