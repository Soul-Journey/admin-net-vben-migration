<script setup lang="ts">
import type { FormInstance, TableColumnsType } from 'ant-design-vue';
import type { Rule } from 'ant-design-vue/es/form';

import type {
  SavePosParams,
  SysPosRecord,
  SysPosUser,
  SysTenantOption,
} from '#/api';

import { computed, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';
import { IconifyIcon } from '@vben/icons';
import { useUserStore } from '@vben/stores';

import {
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
  Tag,
  Tooltip,
} from 'ant-design-vue';

import {
  addPositionApi,
  deletePositionApi,
  getTenantListApi,
  listPositionsApi,
  updatePositionApi,
} from '#/api';

defineOptions({ name: 'AdminNetSystemPos' });

type PosFormState = Partial<SavePosParams> & { id?: number };

const ENABLED = 1;
const DISABLED = 2;
const SUPER_ADMIN_ACCOUNT = 999;

const { hasAccessByCodes } = useAccess();
const userStore = useUserStore();

const loading = ref(false);
const tenantLoading = ref(false);
const submitLoading = ref(false);
const modalOpen = ref(false);
const modalTitle = ref('新增职位');
const formRef = ref<FormInstance>();
const positions = ref<SysPosRecord[]>([]);
const tenantList = ref<SysTenantOption[]>([]);
const formState = reactive<PosFormState>({});

const query = reactive({
  code: '',
  name: '',
  tenantId: undefined as number | undefined,
});

const columns: TableColumnsType<SysPosRecord> = [
  { key: 'index', title: '序号', width: 58 },
  { dataIndex: 'name', key: 'name', title: '职位名称', width: 180 },
  { dataIndex: 'code', key: 'code', title: '职位编码', width: 160 },
  { key: 'userCount', title: '在职人数', width: 88 },
  { key: 'userDetail', title: '人员明细', width: 122 },
  { dataIndex: 'orderNo', key: 'orderNo', title: '排序', width: 76 },
  { key: 'status', title: '状态', width: 78 },
  { key: 'modifyRecord', title: '修改记录', width: 112 },
  { key: 'actions', fixed: 'right', title: '操作', width: 188 },
];

const userColumns: TableColumnsType<SysPosUser> = [
  { key: 'index', title: '序号', width: 56 },
  { dataIndex: 'account', key: 'account', title: '账号', width: 110 },
  { dataIndex: 'realName', key: 'realName', title: '姓名', width: 110 },
];

const formRules: Record<string, Rule[]> = {
  code: [
    { message: '请输入职位编码', required: true, trigger: 'blur', type: 'string' },
  ],
  name: [
    { message: '请输入职位名称', required: true, trigger: 'blur', type: 'string' },
  ],
};

const statusOptions = [
  { label: '启用', value: ENABLED },
  { label: '禁用', value: DISABLED },
];

const isSuperAdmin = computed(
  () => Number((userStore.userInfo as any)?.accountType) === SUPER_ADMIN_ACCOUNT,
);

const tenantOptions = computed(() =>
  tenantList.value.map((item) => ({
    label: item.host ? `${item.label} (${item.host})` : item.label,
    value: item.value,
  })),
);

function can(code: string) {
  return hasAccessByCodes([code]);
}

function asPos(record: unknown) {
  return record as SysPosRecord;
}

function getValueText(value?: null | number | string) {
  return value === undefined || value === null || value === '' ? '无' : value;
}

function getStatusMeta(status?: number) {
  return status === ENABLED
    ? { color: 'success', label: '启用' }
    : { color: 'default', label: '禁用' };
}

function getUserCount(record: SysPosRecord) {
  return record.userList?.length ?? 0;
}

function resetFormState(values: PosFormState) {
  for (const key of Object.keys(formState)) {
    delete formState[key as keyof PosFormState];
  }
  Object.assign(formState, values);
}

async function loadTenants() {
  if (!isSuperAdmin.value) {
    return;
  }
  tenantLoading.value = true;
  try {
    tenantList.value = await getTenantListApi();
    if (!query.tenantId && tenantList.value[0]?.value) {
      query.tenantId = tenantList.value[0].value;
    }
  } finally {
    tenantLoading.value = false;
  }
}

async function loadPositions() {
  if (!can('sysPos:list')) {
    return;
  }
  loading.value = true;
  try {
    positions.value = await listPositionsApi({
      code: query.code || undefined,
      name: query.name || undefined,
      tenantId: query.tenantId,
    });
  } finally {
    loading.value = false;
  }
}

async function handleQuery() {
  await loadPositions();
}

async function resetQuery() {
  query.code = '';
  query.name = '';
  await loadPositions();
}

function openCreatePos() {
  modalTitle.value = '新增职位';
  resetFormState({
    orderNo: 100,
    status: ENABLED,
    tenantId: query.tenantId,
  });
  modalOpen.value = true;
}

function openEditPos(record: SysPosRecord) {
  modalTitle.value = '编辑职位';
  resetFormState({
    ...record,
    orderNo: record.orderNo ?? 100,
    status: record.status ?? ENABLED,
  });
  modalOpen.value = true;
}

function openCopyPos(record: SysPosRecord) {
  modalTitle.value = '复制职位';
  resetFormState({
    ...record,
    id: undefined,
    name: '',
    orderNo: record.orderNo ?? 100,
    status: record.status ?? ENABLED,
  });
  modalOpen.value = true;
}

async function submitPos() {
  await formRef.value?.validate();
  submitLoading.value = true;
  try {
    const payload = {
      ...formState,
      orderNo: formState.orderNo ?? 100,
      status: formState.status ?? ENABLED,
      tenantId: formState.tenantId ?? query.tenantId,
    } as SavePosParams & { id?: number };

    if (payload.id) {
      await updatePositionApi(payload as SavePosParams & { id: number });
      message.success('职位已更新');
    } else {
      await addPositionApi(payload);
      message.success('职位已新增');
    }
    modalOpen.value = false;
    await loadPositions();
  } finally {
    submitLoading.value = false;
  }
}

function confirmDelete(record: SysPosRecord) {
  Modal.confirm({
    centered: true,
    content: `确定删除职位「${record.name}」吗？如果职位下存在用户、附属职位或注册方案，后端会拒绝删除。`,
    okButtonProps: { danger: true },
    okText: '删除',
    onOk: async () => {
      await deletePositionApi(record.id);
      message.success('职位已删除');
      await loadPositions();
    },
    title: '删除职位',
  });
}

onMounted(async () => {
  await loadTenants();
  await loadPositions();
});
</script>

<template>
  <div class="pos-page">
    <section class="panel">
      <div class="panel-head">
        <div>
          <div class="panel-title">职位</div>
          <div class="panel-subtitle">维护岗位基础信息和在职人员明细</div>
        </div>
      </div>

      <Form :model="query" layout="inline" class="query-form">
        <Form.Item v-if="isSuperAdmin" label="租户">
          <Select
            v-model:value="query.tenantId"
            :loading="tenantLoading"
            :options="tenantOptions"
            allow-clear
            class="tenant-select"
            placeholder="租户"
            @change="handleQuery"
          />
        </Form.Item>
        <Form.Item label="职位名称">
          <Input
            v-model:value="query.name"
            allow-clear
            placeholder="职位名称"
            @press-enter="handleQuery"
          />
        </Form.Item>
        <Form.Item label="职位编码">
          <Input
            v-model:value="query.code"
            allow-clear
            placeholder="职位编码"
            @press-enter="handleQuery"
          />
        </Form.Item>
        <Form.Item>
          <Space>
            <Button
              v-if="can('sysPos:list')"
              :loading="loading"
              type="primary"
              @click="handleQuery"
            >
              <template #icon>
                <IconifyIcon icon="lucide:search" />
              </template>
              查询
            </Button>
            <Button @click="resetQuery">
              <template #icon>
                <IconifyIcon icon="lucide:rotate-ccw" />
              </template>
              重置
            </Button>
            <Button v-if="can('sysPos:add')" type="primary" @click="openCreatePos">
              <template #icon>
                <IconifyIcon icon="lucide:plus" />
              </template>
              新增
            </Button>
          </Space>
        </Form.Item>
      </Form>

      <Table
        :columns="columns"
        :data-source="positions"
        :loading="loading"
        :pagination="false"
        :scroll="{ x: 1060 }"
        row-key="id"
        size="small"
      >
        <template #bodyCell="{ column, index, record }">
          <template v-if="column.key === 'index'">
            {{ index + 1 }}
          </template>
          <template v-else-if="column.key === 'userCount'">
            <Tag :color="getUserCount(asPos(record)) > 0 ? 'blue' : 'default'">
              {{ getUserCount(asPos(record)) }}
            </Tag>
          </template>
          <template v-else-if="column.key === 'userDetail'">
            <Popover
              v-if="getUserCount(asPos(record)) > 0"
              overlay-class-name="pos-record-popover"
              placement="bottom"
              trigger="hover"
            >
              <template #content>
                <Table
                  :columns="userColumns"
                  :data-source="asPos(record).userList"
                  :pagination="false"
                  row-key="id"
                  size="small"
                >
                  <template #bodyCell="{ column: userColumn, index: userIndex }">
                    <template v-if="userColumn.key === 'index'">
                      {{ userIndex + 1 }}
                    </template>
                  </template>
                </Table>
              </template>
              <Button size="small" type="link">
                <template #icon>
                  <IconifyIcon icon="lucide:info" />
                </template>
                人员明细
              </Button>
            </Popover>
            <Tag v-else color="default">无</Tag>
          </template>
          <template v-else-if="column.key === 'status'">
            <Tag :color="getStatusMeta(asPos(record).status).color">
              {{ getStatusMeta(asPos(record).status).label }}
            </Tag>
          </template>
          <template v-else-if="column.key === 'modifyRecord'">
            <Popover
              overlay-class-name="pos-record-popover"
              placement="bottom"
              trigger="hover"
            >
              <template #content>
                <Descriptions
                  :column="2"
                  bordered
                  class="modify-record"
                  layout="vertical"
                  size="small"
                >
                  <Descriptions.Item label="创建者">
                    <Tag>{{ getValueText(asPos(record).createUserName) }}</Tag>
                  </Descriptions.Item>
                  <Descriptions.Item label="创建时间">
                    <Tag>{{ getValueText(asPos(record).createTime) }}</Tag>
                  </Descriptions.Item>
                  <Descriptions.Item label="修改者">
                    <Tag>{{ getValueText(asPos(record).updateUserName) }}</Tag>
                  </Descriptions.Item>
                  <Descriptions.Item label="修改时间">
                    <Tag>{{ getValueText(asPos(record).updateTime) }}</Tag>
                  </Descriptions.Item>
                  <Descriptions.Item label="备注" :span="2">
                    {{ getValueText(asPos(record).remark) }}
                  </Descriptions.Item>
                </Descriptions>
              </template>
              <Button size="small" type="link">
                <template #icon>
                  <IconifyIcon icon="lucide:info" />
                </template>
                详情
              </Button>
            </Popover>
          </template>
          <template v-else-if="column.key === 'actions'">
            <Space :size="4" wrap>
              <Tooltip title="编辑">
                <Button
                  v-if="can('sysPos:update')"
                  size="small"
                  type="link"
                  @click="openEditPos(asPos(record))"
                >
                  <template #icon>
                    <IconifyIcon icon="lucide:square-pen" />
                  </template>
                  编辑
                </Button>
              </Tooltip>
              <Tooltip title="删除">
                <Button
                  v-if="can('sysPos:delete')"
                  danger
                  size="small"
                  type="link"
                  @click="confirmDelete(asPos(record))"
                >
                  <template #icon>
                    <IconifyIcon icon="lucide:trash-2" />
                  </template>
                  删除
                </Button>
              </Tooltip>
              <Tooltip title="复制">
                <Button
                  v-if="can('sysPos:add')"
                  size="small"
                  type="link"
                  @click="openCopyPos(asPos(record))"
                >
                  <template #icon>
                    <IconifyIcon icon="lucide:copy" />
                  </template>
                  复制
                </Button>
              </Tooltip>
            </Space>
          </template>
        </template>
      </Table>
    </section>

    <Modal
      v-model:open="modalOpen"
      :body-style="{ padding: '16px 20px' }"
      :footer="null"
      :mask-closable="false"
      :title="modalTitle"
      centered
      class="pos-modal"
      destroy-on-close
      width="560"
      @cancel="formRef?.clearValidate()"
    >
      <Form ref="formRef" :model="formState" :rules="formRules" layout="vertical">
        <Row :gutter="16">
          <Col :span="24">
            <Form.Item label="职位名称" name="name">
              <Input v-model:value="formState.name" allow-clear placeholder="职位名称" />
            </Form.Item>
          </Col>
          <Col :span="24">
            <Form.Item label="职位编码" name="code">
              <Input v-model:value="formState.code" allow-clear placeholder="职位编码" />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="排序" name="orderNo">
              <InputNumber
                v-model:value="formState.orderNo"
                class="w-full"
                :min="0"
              />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="状态" name="status">
              <Radio.Group v-model:value="formState.status" :options="statusOptions" />
            </Form.Item>
          </Col>
          <Col :span="24">
            <Form.Item label="备注" name="remark">
              <Input.TextArea
                v-model:value="formState.remark"
                :auto-size="{ minRows: 2, maxRows: 4 }"
                allow-clear
                placeholder="请输入备注内容"
              />
            </Form.Item>
          </Col>
        </Row>
      </Form>
      <div class="modal-footer">
        <Space>
          <Button @click="modalOpen = false">取消</Button>
          <Button :loading="submitLoading" type="primary" @click="submitPos">
            确定
          </Button>
        </Space>
      </div>
    </Modal>
  </div>
</template>

<style scoped>
.pos-page {
  min-height: 100%;
  padding: 12px;
  background: hsl(var(--muted) / 35%);
}

.panel {
  min-width: 0;
  padding: 12px;
  border: 1px solid hsl(var(--border) / 72%);
  border-radius: 8px;
  background: hsl(var(--background));
}

.panel-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 10px;
}

