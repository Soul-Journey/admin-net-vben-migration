<script setup lang="ts">
import type { FormInstance, TableColumnsType } from 'ant-design-vue';
import type { Rule } from 'ant-design-vue/es/form';

import type { SaveNoticeParams, SysNoticeRecord } from '#/api';

import { computed, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';
import { IconifyIcon } from '@vben/icons';
import { VbenTiptap } from '@vben/plugins/tiptap';
import { useUserStore } from '@vben/stores';

import {
  Button,
  Col,
  Descriptions,
  Form,
  Input,
  message,
  Modal,
  Popover,
  Row,
  Select,
  Space,
  Table,
  Tag,
} from 'ant-design-vue';

import {
  addNoticeApi,
  deleteNoticeApi,
  getDictDataByCodeApi,
  pageNoticesApi,
  publishNoticeApi,
  updateNoticeApi,
} from '#/api';
import { ADMIN_PAGINATION_PROPS } from '#/utils/pagination';

defineOptions({ name: 'AdminNetSystemNotice' });

type NoticeFormState = Partial<SaveNoticeParams> & { id?: number };
type DictOption = { color?: string; label: string; value: number };

const DRAFT_STATUS = 0;
const PUBLISHED_STATUS = 1;

const { hasAccessByCodes } = useAccess();
const userStore = useUserStore();
const loading = ref(false);
const publishingId = ref<number>();
const submitLoading = ref(false);
const modalOpen = ref(false);
const formRef = ref<FormInstance>();
const notices = ref<SysNoticeRecord[]>([]);
const noticeTypes = ref<DictOption[]>([]);
const noticeStatuses = ref<DictOption[]>([]);
const total = ref(0);
const formState = reactive<NoticeFormState>({});
const query = reactive({
  page: 1,
  pageSize: 50,
  title: '',
  type: undefined as number | undefined,
});

const columns: TableColumnsType<SysNoticeRecord> = [
  { key: 'index', title: '序号', width: 58 },
  {
    dataIndex: 'title',
    ellipsis: true,
    key: 'title',
    title: '标题',
    width: 220,
  },
  { key: 'content', title: '内容摘要' },
  { key: 'type', title: '类型', width: 92 },
  { dataIndex: 'createTime', key: 'createTime', title: '创建时间', width: 158 },
  { key: 'status', title: '状态', width: 88 },
  {
    dataIndex: 'publicUserName',
    key: 'publicUserName',
    title: '发布人',
    width: 110,
  },
  { dataIndex: 'publicTime', key: 'publicTime', title: '发布时间', width: 158 },
  { key: 'modifyRecord', title: '修改记录', width: 104 },
  { fixed: 'right', key: 'actions', title: '操作', width: 210 },
];

const formRules: Record<string, Rule[]> = {
  content: [{ message: '请输入公告内容', required: true, trigger: 'change' }],
  title: [{ message: '请输入标题', required: true, trigger: 'blur' }],
  type: [
    {
      message: '请选择类型',
      required: true,
      trigger: 'change',
      type: 'number',
    },
  ],
};

const modalTitle = computed(() =>
  formState.id ? '编辑通知公告' : '新增通知公告',
);
const currentUserId = computed(() =>
  Number(
    (userStore.userInfo as any)?.id ?? (userStore.userInfo as any)?.userId ?? 0,
  ),
);

function can(code: string) {
  return hasAccessByCodes([code]);
}

function asNotice(value: unknown) {
  return value as SysNoticeRecord;
}

function optionLabel(options: DictOption[], value?: number) {
  return (
    options.find((item) => item.value === value)?.label ??
    `未知(${value ?? '-'})`
  );
}

function optionColor(options: DictOption[], value?: number) {
  return options.find((item) => item.value === value)?.color || 'default';
}

function plainText(html = '') {
  return html
    .replaceAll(/<style[\s\S]*?<\/style>/gi, ' ')
    .replaceAll(/<script[\s\S]*?<\/script>/gi, ' ')
    .replaceAll(/<[^>]+>/g, ' ')
    .replaceAll('&nbsp;', ' ')
    .replaceAll('&amp;', '&')
    .replaceAll(/\s+/g, ' ')
    .trim();
}

function isOwner(record: SysNoticeRecord) {
  return (
    !record.createUserId ||
    !currentUserId.value ||
    record.createUserId === currentUserId.value
  );
}

function canChange(record: SysNoticeRecord) {
  return record.status !== PUBLISHED_STATUS && isOwner(record);
}

function resetForm(values: NoticeFormState) {
  for (const key of Object.keys(formState))
    delete formState[key as keyof NoticeFormState];
  Object.assign(formState, values);
}

async function loadDictionaries() {
  const [types, statuses] = await Promise.all([
    getDictDataByCodeApi('NoticeTypeEnum', 1),
    getDictDataByCodeApi('NoticeStatusEnum', 1),
  ]);
  noticeTypes.value = types.map((item) => ({
    color: item.tagType,
    label: item.label,
    value: Number(item.value),
  }));
  noticeStatuses.value = statuses.map((item) => ({
    color: item.tagType,
    label: item.label,
    value: Number(item.value),
  }));
}

async function loadNotices() {
  if (!can('sysNotice:page')) return;
  loading.value = true;
  try {
    const result = await pageNoticesApi({
      page: query.page,
      pageSize: query.pageSize,
      title: query.title || undefined,
      type: query.type,
    });
    notices.value = result.items ?? [];
    total.value = result.total ?? 0;
  } finally {
    loading.value = false;
  }
}

async function handleQuery() {
  query.page = 1;
  await loadNotices();
}

async function resetQuery() {
  query.title = '';
  query.type = undefined;
  await handleQuery();
}

function openCreate() {
  resetForm({ content: '', status: DRAFT_STATUS, title: '', type: 1 });
  modalOpen.value = true;
}

function openEdit(record: SysNoticeRecord) {
  resetForm({ ...record, type: record.type ?? 1 });
  modalOpen.value = true;
}

async function submitNotice() {
  await formRef.value?.validate();
  if (!plainText(formState.content || '')) {
    message.warning('公告内容不能为空');
    return;
  }
  submitLoading.value = true;
  try {
    const payload = { ...formState } as SaveNoticeParams & { id?: number };
    if (payload.id) {
      await updateNoticeApi(payload as SaveNoticeParams & { id: number });
      message.success('通知公告已更新');
    } else {
      await addNoticeApi(payload);
      message.success('通知公告已保存为草稿');
    }
    modalOpen.value = false;
    await loadNotices();
  } finally {
    submitLoading.value = false;
  }
}

function confirmPublish(record: SysNoticeRecord) {
  if (publishingId.value !== undefined) return;
  Modal.confirm({
    centered: true,
    content: `发布“${record.title}”后，后端会为全部账号重建该公告的接收记录并向在线用户广播。当前后端不支持撤回，发布后也不能编辑或删除。确定继续吗？`,
    okText: '确认发布',
    onOk: async () => {
      publishingId.value = record.id;
      try {
        await publishNoticeApi(record.id);
        message.success('通知公告已发布');
        await loadNotices();
      } finally {
        publishingId.value = undefined;
      }
    },
    title: '发布通知公告',
  });
}

function confirmDelete(record: SysNoticeRecord) {
  Modal.confirm({
    centered: true,
    content: `确定删除草稿“${record.title}”吗？后端会同时清理该公告已有的接收关系。`,
    okButtonProps: { danger: true },
    okText: '删除',
    onOk: async () => {
      await deleteNoticeApi(record.id);
      message.success('通知公告已删除');
      await loadNotices();
    },
    title: '删除通知公告',
  });
}

onMounted(async () => {
  await Promise.all([loadDictionaries(), loadNotices()]);
});
</script>

<template>
  <div class="notice-page">
    <section class="panel">
      <div class="panel-head">
        <div>
          <div class="panel-title">通知公告</div>
          <div class="panel-subtitle">维护公告草稿并发布给系统账号</div>
        </div>
      </div>

      <Form :model="query" class="query-form" layout="inline">
        <Form.Item label="标题">
          <Input
            v-model:value="query.title"
            allow-clear
            placeholder="公告标题"
            @press-enter="handleQuery"
          />
        </Form.Item>
        <Form.Item label="类型">
          <Select
            v-model:value="query.type"
            :options="noticeTypes"
            allow-clear
            class="type-select"
            placeholder="全部类型"
          />
        </Form.Item>
        <Form.Item>
          <Space>
            <Button
              v-if="can('sysNotice:page')"
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
              v-if="can('sysNotice:add')"
              type="primary"
              @click="openCreate"
            >
              <template #icon><IconifyIcon icon="lucide:plus" /></template>
              新增
            </Button>
          </Space>
        </Form.Item>
      </Form>

      <Table
        :columns="columns"
        :data-source="notices"
        :loading="loading"
        :pagination="{
          ...ADMIN_PAGINATION_PROPS,
          current: query.page,
          pageSize: query.pageSize,
          showTotal: (value: number) => `共 ${value} 条`,
          total,
        }"
        :scroll="{ x: 1320 }"
        row-key="id"
        size="small"
        @change="
          (pagination: any) => {
            query.page = pagination.current;
            query.pageSize = pagination.pageSize;
            loadNotices();
          }
        "
      >
        <template #bodyCell="{ column, index, record }">
          <template v-if="column.key === 'index'">
            {{ (query.page - 1) * query.pageSize + index + 1 }}
          </template>
          <template v-else-if="column.key === 'content'">
            <span
              class="content-summary"
              :title="plainText(asNotice(record).content)"
            >
              {{ plainText(asNotice(record).content) || '无内容' }}
            </span>
          </template>
          <template v-else-if="column.key === 'type'">
            <Tag :color="optionColor(noticeTypes, asNotice(record).type)">
              {{ optionLabel(noticeTypes, asNotice(record).type) }}
            </Tag>
          </template>
          <template v-else-if="column.key === 'status'">
            <Tag :color="optionColor(noticeStatuses, asNotice(record).status)">
              {{ optionLabel(noticeStatuses, asNotice(record).status) }}
            </Tag>
          </template>
          <template v-else-if="column.key === 'modifyRecord'">
            <Popover
              overlay-class-name="notice-record-popover"
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
                    {{ asNotice(record).createUserName || '无' }}
                  </Descriptions.Item>
                  <Descriptions.Item label="创建时间">
                    {{ asNotice(record).createTime || '无' }}
                  </Descriptions.Item>
                  <Descriptions.Item label="修改者">
                    {{ asNotice(record).updateUserName || '无' }}
                  </Descriptions.Item>
                  <Descriptions.Item label="修改时间">
                    {{ asNotice(record).updateTime || '无' }}
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
              <Button
                v-if="can('sysNotice:public')"
                :disabled="
                  publishingId !== undefined || !canChange(asNotice(record))
                "
                :loading="publishingId === asNotice(record).id"
                size="small"
                type="link"
                @click.stop="confirmPublish(asNotice(record))"
              >
                <template #icon><IconifyIcon icon="lucide:send" /></template>
                发布
              </Button>
              <Button
                v-if="can('sysNotice:update')"
                :disabled="!canChange(asNotice(record))"
                size="small"
                type="link"
                @click.stop="openEdit(asNotice(record))"
              >
                <template #icon>
                  <IconifyIcon icon="lucide:square-pen" />
                </template>
                编辑
              </Button>
              <Button
                v-if="can('sysNotice:delete')"
                :disabled="!canChange(asNotice(record))"
                danger
                size="small"
                type="link"
                @click.stop="confirmDelete(asNotice(record))"
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
      :body-style="{ padding: '14px 20px' }"
      :footer="null"
      :mask-closable="false"
      :title="modalTitle"
      centered
      destroy-on-close
      :width="760"
      @cancel="formRef?.clearValidate()"
    >
      <Form
        ref="formRef"
        :model="formState"
        :rules="formRules"
        layout="vertical"
      >
        <Row :gutter="16">
          <Col :span="16">
            <Form.Item label="标题" name="title">
              <Input
                v-model:value="formState.title"
                :maxlength="64"
                allow-clear
                placeholder="请输入公告标题"
              />
            </Form.Item>
          </Col>
          <Col :span="8">
            <Form.Item label="类型" name="type">
              <Select
                v-model:value="formState.type"
                :options="noticeTypes"
                placeholder="请选择类型"
              />
            </Form.Item>
          </Col>
          <Col :span="24">
            <Form.Item label="内容" name="content">
              <VbenTiptap
                v-model="formState.content"
                :max-height="320"
                :min-height="220"
                placeholder="请输入通知公告内容"
              />
            </Form.Item>
          </Col>
        </Row>
      </Form>
      <div class="form-note">
        保存后为草稿；发布会为全部账号生成接收记录并广播在线用户，当前后端不支持撤回。
      </div>
      <div class="modal-footer">
        <Space>
          <Button @click="modalOpen = false">取消</Button>
          <Button :loading="submitLoading" type="primary" @click="submitNotice">
            保存草稿
          </Button>
        </Space>
      </div>
    </Modal>
  </div>
</template>

<style scoped>
.notice-page {
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

.type-select {
  width: 150px;
}

.content-summary {
  display: block;
  overflow: hidden;
  text-overflow: ellipsis;
  color: hsl(var(--muted-foreground));
  white-space: nowrap;
}

.form-note {
  padding: 9px 12px;
  font-size: 12px;
  line-height: 1.6;
  color: hsl(var(--muted-foreground));
  background: hsl(var(--muted) / 28%);
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  padding-top: 12px;
  margin-top: 14px;
  border-top: 1px solid hsl(var(--border));
}

:global(.notice-record-popover .ant-popover-inner) {
  width: 390px;
  padding: 10px;
  background: #fff;
}

:global(.notice-record-popover .modify-record .ant-descriptions-item-label),
:global(.notice-record-popover .modify-record .ant-descriptions-item-content) {
  padding: 6px 8px;
  font-size: 12px;
}

:global(.notice-page .ant-table-cell) {
  vertical-align: middle;
}

:global(.notice-page .ant-btn-link) {
  padding-inline: 4px;
}

:global(.ant-modal .vben-tiptap) {
  border-radius: 6px;
}
</style>
