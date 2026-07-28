<script setup lang="ts">
import type { FormInstance, TreeProps } from 'ant-design-vue';
import type { Rule } from 'ant-design-vue/es/form';

import type {
  StressTestEndpointRecord,
  StressTestParams,
  StressTestResult,
} from '#/api';

import { computed, onMounted, reactive, ref, watch } from 'vue';

import { IconifyIcon } from '@vben/icons';

import {
  Alert,
  Button,
  Checkbox,
  Descriptions,
  Empty,
  Form,
  Input,
  InputNumber,
  message,
  Modal,
  Select,
  Space,
  Statistic,
  Tabs,
  Tag,
  Tooltip,
  Tree,
} from 'ant-design-vue';

import { executeStressTestApi, listStressTestEndpointsApi } from '#/api';

defineOptions({ name: 'AdminNetStressTest' });

type Pair = { key: string; value: string };
type ParameterType = 'headers' | 'path' | 'query' | 'request';
type StressFormState = {
  confirmation: string;
  confirmed: boolean;
  headers: Pair[];
  maxDegreeOfParallelism: number;
  numberOfRequests: number;
  numberOfRounds: number;
  path: Pair[];
  query: Pair[];
  request: Pair[];
};

const loading = ref(false);
const running = ref(false);
const accessDenied = ref(false);
const loadError = ref('');
const modalOpen = ref(false);
const guideOpen = ref(false);
const formRef = ref<FormInstance>();
const endpoints = ref<StressTestEndpointRecord[]>([]);
const selectedEndpoint = ref<StressTestEndpointRecord>();
const selectedKeys = ref<string[]>([]);
const expandedKeys = ref<string[]>([]);
const result = ref<StressTestResult>();
const keyword = ref('');
const selectedGroup = ref<string>();
const activeParameterTab = ref('query');
const form = reactive<StressFormState>({
  confirmation: '',
  confirmed: false,
  headers: [],
  maxDegreeOfParallelism: 5,
  numberOfRequests: 10,
  numberOfRounds: 1,
  path: [],
  query: [],
  request: [],
});

const rules: Record<string, Rule[]> = {
  maxDegreeOfParallelism: [
    { required: true, message: '请输入最大并发量', trigger: 'change' },
  ],
  numberOfRequests: [
    { required: true, message: '请输入每轮请求数', trigger: 'change' },
  ],
  numberOfRounds: [
    { required: true, message: '请输入测试轮数', trigger: 'change' },
  ],
};

const GROUP_LABELS: Record<string, string> = {
  approvalFlow: '审批流程',
  goView: '可视化大屏',
  sysConfig: '参数配置',
  sysDictData: '字典值',
  sysDictType: '字典类型',
  sysJob: '任务调度',
  sysLogDiff: '差异日志',
  sysLogEx: '异常日志',
  sysLogOp: '操作日志',
  sysLogVis: '访问日志',
  sysMenu: '菜单管理',
  sysNotice: '通知公告',
  sysOnlineUser: '在线用户',
  sysOrg: '机构管理',
  sysPos: '职位管理',
  sysRegion: '行政区划',
  sysRole: '角色管理',
  sysTenant: '租户管理',
  sysUser: '账号管理',
};

function groupLabel(value?: string) {
  if (!value) return '其他接口';
  return GROUP_LABELS[value] || value;
}

