<script setup lang="ts">
import type { FormInstance, TableColumnsType, TreeProps } from 'ant-design-vue';
import type { Rule } from 'ant-design-vue/es/form';

import type { SaveRegionParams, SysRegionRecord } from '#/api';

import { computed, nextTick, onMounted, reactive, ref, watch } from 'vue';

import { useAccess } from '@vben/access';
import { IconifyIcon } from '@vben/icons';
import { useUserStore } from '@vben/stores';

import {
  Button,
  Dropdown,
  Empty,
  Form,
  Input,
  InputNumber,
  message,
  Modal,
  Space,
  Table,
  Tag,
  Tooltip,
  Tree,
  TreeSelect,
} from 'ant-design-vue';

import {
  addRegionApi,
  deleteRegionApi,
  listRegionChildrenApi,
  pageRegionsApi,
  syncRegionsApi,
  updateRegionApi,
} from '#/api';
import { ADMIN_PAGINATION_PROPS } from '#/utils/pagination';

defineOptions({ name: 'AdminNetSystemRegion' });

type RegionFormState = Partial<SaveRegionParams>;
type RegionTreeNode = Omit<SysRegionRecord, 'children'> & {
  children?: RegionTreeNode[];
  isLeaf?: boolean;
  key: number;
};

const SUPER_ADMIN_ACCOUNT = 999;

const { hasAccessByCodes } = useAccess();
const userStore = useUserStore();

const loading = ref(false);
const treeLoading = ref(false);
const submitLoading = ref(false);
const syncLoading = ref(false);
const modalOpen = ref(false);
const syncModalOpen = ref(false);
const modalTitle = ref('新增行政区域');
const formRef = ref<FormInstance>();
const regions = ref<SysRegionRecord[]>([]);
const treeData = ref<RegionTreeNode[]>([]);
const selectedTreeKeys = ref<Array<number | string>>([]);
const expandedTreeKeys = ref<Array<number | string>>([]);
const treeKeyword = ref('');
const syncConfirmText = ref('');
const formState = reactive<RegionFormState>({});
const tableShellRef = ref<HTMLElement>();

let regionRequestVersion = 0;

const query = reactive({
  code: '',
  name: '',
  page: 1,
  pageSize: 50,
  pid: 0,
  total: 0,
});

const columns: TableColumnsType<SysRegionRecord> = [
  { dataIndex: 'name', key: 'name', title: '行政名称', width: 210 },
  { dataIndex: 'code', key: 'code', title: '行政代码', width: 150 },
  { key: 'level', title: '层级', width: 84 },
  { dataIndex: 'cityCode', key: 'cityCode', title: '区号', width: 92 },
  { dataIndex: 'orderNo', key: 'orderNo', title: '排序', width: 72 },
  { dataIndex: 'remark', key: 'remark', title: '备注', width: 220 },
  { fixed: 'right', key: 'actions', title: '操作', width: 150 },
];

const formRules: Record<string, Rule[]> = {
  code: [
    { message: '请输入行政代码', required: true, trigger: 'blur' },
    {
      message: '行政代码应为 6、9 或 12 位数字',
      pattern: /^(?:\d{6}|\d{9}|\d{12})$/,
      trigger: 'blur',
    },
  ],
  name: [{ message: '请输入行政名称', required: true, trigger: 'blur' }],
  pid: [{ message: '请选择上级区域', required: true, trigger: 'change' }],
};

const isSuperAdmin = computed(
  () =>
    Number(
      (userStore.userInfo as null | Record<string, unknown>)?.accountType,
    ) === SUPER_ADMIN_ACCOUNT,
);

const currentRegion = computed(() =>
  query.pid ? findRegion(treeData.value, query.pid) : undefined,
);

const parentTreeData = computed<TreeProps['treeData']>(() => [
  { key: 0, title: '根节点（省级）', value: 0 },
  ...toTreeSelectData(treeData.value, formState.id),
]);

const filteredTreeData = computed(() =>
  filterRegionTree(treeData.value, treeKeyword.value),
);

function can(code: string) {
  return hasAccessByCodes([code]);
}

function asRegion(record: unknown) {
  return record as SysRegionRecord;
}

