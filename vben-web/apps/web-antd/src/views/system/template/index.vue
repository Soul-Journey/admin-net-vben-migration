<script setup lang="ts">
import type { FormInstance, TableColumnsType } from 'ant-design-vue';
import type { Rule } from 'ant-design-vue/es/form';

import type { SaveTemplateParams, SysTemplateRecord } from '#/api';

import { computed, onMounted, reactive, ref, watch } from 'vue';

import { useAccess } from '@vben/access';
import { IconifyIcon } from '@vben/icons';
import { VbenTiptap } from '@vben/plugins/tiptap';

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
  Tabs,
  Tag,
  Tooltip,
} from 'ant-design-vue';

import {
  addTemplateApi,
  deleteTemplateApi,
  getDictDataByCodeApi,
  listTemplateGroupsApi,
  pageTemplatesApi,
  renderTemplateApi,
  updateTemplateApi,
} from '#/api';
import { ADMIN_PAGINATION_PROPS } from '#/utils/pagination';

defineOptions({ name: 'AdminNetSystemTemplate' });

type TemplateFormState = Partial<SaveTemplateParams> & { id?: number };
type DictOption = { color?: string; label: string; value: number };
type PreviewParameter = { id: number; key: string; value: string };

const FALLBACK_TYPES: DictOption[] = [
  { color: 'blue', label: '通知', value: 1 },
  { color: 'cyan', label: '短信', value: 2 },
  { color: 'purple', label: '邮件', value: 3 },
  { color: 'green', label: '微信', value: 4 },
  { color: 'orange', label: '钉钉', value: 5 },
  { color: 'geekblue', label: '企业微信', value: 7 },
];

const { hasAccessByCodes } = useAccess();
const loading = ref(false);
const submitLoading = ref(false);
const previewLoading = ref(false);
const modalOpen = ref(false);
const formRef = ref<FormInstance>();
const records = ref<SysTemplateRecord[]>([]);
const templateTypes = ref<DictOption[]>([]);
const groupNames = ref<string[]>([]);
const formState = reactive<TemplateFormState>({});
const activeTab = ref('base');
const contentMode = ref<'plain' | 'rich'>('rich');
const parameterDraft = ref('');
const previewParameters = ref<PreviewParameter[]>([]);
const previewResult = ref('');
let parameterSequence = 0;

const query = reactive({
  code: '',
  groupName: undefined as string | undefined,
  name: '',
  page: 1,
  pageSize: 50,
  total: 0,
  type: undefined as number | undefined,
});

const columns: TableColumnsType<SysTemplateRecord> = [
  { key: 'index', title: '序号', width: 58 },
  { dataIndex: 'name', key: 'name', title: '模板名称', width: 190 },
  { dataIndex: 'code', key: 'code', title: '模板编码', width: 190 },
  { key: 'type', title: '模板类型', width: 105 },
  { dataIndex: 'groupName', key: 'groupName', title: '分组编码', width: 145 },
  { dataIndex: 'orderNo', key: 'orderNo', title: '排序', width: 76 },
  { key: 'modifyRecord', title: '修改记录', width: 104 },
  { fixed: 'right', key: 'actions', title: '操作', width: 150 },
];

const formRules: Record<string, Rule[]> = {
  code: [{ message: '请输入模板编码', required: true, trigger: 'blur' }],
  content: [{ message: '请输入模板内容', required: true, trigger: 'change' }],
  groupName: [{ message: '请输入模板分组', required: true, trigger: 'blur' }],
  name: [{ message: '请输入模板名称', required: true, trigger: 'blur' }],
  type: [
    {
      message: '请选择模板类型',
      required: true,
      trigger: 'change',
      type: 'number',
    },
  ],
};

