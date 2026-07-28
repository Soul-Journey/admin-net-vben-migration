<script setup lang="ts">
import type { TableColumnsType } from 'ant-design-vue';
import type { Dayjs } from 'dayjs';

import type { DiffLogRecord, SysTenantOption } from '#/api';

import { computed, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';
import { IconifyIcon } from '@vben/icons';
import { useUserStore } from '@vben/stores';

import {
  Button,
  DatePicker,
  Descriptions,
  message,
  Modal,
  Select,
  Table,
  Tabs,
  Tag,
  Tooltip,
} from 'ant-design-vue';

import {
  clearDiffLogsApi,
  getDiffLogDetailApi,
  getTenantListApi,
  pageDiffLogsApi,
} from '#/api';
import { ADMIN_PAGINATION_PROPS } from '#/utils/pagination';

defineOptions({ name: 'AdminNetDiffLog' });

interface DiffColumn {
  afterValue?: unknown;
  beforeValue?: unknown;
  columnDescription?: string;
  columnName?: string;
}
interface DiffTable {
  columns?: DiffColumn[];
  tableDescription?: string;
  tableName?: string;
}
interface SqlParameter {
  parameterName?: string;
  typeName?: string;
  value?: unknown;
}

const SUPER_ADMIN_ACCOUNT = 999;
const { hasAccessByCodes } = useAccess();
const userStore = useUserStore();
const loading = ref(false);
const detailLoading = ref(false);
const detailOpen = ref(false);
const detail = ref<DiffLogRecord>();
const records = ref<DiffLogRecord[]>([]);
const tenants = ref<SysTenantOption[]>([]);
const total = ref(0);
const dateRange = ref<[Dayjs, Dayjs]>();
const query = reactive({
  page: 1,
  pageSize: 50,
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
const diffTables = computed(() =>
  parseJson<DiffTable[]>(detail.value?.diffData, []),
);
const parameters = computed(() =>
  parseJson<SqlParameter[]>(detail.value?.parameters, []),
);
const columns: TableColumnsType<DiffLogRecord> = [
  { key: 'index', title: '序号', width: 58 },
  { key: 'diffType', title: '变更类型', width: 110 },
  { key: 'target', title: '影响对象', ellipsis: true, width: 210 },
  { key: 'summary', title: '变更摘要', ellipsis: true, width: 330 },
  { key: 'elapsed', title: '耗时', width: 90 },
  { dataIndex: 'createTime', key: 'createTime', title: '操作时间', width: 170 },
  { fixed: 'right', key: 'actions', title: '操作', width: 76 },
];
const detailColumns: TableColumnsType<DiffColumn> = [
  { key: 'field', title: '字段', width: 220 },
  { key: 'before', title: '变更前' },
  { key: 'after', title: '变更后' },
];
const parameterColumns: TableColumnsType<SqlParameter> = [
  {
    dataIndex: 'parameterName',
    key: 'parameterName',
    title: '参数名',
    width: 200,
  },
  { dataIndex: 'typeName', key: 'typeName', title: '类型', width: 140 },
  { key: 'value', title: '值' },
];

function can(code: string) {
  return hasAccessByCodes([code]);
}
function asLog(value: unknown) {
  return value as DiffLogRecord;
}
function asColumn(value: unknown) {
  return value as DiffColumn;
}
function asParameter(value: unknown) {
  return value as SqlParameter;
}
function parseJson<T>(value: string | undefined, fallback: T): T {
  try {
    return value ? (JSON.parse(value) as T) : fallback;
  } catch {
    return fallback;
  }
}
function displayValue(value: unknown) {
  if (value === undefined || value === null || value === '') return '无';
  return typeof value === 'string' ? value : JSON.stringify(value, null, 2);
}
function typeMeta(type?: string) {
  const value = (type ?? '').toLowerCase();
  if (value.includes('insert')) return ['新增', 'green'];
  if (value.includes('delete')) return ['删除', 'red'];
  if (value.includes('update')) return ['修改', 'blue'];
  return [type || '未知', 'default'];
}
function recordTables(record: DiffLogRecord) {
  return parseJson<DiffTable[]>(record.diffData, []);
}
function targetText(record: DiffLogRecord) {
  const tables = recordTables(record);
  return (
    tables
      .map((item) => item.tableDescription || item.tableName)
      .filter(Boolean)
      .join('、') || displayValue(record.businessData)
  );
}
function summaryText(record: DiffLogRecord) {
  const tables = recordTables(record);
  const fieldCount = tables.reduce(
    (count, table) => count + (table.columns?.length ?? 0),
    0,
  );
  return tables.length > 0
    ? `${tables.length} 张表，${fieldCount} 个字段发生变化`
    : '查看详细差异';
}

async function loadRecords() {
  loading.value = true;
  try {
    const data = await pageDiffLogsApi({
      endTime: dateRange.value?.[1].format('YYYY-MM-DD HH:mm:ss'),
      page: query.page,
      pageSize: query.pageSize,
      startTime: dateRange.value?.[0].format('YYYY-MM-DD HH:mm:ss'),
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
  query.page = 1;
  dateRange.value = undefined;
  await loadRecords();
}
async function openDetail(record: DiffLogRecord) {
  detailOpen.value = true;
  detailLoading.value = true;
  try {
    detail.value = await getDiffLogDetailApi(record.id);
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
      ? `将永久删除租户“${tenantName ?? query.tenantId}”的全部差异日志。`
      : '将永久删除所有租户的全部差异日志。',
    okButtonProps: { danger: true },
    okText: '确认清空',
    title: '清空差异日志',
    async onOk() {
      const count = await clearDiffLogsApi(query.tenantId);
      message.success(`已清理 ${count} 条差异日志`);
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
          <h2>差异日志</h2>
          <p>追踪数据库字段的变更前后值；内容仅以纯文本展示，敏感值自动遮蔽</p>
        </div>
        <Button
          v-if="can('sysDifflog:clear') && isSuperAdmin"
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
          :options="tenantOptions"
          placeholder="选择租户"
          @change="handleQuery"
        /><DatePicker.RangePicker v-model:value="dateRange" show-time /><Button
          type="primary"
          @click="handleQuery"
        >
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
        :scroll="{ x: 1050 }"
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
            {{ (query.page - 1) * query.pageSize + index + 1 }} </template
          ><template v-else-if="column.key === 'diffType'">
            <Tag :color="typeMeta(asLog(record).diffType)[1]">
              {{ typeMeta(asLog(record).diffType)[0] }}
            </Tag> </template
          ><template v-else-if="column.key === 'target'">
            <Tooltip :title="targetText(asLog(record))">
              <span>{{ targetText(asLog(record)) }}</span>
            </Tooltip> </template
          ><template v-else-if="column.key === 'summary'">
            {{ summaryText(asLog(record)) }} </template
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
                  <IconifyIcon icon="lucide:file-diff" /> </template
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
      title="差异日志详情"
      width="min(1100px, 96vw)"
    >
      <div v-if="detailLoading" class="detail-loading">正在加载详情...</div>
      <template v-else-if="detail">
        <Descriptions :column="3" bordered size="small">
          <Descriptions.Item label="变更类型">
            <Tag :color="typeMeta(detail.diffType)[1]">
              {{ typeMeta(detail.diffType)[0] }}
            </Tag> </Descriptions.Item
          ><Descriptions.Item label="耗时">
            {{ detail.elapsed ?? 0 }} ms </Descriptions.Item
          ><Descriptions.Item label="操作时间">
            {{ detail.createTime || '无' }} </Descriptions.Item
          ><Descriptions.Item :span="3" label="业务对象">
            {{ displayValue(detail.businessData) }}
          </Descriptions.Item> </Descriptions
        ><Tabs class="detail-tabs">
          <Tabs.TabPane key="diff" tab="字段差异">
            <div v-if="diffTables.length > 0" class="diff-groups">
              <section
                v-for="(table, tableIndex) in diffTables"
                :key="`${table.tableName}-${tableIndex}`"
                class="diff-group"
              >
                <div class="table-title">
                  <strong>{{
                    table.tableDescription || table.tableName || '未命名数据表'
                  }}</strong
                  ><code v-if="table.tableDescription && table.tableName">{{
                    table.tableName
                  }}</code>
                </div>
                <Table
                  :columns="detailColumns"
                  :data-source="table.columns ?? []"
                  :pagination="false"
                  row-key="columnName"
                  size="small"
                >
                  <template #bodyCell="{ column, record }">
                    <template v-if="column.key === 'field'">
                      <strong>{{
                        asColumn(record).columnDescription ||
                        asColumn(record).columnName
                      }}</strong
                      ><small
                        v-if="
                          asColumn(record).columnDescription &&
                          asColumn(record).columnName
                        "
                        >{{ asColumn(record).columnName }}</small
                      > </template
                    ><template v-else-if="column.key === 'before'">
                      <pre class="value before">{{
                        displayValue(asColumn(record).beforeValue)
                      }}</pre></template
                    ><template v-else-if="column.key === 'after'">
                      <pre class="value after">{{
                        displayValue(asColumn(record).afterValue)
                      }}</pre>
                    </template>
                  </template>
                </Table>
              </section>
            </div>
            <div v-else class="empty-detail">
              没有可解析的字段差异
            </div> </Tabs.TabPane
          ><Tabs.TabPane key="sql" tab="SQL">
            <pre class="code-block">{{
              displayValue(detail.sql)
            }}</pre></Tabs.TabPane
          ><Tabs.TabPane key="parameters" tab="参数">
            <Table
              :columns="parameterColumns"
              :data-source="parameters"
              :pagination="false"
              size="small"
            >
              <template #bodyCell="{ column, record }">
                <template v-if="column.key === 'value'">
                  <pre class="value">{{
                    displayValue(asParameter(record).value)
                  }}</pre>
                </template>
              </template>
            </Table> </Tabs.TabPane
          ><Tabs.TabPane key="business" tab="业务数据">
            <pre class="code-block">{{
              displayValue(detail.businessData)
            }}</pre>
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
  margin-bottom: 12px;
}

.query-bar :deep(.ant-select) {
  width: 220px;
}

.query-bar :deep(.ant-picker-range) {
  width: 330px;
}

.slow {
  font-weight: 600;
  color: #d46b08;
}

.detail-loading,
.empty-detail {
  padding: 40px;
  color: #768196;
  text-align: center;
}

.diff-groups {
  display: grid;
  gap: 12px;
}

.diff-group {
  overflow: hidden;
  border: 1px solid #e4e9f1;
  border-radius: 6px;
}

.table-title {
  display: flex;
  gap: 10px;
  align-items: center;
  padding: 9px 12px;
  background: #f7f9fc;
}

.table-title code {
  color: #8c5c12;
}

.value,
.code-block {
  margin: 0;
  word-break: break-all;
  white-space: pre-wrap;
}

.value {
  max-height: 140px;
  overflow: auto;
  font-size: 12px;
}

.before {
  color: #a61d24;
}

.after {
  color: #237804;
}

.code-block {
  max-height: 46vh;
  padding: 12px;
  overflow: auto;
  background: #f7f9fc;
  border: 1px solid #e4e9f1;
  border-radius: 6px;
}

.detail-tabs small {
  display: block;
  color: #8792a5;
}

.detail-tabs {
  margin-top: 10px;
}

@media (max-width: 768px) {
  .log-page {
    padding: 8px;
  }

  .panel-heading {
    flex-direction: column;
    align-items: stretch;
  }

  .query-bar > * {
    width: 100% !important;
  }
}
</style>