const groupOptions = computed(() =>
  [...new Set(endpoints.value.map((item) => item.groupName || 'other'))]
    .toSorted((left, right) => left.localeCompare(right, 'zh-CN'))
    .map((value) => ({ label: groupLabel(value), value })),
);
const filteredEndpoints = computed(() => {
  const normalizedKeyword = keyword.value.trim().toLowerCase();
  return endpoints.value.filter((item) => {
    const group = item.groupName || 'other';
    if (selectedGroup.value && selectedGroup.value !== group) return false;
    if (!normalizedKeyword) return true;
    return `${item.displayName} ${item.route} ${item.method}`
      .toLowerCase()
      .includes(normalizedKeyword);
  });
});
const endpointMap = computed(
  () =>
    new Map(
      endpoints.value.map((item) => [`${item.method}:${item.route}`, item]),
    ),
);
const treeData = computed<TreeProps['treeData']>(() => {
  const groups = new Map<string, StressTestEndpointRecord[]>();
  for (const item of filteredEndpoints.value) {
    const group = item.groupName || 'other';
    groups.set(group, [...(groups.get(group) ?? []), item]);
  }
  return [...groups.entries()].map(([group, items]) => ({
    children: items.map((item) => ({
      displayName: item.displayName,
      isLeaf: true,
      key: `${item.method}:${item.route}`,
      method: item.method,
      route: item.route,
      title: item.displayName,
    })),
    key: `group:${group}`,
    title: `${groupLabel(group)}（${items.length}）`,
  }));
});
const totalPlannedRequests = computed(
  () => form.numberOfRounds * form.numberOfRequests,
);
const parameterGroups = computed<Record<ParameterType, Pair[]>>(() => ({
  headers: form.headers,
  path: form.path,
  query: form.query,
  request: form.request,
}));
const metrics = computed(() => {
  const value = result.value;
  return [
    { label: '总请求', suffix: '次', value: value?.totalRequests ?? 0 },
    { label: '成功', suffix: '次', value: value?.successfulRequests ?? 0 },
    {
      danger: (value?.failedRequests ?? 0) > 0,
      label: '失败',
      suffix: '次',
      value: value?.failedRequests ?? 0,
    },
    {
      decimals: 2,
      label: 'QPS',
      suffix: '次/秒',
      value: value?.queriesPerSecond ?? 0,
    },
    {
      decimals: 2,
      label: '平均响应',
      suffix: 'ms',
      value: value?.averageResponseTime ?? 0,
    },
    {
      decimals: 2,
      label: 'P99 响应',
      suffix: 'ms',
      value: value?.percentile99ResponseTime ?? 0,
    },
  ];
});

function pairRecord(items: Pair[]) {
  return Object.fromEntries(
    items
      .filter((item) => item.key.trim())
      .map((item) => [item.key.trim(), item.value]),
  );
}

function addParameter(type: ParameterType) {
  parameterGroups.value[type].push({ key: '', value: '' });
}

function removeParameter(type: ParameterType, index: number) {
  parameterGroups.value[type].splice(index, 1);
}

function visibleGroupKeys() {
  return (treeData.value ?? []).map((item) => String(item.key));
}

function expandAllGroups() {
  expandedKeys.value = visibleGroupKeys();
}

function collapseAllGroups() {
  expandedKeys.value = [];
}

function selectEndpoint(keys: Array<number | string>) {
  const key = String(keys[0] ?? '');
  if (key.startsWith('group:')) {
    expandedKeys.value = expandedKeys.value.includes(key)
      ? expandedKeys.value.filter((item) => item !== key)
      : [...expandedKeys.value, key];
    return;
  }
  const endpoint = endpointMap.value.get(key);
  if (!endpoint) return;
  selectedKeys.value = [key];
  selectedEndpoint.value = endpoint;
  form.path = [...endpoint.route.matchAll(/\{([^}]+)\}/g)].map((match) => ({
    key: match[1] || '',
    value: '',
  }));
  result.value = undefined;
}

function openTestModal() {
  if (!selectedEndpoint.value) {
    message.warning('请先选择一个接口');
    return;
  }
  form.confirmation = '';
  form.confirmed = false;
  modalOpen.value = true;
}

async function loadEndpoints() {
  loading.value = true;
  accessDenied.value = false;
  loadError.value = '';
  try {
    endpoints.value = (await listStressTestEndpointsApi()) ?? [];
    expandedKeys.value = [];
  } catch (error: any) {
    const status = Number(error?.response?.status ?? error?.status ?? 0);
    accessDenied.value = status === 401 || status === 403;
    if (!accessDenied.value)
      loadError.value = '接口清单加载失败，请检查后端服务和浏览器控制台后重试';
  } finally {
    loading.value = false;
  }
}