const modalTitle = computed(() =>
  formState.id ? '编辑消息模板' : '新增消息模板',
);
const groupOptions = computed(() =>
  groupNames.value.map((value) => ({ label: value, value })),
);
const previewDocument = computed(
  () => `<!doctype html>
<html><head><meta charset="utf-8"><meta name="color-scheme" content="light dark">
<style>body{margin:0;padding:14px;font:14px/1.65 system-ui,sans-serif;overflow-wrap:anywhere}img{max-width:100%;height:auto}pre{white-space:pre-wrap}</style>
</head><body>${contentMode.value === 'plain' ? `<pre>${escapeHtml(previewResult.value)}</pre>` : previewResult.value}</body></html>`,
);

function can(code: string) {
  return hasAccessByCodes([code]);
}

function asTemplate(value: unknown) {
  return value as SysTemplateRecord;
}

function typeOption(value?: number) {
  return (
    templateTypes.value.find((item) => item.value === value) ?? {
      color: 'default',
      label: `未知(${value ?? '-'})`,
      value: value ?? 0,
    }
  );
}

function escapeHtml(value = '') {
  return value
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#039;');
}

function hasHtml(value = '') {
  return /<([a-z][\w-]*)\b[^>]*>/i.test(value);
}

function resetForm(values: TemplateFormState) {
  for (const key of Object.keys(formState)) {
    delete formState[key as keyof TemplateFormState];
  }
  Object.assign(formState, values);
}

function extractParameterKeys(content = '') {
  return [
    ...new Set(
      [...content.matchAll(/@\(([^)]+)\)/g)]
        .map((match) => match[1]?.trim())
        .filter(Boolean),
    ),
  ] as string[];
}

function syncPreviewParameters(content = '') {
  for (const key of extractParameterKeys(content)) {
    if (!previewParameters.value.some((item) => item.key === key)) {
      previewParameters.value.push({
        id: ++parameterSequence,
        key,
        value: `示例${previewParameters.value.length + 1}`,
      });
    }
  }
}

async function loadOptions() {
  const [types, groups] = await Promise.all([
    getDictDataByCodeApi('TemplateTypeEnum', 1).catch(() => []),
    listTemplateGroupsApi().catch(() => []),
  ]);
  templateTypes.value =
    types.length > 0
      ? types.map((item) => ({
          color: item.tagType,
          label: item.label,
          value: Number(item.value),
        }))
      : FALLBACK_TYPES;
  groupNames.value = [...new Set(groups.filter(Boolean))].toSorted();
}

async function loadTemplates() {
  if (!can('sysTemplate:page')) return;
  loading.value = true;
  try {
    const result = await pageTemplatesApi({
      code: query.code.trim() || undefined,
      groupName: query.groupName || undefined,
      name: query.name.trim() || undefined,
      page: query.page,
      pageSize: query.pageSize,
      type: query.type,
    });
    records.value = result.items ?? [];
    query.total = Number(result.total ?? 0);
  } finally {
    loading.value = false;
  }
}

async function handleQuery() {
  query.page = 1;
  await loadTemplates();
}

async function resetQuery() {
  query.code = '';
  query.groupName = undefined;
  query.name = '';
  query.type = undefined;
  await handleQuery();
}

function prepareEditor(record?: SysTemplateRecord) {
  const content = record?.content ?? '';
  resetForm(
    record
      ? { ...record }
      : {
          code: '',
          content: '',
          groupName: '',
          name: '',
          orderNo: 100,
          remark: '',
          type: 1,
        },
  );
  activeTab.value = 'base';
  contentMode.value = hasHtml(content) ? 'rich' : 'plain';
  parameterDraft.value = '';
  previewResult.value = '';
  previewParameters.value = [];
  syncPreviewParameters(content);
  modalOpen.value = true;
}

function insertParameter() {
  const key = parameterDraft.value.trim().replaceAll(/[()@\s]/g, '');
  if (!key) {
    message.warning('请输入参数名');
    return;
  }
  const token = `@(${key})`;
  formState.content = `${formState.content ?? ''}${formState.content ? ' ' : ''}${token}`;
  syncPreviewParameters(formState.content);
  parameterDraft.value = '';
}

