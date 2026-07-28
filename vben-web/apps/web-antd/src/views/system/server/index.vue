<script setup lang="ts">
import type { TableColumnsType } from 'ant-design-vue';

import type {
  ServerAssemblyInfo,
  ServerBaseInfo,
  ServerDiskInfo,
  ServerUsageInfo,
} from '#/api';

import {
  computed,
  onActivated,
  onBeforeUnmount,
  onDeactivated,
  onMounted,
  ref,
  watch,
} from 'vue';

import { IconifyIcon } from '@vben/icons';

import {
  Button,
  Progress,
  Space,
  Switch,
  Table,
  Tag,
  Tooltip,
} from 'ant-design-vue';
import dayjs from 'dayjs';

import {
  getServerAssembliesApi,
  getServerBaseApi,
  getServerDisksApi,
  getServerUsageApi,
} from '#/api';
import { ADMIN_PAGINATION_PROPS } from '#/utils/pagination';

defineOptions({ name: 'AdminNetServer' });

const loading = ref(false);
const usageLoading = ref(false);
const autoRefresh = ref(true);
const baseInfo = ref<ServerBaseInfo>({});
const usageInfo = ref<ServerUsageInfo>({});
const disks = ref<ServerDiskInfo[]>([]);
const assemblies = ref<ServerAssemblyInfo[]>([]);
const lastUpdated = ref('');
let refreshTimer: ReturnType<typeof setInterval> | undefined;

const cpuPercent = computed(() => parsePercent(usageInfo.value.cpuRate));
const ramPercent = computed(() => parsePercent(usageInfo.value.ramRate));
const diskUsed = computed(() =>
  disks.value.reduce((sum, item) => sum + Number(item.used || 0), 0),
);
const diskTotal = computed(() =>
  disks.value.reduce((sum, item) => sum + Number(item.totalSize || 0), 0),
);
const assemblyColumns: TableColumnsType<ServerAssemblyInfo> = [
  { dataIndex: 'name', key: 'name', title: '程序集', width: 320 },
  { dataIndex: 'version', key: 'version', title: '版本', width: 180 },
];

function parsePercent(value?: string) {
  const result = Number.parseFloat(String(value ?? '0').replace('%', ''));
  return Number.isFinite(result)
    ? Math.min(100, Math.max(0, Math.round(result * 10) / 10))
    : 0;
}
function usageStatus(percent: number) {
  if (percent >= 90) return 'exception';
  if (percent >= 75) return 'active';
  return 'normal';
}
function diskColor(percent: number) {
  if (percent >= 90) return '#cf1322';
  if (percent >= 75) return '#d48806';
  return '#4f6ef7';
}
async function loadUsage() {
  if (usageLoading.value) return;
  usageLoading.value = true;
  try {
    usageInfo.value = await getServerUsageApi();
    lastUpdated.value = dayjs().format('HH:mm:ss');
  } finally {
    usageLoading.value = false;
  }
}
async function loadAll() {
  loading.value = true;
  try {
    const [base, usage, diskList, assemblyList] = await Promise.all([
      getServerBaseApi(),
      getServerUsageApi(),
      getServerDisksApi(),
      getServerAssembliesApi(),
    ]);
    baseInfo.value = base;
    usageInfo.value = usage;
    disks.value = diskList ?? [];
    assemblies.value = assemblyList ?? [];
    lastUpdated.value = dayjs().format('HH:mm:ss');
  } finally {
    loading.value = false;
  }
}
function stopTimer() {
  if (refreshTimer) clearInterval(refreshTimer);
  refreshTimer = undefined;
}
function startTimer() {
  stopTimer();
  if (autoRefresh.value) refreshTimer = setInterval(loadUsage, 10_000);
}

watch(autoRefresh, startTimer);
onMounted(async () => {
  await loadAll();
  startTimer();
});
onActivated(startTimer);
onDeactivated(stopTimer);
onBeforeUnmount(stopTimer);
</script>

