import { access, rm } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { join, resolve, sep } from 'node:path';

const apiBase = process.env.ADMINNET_API_BASE || 'http://localhost:5005/api';
const password = process.env.ADMINNET_TEST_PASSWORD;

if (!password) {
  throw new Error('Missing ADMINNET_TEST_PASSWORD.');
}

const require = createRequire(
  new URL('../../apps/web-antd/package.json', import.meta.url),
);
const { sm2 } = require('sm-crypto-v2');
const { readFile } = await import('node:fs/promises');
const envText = await readFile(
  new URL('../../apps/web-antd/.env.development', import.meta.url),
  'utf8',
);
const publicKey = envText.match(/^VITE_SM_PUBLIC_KEY=(.+)$/m)?.[1]?.trim();

if (!publicKey) {
  throw new Error('VITE_SM_PUBLIC_KEY was not found.');
}

const prefix = `ARTIFACT_${Date.now()}`;
const codeGenRoot = resolve(
  new URL(
    '../../../Admin.NET/Admin.NET.Web.Entry/wwwroot/CodeGen',
    import.meta.url,
  ).pathname.slice(1),
);
const state = {
  approvalFlowId: undefined,
  codeGenId: undefined,
  generatedDirectory: undefined,
  generatedZip: undefined,
  printId: undefined,
  token: undefined,
};
const report = [];

function valueOf(record, key) {
  if (!record || typeof record !== 'object') return undefined;
  return record[key] ?? record[key[0].toUpperCase() + key.slice(1)];
}

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

async function exists(path) {
  try {
    await access(path);
    return true;
  } catch {
    return false;
  }
}

