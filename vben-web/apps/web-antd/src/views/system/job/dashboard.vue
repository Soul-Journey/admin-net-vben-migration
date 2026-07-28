<script setup lang="ts">
import type { TableColumnsType } from 'ant-design-vue';

import type { EchartsUIType } from '@vben/plugins/echarts';

import type {
  JobClusterRecord,
  JobDetailOutput,
  JobExecutionRecord,
  JobTriggerRecord,
} from '#/api';

import { computed, nextTick, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';

import { IconifyIcon } from '@vben/icons';
import { EchartsUI, useEcharts } from '@vben/plugins/echarts';

import { Button, Empty, Space, Table, Tag, Tooltip } from 'ant-design-vue';
import dayjs from 'dayjs';

import { listJobClustersApi, pageJobRecordsApi, pageJobsApi } from '#/api';

import {
  isAbnormalTriggerStatus,
  triggerMeta,
  triggerStatusHint,
} from './status';

defineOptions({ name: 'AdminNetJobDashboard' });

interface TriggerOverview extends JobTriggerRecord {
  jobDescription: string;
}

const router = useRouter();
const chartRef = ref<EchartsUIType>();
const { renderEcharts } = useEcharts(chartRef);
const loading = ref(false);
const jobs = ref<JobDetailOutput[]>([]);
const records = ref<JobExecutionRecord[]>([]);
const recordTotal = ref(0);
const clusters = ref<JobClusterRecord[]>([]);

const triggerRows = computed<TriggerOverview[]>(() =>
  jobs.value.flatMap((job) =>
    (job.jobTriggers ?? []).map((trigger) => ({
      ...trigger,
      jobDescription: job.jobDetail.description || job.jobDetail.jobId,
    })),
  ),
);
const problemTriggers = computed(() =>
  triggerRows.value.filter(
    (item) =>
      isAbnormalTriggerStatus(item.status) ||
      Number(item.numberOfErrors ?? 0) > 0,
  ),
);
const activeTriggers = computed(
  () =>
    triggerRows.value.filter((item) =>
      [0, 1, 2, 5].includes(Number(item.status)),
    ).length,
);
const pausedTriggers = computed(
  () => triggerRows.value.filter((item) => Number(item.status) === 3).length,
);
const failedRecords = computed(
  () =>
    records.value.filter((item) => isAbnormalTriggerStatus(item.status)).length,
);
const recordHealthRate = computed(() =>
  records.value.length === 0
    ? 100
    : Math.round(
        ((records.value.length - failedRecords.value) / records.value.length) *
          1000,
      ) / 10,
);
const onlineClusters = computed(
  () => clusters.value.filter((item) => Number(item.status) === 1).length,
);
const recentRecords = computed(() => records.value.slice(0, 12));

const triggerColumns: TableColumnsType<TriggerOverview> = [
  { key: 'job', title: '任务', width: 240 },
  { key: 'status', title: '状态', width: 120 },
  { dataIndex: 'numberOfErrors', key: 'errors', title: '累计错误', width: 90 },
  { dataIndex: 'numberOfRuns', key: 'runs', title: '运行次数', width: 100 },
  {
    dataIndex: 'lastRunTime',
    key: 'lastRunTime',
    title: '最近运行',
    width: 170,
  },
  {
    dataIndex: 'nextRunTime',
    key: 'nextRunTime',
    title: '下次运行',
    width: 170,
  },
];
const recordColumns: TableColumnsType<JobExecutionRecord> = [
  { dataIndex: 'jobId', key: 'jobId', title: '作业', width: 230 },
  { dataIndex: 'triggerId', key: 'triggerId', title: '触发器', width: 220 },
  { key: 'status', title: '状态', width: 120 },
  { key: 'elapsedTime', title: '耗时', width: 100 },
  {
    dataIndex: 'result',
    ellipsis: true,
    key: 'result',
    title: '执行结果',
    width: 220,
  },
  {
    dataIndex: 'createdTime',
    key: 'createdTime',
    title: '记录时间',
    width: 170,
  },
];

function triggerRowKey(record: TriggerOverview) {
  return `${record.jobId}:${record.triggerId}`;
}

function renderTrend() {
  const days = Array.from({ length: 7 }, (_item, index) =>
    dayjs().subtract(6 - index, 'day'),
  );
  const labels = days.map((date) => date.format('MM-DD'));
  const totals = days.map(
    (date) =>
      records.value.filter((item) =>
        dayjs(item.createdTime).isSame(date, 'day'),
      ).length,
  );
  const failures = days.map(
    (date) =>
      records.value.filter(
        (item) =>
          dayjs(item.createdTime).isSame(date, 'day') &&
          isAbnormalTriggerStatus(item.status),
      ).length,
  );
  renderEcharts({
    animationDuration: 500,
    grid: { bottom: 8, containLabel: true, left: 8, right: 16, top: 34 },
    legend: { data: ['执行次数', '异常次数'], right: 8, top: 0 },
    series: [
      {
        barMaxWidth: 28,
        data: totals,
        itemStyle: { color: '#4f6ef7' },
        name: '执行次数',
        type: 'bar',
      },
      {
        data: failures,
        itemStyle: { color: '#d4380d' },
        name: '异常次数',
        smooth: true,
        symbolSize: 7,
        type: 'line',
      },
    ],
    tooltip: { trigger: 'axis' },
    xAxis: { axisTick: { show: false }, data: labels, type: 'category' },
    yAxis: {
      minInterval: 1,
      splitLine: { lineStyle: { color: '#e9edf3' } },
      type: 'value',
    },
  });
}

async function loadDashboard() {
  loading.value = true;
  try {
    const [jobPage, recordPage, clusterList] = await Promise.all([
      pageJobsApi({ page: 1, pageSize: 500 }),
      pageJobRecordsApi({ page: 1, pageSize: 500 }),
      listJobClustersApi(),
    ]);
    jobs.value = jobPage.items ?? [];
    records.value = recordPage.items ?? [];
    recordTotal.value = recordPage.total ?? 0;
    clusters.value = clusterList ?? [];
    await nextTick();
    renderTrend();
  } finally {
    loading.value = false;
  }
}

onMounted(loadDashboard);
</script>

<template>
  <div class="dashboard-page">
    <section class="dashboard-panel">
      <header class="page-heading">
        <div>
          <h2>任务运行概览</h2>
          <p>基于真实调度记录汇总，统计范围为最近 500 条运行记录</p>
        </div>
        <Space>
          <Button :loading="loading" @click="loadDashboard">
            <template #icon><IconifyIcon icon="lucide:refresh-cw" /></template>
            刷新
          </Button>
          <Button type="primary" @click="router.push('/platform/job')">
            <template #icon><IconifyIcon icon="lucide:list-todo" /></template>
            任务管理
          </Button>
        </Space>
      </header>

      <div class="metrics-strip">
        <div>
          <span>作业</span><strong>{{ jobs.length }}</strong
          ><small>{{ triggerRows.length }} 个触发器</small>
        </div>
        <div>
          <span>活动触发器</span><strong>{{ activeTriggers }}</strong
          ><small>{{ pausedTriggers }} 个已暂停</small>
        </div>
        <div>
          <span>记录健康率</span
          ><strong :class="{ danger: recordHealthRate < 100 }"
            >{{ recordHealthRate }}%</strong
          ><small>最近 {{ records.length }} 条</small>
        </div>
        <div>
          <span>异常触发器</span
          ><strong :class="{ danger: problemTriggers.length > 0 }">{{
            problemTriggers.length
          }}</strong
          ><small>累计错误或异常状态</small>
        </div>
        <div>
          <span>调度节点</span
          ><strong>{{ onlineClusters }}/{{ clusters.length }}</strong
          ><small>在线 / 全部</small>
        </div>
        <div>
          <span>历史记录</span><strong>{{ recordTotal }}</strong
          ><small>数据库累计</small>
        </div>
      </div>

      <div class="content-grid">
        <section class="content-section trend-section">
          <div class="section-heading">
            <div>
              <h3>最近 7 天执行趋势</h3>
              <p>按运行记录时间统计，异常仅包含阻塞和崩溃</p>
            </div>
          </div>
          <EchartsUI ref="chartRef" class="trend-chart" />
        </section>

        <section class="content-section issue-section">
          <div class="section-heading">
            <div>
              <h3>需要关注</h3>
              <p>存在累计错误或异常调度状态的触发器</p>
            </div>
            <Tag v-if="problemTriggers.length > 0" color="error">
              {{ problemTriggers.length }} 项
            </Tag>
          </div>
          <div v-if="problemTriggers.length > 0" class="issue-list">
            <div
              v-for="item in problemTriggers.slice(0, 6)"
              :key="`${item.jobId}:${item.triggerId}`"
              class="issue-row"
            >
              <div>
                <strong>{{ item.jobDescription }}</strong
                ><span>{{ item.triggerId }}</span>
              </div>
              <div class="issue-status">
                <Tooltip :title="triggerStatusHint(item.status)">
                  <Tag :color="triggerMeta(item.status)[1]">
                    {{ triggerMeta(item.status)[0] }}
                  </Tag> </Tooltip
                ><span>{{ item.numberOfErrors ?? 0 }} 次错误</span>
              </div>
            </div>
          </div>
          <Empty
            v-else
            :image="Empty.PRESENTED_IMAGE_SIMPLE"
            description="当前没有异常触发器"
          />
        </section>
      </div>

      <section class="content-section table-section">
        <div class="section-heading">
          <div>
            <h3>触发器状态</h3>
            <p>查看每个任务的当前状态、累计运行次数和下一次执行时间</p>
          </div>
        </div>
        <Table
          :columns="triggerColumns"
          :data-source="triggerRows"
          :loading="loading"
          :pagination="false"
          :row-key="triggerRowKey"
          :scroll="{ x: 900 }"
          size="small"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'job'">
              <div class="name-cell">
                <strong>{{ record.jobDescription }}</strong
                ><span>{{ record.triggerId }}</span>
              </div>
            </template>
            <template v-else-if="column.key === 'status'">
              <Tooltip :title="triggerStatusHint(record.status)">
                <Tag :color="triggerMeta(record.status)[1]">
                  {{ triggerMeta(record.status)[0] }}
                </Tag>
              </Tooltip>
            </template>
            <template v-else-if="column.key === 'errors'">
              <span :class="{ danger: Number(record.numberOfErrors) > 0 }">{{
                record.numberOfErrors ?? 0
              }}</span>
            </template>
            <template v-else-if="column.key === 'lastRunTime'">
              {{ record.lastRunTime || '-' }}
            </template>
            <template v-else-if="column.key === 'nextRunTime'">
              {{ record.nextRunTime || '未计划' }}
            </template>
          </template>
        </Table>
      </section>

      <section class="content-section table-section">
        <div class="section-heading">
          <div>
            <h3>近期运行记录</h3>
            <p>按记录时间倒序展示最近 12 次执行</p>
          </div>
        </div>
        <Table
          :columns="recordColumns"
          :data-source="recentRecords"
          :loading="loading"
          :pagination="false"
          :scroll="{ x: 1060 }"
          row-key="id"
          size="small"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'status'">
              <Tooltip :title="triggerStatusHint(record.status)">
                <Tag :color="triggerMeta(record.status)[1]">
                  {{ triggerMeta(record.status)[0] }}
                </Tag>
              </Tooltip>
            </template>
            <template v-else-if="column.key === 'elapsedTime'">
              {{ record.elapsedTime ?? 0 }} ms
            </template>
            <template v-else-if="column.key === 'result'">
              {{ record.result || '-' }}
            </template>
          </template>
        </Table>
      </section>
    </section>
  </div>
</template>

<style scoped>
.dashboard-page {
  min-height: 100%;
  padding: 12px;
  background: #f4f6fa;
}

.dashboard-panel {
  padding: 14px;
  background: #fff;
  border: 1px solid #e4e9f1;
  border-radius: 8px;
}

.page-heading,
.section-heading {
  display: flex;
  gap: 16px;
  align-items: flex-start;
  justify-content: space-between;
}

.page-heading {
  margin-bottom: 12px;
}

.page-heading h2,
.section-heading h3 {
  margin: 0;
  color: #182033;
  letter-spacing: 0;
}

.page-heading h2 {
  font-size: 16px;
  line-height: 24px;
}

.section-heading h3 {
  font-size: 14px;
  line-height: 22px;
}

.page-heading p,
.section-heading p {
  margin: 2px 0 0;
  font-size: 12px;
  color: #768196;
}

.metrics-strip {
  display: grid;
  grid-template-columns: repeat(6, minmax(120px, 1fr));
  margin-bottom: 12px;
  background: #fafbfc;
  border: 1px solid #e4e9f1;
  border-radius: 6px;
}

.metrics-strip > div {
  display: grid;
  grid-template-columns: 1fr auto;
  gap: 2px 10px;
  padding: 10px 14px;
  border-right: 1px solid #e4e9f1;
}

.metrics-strip > div:last-child {
  border-right: 0;
}

.metrics-strip span,
.metrics-strip small {
  font-size: 12px;
  color: #768196;
}

.metrics-strip small {
  grid-column: 1 / -1;
}

.metrics-strip strong {
  font-size: 18px;
  line-height: 22px;
  color: #182033;
}

.content-grid {
  display: grid;
  grid-template-columns: minmax(0, 1.7fr) minmax(300px, 0.8fr);
  gap: 12px;
  margin-bottom: 12px;
}

.content-section {
  min-width: 0;
  padding: 12px;
  border: 1px solid #e4e9f1;
  border-radius: 6px;
}

.trend-chart {
  height: 260px;
  margin-top: 8px;
}

.issue-list {
  margin-top: 9px;
  border-top: 1px solid #edf0f5;
}

.issue-row {
  display: flex;
  gap: 12px;
  align-items: center;
  justify-content: space-between;
  padding: 10px 2px;
  border-bottom: 1px solid #edf0f5;
}

.issue-row > div:first-child,
.name-cell {
  display: grid;
  min-width: 0;
}

.issue-row strong,
.name-cell strong {
  overflow: hidden;
  text-overflow: ellipsis;
  font-size: 13px;
  color: #293246;
  white-space: nowrap;
}

.issue-row span,
.name-cell span {
  overflow: hidden;
  text-overflow: ellipsis;
  font-size: 11px;
  color: #768196;
  white-space: nowrap;
}

.issue-status {
  display: flex;
  flex-shrink: 0;
  gap: 5px;
  align-items: center;
}

.table-section + .table-section {
  margin-top: 12px;
}

.table-section .section-heading {
  margin-bottom: 10px;
}

.danger {
  font-weight: 600;
  color: #cf1322 !important;
}

@media (max-width: 1100px) {
  .metrics-strip {
    grid-template-columns: repeat(3, 1fr);
  }

  .metrics-strip > div:nth-child(3) {
    border-right: 0;
  }

  .metrics-strip > div:nth-child(-n + 3) {
    border-bottom: 1px solid #e4e9f1;
  }

  .content-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 640px) {
  .dashboard-page {
    padding: 8px;
  }

  .page-heading {
    flex-direction: column;
    align-items: stretch;
  }

  .metrics-strip {
    grid-template-columns: 1fr 1fr;
  }

  .metrics-strip > div:nth-child(3) {
    border-right: 1px solid #e4e9f1;
  }

  .metrics-strip > div:nth-child(2n) {
    border-right: 0;
  }

  .metrics-strip > div:nth-child(-n + 4) {
    border-bottom: 1px solid #e4e9f1;
  }
}
</style>
