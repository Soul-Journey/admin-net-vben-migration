<script setup lang="ts">
import type { FormInstance, TableColumnsType } from 'ant-design-vue';
import type { Rule } from 'ant-design-vue/es/form';

import type { SavePluginParams, SysPluginRecord, SysTenantOption } from '#/api';

import { computed, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';
import { IconifyIcon } from '@vben/icons';
import { useUserStore } from '@vben/stores';

import {
  Alert,
  Button,
  Col,
  Descriptions,
  Form,
  Input,
  InputNumber,
  message,
  Modal,
  Popover,
  Radio,
  Row,
  Select,
  Space,
  Table,
  Tabs,
  Tag,
  Tooltip,
} from 'ant-design-vue';

import {
  addPluginApi,
  deletePluginApi,
  getTenantListApi,
  pagePluginsApi,
  updatePluginApi,
} from '#/api';
import { ADMIN_PAGINATION_PROPS } from '#/utils/pagination';

defineOptions({ name: 'AdminNetSystemPlugin' });

type PluginFormState = Partial<SavePluginParams>;

const SUPER_ADMIN_ACCOUNT = 999;
const MIN_CODE_LENGTH = 100;
const { hasAccessByCodes } = useAccess();
const userStore = useUserStore();
const loading = ref(false);
const submitLoading = ref(false);
const modalOpen = ref(false);
const formRef = ref<FormInstance>();
const records = ref<SysPluginRecord[]>([]);
const tenants = ref<SysTenantOption[]>([]);
const formState = reactive<PluginFormState>({});
const activeTab = ref('base');

const query = reactive({
  name: '',
  page: 1,
  pageSize: 50,
  tenantId: undefined as number | undefined,
  total: 0,
});

const isSuperAdmin = computed(
  () =>
    Number((userStore.userInfo as any)?.accountType) === SUPER_ADMIN_ACCOUNT,
);
const modalTitle = computed(() =>
  formState.id ? '编辑动态插件' : '新增动态插件',
);
const codeLength = computed(() => formState.csharpCode?.length ?? 0);
const tenantOptions = computed(() =>
  tenants.value.map((item) => ({
    label: item.host ? `${item.label} (${item.host})` : item.label,
    value: item.value,
  })),
);

const columns: TableColumnsType<SysPluginRecord> = [
  { key: 'index', title: '序号', width: 58 },
  { dataIndex: 'name', key: 'name', title: '功能名称', width: 190 },
  {
    dataIndex: 'assemblyName',
    ellipsis: true,
    key: 'assemblyName',
    title: '程序集名称',
    width: 300,
  },
  { dataIndex: 'orderNo', key: 'orderNo', title: '排序', width: 76 },
  { key: 'status', title: '状态', width: 86 },
  { key: 'modifyRecord', title: '修改记录', width: 104 },
  { fixed: 'right', key: 'actions', title: '操作', width: 150 },
];

const rules: Record<string, Rule[]> = {
  csharpCode: [
    {
      async validator(_rule, value) {
        if (!String(value ?? '').trim()) throw new Error('请输入 C# 源码');
        if (String(value).trim().length < MIN_CODE_LENGTH) {
          throw new Error(`C# 源码不能少于 ${MIN_CODE_LENGTH} 个字符`);
        }
      },
      trigger: 'change',
    },
  ],
  name: [{ message: '请输入功能名称', required: true, trigger: 'blur' }],
};

function can(code: string) {
  return hasAccessByCodes([code]);
}

function asPlugin(value: unknown) {
  return value as SysPluginRecord;
}

function clearForm() {
  for (const key of Object.keys(formState)) {
    delete formState[key as keyof PluginFormState];
  }
}

async function loadPlugins() {
  if (!can('sysPlugin:page')) return;
  loading.value = true;
  try {
    const result = await pagePluginsApi({
      name: query.name.trim() || undefined,
      page: query.page,
      pageSize: query.pageSize,
      tenantId: query.tenantId,
    });
    records.value = result.items ?? [];
    query.total = Number(result.total ?? 0);
  } finally {
    loading.value = false;
  }
}

async function loadTenants() {
  if (!isSuperAdmin.value) return;
  tenants.value = await getTenantListApi().catch(() => []);
  if (!query.tenantId && tenants.value[0]?.value) {
    query.tenantId = tenants.value[0].value;
  }
}

async function handleQuery() {
  query.page = 1;
  await loadPlugins();
}

async function resetQuery() {
  query.name = '';
  await handleQuery();
}

function openEditor(record?: SysPluginRecord) {
  clearForm();
  Object.assign(
    formState,
    record
      ? { ...record }
      : {
          csharpCode: '',
          name: '',
          orderNo: 100,
          remark: '',
          status: 1,
          tenantId: query.tenantId,
        },
  );
  activeTab.value = 'base';
  modalOpen.value = true;
  requestAnimationFrame(() => formRef.value?.clearValidate());
}

async function savePlugin() {
  await formRef.value?.validate();
  const payload = {
    ...formState,
    csharpCode: formState.csharpCode?.trim() ?? '',
    name: formState.name?.trim() ?? '',
  } as SavePluginParams;

  Modal.confirm({
    cancelText: '取消',
    centered: true,
    content: formState.id
      ? '更新会替换当前正在运行的动态接口。若源码编译失败，接口可能需要修正后重新保存。'
      : '保存后，后端会立即编译源码并注册新的 WebAPI。请确认代码来源可信且已经检查。',
    okButtonProps: { danger: true },
    okText: '确认编译并保存',
    title: formState.id ? '确认更新动态插件？' : '确认创建动态插件？',
    async onOk() {
      submitLoading.value = true;
      try {
        await (formState.id
          ? updatePluginApi({ ...payload, id: formState.id })
          : addPluginApi(payload));
        message.success(formState.id ? '插件更新成功' : '插件创建成功');
        modalOpen.value = false;
        await loadPlugins();
      } finally {
        submitLoading.value = false;
      }
    },
  });
}

function removePlugin(record: SysPluginRecord) {
  Modal.confirm({
    cancelText: '取消',
    centered: true,
    content: `删除后会立即卸载程序集“${record.assemblyName || record.name}”，依赖该接口的功能将无法继续访问。`,
    okButtonProps: { danger: true },
    okText: '确认删除',
    title: `删除动态插件“${record.name}”？`,
    async onOk() {
      await deletePluginApi(record.id);
      message.success('插件已删除并卸载');
      if (records.value.length === 1 && query.page > 1) query.page -= 1;
      await loadPlugins();
    },
  });
}

async function initialize() {
  await loadTenants();
  await loadPlugins();
}

onMounted(initialize);
</script>

<template>
  <div class="plugin-page">
    <section class="plugin-panel">
      <header class="panel-header">
        <div>
          <h2>动态插件</h2>
          <p>通过 C# 源码即时扩展后端接口，仅限可信管理员操作</p>
        </div>
        <Button
          v-if="can('sysPlugin:add')"
          type="primary"
          @click="openEditor()"
        >
          <template #icon><IconifyIcon icon="lucide:plus" /></template>
          新增插件
        </Button>
      </header>

      <Alert
        banner
        class="risk-alert"
        message="动态插件会直接改变运行中的 WebAPI。请先在测试环境验证源码，不要粘贴来源不明的代码。"
        show-icon
        type="warning"
      />

      <div class="query-bar">
        <div v-if="isSuperAdmin" class="query-item tenant-query">
          <span>租户</span>
          <Select
            v-model:value="query.tenantId"
            :options="tenantOptions"
            placeholder="请选择租户"
            @change="handleQuery"
          />
        </div>
        <div class="query-item">
          <span>功能名称</span>
          <Input
            v-model:value="query.name"
            allow-clear
            placeholder="请输入功能名称"
            @press-enter="handleQuery"
          />
        </div>
        <Space>
          <Button
            v-if="can('sysPlugin:page')"
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
        </Space>
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
        :scroll="{ x: 970 }"
        row-key="id"
        size="middle"
        @change="
          (pagination) => {
            query.page = pagination.current || 1;
            query.pageSize = pagination.pageSize || 50;
            loadPlugins();
          }
        "
      >
        <template #bodyCell="{ column, index, record }">
          <template v-if="column.key === 'index'">
            {{ (query.page - 1) * query.pageSize + index + 1 }}
          </template>
          <template v-else-if="column.key === 'name'">
            <div class="plugin-name">
              <strong>{{ asPlugin(record).name }}</strong>
              <small>{{ asPlugin(record).remark || '未填写备注' }}</small>
            </div>
          </template>
          <template v-else-if="column.key === 'status'">
            <Tooltip
              title="当前后端仅记录此状态，已编译接口不会因禁用状态自动卸载"
            >
              <Tag
                :color="asPlugin(record).status === 1 ? 'success' : 'default'"
              >
                {{ asPlugin(record).status === 1 ? '启用' : '禁用' }}
              </Tag>
            </Tooltip>
          </template>
          <template v-else-if="column.key === 'modifyRecord'">
            <Popover
              overlay-class-name="plugin-record-popover"
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
                    {{ asPlugin(record).createUserName || '无' }}
                  </Descriptions.Item>
                  <Descriptions.Item label="创建时间">
                    {{ asPlugin(record).createTime || '无' }}
                  </Descriptions.Item>
                  <Descriptions.Item label="修改者">
                    {{ asPlugin(record).updateUserName || '无' }}
                  </Descriptions.Item>
                  <Descriptions.Item label="修改时间">
                    {{ asPlugin(record).updateTime || '无' }}
                  </Descriptions.Item>
                  <Descriptions.Item :span="2" label="备注">
                    {{ asPlugin(record).remark || '无' }}
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
            <Space :size="2">
              <Button
                v-if="can('sysPlugin:update')"
                type="link"
                @click="openEditor(asPlugin(record))"
              >
                <template #icon>
                  <IconifyIcon icon="lucide:square-pen" />
                </template>
                编辑
              </Button>
              <Button
                v-if="can('sysPlugin:delete')"
                danger
                type="link"
                @click="removePlugin(asPlugin(record))"
              >
                <template #icon><IconifyIcon icon="lucide:trash-2" /></template>
                删除
              </Button>
            </Space>
          </template>
        </template>
      </Table>
    </section>

    <Modal
      v-model:open="modalOpen"
      :confirm-loading="submitLoading"
      :destroy-on-close="false"
      :mask-closable="false"
      :title="modalTitle"
      :width="820"
      centered
      cancel-text="取消"
      ok-text="保存"
      @ok="savePlugin"
    >
      <Alert
        class="modal-alert"
        message="保存操作会在服务器内立即编译并加载源码，请确保命名空间、依赖和路由不会与现有接口冲突。"
        show-icon
        type="warning"
      />
      <Form ref="formRef" :model="formState" :rules="rules" layout="vertical">
        <Tabs v-model:active-key="activeTab">
          <Tabs.TabPane key="base" tab="插件信息">
            <Row :gutter="16">
              <Col :span="16">
                <Form.Item label="功能名称" name="name">
                  <Input
                    v-model:value="formState.name"
                    :maxlength="64"
                    placeholder="例如：订单扩展接口"
                  />
                </Form.Item>
              </Col>
              <Col :span="8">
                <Form.Item label="排序" name="orderNo">
                  <InputNumber
                    v-model:value="formState.orderNo"
                    :min="0"
                    class="full-width"
                  />
                </Form.Item>
              </Col>
              <Col v-if="formState.assemblyName" :span="24">
                <Form.Item label="当前程序集">
                  <Input :value="formState.assemblyName" disabled />
                </Form.Item>
              </Col>
              <Col :span="12">
                <Form.Item label="状态（记录）" name="status">
                  <Radio.Group v-model:value="formState.status">
                    <Radio :value="1">启用</Radio>
                    <Radio :value="2">禁用</Radio>
                  </Radio.Group>
                </Form.Item>
              </Col>
              <Col :span="24">
                <Form.Item label="备注" name="remark">
                  <Input.TextArea
                    v-model:value="formState.remark"
                    :maxlength="128"
                    :rows="3"
                    show-count
                  />
                </Form.Item>
              </Col>
            </Row>
          </Tabs.TabPane>
          <Tabs.TabPane key="code" force-render tab="C# 源码">
            <Form.Item name="csharpCode">
              <Input.TextArea
                v-model:value="formState.csharpCode"
                :auto-size="{ minRows: 16, maxRows: 22 }"
                class="code-editor"
                placeholder="请输入完整、可编译的 C# 源码"
                spellcheck="false"
              />
            </Form.Item>
            <div class="code-footer">
              <span>至少 {{ MIN_CODE_LENGTH }} 个字符</span>
              <span :class="{ invalid: codeLength < MIN_CODE_LENGTH }"
                >{{ codeLength }} 字符</span
              >
            </div>
          </Tabs.TabPane>
        </Tabs>
      </Form>
    </Modal>
  </div>
</template>

<style scoped>
.plugin-page {
  min-height: 100%;
  padding: 12px;
  background: hsl(var(--background-deep));
}

.plugin-panel {
  min-height: calc(100vh - 132px);
  padding: 16px;
  background: hsl(var(--background));
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
}

.panel-header {
  display: flex;
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
.plugin-name small {
  margin: 0;
  font-size: 12px;
  color: hsl(var(--muted-foreground));
}

.risk-alert {
  margin-bottom: 12px;
  border-radius: 5px;
}

.query-bar {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  align-items: flex-end;
  margin-bottom: 12px;
}

.query-item {
  display: grid;
  grid-template-columns: auto 220px;
  gap: 8px;
  align-items: center;
  font-size: 13px;
}

.tenant-query {
  grid-template-columns: auto 280px;
}

.plugin-name {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.modal-alert {
  margin-bottom: 12px;
}

.full-width {
  width: 100%;
}

.code-editor {
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
  font-size: 13px;
  line-height: 1.6;
  tab-size: 4;
  white-space: pre;
}

.code-footer {
  display: flex;
  justify-content: space-between;
  margin-top: -18px;
  font-size: 12px;
  color: hsl(var(--muted-foreground));
}

.code-footer .invalid {
  color: hsl(var(--destructive));
}

@media (max-width: 760px) {
  .query-item,
  .tenant-query {
    grid-template-columns: 1fr;
    width: 100%;
  }

  .panel-header {
    gap: 12px;
  }
}
</style>

<style>
.plugin-record-popover .ant-popover-inner {
  color: hsl(var(--foreground));
  background: hsl(var(--background));
  box-shadow: 0 8px 24px rgb(15 23 42 / 14%);
}

.plugin-record-popover .ant-popover-arrow::before {
  background: hsl(var(--background)) !important;
}
</style>
