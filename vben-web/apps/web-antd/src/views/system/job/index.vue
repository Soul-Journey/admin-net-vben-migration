<script setup lang="ts">
import type { TableColumnsType } from 'ant-design-vue';

import type {
  JobClusterRecord,
  JobDetailOutput,
  JobDetailRecord,
  JobExecutionRecord,
  JobTriggerRecord,
} from '#/api';

import { computed, onMounted, reactive, ref } from 'vue';
import { useRouter } from 'vue-router';

import { useAccess } from '@vben/access';
import { IconifyIcon } from '@vben/icons';
import { useUserStore } from '@vben/stores';

import {
  Button,
  DatePicker,
  Dropdown,
  Form,
  Input,
  InputNumber,
  Menu,
  message,
  Modal,
  Radio,
  Select,
  Space,
  Switch,
  Table,
  Tag,
  Tooltip,
} from 'ant-design-vue';

import {
  addJobApi,
  addJobTriggerApi,
  cancelJobApi,
  deleteJobApi,
  deleteJobTriggerApi,
  listJobClustersApi,
  listJobGroupsApi,
  pageJobRecordsApi,
  pageJobsApi,
  pauseAllJobsApi,
  pauseJobApi,
  pauseJobTriggerApi,
  persistAllJobsApi,
  runJobApi,
  startAllJobsApi,
  startJobApi,
  startJobTriggerApi,
  updateJobApi,
  updateJobTriggerApi,
  wakeSchedulerApi,
} from '#/api';
import { ADMIN_PAGINATION_PROPS } from '#/utils/pagination';

import { triggerMeta, triggerStatusHint } from './status';

defineOptions({ name: 'AdminNetJob' });

const router = useRouter();

const SUPER_ADMIN_ACCOUNT = 999;
const SYS_ADMIN_ACCOUNT = 888;
const PERIOD_TRIGGER = 'Furion.Schedule.PeriodTrigger';
const CRON_TRIGGER = 'Furion.Schedule.CronTrigger';
const HTTP_METHODS = ['GET', 'POST', 'PUT', 'DELETE'];
const { hasAccessByCodes } = useAccess();
const userStore = useUserStore();
const loading = ref(false);
const saving = ref(false);
const recordsLoading = ref(false);
const jobs = ref<JobDetailOutput[]>([]);
const groups = ref<string[]>([]);
const total = ref(0);
const query = reactive({
  description: '',
  groupName: undefined as string | undefined,
  jobId: '',
  page: 1,
  pageSize: 50,
});
const jobOpen = ref(false);
const triggerOpen = ref(false);
const recordsOpen = ref(false);
const clustersOpen = ref(false);
const currentJobId = ref('');
const executionRecords = ref<JobExecutionRecord[]>([]);
const recordPage = reactive({ page: 1, pageSize: 20, total: 0 });
const clusters = ref<JobClusterRecord[]>([]);
const httpForm = reactive({ body: '', method: 'GET', requestUri: '' });
const jobForm = reactive<JobDetailRecord>({
  concurrent: true,
  createType: 2,
  groupName: 'default',
  includeAnnotation: false,
  jobId: '',
  properties: '{}',
  scriptCode: '',
});
const triggerForm = reactive<JobTriggerRecord>({
  args: '60000',
  assemblyName: 'Furion.Pure',
  jobId: '',
  maxNumberOfErrors: 0,
  maxNumberOfRuns: 0,
  numRetries: 0,
  resetOnlyOnce: true,
  retryTimeout: 1000,
  runOnStart: false,
  startNow: true,
  triggerId: '',
  triggerType: PERIOD_TRIGGER,
});

