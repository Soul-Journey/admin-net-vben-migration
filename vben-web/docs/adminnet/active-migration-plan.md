# Admin.NET WebUI 迁移到 Vben 执行总控台

最后更新：2026-06-05

这个文件是当前工作区里 Admin.NET WebUI 迁移的默认执行依据。以后用户说“继续计划”“下一步”“现在到哪了”“改一下计划”，都优先看这个文件，而不是依赖聊天记录。

## 工作规则

- 保持旧前端 `E:\admin.NET\Web` 可运行，作为 Element Plus 版本对照和回退入口。
- 新前端统一放在 `E:\admin.NET\vben-web`，基于 Vben 5 的 `apps/web-antd`。
- 默认优先适配现有 Admin.NET 后端接口，不轻易改后端协议。
- 如果必须改后端，改动要小、明确，并同步写入本文件。
- 每完成一个阶段或关键任务，都更新本文件的进度和“下一步默认任务”。
- 如果用户改变方向，先更新“关键决策”和“下一步默认任务”，再继续写代码。

## 当前状态

整体进度：第 5 阶段进行中，用户管理标杆页已完成，角色管理、菜单管理、字典管理、机构管理、职位管理、租户管理首版已完成，正在进入核心系统页连续迁移。

已完成：

- Admin.NET 后端已运行在 `http://localhost:5005`。
- 旧版 Admin.NET Web 已运行在 `http://localhost:8888`。
- 新版 Vben Web 已运行在 `http://localhost:5666`。
- 后端数据库已切换到 MySQL：
  - 地址：`localhost`
  - 端口：`3306`
  - 用户：`root`
  - 密码：`123456`
  - 数据库：`AdminNET`
- 项目本地 Node 和 pnpm 已安装在 `E:\admin.NET\vben-web\.runtime`。
- Vben 请求层已适配 Admin.NET 返回结构：
  - 成功码：`code === 200`
  - 数据字段：`result`
  - Token 响应头：`access-token`、`x-access-token`
- Vben 账号密码登录已接入 Admin.NET：
  - `POST /api/sysAuth/login`
  - 支持 SM2 密码加密。
  - 页面上已隐藏租户输入框。
  - 内部仍默认提交 `tenantId = -1`。
- Vben 手机号登录已接入 Admin.NET：
  - 发送短信验证码：`POST /api/sysSms/sendSms/{phoneNumber}`
  - 手机号登录：`POST /api/sysAuth/loginPhone`
  - 实际能否收到短信取决于后端 `SMS.json` 是否配置真实短信服务商。
- Vben 用户信息、按钮权限、后端菜单已接入：
  - `GET /api/sysAuth/userInfo`
  - `GET /api/sysMenu/ownBtnPermList`
  - `GET /api/sysMenu/loginMenuTree`
