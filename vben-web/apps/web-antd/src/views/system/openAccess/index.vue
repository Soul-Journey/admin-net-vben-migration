<script setup lang="ts">
import type { FormInstance, TableColumnsType } from 'ant-design-vue';
import type { Rule } from 'ant-design-vue/es/form';

import type {
  AddOpenAccessParams,
  OpenAccessRecord,
  OpenAccessUserOption,
  SysTenantRecord,
} from '#/api';

import { computed, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';
import { IconifyIcon } from '@vben/icons';

import {
  Alert,
  Button,
  Descriptions,
  Form,
  Input,
  message,
  Modal,
  Popover,
  Select,
  Space,
  Table,
  Tabs,
  Tag,
  Tooltip,
} from 'ant-design-vue';

import {
  addOpenAccessApi,
  createOpenAccessSecretApi,
  deleteOpenAccessApi,
  generateStoredSignatureApi,
  listOpenAccessUsersApi,
  pageOpenAccessApi,
  pageTenantsApi,
  rotateOpenAccessSecretApi,
  updateOpenAccessApi,
} from '#/api';
import { ADMIN_PAGINATION_PROPS } from '#/utils/pagination';

defineOptions({ name: 'AdminNetSystemOpenAccess' });

type IdentityFormState = {
  accessKey?: string;
  accessSecret?: string;
  bindTenantId?: number;
  bindUserId?: number;
  id?: number;
};

const HTTP_METHODS = [
  { label: 'GET', value: 0 },
  { label: 'POST', value: 1 },
  { label: 'PUT', value: 2 },
  { label: 'DELETE', value: 3 },
  { label: 'PATCH', value: 4 },
  { label: 'HEAD', value: 5 },
  { label: 'OPTIONS', value: 6 },
];

const { hasAccessByCodes } = useAccess();
const loading = ref(false);
const submitLoading = ref(false);
const userLoading = ref(false);
const secretLoading = ref(false);
const signLoading = ref(false);
const identityOpen = ref(false);
const signOpen = ref(false);
const helpOpen = ref(false);
const oneTimeSecretOpen = ref(false);
const identityFormRef = ref<FormInstance>();
const records = ref<OpenAccessRecord[]>([]);
const tenants = ref<SysTenantRecord[]>([]);
const users = ref<OpenAccessUserOption[]>([]);
const oneTimeSecret = ref('');
const identityForm = reactive<IdentityFormState>({});
const signForm = reactive({
  accessKey: '',
  id: 0,
  method: 0,
  nonce: '',
  sign: '',
  timestamp: undefined as number | undefined,
  url: '',
});

const query = reactive({
  accessKey: '',
  page: 1,
  pageSize: 50,
  total: 0,
});

const identityTitle = computed(() =>
  identityForm.id ? '编辑开放接口身份' : '新增开放接口身份',
);
const tenantOptions = computed(() =>
  tenants.value.map((item) => ({
    label: item.host ? `${item.name} (${item.host})` : item.name,
    value: item.id,
  })),
);
const userOptions = computed(() =>
  users.value.map((item) => ({
    label: item.realName ? `${item.account}（${item.realName}）` : item.account,
    value: item.id,
  })),
);

const columns: TableColumnsType<OpenAccessRecord> = [
  { key: 'index', title: '序号', width: 58 },
  { dataIndex: 'accessKey', key: 'accessKey', title: '身份标识', width: 240 },
  { key: 'secret', title: '密钥', width: 150 },
  {
    dataIndex: 'bindUserAccount',
    key: 'bindUserAccount',
    title: '绑定账号',
    width: 160,
  },
  {
    dataIndex: 'bindTenantName',
    key: 'bindTenantName',
    title: '绑定租户',
    width: 210,
  },
  { key: 'modifyRecord', title: '修改记录', width: 104 },
  { fixed: 'right', key: 'actions', title: '操作', width: 260 },
];

const identityRules: Record<string, Rule[]> = {
  accessKey: [
    { message: '请输入身份标识', required: true, trigger: 'blur' },
    { max: 128, message: '身份标识不能超过 128 个字符', trigger: 'blur' },
  ],
  accessSecret: [
    {
      async validator() {
        if (!identityForm.id && !identityForm.accessSecret?.trim()) {
          throw new Error('请生成或输入密钥');
        }
      },
      trigger: 'change',
    },
  ],
  bindTenantId: [
    {
      message: '请选择绑定租户',
      required: true,
      trigger: 'change',
      type: 'number',
    },
  ],
  bindUserId: [
    {
      message: '请选择绑定用户',
      required: true,
      trigger: 'change',
      type: 'number',
    },
  ],
};

function can(code: string) {
  return hasAccessByCodes([code]);
}

function asIdentity(value: unknown) {
  return value as OpenAccessRecord;
}

function clearIdentityForm() {
  for (const key of Object.keys(identityForm)) {
    delete identityForm[key as keyof IdentityFormState];
  }
}

async function copyText(value: string, label = '内容') {
  try {
    await navigator.clipboard.writeText(value);
    message.success(`${label}已复制`);
  } catch {
    message.warning('浏览器未允许自动复制，请手动选择复制');
  }
}

async function loadRecords() {
  if (!can('sysOpenAccess:page')) return;
  loading.value = true;
  try {
    const result = await pageOpenAccessApi({
      accessKey: query.accessKey.trim() || undefined,
      page: query.page,
      pageSize: query.pageSize,
    });
    records.value = result.items ?? [];
    query.total = Number(result.total ?? 0);
  } finally {
    loading.value = false;
  }
}

async function loadTenants() {
  const result = await pageTenantsApi({ page: 1, pageSize: 10_000 });
  tenants.value = result.items ?? [];
}

async function loadUsers(tenantId?: number, clearUser = true) {
  users.value = [];
  if (clearUser) identityForm.bindUserId = undefined;
  if (!tenantId) return;
  userLoading.value = true;
  try {
    users.value = await listOpenAccessUsersApi(tenantId);
  } finally {
    userLoading.value = false;
  }
}

async function handleQuery() {
  query.page = 1;
  await loadRecords();
}

async function resetQuery() {
  query.accessKey = '';
  await handleQuery();
}

async function openIdentity(record?: OpenAccessRecord) {
  clearIdentityForm();
  Object.assign(
    identityForm,
    record
      ? {
          accessKey: record.accessKey,
          bindTenantId: record.bindTenantId,
          bindUserId: record.bindUserId,
          id: record.id,
        }
      : {},
  );
  identityOpen.value = true;
  await loadUsers(identityForm.bindTenantId, !record);
  requestAnimationFrame(() => identityFormRef.value?.clearValidate());
}

async function createSecret() {
  secretLoading.value = true;
  try {
    identityForm.accessSecret = await createOpenAccessSecretApi();
    await identityFormRef.value?.validateFields(['accessSecret']);
  } finally {
    secretLoading.value = false;
  }
}

async function saveIdentity() {
  await identityFormRef.value?.validate();
  const accessKey = identityForm.accessKey?.trim();
  const accessSecret = identityForm.accessSecret?.trim();
  const bindTenantId = identityForm.bindTenantId;
  const bindUserId = identityForm.bindUserId;
  if (
    !accessKey ||
    bindTenantId === undefined ||
    bindUserId === undefined ||
    (!identityForm.id && !accessSecret)
  ) {
    message.warning('请补全开放接口身份信息');
    return;
  }

  submitLoading.value = true;
  try {
    if (identityForm.id) {
      await updateOpenAccessApi({
        accessKey,
        bindTenantId,
        bindUserId,
        id: identityForm.id,
      });
      message.success('开放接口身份已更新，原密钥保持不变');
    } else {
      await addOpenAccessApi({
        accessKey,
        accessSecret,
        bindTenantId,
        bindUserId,
      } as AddOpenAccessParams);
      message.success('开放接口身份已创建');
    }
    identityOpen.value = false;
    await loadRecords();
  } finally {
    submitLoading.value = false;
  }
}

function removeIdentity(record: OpenAccessRecord) {
  Modal.confirm({
    cancelText: '取消',
    centered: true,
    content: `删除后，使用身份标识“${record.accessKey}”签名的外部系统会立即无法访问接口。`,
    okButtonProps: { danger: true },
    okText: '确认删除',
    title: '删除开放接口身份？',
    async onOk() {
      await deleteOpenAccessApi(record.id);
      message.success('开放接口身份已删除');
      if (records.value.length === 1 && query.page > 1) query.page -= 1;
      await loadRecords();
    },
  });
}

function rotateSecret(record: OpenAccessRecord) {
  Modal.confirm({
    cancelText: '取消',
    centered: true,
    content: `轮换后旧密钥立即失效，使用“${record.accessKey}”的外部系统必须同步更新配置。新密钥关闭后不能再次查看。`,
    okButtonProps: { danger: true },
    okText: '确认轮换',
    title: '轮换访问密钥？',
    async onOk() {
      oneTimeSecret.value = await rotateOpenAccessSecretApi(record.id);
      oneTimeSecretOpen.value = true;
    },
  });
}

function randomNonce() {
  const bytes = new Uint32Array(1);
  crypto.getRandomValues(bytes);
  signForm.nonce = String((bytes[0] ?? 0) % 1_000_000).padStart(6, '0');
  signForm.sign = '';
}

function currentTimestamp() {
  signForm.timestamp = Math.floor(Date.now() / 1000);
  signForm.sign = '';
}

function openSignature(record: OpenAccessRecord) {
  Object.assign(signForm, {
    accessKey: record.accessKey,
    id: record.id,
    method: 0,
    nonce: '',
    sign: '',
    timestamp: undefined,
    url: '',
  });
  currentTimestamp();
  randomNonce();
  signOpen.value = true;
}

async function generateSignature() {
  const url = signForm.url.trim();
  if (!url.startsWith('/') || url.includes('://') || url.includes('?')) {
    message.warning(
      '接口地址应为不含域名和查询参数的路径，例如 /api/demo/helloWord',
    );
    return;
  }
  if (!signForm.timestamp || !/^\d+$/.test(String(signForm.timestamp))) {
    message.warning('请输入有效的秒级时间戳');
    return;
  }
  if (!signForm.nonce.trim()) {
    message.warning('请输入随机数');
    return;
  }
  signLoading.value = true;
  try {
    signForm.sign = await generateStoredSignatureApi({
      id: signForm.id,
      method: signForm.method,
      nonce: signForm.nonce.trim(),
      timestamp: signForm.timestamp,
      url,
    });
  } finally {
    signLoading.value = false;
  }
}

async function initialize() {
  await Promise.all([loadTenants(), loadRecords()]);
}

onMounted(initialize);
</script>

<template>
  <div class="open-access-page">
    <section class="content-panel">
      <header class="panel-header">
        <div>
          <h2>开放接口</h2>
          <p>管理外部系统签名身份及其绑定的租户用户上下文</p>
        </div>
        <Space>
          <Button @click="helpOpen = true">
            <template #icon><IconifyIcon icon="lucide:circle-help" /></template>
            新手使用指南
          </Button>
          <Button
            v-if="can('sysOpenAccess:add')"
            type="primary"
            @click="openIdentity()"
          >
            <template #icon><IconifyIcon icon="lucide:plus" /></template>
            新增身份
          </Button>
        </Space>
      </header>

      <Alert
        banner
        class="security-alert"
        message="签名通过后，请求会以绑定用户的权限执行。密钥不在列表中返回，轮换后旧密钥立即失效。"
        show-icon
        type="warning"
      />

      <div class="query-bar">
        <span>身份标识</span>
        <Input
          v-model:value="query.accessKey"
          allow-clear
          placeholder="请输入身份标识"
          @press-enter="handleQuery"
        />
        <Button
          v-if="can('sysOpenAccess:page')"
          type="primary"
          @click="handleQuery"
        >
          <template #icon><IconifyIcon icon="lucide:search" /></template>
          查询
        </Button>
        <Button @click="resetQuery">
          <template #icon><IconifyIcon icon="lucide:rotate-ccw" /></template>
          重置
        </Button>
      </div>

      <Table
        :columns="columns"
        :data-source="records"
        :loading="loading"
        :pagination="{
          ...ADMIN_PAGINATION_PROPS,
          current: query.page,
          pageSize: query.pageSize,
          total: query.total,
        }"
        :scroll="{ x: 1180 }"
        row-key="id"
        size="middle"
        @change="
          (pagination) => {
            query.page = pagination.current || 1;
            query.pageSize = pagination.pageSize || 50;
            loadRecords();
          }
        "
      >
        <template #bodyCell="{ column, index, record }">
          <template v-if="column.key === 'index'">
            {{ (query.page - 1) * query.pageSize + index + 1 }}
          </template>
          <template v-else-if="column.key === 'accessKey'">
            <div class="identity-key">
              <IconifyIcon icon="lucide:key-round" />
              <span>{{ asIdentity(record).accessKey }}</span>
            </div>
          </template>
          <template v-else-if="column.key === 'secret'">
            <Tooltip title="密钥不会从列表接口返回">
              <Tag color="default">
                <IconifyIcon icon="lucide:shield-check" /> 已保护
              </Tag>
            </Tooltip>
          </template>
          <template v-else-if="column.key === 'modifyRecord'">
            <Popover
              overlay-class-name="open-access-record-popover"
              placement="bottom"
              trigger="hover"
            >
              <template #content>
                <Descriptions
                  :column="2"
                  bordered
                  layout="vertical"
                  size="small"
                >
                  <Descriptions.Item label="创建者">
                    {{ asIdentity(record).createUserName || '无' }}
                  </Descriptions.Item>
                  <Descriptions.Item label="创建时间">
                    {{ asIdentity(record).createTime || '无' }}
                  </Descriptions.Item>
                  <Descriptions.Item label="修改者">
                    {{ asIdentity(record).updateUserName || '无' }}
                  </Descriptions.Item>
                  <Descriptions.Item label="修改时间">
                    {{ asIdentity(record).updateTime || '无' }}
                  </Descriptions.Item>
                </Descriptions>
              </template>
              <Button type="link">
                <template #icon>
                  <IconifyIcon icon="lucide:circle-dot" />
                </template>
                详情
              </Button>
            </Popover>
          </template>
          <template v-else-if="column.key === 'actions'">
            <Space :size="1">
              <Tooltip title="生成当前请求的 HMAC-SHA256 签名">
                <Button type="link" @click="openSignature(asIdentity(record))">
                  <template #icon>
                    <IconifyIcon icon="lucide:fingerprint" />
                  </template>
                  生成签名
                </Button>
              </Tooltip>
              <Button
                v-if="can('sysOpenAccess:update')"
                type="link"
                @click="openIdentity(asIdentity(record))"
              >
                <template #icon>
                  <IconifyIcon icon="lucide:square-pen" />
                </template>
                编辑
              </Button>
              <Popover placement="bottomRight" trigger="click">
                <template #content>
                  <div class="action-menu">
                    <Button
                      v-if="can('sysOpenAccess:update')"
                      type="text"
                      @click="rotateSecret(asIdentity(record))"
                    >
                      <template #icon>
                        <IconifyIcon icon="lucide:refresh-cw-key" />
                      </template>
                      轮换密钥
                    </Button>
                    <Button
                      v-if="can('sysOpenAccess:delete')"
                      danger
                      type="text"
                      @click="removeIdentity(asIdentity(record))"
                    >
                      <template #icon>
                        <IconifyIcon icon="lucide:trash-2" />
                      </template>
                      删除身份
                    </Button>
                  </div>
                </template>
                <Button type="text" aria-label="更多操作">
                  <IconifyIcon icon="lucide:ellipsis" />
                </Button>
              </Popover>
            </Space>
          </template>
        </template>
      </Table>
    </section>

    <Modal
      v-model:open="identityOpen"
      :confirm-loading="submitLoading"
      :mask-closable="false"
      :title="identityTitle"
      :width="640"
      centered
      cancel-text="取消"
      ok-text="保存"
      @ok="saveIdentity"
    >
      <Alert
        class="modal-alert"
        message="签名认证成功后，接口将获得所选用户的身份与权限，请使用专用的最小权限账号。"
        show-icon
        type="info"
      />
      <Form
        ref="identityFormRef"
        :model="identityForm"
        :rules="identityRules"
        layout="vertical"
      >
        <Form.Item label="身份标识" name="accessKey">
          <Input
            v-model:value="identityForm.accessKey"
            :maxlength="128"
            placeholder="例如 partner-order-service"
          />
          <div class="field-help">
            相当于外部系统的账号，建议用“系统名-用途”命名，创建后不要频繁修改。
          </div>
        </Form.Item>
        <Form.Item v-if="!identityForm.id" label="访问密钥" name="accessSecret">
          <Input.Password
            v-model:value="identityForm.accessSecret"
            placeholder="请生成安全密钥"
          >
            <template #addonAfter>
              <Space :size="0">
                <Button
                  :loading="secretLoading"
                  type="text"
                  @click="createSecret"
                >
                  生成
                </Button>
                <Button
                  :disabled="!identityForm.accessSecret"
                  type="text"
                  @click="copyText(identityForm.accessSecret || '', '密钥')"
                >
                  复制
                </Button>
              </Space>
            </template>
          </Input.Password>
          <div class="field-help">
            保存后密钥不会再次显示；请先安全保存，遗失后只能轮换。
          </div>
        </Form.Item>
        <div v-else class="secret-preserved">
          <IconifyIcon icon="lucide:shield-check" />
          编辑身份不会改变现有密钥；需要更换时请使用“轮换密钥”。
        </div>
        <div class="form-grid">
          <Form.Item label="绑定租户" name="bindTenantId">
            <Select
              v-model:value="identityForm.bindTenantId"
              :options="tenantOptions"
              allow-clear
              show-search
              placeholder="请选择租户"
              @change="(value) => loadUsers(Number(value) || undefined)"
            />
            <div class="field-help">决定这套身份访问哪个租户的数据。</div>
          </Form.Item>
          <Form.Item label="绑定用户" name="bindUserId">
            <Select
              v-model:value="identityForm.bindUserId"
              :disabled="!identityForm.bindTenantId"
              :loading="userLoading"
              :options="userOptions"
              allow-clear
              show-search
              placeholder="请选择专用账号"
            />
            <div class="field-help">
              决定外部请求最终拥有哪些菜单、按钮和数据权限，建议绑定专用低权限账号。
            </div>
          </Form.Item>
        </div>
      </Form>
    </Modal>

    <Modal
      v-model:open="signOpen"
      :footer="null"
      :width="680"
      centered
      title="生成请求签名"
    >
      <Alert
        message="签名只对当前方法、路径、时间戳和随机数组合有效，密钥不会返回浏览器。"
        show-icon
        type="info"
      />
      <Form class="signature-form" layout="vertical">
        <Form.Item label="身份标识">
          <Input :value="signForm.accessKey" disabled />
        </Form.Item>
        <Form.Item label="请求接口路径">
          <Input v-model:value="signForm.url" placeholder="/api/demo/helloWord">
            <template #addonBefore>
              <Select
                v-model:value="signForm.method"
                :options="HTTP_METHODS"
                class="method-select"
              />
            </template>
          </Input>
        </Form.Item>
        <div class="form-grid">
          <Form.Item label="秒级时间戳">
            <Input
              v-model:value="signForm.timestamp"
              @change="signForm.sign = ''"
            >
              <template #addonAfter>
                <Button type="text" @click="currentTimestamp">
                  取当前值
                </Button>
              </template>
            </Input>
          </Form.Item>
          <Form.Item label="随机数">
            <Input v-model:value="signForm.nonce" @change="signForm.sign = ''">
              <template #addonAfter>
                <Button type="text" @click="randomNonce"> 重新生成 </Button>
              </template>
            </Input>
          </Form.Item>
        </div>
        <Button
          :loading="signLoading"
          block
          type="primary"
          @click="generateSignature"
        >
          <template #icon><IconifyIcon icon="lucide:fingerprint" /></template>
          生成签名
        </Button>
        <Form.Item
          v-if="signForm.sign"
          class="signature-result"
          label="签名结果"
        >
          <Input.TextArea
            :auto-size="{ minRows: 2, maxRows: 4 }"
            :value="signForm.sign"
            readonly
          />
          <Button class="copy-sign" @click="copyText(signForm.sign, '签名')">
            <template #icon><IconifyIcon icon="lucide:copy" /></template>
            复制签名
          </Button>
        </Form.Item>
      </Form>
    </Modal>

    <Modal
      v-model:open="oneTimeSecretOpen"
      :footer="null"
      :mask-closable="false"
      :width="560"
      centered
      title="新密钥（仅显示一次）"
    >
      <Alert
        message="旧密钥已经失效。关闭窗口前，请将新密钥保存到外部系统的安全配置中。"
        show-icon
        type="warning"
      />
      <div class="one-time-secret">
        <Input.Password :value="oneTimeSecret" readonly />
        <Button type="primary" @click="copyText(oneTimeSecret, '新密钥')">
          <template #icon><IconifyIcon icon="lucide:copy" /></template>
          复制新密钥
        </Button>
      </div>
    </Modal>

    <Modal
      v-model:open="helpOpen"
      :footer="null"
      :width="820"
      centered
      title="开放接口新手使用指南"
    >
      <div class="help-content">
        <Alert
          message="简单理解：开放接口身份是一张给外部服务器使用的门禁卡，不是给普通用户登录后台用的账号。"
          show-icon
          type="info"
        />
        <Tabs default-active-key="overview">
          <Tabs.TabPane key="overview" tab="先弄懂它">
            <h3>什么时候需要它？</h3>
            <p>
              当 ERP、商城、支付服务、定时脚本等外部服务器需要自动调用 Admin.NET
              接口，又不适合人工登录时使用。普通后台用户登录、浏览器页面调用，不需要创建开放接口身份。
            </p>
            <Descriptions :column="1" bordered size="small">
              <Descriptions.Item label="身份标识 accessKey">
                外部系统的公开账号，可以告诉对接方。
              </Descriptions.Item>
              <Descriptions.Item label="访问密钥 accessSecret">
                相当于密码，只在外部服务器安全保存，不能放进网页、App、URL
                或日志。
              </Descriptions.Item>
              <Descriptions.Item label="绑定租户">
                决定外部请求可以访问哪个租户的数据。
              </Descriptions.Item>
              <Descriptions.Item label="绑定用户">
                决定外部请求拥有哪些业务权限。建议先创建一个专用低权限账号，再绑定它。
              </Descriptions.Item>
              <Descriptions.Item label="签名 sign">
                外部服务器用密钥算出的“一次性证明”，Admin.NET
                用它确认请求没有被伪造。
              </Descriptions.Item>
            </Descriptions>
            <Alert
              class="help-alert"
              message="绑定用户的权限就是外部系统的权限。不要为了省事绑定超级管理员。"
              show-icon
              type="warning"
            />
          </Tabs.TabPane>

          <Tabs.TabPane key="steps" tab="一步步配置">
            <ol class="guide-steps">
              <li>
                <strong>准备专用账号：</strong
                >在账号管理中创建一个只拥有必要接口权限的账号，例如“ERP同步账号”。
              </li>
              <li>
                <strong>新增身份：</strong
                >填写容易识别的身份标识，选择该账号所属租户，再选择专用账号。
              </li>
              <li>
                <strong>生成并保存密钥：</strong
                >点击“生成”，立刻保存到外部服务器的环境变量或密钥管理系统。保存身份后，后台不再显示密钥。
              </li>
              <li>
                <strong>开发签名：</strong
                >外部服务器每次请求都生成当前秒级时间戳、全新随机数和签名，并放入请求头。
              </li>
              <li>
                <strong>联调验证：</strong
                >可用列表中的“生成签名”检查单次请求参数是否正确，但正式系统应在自己的服务器内计算签名。
              </li>
              <li>
                <strong>上线维护：</strong
                >怀疑密钥泄露时使用“轮换密钥”；不再合作时删除身份。两种操作都会让旧配置立即失效。
              </li>
            </ol>
          </Tabs.TabPane>

          <Tabs.TabPane key="request" tab="请求怎么发">
            <h3>每次请求必须携带 4 个请求头</h3>
            <Descriptions :column="1" bordered size="small">
              <Descriptions.Item label="accessKey">
                后台创建的身份标识
              </Descriptions.Item>
              <Descriptions.Item label="timestamp">
                当前 Unix 秒级时间戳，例如 1784268000
              </Descriptions.Item>
              <Descriptions.Item label="nonce">
                本次请求唯一的随机数，例如 483921；相同值不能重复使用
              </Descriptions.Item>
              <Descriptions.Item label="sign">
                使用访问密钥计算出的 HMAC-SHA256 Base64 结果
              </Descriptions.Item>
            </Descriptions>
            <h3>签名原文</h3>
            <code
              >大写请求方法&amp;接口路径&amp;accessKey&amp;timestamp&amp;nonce</code
            >
            <div class="example-box">
              <span>示例参数</span>
              <pre>
