<script setup lang="ts">
import type { FormInstance, TableColumnsType } from 'ant-design-vue';
import type { Rule } from 'ant-design-vue/es/form';

import type { ApprovalFlowRecord, SaveApprovalFlowParams } from '#/api';

import { computed, onMounted, reactive, ref } from 'vue';

import { IconifyIcon } from '@vben/icons';
import { useUserStore } from '@vben/stores';

import {
  Alert,
  Button,
  Descriptions,
  Empty,
  Form,
  Input,
  message,
  Modal,
  Pagination,
  Popover,
  Radio,
  Select,
  Space,
  Table,
  Tag,
  Tooltip,
} from 'ant-design-vue';

import {
  addApprovalFlowApi,
  deleteApprovalFlowApi,
  getApprovalFlowDetailApi,
  listDatabasesApi,
  listDatabaseTablesApi,
  pageApprovalFlowsApi,
  updateApprovalFlowApi,
  updateApprovalFlowDesignApi,
  updateApprovalFlowFormApi,
} from '#/api';
import { ADMIN_PAGINATION_PROPS } from '#/utils/pagination';

import FlowDesigner from './flow-designer.vue';

defineOptions({ name: 'AdminNetApprovalFlow' });

type FlowFormState = Partial<SaveApprovalFlowParams>;
type BindingState = {
  configId?: string;
  tableName?: string;
  typeName?: string;
};

const SUPER_ADMIN_ACCOUNT = 999;
const userStore = useUserStore();
const loading = ref(false);
const submitLoading = ref(false);
const bindingLoading = ref(false);
const designerSaving = ref(false);
const editorOpen = ref(false);
const bindingOpen = ref(false);
const detailOpen = ref(false);
const designerOpen = ref(false);
const helpOpen = ref(false);
const formRef = ref<FormInstance>();
const bindingFormRef = ref<FormInstance>();
const records = ref<ApprovalFlowRecord[]>([]);
const currentRecord = ref<ApprovalFlowRecord>();
const detailRecord = ref<ApprovalFlowRecord>();
const databases = ref<string[]>([]);
const tables = ref<Array<{ description?: string; name: string }>>([]);

const query = reactive({
  keyword: '',
  page: 1,
  pageSize: 50,
  total: 0,
});
const formState = reactive<FlowFormState>({});
const bindingState = reactive<BindingState>({});

const isSuperAdmin = computed(
  () =>
    Number((userStore.userInfo as any)?.accountType) === SUPER_ADMIN_ACCOUNT,
);
const editorTitle = computed(() =>
  formState.id ? '编辑流程定义' : '新增流程定义',
);
const configuredFormCount = computed(
  () => records.value.filter((item) => Boolean(item.formJson)).length,
);
const configuredFlowCount = computed(
  () => records.value.filter((item) => Boolean(item.flowJson)).length,
);

const columns: TableColumnsType<ApprovalFlowRecord> = [
  { key: 'index', title: '序号', width: 58 },
  { dataIndex: 'code', key: 'code', title: '编号', width: 140 },
  { dataIndex: 'name', ellipsis: true, key: 'name', title: '名称', width: 190 },
  { key: 'binding', title: '业务表绑定', width: 150 },
  { key: 'flow', title: '流程设计', width: 130 },
  { key: 'status', title: '状态', width: 82 },
  {
    dataIndex: 'remark',
    ellipsis: true,
    key: 'remark',
    title: '备注',
    width: 200,
  },
  { key: 'modifyRecord', title: '修改记录', width: 110 },
  { fixed: 'right', key: 'actions', title: '操作', width: 220 },
];

const formRules: Record<string, Rule[]> = {
  name: [
    { message: '请输入流程名称', required: true, trigger: 'blur' },
    { max: 32, message: '名称最多 32 个字符', trigger: 'blur' },
  ],
  status: [
    {
      message: '请选择状态',
      required: true,
      trigger: 'change',
      type: 'number',
    },
  ],
};
const bindingRules: Record<string, Rule[]> = {
  configId: [{ message: '请选择数据库', required: true, trigger: 'change' }],
  tableName: [{ message: '请选择业务表', required: true, trigger: 'change' }],
  typeName: [{ message: '请选择业务操作', required: true, trigger: 'change' }],
};