- Vben 动态菜单已经和旧版 `8888` 菜单基本一致。
- 未迁移页面会进入迁移占位页，不再 404 或白屏。
- 微信、QQ、GitHub、Google、扫码登录、注册入口已暂时隐藏，等 OAuth 和账号绑定方案确定后再开启。
- `system/user` 已进入新的 Vben 用户管理页，不再进入迁移占位页。
- 用户管理页已接入真实后端用户列表、搜索、机构树、角色/职位选项、状态切换、删除、复制、重置密码、解除锁定等第一版能力。
- `system/role` 已进入新的 Vben 角色管理页，不再进入迁移占位页。
- 角色管理页已接入真实后端角色列表、租户筛选、搜索、状态切换、删除、修改记录、角色新增/编辑、菜单授权、数据范围授权等第一版能力。
- `system/menu` 已进入新的 Vben 菜单管理页，不再进入迁移占位页。
- 菜单管理页已接入真实后端菜单树、租户筛选、菜单名称/类型搜索、树表展开折叠、刷新、新增/编辑/复制/删除、修改记录等第一版能力。
- `system/dict` 已进入新的 Vben 字典管理页，不再进入迁移占位页。
- 字典管理页已接入真实后端字典类型和字典值分页、左右联动、搜索、新增/编辑/复制/删除、系统内置保护、修改记录等第一版能力。
- 字典值新增/编辑的标签类型已改为彩色单选标签，界面显示中文语义，保存值仍兼容后端 `primary/success/info/warning/danger`。
- 字典类型、字典值、机构这类短表单弹窗已取消不必要的内层滚动条，内容自然撑开显示。
- `system/org` 已进入新的 Vben 机构管理页，不再进入迁移占位页。
- 机构管理页已接入真实后端机构树、左侧租户选择、左侧机构导航、右侧树表、机构类型字典、搜索、展开折叠、刷新、新增/编辑/复制/删除、修改记录等第一版能力。
- 机构管理修改记录已从黑底 Tooltip 改为白色 Popover，与账号、字典页细节保持一致。
- `system/pos` 已进入新的 Vben 职位管理页，不再进入迁移占位页。
- 职位管理页已接入真实后端职位列表、租户筛选、职位名称/编码搜索、新增/编辑/复制/删除、在职人数、人员明细、状态、修改记录等第一版能力。
- `system/tenant` 已进入新的 Vben 租户管理页，不再进入迁移占位页。
- 租户管理页已接入真实后端租户分页、租户名称/电话搜索、新增/编辑、Logo Base64 上传、启用注册、默认注册方案、状态切换、创建库、进入租管端、切换租户、授权菜单、同步授权、重置密码、删除租户等第一版能力。
- 租户管理高风险动作已统一放入二次确认：创建库、进入租管端、切换租户、同步授权、重置密码、删除租户。
- 租户管理三个点菜单的六个动作已补齐图标、二次确认、真实接口调用、成功提示和防重复执行状态。
- 租户管理“进入租管端”和“切换租户”已明确提示身份差异：进入租管端会切到该租户管理员账号，切换租户会保留当前用户身份，二者菜单权限可能不同。
- 租户授权菜单已改为按被授权租户 ID 加载菜单树，避免切换登录上下文后用错当前账号菜单范围。
- 租户授权菜单树已按旧版习惯改成横向紧凑布局：菜单显示路径标签，按钮权限横向换行，只显示中文动作名。
- 租户编辑弹窗已补回旧版“从库连接串”格式提示，避免用户不知道 JSON 权重连接串怎么填。
- 退出登录后的登录页已关闭右侧登录壳层的进入/浮动动画，并修复登录页 redirect 套娃导致 URL 越滚越长、页面反复闪动的问题。
- 登录成功后的跳转已改为保存 Token 和用户信息后直接进入 `/dashboard/home`，不再依赖登录页残留的 redirect 状态，避免第一次登录停留在登录页。
- 登录页登录和后台“登录过期续登弹窗”必须分开处理：登录页即使残留 `loginExpired` 也要进入工作台；续登弹窗只关闭弹窗、不改变当前业务页面。
- Vben 开发环境已补齐 Admin.NET SM2 登录公钥，重启后账号密码登录仍可正常对接后端。

## 关键决策

- 迁移策略：用 Vben 重建新前端，不在旧 Element Plus Web 上做大改。
- UI 方向：Vben 5 + Ant Design Vue。
- 后端接口：尽量保持 Admin.NET 原接口不变，由新前端做适配。
- 租户登录逻辑：
  - `tenantId = -1` 或空值代表默认租户。
  - 正数 `tenantId` 代表指定租户。
  - 租户不存在或被禁用时，由后端拒绝登录。
  - 租户的 ID 隔离、数据库隔离是数据隔离类型，不是登录状态。
- 登录 UI：
  - 账号密码登录作为主入口。
  - 手机号登录保留并接入后端。
  - 第三方登录、扫码登录、注册以后再做。
