<script setup lang="ts">
import type { ReceivedNoticeRecord, SysOrg } from '#/api';

import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';

import { IconifyIcon } from '@vben/icons';
import { preferences } from '@vben/preferences';
import { useUserStore } from '@vben/stores';

import { Avatar, Button, Empty, Skeleton, Tag } from 'ant-design-vue';

import {
  getOrgListApi,
  pageOnlineUsersApi,
  pageReceivedNoticesApi,
  pageRolesApi,
  pageUsersApi,
} from '#/api';

defineOptions({ name: 'AdminNetWorkspace' });

type MetricKey = 'online' | 'orgs' | 'roles' | 'users';

const router = useRouter();
const userStore = useUserStore();
const loading = ref(false);
const lastUpdated = ref('');
const notices = ref<ReceivedNoticeRecord[]>([]);
const metrics = ref<Record<MetricKey, number | undefined>>({
  online: undefined,
  orgs: undefined,
  roles: undefined,
  users: undefined,
});

const userInfo = computed(
  () => userStore.userInfo as null | Record<string, any>,
);
const greeting = computed(() => {
  const hour = new Date().getHours();
  if (hour < 6) return '夜深了';
  if (hour < 12) return '早上好';
  if (hour < 18) return '下午好';
  return '晚上好';
});
const unreadCount = computed(
  () => notices.value.filter((item) => item.readStatus === 0).length,
);

const metricCards = computed(() => [
  {
    color: 'blue',
    icon: 'lucide:users',
    key: 'users' as const,
    label: '账号总数',
    value: metrics.value.users,
  },
  {
    color: 'green',
    icon: 'lucide:shield-check',
    key: 'roles' as const,
    label: '角色总数',
    value: metrics.value.roles,
  },
  {
    color: 'amber',
    icon: 'lucide:building-2',
    key: 'orgs' as const,
    label: '机构节点',
    value: metrics.value.orgs,
  },
  {
    color: 'red',
    icon: 'lucide:radio-tower',
    key: 'online' as const,
    label: '在线连接',
    value: metrics.value.online,
  },
]);

const quickActions = [
  {
    description: '维护账号、角色和所属机构',
    icon: 'lucide:user-round-cog',
    path: '/system/user',
    title: '账号管理',
  },
  {
    description: '配置角色菜单与数据范围',
    icon: 'lucide:shield-check',
    path: '/system/role',
    title: '角色管理',
  },
  {
    description: '维护组织树和机构信息',
    icon: 'lucide:building-2',
    path: '/system/org',
    title: '机构管理',
  },
  {
    description: '查看系统通知与公告',
    icon: 'lucide:bell',
    path: '/dashboard/notice',
    title: '站内信',
  },
  {
    description: '查看定时作业和运行记录',
    icon: 'lucide:timer',
    path: '/platform/job',
    title: '任务调度',
  },
  {
    description: '追踪后台操作和接口调用',
    icon: 'lucide:clipboard-list',
    path: '/log/oplog',
    title: '操作日志',
  },
];

const visibleQuickActions = computed(() => {
  const paths = new Set(router.getRoutes().map((route) => route.path));
  return quickActions.filter((item) => paths.has(item.path));
});

function countOrgNodes(items: SysOrg[]): number {
  return items.reduce(
    (total, item) => total + 1 + countOrgNodes(item.children ?? []),
    0,
  );
}

function formatTime(value?: string) {
  if (!value) return '未记录时间';
  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? value
    : date.toLocaleString('zh-CN', { hour12: false });
}

async function loadWorkspace() {
  loading.value = true;
  const [users, roles, orgs, online, received] = await Promise.allSettled([
    pageUsersApi({ orgId: -1, page: 1, pageSize: 1 }),
    pageRolesApi({ page: 1, pageSize: 1 }),
    getOrgListApi(),
    pageOnlineUsersApi({ page: 1, pageSize: 1 }),
    pageReceivedNoticesApi({ page: 1, pageSize: 5 }),
  ]);

  metrics.value = {
    online: online.status === 'fulfilled' ? online.value.total : undefined,
    orgs: orgs.status === 'fulfilled' ? countOrgNodes(orgs.value) : undefined,
    roles: roles.status === 'fulfilled' ? roles.value.total : undefined,
    users: users.status === 'fulfilled' ? users.value.total : undefined,
  };
  notices.value = received.status === 'fulfilled' ? received.value.items : [];
  lastUpdated.value = new Date().toLocaleTimeString('zh-CN', {
    hour12: false,
    hour: '2-digit',
    minute: '2-digit',
  });
  loading.value = false;
}

