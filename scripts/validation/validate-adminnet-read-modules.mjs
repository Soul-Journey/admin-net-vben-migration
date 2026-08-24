import { readFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { pathToFileURL } from 'node:url';

const apiBase = process.env.ADMINNET_API_BASE || 'http://localhost:5005/api';
const password = process.env.ADMINNET_TEST_PASSWORD;
if (!password) throw new Error('缺少 ADMINNET_TEST_PASSWORD 环境变量');

const workspace = new URL('../../', import.meta.url).pathname
  .replace(/^\/([A-Z]:)/i, '$1')
  .replaceAll('/', '\\')
  .replace(/\\$/, '');
const require = createRequire(
  pathToFileURL(`${workspace}/vben-web/apps/web-antd/package.json`),
);
const { sm2 } = await import(pathToFileURL(require.resolve('sm-crypto-v2')));
const envText = await readFile(
  `${workspace}/vben-web/apps/web-antd/.env.development`,
  'utf8',
);
const publicKey = envText.match(/^VITE_SM_PUBLIC_KEY=(.+)$/m)?.[1]?.trim();
if (!publicKey) throw new Error('未找到 VITE_SM_PUBLIC_KEY');

function valueOf(record, key) {
  if (!record || typeof record !== 'object') return undefined;
  return record[key] ?? record[key[0].toUpperCase() + key.slice(1)];
}

async function rawRequest(path, { body, method = 'GET', token } = {}) {
  const response = await fetch(`${apiBase}${path}`, {
    body: body === undefined ? undefined : JSON.stringify(body),
    headers: {
      Accept: 'application/json',
      ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    method,
    signal: AbortSignal.timeout(20_000),
  });
  const payload = await response.json().catch(() => ({}));
  const code = valueOf(payload, 'code');
  if (!response.ok || (code !== undefined && code !== 200)) {
    throw new Error(
      valueOf(payload, 'message') || `HTTP ${response.status}`,
    );
  }
  return code === undefined ? payload : valueOf(payload, 'result');
}

const login = await rawRequest('/sysAuth/login', {
  body: {
    account: 'superadmin',
    code: null,
    codeId: 0,
    password: sm2.doEncrypt(password, publicKey, 1),
    tenantId: -1,
  },
  method: 'POST',
});
const token = valueOf(login, 'accessToken');
if (!token) throw new Error('登录未返回访问令牌');

const page = { page: 1, pageSize: 5 };
const checks = [
  ['认证-用户信息', 'GET', '/sysAuth/userInfo'],
  ['认证-动态菜单', 'GET', '/sysMenu/loginMenuTree'],
  ['认证-按钮权限', 'GET', '/sysMenu/ownBtnPermList'],
  ['账号管理', 'POST', '/sysUser/page', { ...page, orgId: -1 }],
  ['角色管理', 'POST', '/sysRole/page', page],
  ['机构管理', 'GET', '/sysOrg/list?Id=0'],
  ['职位管理', 'GET', '/sysPos/list'],
  ['通知公告', 'POST', '/sysNotice/page', page],
  ['个人站内信', 'POST', '/sysNotice/pageReceived', page],
  ['三方账号', 'POST', '/sysWechatUser/page', page],
  ['AD域配置', 'POST', '/sysLdap/page', page],
  ['在线用户', 'POST', '/sysOnlineUser/page', page],
  ['租户管理', 'POST', '/sysTenant/page', page],
  ['租户选项', 'GET', '/sysTenant/list'],
  ['注册方案', 'POST', '/sysUserRegWay/list', {}],
  ['菜单管理', 'GET', '/sysMenu/list'],
  ['参数配置', 'POST', '/sysConfig/page', page],
  ['参数分组', 'GET', '/sysConfig/groupList'],
  ['系统信息配置', 'GET', '/sysConfig/sysInfo'],
  ['字典类型', 'POST', '/sysDictType/page', page],
  ['字典类型选项', 'GET', '/sysDictType/list'],
  ['账号类型字典', 'GET', '/sysDictData/dataList/AccountTypeEnum'],
  ['模板管理', 'POST', '/sysTemplate/page', page],
  ['模板分组', 'GET', '/sysTemplate/groupList'],
  ['任务调度', 'POST', '/sysJob/pageJobDetail', page],
  ['任务分组', 'POST', '/sysJob/listJobGroup'],
  ['任务运行记录', 'POST', '/sysJob/pageJobTriggerRecord', page],
  ['任务集群', 'GET', '/sysJob/jobClusterList'],
  ['服务器基础信息', 'GET', '/sysServer/serverBase'],
  ['服务器资源', 'GET', '/sysServer/serverUsed'],
  ['服务器磁盘', 'GET', '/sysServer/serverDisk'],
  ['服务器程序集', 'GET', '/sysServer/assemblyList'],
  ['缓存管理', 'GET', '/sysCache/keyList'],
  ['行政区划分页', 'POST', '/sysRegion/page', page],
  ['行政区划树', 'GET', '/sysRegion/list?id=0'],
  ['文件管理', 'POST', '/sysFile/page', page],
  ['打印模板', 'POST', '/sysPrint/page', page],
  ['动态插件', 'POST', '/sysPlugin/page', page],
  ['开放接口', 'POST', '/sysOpenAccess/pageSafe', page],
  ['系统更新配置', 'GET', '/sysUpdate/configurationStatus'],
  ['系统更新备份', 'POST', '/sysUpdate/list'],
  ['系统更新日志', 'GET', '/sysUpdate/logs'],
  ['微信支付配置', 'GET', '/sysWechatPay/configurationStatus'],
  ['微信支付记录', 'POST', '/sysWechatPay/page', page],
  ['代码生成配置', 'POST', '/sysCodeGen/page', page],
  ['代码生成数据库', 'GET', '/sysCodeGen/databaseList'],
  ['代码生成命名空间', 'GET', '/sysCodeGen/applicationNamespaces'],
  ['代码生成方式', 'GET', '/sysCodeGen/generateTypeList'],
  ['库表管理数据库', 'GET', '/sysDatabase/list'],
  ['审批流程', 'POST', '/approvalFlow/page', page],
  ['访问日志', 'POST', '/sysLogVis/page', page],
  ['操作日志', 'POST', '/sysLogOp/page', page],
  ['异常日志', 'POST', '/sysLogEx/page', page],
  ['差异日志', 'POST', '/sysLogDiff/page', page],
  ['接口压测目录', 'GET', '/sysCommon/stressTestEndpoints'],
];

const passed = [];
const failed = [];
for (const [name, method, path, body] of checks) {
  try {
    await rawRequest(path, { body, method, token });
    passed.push(name);
  } catch (error) {
    failed.push({ name, reason: error instanceof Error ? error.message : String(error) });
  }
}

console.log(
  JSON.stringify(
    {
      failed,
      passed: passed.length,
      status: failed.length === 0 ? 'passed' : 'failed',
      total: checks.length,
    },
    null,
    2,
  ),
);
if (failed.length > 0) process.exitCode = 1;
