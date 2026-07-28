<script setup lang="ts">
import type { TableColumnsType } from 'ant-design-vue';
import type { Dayjs } from 'dayjs';

import type { OperationLogRecord, SysTenantOption } from '#/api';

import { computed, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';
import { IconifyIcon } from '@vben/icons';
import { useUserStore } from '@vben/stores';

import {
  Button,
  DatePicker,
  Descriptions,
  Input,
  InputNumber,
  message,
  Modal,
  Select,
  Space,
  Table,
  Tabs,
  Tag,
  Tooltip,
} from 'ant-design-vue';

import {
  clearOperationLogsApi,
  exportOperationLogsApi,
  getOperationLogDetailApi,
  getTenantListApi,
  pageOperationLogsApi,
} from '#/api';
import { ADMIN_PAGINATION_PROPS } from '#/utils/pagination';

defineOptions({ name: 'AdminNetOperationLog' });

const SUPER_ADMIN_ACCOUNT = 999;
const { hasAccessByCodes } = useAccess();
const userStore = useUserStore();
const loading = ref(false);
const detailLoading = ref(false);
const detailOpen = ref(false);
const detail = ref<OperationLogRecord>();
const records = ref<OperationLogRecord[]>([]);
const tenants = ref<SysTenantOption[]>([]);
const total = ref(0);
const dateRange = ref<[Dayjs, Dayjs]>();
const query = reactive({
  account: '',
  actionName: '',
  controllerName: '',
  elapsed: undefined as number | undefined,
  page: 1,
  pageSize: 50,
  remoteIp: '',
  status: undefined as string | undefined,
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
const statusOptions = [
  { label: '成功', value: '200' },
  { label: '失败', value: 'error' },
];
const columns: TableColumnsType<OperationLogRecord> = [
  { key: 'index', title: '序号', width: 58 },
  {
    dataIndex: 'controllerName',
    key: 'controllerName',
    title: '模块',
    ellipsis: true,
    width: 140,
  },
  {
    dataIndex: 'displayTitle',
    key: 'displayTitle',
    title: '操作名称',
    ellipsis: true,
    width: 180,
  },
  { key: 'request', title: '请求', ellipsis: true, width: 260 },
  { dataIndex: 'account', key: 'account', title: '账号', width: 110 },
  { dataIndex: 'remoteIp', key: 'remoteIp', title: 'IP 地址', width: 125 },
  { key: 'status', title: '状态', width: 78 },
  { key: 'elapsed', title: '耗时', sorter: true, width: 94 },
  {
    dataIndex: 'logDateTime',
    key: 'logDateTime',
    title: '日志时间',
    sorter: true,
    width: 170,
  },
  { fixed: 'right', key: 'actions', title: '操作', width: 76 },
];

function can(code: string) {
  return hasAccessByCodes([code]);
}
function asLog(value: unknown) {
  return value as OperationLogRecord;
}
function valueText(value: unknown) {
  return value === undefined || value === null || value === ''
    ? '无'
    : String(value);
}
function levelMeta(level?: number) {
  return (
    (
      {
        1: ['调试', 'blue'],
        2: ['消息', 'green'],
        3: ['警告', 'orange'],
        4: ['错误', 'red'],
      } as Record<number, string[]>
    )[level ?? 0] ?? ['其他', 'default']
  );
}
function formatContent(value?: string) {
  if (!value) return '无';
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}
function currentRange() {
  return {
    endTime: dateRange.value?.[1].format('YYYY-MM-DD HH:mm:ss'),
    startTime: dateRange.value?.[0].format('YYYY-MM-DD HH:mm:ss'),
    tenantId: query.tenantId,
  };
}

async function loadRecords(sorter?: any) {
  loading.value = true;
  try {
    const data = await pageOperationLogsApi({
      ...currentRange(),
      account: query.account || undefined,
      actionName: query.actionName || undefined,
      controllerName: query.controllerName || undefined,
      elapsed: query.elapsed,
      page: query.page,
      pageSize: query.pageSize,
      remoteIp: query.remoteIp || undefined,
      status: query.status,
      field: sorter?.field ?? 'createTime',
      order: sorter?.order ?? 'descending',
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
  Object.assign(query, {
    account: '',
    actionName: '',
    controllerName: '',
    elapsed: undefined,
    page: 1,
    remoteIp: '',
    status: undefined,
  });
  dateRange.value = undefined;
  await loadRecords();
}

async function openDetail(record: OperationLogRecord) {
  detailOpen.value = true;
  detailLoading.value = true;
  try {
    detail.value = await getOperationLogDetailApi(record.id);
  } finally {
    detailLoading.value = false;
  }
}

function clearLogs() {
  const tenantName = tenantOptions.value.find(
    (item) => item.value === query.tenantId,
  )?.label;
  Modal.confirm({
    content: query.tenantId
      ? `将永久删除租户“${tenantName ?? query.tenantId}”的全部操作日志。`
      : '将永久删除所有租户的全部操作日志。',
    okButtonProps: { danger: true },
    okText: '确认清空',
    title: '清空操作日志',
    async onOk() {
      const count = await clearOperationLogsApi(query.tenantId);
      message.success(`已清理 ${count} 条操作日志`);
      await loadRecords();
    },
  });
}

async function exportLogs() {
  const blob = await exportOperationLogsApi(currentRange());
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = `操作日志-${new Date().toISOString().slice(0, 10)}.xlsx`;
  anchor.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

onMounted(async () => {
  if (isSuperAdmin.value) {
    tenants.value = await getTenantListApi();
    query.tenantId = tenants.value[0]?.value;
  }
  await loadRecords();
});
</script>

<template>
  <div class="log-page">
    <section class="page-panel">
      <div class="panel-heading">
        <div>
          <h2>操作日志</h2>
          <p>追踪管理端请求、执行状态和耗时，详情中的凭证类字段自动脱敏</p>
        </div>
        <Space>
          <Button v-if="can('sysOplog:export')" @click="exportLogs">
            <template #icon><IconifyIcon icon="lucide:download" /></template
            >导出 </Button
          ><Button
            v-if="can('sysOplog:clear') && isSuperAdmin"
            danger
            @click="clearLogs"
          >
            <template #icon><IconifyIcon icon="lucide:trash-2" /></template>清空
          </Button>
        </Space>
      </div>
      <div class="query-bar">
        <Select
          v-if="isSuperAdmin"
          v-model:value="query.tenantId"
          :options="tenantOptions"
          placeholder="选择租户"
          @change="handleQuery"
        /><DatePicker.RangePicker v-model:value="dateRange" show-time /><Input
          v-model:value="query.controllerName"
          allow-clear
          placeholder="模块名称"
        /><Input
          v-model:value="query.actionName"
          allow-clear
          placeholder="方法名称"
        /><Input
          v-model:value="query.account"
          allow-clear
          placeholder="账号"
        /><Input
          v-model:value="query.remoteIp"
          allow-clear
          placeholder="IP 地址"
        /><InputNumber
          v-model:value="query.elapsed"
          :min="0"
          placeholder="耗时 ≥ ms"
        /><Select
          v-model:value="query.status"
          allow-clear
          :options="statusOptions"
          placeholder="全部状态"
        /><Button type="primary" @click="handleQuery">
          <template #icon><IconifyIcon icon="lucide:search" /></template
          >查询 </Button
        ><Button @click="resetQuery">
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
        :scroll="{ x: 1320 }"
        row-key="id"
        size="small"
        @change="
          (pagination, _filters, sorter) => {
            query.page = pagination.current ?? 1;
            query.pageSize = pagination.pageSize ?? 50;
            loadRecords(sorter);
          }
        "
      >
        <template #bodyCell="{ column, index, record }">
          <template v-if="column.key === 'index'">
            {{ (query.page - 1) * query.pageSize + index + 1 }} </template
          ><template v-else-if="column.key === 'request'">
            <div class="request-cell">
              <Tag>{{ asLog(record).httpMethod || '未知' }}</Tag
              ><Tooltip :title="asLog(record).requestUrl">
                <span>{{ asLog(record).requestUrl || '无' }}</span>
              </Tooltip>
            </div> </template
          ><template v-else-if="column.key === 'status'">
            <Tag :color="asLog(record).status === '200' ? 'green' : 'red'">
              {{ asLog(record).status === '200' ? '成功' : '失败' }}
            </Tag> </template
          ><template v-else-if="column.key === 'elapsed'">
            <span :class="{ slow: Number(asLog(record).elapsed) >= 1000 }"
              >{{ asLog(record).elapsed ?? 0 }} ms</span
            > </template
          ><template v-else-if="column.key === 'actions'">
            <Tooltip title="查看详情">
              <Button
                size="small"
                type="link"
                @click="openDetail(asLog(record))"
              >
                <template #icon>
                  <IconifyIcon icon="lucide:file-search" /> </template
                >详情
              </Button>
            </Tooltip>
          </template>
        </template>
      </Table>
    </section>

    <Modal
      v-model:open="detailOpen"
      :footer="null"
      title="操作日志详情"
      width="min(980px, 94vw)"
    >
      <div v-if="detailLoading" class="detail-loading">正在加载详情...</div>
      <template v-else-if="detail">
        <Descriptions :column="3" bordered size="small">
          <Descriptions.Item label="模块">
            {{ valueText(detail.controllerName) }} </Descriptions.Item
          ><Descriptions.Item label="操作">
            {{ valueText(detail.displayTitle) }} </Descriptions.Item
          ><Descriptions.Item label="方法">
            {{ valueText(detail.actionName) }} </Descriptions.Item
          ><Descriptions.Item label="请求方式">
            {{ valueText(detail.httpMethod) }} </Descriptions.Item
          ><Descriptions.Item :span="2" label="请求地址">
            {{ valueText(detail.requestUrl) }} </Descriptions.Item
          ><Descriptions.Item label="账号">
            {{ valueText(detail.account) }} /
            {{ valueText(detail.realName) }} </Descriptions.Item
          ><Descriptions.Item label="IP / 地点">
            {{ valueText(detail.remoteIp) }} /
            {{ valueText(detail.location) }} </Descriptions.Item
          ><Descriptions.Item label="浏览器 / 系统">
            {{ valueText(detail.browser) }} /
            {{ valueText(detail.os) }} </Descriptions.Item
          ><Descriptions.Item label="状态">
            <Tag :color="detail.status === '200' ? 'green' : 'red'">
              {{ detail.status === '200' ? '成功' : '失败' }}
            </Tag> </Descriptions.Item
          ><Descriptions.Item label="级别">
            <Tag :color="levelMeta(detail.logLevel)[1]">
              {{ levelMeta(detail.logLevel)[0] }}
            </Tag> </Descriptions.Item
          ><Descriptions.Item label="耗时">
            {{ detail.elapsed ?? 0 }} ms </Descriptions.Item
          ><Descriptions.Item :span="3" label="跟踪 ID">
            {{ valueText(detail.traceId) }}
          </Descriptions.Item> </Descriptions
        ><Tabs class="detail-tabs">
          <Tabs.TabPane key="request" tab="请求参数">
            <pre>{{ formatContent(detail.requestParam) }}</pre></Tabs.TabPane
          ><Tabs.TabPane key="response" tab="返回结果">
            <pre>{{ formatContent(detail.returnResult) }}</pre></Tabs.TabPane
          ><Tabs.TabPane key="message" tab="日志消息">
            <pre>{{ formatContent(detail.message) }}</pre></Tabs.TabPane
          ><Tabs.TabPane key="exception" tab="异常信息">
            <pre>{{ formatContent(detail.exception) }}</pre>
          </Tabs.TabPane>
        </Tabs>
      </template>
    </Modal>
  </div>
</template>

<style scoped>
.log-page {
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
  width: 150px;
}

.query-bar > .ant-input-affix-wrapper {
  width: 140px;
}

.query-bar > .ant-picker {
  width: 310px;
}

.query-bar > .ant-input-number {
  width: 118px;
}

.request-cell {
  display: flex;
  gap: 5px;
  align-items: center;
  min-width: 0;
}

.request-cell span {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.slow {
  font-weight: 600;
  color: #d97706;
}

.detail-loading {
  display: grid;
  place-items: center;
  min-height: 300px;
  color: #667085;
}

.detail-tabs pre {
  min-height: 220px;
  max-height: 48vh;
  padding: 14px;
  overflow: auto;
  font:
    12px/1.7 ui-monospace,
    SFMono-Regular,
    Menlo,
    Consolas,
    monospace;
  color: #27364b;
  overflow-wrap: anywhere;
  white-space: pre-wrap;
  background: #f8fafc;
  border: 1px solid #e7eaf0;
  border-radius: 6px;
}
</style>