function levelMeta(level = 0) {
  const options: Record<number, { color: string; label: string }> = {
    1: { color: 'blue', label: '省级' },
    2: { color: 'cyan', label: '市级' },
    3: { color: 'green', label: '区县级' },
    4: { color: 'orange', label: '街道级' },
    5: { color: 'purple', label: '村级' },
  };
  return options[level] ?? { color: 'default', label: `第 ${level} 级` };
}

function normalizeTreeNodes(items: SysRegionRecord[]): RegionTreeNode[] {
  return items.map((item) => ({
    ...item,
    children: item.children ? normalizeTreeNodes(item.children) : undefined,
    isLeaf: item.children ? item.children.length === 0 : false,
    key: item.id,
  }));
}

function findRegion(
  items: RegionTreeNode[],
  id: number,
): RegionTreeNode | undefined {
  for (const item of items) {
    if (item.id === id) return item;
    const child = findRegion(item.children ?? [], id);
    if (child) return child;
  }
  return undefined;
}

function getAllTreeKeys(items: RegionTreeNode[]): Array<number | string> {
  return items.flatMap((item) => [
    item.id,
    ...getAllTreeKeys(item.children ?? []),
  ]);
}

function filterRegionTree(
  items: RegionTreeNode[],
  keyword: string,
): RegionTreeNode[] {
  const normalized = keyword.trim().toLowerCase();
  if (!normalized) return items;
  return items
    .map((item) => {
      const children = filterRegionTree(item.children ?? [], normalized);
      const matched = `${item.name} ${item.code}`
        .toLowerCase()
        .includes(normalized);
      return matched || children.length > 0 ? { ...item, children } : undefined;
    })
    .filter(Boolean) as RegionTreeNode[];
}

function toTreeSelectData(
  items: RegionTreeNode[],
  excludeId?: number,
): NonNullable<TreeProps['treeData']> {
  return items
    .filter((item) => item.id !== excludeId)
    .map((item) => ({
      children: toTreeSelectData(item.children ?? [], excludeId),
      isLeaf: item.isLeaf,
      key: item.id,
      title: `${item.name}（${item.code}）`,
      value: item.id,
    }));
}

function resetFormState(values: RegionFormState) {
  for (const key of Object.keys(formState)) {
    delete formState[key as keyof RegionFormState];
  }
  Object.assign(formState, values);
}

async function loadRootTree() {
  treeLoading.value = true;
  try {
    treeData.value = normalizeTreeNodes(await listRegionChildrenApi(0));
    expandedTreeKeys.value = [];
  } finally {
    treeLoading.value = false;
  }
}

async function loadTreeNode(treeNode: Record<string, unknown>) {
  const node = (treeNode.dataRef ?? treeNode) as RegionTreeNode;
  if (node.children || node.isLeaf) return;
  const children = normalizeTreeNodes(await listRegionChildrenApi(node.id));
  node.children = children;
  node.isLeaf = children.length === 0;
}

async function loadRegions() {
  if (!can('sysRegion:page')) return;
  const requestVersion = ++regionRequestVersion;
  loading.value = true;
  try {
    const data = await pageRegionsApi({
      code: query.code.trim() || undefined,
      name: query.name.trim() || undefined,
      page: query.page,
      pageSize: query.pageSize,
      pid: query.pid || undefined,
    });
    if (requestVersion === regionRequestVersion) {
      regions.value = data.items ?? [];
      query.total = Number(data.total ?? 0);
    }
  } finally {
    if (requestVersion === regionRequestVersion) loading.value = false;
  }
}

async function handleQuery() {
  query.page = 1;
  await loadRegions();
}

async function selectRoot() {
  query.pid = 0;
  query.page = 1;
  selectedTreeKeys.value = [];
  await loadRegions();
}

async function handleTreeSelect(keys: Array<number | string>) {
  const id = Number(keys[0] ?? 0);
  if (!id) return;
  query.pid = id;
  query.name = '';
  query.code = '';
  query.page = 1;
  selectedTreeKeys.value = [id];
  await loadRegions();
}

async function resetQuery() {
  query.name = '';
  query.code = '';
  query.page = 1;
  await loadRegions();
}

