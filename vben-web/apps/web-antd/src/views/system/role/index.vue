<script setup lang="ts">
import type { FormInstance, TableColumnsType, TreeProps } from 'ant-design-vue';
import type { Rule } from 'ant-design-vue/es/form';

import type {
  SaveRoleParams,
  SysMenuTree,
  SysOrg,
  SysRoleRecord,
  SysTenantOption,
} from '#/api';

import { computed, onMounted, reactive, ref, watch } from 'vue';

import { useAccess } from '@vben/access';
import { IconifyIcon } from '@vben/icons';
import { useUserStore } from '@vben/stores';

import {
  Button,
  Col,
  Descriptions,
  Empty,
  Form,
  Input,
  InputNumber,
  message,
  Modal,
  Pagination,
  Popover,
  Radio,
  Row,
  Select,
  Space,
  Switch,
  Table,
  Tag,
  Tooltip,
  Tree,
} from 'ant-design-vue';

import {
  addRoleApi,
  deleteRoleApi,
  getMenuListApi,
  getOrgListApi,
  getRoleOwnMenuIdsApi,
  getRoleOwnOrgIdsApi,
  getTenantListApi,
  grantRoleDataScopeApi,
  pageRolesApi,
  setRoleStatusApi,
  updateRoleApi,
} from '#/api';

defineOptions({ name: 'AdminNetSystemRole' });

type RoleFormState = Partial<SaveRoleParams> & { id?: number };

const ENABLED = 1;
const DISABLED = 2;
const DATA_SCOPE_ALL = 1;
const DATA_SCOPE_DEPT_WITH_CHILDREN = 2;
const DATA_SCOPE_DEPT = 3;
const DATA_SCOPE_SELF = 4;
const DATA_SCOPE_DEFINE = 5;
const SUPER_ADMIN_ACCOUNT = 999;

const { hasAccessByCodes } = useAccess();
const userStore = useUserStore();

const loading = ref(false);
const optionLoading = ref(false);
const submitLoading = ref(false);
const menuLoading = ref(false);
const dataScopeLoading = ref(false);
const dataScopeOrgLoading = ref(false);
const drawerOpen = ref(false);
const dataScopeOpen = ref(false);
const drawerTitle = ref('新增角色');
const roleFormRef = ref<FormInstance>();
const roleFormState = reactive<RoleFormState>({});
const dataScopeRole = ref<SysRoleRecord>();

const roles = ref<SysRoleRecord[]>([]);
const tenantList = ref<SysTenantOption[]>([]);
const menuTree = ref<SysMenuTree[]>([]);
const orgTree = ref<SysOrg[]>([]);
const checkedMenuKeys = ref<Array<number | string>>([]);
const expandedMenuKeys = ref<Array<number | string>>([]);
const checkedOrgKeys = ref<Array<number | string>>([]);
const expandedOrgKeys = ref<Array<number | string>>([]);
const menuFilterText = ref('');

const query = reactive({
  code: '',
  name: '',
  tenantId: undefined as number | undefined,
});

const pagination = reactive({
  page: 1,
  pageSize: 50,
  total: 0,
});

const dataScopeState = reactive({
  dataScope: DATA_SCOPE_DEPT_WITH_CHILDREN,
  id: 0,
  tenantId: undefined as number | undefined,
});

const dataScopeOptions = [
  { label: '全部数据', value: DATA_SCOPE_ALL },
  { label: '本部门及以下数据', value: DATA_SCOPE_DEPT_WITH_CHILDREN },
  { label: '本部门数据', value: DATA_SCOPE_DEPT },
  { label: '仅本人数据', value: DATA_SCOPE_SELF },
  { label: '自定义数据', value: DATA_SCOPE_DEFINE },
];

const columns: TableColumnsType<SysRoleRecord> = [
  { key: 'index', title: '序号', width: 64 },
  { dataIndex: 'name', key: 'name', title: '角色名称', width: 180 },
  { dataIndex: 'code', key: 'code', title: '角色编码', width: 180 },
  { key: 'dataScope', title: '数据范围', width: 150 },
  { dataIndex: 'orderNo', key: 'orderNo', title: '排序', width: 80 },
  { key: 'status', title: '状态', width: 88 },
  { key: 'modifyRecord', title: '修改记录', width: 116 },
  { key: 'actions', fixed: 'right', title: '操作', width: 232 },
];

