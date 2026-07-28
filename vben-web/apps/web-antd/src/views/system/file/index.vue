<script setup lang="ts">
import type {
  UploadFile as AntUploadFile,
  FormInstance,
  TableColumnsType,
} from 'ant-design-vue';
import type { Rule } from 'ant-design-vue/es/form';
import type { Dayjs } from 'dayjs';

import type { SysFileRecord, SysTenantOption, UpdateFileParams } from '#/api';

import { computed, onBeforeUnmount, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';
import { IconifyIcon } from '@vben/icons';
import { useUserStore } from '@vben/stores';

import {
  Button,
  Col,
  DatePicker,
  Descriptions,
  Empty,
  Form,
  Image,
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
  Upload,
} from 'ant-design-vue';

import {
  deleteFileApi,
  downloadFileApi,
  getTenantListApi,
  pageFilesApi,
  previewFileApi,
  updateFileApi,
  uploadFileApi,
} from '#/api';
import { ADMIN_PAGINATION_PROPS } from '#/utils/pagination';

defineOptions({ name: 'AdminNetSystemFile' });

const SUPER_ADMIN_ACCOUNT = 999;
const IMAGE_SUFFIXES = new Set([
  '.bmp',
  '.gif',
  '.jpeg',
  '.jpg',
  '.png',
  '.webp',
]);
const PREVIEW_SUFFIXES = new Set([...IMAGE_SUFFIXES, '.pdf']);
const ACCEPT = '.jpg,.jpeg,.png,.bmp,.gif,.webp,.txt,.xml,.pdf,.xlsx,.docx';
const { hasAccessByCodes } = useAccess();
const userStore = useUserStore();
const loading = ref(false);
const submitting = ref(false);
const uploadOpen = ref(false);
const editOpen = ref(false);
const previewOpen = ref(false);
const previewLoading = ref(false);
const uploadList = ref<AntUploadFile[]>([]);
const tenants = ref<SysTenantOption[]>([]);
const records = ref<SysFileRecord[]>([]);
const total = ref(0);
const editing = reactive<Partial<UpdateFileParams>>({});
const uploadState = reactive({ fileType: '相关文件', isPublic: false });
const editFormRef = ref<FormInstance>();
const dateRange = ref<[Dayjs, Dayjs]>();
const previewRecord = ref<SysFileRecord>();
const previewUrl = ref('');

const query = reactive({
  fileName: '',
  page: 1,
  pageSize: 50,
  suffix: '',
  tenantId: undefined as number | undefined,
});
const isSuperAdmin = computed(
  () =>
    Number((userStore.userInfo as any)?.accountType) === SUPER_ADMIN_ACCOUNT,
);
const tenantOptions = computed(() =>
  tenants.value.map((item) => ({
    label: `${item.label}${item.host ? ` (${item.host})` : ''}`,
    value: item.value,
  })),
);
const fileTypeOptions = [
  { label: '相关文件', value: '相关文件' },
  { label: '归档文件', value: '归档文件' },
];
const columns: TableColumnsType<SysFileRecord> = [
  { key: 'index', title: '序号', width: 58 },
  {
    dataIndex: 'fileName',
    key: 'fileName',
    title: '名称',
    ellipsis: true,
    width: 190,
  },
  { key: 'suffix', title: '格式', width: 82 },
  { key: 'size', title: '大小', width: 92 },
  { key: 'preview', title: '预览', width: 72 },
  {
    dataIndex: 'fileType',
    key: 'fileType',
    title: '文件类别',
    ellipsis: true,
    width: 110,
  },
  { key: 'isPublic', title: '公开', width: 76 },
  {
    dataIndex: 'bucketName',
    key: 'bucketName',
    title: '存储位置',
    ellipsis: true,
    width: 110,
  },
  {
    dataIndex: 'relationName',
    key: 'relation',
    title: '关联对象',
    ellipsis: true,
    width: 160,
  },
  { key: 'modifyRecord', title: '修改记录', width: 108 },
  { fixed: 'right', key: 'actions', title: '操作', width: 154 },
];
const editRules: Record<string, Rule[]> = {
  fileName: [{ required: true, message: '请输入文件名称', trigger: 'blur' }],
};

function can(code: string) {
  return hasAccessByCodes([code]);
}
function asFile(value: unknown) {
  return value as SysFileRecord;
}
function valueText(value: unknown) {
  return value === undefined || value === null || value === ''
    ? '无'
    : String(value);
}
function isImage(record: SysFileRecord) {
  return IMAGE_SUFFIXES.has((record.suffix ?? '').toLowerCase());
}
function canPreview(record: SysFileRecord) {
  return PREVIEW_SUFFIXES.has((record.suffix ?? '').toLowerCase());
}
function formatSize(record: SysFileRecord) {
  if (record.sizeInfo) return record.sizeInfo;
  const kb = Number(record.sizeKb ?? 0);
  return kb >= 1024
    ? `${(kb / 1024).toFixed(kb >= 10_240 ? 0 : 1)} MB`
    : `${kb} KB`;
}
function directFileUrl(record: SysFileRecord) {
  if (record.url && /^https?:\/\//i.test(record.url)) return record.url;
  const path =
    record.url || `${record.filePath ?? ''}/${record.id}${record.suffix ?? ''}`;
  return `/${path.replace(/^\/+/, '')}`;
}
function revokePreviewUrl() {
  if (previewUrl.value) URL.revokeObjectURL(previewUrl.value);
  previewUrl.value = '';
}

async function loadRecords() {
  loading.value = true;
  try {
    const data = await pageFilesApi({
      endTime: dateRange.value?.[1]?.format?.('YYYY-MM-DD HH:mm:ss'),
      fileName: query.fileName || undefined,
      page: query.page,
      pageSize: query.pageSize,
      startTime: dateRange.value?.[0]?.format?.('YYYY-MM-DD HH:mm:ss'),
      suffix: query.suffix || undefined,
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

async function resetQuery() {
  query.fileName = '';
  query.suffix = '';
  dateRange.value = undefined;
  query.page = 1;
  await loadRecords();
}

function openUpload() {
  uploadList.value = [];
  uploadState.fileType = '相关文件';
  uploadState.isPublic = false;
  uploadOpen.value = true;
}

async function submitUpload() {
  const file = uploadList.value[0]?.originFileObj as File | undefined;
  if (!file) return void message.warning('请先选择一个文件');
  if (file.size > 10 * 1024 * 1024)
    return void message.warning('文件不能超过 10MB');
  submitting.value = true;
  try {
    await uploadFileApi(file, uploadState.fileType, uploadState.isPublic);
    message.success('文件上传成功');
    uploadOpen.value = false;
    await loadRecords();
  } finally {
    submitting.value = false;
  }
}

function openEdit(record: SysFileRecord) {
  Object.assign(editing, {
    belongId: record.belongId,
    fileName: record.fileName,
    fileType: record.fileType,
    id: record.id,
    isPublic: Boolean(record.isPublic),
    relationId: record.relationId,
    relationName: record.relationName,
  });
  editOpen.value = true;
}

async function submitEdit() {
  await editFormRef.value?.validate();
  submitting.value = true;
  try {
    await updateFileApi(editing as UpdateFileParams);
    message.success('文件信息已更新');
    editOpen.value = false;
    await loadRecords();
  } finally {
    submitting.value = false;
  }
}

function removeFile(record: SysFileRecord) {
  Modal.confirm({
    content: `将同时删除存储中的实际文件，确定删除“${record.fileName}${record.suffix ?? ''}”吗？`,
    okButtonProps: { danger: true },
    okText: '删除',
    title: '删除文件',
    async onOk() {
      await deleteFileApi(record.id);
      message.success('文件已删除');
      await loadRecords();
    },
  });
}

async function downloadFile(record: SysFileRecord) {
  const blob = await downloadFileApi(record);
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = `${record.fileName ?? record.id}${record.suffix ?? ''}`;
  anchor.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

async function openPreview(record: SysFileRecord) {
  previewRecord.value = record;
  previewOpen.value = true;
  revokePreviewUrl();
  if (!canPreview(record)) return;
  previewLoading.value = true;
  try {
    previewUrl.value = URL.createObjectURL(await previewFileApi(record.id));
  } finally {
    previewLoading.value = false;
  }
}

onBeforeUnmount(revokePreviewUrl);
onMounted(async () => {
  if (isSuperAdmin.value) {
    tenants.value = await getTenantListApi();
    query.tenantId = tenants.value[0]?.value;
  }
  await loadRecords();
});
</script>

<template>
  <div class="file-page">
    <section class="page-panel">
      <div class="panel-heading">
        <div>
          <h2>文件管理</h2>
          <p>统一查看附件、存储位置与业务关联，公开文件可跨租户读取</p>
        </div>
        <Button
          v-if="can('sysFile:uploadFile')"
          type="primary"
          @click="openUpload"
        >
          <template #icon><IconifyIcon icon="lucide:upload" /></template>上传
        </Button>
      </div>
      <div class="query-bar">
        <Select
          v-if="isSuperAdmin"
          v-model:value="query.tenantId"
          :options="tenantOptions"
          placeholder="选择租户"
          @change="handleQuery"
        />
        <Input
          v-model:value="query.fileName"
          allow-clear
          placeholder="文件名称"
          @press-enter="loadRecords"
        />
        <Input
          v-model:value="query.suffix"
          allow-clear
          placeholder="后缀，如 .pdf"
          @press-enter="loadRecords"
        />
        <DatePicker.RangePicker v-model:value="dateRange" show-time />
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
        :scroll="{ x: 1250 }"
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
          <template v-else-if="column.key === 'suffix'">
            <Tag>{{ asFile(record).suffix || '无' }}</Tag>
          </template>
          <template v-else-if="column.key === 'size'">
            {{ formatSize(asFile(record)) }}
          </template>
          <template v-else-if="column.key === 'preview'">
            <button
              v-if="isImage(asFile(record))"
              class="thumb"
              type="button"
              @click="openPreview(asFile(record))"
            >
              <Image
                :fallback="undefined"
                :preview="false"
                :src="directFileUrl(asFile(record))"
              />
            </button>
            <IconifyIcon
              v-else
              class="file-icon"
              :icon="
                asFile(record).suffix === '.pdf'
                  ? 'lucide:file-text'
                  : 'lucide:file'
              "
            />
          </template>
          <template v-else-if="column.key === 'isPublic'">
            <Tag :color="asFile(record).isPublic ? 'green' : 'default'">
              {{ asFile(record).isPublic ? '是' : '否' }}
            </Tag>
          </template>
          <template v-else-if="column.key === 'relation'">
            {{ asFile(record).relationName || '未关联'
            }}<span v-if="asFile(record).relationId" class="muted">
              #{{ asFile(record).relationId }}</span
            >
          </template>
          <template v-else-if="column.key === 'modifyRecord'">
            <Popover
              overlay-class-name="file-record-popover"
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
                    {{
                      valueText(asFile(record).createUserName)
                    }} </Descriptions.Item
                  ><Descriptions.Item label="创建时间">
                    {{
                      valueText(asFile(record).createTime)
                    }} </Descriptions.Item
                  ><Descriptions.Item label="修改者">
                    {{
                      valueText(asFile(record).updateUserName)
                    }} </Descriptions.Item
                  ><Descriptions.Item label="修改时间">
                    {{
                      valueText(asFile(record).updateTime)
                    }} </Descriptions.Item
                  ><Descriptions.Item :span="2" label="存储标识">
                    {{ asFile(record).id }}
                  </Descriptions.Item>
                </Descriptions> </template
              ><Button size="small" type="link">
                <template #icon><IconifyIcon icon="lucide:info" /></template
                >详情
              </Button>
            </Popover>
          </template>
          <template v-else-if="column.key === 'actions'">
            <Space :size="0">
              <Tooltip
                :title="
                  canPreview(asFile(record)) ? '预览' : '该格式不支持在线预览'
                "
              >
                <Button
                  :disabled="!canPreview(asFile(record))"
                  size="small"
                  type="text"
                  @click="openPreview(asFile(record))"
                >
                  <template #icon>
                    <IconifyIcon icon="lucide:eye" />
                  </template>
                </Button>
              </Tooltip>
              <Tooltip title="下载">
                <Button
                  v-if="can('sysFile:downloadFile')"
                  size="small"
                  type="text"
                  @click="downloadFile(asFile(record))"
                >
                  <template #icon>
                    <IconifyIcon icon="lucide:download" />
                  </template>
                </Button>
              </Tooltip>
              <Tooltip title="编辑">
                <Button
                  v-if="can('sysFile:update')"
                  size="small"
                  type="text"
                  @click="openEdit(asFile(record))"
                >
                  <template #icon>
                    <IconifyIcon icon="lucide:square-pen" />
                  </template>
                </Button>
              </Tooltip>
              <Tooltip title="删除">
                <Button
                  v-if="can('sysFile:delete')"
                  danger
                  size="small"
                  type="text"
                  @click="removeFile(asFile(record))"
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
      v-model:open="uploadOpen"
      :confirm-loading="submitting"
      title="上传文件"
      width="540px"
      @ok="submitUpload"
    >
      <div class="modal-form">
        <Row :gutter="16">
          <Col :span="14">
            <label class="field-label">文件类别</label
            ><Select
              v-model:value="uploadState.fileType"
              :options="fileTypeOptions"
              class="full-width"
            /> </Col
          ><Col :span="10">
            <label class="field-label">是否公开</label
            ><Radio.Group v-model:value="uploadState.isPublic">
              <Radio :value="false">否</Radio><Radio :value="true">是</Radio>
            </Radio.Group>
          </Col> </Row
        ><Upload.Dragger
          v-model:file-list="uploadList"
          :accept="ACCEPT"
          :before-upload="() => false"
          :max-count="1"
          class="upload-dragger"
        >
          <p class="upload-icon"><IconifyIcon icon="lucide:cloud-upload" /></p>
          <p>点击或拖拽文件到此处</p>
          <p class="upload-hint">
            支持图片、PDF、Word、Excel、文本，单文件不超过 10MB
          </p>
        </Upload.Dragger>
      </div>
    </Modal>

    <Modal
      v-model:open="editOpen"
      :confirm-loading="submitting"
      title="编辑文件信息"
      width="620px"
      @ok="submitEdit"
    >
      <Form
        ref="editFormRef"
        :label-col="{ span: 7 }"
        :model="editing"
        :rules="editRules"
        class="edit-form"
      >
        <Row :gutter="16">
          <Col :span="24">
            <Form.Item label="文件名称" name="fileName">
              <Input v-model:value="editing.fileName" />
            </Form.Item> </Col
          ><Col :span="12">
            <Form.Item label="文件类别">
              <Select
                v-model:value="editing.fileType"
                :options="fileTypeOptions"
                class="full-width"
              />
            </Form.Item> </Col
          ><Col :span="12">
            <Form.Item label="是否公开">
              <Radio.Group v-model:value="editing.isPublic">
                <Radio :value="false">否</Radio><Radio :value="true">是</Radio>
              </Radio.Group>
            </Form.Item> </Col
          ><Col :span="24">
            <Form.Item label="关联对象名称">
              <Input
                v-model:value="editing.relationName"
                placeholder="例如：合同、工单"
              />
            </Form.Item> </Col
          ><Col :span="12">
            <Form.Item label="关联对象 ID">
              <InputNumber
                v-model:value="editing.relationId"
                :min="0"
                class="full-width"
              />
            </Form.Item> </Col
          ><Col :span="12">
            <Form.Item label="所属 ID">
              <InputNumber
                v-model:value="editing.belongId"
                :min="0"
                class="full-width"
              />
            </Form.Item>
          </Col>
        </Row>
      </Form>
    </Modal>

    <Modal
      v-model:open="previewOpen"
      :footer="null"
      :title="`${previewRecord?.fileName ?? '文件预览'}${previewRecord?.suffix ?? ''}`"
      width="min(900px, 92vw)"
      @after-close="revokePreviewUrl"
    >
      <div class="preview-stage">
        <div v-if="previewLoading" class="preview-loading">正在加载文件...</div>
        <Image
          v-else-if="previewUrl && previewRecord && isImage(previewRecord)"
          :src="previewUrl"
        /><iframe
          v-else-if="previewUrl && previewRecord?.suffix === '.pdf'"
          :src="previewUrl"
          title="PDF 预览"
        ></iframe
        ><Empty v-else description="该格式暂不支持在线预览，请下载后查看">
          <Button
            v-if="previewRecord && can('sysFile:downloadFile')"
            type="primary"
            @click="downloadFile(previewRecord)"
          >
            <template #icon><IconifyIcon icon="lucide:download" /></template
            >下载文件
          </Button>
        </Empty>
      </div>
    </Modal>
  </div>
</template>

<style scoped>
.file-page {
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
  flex-wrap: wrap;
  gap: 8px;
  align-items: center;
  margin-bottom: 12px;
}

.query-bar > .ant-select {
  width: 210px;
}

.query-bar > .ant-input-affix-wrapper {
  width: 180px;
}

.thumb {
  width: 42px;
  height: 42px;
  padding: 2px;
  overflow: hidden;
  background: #f8fafc;
  border: 1px solid #e5e9f0;
  border-radius: 6px;
}

.thumb :deep(.ant-image),
.thumb :deep(img) {
  width: 100%;
  height: 100%;
  object-fit: contain;
}

.file-icon {
  font-size: 24px;
  color: #8090a8;
}

.muted {
  font-size: 12px;
  color: #98a2b3;
}

.modal-form {
  padding: 8px 4px 0;
}

.field-label {
  display: block;
  margin-bottom: 7px;
  font-size: 13px;
  color: #344054;
}

.upload-dragger {
  display: block;
  margin-top: 18px;
}

.upload-icon {
  margin: 0 0 4px;
  font-size: 34px;
  color: #4f6bff;
}

.upload-hint {
  margin: 4px 0 0;
  font-size: 12px;
  color: #8a94a6;
}

.edit-form {
  padding: 8px 4px 0;
}

.edit-form :deep(.ant-form-item) {
  margin-bottom: 16px;
}

.full-width {
  width: 100%;
}

.preview-stage {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 360px;
  overflow: hidden;
  background: #f8fafc;
  border: 1px solid #e7eaf0;
  border-radius: 6px;
}

.preview-stage iframe {
  width: 100%;
  height: 68vh;
  border: 0;
}

.preview-stage :deep(.ant-image-img) {
  max-height: 68vh;
  object-fit: contain;
}

.preview-loading {
  color: #667085;
}
</style>

<style>
.file-record-popover .ant-popover-inner {
  padding: 10px;
  background: #fff;
}

.file-record-popover .ant-descriptions {
  width: 430px;
}
</style>