function openCreateRegion() {
  modalTitle.value = '新增行政区域';
  resetFormState({
    cityCode: '',
    code: '',
    name: '',
    orderNo: 100,
    pid: query.pid || 0,
    remark: '',
  });
  modalOpen.value = true;
}

function openEditRegion(record: SysRegionRecord) {
  modalTitle.value = '编辑行政区域';
  resetFormState({
    cityCode: record.cityCode,
    code: record.code,
    id: record.id,
    name: record.name,
    orderNo: record.orderNo ?? 100,
    pid: record.pid,
    remark: record.remark,
  });
  modalOpen.value = true;
}

async function submitRegion() {
  await formRef.value?.validate();
  submitLoading.value = true;
  try {
    const payload = {
      ...formState,
      code: formState.code?.trim() ?? '',
      name: formState.name?.trim() ?? '',
      orderNo: formState.orderNo ?? 100,
      pid: formState.pid ?? 0,
    } as SaveRegionParams;
    if (payload.id) {
      await updateRegionApi(payload as SaveRegionParams & { id: number });
      message.success('行政区域已更新');
    } else {
      await addRegionApi(payload);
      message.success('行政区域已新增');
    }
    modalOpen.value = false;
    await Promise.all([loadRootTree(), loadRegions()]);
  } finally {
    submitLoading.value = false;
  }
}

function confirmDelete(record: SysRegionRecord) {
  Modal.confirm({
    centered: true,
    content: `删除“${record.name}”会同时删除它的所有下级区域。该操作不可撤销，请确认当前选择无误。`,
    okButtonProps: { danger: true },
    okText: '删除区域及下级',
    title: '删除行政区域？',
    async onOk() {
      const count = await deleteRegionApi(record.id);
      message.success(`已删除 ${count || 1} 条区域数据`);
      if (query.pid === record.id) await selectRoot();
      await Promise.all([loadRootTree(), loadRegions()]);
    },
  });
}

function openSyncModal() {
  syncConfirmText.value = '';
  syncModalOpen.value = true;
}

async function submitSync() {
  if (syncConfirmText.value.trim() !== '同步') {
    message.warning('请输入“同步”后再继续');
    return;
  }
  syncLoading.value = true;
  try {
    const result = await syncRegionsApi();
    message.success(
      `已同步 ${result.total} 条：省级 ${result.provinceCount}、市级 ${result.cityCount}、区县级 ${result.countyCount}`,
      6,
    );
    syncModalOpen.value = false;
    await selectRoot();
    await loadRootTree();
  } finally {
    syncLoading.value = false;
  }
}

async function handleTreeMenu({ key }: { key: number | string }) {
  const command = String(key);
  if (command === 'expand')
    expandedTreeKeys.value = getAllTreeKeys(treeData.value);
  if (command === 'collapse') expandedTreeKeys.value = [];
  if (command === 'root') await selectRoot();
  if (command === 'refresh') await loadRootTree();
}

async function changePage(page: number, pageSize: number) {
  query.page = page;
  query.pageSize = pageSize;
  await loadRegions();
  await nextTick();
  tableShellRef.value
    ?.querySelector<HTMLElement>('.ant-table-body')
    ?.scrollTo({ behavior: 'smooth', top: 0 });
}

watch(treeKeyword, (value) => {
  if (value.trim())
    expandedTreeKeys.value = getAllTreeKeys(filteredTreeData.value);
});

onMounted(async () => {
  await Promise.all([loadRootTree(), loadRegions()]);
});
</script>

