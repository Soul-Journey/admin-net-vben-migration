# Admin.NET Vben 生产部署说明

适用范围：`apps/web-antd` 新版前端。旧版 `E:\admin.NET\Web` 继续保留为只读对照和回退入口。

## 发布产物

1. 使用项目本地 Node 22 和 pnpm 11 执行 `pnpm build`。
2. 发布目录为 `apps/web-antd/dist`，压缩包为 `apps/web-antd/dist.zip`。
3. 生产运行时配置由构建产物中的 `_app-config-*.js` 提供，当前 API 地址为同源 `/api`。
4. `index.html` 和 `_app-config-*.js` 必须禁用缓存；带内容哈希的 `js/css/jse` 资源可以长期缓存。

## 反向代理

仓库提供两份配置：

- `scripts/deploy/nginx.conf`：前后端部署在同一台主机时的完整 Nginx 示例，默认后端为 `127.0.0.1:5005`。
- `scripts/deploy/adminnet.conf.template`：Docker 镜像使用的模板，通过 `ADMINNET_BACKEND` 注入后端地址。

代理必须同时保留以下路径：

- `/api/`：Admin.NET API，请求路径中的 `/api` 不得被删除。
- `/hubs/`：SignalR，必须转发 WebSocket Upgrade 头，并使用长连接超时。
- `/upload/`：上传文件访问与下载。

推荐保持前后端同源，由 Nginx 统一提供 HTTPS，避免浏览器跨域、Cookie、下载响应头和 SignalR 配置分裂。模板不再使用全开放 CORS。

## Docker

镜像默认通过 `http://host.docker.internal:5005` 访问宿主机后端，可以覆盖：

```bash
docker run -d \
  -p 8010:8080 \
  -e ADMINNET_BACKEND=http://host.docker.internal:5005 \
  --name vben-admin-local \
  vben-admin-local
```

Linux 宿主机需要把 `ADMINNET_BACKEND` 改为容器网络中的后端服务名，或显式配置宿主机网关。

## 安全边界

- 生产登录页不会预填开发账号或密码；`.env.development` 只用于本地开发。
- 数据库连接串、JWT 密钥、SM2 私钥、第三方令牌不得进入前端构建产物。
- SM2 公钥可以公开；前端加密不能替代 HTTPS。
- 当前 VForm 3 和 `vue-plugin-hiprint` 为兼容旧版表单/打印协议，内部包含动态代码执行能力。它们已经按路由懒加载，但暂时无法在不破坏功能的情况下启用禁止 `unsafe-eval` 的全局严格 CSP。
- 不要为了这两个重组件给整个后台静默添加宽松 CSP。上线时应把它们视为受信任的管理员功能，限制导入来源；后续独立升级或隔离到专用子域。
- 生产环境应关闭或限制 Swagger、任务调度看板和其他管理入口，并在网关层配置 HTTPS、访问控制、请求体大小和超时。

## 上线前检查

- 新旧前端和后端均可启动，旧版目录无 Git 改动。
- 登录、动态菜单、按钮权限、租户切换和 SignalR 在线用户正常。
- 隔离测试租户的新增、授权、同步、切换和删除完成，权限关系表无重复与孤儿记录。
- MySQL 已生成可恢复备份，并记录校验值。
- 生产包不包含本机端口、默认密码、数据库凭据或私钥。
- 先部署到预发布地址进行人工验收，验收通过后再切换生产入口；旧版入口至少保留一个发布周期。