<template>
  <div class="server-page">
    <section class="server-panel">
      <header class="page-heading">
        <div>
          <h2>系统监控</h2>
          <p>查看当前 Admin.NET 服务进程、主机资源、磁盘容量和核心程序集</p>
        </div>
        <Space>
          <span class="refresh-state"
            ><i></i>更新于 {{ lastUpdated || '-' }}</span
          >
          <Tooltip
            title="开启后每 10 秒刷新 CPU 和内存，磁盘与系统信息仍由手动刷新更新"
          >
            <span class="auto-refresh"
              ><Switch
                v-model:checked="autoRefresh"
                size="small"
              />自动刷新</span
            >
          </Tooltip>
          <Button :loading="loading" @click="loadAll">
            <template #icon><IconifyIcon icon="lucide:refresh-cw" /></template>
            刷新全部
          </Button>
        </Space>
      </header>

      <div class="metrics-strip">
        <div>
          <span>CPU 使用率</span
          ><strong :class="{ danger: cpuPercent >= 90 }"
            >{{ cpuPercent }}%</strong
          ><small>{{ baseInfo.processorCount || '-' }}</small>
        </div>
        <div>
          <span>内存使用率</span
          ><strong :class="{ danger: ramPercent >= 90 }"
            >{{ ramPercent }}%</strong
          ><small
            >{{ usageInfo.usedRam || '-' }} /
            {{ usageInfo.totalRam || '-' }}</small
          >
        </div>
        <div>
          <span>服务运行</span
          ><strong class="text-metric">{{ usageInfo.runTime || '-' }}</strong
          ><small>启动于 {{ usageInfo.startTime || '-' }}</small>
        </div>
        <div>
          <span>磁盘使用</span
          ><strong class="text-metric">{{ diskUsed.toFixed(1) }} GB</strong
          ><small
            >共 {{ diskTotal.toFixed(1) }} GB · {{ disks.length }} 个磁盘</small
          >
        </div>
      </div>

      <div class="content-grid">
        <section class="content-section usage-section">
          <div class="section-heading">
            <div>
              <h3>资源使用</h3>
              <p>CPU 与内存来自当前主机实时采样</p>
            </div>
            <Tag color="processing">10 秒刷新</Tag>
          </div>
          <div class="usage-row">
            <div class="usage-label">
              <span>CPU</span><strong>{{ cpuPercent }}%</strong>
            </div>
            <Progress
              :percent="cpuPercent"
              :show-info="false"
              :status="usageStatus(cpuPercent)"
            />
          </div>
          <div class="usage-row">
            <div class="usage-label">
              <span>内存</span
              ><strong
                >{{ usageInfo.usedRam || '-' }} /
                {{ usageInfo.totalRam || '-' }}</strong
              >
            </div>
            <Progress
              :percent="ramPercent"
              :show-info="false"
              :status="usageStatus(ramPercent)"
            />
            <small>剩余 {{ usageInfo.freeRam || '-' }}</small>
          </div>
          <dl class="runtime-list">
            <div>
              <dt>服务启动时间</dt>
              <dd>{{ usageInfo.startTime || '-' }}</dd>
            </div>
            <div>
              <dt>服务运行时长</dt>
              <dd>{{ usageInfo.runTime || '-' }}</dd>
            </div>
            <div>
              <dt>主机运行时长</dt>
              <dd>{{ baseInfo.sysRunTime || '-' }}</dd>
            </div>
            <div>
              <dt>运行环境</dt>
              <dd>
                <Tag
                  :color="
                    baseInfo.environment === 'Production' ? 'green' : 'blue'
                  "
                >
                  {{ baseInfo.environment || '-' }}
                </Tag>
              </dd>
            </div>
          </dl>
        </section>

        <section class="content-section system-section">
          <div class="section-heading">
            <div>
              <h3>系统信息</h3>
              <p>仅超级管理员和系统管理员可读取的主机及运行环境信息</p>
            </div>
          </div>
          <dl class="info-list">
            <div>
              <dt>主机名称</dt>
              <dd>{{ baseInfo.hostName || '-' }}</dd>
            </div>
            <div>
              <dt>操作系统</dt>
              <dd>{{ baseInfo.systemOs || '-' }}</dd>
            </div>
            <div>
              <dt>系统架构</dt>
              <dd>{{ baseInfo.osArchitecture || '-' }}</dd>
            </div>
            <div>
              <dt>运行框架</dt>
              <dd>{{ baseInfo.frameworkDescription || '-' }}</dd>
            </div>
            <div>
              <dt>内网地址</dt>
              <dd>{{ baseInfo.localIp || '-' }}</dd>
            </div>
            <div>
              <dt>外网地址</dt>
              <dd>{{ baseInfo.remoteIp || '-' }}</dd>
            </div>
            <div class="full">
              <dt>网站目录</dt>
              <dd class="path-value">{{ baseInfo.wwwroot || '-' }}</dd>
            </div>
            <div>
              <dt>环境标识</dt>
              <dd>{{ baseInfo.stage || '-' }}</dd>
            </div>
          </dl>
        </section>
      </div>

      <section class="content-section disk-section">
        <div class="section-heading">
          <div>
            <h3>磁盘信息</h3>
            <p>容量单位为 GB，进度表示已使用空间</p>
          </div>
        </div>
        <div class="disk-grid">
          <div v-for="disk in disks" :key="disk.diskName" class="disk-row">
            <div class="disk-title">
              <div>
                <IconifyIcon icon="lucide:hard-drive" /><strong>{{
                  disk.diskName
                }}</strong
                ><Tag>{{ disk.typeName || '磁盘' }}</Tag>
              </div>
              <span>{{ disk.availablePercent }}%</span>
            </div>
            <Progress
              :percent="disk.availablePercent"
              :show-info="false"
              :stroke-color="diskColor(disk.availablePercent)"
            />
            <div class="disk-meta">
              <span>已用 {{ disk.used }} GB</span
              ><span>剩余 {{ disk.availableFreeSpace }} GB</span
              ><span>总计 {{ disk.totalSize }} GB</span>
            </div>
          </div>
        </div>
      </section>

      <section class="content-section assembly-section">
        <div class="section-heading">
          <div>
            <h3>核心程序集</h3>
            <p>用于排查部署环境中的框架和依赖版本</p>
          </div>
          <Tag>{{ assemblies.length }} 项</Tag>
        </div>
        <Table
          :columns="assemblyColumns"
          :data-source="assemblies"
          :loading="loading"
          :pagination="{ ...ADMIN_PAGINATION_PROPS, pageSize: 20 }"
          :scroll="{ x: 520 }"
          row-key="name"
          size="small"
        />
      </section>
    </section>
  </div>