async function submitTest() {
  if (
    !selectedEndpoint.value ||
    !form.confirmed ||
    form.confirmation !== '压测'
  )
    return;
  await formRef.value?.validate();
  if (totalPlannedRequests.value > 2000) {
    message.error('单次总请求数不能超过 2000');
    return;
  }

  const requestParameters = form.request
    .filter((item) => item.key.trim())
    .map((item) => ({ key: item.key.trim(), value: item.value }));
  const params: StressTestParams = {
    headers: pairRecord(form.headers),
    maxDegreeOfParallelism: form.maxDegreeOfParallelism,
    numberOfRequests: form.numberOfRequests,
    numberOfRounds: form.numberOfRounds,
    pathParameters: pairRecord(form.path),
    queryParameters: pairRecord(form.query),
    requestMethod: selectedEndpoint.value.method,
    requestParameters,
    requestUri: selectedEndpoint.value.route,
  };

  running.value = true;
  try {
    result.value = await executeStressTestApi(params);
    modalOpen.value = false;
    if (result.value.timedOut) {
      message.warning('压测达到 30 秒安全时限，已自动停止');
    } else {
      message.success('压测完成');
    }
  } finally {
    running.value = false;
  }
}

onMounted(loadEndpoints);

watch([keyword, selectedGroup], ([currentKeyword, currentGroup]) => {
  if (currentKeyword?.trim() || currentGroup) expandAllGroups();
});
</script>

