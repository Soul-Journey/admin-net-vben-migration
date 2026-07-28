<script setup lang="ts">
import type { TreeProps } from 'ant-design-vue';

import { computed, onMounted, ref } from 'vue';

import { useAccess } from '@vben/access';
import { IconifyIcon } from '@vben/icons';

import {
  Alert,
  Button,
  Empty,
  Input,
  message,
  Modal,
  Space,
  Spin,
  Tag,
  Tooltip,
  Tree,
} from 'ant-design-vue';

import {
  clearCachesApi,
  deleteCacheApi,
  deleteCachePrefixApi,
  getCacheKeysApi,
  getCacheValueApi,
} from '#/api';

defineOptions({ name: 'AdminNetSystemCache' });

type CacheNode = NonNullable<TreeProps['treeData']>[number] & {
  count?: number;
  isGroup?: boolean;
  rawKey?: string;
};

const { hasAccessByCodes } = useAccess();
const loading = ref(false);
const valueLoading = ref(false);
const actionLoading = ref('');
const keys = ref<string[]>([]);
const keyword = ref('');
const selectedKeys = ref<(number | string)[]>([]);
const expandedKeys = ref<(number | string)[]>([]);
const selectedNode = ref<CacheNode>();
const cacheValue = ref<unknown>();

const groups = computed(() => {
  const map = new Map<string, string[]>();
  for (const key of keys.value) {
    const group = key.includes(':') ? key.slice(0, key.indexOf(':')) : '其他';
    const list = map.get(group) ?? [];
    list.push(key);
    map.set(group, list);
  }
  return [...map.entries()].toSorted(([a], [b]) => a.localeCompare(b));
});

const treeData = computed<CacheNode[]>(() => {
  const term = keyword.value.trim().toLowerCase();
  return groups.value.flatMap(([group, groupKeys]) => {
    const filtered = term
      ? groupKeys.filter((key) => key.toLowerCase().includes(term))
      : groupKeys;
    if (filtered.length === 0 && !group.toLowerCase().includes(term)) return [];
    const visible = filtered.length > 0 ? filtered : groupKeys;
    return [
      {
        children: visible.map((key) => ({
          key: `key:${key}`,
          rawKey: key,
          title: key.slice(group === '其他' ? 0 : group.length + 1) || key,
        })),
        count: groupKeys.length,
        isGroup: true,
        key: `group:${group}`,
        rawKey: group === '其他' ? '' : group,
        title: group,
      },
    ];
  });
});

const displayValue = computed(() => {
  if (cacheValue.value === undefined) return '';
  if (typeof cacheValue.value === 'string') {
    try {
      return JSON.stringify(JSON.parse(cacheValue.value), null, 2);
    } catch {
      return cacheValue.value;
    }
  }
  return JSON.stringify(cacheValue.value, null, 2);
});

function can(code: string) {
  return hasAccessByCodes([code]);
}

async function loadKeys(showFeedback = false) {
  loading.value = true;
  try {
    keys.value = await getCacheKeysApi();
    selectedKeys.value = [];
    selectedNode.value = undefined;
    cacheValue.value = undefined;
    if (showFeedback)
      message.success(`缓存列表已刷新，共 ${keys.value.length} 个键`);
  } finally {
    loading.value = false;
  }
}

async function selectNode(_: (number | string)[], info: any) {
  const node = info.node as CacheNode;
  selectedKeys.value = [node.key as string];
  selectedNode.value = node;
  cacheValue.value = undefined;
  if (node.isGroup || !node.rawKey) return;
  valueLoading.value = true;
  try {
    cacheValue.value = await getCacheValueApi(node.rawKey);
  } finally {
    valueLoading.value = false;
  }
}