</template>

<style scoped>
.server-page {
  min-height: 100%;
  padding: 12px;
  background: #f4f6fa;
}

.server-panel {
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

.refresh-state,
.auto-refresh {
  display: inline-flex;
  gap: 6px;
  align-items: center;
  font-size: 12px;
  color: #667085;
  white-space: nowrap;
}

.refresh-state i {
  width: 6px;
  height: 6px;
  background: #20a46b;
  border-radius: 50%;
  box-shadow: 0 0 0 3px #e8f7f0;
}

.metrics-strip {
  display: grid;
  grid-template-columns: repeat(4, minmax(150px, 1fr));
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

.metrics-strip .text-metric {
  font-size: 14px;
}

.content-grid {
  display: grid;
  grid-template-columns: minmax(0, 0.9fr) minmax(0, 1.1fr);
  gap: 12px;
  margin-bottom: 12px;
}

.content-section {
  min-width: 0;
  padding: 12px;
  border: 1px solid #e4e9f1;
  border-radius: 6px;
}

.usage-row {
  margin-top: 17px;
}

.usage-label,
.disk-title,
.disk-title > div,
.disk-meta {
  display: flex;
  align-items: center;
}

.usage-label,
.disk-title {
  gap: 12px;
  justify-content: space-between;
}

.usage-label span {
  font-size: 13px;
  font-weight: 600;
  color: #44506a;
}

.usage-label strong {
  font-size: 13px;
  color: #182033;
}

.usage-row small {
  font-size: 11px;
  color: #768196;
}

.runtime-list,
.info-list {
  margin: 14px 0 0;
}

.runtime-list > div,
.info-list > div {
  display: grid;
  grid-template-columns: 110px minmax(0, 1fr);
  align-items: center;
  min-height: 36px;
  border-bottom: 1px solid #edf0f5;
}

.runtime-list dt,
.info-list dt {
  font-size: 12px;
  color: #768196;
}

.runtime-list dd,
.info-list dd {
  min-width: 0;
  margin: 0;
  font-size: 12px;
  color: #293246;
  word-break: break-all;
}

.info-list {
  display: grid;
  grid-template-columns: 1fr 1fr;
  column-gap: 20px;
}

.info-list .full {
  grid-column: 1 / -1;
}

.path-value {
  font-family: Consolas, monospace;
}

.disk-section {
  margin-bottom: 12px;
}

.disk-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px 24px;
  margin-top: 12px;
}

.disk-row {
  min-width: 0;
  padding: 10px 12px;
  background: #fafbfc;
  border: 1px solid #edf0f5;
  border-radius: 6px;
}

.disk-title > div {
  gap: 7px;
  min-width: 0;
}

.disk-title strong {
  overflow: hidden;
  text-overflow: ellipsis;
  font-size: 13px;
  color: #293246;
  white-space: nowrap;
}

.disk-title > span {
  font-size: 12px;
  font-weight: 600;
  color: #44506a;
}

.disk-meta {
  gap: 8px;
  justify-content: space-between;
  font-size: 11px;
  color: #768196;
}

.assembly-section .section-heading {
  margin-bottom: 10px;
}

.danger {
  color: #cf1322 !important;
}

@media (max-width: 960px) {
  .content-grid {
    grid-template-columns: 1fr;
  }

  .disk-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 720px) {
  .server-page {
    padding: 8px;
  }

  .page-heading {
    flex-direction: column;
    align-items: stretch;
  }

  .metrics-strip {
    grid-template-columns: 1fr 1fr;
  }

  .metrics-strip > div:nth-child(2) {
    border-right: 0;
  }

  .metrics-strip > div:nth-child(-n + 2) {
    border-bottom: 1px solid #e4e9f1;
  }

  .info-list {
    grid-template-columns: 1fr;
  }

  .info-list .full {
    grid-column: auto;
  }
}
</style>