.panel-title {
  color: hsl(var(--foreground));
  font-size: 14px;
  font-weight: 650;
}

.panel-subtitle {
  margin-top: 2px;
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.query-form {
  margin-bottom: 2px;
}

.tenant-select {
  width: 220px;
}

.modify-record {
  width: 360px;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  margin: 14px -20px -16px;
  padding: 10px 20px;
  border-top: 1px solid hsl(var(--border) / 72%);
  background: hsl(var(--background));
}

:global(.pos-modal) {
  width: min(560px, calc(100vw - 32px)) !important;
}

:global(.pos-modal .ant-modal-content) {
  border-radius: 8px;
}

:global(.pos-record-popover .ant-popover-inner) {
  padding: 8px;
  border: 1px solid hsl(var(--border));
  border-radius: 8px;
  box-shadow:
    0 12px 28px rgb(15 23 42 / 12%),
    0 2px 8px rgb(15 23 42 / 8%);
}

:global(.pos-record-popover .ant-popover-inner-content) {
  padding: 0;
}

:global(.pos-record-popover) {
  z-index: 1060;
}

:deep(.ant-form-inline .ant-form-item) {
  margin-bottom: 12px;
}

:deep(.ant-table-thead > tr > th) {
  white-space: nowrap;
}
</style>