- 视觉方向：
  - 高密度、克制、清爽、可扫描。
  - 后台业务页不要做成营销页。
  - 列表页优先保证搜索、批量操作、列设置、密度、权限按钮好用。
  - 左侧树和权限树要清爽、有层级图标、hover/选中态舒适，不保留无意义数字徽标。
  - 三点工具菜单点击后必须自动关闭，刷新动作必须有加载态或成功反馈。
  - 登录页、弹窗、表单区域禁止出现无业务意义的持续闪动动画；后台系统优先稳定、清晰、可信。
  - 角色管理这类中等复杂表单优先使用居中弹出层；侧边栏只用于更适合右侧连续查看的详情或编辑场景。
  - 权限树默认不要全部展开；应提供搜索、展开、折叠、刷新和已选数量反馈，避免菜单/按钮权限过多时拥挤难找。
  - 修改记录/详情类内容使用白色 Popover，不用黑底 Tooltip 承载复杂内容；表格里只放轻量图标按钮，不暴露大段“详情”文字。
  - 如果页面有独立“修改记录”列，入口保持和角色管理一致：信息图标 + “详情”文字；字典管理这类列宽紧张页面才收进固定操作列并图标化。
  - 表格操作按钮必须阻止行选中冒泡；独立“修改记录”列可用 hover 触发，若详情入口收进操作列则用 click 触发，避免浮层遮挡同一行编辑/删除按钮。
  - 列较多的左右分栏表格要优先合并低频列到操作区，减少横向滚动；操作列保持固定可见。
  - 新增按钮优先放在查询工具区，同一类 CRUD 页面的按钮位置、弹窗宽度、底部按钮区要保持统一，避免页面显得臃肿。
  - 短表单弹窗不要为了统一高度强行加内层竖向滚动条；能在当前视口自然放下就直接展示。确实超长时优先用标签页、分组、步骤或更合理的弹窗宽度解决。
  - 字典、枚举、状态、标签类型等选项不能只暴露英文值；表单里优先显示中文语义、颜色预览或示例标签，保存仍使用后端需要的编码。
  - 连接串、JSON 配置、扩展数据这类容易填错的字段，要保留旧版提示、格式示例或说明文本，不能只给空输入框。
  - 高风险或会切换上下文的动作必须有二次确认、执行中状态、防重复点击和成功/失败反馈。
  - 后续页面默认带入账号管理的细节标准：控件密度统一、动作反馈明确、旧版功能不漏、视觉不要显得粗糙。

## 阶段清单

### 第 1 阶段：基线和运行环境

状态：已完成。

- [x] 安装项目本地 Node 22。
- [x] 安装项目本地 pnpm 11。
- [x] Vben 使用项目本地 Node，不依赖系统全局 Node。
- [x] 启动后端。
- [x] 启动旧 Web。
- [x] 启动 Vben Web。
- [x] 后端数据库切换到 MySQL。
- [x] 保留旧 Web 作为对照和回退。

### 第 2 阶段：Vben 基础骨架

状态：已完成。

- [x] 引入 Vben 5 `web-antd` 工作区。
- [x] 配置开发端口 `5666`。
- [x] 配置代理到 Admin.NET 后端。
- [x] 增加 Admin.NET 请求适配层。
- [x] 增加 Token 响应头持久化。
- [x] 增加刷新 Token 请求头支持。
- [x] 增加后端动态菜单组件白名单。
- [x] 增加未迁移页面占位页。

### 第 3 阶段：登录、用户信息、权限、菜单

状态：第一轮已完成。

- [x] 账号密码登录。
- [x] 退出登录。
- [x] 用户信息。
- [x] 按钮权限码。
- [x] 后端菜单树。
- [x] 动态路由转换。
- [x] 首页 `/dashboard/home` 映射。
- [x] 隐藏可见租户输入框，内部默认提交租户。
- [x] 保留并接入手机号登录。
- [x] 隐藏暂缓的第三方登录入口。

待加固：

- [ ] 给请求解包加单元测试。
- [ ] 给 Token 响应头同步加单元测试。
- [ ] 给菜单转换器加边界用例测试。
- [ ] 决定生产环境是否需要暴露租户切换。
- [ ] 决定 Vben 版本是否长期关闭后端图片验证码。

### 第 4 阶段：第一个业务标杆页

状态：已完成，后续作为 CRUD 页面基准继续复用。

默认目标：用户管理。

目标：

- 做出 Vben/Ant Design Vue 版本的用户管理页。
- 把它作为后续 CRUD 页面复用范式。
- 验证表格、搜索表单、弹窗或抽屉表单、组织树、租户过滤、树工具菜单、角色分配、档案信息、修改记录、状态切换、重置密码、导入导出、按钮权限。

预期交付：

