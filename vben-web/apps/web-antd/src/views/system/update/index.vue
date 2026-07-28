<script setup lang="ts">
import type {
  SystemBackupRecord,
  SystemUpdateConfigurationStatus,
} from '#/api';

import { computed, nextTick, onMounted, onUnmounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';
import { IconifyIcon } from '@vben/icons';

import {
  Alert,
  Button,
  Descriptions,
  Empty,
  Input,
  List,
  message,
  Modal,
  Space,
  Tag,
  Tooltip,
} from 'ant-design-vue';

import {
  clearSystemUpdateLogsApi,
  executeSystemUpdateApi,
  getSystemUpdateConfigurationStatusApi,
  getSystemUpdateWebhookKeyApi,
  listSystemBackupsApi,
  listSystemUpdateLogsApi,
  restoreSystemBackupApi,
} from '#/api';

defineOptions({ name: 'AdminNetSystemUpdate' });

type DangerousAction = 'restore' | 'update';

const emptyStatus: SystemUpdateConfigurationStatus = {
  accessTokenConfigured: false,
  backendOutputConfigured: false,
  backendOutputExists: false,
  backupCount: 0,
  enabled: false,
  publishConfigured: false,
  readyForRestore: false,
  readyForUpdate: false,
  updateInterval: 0,
};

const { hasAccessByCodes } = useAccess();
const loading = ref(false);
const running = ref(false);
const logsLoading = ref(false);
const actionModalOpen = ref(false);
const secretModalOpen = ref(false);
const guideOpen = ref(false);
const dangerousAction = ref<DangerousAction>('update');
const confirmation = ref('');
const webhookKey = ref('');
const backups = ref<SystemBackupRecord[]>([]);
const selectedBackup = ref<SystemBackupRecord>();
const logs = ref<string[]>([]);
const terminalRef = ref<HTMLElement>();
const status = reactive<SystemUpdateConfigurationStatus>({ ...emptyStatus });
let pollingTimer: number | undefined;

const expectedConfirmation = computed(() =>
  dangerousAction.value === 'update'
    ? '更新'
    : selectedBackup.value?.fileName || '',
);
const actionTitle = computed(() =>
  dangerousAction.value === 'update' ? '确认执行系统更新' : '确认还原系统备份',
);
const actionDescription = computed(() =>
  dangerousAction.value === 'update'
    ? '系统将从远端仓库下载代码、在服务器编译、备份现有部署目录并覆盖程序文件。更新完成后通常需要重启后端。'
    : `系统会把备份 ${selectedBackup.value?.fileName || ''} 解压并覆盖当前部署目录。新增文件不会被自动删除，这不是数据库回滚。`,
);
const configurationItems = computed(() => [
  {
    label: '持续部署',
    ready: status.enabled,
    text: status.enabled ? '已启用' : '未启用',
  },
  {
    label: '仓库令牌',
    ready: status.accessTokenConfigured,
    text: status.accessTokenConfigured ? '已配置' : '未配置',
  },
  {
    label: '部署目录',
    ready: status.backendOutputExists,
    text: status.backendOutputExists ? '可访问' : '不可访问',
  },
  {
    label: '发布参数',
    ready: status.publishConfigured,
    text: status.publishConfigured ? '完整' : '不完整',
  },
]);

function can(code: string) {
  return hasAccessByCodes([code]);
}

function formatTime(value?: string) {
  return value
    ? new Date(value).toLocaleString('zh-CN', { hour12: false })
    : '未知时间';
}

async function scrollTerminalToBottom() {
  await nextTick();
  if (terminalRef.value)
    terminalRef.value.scrollTop = terminalRef.value.scrollHeight;
}

async function loadLogs(silent = false) {
  if (!can('sysUpdate:logs')) return;
  if (!silent) logsLoading.value = true;
  try {
    logs.value = (await listSystemUpdateLogsApi()) ?? [];
    await scrollTerminalToBottom();
  } finally {
    if (!silent) logsLoading.value = false;
  }
}

async function loadPage() {
  loading.value = true;
  try {
    const tasks: Promise<unknown>[] = [
      getSystemUpdateConfigurationStatusApi().then((value) =>
        Object.assign(status, value),
      ),
    ];
    if (can('sysUpdate:list')) {
      tasks.push(
        listSystemBackupsApi().then((value) => {
          backups.value = value ?? [];
          if (
            selectedBackup.value &&
            !backups.value.some(
              (item) => item.fileName === selectedBackup.value?.fileName,
            )
          ) {
            selectedBackup.value = undefined;
          }
        }),
      );
    }
    if (can('sysUpdate:logs')) tasks.push(loadLogs(true));
    await Promise.all(tasks);
  } finally {
    loading.value = false;
  }
}

function openDangerousAction(action: DangerousAction) {
  if (action === 'restore' && !selectedBackup.value) {
    message.warning('请先在左侧选择一个备份');
    return;
  }
  dangerousAction.value = action;
  confirmation.value = '';
  actionModalOpen.value = true;
}

function startPolling() {
  stopPolling();
  pollingTimer = window.setInterval(() => void loadLogs(true), 1500);
}

function stopPolling() {
  if (pollingTimer) window.clearInterval(pollingTimer);
  pollingTimer = undefined;
}

async function executeDangerousAction() {
  if (confirmation.value !== expectedConfirmation.value) return;
  running.value = true;
  actionModalOpen.value = false;
  startPolling();
  try {
    if (dangerousAction.value === 'update') {
      await executeSystemUpdateApi();
      message.success('更新流程执行完成，请检查日志并按部署方式重启后端');
    } else if (selectedBackup.value) {
      await restoreSystemBackupApi(selectedBackup.value.fileName);
      message.success('备份已覆盖到部署目录，请检查日志并重启后端');
    }
    await loadPage();
  } finally {
    stopPolling();
    running.value = false;
    await loadLogs(true);
  }
}

function confirmClearLogs() {
  Modal.confirm({
    cancelText: '取消',
    content:
      '只清除“系统更新”页面的缓存日志，不会删除备份、业务日志或数据库数据。',
    okText: '清空日志',
    onOk: async () => {
      await clearSystemUpdateLogsApi();
      logs.value = [];
      message.success('更新日志已清空');
    },
    title: '确认清空更新日志？',
  });
}

async function showWebhookKey() {
  webhookKey.value = await getSystemUpdateWebhookKeyApi();
  secretModalOpen.value = true;
}

async function copyWebhookKey() {
  await navigator.clipboard.writeText(webhookKey.value);
  message.success('WebHook 密钥已复制');
}

onMounted(loadPage);
onUnmounted(stopPolling);
</script>

<template>
  <div class="update-page">
    <header class="page-header">
      <div>
        <h1>系统更新</h1>
        <p>从指定代码仓库发布后端程序，并保留部署目录备份</p>
      </div>
      <Space wrap>
        <Button @click="guideOpen = true">
          <template #icon><IconifyIcon icon="lucide:circle-help" /></template>
          使用说明
        </Button>
        <Tooltip title="重新读取配置、备份和日志">
          <Button :loading="loading" @click="loadPage">
            <template #icon><IconifyIcon icon="lucide:refresh-cw" /></template>
            刷新
          </Button>
        </Tooltip>
      </Space>
    </header>

    <Alert
      v-if="!status.readyForUpdate"
      class="status-alert"
      message="当前环境未满足自动更新条件"
      show-icon
      type="warning"
    >
      <template #description>
        更新按钮已禁用。请由运维人员检查后端
        CDConfig：远端仓库令牌、部署目录和发布参数必须完整，且部署目录必须真实存在。
      </template>
    </Alert>
    <Alert
      v-else
      class="status-alert"
      message="自动更新已就绪"
      show-icon
      type="success"
      description="执行更新会修改服务器程序文件；建议先确认数据库备份、维护窗口和重启方案。"
    />

    <section class="configuration-band">
      <div
        v-for="item in configurationItems"
        :key="item.label"
        class="configuration-item"
      >
        <span>{{ item.label }}</span>
        <Tag :color="item.ready ? 'success' : 'warning'">{{ item.text }}</Tag>
      </div>
      <div class="configuration-context">
        <span>{{ status.repository || '仓库未配置' }}</span>
        <span>{{ status.branch || '分支未配置' }}</span>
        <span>{{ status.targetFramework || '框架未配置' }}</span>
        <span>{{ status.runtimeIdentifier || '运行平台未配置' }}</span>
      </div>
    </section>

    <main class="update-workbench">
      <aside class="backup-section">
        <div class="section-heading">
          <div>
            <h2>部署备份</h2>
            <p>更新前自动生成，只包含程序部署目录</p>
          </div>
          <Tag>{{ backups.length }} 份</Tag>
        </div>
        <List
          v-if="backups.length > 0 || loading"
          :data-source="backups"
          :loading="loading"
          class="backup-list"
        >
          <template #renderItem="{ item }">
            <List.Item
              class="backup-item"
              :class="{ selected: selectedBackup?.fileName === item.fileName }"
              @click="selectedBackup = item"
            >
              <div class="backup-name">
                <IconifyIcon icon="lucide:archive" />
                <span>{{ item.fileName }}</span>
              </div>
              <time>{{ formatTime(item.createTime) }}</time>
            </List.Item>
          </template>
        </List>
        <Empty
          v-else
          class="backup-empty"
          description="暂无部署备份"
          :image="Empty.PRESENTED_IMAGE_SIMPLE"
        />
      </aside>

      <section class="log-section">
        <div class="section-heading log-heading">
          <div>
            <h2>执行日志</h2>
            <p>更新和还原过程会记录在这里</p>
          </div>
          <Space wrap>
            <Button
              v-if="can('sysUpdate:webHookKey')"
              :disabled="!status.accessTokenConfigured"
              @click="showWebhookKey"
            >
              WebHook 密钥
            </Button>
            <Button
              v-if="can('sysUpdate:clear')"
              :disabled="running || logs.length === 0"
              @click="confirmClearLogs"
            >
              清空日志
            </Button>
            <Button
              v-if="can('sysUpdate:restore')"
              :disabled="!status.readyForRestore || !selectedBackup || running"
              @click="openDangerousAction('restore')"
            >
              还原备份
            </Button>
            <Button
              v-if="can('sysUpdate:update')"
              danger
              type="primary"
              :disabled="!status.readyForUpdate || running"
              :loading="running"
              @click="openDangerousAction('update')"
            >
              执行更新
            </Button>
          </Space>
        </div>
        <div
          ref="terminalRef"
          class="terminal"
          :class="{ loading: logsLoading }"
        >
          <pre v-if="logs.length > 0">{{ logs.join('\n') }}</pre>
          <div v-else class="terminal-empty">暂无执行日志</div>
        </div>
      </section>
    </main>

    <Modal
      v-model:open="actionModalOpen"
      :confirm-loading="running"
      :ok-button-props="{
        danger: true,
        disabled: confirmation !== expectedConfirmation,
      }"
      cancel-text="取消"
      ok-text="确认执行"
      :title="actionTitle"
      width="560px"
      @ok="executeDangerousAction"
    >
      <Alert :message="actionDescription" show-icon type="error" />
      <p class="confirmation-hint">
        请输入 <strong>{{ expectedConfirmation }}</strong> 确认：
      </p>
      <Input v-model:value="confirmation" :placeholder="expectedConfirmation" />
    </Modal>

    <Modal
      v-model:open="secretModalOpen"
      :footer="null"
      title="WebHook 密钥"
      width="520px"
    >
      <Alert
        message="此密钥允许通过仓库 WebHook 触发服务器更新，请只配置在受信任的仓库中，不要粘贴到聊天、工单或日志。"
        show-icon
        type="warning"
      />
      <div class="secret-row">
        <Input.Password :value="webhookKey" readonly />
        <Button type="primary" @click="copyWebhookKey">复制</Button>
      </div>
    </Modal>

    <Modal
      v-model:open="guideOpen"
      :footer="null"
      title="系统更新使用说明"
      width="680px"
    >
      <Descriptions bordered :column="1" size="small">
        <Descriptions.Item label="执行更新">
          拉取配置的仓库分支，在服务器执行 dotnet
          publish，备份现有部署目录，再覆盖程序文件。它不是只检查版本。
        </Descriptions.Item>
        <Descriptions.Item label="还原备份">
          将所选压缩包覆盖回部署目录，不还原数据库，也不会自动删除备份之后新增的文件。
        </Descriptions.Item>
        <Descriptions.Item label="清空日志">
          只清理本页面缓存的更新过程文本，不影响操作日志、异常日志、备份和业务数据。
        </Descriptions.Item>
        <Descriptions.Item label="WebHook 密钥">
          配置到 Gitee 仓库 WebHook
          后，受支持的推送或合并事件可以自动触发更新。密钥泄露等同于暴露部署触发凭据。
        </Descriptions.Item>
        <Descriptions.Item label="更新前准备">
          确认数据库已单独备份、部署目录权限正常、服务器具备对应 .NET
          SDK，并安排可重启后端的维护窗口。
        </Descriptions.Item>
      </Descriptions>
    </Modal>
  </div>
