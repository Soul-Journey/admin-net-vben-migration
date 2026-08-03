# Admin.NET Vben 文档中心

这里集中保存新版 Vben 管理端的学习、使用、开发和部署文档。

## 建议阅读顺序

1. [功能与使用手册](./function-and-user-guide.md)：面向管理员和业务使用人员，介绍功能、权限概念和实际操作流程。
2. [二次开发指南](./secondary-development-guide.md)：面向开发人员，介绍目录、接口、权限、多租户、页面规范和验收流程。
3. [迁移执行总控台](./active-migration-plan.md)：记录迁移决策、历史问题、风险规则和当前完成状态。
4. [生产部署说明](./production-deployment.md)：介绍构建产物、Nginx、Docker、HTTPS 和上线检查。
5. [迁移盘点](./migration-inventory.md)：早期迁移范围和兼容性记录。

## 项目边界

- 新版前端：E:\admin.NET\vben-web\apps\web-antd
- Admin.NET 后端：E:\admin.NET\Admin.NET
- 旧版前端：E:\admin.NET\Web
- 旧版前端永久只读，只允许运行和功能对照，禁止继续修改。
- 新业务统一在新版 Vben 和现有 Admin.NET 后端上二次开发。

## 当前结论

数据库中已启用、旧版真实存在的内部管理功能已经完成 Vben 迁移主体，可作为二次开发基础框架。短信、邮件、LDAP、微信支付等依赖外部账号或密钥的能力，需要在具备真实测试配置后单独联调。