function navigate(path: string) {
  router.push(path);
}

onMounted(loadWorkspace);
</script>

<template>
  <main class="workspace-page">
    <header class="workspace-header">
      <div class="identity-block">
        <Avatar
          :size="48"
          :src="userInfo?.avatar || preferences.app.defaultAvatar"
        />
        <div class="min-w-0">
          <h1>
            {{ greeting }}，{{
              userInfo?.realName || userInfo?.username || '管理员'
            }}
          </h1>
          <p>
            <span>{{ userInfo?.orgName || '当前组织未设置' }}</span>
            <span v-if="userInfo?.posName"> · {{ userInfo.posName }}</span>
            <span v-if="lastUpdated"> · 数据更新于 {{ lastUpdated }}</span>
          </p>
        </div>
      </div>
      <Button :loading="loading" @click="loadWorkspace">
        <template #icon><IconifyIcon icon="lucide:refresh-cw" /></template>
        刷新数据
      </Button>
    </header>

    <section class="metric-strip" aria-label="系统概览">
      <div v-for="item in metricCards" :key="item.key" class="metric-item">
        <span class="metric-icon" :class="`is-${item.color}`">
          <IconifyIcon :icon="item.icon" />
        </span>
        <div>
          <span>{{ item.label }}</span>
          <strong>{{ item.value ?? '--' }}</strong>
        </div>
      </div>
    </section>

    <div class="workspace-grid">
      <section class="workspace-section quick-section">
        <div class="section-heading">
          <div>
            <h2>常用功能</h2>
            <p>进入当前账号有权限使用的核心模块</p>
          </div>
        </div>
        <div class="quick-grid">
          <button
            v-for="item in visibleQuickActions"
            :key="item.path"
            class="quick-item"
            type="button"
            @click="navigate(item.path)"
          >
            <span class="quick-icon"><IconifyIcon :icon="item.icon" /></span>
            <span class="quick-copy"
              ><strong>{{ item.title }}</strong
              ><small>{{ item.description }}</small></span
            >
            <IconifyIcon class="quick-arrow" icon="lucide:chevron-right" />
          </button>
        </div>
      </section>

      <section class="workspace-section session-section">
        <div class="section-heading">
          <div>
            <h2>当前会话</h2>
            <p>登录账号与权限上下文</p>
          </div>
        </div>
        <dl class="session-list">
          <div>
            <dt>登录账号</dt>
            <dd>{{ userInfo?.username || '--' }}</dd>
          </div>
          <div>
            <dt>所属机构</dt>
            <dd>{{ userInfo?.orgName || '--' }}</dd>
          </div>
          <div>
            <dt>当前职位</dt>
            <dd>{{ userInfo?.posName || '--' }}</dd>
          </div>
          <div>
            <dt>租户编号</dt>
            <dd>{{ userInfo?.tenantId ?? '--' }}</dd>
          </div>
        </dl>
      </section>

      <section class="workspace-section notice-section">
        <div class="section-heading">
          <div class="notice-title">
            <h2>最新站内信</h2>
            <Tag v-if="unreadCount" color="blue">{{ unreadCount }} 条未读</Tag>
          </div>
          <Button type="link" @click="navigate('/dashboard/notice')">
            查看全部
          </Button>
        </div>
        <Skeleton
          v-if="loading && notices.length === 0"
          :paragraph="{ rows: 3 }"
          active
        />
        <div v-else-if="notices.length > 0" class="notice-list">
          <button
            v-for="item in notices"
            :key="item.id"
            type="button"
            @click="navigate('/dashboard/notice')"
          >
            <span
              class="notice-dot"
              :class="{ unread: item.readStatus === 0 }"
              aria-hidden="true"
            ></span>
            <span class="notice-content"
              ><strong>{{ item.notice.title }}</strong
              ><small>{{
                formatTime(item.notice.publicTime || item.notice.createTime)
              }}</small></span
            >
            <Tag :color="item.readStatus === 0 ? 'blue' : 'default'">
              {{ item.readStatus === 0 ? '未读' : '已读' }}
            </Tag>
          </button>
        </div>
        <Empty
          v-else
          description="暂无站内信"
          :image="Empty.PRESENTED_IMAGE_SIMPLE"
        />
      </section>

      <section class="workspace-section guide-section">
        <div class="section-heading">
          <div>
            <h2>管理提示</h2>
            <p>减少权限和数据配置风险</p>
          </div>
        </div>
        <ul>
          <li>
            <IconifyIcon icon="lucide:shield-check" /><span
              >权限调整后使用对应租户账号重新登录验收。</span
            >
          </li>
          <li>
            <IconifyIcon icon="lucide:database" /><span
              >同步、清理和批量操作前先确认影响范围。</span
            >
          </li>
          <li>
            <IconifyIcon icon="lucide:history" /><span
              >重要配置修改后及时核对操作日志。</span
            >
          </li>
        </ul>
      </section>
    </div>
  </main>