const roleRules: Record<string, Rule[]> = {
  code: [
    { message: '请输入角色编码', required: true, trigger: 'blur', type: 'string' },
  ],
  name: [
    { message: '请输入角色名称', required: true, trigger: 'blur', type: 'string' },
  ],
};

const tenantOptions = computed(() =>
  tenantList.value.map((item) => ({
    label: item.host ? `${item.label} (${item.host})` : item.label,
    value: item.value,
  })),
);

const isSuperAdmin = computed(
  () => Number((userStore.userInfo as any)?.accountType) === SUPER_ADMIN_ACCOUNT,
);

function can(code: string) {
  return hasAccessByCodes([code]);
}

function asRoleRecord(record: unknown) {
  return record as SysRoleRecord;
}

function getValueText(value?: null | number | string) {
  return value === undefined || value === null || value === '' ? '无' : value;
}

function getDataScopeMeta(value?: number) {
  return (
    {
      [DATA_SCOPE_ALL]: { color: 'blue', label: '全部数据' },
      [DATA_SCOPE_DEPT_WITH_CHILDREN]: {
        color: 'cyan',
        label: '本部门及以下',
      },
      [DATA_SCOPE_DEPT]: { color: 'green', label: '本部门' },
      [DATA_SCOPE_SELF]: { color: 'default', label: '仅本人' },
      [DATA_SCOPE_DEFINE]: { color: 'purple', label: '自定义' },
    }[Number(value)] ?? { color: 'default', label: `范围 ${value ?? '-'}` }
  );
}

function getAllMenuKeys(items: SysMenuTree[] = []): Array<number | string> {
  return items.flatMap((item) => [
    item.id,
    ...getAllMenuKeys(item.children ?? []),
  ]);
}

function getRootMenuKeys(items: SysMenuTree[] = []): Array<number | string> {
  return items.map((item) => item.id);
}

function getAllOrgKeys(items: SysOrg[] = []): Array<number | string> {
  return items.flatMap((item) => [
    item.id,
    ...getAllOrgKeys(item.children ?? []),
  ]);
}

function getMenuIcon(type?: number) {
  if (type === 1) {
    return 'lucide:folder';
  }
  if (type === 3) {
    return 'lucide:mouse-pointer-click';
  }
  return 'lucide:file-text';
}

function getOrgIcon(level: number) {
  if (level <= 1) {
    return 'lucide:building-2';
  }
  if (level === 2) {
    return 'lucide:house';
  }
  return 'lucide:tag';
}

function filterMenuTree(items: SysMenuTree[] = [], keyword = ''): SysMenuTree[] {
  const normalizedKeyword = keyword.trim().toLowerCase();
  if (!normalizedKeyword) {
    return items;
  }

  return items
    .map((item) => {
      const children = filterMenuTree(item.children ?? [], normalizedKeyword);
      const matched = [item.title, item.name, item.code, item.path]
        .filter(Boolean)
        .some((value) =>
          String(value).toLowerCase().includes(normalizedKeyword),
        );

      if (!matched && children.length === 0) {
        return undefined;
      }
      return { ...item, children };
    })
    .filter(Boolean) as SysMenuTree[];
}

function toMenuTreeData(items: SysMenuTree[] = []): TreeProps['treeData'] {
  return items.map((item) => ({
    children: toMenuTreeData(item.children),
    icon: getMenuIcon(item.type),
    key: item.id,
    title: item.title || item.name || item.code || `菜单 ${item.id}`,
    type: item.type,
  }));
}

function toOrgTreeData(items: SysOrg[] = [], level = 1): TreeProps['treeData'] {
  return items.map((item) => ({
    children: toOrgTreeData(item.children, level + 1),
    icon: getOrgIcon(level),
    key: item.id,
    level,
    title: item.name,
  }));
}

const filteredMenuTree = computed(() =>
  filterMenuTree(menuTree.value, menuFilterText.value),
);

const menuTreeData = computed<TreeProps['treeData']>(() =>
  toMenuTreeData(filteredMenuTree.value),
);

const orgTreeData = computed<TreeProps['treeData']>(() =>
  toOrgTreeData(orgTree.value),
);

const checkedMenuCount = computed(() => checkedMenuKeys.value.length);