function addPreviewParameter() {
  previewParameters.value.push({
    id: ++parameterSequence,
    key: '',
    value: '',
  });
}

function removePreviewParameter(id: number) {
  previewParameters.value = previewParameters.value.filter(
    (item) => item.id !== id,
  );
}

function buildPreviewData() {
  const data: Record<string, string> = {};
  for (const item of previewParameters.value) {
    const key = item.key.trim();
    if (!key) continue;
    if (Object.hasOwn(data, key)) {
      throw new Error(`预览参数“${key}”重复`);
    }
    data[key] = item.value;
  }
  return data;
}

async function previewTemplate() {
  if (!formState.content?.trim()) {
    activeTab.value = 'content';
    message.warning('请先填写模板内容');
    return;
  }
  previewLoading.value = true;
  try {
    previewResult.value = await renderTemplateApi(
      formState.content,
      buildPreviewData(),
    );
    activeTab.value = 'content';
  } catch (error) {
    if (error instanceof Error && error.message.startsWith('预览参数')) {
      message.warning(error.message);
      return;
    }
    throw error;
  } finally {
    previewLoading.value = false;
  }
}

async function submitTemplate() {
  await formRef.value?.validate();
  const payload = {
    ...formState,
    code: formState.code?.trim(),
    content: formState.content?.trim(),
    groupName: formState.groupName?.trim(),
    name: formState.name?.trim(),
    orderNo: formState.orderNo ?? 100,
    remark: formState.remark?.trim() || undefined,
    type: formState.type ?? 1,
  } as SaveTemplateParams & { id?: number };
  submitLoading.value = true;
  try {
    if (payload.id) {
      await updateTemplateApi(payload as SaveTemplateParams & { id: number });
      message.success('消息模板已更新');
    } else {
      await addTemplateApi(payload);
      message.success('消息模板已新增');
    }
    modalOpen.value = false;
    await Promise.all([loadTemplates(), loadOptions()]);
  } finally {
    submitLoading.value = false;
  }
}

function confirmDelete(record: SysTemplateRecord) {
  Modal.confirm({
    centered: true,
    content: `删除后，使用编码“${record.code}”发送消息的业务将无法获取该模板。`,
    okButtonProps: { danger: true },
    okText: '删除',
    title: `删除模板“${record.name}”？`,
    async onOk() {
      await deleteTemplateApi(record.id);
      message.success('消息模板已删除');
      if (records.value.length === 1 && query.page > 1) query.page -= 1;
      await Promise.all([loadTemplates(), loadOptions()]);
    },
  });
}

watch(
  () => formState.content,
  (content) => syncPreviewParameters(content),
);

onMounted(async () => {
  await Promise.all([loadOptions(), loadTemplates()]);
});
</script>

