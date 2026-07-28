<script setup lang="ts">
import type {
  FormInstance,
  TableColumnsType,
  TableProps,
} from 'ant-design-vue';
import type { Rule } from 'ant-design-vue/es/form';

import type { SaveConfigParams, SysConfigRecord } from '#/api';

import { computed, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';
import { IconifyIcon } from '@vben/icons';
import { useUserStore } from '@vben/stores';

import {
  AutoComplete,
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
  addConfigApi,
  batchDeleteConfigsApi,
  deleteConfigApi,
  getConfigGroupsApi,
  pageConfigsApi,
  updateConfigApi,
} from '#/api';
import { ADMIN_PAGINATION_PROPS } from '#/utils/pagination';

defineOptions({ name: 'AdminNetSystemConfig' });

type ConfigFormState = Partial<SaveConfigParams>;

const SUPER_ADMIN_ACCOUNT = 999;
const { hasAccessByCodes } = useAccess();
const userStore = useUserStore();
const loading = ref(false);
const submitLoading = ref(false);
const modalOpen = ref(false);
const editingId = ref<number>();
const formRef = ref<FormInstance>();
const records = ref<SysConfigRecord[]>([]);
const groups = ref<string[]>([]);
const total = ref(0);
const selectedKeys = ref<number[]>([]);
const formState = reactive<ConfigFormState>({});

const query = reactive({
  code: '',
  groupCode: undefined as string | undefined,
  name: '',
  page: 1,
  pageSize: 50,
});

const isSuperAdmin = computed(
  () =>
    Number((userStore.userInfo as any)?.accountType) === SUPER_ADMIN_ACCOUNT,
);
const modalTitle = computed(() => (editingId.value ? '编辑参数' : '新增参数'));
const groupOptions = computed(() =>
  groups.value.map((value) => ({ label: value, value })),
);
const columns: TableColumnsType<SysConfigRecord> = [
  { key: 'index', title: '序号', width: 58 },
  { dataIndex: 'name', key: 'name', title: '参数名称', width: 180 },
  {
    dataIndex: 'code',
    key: 'code',
    title: '参数编码',
    ellipsis: true,
    width: 210,
  },
  { key: 'value', title: '参数值', ellipsis: true, width: 200 },
  { key: 'sysFlag', title: '内置参数', width: 96 },
  { dataIndex: 'groupCode', key: 'groupCode', title: '分组编码', width: 120 },
  { dataIndex: 'orderNo', key: 'orderNo', title: '排序', width: 76 },
  { key: 'modifyRecord', title: '修改记录', width: 112 },
  { key: 'actions', fixed: 'right', title: '操作', width: 126 },
];

const rules: Record<string, Rule[]> = {
  code: [{ required: true, message: '请输入参数编码', trigger: 'blur' }],
  name: [{ required: true, message: '请输入参数名称', trigger: 'blur' }],
  value: [{ required: true, message: '请输入参数值', trigger: 'blur' }],
};

const rowSelection = computed<TableProps['rowSelection']>(() => ({
  getCheckboxProps: (record: SysConfigRecord) => ({
    disabled: record.sysFlag === 1,
  }),
  onChange: (keys) => {
    selectedKeys.value = keys.map(Number);
  },
  selectedRowKeys: selectedKeys.value,
}));

function can(code: string) {
  return hasAccessByCodes([code]);
}

function asConfig(record: unknown) {
  return record as SysConfigRecord;
}

function valueText(value?: null | number | string) {
  return value === undefined || value === null || value === ''
    ? '无'
    : String(value);
}

function isBooleanValue(value?: string) {
  return value?.toLowerCase() === 'true' || value?.toLowerCase() === 'false';
}

function booleanLabel(value?: string) {
  return value?.toLowerCase() === 'true' ? '开启' : '关闭';
}

function resetForm(values: ConfigFormState) {
  Object.keys(formState).forEach(
    (key) => delete formState[key as keyof ConfigFormState],
  );
  Object.assign(formState, values);
}

async function loadGroups() {
  const result = await getConfigGroupsApi();
  groups.value = result.filter(Boolean);
}

async function loadRecords() {
  loading.value = true;
  try {
    const data = await pageConfigsApi({
      code: query.code || undefined,
      groupCode: query.groupCode,
      name: query.name || undefined,
      page: query.page,
      pageSize: query.pageSize,
    });
    records.value = data.items ?? [];
    total.value = data.total ?? 0;
    selectedKeys.value = [];
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
    groupCode: groups.value[0] ?? 'Default',
    orderNo: 100,
    sysFlag: 2,
    value: '',
  });
  modalOpen.value = true;
}

