<script setup lang="ts">
import type { TableColumnsType } from 'ant-design-vue';
import type { Dayjs } from 'dayjs';

import type { SysTenantOption, VisitLogRecord } from '#/api';

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
  Table,
  Tag,
  Tooltip,
} from 'ant-design-vue';

import { clearVisitLogsApi, getTenantListApi, pageVisitLogsApi } from '#/api';
import { ADMIN_PAGINATION_PROPS } from '#/utils/pagination';

defineOptions({ name: 'AdminNetVisitLog' });

const SUPER_ADMIN_ACCOUNT = 999;
const { hasAccessByCodes } = useAccess();
const userStore = useUserStore();
const loading = ref(false);
const detailOpen = ref(false);
const current = ref<VisitLogRecord>();
const records = ref<VisitLogRecord[]>([]);
const tenants = ref<SysTenantOption[]>([]);
const total = ref(0);
const dateRange = ref<[Dayjs, Dayjs]>();
const query = reactive({
  account: '',
  actionName: '',
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
const columns: TableColumnsType<VisitLogRecord> = [
  { key: 'index', title: '序号', width: 58 },
  {
    dataIndex: 'displayTitle',
    key: 'displayTitle',
    title: '访问动作',
    ellipsis: true,
    width: 170,
  },
  { dataIndex: 'account', key: 'account', title: '账号', width: 110 },
  { dataIndex: 'realName', key: 'realName', title: '姓名', width: 105 },
  { dataIndex: 'remoteIp', key: 'remoteIp', title: 'IP 地址', width: 130 },
  {
    dataIndex: 'location',
    key: 'location',
    title: '访问地点',
    ellipsis: true,
    width: 150,
  },
  { key: 'client', title: '客户端', ellipsis: true, width: 210 },
  { key: 'status', title: '状态', width: 78 },
  { key: 'elapsed', title: '耗时', width: 92 },
  {
    dataIndex: 'logDateTime',
    key: 'logDateTime',
    title: '访问时间',
    width: 170,
  },
  { fixed: 'right', key: 'actions', title: '操作', width: 76 },
];

function can(code: string) {
  return hasAccessByCodes([code]);
}
function asLog(value: unknown) {
  return value as VisitLogRecord;
}
function valueText(value: unknown) {
  return value === undefined || value === null || value === ''
    ? '无'
    : String(value);
}

async function loadRecords() {
  loading.value = true;
  try {
    const data = await pageVisitLogsApi({
      account: query.account || undefined,
      actionName: query.actionName || undefined,
      elapsed: query.elapsed,
      endTime: dateRange.value?.[1].format('YYYY-MM-DD HH:mm:ss'),
      page: query.page,
      pageSize: query.pageSize,
      remoteIp: query.remoteIp || undefined,
      startTime: dateRange.value?.[0].format('YYYY-MM-DD HH:mm:ss'),
      status: query.status,
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
  Object.assign(query, {
    account: '',
    actionName: '',
    elapsed: undefined,
    page: 1,
    remoteIp: '',
    status: undefined,
  });
  dateRange.value = undefined;
  await loadRecords();
}

function showDetail(record: VisitLogRecord) {
  current.value = record;
  detailOpen.value = true;
}

function clearLogs() {
  const tenantName = tenantOptions.value.find(
    (item) => item.value === query.tenantId,
  )?.label;
  Modal.confirm({
    content: query.tenantId
      ? `将永久删除租户“${tenantName ?? query.tenantId}”的全部访问日志。`
      : '将永久删除所有租户的全部访问日志。',
    okButtonProps: { danger: true },
    okText: '确认清空',
    title: '清空访问日志',
    async onOk() {
      const count = await clearVisitLogsApi(query.tenantId);
      message.success(`已清理 ${count} 条访问日志`);
      await loadRecords();
    },
  });
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
          <h2>访问日志</h2>
          <p>查看用户登录、退出和访问行为，快速定位异常来源与慢请求</p>
        </div>
        <Button
          v-if="can('sysVislog:clear') && isSuperAdmin"
          danger
          @click="clearLogs"
        >
          <template #icon><IconifyIcon icon="lucide:trash-2" /></template>清空
        </Button>
      </div>
      <div class="query-bar">
        <Select
          v-if="isSuperAdmin"
          v-model:value="query.tenantId"
          class="query-control tenant-control"
          :options="tenantOptions"
          placeholder="选择租户"
          @change="handleQuery"
        />
        <DatePicker.RangePicker
          v-model:value="dateRange"
          class="query-control date-control"
          show-time
        />
        <Input
          v-model:value="query.actionName"
          allow-clear
          class="query-control"
          placeholder="方法名称"
        />
        <Input
          v-model:value="query.account"
          allow-clear
          class="query-control"
          placeholder="账号"
        />
        <Input
          v-model:value="query.remoteIp"
          allow-clear
          class="query-control"
          placeholder="IP 地址"
        />
        <InputNumber
          v-model:value="query.elapsed"
          class="query-control elapsed-control"
          :min="0"
          placeholder="耗时 >= ms"
        />
        <Select
          v-model:value="query.status"
          allow-clear
          class="query-control status-control"
          :options="statusOptions"
          placeholder="全部状态"
        />
        <div class="query-actions">
          <Button type="primary" @click="handleQuery">
            <template #icon><IconifyIcon icon="lucide:search" /></template
            >查询 </Button
          ><Button @click="resetQuery">
            <template #icon><IconifyIcon icon="lucide:rotate-ccw" /></template
            >重置
          </Button>
        </div>
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
        :scroll="{ x: 1350 }"
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
          <template v-else-if="column.key === 'client'">
            <Tooltip
              :title="`${asLog(record).browser || '未知浏览器'} / ${asLog(record).os || '未知系统'}`"
            >
              <span
                >{{ asLog(record).browser || '未知浏览器' }} /
                {{ asLog(record).os || '未知系统' }}</span
              >
            </Tooltip>
          </template>
          <template v-else-if="column.key === 'status'">
            <Tag :color="asLog(record).status === '200' ? 'green' : 'red'">
              {{ asLog(record).status === '200' ? '成功' : '失败' }}
            </Tag>
          </template>
          <template v-else-if="column.key === 'elapsed'">
            <span :class="{ slow: Number(asLog(record).elapsed) >= 1000 }"
              >{{ asLog(record).elapsed ?? 0 }} ms</span
            >
          </template>
          <template v-else-if="column.key === 'actions'">
            <Tooltip title="查看详情">
              <Button
                size="small"
                type="link"
                @click="showDetail(asLog(record))"
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
      title="访问日志详情"
      width="min(760px, 94vw)"
    >
      <Descriptions v-if="current" :column="2" bordered size="small">
        <Descriptions.Item label="访问动作">
          {{ valueText(current.displayTitle) }} </Descriptions.Item
        ><Descriptions.Item label="方法">
          {{ valueText(current.actionName) }}
        </Descriptions.Item>
        <Descriptions.Item label="账号">
          {{ valueText(current.account) }} /
          {{ valueText(current.realName) }} </Descriptions.Item
        ><Descriptions.Item label="状态">
          <Tag :color="current.status === '200' ? 'green' : 'red'">
            {{ current.status === '200' ? '成功' : '失败' }}
          </Tag>
        </Descriptions.Item>
        <Descriptions.Item label="IP 地址">
          {{ valueText(current.remoteIp) }} </Descriptions.Item
        ><Descriptions.Item label="访问地点">
          {{ valueText(current.location) }}
        </Descriptions.Item>
        <Descriptions.Item label="浏览器">
          {{ valueText(current.browser) }} </Descriptions.Item
        ><Descriptions.Item label="操作系统">
          {{ valueText(current.os) }}
        </Descriptions.Item>
        <Descriptions.Item label="经纬度">
          {{ valueText(current.longitude) }} /
          {{ valueText(current.latitude) }} </Descriptions.Item
        ><Descriptions.Item label="耗时">
          {{ current.elapsed ?? 0 }} ms
        </Descriptions.Item>
        <Descriptions.Item :span="2" label="访问时间">
          {{ valueText(current.logDateTime) }}
        </Descriptions.Item>
      </Descriptions>
    </Modal>
  </div>
</template>

<style scoped>
.log-page {
  min-height: 100%;
  padding: 12px;
  background: #f4f6fa;
}

.page-panel {
  padding: 14px;
  overflow: hidden;
  background: #fff;
  border: 1px solid #e4e9f1;
  border-radius: 8px;
}

.panel-heading {
  display: flex;
  gap: 16px;
  align-items: flex-start;
  justify-content: space-between;
  margin-bottom: 12px;
}

.panel-heading h2 {
  margin: 0;
  font-size: 16px;
  line-height: 24px;
}

.panel-heading p {
  margin: 2px 0 0;
  font-size: 12px;
  color: #768196;
}

.query-bar {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: center;
  max-width: 1160px;
  margin-bottom: 12px;
}

.query-control {
  flex: 0 0 160px;
  width: 160px;
}

.tenant-control {
  flex-basis: 190px;
  width: 190px;
}

.date-control {
  flex-basis: 300px;
  width: 300px;
}

.elapsed-control {
  flex-basis: 145px;
  width: 145px;
}

.status-control {
  flex-basis: 140px;
  width: 140px;
}

.query-actions {
  display: inline-flex;
  flex: none;
  gap: 8px;
}

.slow {
  font-weight: 600;
  color: #d46b08;
}

@media (max-width: 768px) {
  .log-page {
    padding: 8px;
  }

  .panel-heading {
    flex-direction: column;
    align-items: stretch;
  }

  .query-control {
    flex-basis: 100%;
    width: 100%;
  }

  .query-actions {
    width: 100%;
  }

  .query-actions :deep(.ant-btn) {
    flex: 1;
  }
}
</style>
