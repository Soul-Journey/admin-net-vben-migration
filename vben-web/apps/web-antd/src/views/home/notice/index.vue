<script setup lang="ts">
import type { TableColumnsType } from 'ant-design-vue';

import type { ReceivedNoticeRecord } from '#/api';

import { computed, onMounted, reactive, ref } from 'vue';

import { IconifyIcon } from '@vben/icons';

import {
  Button,
  Form,
  Input,
  Modal,
  Select,
  Space,
  Table,
  Tag,
} from 'ant-design-vue';
import DOMPurify from 'dompurify';

import {
  getDictDataByCodeApi,
  pageReceivedNoticesApi,
  setNoticeReadApi,
} from '#/api';
import { ADMIN_PAGINATION_PROPS } from '#/utils/pagination';

defineOptions({ name: 'AdminNetReceivedNotice' });

type DictOption = { color?: string; label: string; value: number };

const loading = ref(false);
const detailOpen = ref(false);
const markingRead = ref(false);
const receivedNotices = ref<ReceivedNoticeRecord[]>([]);
const selected = ref<ReceivedNoticeRecord>();
const noticeTypes = ref<DictOption[]>([]);
const readStatuses = ref<DictOption[]>([]);
const total = ref(0);
const query = reactive({
  page: 1,
  pageSize: 50,
  title: '',
  type: undefined as number | undefined,
});

const columns: TableColumnsType<ReceivedNoticeRecord> = [
  { key: 'index', title: '序号', width: 58 },
  { key: 'title', title: '标题', width: 230 },
  { key: 'content', responsive: ['md'], title: '内容摘要' },
  { key: 'type', title: '类型', width: 90 },
  { key: 'readStatus', title: '阅读状态', width: 96 },
  { key: 'publisher', responsive: ['xl'], title: '发布者', width: 110 },
  { key: 'publicTime', responsive: ['lg'], title: '发布时间', width: 160 },
  { key: 'readTime', responsive: ['xl'], title: '阅读时间', width: 160 },
  { fixed: 'right', key: 'actions', title: '操作', width: 84 },
];

const safeDetailHtml = computed(() =>
  DOMPurify.sanitize(selected.value?.notice.content ?? '', {
    USE_PROFILES: { html: true },
  }),
);

function optionLabel(options: DictOption[], value?: number) {
  return (
    options.find((item) => item.value === value)?.label ??
    `未知(${value ?? '-'})`
  );
}

function optionColor(options: DictOption[], value?: number) {
  return options.find((item) => item.value === value)?.color || 'default';
}

function asReceived(value: unknown) {
  return value as ReceivedNoticeRecord;
}

function plainText(html = '') {
  return DOMPurify.sanitize(html, { ALLOWED_TAGS: [] })
    .replaceAll('&nbsp;', ' ')
    .replaceAll('&amp;', '&')
    .replaceAll(/\s+/g, ' ')
    .trim();
}

async function loadDictionaries() {
  const [types, statuses] = await Promise.all([
    getDictDataByCodeApi('NoticeTypeEnum', 1),
    getDictDataByCodeApi('NoticeUserStatusEnum', 1),
  ]);
  noticeTypes.value = types.map((item) => ({
    color: item.tagType,
    label: item.label,
    value: Number(item.value),
  }));
  readStatuses.value = statuses.map((item) => ({
    color: item.tagType,
    label: item.label,
    value: Number(item.value),
  }));
}