function resetRoleFormState(values: RoleFormState) {
  for (const key of Object.keys(roleFormState)) {
    delete roleFormState[key as keyof RoleFormState];
  }
  Object.assign(roleFormState, values);
}

function makeDefaultRole(): RoleFormState {
  return {
    code: '',
    menuIdList: [],
    name: '',
    orderNo: 100,
    remark: '',
    status: ENABLED,
    tenantId: query.tenantId,
  };
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

async function loadRoles() {
  if (!can('sysRole:page')) {
    return;
  }
  loading.value = true;
  try {
    const data = await pageRolesApi({
      code: query.code || undefined,
      name: query.name || undefined,
      page: pagination.page,
      pageSize: pagination.pageSize,
      tenantId: query.tenantId,
    });
    roles.value = data.items ?? [];
    pagination.total = data.total ?? 0;
  } finally {
    loading.value = false;
  }
}

async function loadMenuTree(tenantId?: number) {
  menuLoading.value = true;
  try {
    const data = await getMenuListApi({ tenantId });
    menuTree.value = data;
    expandedMenuKeys.value = menuFilterText.value.trim()
      ? getAllMenuKeys(filteredMenuTree.value)
      : getRootMenuKeys(data);
  } finally {
    menuLoading.value = false;
  }
}

async function loadOrgTree(tenantId?: number) {
  dataScopeOrgLoading.value = true;
  try {
    const data = await getOrgListApi({ id: 0, tenantId });
    orgTree.value = data;
    expandedOrgKeys.value = getAllOrgKeys(data);
  } finally {
    dataScopeOrgLoading.value = false;
  }
}

async function handleSearch() {
  pagination.page = 1;
  await loadRoles();
}

async function resetQuery() {
  query.code = '';
  query.name = '';
  await handleSearch();
}

async function handleTenantChange() {
  pagination.page = 1;
  await loadRoles();
}

async function handlePageChange(page: number, pageSize: number) {
  pagination.page = page;
  pagination.pageSize = pageSize;
  await loadRoles();
}

function handleMenuCheck(keys: unknown) {
  checkedMenuKeys.value = Array.isArray(keys)
    ? (keys as Array<number | string>)
    : ((keys as { checked?: Array<number | string> })?.checked ?? []);
}

function expandAllMenus() {
  expandedMenuKeys.value = getAllMenuKeys(filteredMenuTree.value);
}

function collapseAllMenus() {
  expandedMenuKeys.value = [];
}

async function refreshMenuTree() {
  await loadMenuTree(roleFormState.tenantId);
  message.success('菜单权限已刷新');
}

function handleOrgCheck(keys: unknown) {
  checkedOrgKeys.value = Array.isArray(keys)
    ? (keys as Array<number | string>)
    : ((keys as { checked?: Array<number | string> })?.checked ?? []);
}

async function openCreateRole() {
  drawerTitle.value = '新增角色';
  resetRoleFormState(makeDefaultRole());
  checkedMenuKeys.value = [];
  drawerOpen.value = true;
  await loadMenuTree(roleFormState.tenantId);
}

async function openEditRole(record: SysRoleRecord) {
  drawerTitle.value = '编辑角色';
  resetRoleFormState({
    ...record,
    menuIdList: [],
    orderNo: record.orderNo ?? 100,
    status: record.status ?? ENABLED,
  });
  checkedMenuKeys.value = [];
  drawerOpen.value = true;
  const [menuIds] = await Promise.all([
    getRoleOwnMenuIdsApi(record.id),
    loadMenuTree(record.tenantId),
  ]);
  checkedMenuKeys.value = menuIds ?? [];
}

async function submitRole() {
  await roleFormRef.value?.validate();
  submitLoading.value = true;
  try {
    const menuIdList = checkedMenuKeys.value
      .map(Number)
      .filter((item) => !Number.isNaN(item));
    const payload = {
      ...roleFormState,
      menuIdList,
      orderNo: roleFormState.orderNo ?? 100,
      status: roleFormState.status ?? ENABLED,
    } as SaveRoleParams & { id?: number };

    if (payload.id) {
      await updateRoleApi(payload as SaveRoleParams & { id: number });
      message.success('角色已更新');
    } else {
      await addRoleApi(payload);
      message.success('角色已新增');
    }
    drawerOpen.value = false;
    await loadRoles();
  } finally {
    submitLoading.value = false;
  }
}

function confirmDelete(record: SysRoleRecord) {
  Modal.confirm({
    centered: true,
    content: `确定删除角色「${record.name}」吗？`,
    okButtonProps: { danger: true },
    okText: '删除',
    title: '删除确认',
    async onOk() {
      await deleteRoleApi(record.id);
      message.success('角色已删除');
      await loadRoles();
    },
  });
}

async function changeStatus(record: SysRoleRecord, checked: boolean) {
  const previous = record.status;
  const nextStatus = checked ? ENABLED : DISABLED;
  record.status = nextStatus;
  try {
    await setRoleStatusApi(record.id, nextStatus);
    message.success('角色状态已更新');
  } catch (error) {
    record.status = previous;
    throw error;
  }
}

async function openDataScope(record: SysRoleRecord) {
  dataScopeRole.value = record;
  dataScopeState.id = record.id;
  dataScopeState.tenantId = record.tenantId;
  dataScopeState.dataScope = record.dataScope ?? DATA_SCOPE_DEPT_WITH_CHILDREN;
  checkedOrgKeys.value = [];
  dataScopeOpen.value = true;

  dataScopeLoading.value = true;
  try {
    const [orgIds] = await Promise.all([
      getRoleOwnOrgIdsApi(record.id),
      loadOrgTree(record.tenantId),
    ]);
    checkedOrgKeys.value = orgIds ?? [];
  } finally {
    dataScopeLoading.value = false;
  }
}

async function submitDataScope() {
  dataScopeLoading.value = true;
  try {
    await grantRoleDataScopeApi({
      dataScope: dataScopeState.dataScope,
      id: dataScopeState.id,
      orgIdList:
        dataScopeState.dataScope === DATA_SCOPE_DEFINE
          ? checkedOrgKeys.value
              .map(Number)
              .filter((item) => !Number.isNaN(item))
          : [],
      tenantId: dataScopeState.tenantId,
    });
    message.success('数据范围已更新');
    dataScopeOpen.value = false;
    await loadRoles();
  } finally {
    dataScopeLoading.value = false;
  }
}

watch(
  () => dataScopeState.dataScope,
  async (value) => {
    if (value === DATA_SCOPE_DEFINE && orgTree.value.length === 0) {
      await loadOrgTree(dataScopeState.tenantId);
    }
  },
);

watch(menuFilterText, (value) => {
  expandedMenuKeys.value = value.trim()
    ? getAllMenuKeys(filteredMenuTree.value)
    : getRootMenuKeys(menuTree.value);
});

onMounted(async () => {
  await loadTenants();
  await loadRoles();
});
</script>

<template>
  <div class="role-page">
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
        <Form.Item label="角色名称">
          <Input
            v-model:value="query.name"
            allow-clear
            placeholder="角色名称"
            @press-enter="handleSearch"
          />
        </Form.Item>
        <Form.Item label="角色编码">
          <Input
            v-model:value="query.code"
            allow-clear
            placeholder="角色编码"
            @press-enter="handleSearch"
          />
        </Form.Item>
        <Form.Item>
          <Space :size="8">
            <Button
              v-if="can('sysRole:page')"
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
              v-if="can('sysRole:add')"
              type="primary"
              @click="openCreateRole"
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
      <Table
        :columns="columns"
        :data-source="roles"
        :loading="loading"
        :pagination="false"
        :scroll="{ x: 1120 }"
        row-key="id"
        size="small"
      >
        <template #bodyCell="{ column, index, record }">
          <template v-if="column.key === 'index'">
            {{ (pagination.page - 1) * pagination.pageSize + index + 1 }}
          </template>
          <template v-else-if="column.key === 'dataScope'">
            <Tag :color="getDataScopeMeta(record.dataScope).color">
              {{ getDataScopeMeta(record.dataScope).label }}
            </Tag>
          </template>
          <template v-else-if="column.key === 'status'">
            <Switch
              :checked="record.status === ENABLED"
              :disabled="!can('sysRole:setStatus')"
              size="small"
              @change="(checked) => changeStatus(asRoleRecord(record), Boolean(checked))"
            />
          </template>
          <template v-else-if="column.key === 'modifyRecord'">
            <Popover placement="bottom" trigger="hover">
              <template #content>
                <Descriptions
                  :column="2"
                  bordered
                  class="modify-record"
                  layout="vertical"
                  size="small"
                >
                  <Descriptions.Item label="创建者">
                    <Tag>{{ getValueText(record.createUserName) }}</Tag>
                  </Descriptions.Item>
                  <Descriptions.Item label="创建时间">
                    <Tag>{{ getValueText(record.createTime) }}</Tag>
                  </Descriptions.Item>
                  <Descriptions.Item label="修改者">
                    <Tag>{{ getValueText(record.updateUserName) }}</Tag>
                  </Descriptions.Item>
                  <Descriptions.Item label="修改时间">
                    <Tag>{{ getValueText(record.updateTime) }}</Tag>
                  </Descriptions.Item>
                  <Descriptions.Item label="备注" :span="2">
                    {{ getValueText(record.remark) }}
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
              <Button
                v-if="can('sysRole:grantDataScope')"
                size="small"
                type="link"
                @click="openDataScope(asRoleRecord(record))"
              >
                <template #icon>
                  <IconifyIcon icon="lucide:building-2" />
                </template>
                数据范围
              </Button>
              <Tooltip title="编辑">
                <Button
                  v-if="can('sysRole:update')"
                  size="small"
                  type="link"
                  @click="openEditRole(asRoleRecord(record))"
                >
                  <template #icon>
                    <IconifyIcon icon="lucide:square-pen" />
                  </template>
                </Button>
              </Tooltip>
              <Tooltip title="删除">
                <Button
                  v-if="can('sysRole:delete')"
                  danger
                  size="small"
                  type="link"
                  @click="confirmDelete(asRoleRecord(record))"
                >
                  <template #icon>
                    <IconifyIcon icon="lucide:trash-2" />
                  </template>
                </Button>
              </Tooltip>
            </Space>
          </template>
        </template>
      </Table>

      <div class="table-footer">
        <Pagination
          v-model:current="pagination.page"
          v-model:page-size="pagination.pageSize"
          :page-size-options="['10', '20', '50', '100']"
          :show-total="(total) => `共 ${total} 条`"
          :total="pagination.total"
          show-quick-jumper
          show-size-changer
          size="small"
          @change="handlePageChange"
          @show-size-change="handlePageChange"
        />
      </div>
    </section>

    <Modal
      v-model:open="drawerOpen"
      :body-style="{ maxHeight: '72vh', overflowY: 'auto', padding: '16px 20px' }"
      :footer="null"
      :mask-closable="false"
      :title="drawerTitle"
      centered
      class="role-modal"
      destroy-on-close
      width="760"
      @cancel="roleFormRef?.clearValidate()"
    >
      <Form
        ref="roleFormRef"
        :model="roleFormState"
        :rules="roleRules"
        layout="vertical"
      >
        <Row :gutter="16">
          <Col :span="12">
            <Form.Item label="角色名称" name="name">
              <Input v-model:value="roleFormState.name" allow-clear />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="角色编码" name="code">
              <Input
                v-model:value="roleFormState.code"
                :disabled="roleFormState.code === 'sys_admin' && !!roleFormState.id"
                allow-clear
              />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="排序" name="orderNo">
              <InputNumber
                v-model:value="roleFormState.orderNo"
                class="w-full"
                :min="0"
              />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="状态" name="status">
              <Radio.Group v-model:value="roleFormState.status">
                <Radio :value="ENABLED">启用</Radio>
                <Radio :value="DISABLED">禁用</Radio>
              </Radio.Group>
            </Form.Item>
          </Col>
          <Col :span="24">
            <Form.Item label="备注" name="remark">
              <Input.TextArea
                v-model:value="roleFormState.remark"
                :auto-size="{ minRows: 2, maxRows: 4 }"
                allow-clear
                placeholder="请输入备注内容"
              />
            </Form.Item>
          </Col>
        </Row>

        <div class="permission-head">
          <div>
            <div class="permission-title">菜单权限</div>
            <div class="permission-subtitle">搜索定位菜单，必要时展开后勾选按钮权限</div>
          </div>
          <Tag class="permission-count" color="blue">
            已选 {{ checkedMenuCount }} 项
          </Tag>
        </div>
        <div class="permission-toolbar">
          <Input
            v-model:value="menuFilterText"
            allow-clear
            placeholder="搜索菜单、按钮或编码"
          >
            <template #prefix>
              <IconifyIcon icon="lucide:search" />
            </template>
          </Input>
          <Space :size="6" wrap>
            <Button size="small" @click="expandAllMenus">
              <template #icon>
                <IconifyIcon icon="lucide:chevrons-down" />
              </template>
              展开
            </Button>
            <Button size="small" @click="collapseAllMenus">
              <template #icon>
                <IconifyIcon icon="lucide:chevrons-up" />
              </template>
              折叠
            </Button>
            <Button size="small" :loading="menuLoading" @click="refreshMenuTree">
              <template #icon>
                <IconifyIcon icon="lucide:refresh-cw" />
              </template>
              刷新
            </Button>
          </Space>
        </div>
        <div class="tree-shell permission-tree" :class="{ 'is-loading': menuLoading }">
          <Tree
            v-if="menuTreeData?.length"
            :checked-keys="checkedMenuKeys"
            :expanded-keys="expandedMenuKeys"
            :tree-data="menuTreeData"
            block-node
            checkable
            @check="handleMenuCheck"
            @expand="(keys) => (expandedMenuKeys = keys)"
          >
            <template #title="{ icon, title, type }">
              <span class="tree-node">
                <span class="tree-node-icon-wrap" :class="`type-${type ?? 2}`">
                  <IconifyIcon :icon="icon" class="tree-node-icon" />
                </span>
                <span class="tree-node-title">{{ title }}</span>
              </span>
            </template>
          </Tree>
          <Empty v-else :image="Empty.PRESENTED_IMAGE_SIMPLE" />
        </div>
      </Form>

      <div class="modal-footer">
        <Space>
          <Button @click="drawerOpen = false">取消</Button>
          <Button :loading="submitLoading" type="primary" @click="submitRole">
            确定
          </Button>
        </Space>
      </div>
    </Modal>

    <Modal
      v-model:open="dataScopeOpen"
      :body-style="{ maxHeight: '68vh', overflowY: 'auto', padding: '16px 20px' }"
      :footer="null"
      :mask-closable="false"
      centered
      destroy-on-close
      class="role-scope-modal"
      title="授权数据范围"
      width="520"
    >
      <div class="scope-summary">
        <div class="scope-summary-label">当前角色</div>
        <div class="scope-summary-value">{{ dataScopeRole?.name }}</div>
      </div>

      <Form :model="dataScopeState" layout="vertical">
        <Form.Item label="数据范围">
          <Select
            v-model:value="dataScopeState.dataScope"
            :options="dataScopeOptions"
          />
        </Form.Item>
      </Form>

      <div
        v-if="dataScopeState.dataScope === DATA_SCOPE_DEFINE"
        class="tree-shell scope-tree"
        :class="{ 'is-loading': dataScopeOrgLoading || dataScopeLoading }"
      >
        <Tree
          v-if="orgTreeData?.length"
          :checked-keys="checkedOrgKeys"
          :expanded-keys="expandedOrgKeys"
          :tree-data="orgTreeData"
          block-node
          checkable
          @check="handleOrgCheck"
          @expand="(keys) => (expandedOrgKeys = keys)"
        >
          <template #title="{ icon, title }">
            <span class="tree-node">
              <span class="tree-node-icon-wrap">
                <IconifyIcon :icon="icon" class="tree-node-icon" />
              </span>
              <span class="tree-node-title">{{ title }}</span>
            </span>
          </template>
        </Tree>
        <Empty v-else :image="Empty.PRESENTED_IMAGE_SIMPLE" />
      </div>
      <div v-else class="scope-hint">
        <IconifyIcon icon="lucide:shield-check" />
        <span>当前数据范围由系统规则自动计算，无需勾选机构。</span>
      </div>

      <div class="modal-footer">
        <Space>
          <Button @click="dataScopeOpen = false">取消</Button>
          <Button
            :loading="dataScopeLoading"
            type="primary"
            @click="submitDataScope"
          >
            确定
          </Button>
        </Space>
      </div>
    </Modal>
  </div>
</template>

<style scoped>
.role-page {
  display: flex;
  min-height: 100%;
  flex-direction: column;
  gap: 12px;
  padding: 12px;
  background: hsl(var(--muted) / 35%);
}

.query-panel,
.table-panel {
  border: 1px solid hsl(var(--border) / 72%);
  border-radius: 8px;
  background: hsl(var(--background));
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

.table-footer {
  display: flex;
  justify-content: flex-end;
  padding-top: 12px;
}

.modify-record {
  width: 360px;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  margin: 18px -20px -16px;
  padding: 12px 20px;
  border-top: 1px solid hsl(var(--border) / 72%);
  background: hsl(var(--background));
}

.permission-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin: 4px 0 8px;
}

.permission-title {
  color: hsl(var(--foreground));
  font-size: 14px;
  font-weight: 650;
}

.permission-subtitle {
  margin-top: 2px;
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.permission-count {
  flex: none;
  margin-inline-end: 0;
}

.permission-toolbar {
  display: grid;
  grid-template-columns: minmax(240px, 1fr) auto;
  gap: 10px;
  margin-bottom: 10px;
}

.tree-shell {
  min-height: 360px;
  padding: 8px;
  border: 1px solid hsl(var(--border) / 75%);
  border-radius: 8px;
  background:
    linear-gradient(180deg, hsl(var(--muted) / 30%), transparent 58px),
    hsl(var(--background));
}

.tree-shell.is-loading {
  opacity: 0.62;
  pointer-events: none;
}

.permission-tree {
  height: 390px;
  min-height: 320px;
  overflow: auto;
}

.scope-tree {
  height: 360px;
  overflow: auto;
}

.tree-node {
  display: flex;
  min-width: 0;
  align-items: center;
  gap: 8px;
  color: hsl(var(--foreground));
  font-size: 13px;
  font-weight: 500;
  line-height: 30px;
}

.tree-node-icon-wrap {
  display: inline-flex;
  width: 22px;
  height: 22px;
  flex: none;
  align-items: center;
  justify-content: center;
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
  background: hsl(var(--background));
}

.tree-node-icon-wrap.type-1 {
  background: hsl(var(--primary) / 9%);
  color: hsl(var(--primary));
}

.tree-node-icon-wrap.type-3 {
  background: hsl(var(--warning) / 12%);
  color: hsl(var(--warning));
}

.tree-node-icon {
  width: 14px;
  height: 14px;
  flex: none;
  color: currentColor;
}

.tree-node-title {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.scope-summary {
  margin-bottom: 14px;
  padding: 12px;
  border: 1px solid hsl(var(--border) / 72%);
  border-radius: 8px;
  background: hsl(var(--muted) / 28%);
}

.scope-summary-label {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.scope-summary-value {
  margin-top: 4px;
  color: hsl(var(--foreground));
  font-size: 15px;
  font-weight: 650;
}

.scope-hint {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 14px;
  border: 1px dashed hsl(var(--border));
  border-radius: 8px;
  color: hsl(var(--muted-foreground));
}

:deep(.ant-form-inline .ant-form-item) {
  margin-bottom: 12px;
}

:deep(.ant-table-thead > tr > th) {
  white-space: nowrap;
}

:deep(.ant-tree) {
  background: transparent;
}

:deep(.ant-tree .ant-tree-treenode) {
  width: 100%;
  padding: 2px 0;
}

:deep(.ant-tree .ant-tree-node-content-wrapper) {
  min-width: 0;
  flex: 1;
  height: 30px;
  padding-inline: 5px 8px;
  border-radius: 8px;
  transition:
    background-color 0.16s ease,
    box-shadow 0.16s ease,
    color 0.16s ease;
}

:deep(.ant-tree .ant-tree-node-content-wrapper:hover) {
  background: hsl(var(--accent) / 72%);
}

:deep(.ant-tree .ant-tree-checkbox) {
  margin-block-start: 7px;
}

:deep(.ant-tree .ant-tree-switcher) {
  width: 18px;
  color: hsl(var(--muted-foreground));
}

:deep(.ant-tree .ant-tree-indent-unit) {
  width: 14px;
}

@media (max-width: 900px) {
  .tenant-query {
    width: 100%;
  }

  .permission-toolbar {
    grid-template-columns: 1fr;
  }

  .permission-tree,
  .scope-tree {
    height: 320px;
  }
}
</style>