- [x] `system/user` 路由进入新的 Vben 用户管理页，而不是迁移占位页。
- [x] 页面使用 Vben/Ant Design Vue 组件。
- [x] 页面调用现有 Admin.NET 用户相关接口。
- [x] 用户列表和搜索区接入真实数据。
- [x] 左侧机构树已对齐旧版，显示“系统默认、市场部、开发部、售后部、其他”。
- [x] 按钮级权限生效。
- [x] 旧 Web 不改，继续作为对照。
- [x] 机构树补齐旧版能力：租户下拉、机构搜索、全部展开、全部折叠、根节点、刷新。
- [x] 表格补齐旧版“修改记录”详情浮层。
- [x] 新增/编辑弹窗补齐三标签：基础信息、角色授权、档案信息。
- [x] 角色授权补齐旧版左右穿梭框交互。
- [x] 档案信息补齐证件、生日、性别、民族、地址、学历、政治面貌、办公电话、紧急联系人、备注等字段。
- [x] 对齐旧版账号类型过滤：表单提供“会员、普通账号、系统管理员”，不提供“超级管理员”创建入口，但已有数据仍可显示该类型。
- [x] 按反馈优化机构树视觉：补齐组织导航头、机构数量、树容器、层级图标、hover、选中态、菜单宽度和节点间距。
- [x] 将机构树三点菜单改为自定义工具菜单，包含全部展开、全部折叠、根节点、刷新。
- [x] 修复机构树工具菜单点击后不关闭的问题，并为刷新增加加载态和成功反馈。
- [x] 移除机构数量徽标，避免右上角数字干扰页面视线。
- [x] 新增、编辑、删除、状态切换、重置密码、解除锁定已完成第一版接入。
- [ ] 批量操作、导入导出、列设置、密度切换后续按旧版能力补齐。
- [ ] 持续视觉打磨左侧机构树，重点观察选中态、密度、租户下拉是否需要折叠。

验收标准：

- 从 Vben 菜单打开用户管理。
- 可以搜索用户。
- 可以新增用户。
- 可以编辑用户。
- 新增/编辑表单包含“基础信息 / 角色授权 / 档案信息”。
- 角色授权支持左右穿梭选择。
- 机构树支持租户选择、搜索、展开、折叠、根节点、刷新。
- 表格能查看创建者、创建时间、修改者、修改时间、备注。
- 可以启用、禁用用户。
- 如果后端支持，可以重置密码。
- 如果后端支持，可以删除或批量删除。
- 无权限按钮不会显示或不可操作。

### 第 5 阶段：核心系统页

状态：进行中，角色管理首版已完成。

建议顺序：

- [x] 角色管理。
  - [x] 角色列表、租户筛选、角色名称/编码搜索、分页。
  - [x] 数据范围字典显示。
  - [x] 状态开关。
  - [x] 修改记录浮层。
  - [x] 新增/编辑角色，按反馈从侧边栏改为居中弹出层。
  - [x] 菜单权限树授权，并按反馈优化为默认只展开根层，提供搜索、展开、折叠、刷新和已选数量。
  - [x] 数据范围授权，支持自定义机构树，并按反馈改为居中弹出层。
  - [ ] 浏览器手工验收新增、编辑、菜单授权、数据范围授权、删除。
- [x] 菜单管理。
  - [x] 树表列表、租户筛选、菜单名称/类型搜索。
  - [x] 默认只展开根层，提供展开、折叠、刷新。
  - [x] 显示菜单图标、菜单类型、路由路径、组件路径、权限标识、排序、状态。
  - [x] 修改记录浮层。
  - [x] 新增/编辑菜单，按目录/菜单/按钮动态展示字段。
  - [x] 复制菜单。
  - [x] 删除菜单。
  - [ ] 浏览器手工验收新增、编辑、复制、删除和动态菜单刷新后的实际路由效果。
- [x] 字典管理。
  - [x] 左右双表：字典类型、字典值。
  - [x] 字典类型搜索、分页、选中联动字典值。
  - [x] 字典值显示文本搜索、分页。
  - [x] 字典类型新增、编辑、删除，保留系统内置和枚举类保护。
  - [x] 字典值新增、编辑、复制、删除。
  - [x] 标签类型、Style、Class、扩展数据字段。
  - [x] 修改记录浮层。
  - [x] 按反馈收敛字典页视觉：详情改白色 Popover 和图标按钮，新增按钮归入查询工具区，弹窗尺寸统一收紧。
  - [x] 按反馈二次优化：修改记录合并到操作列、详情改为点击触发、操作按钮阻止行选中、字典值弹窗强制收窄。
  - [ ] 浏览器手工验收字典类型和字典值新增、编辑、复制、删除，以及字典缓存刷新效果。
- [x] 机构管理。
  - [x] 左侧机构导航树，支持搜索、展开、折叠、刷新。
  - [x] 左侧补齐超管租户选择，切换租户后同步刷新机构树和右侧树表。
  - [x] 右侧机构树表，默认只展开根层。
  - [x] 机构名称、机构类型搜索。
  - [x] 新增/编辑/复制/删除机构。
  - [x] 机构类型从 `org_type` 字典加载。
  - [x] 修改记录浮层。
  - [ ] 浏览器手工验收新增、编辑、复制、删除，以及删除时后端限制提示。