async function loadNotices() {
  loading.value = true;
  try {
    const result = await pageReceivedNoticesApi({
      page: query.page,
      pageSize: query.pageSize,
      title: query.title || undefined,
      type: query.type,
    });
    receivedNotices.value = result.items ?? [];
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

async function viewDetail(record: ReceivedNoticeRecord) {
  selected.value = record;
  detailOpen.value = true;
  if (record.readStatus === 1 || markingRead.value) return;

  markingRead.value = true;
  try {
    await setNoticeReadApi(record.noticeId);
    record.readStatus = 1;
    record.readTime = new Date().toLocaleString('zh-CN', { hour12: false });
  } finally {
    markingRead.value = false;
  }
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
          <div class="panel-title">站内信</div>
          <div class="panel-subtitle">
            查看系统发布给当前账号的通知和公告，打开未读消息后自动标记已读
          </div>
        </div>
      </div>

      <Form :model="query" class="query-form" layout="inline">
        <Form.Item label="标题">
          <Input
            v-model:value="query.title"
            allow-clear
            placeholder="通知或公告标题"
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
            <Button :loading="loading" type="primary" @click="handleQuery">
              <template #icon><IconifyIcon icon="lucide:search" /></template>
              查询
            </Button>
            <Button @click="resetQuery">
              <template #icon>
                <IconifyIcon icon="lucide:rotate-ccw" />
              </template>
              重置
            </Button>
          </Space>
        </Form.Item>
      </Form>

      <Table
        :columns="columns"
        :data-source="receivedNotices"
        :loading="loading"
        :pagination="{
          ...ADMIN_PAGINATION_PROPS,
          current: query.page,
          pageSize: query.pageSize,
          showTotal: (value: number) => `共 ${value} 条`,
          total,
        }"
        :row-class-name="
          (record: ReceivedNoticeRecord) =>
            record.readStatus === 0 ? 'unread-row' : ''
        "
        :scroll="{ x: 'max-content' }"
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
          <template v-else-if="column.key === 'title'">
            <div class="title-cell">
              <span
                v-if="record.readStatus === 0"
                class="unread-dot"
                aria-label="未读"
              ></span>
              <span
                :class="{ 'title-unread': record.readStatus === 0 }"
                :title="record.notice.title"
              >
                {{ record.notice.title }}
              </span>
            </div>
          </template>
          <template v-else-if="column.key === 'content'">
            <span
              class="content-summary"
              :title="plainText(record.notice.content)"
            >
              {{ plainText(record.notice.content) || '无内容' }}
            </span>
          </template>
          <template v-else-if="column.key === 'type'">
            <Tag :color="optionColor(noticeTypes, record.notice.type)">
              {{ optionLabel(noticeTypes, record.notice.type) }}
            </Tag>
          </template>
          <template v-else-if="column.key === 'readStatus'">
            <Tag :color="optionColor(readStatuses, record.readStatus)">
              {{ optionLabel(readStatuses, record.readStatus) }}
            </Tag>
          </template>
          <template v-else-if="column.key === 'publisher'">
            {{ record.notice.publicUserName || '系统' }}
          </template>
          <template v-else-if="column.key === 'publicTime'">
            {{ record.notice.publicTime || '-' }}
          </template>
          <template v-else-if="column.key === 'readTime'">
            {{ record.readTime || '-' }}
          </template>
          <template v-else-if="column.key === 'actions'">
            <Button
              size="small"
              type="link"
              @click="viewDetail(asReceived(record))"
            >
              <template #icon><IconifyIcon icon="lucide:eye" /></template>
              查看
            </Button>
          </template>
        </template>
      </Table>
    </section>

    <Modal
      v-model:open="detailOpen"
      :footer="null"
      centered
      destroy-on-close
      :width="720"
    >
      <template #title>
        <div class="detail-title">
          {{ selected?.notice.title || '消息详情' }}
        </div>
      </template>
      <div class="detail-meta">
        <Tag :color="optionColor(noticeTypes, selected?.notice.type)">
          {{ optionLabel(noticeTypes, selected?.notice.type) }}
        </Tag>
        <span>{{ selected?.notice.publicUserName || '系统' }}</span>
        <span>{{ selected?.notice.publicTime || '-' }}</span>
        <span v-if="markingRead">正在标记已读...</span>
      </div>
      <!-- eslint-disable-next-line vue/no-v-html -- 内容经过 DOMPurify 清洗后才进入富文本展示区。 -->
      <div class="detail-content" v-html="safeDetailHtml"></div>
      <div class="modal-footer">
        <Button type="primary" @click="detailOpen = false">关闭</Button>
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
  font-size: 16px;
  font-weight: 650;
  color: hsl(var(--foreground));
}

.panel-subtitle {
  margin-top: 3px;
  font-size: 12px;
  color: hsl(var(--muted-foreground));
}

.query-form {
  margin-bottom: 10px;
}

.query-form :deep(.ant-form-item) {
  margin-bottom: 0;
}

.query-form :deep(.ant-input) {
  width: 240px;
}

.type-select {
  width: 150px;
}

.title-cell {
  display: flex;
  gap: 8px;
  align-items: center;
  min-width: 0;
}

.title-cell span:last-child {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.title-unread {
  font-weight: 650;
}

.unread-dot {
  flex: 0 0 7px;
  width: 7px;
  height: 7px;
  background: #1677ff;
  border-radius: 50%;
}

.content-summary {
  display: block;
  overflow: hidden;
  text-overflow: ellipsis;
  color: hsl(var(--muted-foreground));
  white-space: nowrap;
}

:deep(.unread-row > td) {
  background: rgb(22 119 255 / 3%);
}

.detail-title {
  padding-right: 28px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.detail-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  align-items: center;
  padding-bottom: 12px;
  font-size: 12px;
  color: hsl(var(--muted-foreground));
  border-bottom: 1px solid hsl(var(--border));
}

.detail-content {
  min-height: 120px;
  padding: 18px 4px 10px;
  line-height: 1.75;
  overflow-wrap: anywhere;
}

.detail-content :deep(img) {
  max-width: 100%;
  height: auto;
}

.detail-content :deep(table) {
  max-width: 100%;
  border-collapse: collapse;
}

.detail-content :deep(td),
.detail-content :deep(th) {
  padding: 6px 8px;
  border: 1px solid hsl(var(--border));
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  padding-top: 12px;
  margin-top: 14px;
  border-top: 1px solid hsl(var(--border));
}

@media (max-width: 760px) {
  .notice-page {
    padding: 8px;
  }

  .query-form :deep(.ant-form-item) {
    width: 100%;
    margin-bottom: 8px;
  }

  .query-form :deep(.ant-input),
  .type-select {
    width: 100%;
  }
}
</style>
