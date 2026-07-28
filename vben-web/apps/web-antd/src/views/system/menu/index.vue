<script setup lang="ts">
import type { FormInstance, TableColumnsType, TreeProps } from 'ant-design-vue';
import type { Rule } from 'ant-design-vue/es/form';

import type { SaveMenuParams, SysMenuRecord, SysTenantOption } from '#/api';

import { computed, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';
import { IconifyIcon } from '@vben/icons';
import { useUserStore } from '@vben/stores';

import {
  Button,
  Col,
  Descriptions,
  Form,
  Input,
  InputNumber,
  message,
  Modal,
  Popover,
  Radio,
  Row,
  Select,
  Space,
  Switch,
  Table,
  Tag,
  Tooltip,
  TreeSelect,
} from 'ant-design-vue';

import {
  addMenuApi,
  deleteMenuApi,
  getTenantListApi,
  listMenusApi,
  updateMenuApi,
} from '#/api';

defineOptions({ name: 'AdminNetSystemMenu' });

type MenuFormState = Partial<SaveMenuParams> & { id?: number };

const MENU_TYPE_DIR = 1;
const MENU_TYPE_MENU = 2;
const MENU_TYPE_BUTTON = 3;
const ENABLED = 1;
const DISABLED = 2;
const SUPER_ADMIN_ACCOUNT = 999;

const { hasAccessByCodes } = useAccess();
const userStore = useUserStore();

const loading = ref(false);
const optionLoading = ref(false);
const submitLoading = ref(false);
const modalOpen = ref(false);
const modalTitle = ref('新增菜单');
const menuFormRef = ref<FormInstance>();

const menus = ref<SysMenuRecord[]>([]);
const allMenus = ref<SysMenuRecord[]>([]);
const tenantList = ref<SysTenantOption[]>([]);
const expandedRowKeys = ref<Array<number | string>>([]);
const formState = reactive<MenuFormState>({});

const query = reactive({
  tenantId: undefined as number | undefined,
  title: '',
  type: undefined as number | undefined,
});

const menuTypeOptions = [
  { label: '目录', value: MENU_TYPE_DIR },
  { label: '菜单', value: MENU_TYPE_MENU },
  { label: '按钮', value: MENU_TYPE_BUTTON },
];

const statusOptions = [
  { label: '启用', value: ENABLED },
  { label: '禁用', value: DISABLED },
];

const commonIconOptions = [
  'ele-Menu',
  'ele-Setting',
  'ele-User',
  'ele-Folder',
  'ele-Document',
  'ele-Bell',
  'ele-Printer',
  'lucide:layout-dashboard',
  'lucide:shield-check',
  'lucide:database',
].map((value) => ({ label: value, value }));

const columns: TableColumnsType<SysMenuRecord> = [
  { dataIndex: 'title', key: 'title', title: '菜单名称', width: 230 },
  { key: 'type', title: '类型', width: 82 },
  { dataIndex: 'path', key: 'path', title: '路由路径', width: 180 },
  { dataIndex: 'component', key: 'component', title: '组件路径', width: 210 },
  { dataIndex: 'permission', key: 'permission', title: '权限标识', width: 190 },
  { dataIndex: 'orderNo', key: 'orderNo', title: '排序', width: 80 },
  { key: 'status', title: '状态', width: 82 },
  { key: 'modifyRecord', title: '修改记录', width: 116 },
  { key: 'actions', fixed: 'right', title: '操作', width: 188 },
];

const formRules: Record<string, Rule[]> = {
  outLink: [
    {
      validator: async (_rule, value) => {
        const link = String(value || '').trim();
        if (!link && !formState.isIframe) return;
        if (!link) throw new Error('内嵌页面必须填写链接地址');
        try {
          const url = new URL(link);
          if (
            !['http:', 'https:'].includes(url.protocol) ||
            url.username ||
            url.password
          ) {
            throw new Error('unsupported link');
          }
        } catch {
          throw new Error('请输入不含账号密码的 http/https 完整地址');
        }
      },
    },
  ],
  permission: [
    {
      validator: async (_rule, value) => {
        if (formState.type !== MENU_TYPE_BUTTON) {
          return;
        }
        if (!value) {
          throw new Error('请输入权限标识');
        }
        if (!String(value).includes(':')) {
          throw new Error('权限标识需包含冒号，例如 sysMenu:add');
        }
      },
    },
  ],
  title: [
    {
      message: '请输入菜单名称',
      required: true,
      trigger: 'blur',
      type: 'string',
    },
  ],
  type: [
    {
      message: '请选择菜单类型',
      required: true,
      trigger: 'change',
      type: 'number',
    },
  ],
};

const tenantOptions = computed(() =>
  tenantList.value.map((item) => ({
    label: item.host ? `${item.label} (${item.host})` : item.label,
    value: item.value,
  })),
);

const isSuperAdmin = computed(
  () =>
    Number((userStore.userInfo as any)?.accountType) === SUPER_ADMIN_ACCOUNT,
);

const isRouteMenu = computed(
  () => formState.type === MENU_TYPE_DIR || formState.type === MENU_TYPE_MENU,
);

const isButtonMenu = computed(() => formState.type === MENU_TYPE_BUTTON);

const parentMenuTreeData = computed<TreeProps['treeData']>(() => [
  {
    key: 0,
    title: '根节点',
    value: 0,
  },
  ...(toParentTreeData(allMenus.value, formState.id) ?? []),
]);

function can(code: string) {
  return hasAccessByCodes([code]);
}

function asMenuRecord(record: unknown) {
  return record as SysMenuRecord;
}

function getValueText(value?: null | number | string) {
  return value === undefined || value === null || value === '' ? '无' : value;
}

function getAllMenuKeys(items: SysMenuRecord[] = []): Array<number | string> {
  return items.flatMap((item) => [
    item.id,
    ...getAllMenuKeys(item.children ?? []),
  ]);
}

function getRootMenuKeys(items: SysMenuRecord[] = []): Array<number | string> {
  return items.map((item) => item.id);
}

function getMenuTypeMeta(type?: number) {
  return (
    {
      [MENU_TYPE_DIR]: {
        color: 'blue',
        icon: 'lucide:folder',
        label: '目录',
      },
      [MENU_TYPE_MENU]: {
        color: 'green',
        icon: 'lucide:file-text',
        label: '菜单',
      },
      [MENU_TYPE_BUTTON]: {
        color: 'purple',
        icon: 'lucide:mouse-pointer-click',
        label: '按钮',
      },
    }[Number(type)] ?? {
      color: 'default',
      icon: 'lucide:circle',
      label: `类型 ${type ?? '-'}`,
    }
  );
}

function getStatusMeta(status?: number) {
  return status === ENABLED
    ? { color: 'success', label: '启用' }
    : { color: 'default', label: '禁用' };
}

function normalizeIcon(icon?: string) {
  const iconMap: Record<string, string> = {
    'ele-Bell': 'lucide:bell',
    'ele-Document': 'lucide:file-text',
    'ele-Folder': 'lucide:folder',
    'ele-Menu': 'lucide:menu',
    'ele-Printer': 'lucide:printer',
    'ele-Setting': 'lucide:settings',
    'ele-User': 'lucide:user',
  };
  if (!icon) {
    return 'lucide:circle';
  }
  return iconMap[icon] ?? icon.replace(/^ele-/, 'lucide:');
}

function toParentTreeData(
  items: SysMenuRecord[] = [],
  excludeId?: number,
): TreeProps['treeData'] {
  return items
    .filter((item) => item.id !== excludeId && item.type !== MENU_TYPE_BUTTON)
    .map((item) => ({
      children: toParentTreeData(item.children, excludeId),
      key: item.id,
      title: item.title,
      value: item.id,
    }));
}

function resetFormState(values: MenuFormState) {
  for (const key of Object.keys(formState)) {
    delete formState[key as keyof MenuFormState];
  }
  Object.assign(formState, values);
}

function makeDefaultMenu(): MenuFormState {
  return {
    component: '',
    icon: 'ele-Menu',
    isAffix: false,
    isHide: false,
    isIframe: false,
    isKeepAlive: true,
    orderNo: 100,
    pid: 0,
    status: ENABLED,
    tenantId: query.tenantId,
    title: '',
    type: MENU_TYPE_MENU,
  };
}

function sanitizePayload(payload: SaveMenuParams & { id?: number }) {
  if (payload.type === MENU_TYPE_BUTTON) {
    payload.name = undefined;
    payload.path = undefined;
    payload.component = undefined;
    payload.icon = undefined;
    payload.redirect = undefined;
    payload.outLink = undefined;
    payload.isHide = false;
    payload.isKeepAlive = true;
    payload.isAffix = false;
    payload.isIframe = false;
  } else {
    payload.permission = undefined;
  }
  payload.pid = payload.pid ?? 0;
  payload.orderNo = payload.orderNo ?? 100;
  payload.status = payload.status ?? ENABLED;
  return payload;
}

async function loadTenants() {
  if (!isSuperAdmin.value) {
    return;
  }
  optionLoading.value = true;
  try {
    tenantList.value = await getTenantListApi();
    if (!query.tenantId && tenantList.value[0]?.value) {
      query.tenantId = tenantList.value[0].value;
    }
  } finally {
    optionLoading.value = false;
  }
}

async function loadMenus() {
  if (!can('sysMenu:list')) {
    return;
  }
  loading.value = true;
  try {
    const data = await listMenusApi({
      tenantId: query.tenantId,
      title: query.title || undefined,
      type: query.type,
    });
    menus.value = data;
    expandedRowKeys.value =
      query.title || query.type ? getAllMenuKeys(data) : getRootMenuKeys(data);
  } finally {
    loading.value = false;
  }
}

async function loadAllMenus() {
  allMenus.value = await listMenusApi({ tenantId: query.tenantId });
}

async function handleSearch() {
  await loadMenus();
}

async function resetQuery() {
  query.title = '';
  query.type = undefined;
  await loadMenus();
}

async function handleTenantChange() {
  await loadMenus();
}

function expandAllRows() {
  expandedRowKeys.value = getAllMenuKeys(menus.value);
}

function collapseAllRows() {
  expandedRowKeys.value = [];
}

async function refreshMenus() {
  await loadMenus();
  message.success('菜单列表已刷新');
}

async function openCreateMenu() {
  modalTitle.value = '新增菜单';
  resetFormState(makeDefaultMenu());
  await loadAllMenus();
  modalOpen.value = true;
}

async function openEditMenu(record: SysMenuRecord) {
  modalTitle.value = '编辑菜单';
  resetFormState({
    ...record,
    orderNo: record.orderNo ?? 100,
    status: record.status ?? ENABLED,
  });
  await loadAllMenus();
  modalOpen.value = true;
}

async function openCopyMenu(record: SysMenuRecord) {
  modalTitle.value = '复制菜单';
  resetFormState({
    ...record,
    id: undefined,
    title: '',
  });
  await loadAllMenus();
  modalOpen.value = true;
}

async function submitMenu() {
  await menuFormRef.value?.validate();
  submitLoading.value = true;
  try {
    const payload = sanitizePayload({ ...formState } as SaveMenuParams & {
      id?: number;
    });
    if (payload.id && payload.id > 0) {
      await updateMenuApi(payload as SaveMenuParams & { id: number });
      message.success('菜单已更新');
    } else {
      await addMenuApi(payload);
      message.success('菜单已新增');
    }
    modalOpen.value = false;
    await loadMenus();
  } finally {
    submitLoading.value = false;
  }
}

function confirmDelete(record: SysMenuRecord) {
  Modal.confirm({
    centered: true,
    content: `确定删除菜单「${record.title}」吗？子菜单和按钮会一起删除。`,
    okButtonProps: { danger: true },
    okText: '删除',
    title: '删除确认',
    async onOk() {
      await deleteMenuApi(record.id);
      message.success('菜单已删除');
      await loadMenus();
    },
  });
}

onMounted(async () => {
  await loadTenants();
  await loadMenus();
});
</script>

<template>
  <div class="menu-page">
    <section class="query-panel">
      <Form :model="query" layout="inline">
        <Form.Item v-if="isSuperAdmin" label="租户">
          <Select
            v-model:value="query.tenantId"
            :loading="optionLoading"
            :options="tenantOptions"
            class="tenant-query"
            placeholder="租户"
            @change="handleTenantChange"
          />
        </Form.Item>
        <Form.Item label="菜单名称">
          <Input
            v-model:value="query.title"
            allow-clear
            placeholder="菜单名称"
            @press-enter="handleSearch"
          />
        </Form.Item>
        <Form.Item label="类型">
          <Select
            v-model:value="query.type"
            :options="menuTypeOptions"
            allow-clear
            class="type-query"
            placeholder="类型"
          />
        </Form.Item>
        <Form.Item>
          <Space :size="8" wrap>
            <Button
              v-if="can('sysMenu:list')"
              type="primary"
              @click="handleSearch"
            >
              <template #icon>
                <IconifyIcon icon="lucide:search" />
              </template>
              查询
            </Button>
            <Button @click="resetQuery">
              <template #icon>
                <IconifyIcon icon="lucide:rotate-ccw" />
              </template>
              重置
            </Button>
            <Button
              v-if="can('sysMenu:add')"
              type="primary"
              @click="openCreateMenu"
            >
              <template #icon>
                <IconifyIcon icon="lucide:plus" />
              </template>
              新增
            </Button>
          </Space>
        </Form.Item>
      </Form>
    </section>

    <section class="table-panel">
      <div class="table-tools">
        <div>
          <div class="table-title">菜单树</div>
          <div class="table-subtitle">默认仅展开根层，查询时自动展开结果</div>
        </div>
        <Space :size="6" wrap>
          <Button size="small" @click="expandAllRows">
            <template #icon>
              <IconifyIcon icon="lucide:chevrons-down" />
            </template>
            展开
          </Button>
          <Button size="small" @click="collapseAllRows">
            <template #icon>
              <IconifyIcon icon="lucide:chevrons-up" />
            </template>
            折叠
          </Button>
          <Button size="small" :loading="loading" @click="refreshMenus">
            <template #icon>
              <IconifyIcon icon="lucide:refresh-cw" />
            </template>
            刷新
          </Button>
        </Space>
      </div>

      <Table
        v-model:expanded-row-keys="expandedRowKeys"
        :columns="columns"
        :data-source="menus"
        :loading="loading"
        :pagination="false"
        :scroll="{ x: 1280 }"
        row-key="id"
        size="small"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'title'">
            <span class="menu-title-cell">
              <span class="menu-icon-wrap">
                <IconifyIcon
                  :icon="normalizeIcon(asMenuRecord(record).icon)"
                  class="menu-icon"
                />
              </span>
              <span class="menu-title-text">
                {{ asMenuRecord(record).title }}
              </span>
            </span>
          </template>
          <template v-else-if="column.key === 'type'">
            <Tag :color="getMenuTypeMeta(asMenuRecord(record).type).color">
              {{ getMenuTypeMeta(asMenuRecord(record).type).label }}
            </Tag>
          </template>
          <template v-else-if="column.key === 'status'">
            <Tag :color="getStatusMeta(asMenuRecord(record).status).color">
              {{ getStatusMeta(asMenuRecord(record).status).label }}
            </Tag>
          </template>
          <template v-else-if="column.key === 'modifyRecord'">
            <Popover
              overlay-class-name="menu-record-popover"
              placement="bottom"
              trigger="hover"
            >
              <template #content>
                <Descriptions
                  :column="2"
                  bordered
                  class="modify-record"
                  layout="vertical"
                  size="small"
                >
                  <Descriptions.Item label="创建者">
                    {{ getValueText(asMenuRecord(record).createUserName) }}
                  </Descriptions.Item>
                  <Descriptions.Item label="创建时间">
                    {{ getValueText(asMenuRecord(record).createTime) }}
                  </Descriptions.Item>
                  <Descriptions.Item label="修改者">
                    {{ getValueText(asMenuRecord(record).updateUserName) }}
                  </Descriptions.Item>
                  <Descriptions.Item label="修改时间">
                    {{ getValueText(asMenuRecord(record).updateTime) }}
                  </Descriptions.Item>
                  <Descriptions.Item label="备注" :span="2">
                    {{ getValueText(asMenuRecord(record).remark) }}
                  </Descriptions.Item>
                </Descriptions>
              </template>
              <Button size="small" type="link">
                <template #icon>
                  <IconifyIcon icon="lucide:info" />
                </template>
                详情
              </Button>
            </Popover>
          </template>
          <template v-else-if="column.key === 'actions'">
            <Space :size="4" wrap>
              <Tooltip title="编辑">
                <Button
                  v-if="can('sysMenu:update')"
                  size="small"
                  type="link"
                  @click="openEditMenu(asMenuRecord(record))"
                >
                  <template #icon>
                    <IconifyIcon icon="lucide:square-pen" />
                  </template>
                </Button>
              </Tooltip>
              <Tooltip title="删除">
                <Button
                  v-if="can('sysMenu:delete')"
                  danger
                  size="small"
                  type="link"
                  @click="confirmDelete(asMenuRecord(record))"
                >
                  <template #icon>
                    <IconifyIcon icon="lucide:trash-2" />
                  </template>
                </Button>
              </Tooltip>
              <Tooltip title="复制">
                <Button
                  v-if="can('sysMenu:add')"
                  size="small"
                  type="link"
                  @click="openCopyMenu(asMenuRecord(record))"
                >
                  <template #icon>
                    <IconifyIcon icon="lucide:copy" />
                  </template>
                </Button>
              </Tooltip>
            </Space>
          </template>
        </template>
      </Table>
    </section>

    <Modal
      v-model:open="modalOpen"
      :body-style="{
        maxHeight: 'calc(100dvh - 190px)',
        overflowY: 'auto',
        padding: '14px 18px',
      }"
      :mask-closable="false"
      :title="modalTitle"
      centered
      destroy-on-close
      :width="720"
      @cancel="menuFormRef?.clearValidate()"
    >
      <Form
        ref="menuFormRef"
        class="menu-editor-form"
        :model="formState"
        :rules="formRules"
        layout="vertical"
      >
        <Row :gutter="16">
          <Col :span="24">
            <Form.Item label="上级菜单" name="pid">
              <TreeSelect
                v-model:value="formState.pid"
                :tree-data="parentMenuTreeData"
                allow-clear
                show-search
                tree-default-expand-all
              />
            </Form.Item>
          </Col>
          <Col :span="24">
            <Form.Item label="菜单类型" name="type">
              <Radio.Group v-model:value="formState.type">
                <Radio.Button
                  v-for="item in menuTypeOptions"
                  :key="item.value"
                  :value="item.value"
                >
                  {{ item.label }}
                </Radio.Button>
              </Radio.Group>
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="菜单名称" name="title">
              <Input v-model:value="formState.title" allow-clear />
            </Form.Item>
          </Col>
          <Col v-if="isRouteMenu" :span="12">
            <Form.Item label="路由名称" name="name">
              <Input v-model:value="formState.name" allow-clear />
            </Form.Item>
          </Col>
          <Col v-if="isRouteMenu" :span="12">
            <Form.Item label="路由路径" name="path">
              <Input v-model:value="formState.path" allow-clear />
            </Form.Item>
          </Col>
          <Col v-if="isRouteMenu" :span="12">
            <Form.Item label="组件路径" name="component">
              <Input v-model:value="formState.component" allow-clear />
            </Form.Item>
          </Col>
          <Col v-if="isRouteMenu" :span="12">
            <Form.Item label="菜单图标" name="icon">
              <Select
                v-model:value="formState.icon"
                :options="commonIconOptions"
                allow-clear
                show-search
              >
                <template #option="{ value }">
                  <span class="icon-option">
                    <IconifyIcon :icon="normalizeIcon(value)" />
                    <span>{{ value }}</span>
                  </span>
                </template>
              </Select>
            </Form.Item>
          </Col>
          <Col v-if="isRouteMenu" :span="12">
            <Form.Item label="重定向" name="redirect">
              <Input v-model:value="formState.redirect" allow-clear />
            </Form.Item>
          </Col>
          <Col v-if="isRouteMenu" :span="12">
            <Form.Item label="链接地址" name="outLink">
              <Input
                v-model:value="formState.outLink"
                allow-clear
                placeholder="例如 https://docs.example.com"
              />
            </Form.Item>
          </Col>
          <Col v-if="isButtonMenu" :span="12">
            <Form.Item label="权限标识" name="permission">
              <Input
                v-model:value="formState.permission"
                allow-clear
                placeholder="例如 sysMenu:add"
              />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="排序" name="orderNo">
              <InputNumber
                v-model:value="formState.orderNo"
                class="w-full"
                :min="0"
              />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="状态" name="status">
              <Radio.Group
                v-model:value="formState.status"
                :options="statusOptions"
              />
            </Form.Item>
          </Col>
        </Row>

        <div v-if="isRouteMenu" class="switch-grid">
          <div class="switch-item">
            <span>隐藏菜单</span>
            <Switch v-model:checked="formState.isHide" />
          </div>
          <div class="switch-item">
            <span>页面缓存</span>
            <Switch v-model:checked="formState.isKeepAlive" />
          </div>
          <div class="switch-item">
            <span>固定标签</span>
            <Switch v-model:checked="formState.isAffix" />
          </div>
          <div class="switch-item">
            <span>内嵌页面</span>
            <Switch v-model:checked="formState.isIframe" />
          </div>
        </div>

        <Form.Item label="备注" name="remark">
          <Input.TextArea
            v-model:value="formState.remark"
            :auto-size="{ minRows: 2, maxRows: 4 }"
            allow-clear
          />
        </Form.Item>
      </Form>

      <template #footer>
        <Space>
          <Button @click="modalOpen = false">取消</Button>
          <Button :loading="submitLoading" type="primary" @click="submitMenu">
            确定
          </Button>
        </Space>
      </template>
    </Modal>
  </div>