<template>
  <div class="template-page">
    <section class="panel">
      <div class="panel-head">
        <div>
          <div class="panel-title">模板管理</div>
          <div class="panel-subtitle">维护通知、短信、邮件和第三方消息内容</div>
        </div>
      </div>

      <Form :model="query" class="query-form" layout="inline">
        <Form.Item label="名称">
          <Input
            v-model:value="query.name"
            allow-clear
            placeholder="模板名称"
            @press-enter="handleQuery"
          />
        </Form.Item>
        <Form.Item label="编码">
          <Input
            v-model:value="query.code"
            allow-clear
            placeholder="模板编码"
            @press-enter="handleQuery"
          />
        </Form.Item>
        <Form.Item label="类型">
          <Select
            v-model:value="query.type"
            :options="templateTypes"
            allow-clear
            class="query-select"
            placeholder="全部类型"
          />
        </Form.Item>
        <Form.Item label="分组">
          <Select
            v-model:value="query.groupName"
            :options="groupOptions"
            allow-clear
            class="query-select"
            placeholder="全部分组"
          />
        </Form.Item>
        <Form.Item>
          <Space>
            <Button
              v-if="can('sysTemplate:page')"
              :loading="loading"
              type="primary"
              @click="handleQuery"
            >
              <template #icon><IconifyIcon icon="lucide:search" /></template>
              查询
            </Button>
            <Button @click="resetQuery">
              <template #icon>
                <IconifyIcon icon="lucide:rotate-ccw" />
              </template>
              重置
            </Button>
            <Button
              v-if="can('sysTemplate:add')"
              type="primary"
              @click="prepareEditor()"
            >
              <template #icon><IconifyIcon icon="lucide:plus" /></template>
              新增
            </Button>
          </Space>
        </Form.Item>
      </Form>

      <Table
        :columns="columns"
        :data-source="records"
        :loading="loading"
        :pagination="{
          ...ADMIN_PAGINATION_PROPS,
          current: query.page,
          pageSize: query.pageSize,
          showTotal: (total: number) => `共 ${total} 条`,
          total: query.total,
        }"
        row-key="id"
        :scroll="{ x: 1030 }"
        size="small"
        @change="
          (pagination: any) => {
            query.page = pagination.current || 1;
            query.pageSize = pagination.pageSize || 50;
            loadTemplates();
          }
        "
      >
        <template #bodyCell="{ column, index, record }">
          <template v-if="column.key === 'index'">
            {{ (query.page - 1) * query.pageSize + index + 1 }}
          </template>
          <template v-else-if="column.key === 'name'">
            <div class="name-cell">
              <strong>{{ asTemplate(record).name }}</strong>
              <small>{{ asTemplate(record).remark || '未填写备注' }}</small>
            </div>
          </template>
          <template v-else-if="column.key === 'type'">
            <Tag :color="typeOption(asTemplate(record).type).color">
              {{ typeOption(asTemplate(record).type).label }}
            </Tag>
          </template>
          <template v-else-if="column.key === 'modifyRecord'">
            <Popover
              overlay-class-name="template-record-popover"
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
                    {{ asTemplate(record).createUserName || '无' }}
                  </Descriptions.Item>
                  <Descriptions.Item label="创建时间">
                    {{ asTemplate(record).createTime || '无' }}
                  </Descriptions.Item>
                  <Descriptions.Item label="修改者">
                    {{ asTemplate(record).updateUserName || '无' }}
                  </Descriptions.Item>
                  <Descriptions.Item label="修改时间">
                    {{ asTemplate(record).updateTime || '无' }}
                  </Descriptions.Item>
                  <Descriptions.Item :span="2" label="备注">
                    {{ asTemplate(record).remark || '无' }}
                  </Descriptions.Item>
                </Descriptions>
              </template>
              <Button size="small" type="link">
                <template #icon><IconifyIcon icon="lucide:info" /></template>
                详情
              </Button>
            </Popover>
          </template>
          <template v-else-if="column.key === 'actions'">
            <Space :size="2">
              <Tooltip title="编辑模板">
                <Button
                  v-if="can('sysTemplate:update')"
                  size="small"
                  type="link"
                  @click.stop="prepareEditor(asTemplate(record))"
                >
                  <template #icon>
                    <IconifyIcon icon="lucide:square-pen" />
                  </template>
                  编辑
                </Button>
              </Tooltip>
              <Tooltip title="删除模板">
                <Button
                  v-if="can('sysTemplate:delete')"
                  danger
                  size="small"
                  type="link"
                  @click.stop="confirmDelete(asTemplate(record))"
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
      :body-style="{ padding: '10px 20px 14px' }"
      :footer="null"
      :mask-closable="false"
      :title="modalTitle"
      centered
      destroy-on-close
      :width="800"
      @cancel="formRef?.clearValidate()"
    >
      <Form
        ref="formRef"
        :model="formState"
        :rules="formRules"
        layout="vertical"
      >
        <Tabs v-model:active-key="activeTab" class="template-tabs">
          <Tabs.TabPane key="base" tab="基础信息">
            <Row :gutter="16">
              <Col :span="12">
                <Form.Item label="模板名称" name="name">
                  <Input
                    v-model:value="formState.name"
                    :maxlength="128"
                    allow-clear
                    placeholder="例如：登录验证码"
                  />
                </Form.Item>
              </Col>
              <Col :span="12">
                <Form.Item label="模板编码" name="code">
                  <Input
                    v-model:value="formState.code"
                    :maxlength="128"
                    allow-clear
                    placeholder="例如：LoginCode"
                  />
                </Form.Item>
              </Col>
              <Col :span="12">
                <Form.Item label="分组编码" name="groupName">
                  <AutoComplete
                    v-model:value="formState.groupName"
                    :options="groupOptions"
                    :maxlength="32"
                    allow-clear
                    placeholder="选择已有分组或输入新分组"
                  />
                </Form.Item>
              </Col>
              <Col :span="8">
                <Form.Item label="模板类型" name="type">
                  <Select
                    v-model:value="formState.type"
                    :options="templateTypes"
                    placeholder="请选择类型"
                  />
                </Form.Item>
              </Col>
              <Col :span="4">
                <Form.Item label="排序" name="orderNo">
                  <InputNumber
                    v-model:value="formState.orderNo"
                    class="w-full"
                    :min="0"
                  />
                </Form.Item>
              </Col>
              <Col :span="24">
                <Form.Item label="备注" name="remark">
                  <Input.TextArea
                    v-model:value="formState.remark"
                    :auto-size="{ minRows: 2, maxRows: 3 }"
                    :maxlength="128"
                    placeholder="可选"
                    show-count
                  />
                </Form.Item>
              </Col>
            </Row>
          </Tabs.TabPane>

          <Tabs.TabPane key="content" tab="模板内容">
            <div class="content-toolbar">
              <Radio.Group
                v-model:value="contentMode"
                button-style="solid"
                size="small"
              >
                <Radio.Button value="rich">富文本</Radio.Button>
                <Radio.Button value="plain">纯文本</Radio.Button>
              </Radio.Group>
              <Input
                v-model:value="parameterDraft"
                class="parameter-input"
                placeholder="参数名，如 name"
                @press-enter="insertParameter"
              />
              <Button size="small" @click="insertParameter">
                <template #icon><IconifyIcon icon="lucide:braces" /></template>
                插入参数
              </Button>
            </div>

            <Form.Item label="内容" name="content">
              <VbenTiptap
                v-if="contentMode === 'rich'"
                v-model="formState.content"
                :max-height="260"
                :min-height="190"
                placeholder="输入模板内容，参数格式为 @(name)"
              />
              <Input.TextArea
                v-else
                v-model:value="formState.content"
                :auto-size="{ minRows: 8, maxRows: 12 }"
                placeholder="输入纯文本内容，参数格式为 @(name)"
              />
            </Form.Item>

            <div class="preview-head">
              <div>
                <strong>预览参数</strong>
                <span>参数名对应模板中的 @(参数名)</span>
              </div>
              <Button size="small" type="text" @click="addPreviewParameter">
                <template #icon><IconifyIcon icon="lucide:plus" /></template>
              </Button>
            </div>
            <div v-if="previewParameters.length > 0" class="parameter-list">
              <div
                v-for="item in previewParameters"
                :key="item.id"
                class="parameter-row"
              >
                <Input v-model:value="item.key" placeholder="参数名" />
                <Input v-model:value="item.value" placeholder="预览值" />
                <Tooltip title="删除参数">
                  <Button
                    danger
                    size="small"
                    type="text"
                    @click="removePreviewParameter(item.id)"
                  >
                    <template #icon>
                      <IconifyIcon icon="lucide:trash-2" />
                    </template>
                  </Button>
                </Tooltip>
              </div>
            </div>
            <div v-else class="empty-parameters">
              模板中暂未识别到参数，可手动添加预览参数
            </div>

            <div v-if="previewResult" class="preview-panel">
              <div class="preview-label">预览结果</div>
              <iframe
                :srcdoc="previewDocument"
                sandbox=""
                title="模板预览"
              ></iframe>
            </div>
          </Tabs.TabPane>
        </Tabs>
      </Form>

      <div class="modal-footer">
        <Space>
          <Button @click="modalOpen = false">取消</Button>
          <Button
            v-if="can('sysTemplate:preview')"
            :loading="previewLoading"
            @click="previewTemplate"
          >
            <template #icon><IconifyIcon icon="lucide:eye" /></template>
            预览
          </Button>
          <Button
            :loading="submitLoading"
            type="primary"
            @click="submitTemplate"
          >
            保存
          </Button>
        </Space>
      </div>
    </Modal>
  </div>