const isSystemAdmin = computed(() => {
  const accountType = Number((userStore.userInfo as any)?.accountType);
  return (
    accountType === SUPER_ADMIN_ACCOUNT || accountType === SYS_ADMIN_ACCOUNT
  );
});
const isJobEdit = computed(() => Boolean(jobForm.id));
const isTriggerEdit = computed(() => Boolean(triggerForm.id));
const stats = computed(() => {
  const triggerList = jobs.value.flatMap((item) => item.jobTriggers ?? []);
  return {
    errors: triggerList.reduce(
      (sum, item) => sum + Number(item.numberOfErrors ?? 0),
      0,
    ),
    jobs: total.value,
    running: triggerList.filter(
      (item) => item.status === 1 || item.status === 2,
    ).length,
    triggers: triggerList.length,
  };
});
const columns: TableColumnsType<JobDetailOutput> = [
  { key: 'job', title: '作业', width: 260 },
  { key: 'type', title: '类型', width: 100 },
  { key: 'mode', title: '执行方式', width: 100 },
  { key: 'triggers', title: '触发器', width: 90 },
  { key: 'next', title: '下次运行', width: 180 },
  { key: 'errors', title: '错误', width: 80 },
  { key: 'updated', title: '更新时间', width: 170 },
  { fixed: 'right', key: 'actions', title: '操作', width: 180 },
];
const triggerColumns: TableColumnsType<JobTriggerRecord> = [
  { key: 'trigger', title: '触发器', width: 230 },
  { key: 'schedule', title: '调度规则', width: 180 },
  { key: 'status', title: '状态', width: 100 },
  { dataIndex: 'numberOfRuns', key: 'runs', title: '运行次数', width: 95 },
  { dataIndex: 'lastRunTime', key: 'last', title: '最近运行', width: 170 },
  { dataIndex: 'nextRunTime', key: 'next', title: '下次运行', width: 170 },
  { fixed: 'right', key: 'actions', title: '操作', width: 190 },
];
const recordColumns: TableColumnsType<JobExecutionRecord> = [
  { dataIndex: 'triggerId', key: 'triggerId', title: '触发器', width: 180 },
  { dataIndex: 'numberOfRuns', key: 'runs', title: '次数', width: 80 },
  { key: 'status', title: '状态', width: 90 },
  { key: 'elapsed', title: '耗时', width: 100 },
  { dataIndex: 'result', key: 'result', title: '执行结果', ellipsis: true },
  { dataIndex: 'createdTime', key: 'time', title: '记录时间', width: 170 },
];
const clusterColumns: TableColumnsType<JobClusterRecord> = [
  { dataIndex: 'clusterId', key: 'clusterId', title: '节点 ID' },
  { dataIndex: 'description', key: 'description', title: '描述' },
  { key: 'status', title: '状态', width: 100 },
  {
    dataIndex: 'updatedTime',
    key: 'updatedTime',
    title: '更新时间',
    width: 180,
  },
];

function can(code: string) {
  return hasAccessByCodes([code]);
}
function detailOf(value: unknown) {
  return (value as JobDetailOutput).jobDetail;
}
function triggerOf(value: unknown) {
  return value as JobTriggerRecord;
}
function recordOf(value: unknown) {
  return value as JobExecutionRecord;
}
function clusterOf(value: unknown) {
  return value as JobClusterRecord;
}
function jobRowKey(record: JobDetailOutput) {
  return record.jobDetail.jobId;
}
function typeMeta(type: number) {
  return (
    (
      {
        0: ['内置', 'default'],
        1: ['脚本', 'orange'],
        2: ['HTTP', 'blue'],
      } as Record<number, string[]>
    )[type] ?? ['未知', 'default']
  );
}
function parseHttpProperties(properties?: string) {
  try {
    const wrapper = JSON.parse(properties || '{}');
    const value = JSON.parse(wrapper.HttpJob || '{}');
    httpForm.requestUri = value.RequestUri || '';
    httpForm.method = value.HttpMethod?.Method || 'GET';
    httpForm.body = value.Body || '';
  } catch {
    Object.assign(httpForm, { body: '', method: 'GET', requestUri: '' });
  }
}
function encodeHttpProperties() {
  return JSON.stringify({
    HttpJob: JSON.stringify({
      Body: httpForm.body,
      ClientName: 'HttpJob',
      EnsureSuccessStatusCode: true,
      HttpMethod: { Method: httpForm.method },
      RequestUri: httpForm.requestUri,
    }),
  });
}

