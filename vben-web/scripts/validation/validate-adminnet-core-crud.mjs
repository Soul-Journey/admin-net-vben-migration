import { readFile } from 'node:fs/promises';
import { createRequire } from 'node:module';

const apiBase = process.env.ADMINNET_API_BASE || 'http://localhost:5005/api';
const password = process.env.ADMINNET_TEST_PASSWORD;

if (!password) {
  throw new Error('Missing ADMINNET_TEST_PASSWORD.');
}

const require = createRequire(
  new URL('../../apps/web-antd/package.json', import.meta.url),
);
const { sm2 } = require('sm-crypto-v2');
const envText = await readFile(
  new URL('../../apps/web-antd/.env.development', import.meta.url),
  'utf8',
);
const publicKey = envText.match(/^VITE_SM_PUBLIC_KEY=(.+)$/m)?.[1]?.trim();

if (!publicKey) {
  throw new Error('VITE_SM_PUBLIC_KEY was not found.');
}

const prefix = `CRUD_${Date.now()}`;
const state = {
  dictDataIds: [],
  dictTypeId: undefined,
  menuId: undefined,
  orgId: undefined,
  posIds: [],
  roleId: undefined,
  token: undefined,
};
const report = [];

function valueOf(record, key) {
  if (!record || typeof record !== 'object') return undefined;
  return record[key] ?? record[key[0].toUpperCase() + key.slice(1)];
}

function flatten(items = []) {
  return items.flatMap((item) => [
    item,
    ...flatten(valueOf(item, 'children') || []),
  ]);
}

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