function ensureWithin(root, target) {
  const normalizedRoot = `${resolve(root)}${sep}`.toLowerCase();
  const normalizedTarget = resolve(target).toLowerCase();
  if (!normalizedTarget.startsWith(normalizedRoot)) {
    throw new Error(`Refusing to clean a path outside ${root}.`);
  }
  return target;
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

function get(path) {
  return request(path, { token: state.token });
}

function post(path, body) {
  return request(path, { body, method: 'POST', token: state.token });
}

async function findPaged(path, query, predicate) {
  const page = await post(path, { ...query, page: 1, pageSize: 1000 });
  return (valueOf(page, 'items') || []).find((item) => predicate(item));
}

async function cleanup() {
  const failures = [];
  const attempt = async (label, action) => {
    try {
      await action();
    } catch (error) {
      failures.push(`${label}: ${error.message}`);
    }
  };

  if (state.approvalFlowId) {
    await attempt('approval flow', () =>
      post('/approvalFlow/delete', { id: state.approvalFlowId }),
    );
  }
  if (state.printId) {
    await attempt('print template', () =>
      post('/sysPrint/delete', { id: state.printId }),
    );
  }
  if (state.codeGenId) {
    await attempt('code generation config', () =>
      post('/sysCodeGen/delete', [{ id: state.codeGenId }]),
    );
  }
  if (state.generatedDirectory) {
    await attempt('generated directory', () =>
      rm(ensureWithin(codeGenRoot, state.generatedDirectory), {
        force: true,
        recursive: true,
      }),
    );
  }
  if (state.generatedZip) {
    await attempt('generated zip', () =>
      rm(ensureWithin(codeGenRoot, state.generatedZip), { force: true }),
    );
  }

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

  const tenants = await get('/sysTenant/list');
  const tenant =
    tenants.find((item) =>
      String(valueOf(item, 'label') || '').includes('系统默认'),
    ) || tenants[0];
  const tenantId = Number(valueOf(tenant, 'value'));
  assert(Number.isFinite(tenantId), 'No enabled tenant was available.');

  const printName = `${prefix}_打印模板`;
  const printTemplate = JSON.stringify({
    panels: [
      {
        height: 296.6,
        index: 0,
        paperFooter: 0,
        paperHeader: 0,
        paperType: 'A4',
        printElements: [],
        width: 210,
      },
    ],
  });
  const printPayload = {
    clientServiceAddress: '',
    name: printName,
    orderNo: 990,
    printDataDemo: JSON.stringify({ title: prefix }),
    printParam: '',
    printType: 1,
    remark: prefix,
    status: 1,
    template: printTemplate,
    tenantId,
  };
  await post('/sysPrint/add', printPayload);
  const printRecord = await findPaged(
    '/sysPrint/page',
    { name: printName, tenantId },
    (item) => valueOf(item, 'name') === printName,
  );
  state.printId = Number(valueOf(printRecord, 'id'));
  assert(Number.isFinite(state.printId), 'The print template was not created.');
  await post('/sysPrint/update', {
    ...printPayload,
    id: state.printId,
    remark: `${prefix}_已更新`,
  });
  const updatedPrint = await findPaged(
    '/sysPrint/page',
    { name: printName, tenantId },
    (item) => Number(valueOf(item, 'id')) === state.printId,
  );
  assert(
    valueOf(updatedPrint, 'remark') === `${prefix}_已更新`,
    'The print template update was not returned by the page API.',
  );
  report.push('print add/update/read');

  state.approvalFlowId = Number(
    await post('/approvalFlow/add', {
      code: `${prefix}_FLOW`,
      name: `${prefix}_审批流`,
      remark: prefix,
      status: 1,
    }),
  );
  assert(
    Number.isFinite(state.approvalFlowId),
    'Approval flow add did not return an id.',
  );
  await post('/approvalFlow/update', {
    code: `${prefix}_FLOW`,
    id: state.approvalFlowId,
    name: `${prefix}_审批流_已更新`,
    remark: `${prefix}_已更新`,
    status: 1,
  });
  const databases = await get('/sysDatabase/list');
  const databaseId = String(databases[0] || '');
  assert(databaseId, 'No database was available for approval flow binding.');
  const databaseTables = await get(
    `/sysDatabase/tableList/${encodeURIComponent(databaseId)}`,
  );
  const businessTable = valueOf(databaseTables[0], 'name');
  assert(businessTable, 'No business table was available for flow binding.');
  const formJson = JSON.stringify({
    configId: databaseId,
    tableName: businessTable,
    typeName: 'select',
  });
  const flowJson = JSON.stringify({
    edges: [
      {
        id: `${prefix}_EDGE`,
        sourceNodeId: `${prefix}_START`,
        targetNodeId: `${prefix}_END`,
        type: 'polyline',
      },
    ],
    nodes: [
      {
        id: `${prefix}_START`,
        text: '开始',
        type: 'bpmn:startEvent',
        x: 120,
        y: 120,
      },
      {
        id: `${prefix}_END`,
        text: '结束',
        type: 'bpmn:endEvent',
        x: 360,
        y: 120,
      },
    ],
  });
  await post('/approvalFlow/updateForm', {
    id: state.approvalFlowId,
    json: formJson,
  });
  await post('/approvalFlow/updateFlow', {
    id: state.approvalFlowId,
    json: flowJson,
  });
  const flowDetail = await get(
    `/approvalFlow/detail?id=${state.approvalFlowId}`,
  );
  assert(
    valueOf(flowDetail, 'formJson') === formJson &&
      valueOf(flowDetail, 'flowJson') === flowJson,
    'Approval flow JSON was not persisted.',
  );
  report.push('approval add/update/binding/design/read');

  const databaseConfigs = await get('/sysCodeGen/databaseList');
  const codeDatabase = databaseConfigs[0];
  const codeConfigId = String(valueOf(codeDatabase, 'configId') || '');
  assert(codeConfigId, 'No code generation database was available.');
  const [codeTables, namespaces, existingPage] = await Promise.all([
    get(`/sysCodeGen/tableList/${encodeURIComponent(codeConfigId)}`),
    get('/sysCodeGen/applicationNamespaces'),
    post('/sysCodeGen/page', { page: 1, pageSize: 1000 }),
  ]);
  const existingEntities = new Set(
    (valueOf(existingPage, 'items') || []).map((item) =>
      String(valueOf(item, 'tableName')),
    ),
  );
  let selectedTable;
  for (const table of codeTables) {
    const entityName = String(valueOf(table, 'entityName') || '');
    if (!entityName || existingEntities.has(entityName)) continue;
    const generatedDirectory = join(codeGenRoot, entityName);
    const generatedZip = join(codeGenRoot, `${entityName}.zip`);
    if (!(await exists(generatedDirectory)) && !(await exists(generatedZip))) {
      selectedTable = table;
      state.generatedDirectory = generatedDirectory;
      state.generatedZip = generatedZip;
      break;
    }
  }
  assert(
    selectedTable,
    'No unconfigured entity with a clean generation path was available.',
  );
  const entityName = String(valueOf(selectedTable, 'entityName'));
  const namespace = String(namespaces[0] || '');
  assert(namespace, 'No allowed backend namespace was available.');
  const codePayload = {
    authorName: 'Codex',
    busName: `${prefix}_代码生成`,
    configId: codeConfigId,
    generateMenu: false,
    generateType: '121',
    nameSpace: namespace,
    pagePath: 'validation',
    tableName: entityName,
    tableUniqueList: [],
  };
  await post('/sysCodeGen/add', codePayload);
  const codeRecord = await findPaged(
    '/sysCodeGen/page',
    { busName: prefix },
    (item) => valueOf(item, 'busName') === codePayload.busName,
  );
  state.codeGenId = Number(valueOf(codeRecord, 'id'));
  assert(
    Number.isFinite(state.codeGenId),
    'Code generation config was not created.',
  );
  await post('/sysCodeGen/update', {
    ...codePayload,
    busName: `${prefix}_代码生成_已更新`,
    id: state.codeGenId,
  });
  const fieldConfigs = await get(
    `/sysCodeGenConfig/list?codeGenId=${state.codeGenId}`,
  );
  assert(
    Array.isArray(fieldConfigs) && fieldConfigs.length > 0,
    'Code generation field configs were not created.',
  );
  await post('/sysCodeGenConfig/update', fieldConfigs);
  const preview = await post('/sysCodeGen/preview', { id: state.codeGenId });
  assert(
    preview &&
      typeof preview === 'object' &&
      Object.values(preview).some((content) => String(content).trim()),
    'Code generation preview was empty.',
  );
  const generation = await post('/sysCodeGen/runLocal', {
    id: state.codeGenId,
  });
  const downloadUrl = valueOf(generation, 'url');
  assert(downloadUrl, 'Backend ZIP generation did not return a download URL.');
  const zipResponse = await fetch(downloadUrl);
  const zipBytes = await zipResponse.arrayBuffer();
  assert(
    zipResponse.ok && zipBytes.byteLength > 0,
    'Generated ZIP could not be downloaded.',
  );
  report.push('codegen add/update/fields/preview/backend-zip');
} catch (error) {
  runError = error;
} finally {
  try {
    await cleanup();
  } catch (cleanupError) {
    runError = runError
      ? new AggregateError([runError, cleanupError], 'Run and cleanup failed.')
      : cleanupError;
  }
}

if (runError) throw runError;

console.log(
  JSON.stringify(
    {
      prefix,
      report,
      result: 'passed-and-cleaned',
    },
    null,
    2,
  ),
);