</template>

<style scoped>
.menu-page {
  display: flex;
  flex-direction: column;
  gap: 12px;
  min-height: 100%;
  padding: 12px;
  background: hsl(var(--muted) / 35%);
}

.query-panel,
.table-panel {
  background: hsl(var(--background));
  border: 1px solid hsl(var(--border) / 72%);
  border-radius: 8px;
}

.query-panel {
  padding: 12px 12px 0;
}

.table-panel {
  flex: 1;
  min-width: 0;
  padding: 12px;
}

.tenant-query {
  width: 220px;
}

.type-query {
  width: 140px;
}

.table-tools {
  display: flex;
  gap: 12px;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 10px;
}

.table-title {
  font-size: 14px;
  font-weight: 650;
  color: hsl(var(--foreground));
}

.table-subtitle {
  margin-top: 2px;
  font-size: 12px;
  color: hsl(var(--muted-foreground));
}

.menu-title-cell {
  display: inline-flex;
  gap: 8px;
  align-items: center;
  min-width: 0;
}

.menu-icon-wrap {
  display: inline-flex;
  flex: none;
  align-items: center;
  justify-content: center;
  width: 22px;
  height: 22px;
  background: hsl(var(--background));
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
}

.menu-icon {
  width: 14px;
  height: 14px;
  color: hsl(var(--muted-foreground));
}