<template>
  <div class="region-page">
    <aside class="region-nav">
      <div class="nav-head">
        <div>
          <div class="nav-title">区域导航</div>
          <div class="nav-subtitle">逐级展开定位行政区域</div>
        </div>
        <Dropdown
          :menu="{
            items: [
              { key: 'expand', label: '展开已加载节点' },
              { key: 'collapse', label: '全部折叠' },
              { key: 'root', label: '查看全部区域' },
              { key: 'refresh', label: '刷新区域树' },
            ],
            onClick: handleTreeMenu,
          }"
          placement="bottomRight"
          :trigger="['click']"
        >
          <Tooltip title="区域树操作">
            <Button aria-label="区域树操作" size="small">
              <template #icon><IconifyIcon icon="lucide:ellipsis" /></template>
            </Button>
          </Tooltip>
        </Dropdown>
      </div>

      <Input
        v-model:value="treeKeyword"
        allow-clear
        placeholder="搜索已加载的名称或代码"
      >
        <template #prefix><IconifyIcon icon="lucide:search" /></template>
      </Input>

      <Button class="root-button" type="text" @click="selectRoot">
        <template #icon><IconifyIcon icon="lucide:map" /></template>
        全部行政区域
      </Button>

      <div class="tree-wrap">
        <Empty
          v-if="!treeLoading && treeData.length === 0"
          :image="Empty.PRESENTED_IMAGE_SIMPLE"
          description="暂无区域数据"
        />
        <Tree
          v-else
          v-model:expanded-keys="expandedTreeKeys"
          v-model:selected-keys="selectedTreeKeys"
          block-node
          :field-names="{ children: 'children', key: 'id', title: 'name' }"
          :load-data="loadTreeNode"
          :loading="treeLoading"
          :tree-data="filteredTreeData"
          @select="handleTreeSelect"
        >
          <template #title="node">
            <span class="tree-title">
              <IconifyIcon
                :icon="node.level === 1 ? 'lucide:map' : 'lucide:map-pin'"
              />
              <span>{{ node.name }}</span>
              <small>{{ node.code }}</small>
            </span>
          </template>
        </Tree>
      </div>
    </aside>

    <main class="region-main">
      <div class="panel-head">
        <div>
          <div class="panel-title">
            {{ currentRegion ? currentRegion.name : '行政区域' }}
          </div>
          <div class="panel-subtitle">
            {{
              currentRegion
                ? `显示当前区域及直属下级 · ${currentRegion.code}`
                : '维护省、市、区县等行政区域基础数据'
            }}
          </div>
        </div>
      </div>

      <Form :model="query" class="query-form" layout="inline">
        <Form.Item label="行政名称">
          <Input
            v-model:value="query.name"
            allow-clear
            placeholder="行政名称"
            @press-enter="handleQuery"
          />
        </Form.Item>
        <Form.Item label="行政代码">
          <Input
            v-model:value="query.code"
            allow-clear
            placeholder="行政代码"
            @press-enter="handleQuery"
          />
        </Form.Item>
        <Form.Item>
          <Space wrap>
            <Button
              v-if="can('sysRegion:page')"
              :loading="loading"
              type="primary"
              @click="handleQuery"
            >
              <template #icon><IconifyIcon icon="lucide:search" /></template>
              查询
            </Button>
            <Button @click="resetQuery">
              <template #icon>
                <IconifyIcon icon="lucide:rotate-ccw" />
              </template>
              重置
            </Button>
            <Button
              v-if="can('sysRegion:add')"
              type="primary"
              @click="openCreateRegion"
            >
              <template #icon><IconifyIcon icon="lucide:plus" /></template>
              新增
            </Button>
            <Button
              v-if="isSuperAdmin && can('sysRegion:sync')"
              danger
              @click="openSyncModal"
            >
              <template #icon>
                <IconifyIcon icon="lucide:cloud-download" />
              </template>
              同步官方区划
            </Button>
          </Space>
        </Form.Item>
      </Form>

      <div ref="tableShellRef" class="region-table-shell">
        <Table
          :columns="columns"
          :data-source="regions"
          :loading="loading"
          :pagination="{
            ...ADMIN_PAGINATION_PROPS,
            current: query.page,
            pageSize: query.pageSize,
            total: query.total,
            showTotal: (total: number) => `共 ${total} 条`,
          }"
          row-key="id"
          :scroll="{
            scrollToFirstRowOnChange: true,
            x: 980,
            y: 'calc(100vh - 350px)',
          }"
          size="small"
          @change="
            (page: any) => changePage(page.current || 1, page.pageSize || 50)
          "
        >
          <template #emptyText>
            <Empty
              :image="Empty.PRESENTED_IMAGE_SIMPLE"
              description="暂无行政区域数据"
            >
              <Space>
                <Button
                  v-if="can('sysRegion:add')"
                  size="small"
                  type="primary"
                  @click="openCreateRegion"
                >
                  手工新增
                </Button>
                <Button
                  v-if="isSuperAdmin && can('sysRegion:sync')"
                  danger
                  size="small"
                  @click="openSyncModal"
                >
                  同步官方区划
                </Button>
              </Space>
            </Empty>
          </template>
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'name'">
              <div class="region-name">
                <IconifyIcon icon="lucide:map-pin" />
                <span>{{ asRegion(record).name }}</span>
              </div>
            </template>
            <template v-else-if="column.key === 'level'">
              <Tag :color="levelMeta(asRegion(record).level).color">
                {{ levelMeta(asRegion(record).level).label }}
              </Tag>
            </template>
            <template v-else-if="column.key === 'cityCode'">
              {{ asRegion(record).cityCode || '-' }}
            </template>
            <template v-else-if="column.key === 'remark'">
              <span class="remark-text">{{
                asRegion(record).remark || '-'
              }}</span>
            </template>
            <template v-else-if="column.key === 'actions'">
              <Space :size="4">
                <Button
                  v-if="can('sysRegion:update')"
                  size="small"
                  type="link"
                  @click="openEditRegion(asRegion(record))"
                >
                  <template #icon>
                    <IconifyIcon icon="lucide:square-pen" />
                  </template>
                  编辑
                </Button>
                <Button
                  v-if="can('sysRegion:delete')"
                  danger
                  size="small"
                  type="link"
                  @click="confirmDelete(asRegion(record))"
                >
                  <template #icon>
                    <IconifyIcon icon="lucide:trash-2" />
                  </template>
                  删除
                </Button>
              </Space>
            </template>
          </template>
        </Table>
      </div>
    </main>

    <Modal
      v-model:open="modalOpen"
      :body-style="{ padding: '16px 20px' }"
      :footer="null"
      :mask-closable="false"
      :title="modalTitle"
      centered
      class="region-modal"
      destroy-on-close
      :width="560"
      @cancel="formRef?.clearValidate()"
    >
      <Form
        ref="formRef"
        :model="formState"
        :rules="formRules"
        layout="vertical"
      >
        <Form.Item label="上级区域" name="pid">
          <TreeSelect
            v-model:value="formState.pid"
            :dropdown-style="{ maxHeight: '300px', overflow: 'auto' }"
            :load-data="loadTreeNode"
            placeholder="选择上级区域"
            :tree-data="parentTreeData"
            :tree-default-expand-all="false"
          />
        </Form.Item>
        <div class="form-grid">
          <Form.Item label="行政名称" name="name">
            <Input
              v-model:value="formState.name"
              allow-clear
              placeholder="例如：武汉市"
            />
          </Form.Item>
          <Form.Item label="行政代码" name="code">
            <Input
              v-model:value="formState.code"
              allow-clear
              :maxlength="12"
              placeholder="6、9 或 12 位数字"
            />
          </Form.Item>
          <Form.Item label="区号" name="cityCode">
            <Input
              v-model:value="formState.cityCode"
              allow-clear
              :maxlength="6"
              placeholder="可选"
            />
          </Form.Item>
          <Form.Item label="排序" name="orderNo">
            <InputNumber
              v-model:value="formState.orderNo"
              class="w-full"
              :min="0"
            />
          </Form.Item>
        </div>
        <Form.Item label="备注" name="remark">
          <Input.TextArea
            v-model:value="formState.remark"
            :auto-size="{ minRows: 2, maxRows: 3 }"
            :maxlength="128"
            placeholder="可选"
            show-count
          />
        </Form.Item>
      </Form>
      <div class="modal-footer">
        <Space>
          <Button @click="modalOpen = false">取消</Button>
          <Button :loading="submitLoading" type="primary" @click="submitRegion">
            确定
          </Button>
        </Space>
      </div>
    </Modal>

    <Modal
      v-model:open="syncModalOpen"
      :footer="null"
      :mask-closable="false"
      centered
      title="同步全国行政区域"
      :width="520"
    >
      <div class="sync-warning">
        <IconifyIcon icon="lucide:triangle-alert" />
        <div>
          <strong>该操作会替换现有行政区域数据</strong>
          <p>
            数据来源为民政部公开的 2024
            年县以上行政区划代码。系统会先获取并校验全部数据，只有编码和父子关系完整时才在事务中替换；校验失败时原数据保持不变。
          </p>
        </div>
      </div>
      <label class="confirm-label">输入“同步”确认继续</label>
      <Input
        v-model:value="syncConfirmText"
        allow-clear
        placeholder="同步"
        @press-enter="submitSync"
      />
      <div class="modal-footer sync-footer">
        <Space>
          <Button @click="syncModalOpen = false">取消</Button>
          <Button
            danger
            :disabled="syncConfirmText.trim() !== '同步'"
            :loading="syncLoading"
            type="primary"
            @click="submitSync"
          >
            开始同步
          </Button>
        </Space>
      </div>
    </Modal>
  </div>