<template>
  <div class="stress-page">
    <header class="page-header">
      <div>
        <h1>接口压测</h1>
        <p>对当前 Admin.NET 服务的只读接口执行受控并发请求</p>
      </div>
      <Space wrap>
        <Button @click="guideOpen = true">
          <template #icon><IconifyIcon icon="lucide:circle-help" /></template>
          使用说明
        </Button>
        <Button :loading="loading" @click="loadEndpoints">
          <template #icon><IconifyIcon icon="lucide:refresh-cw" /></template>
          刷新接口
        </Button>
        <Button
          danger
          type="primary"
          :disabled="!selectedEndpoint || accessDenied"
          @click="openTestModal"
        >
          <template #icon><IconifyIcon icon="lucide:gauge" /></template>
          配置压测
        </Button>
      </Space>
    </header>

    <Alert
      v-if="accessDenied"
      class="page-alert"
      message="仅超级管理员可以使用接口压测"
      description="压力流量由后端服务器发起，会占用连接、线程、CPU 和数据库资源。系统管理员及普通账号不能执行。"
      show-icon
      type="warning"
    />
    <Alert
      v-else-if="loadError"
      class="page-alert"
      :message="loadError"
      description="页面不会在接口清单缺失时允许手工输入目标地址，因此当前已禁止执行压测。"
      show-icon
      type="error"
    />
    <Alert
      v-else
      class="page-alert"
      message="安全限制已启用"
      description="只能选择后端核准的当前服务只读接口；单次最多 2000 个请求、并发最多 50、最长运行 30 秒。登录、写数据、支付、更新和密钥接口不会出现在列表中。"
      show-icon
      type="info"
    />

    <main class="stress-workbench">
      <aside class="endpoint-section">
        <div class="section-heading">
          <div>
            <h2>接口列表</h2>
            <p>{{ endpoints.length }} 个允许压测的只读接口</p>
          </div>
          <Space :size="4">
            <Tooltip title="全部展开">
              <Button size="small" type="text" @click="expandAllGroups">
                <template #icon>
                  <IconifyIcon icon="lucide:chevrons-up-down" />
                </template>
              </Button>
            </Tooltip>
            <Tooltip title="全部折叠">
              <Button size="small" type="text" @click="collapseAllGroups">
                <template #icon>
                  <IconifyIcon icon="lucide:chevrons-down-up" />
                </template>
              </Button>
            </Tooltip>
          </Space>
        </div>
        <div class="endpoint-filters">
          <Select
            v-model:value="selectedGroup"
            allow-clear
            :options="groupOptions"
            placeholder="全部分组"
          />
          <Input
            v-model:value="keyword"
            allow-clear
            placeholder="搜索名称或路径"
          >
            <template #prefix><IconifyIcon icon="lucide:search" /></template>
          </Input>
        </div>
        <Tree
          v-if="treeData && treeData.length > 0"
          v-model:expanded-keys="expandedKeys"
          :selected-keys="selectedKeys"
          :tree-data="treeData"
          block-node
          class="endpoint-tree"
          @select="selectEndpoint"
        >
          <template #title="node">
            <div v-if="node.isLeaf" class="endpoint-title">
              <Tag :color="node.method === 'GET' ? 'blue' : 'cyan'">
                {{ node.method }}
              </Tag>
              <span>{{ node.displayName }}</span>
              <code>{{ node.route }}</code>
            </div>
            <strong v-else>{{ node.title }}</strong>
          </template>
        </Tree>
        <Empty
          v-else
          description="没有匹配的安全接口"
          :image="Empty.PRESENTED_IMAGE_SIMPLE"
        />
      </aside>

      <section class="result-section">
        <div class="selected-endpoint">
          <div>
            <span class="selected-label">当前接口</span>
            <h2>{{ selectedEndpoint?.displayName || '尚未选择接口' }}</h2>
            <code>{{ selectedEndpoint?.route || '从左侧接口列表中选择' }}</code>
          </div>
          <Tag
            v-if="selectedEndpoint"
            :color="selectedEndpoint.method === 'GET' ? 'blue' : 'cyan'"
          >
            {{ selectedEndpoint.method }}
          </Tag>
        </div>

        <div class="metric-grid">
          <div v-for="item in metrics" :key="item.label" class="metric-item">
            <Statistic
              :precision="item.decimals"
              :suffix="item.suffix"
              :title="item.label"
              :value="item.value"
              :value-style="item.danger ? { color: '#cf1322' } : undefined"
            />
          </div>
        </div>

        <div class="latency-section">
          <div class="section-heading">
            <div>
              <h2>响应时间分布</h2>
              <p>百分位越高，越接近最慢的一批请求</p>
            </div>
            <Tag v-if="result?.timedOut" color="warning">达到 30 秒时限</Tag>
          </div>
          <Descriptions bordered :column="2" size="small">
            <Descriptions.Item label="总用时">
              {{ (result?.totalTimeInSeconds ?? 0).toFixed(2) }}
              秒
            </Descriptions.Item>
            <Descriptions.Item label="最小 / 最大">
              {{ (result?.minResponseTime ?? 0).toFixed(2) }} /
              {{ (result?.maxResponseTime ?? 0).toFixed(2) }} ms
            </Descriptions.Item>
            <Descriptions.Item label="P10 / P25">
              {{ (result?.percentile10ResponseTime ?? 0).toFixed(2) }} /
              {{ (result?.percentile25ResponseTime ?? 0).toFixed(2) }} ms
            </Descriptions.Item>
            <Descriptions.Item label="P50 / P75">
              {{ (result?.percentile50ResponseTime ?? 0).toFixed(2) }} /
              {{ (result?.percentile75ResponseTime ?? 0).toFixed(2) }} ms
            </Descriptions.Item>
            <Descriptions.Item label="P90 / P99">
              {{ (result?.percentile90ResponseTime ?? 0).toFixed(2) }} /
              {{ (result?.percentile99ResponseTime ?? 0).toFixed(2) }} ms
            </Descriptions.Item>
            <Descriptions.Item label="P99.9">
              {{ (result?.percentile999ResponseTime ?? 0).toFixed(2) }} ms
            </Descriptions.Item>
          </Descriptions>
          <Empty
            v-if="!result"
            class="result-empty"
            description="完成一次压测后显示结果"
            :image="Empty.PRESENTED_IMAGE_SIMPLE"
          />
        </div>
      </section>
    </main>

    <Modal
      v-model:open="modalOpen"
      :confirm-loading="running"
      :ok-button-props="{
        danger: true,
        disabled:
          !form.confirmed ||
          form.confirmation !== '压测' ||
          totalPlannedRequests > 2000,
      }"
      cancel-text="取消"
      ok-text="开始压测"
      title="配置接口压测"
      width="760px"
      @ok="submitTest"
    >
      <Alert
        :message="`${selectedEndpoint?.method || ''} ${selectedEndpoint?.route || ''}`"
        description="请求只会发送到当前 Admin.NET 后端；页面填写的主机地址不会被服务端采用。"
        show-icon
        type="warning"
      />
      <Form
        ref="formRef"
        :model="form"
        :rules="rules"
        class="stress-form"
        layout="vertical"
      >
        <div class="number-grid">
          <Form.Item label="测试轮数" name="numberOfRounds">
            <InputNumber
              v-model:value="form.numberOfRounds"
              :min="1"
              :max="10"
            />
          </Form.Item>
          <Form.Item label="每轮请求数" name="numberOfRequests">
            <InputNumber
              v-model:value="form.numberOfRequests"
              :min="1"
              :max="500"
            />
          </Form.Item>
          <Form.Item label="最大并发量" name="maxDegreeOfParallelism">
            <InputNumber
              v-model:value="form.maxDegreeOfParallelism"
              :min="1"
              :max="50"
            />
          </Form.Item>
          <div class="planned-total">
            <span>计划请求总数</span>
            <strong :class="{ danger: totalPlannedRequests > 2000 }">{{
              totalPlannedRequests
            }}</strong>
          </div>
        </div>

        <Tabs v-model:active-key="activeParameterTab" size="small">
          <Tabs.TabPane key="query" tab="Query 参数">
            <div class="parameter-heading">
              <span>附加到 URL 的查询参数</span>
              <Button size="small" @click="addParameter('query')">
                新增参数
              </Button>
            </div>
            <div
              v-for="(item, index) in form.query"
              :key="`query-${index}`"
              class="parameter-row"
            >
              <Input v-model:value="item.key" placeholder="参数名" />
              <Input v-model:value="item.value" placeholder="参数值" />
              <Tooltip title="删除参数">
                <Button
                  danger
                  type="text"
                  @click="removeParameter('query', index)"
                >
                  <template #icon>
                    <IconifyIcon icon="lucide:trash-2" />
                  </template>
                </Button>
              </Tooltip>
            </div>
            <Empty
              v-if="form.query.length === 0"
              description="暂无 Query 参数"
              :image="Empty.PRESENTED_IMAGE_SIMPLE"
            />
          </Tabs.TabPane>
          <Tabs.TabPane key="path" tab="Path 参数">
            <div class="parameter-heading">
              <span>替换接口路径中的 {参数名}</span>
            </div>
            <div
              v-for="(item, index) in form.path"
              :key="`path-${index}`"
              class="parameter-row fixed-key"
            >
              <Input :value="item.key" disabled />
              <Input v-model:value="item.value" placeholder="必填参数值" />
              <span></span>
            </div>
            <Empty
              v-if="form.path.length === 0"
              description="该接口没有 Path 参数"
              :image="Empty.PRESENTED_IMAGE_SIMPLE"
            />
          </Tabs.TabPane>
          <Tabs.TabPane key="request" tab="Body 参数">
            <div class="parameter-heading">
              <span>POST 请求体键值参数</span>
              <Button size="small" @click="addParameter('request')">
                新增参数
              </Button>
            </div>
            <div
              v-for="(item, index) in form.request"
              :key="`request-${index}`"
              class="parameter-row"
            >
              <Input v-model:value="item.key" placeholder="参数名" />
              <Input v-model:value="item.value" placeholder="参数值" />
              <Button
                danger
                type="text"
                @click="removeParameter('request', index)"
              >
                <template #icon><IconifyIcon icon="lucide:trash-2" /></template>
              </Button>
            </div>
            <Empty
              v-if="form.request.length === 0"
              description="暂无 Body 参数"
              :image="Empty.PRESENTED_IMAGE_SIMPLE"
            />
          </Tabs.TabPane>
          <Tabs.TabPane key="headers" tab="Headers">
            <div class="parameter-heading">
              <span>身份令牌由后端自动沿用，不需要在这里填写</span>
              <Button size="small" @click="addParameter('headers')">
                新增请求头
              </Button>
            </div>
            <div
              v-for="(item, index) in form.headers"
              :key="`header-${index}`"
              class="parameter-row"
            >
              <Input v-model:value="item.key" placeholder="请求头名称" />
              <Input v-model:value="item.value" placeholder="请求头值" />
              <Button
                danger
                type="text"
                @click="removeParameter('headers', index)"
              >
                <template #icon><IconifyIcon icon="lucide:trash-2" /></template>
              </Button>
            </div>
            <Empty
              v-if="form.headers.length === 0"
              description="暂无自定义请求头"
              :image="Empty.PRESENTED_IMAGE_SIMPLE"
            />
          </Tabs.TabPane>
        </Tabs>

        <div class="confirmation-block">
          <Checkbox v-model:checked="form.confirmed">
            我确认该操作会在短时间内占用服务器和数据库资源
          </Checkbox>
          <Input
            v-model:value="form.confirmation"
            placeholder="输入“压测”确认"
          />
        </div>
      </Form>
    </Modal>

    <Modal
      v-model:open="guideOpen"
      :footer="null"
      title="接口压测使用说明"
      width="680px"
    >
      <Descriptions bordered :column="1" size="small">
        <Descriptions.Item label="它能做什么">
          在短时间内重复调用一个只读接口，统计吞吐量、成功率和不同百分位的响应时间，用于发现接口在并发下是否变慢。
        </Descriptions.Item>
        <Descriptions.Item label="轮数与每轮请求数">
          两者相乘就是请求总数。例如 2 轮、每轮 100 次，共发出 200
          次请求；单次任务最多 2000 次。
        </Descriptions.Item>
        <Descriptions.Item label="最大并发量">
          同一时刻最多有多少个请求正在等待响应。数值越大，压力越高；建议从 5
          开始逐步增加，不要直接填 50。
        </Descriptions.Item>
        <Descriptions.Item label="QPS 与平均响应">
          QPS
          表示每秒完成多少请求；平均响应容易掩盖少量慢请求，判断稳定性时还要同时看
          P90、P99 和失败数。
        </Descriptions.Item>
        <Descriptions.Item label="P99">
          99% 的请求响应时间不超过这个值。P99
          明显高于平均值，通常说明少量请求存在锁等待、慢 SQL 或资源争用。
        </Descriptions.Item>
        <Descriptions.Item label="安全边界">
          只允许当前后端核准的只读接口，不接受任意网址；任务最长 30
          秒且同一后端实例只运行一个压测。正式压测仍建议在独立测试环境进行。
        </Descriptions.Item>
      </Descriptions>
    </Modal>
  </div>