function removeSelectedKey() {
  const key = selectedNode.value?.rawKey;
  if (!key || selectedNode.value?.isGroup) return;
  Modal.confirm({
    content: `删除后依赖该缓存的功能会在下次访问时重新计算。确定删除“${key}”吗？`,
    okButtonProps: { danger: true },
    okText: '删除缓存',
    title: '删除单个缓存',
    async onOk() {
      actionLoading.value = 'key';
      try {
        await deleteCacheApi(key);
        message.success('缓存已删除');
        await loadKeys();
      } finally {
        actionLoading.value = '';
      }
    },
  });
}

function removeSelectedGroup() {
  const node = selectedNode.value;
  const prefix = node?.rawKey;
  if (!node?.isGroup || !prefix) return;
  Modal.confirm({
    content: `将删除前缀“${prefix}”下的 ${node.count ?? 0} 个缓存键。相关功能会在后续请求中重建缓存。`,
    okButtonProps: { danger: true },
    okText: '清除此分组',
    title: '按前缀清理缓存',
    async onOk() {
      actionLoading.value = 'group';
      try {
        const count = await deleteCachePrefixApi(prefix);
        message.success(`已删除 ${count} 个缓存键`);
        await loadKeys();
      } finally {
        actionLoading.value = '';
      }
    },
  });
}

function clearAll() {
  Modal.confirm({
    content: `将删除 Admin.NET 当前环境的 ${keys.value.length} 个应用缓存键，并清理本机进程缓存。登录、权限、字典等缓存会按请求重新生成。`,
    okButtonProps: { danger: true },
    okText: '确认清空',
    title: '清空应用缓存',
    async onOk() {
      actionLoading.value = 'all';
      try {
        await clearCachesApi();
        message.success('Admin.NET 应用缓存已清空');
        await loadKeys();
      } finally {
        actionLoading.value = '';
      }
    },
  });
}

function expandAll() {
  expandedKeys.value = treeData.value.map((node) => node.key as string);
}
function collapseAll() {
  expandedKeys.value = [];
}

onMounted(() => loadKeys());
</script>

<template>
  <div class="cache-page">
    <Alert
      banner
      message="缓存操作会影响当前环境的登录、权限、字典和业务读取结果，请确认范围后再执行。"
      show-icon
      type="warning"
    />
    <div class="cache-workspace">
      <aside class="cache-nav">
        <div class="panel-heading">
          <div>
            <h2>缓存键</h2>
            <p>{{ keys.length }} 个键，{{ groups.length }} 个分组</p>
          </div>
          <Space :size="2">
            <Tooltip title="刷新">
              <Button
                :loading="loading"
                size="small"
                type="text"
                @click="loadKeys(true)"
              >
                <template #icon>
                  <IconifyIcon icon="lucide:refresh-cw" />
                </template>
              </Button> </Tooltip
            ><Tooltip title="清空全部应用缓存">
              <Button
                v-if="can('sysCache:clear')"
                danger
                :loading="actionLoading === 'all'"
                size="small"
                type="text"
                @click="clearAll"
              >
                <template #icon>
                  <IconifyIcon icon="lucide:trash-2" />
                </template>
              </Button>
            </Tooltip>
          </Space>
        </div>
        <Input v-model:value="keyword" allow-clear placeholder="搜索缓存键">
          <template #prefix><IconifyIcon icon="lucide:search" /></template>
        </Input>
        <div class="tree-tools">
          <Button size="small" type="link" @click="expandAll">全部展开</Button
          ><Button size="small" type="link" @click="collapseAll">
            全部折叠
          </Button>
        </div>
        <Spin :spinning="loading">
          <Tree
            v-model:expanded-keys="expandedKeys"
            :selected-keys="selectedKeys"
            :tree-data="treeData"
            block-node
            @select="selectNode"
          >
            <template #title="{ title, isGroup, count }">
              <div class="tree-title">
                <IconifyIcon
                  :icon="isGroup ? 'lucide:folder' : 'lucide:key-round'"
                /><span class="tree-label">{{ title }}</span
                ><Tag v-if="isGroup" class="count-tag">{{ count }}</Tag>
              </div>
            </template>
          </Tree>
        </Spin>
      </aside>
      <section class="cache-detail">
        <div class="detail-heading">
          <div>
            <h2>
              {{
                selectedNode?.isGroup
                  ? `缓存分组：${selectedNode.title}`
                  : selectedNode?.rawKey
                    ? '缓存值'
                    : '缓存数据'
              }}
            </h2>
            <p v-if="selectedNode?.rawKey">{{ selectedNode.rawKey }}</p>
            <p v-else>从左侧选择缓存键查看内容</p>
          </div>
          <Button
            v-if="
              selectedNode?.isGroup &&
              selectedNode.rawKey &&
              can('sysCache:delete')
            "
            danger
            :loading="actionLoading === 'group'"
            @click="removeSelectedGroup"
          >
            <template #icon><IconifyIcon icon="lucide:folder-x" /></template
            >清除此分组 </Button
          ><Button
            v-else-if="selectedNode?.rawKey && can('sysCache:delete')"
            danger
            :loading="actionLoading === 'key'"
            @click="removeSelectedKey"
          >
            <template #icon><IconifyIcon icon="lucide:trash-2" /></template
            >删除缓存
          </Button>
        </div>
        <Spin :spinning="valueLoading" class="value-spin">
          <div v-if="selectedNode?.isGroup" class="group-summary">
            <IconifyIcon icon="lucide:folder-open" /><strong>{{
              selectedNode.count ?? 0
            }}</strong
            ><span>个缓存键</span>
            <p>展开左侧分组并选择具体键，可查看缓存值。</p>
          </div>
          <pre v-else-if="displayValue" class="json-viewer">{{
            displayValue
          }}</pre>
          <Empty
            v-else
            :description="selectedNode ? '缓存值为空' : '请选择缓存键'"
          />
        </Spin>
      </section>
    </div>
  </div>