</template>

<style scoped>
.region-page {
  display: grid;
  grid-template-columns: 280px minmax(0, 1fr);
  gap: 10px;
  min-height: 100%;
  padding: 12px;
  background: hsl(var(--muted) / 35%);
}

.region-nav,
.region-main {
  min-width: 0;
  background: hsl(var(--background));
  border: 1px solid hsl(var(--border) / 72%);
  border-radius: 8px;
}

.region-nav {
  display: flex;
  flex-direction: column;
  min-height: 620px;
  padding: 12px;
}

.region-main {
  padding: 12px;
}

.nav-head,
.panel-head {
  display: flex;
  gap: 12px;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 10px;
}

.nav-title,
.panel-title {
  font-size: 14px;
  font-weight: 650;
  color: hsl(var(--foreground));
}

.nav-subtitle,
.panel-subtitle {
  margin-top: 2px;
  font-size: 12px;
  color: hsl(var(--muted-foreground));
}

.root-button {
  justify-content: flex-start;
  height: 34px;
  padding: 0 8px;
  margin: 8px 0 4px;
  color: hsl(var(--primary));
}

.tree-wrap {
  flex: 1;
  min-height: 0;
  padding: 2px;
  overflow: auto;
  border-top: 1px solid hsl(var(--border) / 60%);
}