async function request(path, { body, method = 'GET', token } = {}) {
  const response = await fetch(`${apiBase}${path}`, {
    body: body === undefined ? undefined : JSON.stringify(body),
    headers: {
      Accept: 'application/json',
      ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    method,
  });
  const payload = await response.json().catch(() => ({}));
  const code = valueOf(payload, 'code');
  if (!response.ok || (code !== undefined && code !== 200)) {
    throw new Error(
      `${method} ${path} failed: ${
        valueOf(payload, 'message') || response.status
      }`,
    );
  }
  return code === undefined ? payload : valueOf(payload, 'result');
}

function get(path, token) {
  return request(path, { token });
}

function post(path, body, token) {
  return request(path, { body, method: 'POST', token });
}

async function findPaged(path, query, predicate) {
  const page = await post(
    path,
    { ...query, page: 1, pageSize: 50 },
    state.token,
  );
  return (valueOf(page, 'items') || []).find((item) => predicate(item));
}

async function cleanup() {
  const failures = [];
  const remove = async (label, path, id) => {
    if (!id) return;
    try {
      await post(path, { id }, state.token);
    } catch (error) {
      failures.push(`${label}: ${error.message}`);
    }
  };

  for (const id of state.dictDataIds.toReversed()) {
    await remove('dict data', '/sysDictData/delete', id);
  }
  await remove('dict type', '/sysDictType/delete', state.dictTypeId);
  await remove('role', '/sysRole/delete', state.roleId);
  for (const id of state.posIds.toReversed()) {
    await remove('position', '/sysPos/delete', id);
  }
  await remove('organization', '/sysOrg/delete', state.orgId);
  await remove('menu', '/sysMenu/delete', state.menuId);

  if (failures.length > 0) {
    throw new Error(`Cleanup failed:\n${failures.join('\n')}`);
  }
}

let runError;
try {
  const login = await post('/sysAuth/login', {
    account: 'superadmin',
    code: null,
    codeId: 0,
    password: sm2.doEncrypt(password, publicKey, 1),
    tenantId: -1,
  });
  state.token = valueOf(login, 'accessToken');
  assert(state.token, 'Login did not return an access token.');
  report.push('login');

  const tenants = await get('/sysTenant/list', state.token);
  const tenant =
    tenants.find((item) =>
      String(valueOf(item, 'label') || '').includes('系统默认'),
    ) || tenants[0];
  const tenantId = Number(valueOf(tenant, 'value'));
  assert(Number.isFinite(tenantId), 'No enabled tenant was available.');

  const initialMenus = await get(
    `/sysMenu/list?TenantId=${tenantId}`,
    state.token,
  );
  const menuParent = flatten(initialMenus).find(
    (item) => valueOf(item, 'path') === '/platform/menu',
  );
  assert(menuParent, 'The menu management parent was not found.');

  const permission = `validation:${prefix.toLowerCase()}:read`;
  state.menuId = Number(
    await post(
      '/sysMenu/add',
      {
        isAffix: false,
        isHide: false,
        isIframe: false,
        isKeepAlive: true,
        orderNo: 990,
        permission,
        pid: valueOf(menuParent, 'id'),
        status: 1,
        tenantId,
        title: `${prefix}_按钮`,
        type: 3,
      },
      state.token,
    ),
  );
  assert(Number.isFinite(state.menuId), 'Menu add did not return an id.');
  await post(
    '/sysMenu/update',
    {
      id: state.menuId,
      isAffix: false,
      isHide: false,
      isIframe: false,
      isKeepAlive: true,
      orderNo: 991,
      permission,
      pid: valueOf(menuParent, 'id'),
      status: 1,
      tenantId,
      title: `${prefix}_按钮_已更新`,
      type: 3,
    },
    state.token,
  );
  const updatedMenus = flatten(
    await get(`/sysMenu/list?TenantId=${tenantId}`, state.token),
  );
  assert(
    updatedMenus.some(
      (item) =>
        Number(valueOf(item, 'id')) === state.menuId &&
        valueOf(item, 'title') === `${prefix}_按钮_已更新`,
    ),
    'Updated menu was not returned by the list API.',
  );
  report.push('menu add/update');

  const orgTree = await get(
    `/sysOrg/list?Id=0&TenantId=${tenantId}`,
    state.token,
  );
  const rootOrg =
    flatten(orgTree).find(
      (item) =>
        Number(valueOf(item, 'tenantId')) === tenantId &&
        Number(valueOf(item, 'pid')) === 0,
    ) || flatten(orgTree)[0];
  assert(rootOrg, 'The tenant root organization was not found.');

  state.orgId = Number(
    await post(
      '/sysOrg/add',
      {
        code: `${prefix}_ORG`,
        name: `${prefix}_机构`,
        orderNo: 990,
        pid: valueOf(rootOrg, 'id'),
        remark: prefix,
        status: 1,
        tenantId,
      },
      state.token,
    ),
  );
  assert(
    Number.isFinite(state.orgId),
    'Organization add did not return an id.',
  );
  await post(
    '/sysOrg/update',
    {
      code: `${prefix}_ORG`,
      id: state.orgId,
      name: `${prefix}_机构_已更新`,
      orderNo: 991,
      pid: valueOf(rootOrg, 'id'),
      remark: prefix,
      status: 1,
      tenantId,
    },
    state.token,
  );
  const updatedOrgs = flatten(
    await get(`/sysOrg/list?Id=0&TenantId=${tenantId}`, state.token),
  );
  assert(
    updatedOrgs.some(
      (item) =>
        Number(valueOf(item, 'id')) === state.orgId &&
        valueOf(item, 'name') === `${prefix}_机构_已更新`,
    ),
    'Updated organization was not returned by the list API.',
  );
  report.push('organization add/update');

  await post(
    '/sysPos/add',
    {
      code: `${prefix}_POS`,
      name: `${prefix}_职位`,
      orderNo: 990,
      remark: prefix,
      status: 1,
      tenantId,
    },
    state.token,
  );
  let positions = await get(
    `/sysPos/list?TenantId=${tenantId}&Code=${encodeURIComponent(
      `${prefix}_POS`,
    )}`,
    state.token,
  );
  let position = positions.find(
    (item) => valueOf(item, 'code') === `${prefix}_POS`,
  );
  assert(position, 'Added position was not returned by the list API.');
  state.posIds.push(Number(valueOf(position, 'id')));
  await post(
    '/sysPos/update',
    {
      ...position,
      id: state.posIds[0],
      name: `${prefix}_职位_已更新`,
      orderNo: 991,
      status: 1,
      tenantId,
    },
    state.token,
  );
  positions = await get(
    `/sysPos/list?TenantId=${tenantId}&Code=${encodeURIComponent(
      `${prefix}_POS`,
    )}`,
    state.token,
  );
  position = positions.find(
    (item) => Number(valueOf(item, 'id')) === state.posIds[0],
  );
  assert(
    valueOf(position, 'name') === `${prefix}_职位_已更新`,
    'Updated position was not returned by the list API.',
  );
  await post(
    '/sysPos/add',
    {
      code: `${prefix}_POS_COPY`,
      name: `${prefix}_职位_复制`,
      orderNo: 992,
      remark: prefix,
      status: 1,
      tenantId,
    },
    state.token,
  );
  positions = await get(
    `/sysPos/list?TenantId=${tenantId}&Code=${encodeURIComponent(
      `${prefix}_POS_COPY`,
    )}`,
    state.token,
  );
  const copiedPosition = positions.find(
    (item) => valueOf(item, 'code') === `${prefix}_POS_COPY`,
  );
  assert(copiedPosition, 'Copied position was not returned by the list API.');
  state.posIds.push(Number(valueOf(copiedPosition, 'id')));
  report.push('position add/update/copy');

  await post(
    '/sysRole/add',
    {
      code: `${prefix}_ROLE`,
      menuIdList: [state.menuId],
      name: `${prefix}_角色`,
      orderNo: 990,
      remark: prefix,
      status: 1,
      tenantId,
    },
    state.token,
  );
  let role = await findPaged(
    '/sysRole/page',
    { code: `${prefix}_ROLE`, tenantId },
    (item) => valueOf(item, 'code') === `${prefix}_ROLE`,
  );
  assert(role, 'Added role was not returned by the page API.');
  state.roleId = Number(valueOf(role, 'id'));
  const ownMenuIds = await get(
    `/sysRole/ownMenuList?Id=${state.roleId}`,
    state.token,
  );
  assert(
    ownMenuIds.map(Number).includes(state.menuId),
    'Role menu authorization did not include the test menu.',
  );
  await post(
    '/sysRole/grantDataScope',
    {
      dataScope: 5,
      id: state.roleId,
      orgIdList: [state.orgId],
      tenantId,
    },
    state.token,
  );
  const ownOrgIds = await get(
    `/sysRole/ownOrgList?Id=${state.roleId}`,
    state.token,
  );
  assert(
    ownOrgIds.map(Number).includes(state.orgId),
    'Role data scope did not include the test organization.',
  );
  await post(
    '/sysRole/update',
    {
      ...role,
      code: `${prefix}_ROLE`,
      id: state.roleId,
      menuIdList: [state.menuId],
      name: `${prefix}_角色_已更新`,
      orderNo: 991,
      status: 1,
      tenantId,
    },
    state.token,
  );
  role = await findPaged(
    '/sysRole/page',
    { code: `${prefix}_ROLE`, tenantId },
    (item) => Number(valueOf(item, 'id')) === state.roleId,
  );
  assert(
    valueOf(role, 'name') === `${prefix}_角色_已更新`,
    'Updated role was not returned by the page API.',
  );
  report.push('role add/menu grant/data grant/update');

  await post(
    '/sysDictType/add',
    {
      code: `${prefix}_DICT`,
      name: `${prefix}_字典`,
      orderNo: 990,
      remark: prefix,
      status: 1,
      sysFlag: 2,
    },
    state.token,
  );
  const dictType = await findPaged(
    '/sysDictType/page',
    { code: `${prefix}_DICT` },
    (item) => valueOf(item, 'code') === `${prefix}_DICT`,
  );
  assert(dictType, 'Added dictionary type was not returned by the page API.');
  state.dictTypeId = Number(valueOf(dictType, 'id'));
  await post(
    '/sysDictType/update',
    {
      ...dictType,
      id: state.dictTypeId,
      name: `${prefix}_字典_已更新`,
      orderNo: 991,
      status: 1,
      sysFlag: 2,
    },
    state.token,
  );

  await post(
    '/sysDictData/add',
    {
      dictTypeId: state.dictTypeId,
      label: `${prefix}_值`,
      orderNo: 990,
      status: 1,
      tagType: 'success',
      value: '1',
    },
    state.token,
  );
  let dictData = await findPaged(
    '/sysDictData/page',
    { dictTypeId: state.dictTypeId, label: prefix },
    (item) => String(valueOf(item, 'value')) === '1',
  );
  assert(dictData, 'Added dictionary value was not returned by the page API.');
  state.dictDataIds.push(Number(valueOf(dictData, 'id')));
  await post(
    '/sysDictData/update',
    {
      ...dictData,
      dictTypeId: state.dictTypeId,
      id: state.dictDataIds[0],
      label: `${prefix}_值_已更新`,
      orderNo: 991,
      status: 1,
      tagType: 'warning',
      value: '1',
    },
    state.token,
  );
  await post(
    '/sysDictData/add',
    {
      dictTypeId: state.dictTypeId,
      label: `${prefix}_值_复制`,
      orderNo: 992,
      status: 1,
      tagType: 'info',
      value: '2',
    },
    state.token,
  );
  dictData = await findPaged(
    '/sysDictData/page',
    { dictTypeId: state.dictTypeId, label: prefix },
    (item) => String(valueOf(item, 'value')) === '2',
  );
  assert(dictData, 'Copied dictionary value was not returned by the page API.');
  state.dictDataIds.push(Number(valueOf(dictData, 'id')));
  const cachedValues = await get(
    `/sysDictData/dataList?Code=${encodeURIComponent(`${prefix}_DICT`)}`,
    state.token,
  );
  assert(
    cachedValues.length === 2,
    'Dictionary cache/list API did not return both test values.',
  );
  report.push('dictionary type/value add/update/copy/cache');
} catch (error) {
  runError = error;
} finally {
  try {
    await cleanup();
  } catch (cleanupError) {
    runError = runError
      ? new AggregateError(
          [runError, cleanupError],
          'Validation and cleanup both failed.',
        )
      : cleanupError;
  }
}

if (runError) {
  throw runError;
}

console.log(
  JSON.stringify(
    {
      completed: report,
      prefix,
      result: 'passed-and-cleaned',
    },
    null,
    2,
  ),
);