</template>

<style scoped>
.update-page {
  min-height: 100%;
  padding: 16px;
  color: hsl(var(--foreground));
  background: hsl(var(--background));
}

.page-header,
.section-heading,
.configuration-band,
.configuration-item,
.configuration-context,
.backup-name,
.secret-row {
  display: flex;
  align-items: center;
}

.page-header,
.section-heading {
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

.status-alert {
  margin-top: 14px;
}

.configuration-band {
  flex-wrap: wrap;
  gap: 10px 20px;
  padding: 10px 14px;
  margin-top: 12px;
  background: hsl(var(--card));
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
}

.configuration-item {
  gap: 8px;
  font-size: 13px;
}

.configuration-context {
  flex: 1;
  flex-wrap: wrap;
  gap: 6px 14px;
  justify-content: flex-end;
  min-width: 0;
  font-size: 12px;
  color: hsl(var(--muted-foreground));
}

.update-workbench {
  display: grid;
  grid-template-columns: minmax(240px, 300px) minmax(0, 1fr);
  gap: 12px;
  min-height: 520px;
  margin-top: 12px;
}

.backup-section,
.log-section {
  min-width: 0;
  padding: 14px;
  background: hsl(var(--card));
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
}

.backup-list {
  margin-top: 10px;
}

.backup-empty {
  margin-top: 72px;
}

.backup-item {
  display: block;
  padding: 10px !important;
  cursor: pointer;
  border-radius: 5px;
  transition: background-color 0.15s ease;
}

.backup-item:hover,
.backup-item.selected {
  background: hsl(var(--accent));
}

.backup-item.selected {
  box-shadow: inset 3px 0 hsl(var(--primary));
}

.backup-name {
  gap: 8px;
  min-width: 0;
  font-weight: 550;
}

.backup-name span {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.backup-item time {
  display: block;
  margin: 4px 0 0 24px;
  font-size: 12px;
  color: hsl(var(--muted-foreground));
}

.log-section {
  display: flex;
  flex-direction: column;
}

.log-heading {
  align-items: flex-start;
}

.terminal {
  flex: 1;
  min-height: 420px;
  padding: 14px;
  margin-top: 12px;
  overflow: auto;
  font-family: Consolas, 'Courier New', monospace;
  font-size: 12px;
  line-height: 1.65;
  color: #dfe4ea;
  background: #17191d;
  border: 1px solid #30343a;
  border-radius: 5px;
}

.terminal.loading {
  opacity: 0.72;
}

.terminal pre {
  margin: 0;
  overflow-wrap: anywhere;
  white-space: pre-wrap;
}

.terminal-empty {
  display: grid;
  place-items: center;
  height: 100%;
  color: #8d96a0;
}

.confirmation-hint {
  margin: 16px 0 8px;
}

.secret-row {
  gap: 8px;
  margin-top: 14px;
}

@media (max-width: 900px) {
  .update-workbench {
    grid-template-columns: 1fr;
  }

  .configuration-context {
    flex-basis: 100%;
    justify-content: flex-start;
  }
}

@media (max-width: 640px) {
  .update-page {
    padding: 10px;
  }

  .page-header,
  .log-heading {
    flex-direction: column;
    align-items: stretch;
  }
}
</style>