- [x] 职位管理。
  - [x] 职位列表、租户筛选、职位名称/编码搜索。
  - [x] 在职人数和人员明细浮层。
  - [x] 状态显示。
  - [x] 修改记录浮层。
  - [x] 新增/编辑/复制/删除职位。
  - [ ] 浏览器手工验收新增、编辑、复制、删除，以及职位下有用户时后端限制提示。
- [x] 租户管理。
  - [x] 租户分页、租户名称/电话搜索。
  - [x] 新增/编辑租户，包含基本信息和站点信息。
  - [x] Logo 本地选择转 Base64 后提交。
  - [x] 表格显示启用注册状态，编辑弹窗支持默认注册方案选择。
  - [x] 状态切换，默认租户禁用状态开关。
  - [x] 站点信息、连接串和修改记录浮层。
  - [x] 创建库、进入租管端、切换租户、授权菜单、同步授权、重置密码、删除租户。
  - [x] 授权菜单树支持搜索、展开、折叠、刷新和已选数量。
  - [ ] 浏览器手工验收新增、编辑、授权菜单、切换租户、进入租管端、创建库、重置密码、删除租户。

### 第 6 阶段：运维和配置页

状态：待开始。

- [ ] 日志。
- [ ] 文件管理。
- [ ] 通知公告。
- [ ] 定时任务。
- [ ] 系统配置。
- [ ] 代码生成。
- [ ] 导入导出。
- [ ] 打印。

### 第 7 阶段：重型和专项模块

状态：待开始。

每个模块先分类：重写、桥接、iframe、延期。

- [ ] 审批流。
- [ ] 低代码表单设计器。
- [ ] LogicFlow。
- [ ] Monaco 编辑器。
- [ ] Office 预览。
- [ ] MQTT。
- [ ] SignalR。
- [ ] ECharts 大屏。
- [ ] 富文本编辑器。
- [ ] 打印设计器。

## 下一步默认任务

继续第 5 阶段：以“账号管理”的细节标准推进核心系统页，当前优先验收“职位管理、租户管理”，然后进入“个人中心/通知公告/第三方账号”等剩余系统入口。

下一步按顺序做：

1. 打开 `http://localhost:5666/system/tenant`，对照旧版 `http://localhost:8888/system/tenant` 验证租户管理。
2. 验证租户新增、编辑、状态切换、授权菜单、同步授权、重置密码、删除租户。
3. 谨慎验证进入租管端、切换租户、创建库；这些动作会改变登录上下文或影响租户数据库。
4. 继续补齐剩余系统入口：个人中心、通知公告、第三方账号、AD域配置。
5. 后续页面继续复用：查询栏、表格/树表、修改记录浮层、权限按钮、弹窗表单、树视觉、明确反馈。

## 风险登记

- Element Plus 和 Ant Design Vue 的表单校验、树、表格、上传、日期选择器、弹窗生命周期 API 不一致。
- 旧 Web 生成的 API 名称不一定符合直觉，需要对照实际接口。
- Admin.NET 在 SQLite 和 MySQL 下的种子数据、配置值可能不同。
- 手机号登录依赖真实短信服务商配置，本地可能只能验证接口调用，不能验证收短信。
- 后端动态组件路径必须继续经过前端白名单，不能直接信任后端路径。
- 租户管理包含高风险动作：创建数据库、切换租户、进入租管端、删除租户、重置密码，必须保留二次确认并避免误触。
- 重型模块可能携带全局 CSS 或深度依赖 Element Plus。
- 旧 Web 和 Vben 依赖并存会增加磁盘占用和构建体积。
- 按钮权限容易遗漏，迁移每个页面都要逐项对照旧 Web。

## 验证命令

Vben 必须使用项目本地 Node：

```powershell
$env:PATH="E:\admin.NET\vben-web\.runtime\node-v22.22.0-win-x64;E:\admin.NET\vben-web\.runtime\npm-global;$env:PATH"
cd E:\admin.NET\vben-web
pnpm dev
pnpm exec eslint "apps/web-antd/src/**/*.{ts,vue}"
pnpm -F @vben/web-antd run typecheck
```

说明：当前工作区里 Vben 全量 typecheck 可能比较慢。日常迭代优先跑定向 lint 和页面验证，大任务交付前再跑更完整检查。

## 快速启动三服务

目标端口：

- 后端：`http://localhost:5005`
- 旧版 Web：`http://localhost:8888`
- 新版 Vben Web：`http://localhost:5666`