const databaseOptions = computed(() =>
  databases.value.map((item) => ({ label: item, value: item })),
);
const tableOptions = computed(() =>
  tables.value.map((item) => ({
    label: item.description ? `${item.name}（${item.description}）` : item.name,
    value: item.name,
  })),
);
const operationOptions = [
  { label: '新增数据（add）', value: 'add' },
  { label: '更新数据（update）', value: 'update' },
  { label: '删除数据（delete）', value: 'delete' },
  { label: '查询数据（select）', value: 'select' },
  { label: '导出数据（export）', value: 'export' },
];

function asFlow(value: unknown) {
  return value as ApprovalFlowRecord;
}

async function loadRecords() {
  if (!isSuperAdmin.value) return;
  loading.value = true;
  try {
    const data = await pageApprovalFlowsApi({
      keyword: query.keyword.trim() || undefined,
      page: query.page,
      pageSize: query.pageSize,
    });
    records.value = data.items ?? [];
    query.total = data.total ?? 0;
  } finally {
    loading.value = false;
  }
}

function resetQuery() {
  query.keyword = '';
  query.page = 1;
  loadRecords();
}

function resetObject(target: Record<string, unknown>) {
  Object.keys(target).forEach((key) => delete target[key]);
}

function openCreate() {
  resetObject(formState);
  Object.assign(formState, { status: 1 });
  editorOpen.value = true;
}

async function openEdit(record: ApprovalFlowRecord) {
  const detail = await getApprovalFlowDetailApi(record.id);
  resetObject(formState);
  Object.assign(formState, {
    code: detail.code,
    id: detail.id,
    name: detail.name,
    remark: detail.remark,
    status: detail.status ?? 1,
  });
  editorOpen.value = true;
}

async function submitFlow() {
  await formRef.value?.validate();
  submitLoading.value = true;
  try {
    const payload = {
      code: formState.code?.trim() || undefined,
      name: formState.name?.trim() ?? '',
      remark: formState.remark?.trim() || undefined,
      status: formState.status ?? 1,
    };
    await (formState.id
      ? updateApprovalFlowApi({ ...payload, id: formState.id })
      : addApprovalFlowApi(payload));
    message.success(formState.id ? '流程定义已更新' : '流程定义已创建');
    editorOpen.value = false;
    await loadRecords();
  } finally {
    submitLoading.value = false;
  }
}

async function openDetail(record: ApprovalFlowRecord) {
  detailRecord.value = await getApprovalFlowDetailApi(record.id);
  detailOpen.value = true;
}

function parseBinding(json?: string): BindingState {
  if (!json) return {};
  try {
    const data = JSON.parse(json) as BindingState;
    return data && typeof data === 'object' ? data : {};
  } catch {
    message.warning('原业务表绑定配置无法解析，取消可保留原数据');
    return {};
  }
}

async function loadTables(configId?: string) {
  tables.value = configId ? await listDatabaseTablesApi(configId) : [];
}

async function openBinding(record: ApprovalFlowRecord) {
  currentRecord.value = await getApprovalFlowDetailApi(record.id);
  resetObject(bindingState);
  Object.assign(bindingState, parseBinding(currentRecord.value.formJson));
  bindingLoading.value = true;
  bindingOpen.value = true;
  try {
    if (databases.value.length === 0)
      databases.value = await listDatabasesApi();
    await loadTables(bindingState.configId);
  } finally {
    bindingLoading.value = false;
  }
}

async function handleDatabaseChange(value: unknown) {
  bindingState.tableName = undefined;
  bindingLoading.value = true;
  try {
    await loadTables(typeof value === 'string' ? value : undefined);
  } finally {
    bindingLoading.value = false;
  }
}

async function submitBinding() {
  await bindingFormRef.value?.validate();
  if (!currentRecord.value) return;
  bindingLoading.value = true;
  try {
    await updateApprovalFlowFormApi(
      currentRecord.value.id,
      JSON.stringify(bindingState),
    );
    message.success('业务表绑定已保存');
    bindingOpen.value = false;
    await loadRecords();
  } finally {
    bindingLoading.value = false;
  }
}