async function loadJobs() {
  loading.value = true;
  try {
    const data = await pageJobsApi({
      description: query.description || undefined,
      groupName: query.groupName,
      jobId: query.jobId || undefined,
      page: query.page,
      pageSize: query.pageSize,
    });
    jobs.value = data.items ?? [];
    total.value = data.total ?? 0;
  } finally {
    loading.value = false;
  }
}
async function handleQuery() {
  query.page = 1;
  await loadJobs();
}
async function resetQuery() {
  Object.assign(query, {
    description: '',
    groupName: undefined,
    jobId: '',
    page: 1,
  });
  await loadJobs();
}

function openJob(record?: JobDetailOutput) {
  Object.assign(jobForm, {
    concurrent: true,
    createType: 2,
    description: '',
    groupName: 'default',
    id: undefined,
    includeAnnotation: false,
    jobId: '',
    properties: '{}',
    scriptCode: '',
  });
  Object.assign(httpForm, { body: '', method: 'GET', requestUri: '' });
  if (record) {
    Object.assign(jobForm, structuredClone(record.jobDetail));
    if (jobForm.createType === 2) parseHttpProperties(jobForm.properties);
  }
  jobOpen.value = true;
}
async function saveJob() {
  if (!jobForm.jobId.trim() || !jobForm.groupName?.trim())
    return void message.warning('请填写作业编号和组名称');
  if (jobForm.createType === 1 && !jobForm.scriptCode?.trim())
    return void message.warning('脚本代码不能为空');
  if (jobForm.createType === 2 && !httpForm.requestUri.trim())
    return void message.warning('请求地址不能为空');
  saving.value = true;
  try {
    if (jobForm.createType === 2) jobForm.properties = encodeHttpProperties();
    await (isJobEdit.value
      ? updateJobApi({ ...jobForm })
      : addJobApi({ ...jobForm }));
    message.success(isJobEdit.value ? '作业已更新' : '作业已创建');
    jobOpen.value = false;
    await loadJobs();
  } finally {
    saving.value = false;
  }
}

function openTrigger(jobId: string, trigger?: JobTriggerRecord) {
  Object.assign(triggerForm, {
    args: '60000',
    assemblyName: 'Furion.Pure',
    description: '',
    endTime: undefined,
    id: undefined,
    jobId,
    maxNumberOfErrors: 0,
    maxNumberOfRuns: 0,
    numRetries: 0,
    resetOnlyOnce: true,
    retryTimeout: 1000,
    runOnStart: false,
    startNow: true,
    startTime: undefined,
    triggerId: '',
    triggerType: PERIOD_TRIGGER,
  });
  if (trigger) Object.assign(triggerForm, structuredClone(trigger));
  triggerOpen.value = true;
}
async function saveTrigger() {
  if (!triggerForm.triggerId.trim() || !triggerForm.args?.trim())
    return void message.warning('请填写触发器编号和调度规则');
  saving.value = true;
  try {
    await (isTriggerEdit.value
      ? updateJobTriggerApi({ ...triggerForm })
      : addJobTriggerApi({ ...triggerForm }));
    message.success(isTriggerEdit.value ? '触发器已更新' : '触发器已创建');
    triggerOpen.value = false;
    await loadJobs();
  } finally {
    saving.value = false;
  }
}

function confirmAction(
  title: string,
  content: string,
  action: () => Promise<unknown>,
  danger = false,
) {
  Modal.confirm({
    content,
    okButtonProps: { danger },
    okText: '确认',
    title,
    async onOk() {
      await action();
      message.success(`${title}操作已提交`);
      await loadJobs();
    },
  });
}
function handleJobAction(key: string, record: JobDetailOutput) {
  const id = record.jobDetail.jobId;
  if (key === 'edit') return openJob(record);
  if (key === 'records') return openRecords(id);
  const actions: Record<
    string,
    [string, string, () => Promise<unknown>, boolean?]
  > = {
    cancel: [
      '取消作业',
      `取消“${id}”当前正在执行的实例，不会删除作业配置。`,
      () => cancelJobApi(id),
      true,
    ],
    delete: [
      '删除作业',
      `将删除“${id}”及其全部触发器配置，运行历史保留。此操作不可撤销。`,
      () => deleteJobApi(id),
      true,
    ],
    pause: [
      '暂停作业',
      `暂停“${id}”后，触发器将不再调度新实例。`,
      () => pauseJobApi(id),
    ],
    run: [
      '立即执行',
      `立即运行“${id}”。该任务可能调用外部接口或修改业务数据。`,
      () => runJobApi(id),
      true,
    ],
    start: [
      '启动作业',
      `恢复“${id}”及其可用触发器的调度。`,
      () => startJobApi(id),
    ],
  };
  const item = actions[key];
  if (item) confirmAction(...item);
}
function removeTrigger(trigger: JobTriggerRecord) {
  confirmAction(
    '删除触发器',
    `将永久删除触发器“${trigger.triggerId}”，作业本身保留。`,
    () => deleteJobTriggerApi(trigger.jobId, trigger.triggerId),
    true,
  );
}
async function openRecords(jobId: string) {
  currentJobId.value = jobId;
  recordPage.page = 1;
  recordsOpen.value = true;
  await loadRecords();
}
async function loadRecords() {
  recordsLoading.value = true;
  try {
    const data = await pageJobRecordsApi({
      jobId: currentJobId.value,
      page: recordPage.page,
      pageSize: recordPage.pageSize,
    });
    executionRecords.value = data.items ?? [];
    recordPage.total = data.total ?? 0;
  } finally {
    recordsLoading.value = false;
  }
}
async function openClusters() {
  clustersOpen.value = true;
  clusters.value = await listJobClustersApi();
}