</template>

<style scoped>
.stress-page {
  min-height: 100%;
  padding: 16px;
  background: hsl(var(--background));
}

.page-header,
.section-heading,
.selected-endpoint,
.endpoint-title,
.parameter-heading,
.confirmation-block {
  display: flex;
  align-items: center;
}

.page-header,
.section-heading,
.selected-endpoint,
.parameter-heading {
  gap: 16px;
  justify-content: space-between;
}

h1,
h2,
p {
  margin: 0;
}

h1 {
  font-size: 18px;
  font-weight: 650;
}

h2 {
  font-size: 15px;
  font-weight: 650;
}

.page-header p,
.section-heading p {
  margin-top: 4px;
  font-size: 12px;
  color: hsl(var(--muted-foreground));
}

.page-alert {
  margin-top: 14px;
}

.stress-workbench {
  display: grid;
  grid-template-columns: minmax(300px, 380px) minmax(0, 1fr);
  gap: 12px;
  margin-top: 12px;
}

.endpoint-section,
.result-section {
  min-width: 0;
  padding: 14px;
  background: hsl(var(--card));
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
}

.endpoint-filters {
  display: grid;
  grid-template-columns: 130px minmax(0, 1fr);
  gap: 8px;
  margin-top: 12px;
}

.endpoint-tree {
  max-height: 610px;
  padding: 4px;
  margin-top: 10px;
  overflow: auto;
  border: 1px solid hsl(var(--border));
  border-radius: 5px;
}

