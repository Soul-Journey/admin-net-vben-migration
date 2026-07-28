<script setup lang="ts">
import type { HubConnection } from '@microsoft/signalr';
import type { TableColumnsType } from 'ant-design-vue';

import type { OnlineUserRecord, TenantOption } from '#/api';

import { computed, onBeforeUnmount, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';
import { IconifyIcon } from '@vben/icons';
import { useUserStore } from '@vben/stores';

import {
  Badge,
  Button,
  Drawer,
  Input,
  message,
  Modal,
  Select,
  Space,
  Table,
  Tag,
  Tooltip,
} from 'ant-design-vue';

import {
  forceOfflineApi,
  listOnlineUserTenantsApi,
  pageOnlineUsersApi,
} from '#/api';
import { useAuthStore } from '#/store';
import { getStoredAccessToken } from '#/utils/adminnet/token';
import { ADMIN_PAGINATION_PROPS } from '#/utils/pagination';

const SUPER_ADMIN_ACCOUNT = 999;
const SYS_ADMIN_ACCOUNT = 888;

const { hasAccessByCodes } = useAccess();
const userStore = useUserStore();
const authStore = useAuthStore();
const open = ref(false);
const loading = ref(false);
const connectionState = ref<'connected' | 'connecting' | 'offline'>('offline');
const activeConnectionId = ref('');
const users = ref<OnlineUserRecord[]>([]);
const tenants = ref<TenantOption[]>([]);
const pager = reactive({ current: 1, pageSize: 20, total: 0 });
const query = reactive<{
  realName?: string;
  tenantId?: number;
  userName?: string;
}>({});
let connection: HubConnection | undefined;
let loggingOut = false;

const accountType = computed(() =>
  Number((userStore.userInfo as null | Record<string, unknown>)?.accountType),
);
const isSuperAdmin = computed(() => accountType.value === SUPER_ADMIN_ACCOUNT);
const isSystemAdmin = computed(
  () => isSuperAdmin.value || accountType.value === SYS_ADMIN_ACCOUNT,
);
const canForceOffline = computed(
  () => isSystemAdmin.value && hasAccessByCodes(['sysOnlineUser:forceOffline']),
);
const currentUserId = computed(() => String(userStore.userInfo?.userId ?? ''));
const columns = computed<TableColumnsType<OnlineUserRecord>>(() => {
  const result: TableColumnsType<OnlineUserRecord> = [
    { dataIndex: 'userName', key: 'userName', title: '账号', width: 120 },
    { dataIndex: 'realName', key: 'realName', title: '姓名', width: 120 },
    { key: 'session', title: '会话', width: 140 },
  ];
  if (isSuperAdmin.value) {
    result.push({
      dataIndex: 'tenantId',
      key: 'tenantId',
      title: '租户',
      width: 160,
    });
  }
  result.push(
    { dataIndex: 'ip', key: 'ip', title: 'IP 地址', width: 130 },
    { dataIndex: 'browser', key: 'browser', title: '浏览器', width: 150 },
    { dataIndex: 'os', key: 'os', title: '操作系统', width: 140 },
    { dataIndex: 'time', key: 'time', title: '连接时间', width: 170 },
    { fixed: 'right', key: 'action', title: '操作', width: 120 },
  );
  return result;
});

function tenantName(tenantId?: number) {
  const tenant = tenants.value.find((item) => item.value === tenantId);
  return tenant?.label || String(tenantId || '-');
}

function displayIp(ip?: string) {
  return ip === '0.0.0.1' || ip === '::1' ? '127.0.0.1（本机）' : ip || '-';
}

function asOnlineUser(value: Record<string, unknown>) {
  return value as unknown as OnlineUserRecord;
}

function isCurrentSession(record: OnlineUserRecord) {
  return (
    Boolean(activeConnectionId.value) &&
    record.connectionId === activeConnectionId.value
  );
}

function isOwnAccount(record: OnlineUserRecord) {
  return String(record.userId) === currentUserId.value;
}

async function loadUsers() {
  if (!isSystemAdmin.value || loading.value) return;
  loading.value = true;
  try {
    const data = await pageOnlineUsersApi({
      page: pager.current,
      pageSize: pager.pageSize,
      realName: query.realName?.trim() || undefined,
      tenantId: isSuperAdmin.value ? query.tenantId : undefined,
      userName: query.userName?.trim() || undefined,
    });
    users.value = data.items ?? [];
    pager.total = Number(data.total ?? 0);
  } finally {
    loading.value = false;
  }
}

async function showPanel() {
  open.value = true;
  if (isSuperAdmin.value && tenants.value.length === 0) {
    tenants.value = await listOnlineUserTenantsApi();
  }
  await loadUsers();
}

function search() {
  pager.current = 1;
  void loadUsers();
}

function reset() {
  query.userName = undefined;
  query.realName = undefined;
  query.tenantId = undefined;
  search();
}

function changePage(page: number, pageSize: number) {
  pager.current = page;
  pager.pageSize = pageSize;
  void loadUsers();
}

function confirmOffline(record: OnlineUserRecord) {
  const ownAccount = isOwnAccount(record);
  Modal.confirm({
    centered: true,
    content: ownAccount
      ? '该浏览器或标签页会立即退出，不影响你正在使用的当前窗口。'
      : `下线后，“${record.realName || record.userName}”需要重新登录。系统会再次核对账号、租户和连接状态。`,
    okButtonProps: { danger: true },
    okText: ownAccount ? '退出此会话' : '确认下线',
    title: ownAccount ? '退出其他登录会话？' : '强制下线该账号？',
    async onOk() {
      await forceOfflineApi(record.connectionId, activeConnectionId.value);
      message.success(ownAccount ? '其他登录会话已退出' : '下线指令已发送');
      await loadUsers();
    },
  });
}

function hubUrl() {
  const apiUrl = String(import.meta.env.VITE_GLOB_API_URL || '/api').replace(
    /\/$/,
    '',
  );
  const baseUrl = apiUrl.replace(/\/api$/, '');
  const token = getStoredAccessToken() ?? '';
  return `${baseUrl}/hubs/onlineUser?token=${encodeURIComponent(token)}`;
}

async function startConnection() {
  const token = getStoredAccessToken();
  if (!token || connection) return;
  const signalR = await import('@microsoft/signalr');
  connectionState.value = 'connecting';
  connection = new signalR.HubConnectionBuilder()
    .configureLogging(signalR.LogLevel.Warning)
    .withUrl(hubUrl())
    .withAutomaticReconnect([0, 3000, 5000, 10_000])
    .build();

  connection.on('OnlineUserList', () => {
    if (open.value && isSystemAdmin.value) void loadUsers();
  });
  connection.on('ForceOffline', async () => {
    if (loggingOut) return;
    loggingOut = true;
    message.warning('当前连接已被管理员下线，请重新登录');
    await connection?.stop();
    await authStore.logout();
  });
  connection.onreconnecting(() => {
    activeConnectionId.value = '';
    connectionState.value = 'connecting';
  });
  connection.onreconnected(() => {
    connectionState.value = 'connected';
    activeConnectionId.value = connection?.connectionId ?? '';
    if (open.value && isSystemAdmin.value) void loadUsers();
  });
  connection.onclose(() => {
    activeConnectionId.value = '';
    connectionState.value = 'offline';
  });

  try {
    await connection.start();
    activeConnectionId.value = connection.connectionId ?? '';
    connectionState.value = 'connected';
  } catch {
    connectionState.value = 'offline';
    activeConnectionId.value = '';
    connection = undefined;
  }
}

onMounted(startConnection);
onBeforeUnmount(async () => {
  await connection?.stop();
  connection = undefined;
});
</script>

<template>
  <template v-if="isSystemAdmin">
    <Tooltip title="在线用户">
      <Badge :count="pager.total" :overflow-count="99" size="small">
        <Button
          aria-label="在线用户"
          class="online-trigger"
          type="text"
          @click="showPanel"
        >
          <template #icon>
            <IconifyIcon icon="lucide:users-round" />
          </template>
        </Button>
      </Badge>
    </Tooltip>

    <Drawer
      v-model:open="open"
      class="online-drawer"
      :footer="null"
      title="在线用户"
      width="min(960px, 96vw)"
    >
      <template #extra>
        <Tag
          :color="
            connectionState === 'connected'
              ? 'green'
              : connectionState === 'connecting'
                ? 'orange'
                : 'default'
          "
        >
          {{
            connectionState === 'connected'
              ? '实时连接'
              : connectionState === 'connecting'
                ? '正在重连'
                : '连接断开'
          }}
        </Tag>
      </template>

      <div class="online-summary">
        <div>
          <strong>{{ pager.total }}</strong>
          <span>个在线会话</span>
        </div>
        <p>
          每条记录代表一个浏览器或标签页连接；同一账号在多个窗口登录，会显示多条记录。
        </p>
      </div>

      <div class="online-filters">
        <Select
          v-if="isSuperAdmin"
          v-model:value="query.tenantId"
          allow-clear
          :options="
            tenants.map((item) => ({
              label: `${item.label}${item.host ? ` (${item.host})` : ''}`,
              value: item.value,
            }))
          "
          placeholder="全部租户"
        />
        <Input
          v-model:value="query.userName"
          allow-clear
          placeholder="账号"
          @press-enter="search"
        />
        <Input
          v-model:value="query.realName"
          allow-clear
          placeholder="姓名"
          @press-enter="search"
        />
        <Space>
          <Button type="primary" @click="search">
            <template #icon><IconifyIcon icon="lucide:search" /></template>
            查询
          </Button>
          <Button @click="reset">
            <template #icon>
              <IconifyIcon icon="lucide:rotate-ccw" />
            </template>
            重置
          </Button>
        </Space>
      </div>

      <Table
        :columns="columns"
        :data-source="users"
        :loading="loading"
        :pagination="{
          ...ADMIN_PAGINATION_PROPS,
          current: pager.current,
          pageSize: pager.pageSize,
          total: pager.total,
          showTotal: (total: number) => `共 ${total} 个会话`,
        }"
        row-key="connectionId"
        :scroll="{ x: isSuperAdmin ? 1230 : 1070 }"
        size="small"
        @change="
          (page: any) => changePage(page.current || 1, page.pageSize || 20)
        "
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'tenantId'">
            {{ tenantName(record.tenantId) }}
          </template>
          <template v-else-if="column.key === 'ip'">
            {{ displayIp(record.ip) }}
          </template>
          <template v-else-if="column.key === 'session'">
            <Tag v-if="isCurrentSession(asOnlineUser(record))" color="green">
              当前窗口
            </Tag>
            <Tag v-else-if="isOwnAccount(asOnlineUser(record))" color="blue">
              同账号其他窗口
            </Tag>
            <Tag v-else>其他账号</Tag>
          </template>
          <template v-else-if="column.key === 'action'">
            <span
              v-if="isCurrentSession(asOnlineUser(record))"
              class="current-session"
            >
              正在使用
            </span>
            <Button
              v-else-if="canForceOffline"
              danger
              size="small"
              type="link"
              @click="confirmOffline(asOnlineUser(record))"
            >
              {{ isOwnAccount(asOnlineUser(record)) ? '退出此会话' : '下线' }}
            </Button>
            <span v-else>-</span>
          </template>
        </template>
      </Table>
    </Drawer>
  </template>
</template>

<style scoped>
.online-trigger {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  padding: 0;
  border-radius: 6px;
}

.online-summary {
  display: flex;
  gap: 16px;
  align-items: center;
  justify-content: space-between;
  padding: 10px 12px;
  margin-bottom: 12px;
  background: hsl(var(--muted) / 40%);
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
}

.online-summary div {
  display: flex;
  flex: none;
  gap: 8px;
  align-items: baseline;
}

.online-summary strong {
  font-size: 20px;
  color: hsl(var(--foreground));
}

.online-summary span,
.online-summary p {
  font-size: 12px;
  color: hsl(var(--muted-foreground));
}

.online-summary p {
  margin: 0;
  text-align: right;
}

.online-filters {
  display: grid;
  grid-template-columns: repeat(3, minmax(130px, 1fr)) auto;
  gap: 8px;
  margin-bottom: 12px;
}

.current-session {
  font-size: 12px;
  color: hsl(var(--muted-foreground));
}

@media (max-width: 760px) {
  .online-summary {
    flex-direction: column;
    align-items: flex-start;
  }

  .online-summary p {
    text-align: left;
  }

  .online-filters {
    grid-template-columns: 1fr;
  }
}
</style>