</template>

<style scoped>
.cache-page {
  min-height: 100%;
  padding: 12px;
  background: #f5f7fb;
}

.cache-workspace {
  display: grid;
  grid-template-columns: minmax(280px, 330px) minmax(0, 1fr);
  min-height: calc(100vh - 205px);
  margin-top: 10px;
  overflow: hidden;
  background: #fff;
  border: 1px solid #e7eaf0;
  border-radius: 8px;
}

.cache-nav {
  padding: 14px;
  overflow: auto;
  border-right: 1px solid #e7eaf0;
}

.panel-heading,
.detail-heading {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  margin-bottom: 12px;
}

.panel-heading h2,
.detail-heading h2 {
  margin: 0;
  font-size: 16px;
  font-weight: 650;
}

.panel-heading p,
.detail-heading p {
  max-width: 680px;
  margin: 3px 0 0;
  overflow: hidden;
  text-overflow: ellipsis;
  font-size: 12px;
  color: #667085;
  white-space: nowrap;
}

.tree-tools {
  display: flex;
  justify-content: flex-end;
  margin: 5px 0;
}

.tree-title {
  display: flex;
  gap: 7px;
  align-items: center;
  min-width: 0;
}

.tree-label {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.count-tag {
  margin-left: auto;
  color: #667085;
}

.cache-detail {
  min-width: 0;
  padding: 14px;
}

.detail-heading {
  min-height: 45px;
  padding-bottom: 10px;
  border-bottom: 1px solid #edf0f4;
}

.value-spin {
  display: block;
  min-height: 420px;
}

.json-viewer {
  min-height: 420px;
  max-height: calc(100vh - 290px);
  padding: 16px;
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

.group-summary {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  min-height: 360px;
  color: #667085;
}

.group-summary > svg {
  margin-bottom: 10px;
  font-size: 42px;
  color: #6684ff;
}

.group-summary strong {
  font-size: 28px;
  color: #1d2939;
}

.group-summary p {
  margin-top: 14px;
  font-size: 12px;
}

@media (max-width: 900px) {
  .cache-workspace {
    grid-template-columns: 1fr;
  }

  .cache-nav {
    max-height: 360px;
    border-right: 0;
    border-bottom: 1px solid #e7eaf0;
  }
}
</style>