GET&amp;/api/demo/helloWord&amp;partner-erp&amp;1784268000&amp;483921</pre
              >
            </div>
            <p>
              使用 <code>accessSecret</code> 作为密钥，对上面的完整字符串计算
              HMAC-SHA256，再把结果转换为 Base64，得到 <code>sign</code>。
            </p>
            <Alert
              message="签名只使用接口路径，例如 /api/demo/helloWord。不要带 http://域名，也不要带 ?page=1 等查询参数。"
              show-icon
              type="warning"
            />
          </Tabs.TabPane>

          <Tabs.TabPane key="actions" tab="按钮与报错">
            <h3>页面按钮是什么意思？</h3>
            <Descriptions :column="1" bordered size="small">
              <Descriptions.Item label="编辑">
                修改身份标识或绑定账号，不会改变当前密钥。
              </Descriptions.Item>
              <Descriptions.Item label="生成签名">
                联调工具。后台用已保存密钥生成一次签名，但不会把密钥返回浏览器。
              </Descriptions.Item>
              <Descriptions.Item label="轮换密钥">
                生成全新密钥，旧密钥立即失效；外部系统必须同步更新。
              </Descriptions.Item>
              <Descriptions.Item label="删除身份">
                彻底停止该身份访问，外部系统之后都会认证失败。
              </Descriptions.Item>
            </Descriptions>
            <h3>常见失败原因</h3>
            <ul class="error-list">
              <li>
                <strong>timestamp 超时：</strong
                >外部服务器时间不准，或生成签名后等待太久才发送。
              </li>
              <li>
                <strong>重复的请求：</strong>同一个 nonce
                被重复使用；每次请求都要生成新随机数。
              </li>
              <li>
                <strong>sign 无效：</strong
                >请求方法大小写、接口路径、身份标识、时间戳、随机数或密钥有一项不一致。
              </li>
              <li>
                <strong>接口返回无权限：</strong
                >签名已经通过，但绑定用户没有该业务接口或数据的权限。
              </li>
              <li>
                <strong>accessKey 无效：</strong
                >身份标识写错、身份已删除，或密钥轮换后外部系统仍在使用旧配置。
              </li>
            </ul>
          </Tabs.TabPane>
        </Tabs>
      </div>
    </Modal>
  </div>