</template>

<style scoped>
.workspace-page {
  min-height: 100%;
  padding: 16px;
  background: hsl(var(--muted) / 35%);
}

.workspace-header {
  display: flex;
  gap: 16px;
  align-items: center;
  justify-content: space-between;
  padding: 4px 2px 16px;
}

.identity-block {
  display: flex;
  gap: 12px;
  align-items: center;
  min-width: 0;
}

.identity-block h1 {
  margin: 0;
  font-size: 20px;
  font-weight: 650;
  line-height: 28px;
  letter-spacing: 0;
}

.identity-block p {
  margin: 3px 0 0;
  overflow: hidden;
  text-overflow: ellipsis;
  font-size: 13px;
  color: hsl(var(--muted-foreground));
  white-space: nowrap;
}

.metric-strip {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  overflow: hidden;
  background: hsl(var(--background));
  border: 1px solid hsl(var(--border));
  border-radius: 8px;
}

.metric-item {
  display: flex;
  gap: 12px;
  align-items: center;
  min-height: 78px;
  padding: 14px 18px;
  border-right: 1px solid hsl(var(--border));
}

.metric-item:last-child {
  border-right: 0;
}

.metric-icon {
  display: inline-flex;
  flex: none;
  align-items: center;
  justify-content: center;
  width: 38px;
  height: 38px;
  font-size: 19px;
  border-radius: 7px;
}

.metric-icon.is-blue {
  color: #2563eb;
  background: #eff6ff;
}

.metric-icon.is-green {
  color: #15803d;
  background: #f0fdf4;
}

.metric-icon.is-amber {
  color: #b45309;
  background: #fffbeb;
}

.metric-icon.is-red {
  color: #be123c;
  background: #fff1f2;
}

.metric-item > div {
  display: flex;
  flex: 1;
  gap: 10px;
  align-items: baseline;
  justify-content: space-between;
  min-width: 0;
}

.metric-item span {
  font-size: 13px;
  color: hsl(var(--muted-foreground));
}

.metric-item strong {
  font-size: 22px;
  font-variant-numeric: tabular-nums;
}

.workspace-grid {
  display: grid;
  grid-template-columns: minmax(0, 1.55fr) minmax(300px, 0.75fr);
  gap: 12px;
  margin-top: 12px;
}

.workspace-section {
  min-width: 0;
  padding: 14px;
  background: hsl(var(--background));
  border: 1px solid hsl(var(--border));
  border-radius: 8px;
}

.section-heading {
  display: flex;
  gap: 12px;
  align-items: flex-start;
  justify-content: space-between;
  margin-bottom: 12px;
}

.section-heading h2 {
  margin: 0;
  font-size: 15px;
  font-weight: 650;
}

.section-heading p {
  margin: 2px 0 0;
  font-size: 12px;
  color: hsl(var(--muted-foreground));
}

.quick-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 8px;
}