启动前先检查端口：

> 注意：下面这一段只负责“检查端口”，不会启动服务。看到 `5666` 没有监听时，需要执行后面的启动命令。

```powershell
Get-NetTCPConnection -LocalPort 5005,8888,5666 -State Listen -ErrorAction SilentlyContinue |
  Select-Object LocalAddress,LocalPort,OwningProcess,@{Name='ProcessName';Expression={(Get-Process -Id $_.OwningProcess -ErrorAction SilentlyContinue).ProcessName}}
```

一键启动三服务：

```powershell
$logDir='E:\admin.NET\.run-logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

$nodePath='E:\admin.NET\vben-web\.runtime\node-v22.22.0-win-x64'
$pnpmPath='E:\admin.NET\vben-web\.runtime\npm-global'

$backendCmd = 'cd /d E:\admin.NET && dotnet run --framework net8.0 --project Admin.NET\Admin.NET.Web.Entry\Admin.NET.Web.Entry.csproj --launch-profile Admin.NET.Web.Entry > E:\admin.NET\.run-logs\backend-5005.log 2>&1'
$oldWebCmd = "set PATH=$nodePath;$pnpmPath;%PATH%&& cd /d E:\admin.NET\Web && npm run dev -- --host 0.0.0.0 --port 8888 --strictPort > E:\admin.NET\.run-logs\old-web-8888.log 2>&1"
$vbenCmd = "set PATH=$nodePath;$pnpmPath;%PATH%&& cd /d E:\admin.NET\vben-web\apps\web-antd && pnpm vite --mode development --host 0.0.0.0 --port 5666 --strictPort > E:\admin.NET\.run-logs\vben-5666.log 2>&1"

Start-Process -FilePath cmd.exe -ArgumentList '/c', $backendCmd -WindowStyle Hidden
Start-Process -FilePath cmd.exe -ArgumentList '/c', $oldWebCmd -WindowStyle Hidden
Start-Process -FilePath cmd.exe -ArgumentList '/c', $vbenCmd -WindowStyle Hidden
```

只单独启动新版 Vben Web：

```powershell
$logDir='E:\admin.NET\.run-logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

$nodePath='E:\admin.NET\vben-web\.runtime\node-v22.22.0-win-x64'
$pnpmPath='E:\admin.NET\vben-web\.runtime\npm-global'
$vbenCmd = "set PATH=$nodePath;$pnpmPath;%PATH%&& cd /d E:\admin.NET\vben-web\apps\web-antd && pnpm vite --mode development --host 0.0.0.0 --port 5666 --strictPort > E:\admin.NET\.run-logs\vben-5666.log 2>&1"

Start-Process -FilePath cmd.exe -ArgumentList '/c', $vbenCmd -WindowStyle Hidden
```

启动后验证：

```powershell
Start-Sleep -Seconds 10
Get-NetTCPConnection -LocalPort 5005,8888,5666 -State Listen -ErrorAction SilentlyContinue |
  Select-Object LocalAddress,LocalPort,OwningProcess,@{Name='ProcessName';Expression={(Get-Process -Id $_.OwningProcess -ErrorAction SilentlyContinue).ProcessName}}

(Invoke-WebRequest -Uri http://localhost:5005 -UseBasicParsing -TimeoutSec 10).StatusCode
(Invoke-WebRequest -Uri http://localhost:8888 -UseBasicParsing -TimeoutSec 10).StatusCode
(Invoke-WebRequest -Uri http://localhost:5666 -UseBasicParsing -TimeoutSec 10).StatusCode
```

查看日志：

```powershell
Get-Content -Tail 80 E:\admin.NET\.run-logs\backend-5005.log
Get-Content -Tail 80 E:\admin.NET\.run-logs\old-web-8888.log
Get-Content -Tail 80 E:\admin.NET\.run-logs\vben-5666.log
```

如果前端错误跑到 `5173/5174`，先停掉当前项目相关 Node 再按固定端口重启：

```powershell
Get-CimInstance Win32_Process |
  Where-Object { $_.Name -in @('node.exe','cmd.exe') -and ($_.CommandLine -like '*E:\admin.NET\Web*' -or $_.CommandLine -like '*E:\admin.NET\vben-web*') } |
  ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
```

## 服务地址

- 后端：`http://localhost:5005`
- 旧版 Web 对照：`http://localhost:8888`
- 新版 Vben Web：`http://localhost:5666`
