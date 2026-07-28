# Admin.NET Vben Web

This directory is the new Vben 5 + Ant Design Vue frontend for Admin.NET. The original Admin.NET `Web` directory remains unchanged and can be used as the fallback UI during migration.

## Requirements

- Node.js `^22.18.0` or `^24.0.0`
- pnpm `>=11.0.0`

The current Vben 5.7 toolchain cannot install or run on Node 20.

## Commands

```bash
cd vben-web
pnpm install
pnpm dev
pnpm build
pnpm test:unit
```

The development proxy defaults to `http://localhost:5005/api`. Override it with `VITE_PROXY_TARGET` in `apps/web-antd/.env.development` when your Admin.NET API uses a different port.

## Migration status

- Auth, user info, button permissions, and backend menus are wired to Admin.NET.
- Admin.NET menu components are converted through a safe allowlist.
- Unported pages render `views/adminnet/legacy-placeholder.vue`.
- The seeded Admin.NET home route `/dashboard/home` maps to Vben's workspace dashboard.

使用 `docs/adminnet/active-migration-plan.md` 作为当前迁移执行计划和进度总控台。页面迁移清单见 `docs/adminnet/migration-inventory.md`。