async function openDesigner(record: ApprovalFlowRecord) {
  currentRecord.value = await getApprovalFlowDetailApi(record.id);
  designerOpen.value = true;
}

async function saveDesign(json: string) {
  if (!currentRecord.value) return;
  designerSaving.value = true;
  try {
    await updateApprovalFlowDesignApi(currentRecord.value.id, json);
    message.success('流程设计已保存');
    designerOpen.value = false;
    await loadRecords();
  } finally {
    designerSaving.value = false;
  }
}

function removeRecord(record: ApprovalFlowRecord) {
  Modal.confirm({
    cancelText: '取消',
    centered: true,
    content: `删除后流程定义将不可见，但不会物理清除数据库记录。`,
    okButtonProps: { danger: true },
    okText: '删除',
    onOk: async () => {
      await deleteApprovalFlowApi(record.id);
      message.success('流程定义已删除');
      if (records.value.length === 1 && query.page > 1) query.page -= 1;
      await loadRecords();
    },
    title: `删除“${record.name}”？`,
  });
}

function handlePageChange(page: number, pageSize: number) {
  query.page = pageSize === query.pageSize ? page : 1;
  query.pageSize = pageSize;
  loadRecords();
}

onMounted(loadRecords);
</script>

<template>
  <div class="approval-page">
    <Alert
      v-if="!isSuperAdmin"
      show-icon
      type="warning"
      message="审批流程定义是全局配置，目前仅超级管理员可查看和维护"
      description="该插件的数据表没有租户字段，为避免不同租户互相看到或覆盖配置，系统已在接口层统一限制访问。"
    />

    <template v-else>
      <section class="panel">
        <div class="panel-heading">
          <div>
            <div class="title-row">
              <h2>审批流程</h2>
              <Tag color="orange">定义中心</Tag>
            </div>
            <p>
              配置流程图和业务表触发条件；当前插件尚未提供待办、同意、驳回等运行能力
            </p>
          </div>
          <Space>
            <Button @click="helpOpen = true">
              <template #icon>
                <IconifyIcon icon="lucide:circle-help" /> </template
              >使用说明
            </Button>
            <Button type="primary" @click="openCreate">
              <template #icon><IconifyIcon icon="lucide:plus" /></template
              >新增流程
            </Button>
          </Space>
        </div>

        <div class="summary-strip">
          <div>
            <span>流程总数</span><strong>{{ query.total }}</strong>
          </div>
          <div>
            <span>已绑定业务表</span><strong>{{ configuredFormCount }}</strong>
          </div>
          <div>
            <span>已设计流程图</span><strong>{{ configuredFlowCount }}</strong>
          </div>
        </div>

        <div class="query-bar">
          <Input
            v-model:value="query.keyword"
            allow-clear
            class="keyword-input"
            placeholder="搜索编号、名称或备注"
            @press-enter="loadRecords"
          >
            <template #prefix><IconifyIcon icon="lucide:search" /></template>
          </Input>
          <div class="query-actions">
            <Button
              type="primary"
              @click="
                query.page = 1;
                loadRecords();
              "
            >
              <template #icon><IconifyIcon icon="lucide:search" /></template
              >查询 </Button
            ><Button @click="resetQuery">
              <template #icon>
                <IconifyIcon icon="lucide:rotate-ccw" /> </template
              >重置
            </Button>
          </div>
        </div>

        <Table
          :columns="columns"
          :data-source="records"
          :loading="loading"
          :pagination="false"
          :scroll="{ x: 1280 }"
          row-key="id"
          size="small"
        >
          <template #emptyText><Empty description="暂无流程定义" /></template>
          <template #bodyCell="{ column, record, index }">
            <template v-if="column.key === 'index'">
              {{ (query.page - 1) * query.pageSize + index + 1 }}
            </template>
            <template v-else-if="column.key === 'binding'">
              <Button
                size="small"
                type="link"
                @click="openBinding(asFlow(record))"
              >
                <template #icon>
                  <IconifyIcon icon="lucide:database-zap" />
                </template>
                {{ asFlow(record).formJson ? '已配置' : '去配置' }}
              </Button>
            </template>
            <template v-else-if="column.key === 'flow'">
              <Button
                size="small"
                type="link"
                @click="openDesigner(asFlow(record))"
              >
                <template #icon>
                  <IconifyIcon icon="lucide:workflow" />
                </template>
                {{ asFlow(record).flowJson ? '编辑流程' : '开始设计' }}
              </Button>
            </template>
            <template v-else-if="column.key === 'status'">
              <Tag :color="asFlow(record).status === 1 ? 'green' : 'default'">
                {{ asFlow(record).status === 1 ? '启用' : '停用' }}
              </Tag>
            </template>
            <template v-else-if="column.key === 'modifyRecord'">
              <Popover
                overlay-class-name="approval-record-popover"
                placement="bottomRight"
                trigger="click"
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
                      {{ asFlow(record).createUserName || '无' }}
                    </Descriptions.Item>
                    <Descriptions.Item label="创建时间">
                      {{ asFlow(record).createTime || '无' }}
                    </Descriptions.Item>
                    <Descriptions.Item label="修改者">
                      {{ asFlow(record).updateUserName || '无' }}
                    </Descriptions.Item>
                    <Descriptions.Item label="修改时间">
                      {{ asFlow(record).updateTime || '无' }}
                    </Descriptions.Item>
                  </Descriptions>
                </template>
                <Button size="small" type="link">
                  <template #icon>
                    <IconifyIcon icon="lucide:clock-3" /> </template
                  >详情
                </Button>
              </Popover>
            </template>
            <template v-else-if="column.key === 'actions'">
              <Space :size="2">
                <Button
                  size="small"
                  type="link"
                  @click="openDetail(asFlow(record))"
                >
                  <template #icon><IconifyIcon icon="lucide:eye" /></template
                  >查看
                </Button>
                <Button
                  size="small"
                  type="link"
                  @click="openEdit(asFlow(record))"
                >
                  <template #icon>
                    <IconifyIcon icon="lucide:pencil" /> </template
                  >编辑
                </Button>
                <Tooltip title="删除流程定义">
                  <Button
                    danger
                    size="small"
                    type="link"
                    @click="removeRecord(asFlow(record))"
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

        <div class="pagination-wrap">
          <Pagination
            v-bind="ADMIN_PAGINATION_PROPS"
            v-model:current="query.page"
            v-model:page-size="query.pageSize"
            :show-total="(total: number) => `共 ${total} 条`"
            :total="query.total"
            size="small"
            @change="handlePageChange"
          />
        </div>
      </section>
    </template>

    <Modal
      v-model:open="editorOpen"
      :footer="null"
      :mask-closable="false"
      :title="editorTitle"
      centered
      :width="620"
    >
      <Form
        ref="formRef"
        :model="formState"
        :rules="formRules"
        layout="vertical"
      >
        <div class="two-column-form">
          <Form.Item label="流程编号" name="code">
            <Input
              v-model:value="formState.code"
              allow-clear
              :maxlength="32"
              placeholder="不填则自动生成"
            />
          </Form.Item>
          <Form.Item label="流程名称" name="name">
            <Input
              v-model:value="formState.name"
              allow-clear
              :maxlength="32"
              placeholder="例如：请假审批"
            />
          </Form.Item>
        </div>
        <Form.Item label="状态" name="status">
          <Radio.Group v-model:value="formState.status">
            <Radio :value="1">启用</Radio><Radio :value="2">停用</Radio>
          </Radio.Group>
        </Form.Item>
        <Form.Item label="备注" name="remark">
          <Input.TextArea
            v-model:value="formState.remark"
            :auto-size="{ minRows: 3, maxRows: 5 }"
            :maxlength="256"
            placeholder="说明适用范围和维护责任人"
            show-count
          />
        </Form.Item>
      </Form>
      <div class="modal-footer">
        <Space>
          <Button @click="editorOpen = false">取消</Button
          ><Button :loading="submitLoading" type="primary" @click="submitFlow">
            确定
          </Button>
        </Space>
      </div>
    </Modal>

    <Modal
      v-model:open="bindingOpen"
      :footer="null"
      :mask-closable="false"
      centered
      title="业务表绑定"
      :width="620"
    >
      <Alert
        class="binding-note"
        show-icon
        type="info"
        message="这里只保存触发条件，不会自动操作数据库"
        description="当前插件没有审批运行引擎。该配置供后续接入业务审批时识别数据库、数据表和操作类型。"
      />
      <Form
        ref="bindingFormRef"
        :model="bindingState"
        :rules="bindingRules"
        layout="vertical"
      >
        <Form.Item label="数据库" name="configId">
          <Select
            v-model:value="bindingState.configId"
            :loading="bindingLoading"
            :options="databaseOptions"
            placeholder="选择数据库"
            show-search
            @change="handleDatabaseChange"
          />
        </Form.Item>
        <Form.Item label="业务表" name="tableName">
          <Select
            v-model:value="bindingState.tableName"
            :loading="bindingLoading"
            :options="tableOptions"
            placeholder="选择要匹配的业务表"
            show-search
          />
        </Form.Item>
        <Form.Item label="业务操作" name="typeName">
          <Select
            v-model:value="bindingState.typeName"
            :options="operationOptions"
            placeholder="选择触发审批的操作"
          />
        </Form.Item>
      </Form>
      <div class="modal-footer">
        <Space>
          <Button @click="bindingOpen = false">取消</Button
          ><Button
            :loading="bindingLoading"
            type="primary"
            @click="submitBinding"
          >
            保存绑定
          </Button>
        </Space>
      </div>
    </Modal>

    <Modal
      v-model:open="detailOpen"
      :footer="null"
      centered
      title="流程定义详情"
      :width="680"
    >
      <Descriptions v-if="detailRecord" :column="2" bordered size="small">
        <Descriptions.Item label="编号">
          {{ detailRecord.code || '自动生成' }}
        </Descriptions.Item>
        <Descriptions.Item label="名称">
          {{ detailRecord.name }}
        </Descriptions.Item>
        <Descriptions.Item label="状态">
          <Tag :color="detailRecord.status === 1 ? 'green' : 'default'">
            {{ detailRecord.status === 1 ? '启用' : '停用' }}
          </Tag>
        </Descriptions.Item>
        <Descriptions.Item label="业务表绑定">
          {{ detailRecord.formJson ? '已配置' : '未配置' }}
        </Descriptions.Item>
        <Descriptions.Item label="流程图">
          {{ detailRecord.flowJson ? '已设计' : '未设计' }}
        </Descriptions.Item>
        <Descriptions.Item label="创建人">
          {{ detailRecord.createUserName || '无' }}
        </Descriptions.Item>
        <Descriptions.Item label="创建时间">
          {{ detailRecord.createTime || '无' }}
        </Descriptions.Item>
        <Descriptions.Item label="最后修改">
          {{ detailRecord.updateTime || '无' }}
        </Descriptions.Item>
        <Descriptions.Item label="备注" :span="2">
          {{ detailRecord.remark || '无' }}
        </Descriptions.Item>
      </Descriptions>
    </Modal>

    <Modal
      v-model:open="designerOpen"
      :footer="null"
      :mask-closable="false"
      centered
      class="designer-modal"
      destroy-on-close
      :title="`流程设计：${currentRecord?.name ?? ''}`"
      width="calc(100vw - 32px)"
    >
      <FlowDesigner
        v-if="designerOpen"
        :initial-json="currentRecord?.flowJson"
        :saving="designerSaving"
        @save="saveDesign"
      />
    </Modal>

    <Modal
      v-model:open="helpOpen"
      :footer="null"
      centered
      title="审批流程怎么用"
      :width="720"
    >
      <div class="help-content">
        <Alert
          show-icon
          type="warning"
          message="目前是流程定义工具，不是完整审批系统"
          description="它可以保存流程名称、业务表触发条件和流程图，但不会自动生成待办，也没有同意、驳回、转交、撤回等审批动作。"
        />
        <div class="help-step">
          <strong>1. 新增流程定义</strong
          ><span>填写名称和状态，用来区分请假、采购、报销等流程。</span>
        </div>
        <div class="help-step">
          <strong>2. 配置业务表绑定</strong
          ><span
            >选择业务数据所在的数据库和表，再指定新增、修改、删除、查询或导出哪类操作需要匹配。</span
          >
        </div>
        <div class="help-step">
          <strong>3. 设计流程图</strong
          ><span
            >添加开始、人工审批、条件分支、系统任务和结束节点，通过锚点连接并保存。</span
          >
        </div>
        <div class="help-step">
          <strong>4. 后续才能运行审批</strong
          ><span
            >还需要审批实例、任务队列、审批人规则、状态流转和消息通知等后端能力；这些能力当前源码中不存在。</span
          >
        </div>
      </div>
    </Modal>
  </div>