onMounted(async () => {
  await Promise.all([
    loadJobs(),
    listJobGroupsApi().then((data) => {
      groups.value = data ?? [];
    }),
  ]);
});
</script>

<template>
  <div class="job-page">
    <section class="page-panel">
      <div class="panel-heading">
        <div>
          <h2>任务调度</h2>
          <p>
            统一维护作业、触发器、执行记录和调度集群；全局写操作仅限系统管理员
          </p>
        </div>
        <Space>
          <Button @click="router.push('/platform/job/dashboard')">
            <template #icon>
              <IconifyIcon icon="lucide:chart-no-axes-combined" /> </template
            >任务看板 </Button
          ><Button @click="openClusters">
            <template #icon><IconifyIcon icon="lucide:server" /></template
            >集群 </Button
          ><Dropdown v-if="isSystemAdmin">
            <Button>
              <template #icon>
                <IconifyIcon icon="lucide:settings-2" /> </template
              >调度器 </Button
            ><template #overlay>
              <Menu
                @click="
                  ({ key }) => {
                    if (key === 'start')
                      confirmAction(
                        '启动全部作业',
                        '恢复全部作业调度。',
                        startAllJobsApi,
                      );
                    if (key === 'pause')
                      confirmAction(
                        '暂停全部作业',
                        '暂停后所有作业都不会产生新的调度实例。',
                        pauseAllJobsApi,
                        true,
                      );
                    if (key === 'wake')
                      confirmAction(
                        '唤醒调度器',
                        '强制唤醒调度器并重新检查待执行任务。',
                        wakeSchedulerApi,
                      );
                    if (key === 'persist')
                      confirmAction(
                        '持久化调度器',
                        '强制将当前全部作业和触发器状态写入数据库。',
                        persistAllJobsApi,
                      );
                  }
                "
              >
                <Menu.Item key="start">启动全部</Menu.Item
                ><Menu.Item key="pause">暂停全部</Menu.Item
                ><Menu.Item key="wake">强制唤醒</Menu.Item
                ><Menu.Item key="persist">持久化全部</Menu.Item>
              </Menu>
            </template> </Dropdown
          ><Button
            v-if="can('sysJob:addJobDetail') && isSystemAdmin"
            type="primary"
            @click="openJob()"
          >
            <template #icon><IconifyIcon icon="lucide:plus" /></template
            >新增作业
          </Button>
        </Space>
      </div>
      <div class="summary-strip">
        <div>
          <span>作业总数</span><strong>{{ stats.jobs }}</strong>
        </div>
        <div>
          <span>当前页触发器</span><strong>{{ stats.triggers }}</strong>
        </div>
        <div>
          <span>活动触发器</span><strong>{{ stats.running }}</strong>
        </div>
        <div>
          <span>累计错误</span
          ><strong :class="{ danger: stats.errors > 0 }">{{
            stats.errors
          }}</strong>
        </div>
      </div>
      <div class="query-bar">
        <Input
          v-model:value="query.jobId"
          allow-clear
          placeholder="作业编号"
          @press-enter="handleQuery"
        /><Select
          v-model:value="query.groupName"
          allow-clear
          :options="groups.map((value) => ({ label: value, value }))"
          placeholder="全部分组"
        /><Input
          v-model:value="query.description"
          allow-clear
          placeholder="描述信息"
          @press-enter="handleQuery"
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
        :data-source="jobs"
        :loading="loading"
        :pagination="{
          ...ADMIN_PAGINATION_PROPS,
          current: query.page,
          pageSize: query.pageSize,
          showTotal: (value: number) => `共 ${value} 项`,
          total,
        }"
        :row-key="jobRowKey"
        :scroll="{ x: 1180 }"
        size="small"
        @change="
          (pagination) => {
            query.page = pagination.current ?? 1;
            query.pageSize = pagination.pageSize ?? 50;
            loadJobs();
          }
        "
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'job'">
            <div class="job-name">
              <strong>{{ detailOf(record).jobId }}</strong
              ><span>{{ detailOf(record).description || '暂无描述' }}</span
              ><code>{{ detailOf(record).groupName || 'default' }}</code>
            </div> </template
          ><template v-else-if="column.key === 'type'">
            <Tag :color="typeMeta(detailOf(record).createType)[1]">
              {{ typeMeta(detailOf(record).createType)[0] }}
            </Tag> </template
          ><template v-else-if="column.key === 'mode'">
            <Tag>
              {{ detailOf(record).concurrent ? '并行' : '串行' }}
            </Tag> </template
          ><template v-else-if="column.key === 'triggers'">
            {{ record.jobTriggers?.length ?? 0 }} </template
          ><template v-else-if="column.key === 'next'">
            {{
              record.jobTriggers
                ?.map((item: JobTriggerRecord) => item.nextRunTime)
                .filter(Boolean)
                .sort()[0] || '未计划'
            }} </template
          ><template v-else-if="column.key === 'errors'">
            <span
              :class="{
                danger: record.jobTriggers?.some(
                  (item: JobTriggerRecord) => Number(item.numberOfErrors) > 0,
                ),
              }"
              >{{
                record.jobTriggers?.reduce(
                  (sum: number, item: JobTriggerRecord) =>
                    sum + Number(item.numberOfErrors ?? 0),
                  0,
                )
              }}</span
            > </template
          ><template v-else-if="column.key === 'updated'">
            {{ detailOf(record).updatedTime || '-' }} </template
          ><template v-else-if="column.key === 'actions'">
            <Space :size="2">
              <Tooltip title="新增触发器">
                <Button
                  v-if="isSystemAdmin"
                  size="small"
                  type="text"
                  @click="openTrigger(detailOf(record).jobId)"
                >
                  <template #icon>
                    <IconifyIcon icon="lucide:alarm-clock-plus" />
                  </template>
                </Button> </Tooltip
              ><Button
                size="small"
                type="link"
                @click="openRecords(detailOf(record).jobId)"
              >
                记录 </Button
              ><Dropdown v-if="isSystemAdmin">
                <Button size="small" type="text">
                  <template #icon>
                    <IconifyIcon icon="lucide:ellipsis" />
                  </template> </Button
                ><template #overlay>
                  <Menu
                    @click="
                      ({ key }) =>
                        handleJobAction(String(key), record as JobDetailOutput)
                    "
                  >
                    <Menu.Item key="run">立即执行</Menu.Item
                    ><Menu.Item key="start">启动</Menu.Item
                    ><Menu.Item key="pause">暂停</Menu.Item
                    ><Menu.Item key="cancel">取消当前执行</Menu.Item
                    ><Menu.Divider /><Menu.Item key="edit">编辑</Menu.Item
                    ><Menu.Item key="delete" danger>删除</Menu.Item>
                  </Menu>
                </template>
              </Dropdown>
            </Space>
          </template>
        </template>
        <template #expandedRowRender="{ record }">
          <div class="trigger-area">
            <div class="trigger-heading">
              <strong>触发器</strong><span>调度规则决定作业何时运行</span>
            </div>
            <Table
              :columns="triggerColumns"
              :data-source="record.jobTriggers ?? []"
              :pagination="false"
              :scroll="{ x: 1100 }"
              row-key="triggerId"
              size="small"
            >
              <template #bodyCell="{ column, record: trigger }">
                <template v-if="column.key === 'trigger'">
                  <div class="trigger-name">
                    <strong>{{ triggerOf(trigger).triggerId }}</strong
                    ><span>{{
                      triggerOf(trigger).description || '暂无描述'
                    }}</span>
                  </div> </template
                ><template v-else-if="column.key === 'schedule'">
                  <Tag>
                    {{
                      triggerOf(trigger).triggerType === PERIOD_TRIGGER
                        ? '间隔'
                        : 'Cron'
                    }} </Tag
                  >{{ triggerOf(trigger).args || '-' }} </template
                ><template v-else-if="column.key === 'status'">
                  <Tooltip
                    :title="triggerStatusHint(triggerOf(trigger).status)"
                  >
                    <Tag :color="triggerMeta(triggerOf(trigger).status)[1]">
                      {{ triggerMeta(triggerOf(trigger).status)[0] }}
                    </Tag>
                  </Tooltip> </template
                ><template v-else-if="column.key === 'actions'">
                  <Space v-if="isSystemAdmin" :size="2">
                    <Button
                      size="small"
                      type="link"
                      @click="
                        confirmAction(
                          '启动触发器',
                          `恢复“${triggerOf(trigger).triggerId}”的调度。`,
                          () =>
                            startJobTriggerApi(
                              triggerOf(trigger).jobId,
                              triggerOf(trigger).triggerId,
                            ),
                        )
                      "
                    >
                      启动 </Button
                    ><Button
                      size="small"
                      type="link"
                      @click="
                        confirmAction(
                          '暂停触发器',
                          `暂停“${triggerOf(trigger).triggerId}”的后续调度。`,
                          () =>
                            pauseJobTriggerApi(
                              triggerOf(trigger).jobId,
                              triggerOf(trigger).triggerId,
                            ),
                        )
                      "
                    >
                      暂停 </Button
                    ><Button
                      size="small"
                      type="text"
                      @click="
                        openTrigger(
                          triggerOf(trigger).jobId,
                          triggerOf(trigger),
                        )
                      "
                    >
                      <template #icon>
                        <IconifyIcon icon="lucide:pencil" />
                      </template> </Button
                    ><Button
                      danger
                      size="small"
                      type="text"
                      @click="removeTrigger(triggerOf(trigger))"
                    >
                      <template #icon>
                        <IconifyIcon icon="lucide:trash-2" />
                      </template>
                    </Button> </Space
                  ><span v-else>-</span>
                </template>
              </template>
            </Table>
          </div>
        </template>
      </Table>
    </section>

    <Modal
      v-model:open="jobOpen"
      :confirm-loading="saving"
      :title="isJobEdit ? '编辑作业' : '新增作业'"
      width="min(760px, 94vw)"
      @ok="saveJob"
    >
      <Form layout="vertical">
        <div class="form-grid">
          <Form.Item label="作业编号" required>
            <Input
              v-model:value="jobForm.jobId"
              :disabled="isJobEdit"
            /> </Form.Item
          ><Form.Item label="组名称" required>
            <Input v-model:value="jobForm.groupName" /> </Form.Item
          ><Form.Item label="创建类型">
            <Radio.Group
              v-model:value="jobForm.createType"
              :disabled="isJobEdit"
            >
              <Radio.Button :value="1">脚本</Radio.Button
              ><Radio.Button :value="2">HTTP 请求</Radio.Button>
            </Radio.Group> </Form.Item
          ><Form.Item label="允许并行">
            <Switch v-model:checked="jobForm.concurrent" /> </Form.Item
          ><Form.Item class="full" label="描述信息">
            <Input.TextArea
              v-model:value="jobForm.description"
              :auto-size="{ minRows: 2, maxRows: 3 }"
            /> </Form.Item
          ><template v-if="jobForm.createType === 2">
            <Form.Item class="full" label="请求地址" required>
              <Input
                v-model:value="httpForm.requestUri"
                placeholder="https://example.com/api/task"
              /> </Form.Item
            ><Form.Item label="请求方法">
              <Select
                v-model:value="httpForm.method"
                :options="
                  HTTP_METHODS.map((value) => ({ label: value, value }))
                "
              /> </Form.Item
            ><Form.Item class="full" label="请求体">
              <Input.TextArea
                v-model:value="httpForm.body"
                :auto-size="{ minRows: 3, maxRows: 6 }"
                placeholder="JSON 或文本请求体"
              />
            </Form.Item> </template
          ><template v-else-if="jobForm.createType === 1">
            <Form.Item class="full" label="脚本代码" required>
              <Input.TextArea
                v-model:value="jobForm.scriptCode"
                class="code-editor"
                :auto-size="{
                  minRows: 10,
                  maxRows: 18,
                }"
              />
            </Form.Item> </template
          ><template v-else>
            <Form.Item class="full" label="额外数据">
              <Input.TextArea
                v-model:value="jobForm.properties"
                :auto-size="{ minRows: 3, maxRows: 6 }"
              />
            </Form.Item>
          </template>
        </div>
      </Form>
    </Modal>

    <Modal
      v-model:open="triggerOpen"
      :confirm-loading="saving"
      :title="isTriggerEdit ? '编辑触发器' : '新增触发器'"
      width="min(720px, 94vw)"
      @ok="saveTrigger"
    >
      <Form layout="vertical">
        <div class="form-grid">
          <Form.Item label="触发器编号" required>
            <Input
              v-model:value="triggerForm.triggerId"
              :disabled="isTriggerEdit"
            /> </Form.Item
          ><Form.Item label="触发器类型">
            <Select
              v-model:value="triggerForm.triggerType"
              :options="[
                { label: '固定间隔', value: PERIOD_TRIGGER },
                { label: 'Cron 表达式', value: CRON_TRIGGER },
              ]"
            /> </Form.Item
          ><Form.Item
            class="full"
            :label="
              triggerForm.triggerType === PERIOD_TRIGGER
                ? '间隔时间（毫秒）'
                : 'Cron 表达式'
            "
            required
          >
            <Input
              v-model:value="triggerForm.args"
              :placeholder="
                triggerForm.triggerType === PERIOD_TRIGGER
                  ? '例如 60000'
                  : '例如 @daily 或标准 Cron 表达式'
              "
            /> </Form.Item
          ><Form.Item label="起始时间">
            <DatePicker
              v-model:value="triggerForm.startTime"
              show-time
              value-format="YYYY-MM-DD HH:mm:ss"
            /> </Form.Item
          ><Form.Item label="结束时间">
            <DatePicker
              v-model:value="triggerForm.endTime"
              show-time
              value-format="YYYY-MM-DD HH:mm:ss"
            /> </Form.Item
          ><Form.Item label="最大触发次数">
            <InputNumber
              v-model:value="triggerForm.maxNumberOfRuns"
              :min="0"
            /> </Form.Item
          ><Form.Item label="最大错误次数">
            <InputNumber
              v-model:value="triggerForm.maxNumberOfErrors"
              :min="0"
            /> </Form.Item
          ><Form.Item label="重试次数">
            <InputNumber
              v-model:value="triggerForm.numRetries"
              :min="0"
            /> </Form.Item
          ><Form.Item label="重试间隔（毫秒）">
            <InputNumber
              v-model:value="triggerForm.retryTimeout"
              :min="0"
            /> </Form.Item
          ><Form.Item label="保存后立即启动">
            <Switch v-model:checked="triggerForm.startNow" /> </Form.Item
          ><Form.Item label="服务启动时执行一次">
            <Switch v-model:checked="triggerForm.runOnStart" /> </Form.Item
          ><Form.Item class="full" label="描述">
            <Input.TextArea
              v-model:value="triggerForm.description"
              :auto-size="{ minRows: 2, maxRows: 3 }"
            />
          </Form.Item>
        </div>
      </Form>
    </Modal>

    <Modal
      v-model:open="recordsOpen"
      :footer="null"
      :title="`运行记录：${currentJobId}`"
      width="min(980px, 95vw)"
    >
      <Table
        :columns="recordColumns"
        :data-source="executionRecords"
        :loading="recordsLoading"
        :pagination="{
          ...ADMIN_PAGINATION_PROPS,
          current: recordPage.page,
          pageSize: recordPage.pageSize,
          total: recordPage.total,
        }"
        :scroll="{ x: 900 }"
        row-key="id"
        size="small"
        @change="
          (pagination) => {
            recordPage.page = pagination.current ?? 1;
            recordPage.pageSize = pagination.pageSize ?? 20;
            loadRecords();
          }
        "
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <Tooltip :title="triggerStatusHint(recordOf(record).status)">
              <Tag :color="triggerMeta(recordOf(record).status)[1]">
                {{ triggerMeta(recordOf(record).status)[0] }}
              </Tag>
            </Tooltip> </template
          ><template v-else-if="column.key === 'elapsed'">
            {{ recordOf(record).elapsedTime ?? 0 }} ms
          </template>
        </template>
      </Table>
    </Modal>
    <Modal
      v-model:open="clustersOpen"
      :footer="null"
      title="调度集群"
      width="min(760px, 94vw)"
    >
      <Table
        :columns="clusterColumns"
        :data-source="clusters"
        :pagination="false"
        row-key="id"
        size="small"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <Tag :color="clusterOf(record).status === 1 ? 'green' : 'default'">
              {{
                clusterOf(record).status === 1
                  ? '在线'
                  : `状态 ${clusterOf(record).status ?? '-'}`
              }}
            </Tag>
          </template>
        </template>
      </Table>
    </Modal>
  </div>