.endpoint-title {
  gap: 6px;
  min-width: 0;
}

.endpoint-title span {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.endpoint-title code {
  display: none;
}

.selected-endpoint {
  min-height: 66px;
  padding-bottom: 12px;
  border-bottom: 1px solid hsl(var(--border));
}

.selected-label {
  font-size: 12px;
  color: hsl(var(--muted-foreground));
}

.selected-endpoint h2 {
  margin-top: 3px;
}

.selected-endpoint code {
  display: block;
  max-width: 720px;
  margin-top: 4px;
  font-size: 12px;
  color: hsl(var(--muted-foreground));
  overflow-wrap: anywhere;
}

.metric-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  margin-top: 12px;
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
}

.metric-item {
  min-width: 0;
  padding: 14px;
  border-right: 1px solid hsl(var(--border));
  border-bottom: 1px solid hsl(var(--border));
}

.metric-item:nth-child(3n) {
  border-right: 0;
}

.metric-item:nth-last-child(-n + 3) {
  border-bottom: 0;
}

.latency-section {
  margin-top: 18px;
}

.latency-section :deep(.ant-descriptions) {
  margin-top: 10px;
}

.result-empty {
  margin-top: 32px;
}

.stress-form {
  margin-top: 14px;
}

.number-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 12px;
}