</template>

<style scoped>
.template-page {
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

.panel-head {
  margin-bottom: 10px;
}

.panel-title {
  font-size: 14px;
  font-weight: 700;
  color: hsl(var(--foreground));
}

.panel-subtitle {
  margin-top: 4px;
  font-size: 12px;
  color: hsl(var(--muted-foreground));
}

.query-form {
  gap: 8px 0;
  margin-bottom: 10px;
}

.query-form :deep(.ant-input) {
  width: 165px;
}

.query-select {
  width: 145px;
}

.name-cell {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
}

.name-cell strong,
.name-cell small {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.name-cell small {
  font-size: 11px;
  font-weight: 400;
  color: hsl(var(--muted-foreground));
}

.content-toolbar {
  display: flex;
  gap: 8px;
  align-items: center;
  margin-bottom: 10px;
}

.parameter-input {
  width: 190px;
  margin-left: auto;
}

.preview-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: 4px;
}

.preview-head > div {
  display: flex;
  gap: 8px;
  align-items: baseline;
}

.preview-head span {
  font-size: 11px;
  color: hsl(var(--muted-foreground));
}

.parameter-list {
  display: grid;
  gap: 6px;
  max-height: 126px;
  padding: 8px;
  overflow-y: auto;
  background: hsl(var(--muted) / 22%);
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
}

.parameter-row {
  display: grid;
  grid-template-columns: 180px minmax(0, 1fr) 32px;
  gap: 6px;
}

.empty-parameters {
  padding: 12px;
  font-size: 12px;
  color: hsl(var(--muted-foreground));
  text-align: center;
  border: 1px dashed hsl(var(--border));
  border-radius: 6px;
}

.preview-panel {
  margin-top: 10px;
  overflow: hidden;
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
}

.preview-label {
  padding: 6px 10px;
  font-size: 12px;
  font-weight: 600;
  background: hsl(var(--muted) / 30%);
  border-bottom: 1px solid hsl(var(--border));
}

.preview-panel iframe {
  display: block;
  width: 100%;
  height: 170px;
  background: white;
  border: 0;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  padding-top: 12px;
  margin-top: 10px;
  border-top: 1px solid hsl(var(--border));
}

:global(.template-record-popover .ant-popover-inner) {
  width: 390px;
  padding: 10px;
  background: #fff;
}

:global(.template-record-popover .ant-descriptions-item-label),
:global(.template-record-popover .ant-descriptions-item-content) {
  padding: 6px 8px;
  font-size: 12px;
}

:global(.template-page .ant-table-cell) {
  vertical-align: middle;
}

:global(.template-page .ant-btn-link) {
  padding-inline: 4px;
}

@media (max-width: 900px) {
  .content-toolbar {
    flex-wrap: wrap;
    align-items: stretch;
  }

  .parameter-input {
    width: 180px;
    margin-left: 0;
  }
}
</style>
