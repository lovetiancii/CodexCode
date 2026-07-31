# Tianci OA Web

Tianci OA 的 PC 管理端，使用 Vue 3、TypeScript、Vite、Element Plus、Pinia 与 Vue Router。

## 本地启动

```powershell
pnpm install
pnpm dev
```

默认通过 Vite 将 `/api` 代理到后端开发地址 `http://localhost:5224`，API 基础路径为 `/api/v1`。如后端监听地址不同，可复制 `.env.example` 为 `.env.local` 后调整 `VITE_API_PROXY_TARGET`。

## 工程检查

```powershell
pnpm typecheck
pnpm build
```

生产构建输出在 `dist/`。

项目同时提供多阶段 `Dockerfile` 与 Nginx 反向代理配置；完整 Web、API、MySQL、Redis 编排位于 `../Tianci-OA-Api/docker-compose.yml`。

## 认证与权限

- 登录成功后，访问令牌保存在浏览器本地存储，并由请求拦截器写入 Bearer 请求头。
- 路由守卫按登录状态及权限编码控制访问。
- `v-permission` 仅负责按钮显示；真正的业务鉴权仍由 Tianci-OA-Api 完成。
- HTTP 请求默认调用真实后端；项目不会静默用演示数据覆盖接口异常。