.number-grid :deep(.ant-input-number) {
  width: 100%;
}

.planned-total {
  display: flex;
  flex-direction: column;
  justify-content: center;
  min-height: 64px;
  padding: 0 12px;
  border: 1px solid hsl(var(--border));
  border-radius: 5px;
}

.planned-total span {
  font-size: 12px;
  color: hsl(var(--muted-foreground));
}

.planned-total strong {
  margin-top: 4px;
  font-size: 20px;
}

.planned-total strong.danger {
  color: #cf1322;
}

.parameter-heading {
  margin-bottom: 8px;
  font-size: 12px;
  color: hsl(var(--muted-foreground));
}

.parameter-row {
  display: grid;
  grid-template-columns: minmax(140px, 0.8fr) minmax(220px, 1.2fr) 32px;
  gap: 8px;
  margin-bottom: 8px;
}

.confirmation-block {
  gap: 16px;
  justify-content: space-between;
  padding-top: 14px;
  margin-top: 14px;
  border-top: 1px solid hsl(var(--border));
}

.confirmation-block .ant-input {
  width: 180px;
}

@media (max-width: 1000px) {
  .stress-workbench {
    grid-template-columns: 1fr;
  }

  .endpoint-tree {
    max-height: 360px;
  }
}

@media (max-width: 640px) {
  .stress-page {
    padding: 10px;
  }

  .page-header,
  .confirmation-block {
    flex-direction: column;
    align-items: stretch;
  }

  .endpoint-filters,
  .number-grid {
    grid-template-columns: 1fr;
  }

  .metric-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .metric-item,
  .metric-item:nth-child(3n),
  .metric-item:nth-last-child(-n + 3) {
    border-right: 1px solid hsl(var(--border));
    border-bottom: 1px solid hsl(var(--border));
  }

  .metric-item:nth-child(2n) {
    border-right: 0;
  }

  .metric-item:nth-last-child(-n + 2) {
    border-bottom: 0;
  }

  .parameter-row {
    grid-template-columns: minmax(100px, 0.8fr) minmax(120px, 1.2fr) 32px;
  }

  .confirmation-block .ant-input {
    width: 100%;
  }
}
</style>