</template>

<style scoped>
.open-access-page {
  min-height: 100%;
  padding: 12px;
  background: hsl(var(--background-deep));
}

.content-panel {
  min-height: calc(100vh - 132px);
  padding: 16px;
  background: hsl(var(--background));
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
}

.panel-header {
  display: flex;
  gap: 16px;
  align-items: flex-start;
  justify-content: space-between;
  margin-bottom: 12px;
}

.panel-header h2 {
  margin: 0 0 3px;
  font-size: 16px;
  font-weight: 650;
}

.panel-header p,
.field-help {
  margin: 0;
  font-size: 12px;
  color: hsl(var(--muted-foreground));
}

.security-alert,
.modal-alert {
  margin-bottom: 12px;
  border-radius: 5px;
}

.query-bar {
  display: grid;
  grid-template-columns: auto minmax(220px, 320px) auto auto;
  gap: 8px;
  align-items: center;
  margin-bottom: 12px;
  font-size: 13px;
}

.identity-key {
  display: flex;
  gap: 7px;
  align-items: center;
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
}

.action-menu {
  display: grid;
  min-width: 128px;
}

.form-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
}

.secret-preserved {
  display: flex;
  gap: 8px;
  align-items: center;
  padding: 10px 12px;
  margin-bottom: 16px;
  font-size: 13px;
  color: #1677ff;
  background: #e6f4ff;
  border: 1px solid #91caff;
  border-radius: 5px;
}