.tree-title {
  display: inline-flex;
  gap: 6px;
  align-items: center;
  min-width: 0;
}

.tree-title small {
  font-size: 11px;
  color: hsl(var(--muted-foreground));
}

.query-form {
  margin-bottom: 2px;
}

.region-name {
  display: inline-flex;
  gap: 6px;
  align-items: center;
  font-weight: 550;
}

.region-name svg {
  color: hsl(var(--primary));
}

.remark-text {
  display: block;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.form-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0 16px;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  padding: 10px 20px;
  margin: 14px -20px -16px;
  border-top: 1px solid hsl(var(--border) / 72%);
}

.sync-warning {
  display: flex;
  gap: 10px;
  padding: 10px 12px;
  margin-bottom: 14px;
  color: #613400;
  background: #fff7e6;
  border: 1px solid #ffd591;
  border-radius: 6px;
}

.sync-warning > svg {
  flex: none;
  margin-top: 2px;
  font-size: 18px;
  color: #d46b08;
}

.sync-warning p {
  margin: 4px 0 0;
  font-size: 12px;
  line-height: 1.6;
  color: #874d00;
}

.confirm-label {
  display: block;
  margin-bottom: 6px;
  font-size: 13px;
  font-weight: 550;
}

.sync-footer {
  padding-right: 24px;
  margin-right: -24px;
  margin-bottom: -20px;
  margin-left: -24px;
}

:global(.region-modal) {
  width: min(560px, calc(100vw - 32px)) !important;
}

:global(.region-modal .ant-modal-content) {
  border-radius: 8px;
}

:deep(.ant-form-inline .ant-form-item) {
  margin-bottom: 12px;
}

:deep(.ant-tree .ant-tree-node-content-wrapper) {
  min-width: 0;
  padding: 3px 6px;
  border-radius: 5px;
}

:deep(.ant-tree .ant-tree-treenode) {
  width: 100%;
  padding: 1px 0;
}

:deep(.ant-table-thead > tr > th) {
  white-space: nowrap;
}

@media (max-width: 900px) {
  .region-page {
    grid-template-columns: 1fr;
  }

  .region-nav {
    min-height: 280px;
  }
}

@media (max-width: 620px) {
  .form-grid {
    grid-template-columns: 1fr;
  }
}
</style>
