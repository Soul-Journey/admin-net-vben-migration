<script setup lang="ts">
import type { FormInstance, MenuProps, TableColumnsType } from 'ant-design-vue';
import type { Rule } from 'ant-design-vue/es/form';

import type { SaveLdapParams, SysLdapRecord, SysTenantOption } from '#/api';

import { computed, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';
import { IconifyIcon } from '@vben/icons';
import { useUserStore } from '@vben/stores';

import {
  Button,
  Col,
  Descriptions,
  Dropdown,
  Form,
  Input,
  InputNumber,
  Menu,
  message,
  Modal,
  Popover,
  Radio,
  Row,
  Select,
  Space,
  Table,
  Tag,
  Tooltip,
} from 'ant-design-vue';

import {
  addLdapApi,
  deleteLdapApi,
  getTenantListApi,
  pageLdapApi,
  syncLdapOrgsApi,
  syncLdapUsersApi,
  updateLdapApi,
} from '#/api';
import { ADMIN_PAGINATION_PROPS } from '#/utils/pagination';

defineOptions({ name: 'AdminNetSystemLdap' });

type LdapFormState = Partial<SaveLdapParams>;

const SUPER_ADMIN_ACCOUNT = 999;
const { hasAccessByCodes } = useAccess();
const userStore = useUserStore();
const loading = ref(false);
const submitLoading = ref(false);
const syncingId = ref<number>();
const modalOpen = ref(false);
const editingId = ref<number>();
const formRef = ref<FormInstance>();
const records = ref<SysLdapRecord[]>([]);
const tenants = ref<SysTenantOption[]>([]);
const total = ref(0);

const query = reactive({
  host: '',
  page: 1,
  pageSize: 20,
  tenantId: undefined as number | undefined,
});

const formState = reactive<LdapFormState>({});
const isSuperAdmin = computed(
  () =>
    Number((userStore.userInfo as any)?.accountType) === SUPER_ADMIN_ACCOUNT,
);
const modalTitle = computed(() =>
  editingId.value ? '编辑 AD / LDAP 配置' : '新增 AD / LDAP 配置',
);
const tenantOptions = computed(() =>
  tenants.value.map((item) => ({
    label: item.host ? `${item.label} (${item.host})` : item.label,
    value: item.value,
  })),
);

const columns: TableColumnsType<SysLdapRecord> = [
  { key: 'index', title: '序号', width: 58 },
  { dataIndex: 'host', key: 'server', title: '服务器', width: 180 },
  {
    dataIndex: 'baseDn',
    key: 'baseDn',
    title: '搜索根 Base DN',
    ellipsis: true,
    width: 220,
  },
  {
    dataIndex: 'bindDn',
    key: 'bindDn',
    title: '绑定账号 Bind DN',
    ellipsis: true,
    width: 220,
  },
  {
    dataIndex: 'authFilter',
    key: 'authFilter',
    title: '登录筛选',
    ellipsis: true,
    width: 180,
  },
  { key: 'password', title: '绑定密码', width: 96 },
  { dataIndex: 'version', key: 'version', title: '版本', width: 68 },
  { key: 'status', title: '状态', width: 78 },
  { key: 'modifyRecord', title: '修改记录', width: 112 },
  { key: 'actions', fixed: 'right', title: '操作', width: 188 },
];

const formRules: Record<string, Rule[]> = {
  authFilter: [
    { required: true, message: '请输入登录筛选条件', trigger: 'blur' },
  ],
  baseDn: [
    { required: true, message: '请输入搜索根 Base DN', trigger: 'blur' },
  ],
  bindAttrAccount: [
    { required: true, message: '请输入账号属性', trigger: 'blur' },
  ],
  bindAttrCode: [
    { required: true, message: '请输入机构编码属性', trigger: 'blur' },
  ],
  bindAttrEmployeeId: [
    { required: true, message: '请输入员工编号属性', trigger: 'blur' },
  ],
  bindDn: [
    { required: true, message: '请输入绑定账号 Bind DN', trigger: 'blur' },
  ],
  host: [{ required: true, message: '请输入服务器地址', trigger: 'blur' }],
  port: [
    {
      required: true,
      message: '请输入端口',
      trigger: 'change',
      type: 'number',
    },
  ],
  tenantId: [
    {
      required: true,
      message: '请选择租户',
      trigger: 'change',
      type: 'number',
    },
  ],
};

function can(code: string) {
  return hasAccessByCodes([code]);
}

function asLdap(record: unknown) {
  return record as SysLdapRecord;
}

function valueText(value?: null | number | string) {
  return value === undefined || value === null || value === ''
    ? '无'
    : String(value);
}

function resetForm(values: LdapFormState) {
  Object.keys(formState).forEach(
    (key) => delete formState[key as keyof LdapFormState],
  );
  Object.assign(formState, values);
}

async function loadTenants() {
  if (!isSuperAdmin.value) return;
  tenants.value = await getTenantListApi();
}

async function loadRecords() {
  loading.value = true;
  try {
    const data = await pageLdapApi({
      host: query.host || undefined,
      page: query.page,
      pageSize: query.pageSize,
      tenantId: query.tenantId,
    });
    records.value = data.items ?? [];
    total.value = data.total ?? 0;
  } finally {
    loading.value = false;
  }
}

async function handleQuery() {
  query.page = 1;
  await loadRecords();
}

function openCreate() {
  editingId.value = undefined;
  resetForm({
    authFilter: 'sAMAccountName=%s',
    bindAttrAccount: 'sAMAccountName',
    bindAttrCode: 'objectGUID',
    bindAttrEmployeeId: 'EmployeeId',
    port: 389,
    status: 1,
    tenantId: query.tenantId,
    version: 3,
  });
  modalOpen.value = true;
}

function openEdit(record: SysLdapRecord) {
  editingId.value = record.id;
  resetForm({ ...record, bindPass: '' });
  modalOpen.value = true;
}

async function submitForm() {
  await formRef.value?.validate();
  if (!editingId.value && !formState.bindPass?.trim()) {
    message.warning('新增配置时必须填写绑定密码');
    return;
  }
  submitLoading.value = true;
  try {
    const payload = { ...formState } as SaveLdapParams;
    await (editingId.value
      ? updateLdapApi({ ...payload, id: editingId.value })
      : addLdapApi(payload));
    message.success(editingId.value ? '配置已更新' : '配置已新增');
    modalOpen.value = false;
    await loadRecords();
  } finally {
    submitLoading.value = false;
  }
}

function removeRecord(record: SysLdapRecord) {
  Modal.confirm({
    content: `删除后，该租户将不能使用域账号登录。确定删除 ${record.host}:${record.port} 吗？`,
    okButtonProps: { danger: true },
    okText: '删除',
    title: '删除域配置',
    async onOk() {
      await deleteLdapApi(record.id);
      message.success('配置已删除');
      await loadRecords();
    },
  });
}

function syncRecord(record: SysLdapRecord, kind: 'org' | 'user') {
  const isUser = kind === 'user';
  Modal.confirm({
    content: isUser
      ? '读取域用户并在事务中替换该租户的域账号镜像。会按账号去重，不会删除本地系统用户。'
      : '按机构编码新增或更新域组织，保留现有机构 ID，也不会自动删除本地机构。',
    okText: '开始同步',
    title: isUser ? '同步域用户' : '同步域组织',
    async onOk() {
      syncingId.value = record.id;
      try {
        const result = isUser
          ? await syncLdapUsersApi(record.id)
          : await syncLdapOrgsApi(record.id);
        message.success(
          `同步完成：新增 ${result.added}，更新 ${result.updated}，共 ${result.total} 条`,
        );
      } finally {
        syncingId.value = undefined;
      }
    },
  });
}

function syncMenu(_record: SysLdapRecord): MenuProps['items'] {
  return [
    can('sysLdap:syncUser') ? { key: 'user', label: '同步域用户' } : null,
    can('sysLdap:syncOrg') ? { key: 'org', label: '同步域组织' } : null,
  ].filter(Boolean) as MenuProps['items'];
}

function handleSyncMenu(record: SysLdapRecord, key: string) {
  syncRecord(record, key === 'user' ? 'user' : 'org');
}

async function resetQuery() {
  query.host = '';
  query.tenantId = undefined;
  query.page = 1;
  await loadRecords();
}

onMounted(async () => {
  await loadTenants();
  await loadRecords();
});
</script>

<template>
  <div class="ldap-page">
    <section class="page-panel">
      <div class="panel-heading">
        <div>
          <h2>AD 域配置</h2>
          <p>维护域服务器连接，并按租户同步用户和机构</p>
        </div>
        <Button v-if="can('sysLdap:add')" type="primary" @click="openCreate">
          <template #icon><IconifyIcon icon="lucide:plus" /></template>
          新增
        </Button>
      </div>

      <div class="query-bar">
        <Select
          v-if="isSuperAdmin"
          v-model:value="query.tenantId"
          allow-clear
          class="tenant-select"
          :options="tenantOptions"
          placeholder="选择租户"
        />
        <Input
          v-model:value="query.host"
          allow-clear
          placeholder="服务器地址"
          @press-enter="handleQuery"
        />
        <Button type="primary" @click="handleQuery">
          <template #icon><IconifyIcon icon="lucide:search" /></template>查询
        </Button>
        <Button @click="resetQuery">
          <template #icon><IconifyIcon icon="lucide:rotate-ccw" /></template
          >重置
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
          showTotal: (value: number) => `共 ${value} 条`,
          total,
        }"
        :scroll="{ x: 1500 }"
        row-key="id"
        size="small"
        @change="
          (pagination) => {
            query.page = pagination.current ?? 1;
            query.pageSize = pagination.pageSize ?? 20;
            loadRecords();
          }
        "
      >
        <template #bodyCell="{ column, index, record }">
          <template v-if="column.key === 'index'">
            {{ (query.page - 1) * query.pageSize + index + 1 }}
          </template>
          <template v-else-if="column.key === 'server'">
            <span class="server-address"
              ><IconifyIcon icon="lucide:server" />{{ record.host }}:{{
                record.port
              }}</span
            >
          </template>
          <template v-else-if="column.key === 'password'">
            <Tag :color="record.hasBindPass ? 'green' : 'red'">
              {{ record.hasBindPass ? '已配置' : '未配置' }}
            </Tag>
          </template>
          <template v-else-if="column.key === 'version'">
            <Tag color="blue">LDAP v{{ record.version }}</Tag>
          </template>
          <template v-else-if="column.key === 'status'">
            <Tag :color="record.status === 1 ? 'green' : 'default'">
              {{ record.status === 1 ? '启用' : '停用' }}
            </Tag>
          </template>
          <template v-else-if="column.key === 'modifyRecord'">
            <Popover
              placement="bottom"
              trigger="hover"
              overlay-class-name="ldap-record-popover"
            >
              <template #content>
                <Descriptions
                  :column="2"
                  bordered
                  layout="vertical"
                  size="small"
                >
                  <Descriptions.Item label="创建者">
                    {{ valueText(record.createUserName) }}
                  </Descriptions.Item>
                  <Descriptions.Item label="创建时间">
                    {{ valueText(record.createTime) }}
                  </Descriptions.Item>
                  <Descriptions.Item label="修改者">
                    {{ valueText(record.updateUserName) }}
                  </Descriptions.Item>
                  <Descriptions.Item label="修改时间">
                    {{ valueText(record.updateTime) }}
                  </Descriptions.Item>
                </Descriptions>
              </template>
              <Button size="small" type="link">
                <template #icon><IconifyIcon icon="lucide:info" /></template
                >详情
              </Button>
            </Popover>
          </template>
          <template v-else-if="column.key === 'actions'">
            <Space :size="2">
              <Tooltip title="编辑">
                <Button
                  v-if="can('sysLdap:update')"
                  size="small"
                  type="link"
                  @click="openEdit(asLdap(record))"
                >
                  <template #icon>
                    <IconifyIcon icon="lucide:square-pen" /> </template
                  >编辑
                </Button>
              </Tooltip>
              <Dropdown
                v-if="syncMenu(asLdap(record))?.length"
                :trigger="['click']"
              >
                <Button
                  :loading="syncingId === record.id"
                  size="small"
                  type="link"
                >
                  同步<IconifyIcon icon="lucide:chevron-down" />
                </Button>
                <template #overlay>
                  <Menu
                    :items="syncMenu(asLdap(record))"
                    @click="
                      ({ key }: any) =>
                        handleSyncMenu(asLdap(record), String(key))
                    "
                  />
                </template>
              </Dropdown>
              <Tooltip title="删除">
                <Button
                  v-if="can('sysLdap:delete')"
                  danger
                  size="small"
                  type="link"
                  @click="removeRecord(asLdap(record))"
                >
                  <template #icon>
                    <IconifyIcon icon="lucide:trash-2" />
                  </template>
                </Button>
              </Tooltip>
            </Space>
          </template>
        </template>
      </Table>
    </section>

    <Modal
      v-model:open="modalOpen"
      :confirm-loading="submitLoading"
      :title="modalTitle"
      width="760px"
      @ok="submitForm"
    >
      <Form
        ref="formRef"
        :label-col="{ span: 7 }"
        :model="formState"
        :rules="formRules"
        class="ldap-form"
      >
        <Row :gutter="16">
          <Col v-if="isSuperAdmin" :span="12">
            <Form.Item label="所属租户" name="tenantId">
              <Select
                v-model:value="formState.tenantId"
                :disabled="!!editingId"
                :options="tenantOptions"
                placeholder="请选择租户"
              />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="服务器地址" name="host">
              <Input
                v-model:value="formState.host"
                placeholder="如 192.168.1.10"
              />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="端口" name="port">
              <InputNumber
                v-model:value="formState.port"
                :max="65535"
                :min="1"
                class="full-width"
              />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="LDAP 版本" name="version">
              <Select
                v-model:value="formState.version"
                :options="[
                  { label: 'LDAP v3', value: 3 },
                  { label: 'LDAP v2', value: 2 },
                ]"
              />
            </Form.Item>
          </Col>
          <Col :span="24">
            <Form.Item
              :label-col="{ span: 3 }"
              label="搜索根 Base DN"
              name="baseDn"
            >
              <Input
                v-model:value="formState.baseDn"
                placeholder="如 DC=example,DC=com"
              />
            </Form.Item>
          </Col>
          <Col :span="24">
            <Form.Item
              :label-col="{ span: 3 }"
              label="绑定账号 Bind DN"
              name="bindDn"
            >
              <Input
                v-model:value="formState.bindDn"
                placeholder="如 CN=admin,OU=Users,DC=example,DC=com"
              />
            </Form.Item>
          </Col>
          <Col :span="24">
            <Form.Item :label-col="{ span: 3 }" label="绑定密码">
              <Input.Password
                v-model:value="formState.bindPass"
                :placeholder="
                  editingId ? '留空保持原密码不变' : '请输入域绑定账号密码'
                "
              />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="登录筛选" name="authFilter">
              <Input
                v-model:value="formState.authFilter"
                placeholder="sAMAccountName=%s"
              />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="账号属性" name="bindAttrAccount">
              <Input v-model:value="formState.bindAttrAccount" />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="员工编号属性" name="bindAttrEmployeeId">
              <Input v-model:value="formState.bindAttrEmployeeId" />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="机构编码属性" name="bindAttrCode">
              <Input v-model:value="formState.bindAttrCode" />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="状态">
              <Radio.Group v-model:value="formState.status">
                <Radio :value="1">启用</Radio><Radio :value="2">停用</Radio>
              </Radio.Group>
            </Form.Item>
          </Col>
        </Row>
      </Form>
    </Modal>
  </div>
</template>

<style scoped>
.ldap-page {
  min-height: 100%;
  padding: 12px;
  background: #f5f7fb;
}

.page-panel {
  padding: 14px;
  overflow: hidden;
  background: #fff;
  border: 1px solid #e7eaf0;
  border-radius: 8px;
}

.panel-heading {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 12px;
}

.panel-heading h2 {
  margin: 0;
  font-size: 16px;
  font-weight: 650;
}

.panel-heading p {
  margin: 3px 0 0;
  font-size: 12px;
  color: #667085;
}

.query-bar {
  display: flex;
  gap: 8px;
  align-items: center;
  margin-bottom: 12px;
}

.query-bar .ant-input-affix-wrapper {
  width: 240px;
}

.tenant-select {
  width: 240px;
}

.server-address {
  display: inline-flex;
  gap: 7px;
  align-items: center;
  font-weight: 550;
}

.full-width {
  width: 100%;
}

.ldap-form {
  padding: 8px 8px 0;
}

.ldap-form :deep(.ant-form-item) {
  margin-bottom: 16px;
}
</style>

<style>
.ldap-record-popover .ant-popover-inner {
  padding: 10px;
  background: #fff;
}

.ldap-record-popover .ant-descriptions {
  width: 430px;
}
</style>