.signature-form {
  margin-top: 14px;
}

.method-select {
  width: 112px;
}

.signature-result {
  margin-top: 14px;
}

.copy-sign {
  margin-top: 8px;
}

.one-time-secret {
  display: grid;
  grid-template-columns: 1fr auto;
  gap: 8px;
  margin-top: 16px;
}

.help-content h3 {
  margin: 12px 0 6px;
  font-size: 14px;
}

.help-alert {
  margin-top: 12px;
}

.guide-steps,
.error-list {
  padding-left: 22px;
  margin: 4px 0 0;
}

.guide-steps li,
.error-list li {
  margin-bottom: 10px;
  line-height: 1.65;
}

.example-box {
  padding: 10px 12px;
  margin-top: 8px;
  background: hsl(var(--muted) / 45%);
  border: 1px solid hsl(var(--border));
  border-radius: 5px;
}

.example-box span {
  font-size: 12px;
  color: hsl(var(--muted-foreground));
}

.example-box pre {
  margin: 6px 0 0;
  overflow-x: auto;
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
}

.help-content p {
  margin: 6px 0 10px;
  line-height: 1.65;
  color: hsl(var(--muted-foreground));
}

.help-content code {
  padding: 2px 6px;
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
  color: #0958d9;
  background: #e6f4ff;
  border-radius: 3px;
}

@media (max-width: 720px) {
  .query-bar,
  .form-grid,
  .one-time-secret {
    grid-template-columns: 1fr;
  }
}
</style>

<style>
.open-access-record-popover .ant-popover-inner {
  color: hsl(var(--foreground));
  background: hsl(var(--background));
  box-shadow: 0 8px 24px rgb(15 23 42 / 14%);
}

.open-access-record-popover .ant-popover-arrow::before {
  background: hsl(var(--background)) !important;
}
</style>
