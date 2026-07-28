# Admin.NET Vben Migration Inventory

This workspace keeps the original Admin.NET `Web` app intact and adds the new Vben 5 Ant Design Vue app in `vben-web/apps/web-antd`.

## Implemented foundation

- Backend access mode enabled in Vben preferences.
- Admin.NET request adapter:
  - `code === 200` is treated as success.
  - `result` is returned to feature code.
  - `access-token` and `x-access-token` response headers are persisted.
  - Expired JWT requests send `X-Authorization` with the refresh token.
- Admin.NET auth endpoints:
  - `POST /api/sysAuth/login`
  - `POST /api/sysAuth/logout`
  - `GET /api/sysAuth/userInfo`
  - `GET /api/sysMenu/ownBtnPermList`
  - `GET /api/sysMenu/loginMenuTree`
- Admin.NET menu records are mapped into Vben backend routes through a component allowlist.
- Unknown or not-yet-ported Admin.NET pages land on `views/adminnet/legacy-placeholder.vue` instead of loading arbitrary backend component paths.
- Login form defaults to `superadmin / 123456`, hides the tenant field, submits `tenantId = -1` internally, and encrypts passwords with Admin.NET's SM2 public key.
- Mobile login is wired to Admin.NET `sysSms/sendSms/{phoneNumber}` and `sysAuth/loginPhone`.

## First pages to rebuild

- Dashboard home: `/dashboard/home` from `home/index` is mapped to Vben workspace.
- System management: user, role, menu, dict, org, pos, tenant.
- Operational modules: log, file, notice, job, config, code generator.
- Heavy modules: approval flow, form designer, print designer, Office preview, MQTT, SignalR, ECharts/large-screen views.

## Known compatibility work

- Replace Element Plus form/table/dialog/tree/upload/date APIs with Ant Design Vue or Vben wrappers.
- Recreate the old `v-auth` directive usage with Vben access codes.
- Port tenant selector and backend captcha if the target deployment requires mandatory tenant or captcha login.
- Decide per heavy module whether to rebuild, bridge temporarily, or isolate as an iframe/micro frontend.