</template>

<style scoped>
.approval-page {
  min-height: 100%;
  padding: 12px;
  background: hsl(var(--muted) / 35%);
}

.panel {
  min-width: 0;
  padding: 12px;
  background: hsl(var(--background));
  border: 1px solid hsl(var(--border) / 72%);
  border-radius: 8px;
}

.panel-heading {
  display: flex;
  gap: 14px;
  align-items: flex-start;
  justify-content: space-between;
  margin-bottom: 12px;
}

.title-row {
  display: flex;
  gap: 8px;
  align-items: center;
}

.panel-heading h2 {
  margin: 0;
  font-size: 16px;
  font-weight: 650;
  color: hsl(var(--foreground));
}

.panel-heading p {
  margin: 3px 0 0;
  font-size: 12px;
  color: hsl(var(--muted-foreground));
}

.summary-strip {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  margin-bottom: 12px;
  overflow: hidden;
  background: hsl(var(--muted) / 22%);
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
}

.summary-strip div {
  display: flex;
  align-items: center;
  justify-content: space-between;
  min-height: 48px;
  padding: 0 14px;
  border-right: 1px solid hsl(var(--border));
}

.summary-strip div:last-child {
  border-right: 0;
}

.summary-strip span {
  font-size: 12px;
  color: hsl(var(--muted-foreground));
}