function openEdit(record: SysConfigRecord) {
  editingId.value = record.id;
  resetForm({ ...record });
  modalOpen.value = true;
}

async function submitForm() {
  await formRef.value?.validate();
  submitLoading.value = true;
  try {
    const payload = formState as SaveConfigParams;
    await (editingId.value
      ? updateConfigApi({ ...payload, id: editingId.value })
      : addConfigApi(payload));
    message.success(editingId.value ? '参数已更新，缓存已刷新' : '参数已新增');
    modalOpen.value = false;
    await Promise.all([loadRecords(), loadGroups()]);
  } finally {
    submitLoading.value = false;
  }
}

function removeRecord(record: SysConfigRecord) {
  Modal.confirm({
    content: `删除后会同时删除所有租户对此参数的覆盖值，确定删除“${record.name}”吗？`,
    okButtonProps: { danger: true },
    okText: '删除',
    title: '删除参数定义',
    async onOk() {
      await deleteConfigApi(record.id);
      message.success('参数已删除');
      await Promise.all([loadRecords(), loadGroups()]);
    },
  });
}

function batchRemove() {
  if (selectedKeys.value.length === 0) return;
  Modal.confirm({
    content: `将删除 ${selectedKeys.value.length} 个非内置参数及其所有租户覆盖值。此操作不可撤销。`,
    okButtonProps: { danger: true },
    okText: '批量删除',
    title: '批量删除参数',
    async onOk() {
      await batchDeleteConfigsApi(selectedKeys.value);
      message.success('批量删除完成');
      await Promise.all([loadRecords(), loadGroups()]);
    },
  });
}

async function resetQuery() {
  query.name = '';
  query.code = '';
  query.groupCode = undefined;
  query.page = 1;
  await loadRecords();
}

onMounted(async () => {
  await Promise.all([loadGroups(), loadRecords()]);
});
</script>