.menu-title-text {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.modify-record {
  width: 360px;
}

:global(.menu-record-popover .ant-popover-inner) {
  padding: 8px;
  border: 1px solid hsl(var(--border));
  border-radius: 8px;
  box-shadow:
    0 12px 28px rgb(15 23 42 / 12%),
    0 2px 8px rgb(15 23 42 / 8%);
}

:global(.menu-record-popover .ant-popover-inner-content) {
  padding: 0;
}

:global(.menu-record-popover) {
  z-index: 1060;
}

.switch-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 10px;
  margin: 2px 0 16px;
}

.switch-item {
  display: flex;
  gap: 10px;
  align-items: center;
  justify-content: space-between;
  min-height: 44px;
  padding: 0 12px;
  font-size: 13px;
  font-weight: 500;
  color: hsl(var(--foreground));
  background: hsl(var(--muted) / 22%);
  border: 1px solid hsl(var(--border) / 72%);
  border-radius: 8px;
}

.icon-option {
  display: inline-flex;
  gap: 8px;
  align-items: center;
}

:global(.ant-modal:has(.menu-editor-form)) {
  max-width: calc(100vw - 32px);
}

.menu-editor-form :deep(.ant-form-item) {
  margin-bottom: 12px;
}

:deep(.ant-form-inline .ant-form-item) {
  margin-bottom: 12px;
}

:deep(.ant-table-thead > tr > th) {
  white-space: nowrap;
}

:deep(.ant-table-row-expand-icon-cell) {
  width: 42px;
}

@media (max-width: 900px) {
  .tenant-query,
  .type-query {
    width: 100%;
  }

  .table-tools {
    flex-direction: column;
    align-items: flex-start;
  }

  .switch-grid {
    grid-template-columns: 1fr;
  }
}
</style>