</template>

<style scoped>
.job-page {
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

.summary-strip {
  display: grid;
  grid-template-columns: repeat(4, minmax(120px, 1fr));
  margin-bottom: 12px;
  background: #fafbfc;
  border: 1px solid #e4e9f1;
  border-radius: 6px;
}

.summary-strip div {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 9px 14px;
  border-right: 1px solid #e4e9f1;
}

.summary-strip div:last-child {
  border: 0;
}

.summary-strip span {
  font-size: 12px;
  color: #768196;
}

.summary-strip strong {
  font-size: 18px;
}

.query-bar {
  display: grid;
  grid-template-columns: 220px 180px minmax(260px, 360px) auto auto;
  gap: 8px;
  max-width: 980px;
  margin-bottom: 12px;
}

.query-bar > * {
  min-width: 0;
}

.query-bar :deep(.ant-input),
.query-bar :deep(.ant-select) {
  width: 100%;
}

.job-name,
.trigger-name {
  display: grid;
  min-width: 0;
}

.job-name span,
.trigger-name span {
  overflow: hidden;
  text-overflow: ellipsis;
  font-size: 12px;
  color: #768196;
  white-space: nowrap;
}

.job-name code {
  width: fit-content;
  margin-top: 2px;
  font-size: 11px;
  color: #51647e;
}

.danger {
  font-weight: 600;
  color: #cf1322;
}

.trigger-area {
  padding: 4px 10px 10px 44px;
  background: #f8fafc;
}

.trigger-heading {
  display: flex;
  gap: 10px;
  align-items: baseline;
  padding: 8px 0;
}

.trigger-heading span {
  font-size: 12px;
  color: #768196;
}

.form-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0 16px;
}

.form-grid .full {
  grid-column: 1 / -1;
}

.form-grid :deep(.ant-input-number),
.form-grid :deep(.ant-picker),
.form-grid :deep(.ant-select) {
  width: 100%;
}

.code-editor {
  font-family: Consolas, monospace;
  font-size: 12px;
}

.job-page :deep(.ant-table-expanded-row > td) {
  padding: 0 !important;
}

@media (max-width: 768px) {
  .job-page {
    padding: 8px;
  }

  .panel-heading {
    flex-direction: column;
    align-items: stretch;
  }

  .summary-strip {
    grid-template-columns: 1fr 1fr;
  }

  .summary-strip div:nth-child(2) {
    border-right: 0;
  }

  .summary-strip div:nth-child(-n + 2) {
    border-bottom: 1px solid #e4e9f1;
  }

  .query-bar {
    grid-template-columns: 1fr;
  }

  .query-bar > * {
    width: 100% !important;
  }

  .form-grid {
    grid-template-columns: 1fr;
  }

  .form-grid .full {
    grid-column: auto;
  }

  .trigger-area {
    padding-left: 8px;
  }
}
</style>