.summary-strip strong {
  font-size: 18px;
}

.query-bar {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: center;
  margin-bottom: 12px;
}

.keyword-input {
  flex: 0 1 360px;
  width: min(360px, 100%);
}

.query-actions {
  display: inline-flex;
  flex: none;
  gap: 8px;
}

.pagination-wrap {
  display: flex;
  justify-content: flex-end;
  padding-top: 12px;
}

.two-column-form {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  padding-top: 12px;
  border-top: 1px solid hsl(var(--border));
}

.binding-note {
  margin-bottom: 14px;
}

.help-content {
  display: grid;
  gap: 10px;
}

.help-step {
  display: grid;
  grid-template-columns: 150px 1fr;
  gap: 12px;
  padding: 12px;
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
}

.help-step span {
  line-height: 1.6;
  color: hsl(var(--muted-foreground));
}

:global(.approval-record-popover .modify-record) {
  width: 420px;
}

:global(.approval-record-popover .ant-descriptions-item-label),
:global(.approval-record-popover .ant-descriptions-item-content) {
  padding: 6px 8px !important;
  font-size: 12px;
}

:global(.designer-modal .ant-modal-body) {
  padding: 8px 12px 12px;
}

@media (max-width: 768px) {
  .approval-page {
    padding: 8px;
  }

  .panel-heading {
    flex-direction: column;
    align-items: stretch;
  }

  .summary-strip {
    grid-template-columns: 1fr;
  }

  .summary-strip div {
    border-right: 0;
    border-bottom: 1px solid hsl(var(--border));
  }

  .summary-strip div:last-child {
    border-bottom: 0;
  }

  .keyword-input {
    flex-basis: 100%;
    width: 100%;
  }

  .query-actions {
    width: 100%;
  }

  .query-actions :deep(.ant-btn) {
    flex: 1;
  }

  .two-column-form {
    grid-template-columns: 1fr;
  }

  .help-step {
    grid-template-columns: 1fr;
    gap: 4px;
  }
}
</style>