<template>
  <div class="config-page">
    <section class="page-panel">
      <div class="panel-heading">
        <div>
          <h2>参数配置</h2>
          <p>维护系统运行参数与租户覆盖值，修改后自动刷新缓存</p>
        </div>
        <Space>
          <Button
            v-if="selectedKeys.length > 0 && can('sysConfig:batchDelete')"
            danger
            @click="batchRemove"
          >
            <template #icon><IconifyIcon icon="lucide:trash-2" /></template
            >批量删除
          </Button>
          <Button
            v-if="can('sysConfig:add') && isSuperAdmin"
            type="primary"
            @click="openCreate"
          >
            <template #icon><IconifyIcon icon="lucide:plus" /></template>新增
          </Button>
        </Space>
      </div>

      <div class="query-bar">
        <Input
          v-model:value="query.name"
          allow-clear
          placeholder="参数名称"
          @press-enter="handleQuery"
        />
        <Input
          v-model:value="query.code"
          allow-clear
          placeholder="参数编码"
          @press-enter="handleQuery"
        />
        <Select
          v-model:value="query.groupCode"
          allow-clear
          :options="groupOptions"
          placeholder="全部分组"
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
        :row-selection="isSuperAdmin ? rowSelection : undefined"
        :scroll="{ x: 1200 }"
        row-key="id"
        size="small"
        @change="
          (pagination) => {
            query.page = pagination.current ?? 1;
            query.pageSize = pagination.pageSize ?? 50;
            loadRecords();
          }
        "
      >
        <template #bodyCell="{ column, index, record }">
          <template v-if="column.key === 'index'">
            {{ (query.page - 1) * query.pageSize + index + 1 }}
          </template>
          <template v-else-if="column.key === 'value'">
            <Tag v-if="record.isSensitive" color="orange">
              <IconifyIcon icon="lucide:shield-check" /> 已隐藏
            </Tag>
            <Tag
              v-else-if="isBooleanValue(record.value)"
              :color="
                record.value.toLowerCase() === 'true' ? 'green' : 'default'
              "
            >
              {{ booleanLabel(record.value) }}
            </Tag>
            <span v-else class="config-value">{{
              valueText(record.value)
            }}</span>
          </template>
          <template v-else-if="column.key === 'sysFlag'">
            <Tag :color="record.sysFlag === 1 ? 'blue' : 'default'">
              {{ record.sysFlag === 1 ? '是' : '否' }}
            </Tag>
          </template>
          <template v-else-if="column.key === 'modifyRecord'">
            <Popover
              placement="bottom"
              trigger="hover"
              overlay-class-name="config-record-popover"
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
                  <Descriptions.Item :span="2" label="备注">
                    {{ valueText(record.remark) }}
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
                  v-if="can('sysConfig:update')"
                  size="small"
                  type="link"
                  @click="openEdit(asConfig(record))"
                >
                  <template #icon>
                    <IconifyIcon icon="lucide:square-pen" /> </template
                  >编辑
                </Button>
              </Tooltip>
              <Tooltip
                :title="record.sysFlag === 1 ? '内置参数不能删除' : '删除'"
              >
                <Button
                  v-if="can('sysConfig:delete') && isSuperAdmin"
                  danger
                  :disabled="record.sysFlag === 1"
                  size="small"
                  type="link"
                  @click="removeRecord(asConfig(record))"
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
      width="640px"
      @ok="submitForm"
    >
      <Form
        ref="formRef"
        :label-col="{ span: 6 }"
        :model="formState"
        :rules="rules"
        class="config-form"
      >
        <Row :gutter="16">
          <Col :span="24">
            <Form.Item label="参数名称" name="name">
              <Input
                v-model:value="formState.name"
                :disabled="!isSuperAdmin"
                placeholder="请输入中文名称"
              />
            </Form.Item>
          </Col>
          <Col :span="24">
            <Form.Item label="参数编码" name="code">
              <Input
                v-model:value="formState.code"
                :disabled="
                  !isSuperAdmin || (!!editingId && formState.sysFlag === 1)
                "
                placeholder="建议使用小写下划线"
              />
            </Form.Item>
          </Col>
          <Col :span="24">
            <Form.Item label="参数值" name="value">
              <AutoComplete
                v-model:value="formState.value"
                :options="[
                  { label: '开启（True）', value: 'True' },
                  { label: '关闭（False）', value: 'False' },
                ]"
                placeholder="输入参数值，或选择开关值"
              />
              <div v-if="formState.isSensitive" class="field-hint">
                当前值已隐藏；保持 ****** 不变，输入新值则替换。
              </div>
            </Form.Item>
          </Col>
          <Col v-if="isSuperAdmin" :span="12">
            <Form.Item label="内置参数">
              <Radio.Group
                v-model:value="formState.sysFlag"
                :disabled="!!editingId && formState.sysFlag === 1"
              >
                <Radio :value="1">是</Radio><Radio :value="2">否</Radio>
              </Radio.Group>
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="排序">
              <InputNumber
                v-model:value="formState.orderNo"
                :min="0"
                class="full-width"
              />
            </Form.Item>
          </Col>
          <Col :span="24">
            <Form.Item label="分组编码">
              <AutoComplete
                v-model:value="formState.groupCode"
                :disabled="!isSuperAdmin || formState.sysFlag === 1"
                :options="groupOptions"
                placeholder="选择或输入分组"
              />
            </Form.Item>
          </Col>
          <Col :span="24">
            <Form.Item label="备注">
              <Input.TextArea
                v-model:value="formState.remark"
                :disabled="!isSuperAdmin"
                :rows="3"
                placeholder="说明用途、单位和取值范围"
              />
            </Form.Item>
          </Col>
        </Row>
      </Form>
    </Modal>
  </div>
</template>

<style scoped>
.config-page {
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
  width: 210px;
}

.query-bar .ant-select {
  width: 170px;
}

.config-value {
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 12px;
}

.config-form {
  padding: 8px 8px 0;
}

.config-form :deep(.ant-form-item) {
  margin-bottom: 16px;
}

.field-hint {
  margin-top: 5px;
  font-size: 12px;
  color: #8a94a6;
}

.full-width {
  width: 100%;
}
</style>

<style>
.config-record-popover .ant-popover-inner {
  padding: 10px;
  background: #fff;
}

.config-record-popover .ant-descriptions {
  width: 430px;
}
</style>