.quick-item {
  display: flex;
  gap: 10px;
  align-items: center;
  min-width: 0;
  min-height: 66px;
  padding: 10px 12px;
  text-align: left;
  background: transparent;
  border: 1px solid hsl(var(--border));
  border-radius: 7px;
  transition:
    border-color 160ms,
    background-color 160ms;
}

.quick-item:hover {
  background: hsl(var(--primary) / 5%);
  border-color: hsl(var(--primary) / 45%);
}

.quick-icon {
  display: inline-flex;
  flex: none;
  align-items: center;
  justify-content: center;
  width: 34px;
  height: 34px;
  font-size: 17px;
  color: hsl(var(--primary));
  background: hsl(var(--primary) / 10%);
  border-radius: 6px;
}

.quick-copy {
  display: flex;
  flex: 1;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
}

.quick-copy strong {
  font-size: 13px;
}

.quick-copy small {
  overflow: hidden;
  text-overflow: ellipsis;
  font-size: 12px;
  color: hsl(var(--muted-foreground));
  white-space: nowrap;
}

.quick-arrow {
  flex: none;
  color: hsl(var(--muted-foreground));
}

.session-list {
  margin: 0;
}

.session-list div {
  display: flex;
  gap: 16px;
  align-items: center;
  justify-content: space-between;
  min-height: 42px;
  border-bottom: 1px solid hsl(var(--border) / 65%);
}

.session-list div:last-child {
  border-bottom: 0;
}

.session-list dt {
  font-size: 12px;
  color: hsl(var(--muted-foreground));
}

.session-list dd {
  min-width: 0;
  margin: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  font-size: 13px;
  font-weight: 550;
  white-space: nowrap;
}

.notice-title {
  display: flex;
  gap: 8px;
  align-items: center;
}

.notice-section {
  min-height: 250px;
}

.notice-list {
  display: grid;
}

.notice-list button {
  display: flex;
  gap: 10px;
  align-items: center;
  min-width: 0;
  min-height: 44px;
  padding: 7px 4px;
  text-align: left;
  background: transparent;
  border: 0;
  border-bottom: 1px solid hsl(var(--border) / 65%);
}

.notice-list button:last-child {
  border-bottom: 0;
}

.notice-list button:hover {
  background: hsl(var(--muted) / 38%);
}

.notice-dot {
  flex: none;
  width: 7px;
  height: 7px;
  background: hsl(var(--muted-foreground) / 35%);
  border-radius: 50%;
}

.notice-dot.unread {
  background: #2563eb;
}

.notice-content {
  display: flex;
  flex: 1;
  flex-direction: column;
  min-width: 0;
}

.notice-content strong {
  overflow: hidden;
  text-overflow: ellipsis;
  font-size: 13px;
  font-weight: 550;
  white-space: nowrap;
}

.notice-content small {
  font-size: 11px;
  color: hsl(var(--muted-foreground));
}

.guide-section ul {
  display: grid;
  gap: 10px;
  padding: 0;
  margin: 0;
  list-style: none;
}

.guide-section li {
  display: flex;
  gap: 8px;
  align-items: flex-start;
  font-size: 12px;
  line-height: 1.6;
  color: hsl(var(--muted-foreground));
}

.guide-section li :deep(svg) {
  flex: none;
  margin-top: 2px;
  color: hsl(var(--foreground));
}

@media (max-width: 1100px) {
  .metric-strip {
    grid-template-columns: repeat(2, 1fr);
  }

  .metric-item:nth-child(2) {
    border-right: 0;
  }

  .metric-item:nth-child(-n + 2) {
    border-bottom: 1px solid hsl(var(--border));
  }

  .workspace-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 640px) {
  .workspace-page {
    padding: 10px;
  }

  .workspace-header {
    align-items: flex-start;
  }

  .workspace-header > :deep(.ant-btn) {
    width: 34px;
    padding-inline: 8px;
    overflow: hidden;
  }

  .metric-strip,
  .quick-grid {
    grid-template-columns: 1fr;
  }

  .metric-item {
    border-right: 0;
    border-bottom: 1px solid hsl(var(--border));
  }

  .metric-item:last-child {
    border-bottom: 0;
  }

  .metric-item:nth-child(3) {
    border-bottom: 1px solid hsl(var(--border));
  }
}
</style>
