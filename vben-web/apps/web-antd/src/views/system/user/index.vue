<script setup lang="ts">
import type { FormInstance, TableColumnsType, TreeProps } from 'ant-design-vue';
import type { Rule } from 'ant-design-vue/es/form';

import type {
  SaveUserParams,
  SysOrg,
  SysPos,
  SysRole,
  SysTenantOption,
  SysUserExtOrg,
  SysUserRecord,
} from '#/api';

import { computed, onMounted, reactive, ref, watch } from 'vue';

import { useAccess } from '@vben/access';
import { IconifyIcon } from '@vben/icons';

import {
  Avatar,
  Button,
  Col,
  DatePicker,
  Descriptions,
  Divider,
  Drawer,
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
  Tabs,
  Tag,
  Tooltip,
  Transfer,
  Tree,
  TreeSelect,
} from 'ant-design-vue';

import {
  addUserApi,
  deleteUserApi,
  getOrgListApi,
  getPosListApi,
  getRoleListApi,
  getTenantListApi,
  getUserExtOrgsApi,
  getUserRoleIdsApi,
  pageUsersApi,
  resetUserPasswordApi,
  setUserStatusApi,
  unlockUserLoginApi,
  updateUserApi,
} from '#/api';

defineOptions({ name: 'AdminNetSystemUser' });

type UserFormState = Partial<SaveUserParams> & { id?: number };
type LooseUserRecord = Record<string, any>;

const ENABLED = 1;
const DISABLED = 2;
const MEMBER_ACCOUNT = 666;
const NORMAL_ACCOUNT = 777;
const SYS_ADMIN_ACCOUNT = 888;
const SUPER_ADMIN_ACCOUNT = 999;

const { hasAccessByCodes } = useAccess();

const loading = ref(false);
const orgLoading = ref(false);
const optionLoading = ref(false);
const drawerOpen = ref(false);
const drawerTitle = ref('新增用户');
const submitLoading = ref(false);
const activeFormTab = ref('basic');
const orgCommandOpen = ref(false);
const formRef = ref<FormInstance>();

const orgList = ref<SysOrg[]>([]);
const posList = ref<SysPos[]>([]);
const roleList = ref<SysRole[]>([]);
const tenantList = ref<SysTenantOption[]>([]);
const selectedTenantId = ref<number | undefined>();
const orgFilterText = ref('');
const selectedOrgKeys = ref<Array<number | string>>([]);
const expandedOrgKeys = ref<Array<number | string>>([]);
const autoExpandParent = ref(false);

const query = reactive({
  account: '',
  orgId: -1,
  phone: '',
  posName: '',
  realName: '',
  tenantId: undefined as number | undefined,
});

const pagination = reactive({
  page: 1,
  pageSize: 50,
  total: 0,
});

const users = ref<SysUserRecord[]>([]);
const formState = reactive<UserFormState>({});

const accountTypeOptions = [
  { label: '会员', value: MEMBER_ACCOUNT },
  { label: '普通账号', value: NORMAL_ACCOUNT },
  { label: '系统管理员', value: SYS_ADMIN_ACCOUNT },
];

const accountTypeDisplayOptions = [
  ...accountTypeOptions,
  { label: '超级管理员', value: SUPER_ADMIN_ACCOUNT },
];

const cardTypeOptions = [
  { label: '身份证', value: 0 },
  { label: '护照', value: 1 },
  { label: '军官证', value: 2 },
  { label: '其他', value: 9 },
];

const cultureLevelOptions = [
  { label: '小学', value: 1 },
  { label: '初中', value: 2 },
  { label: '高中', value: 3 },
  { label: '中专', value: 4 },
  { label: '技工学校', value: 5 },
  { label: '大专', value: 6 },
  { label: '本科', value: 7 },
  { label: '硕士', value: 8 },
  { label: '博士', value: 9 },
];

const genderOptions = [
  { label: '未知的性别', value: 0 },
  { label: '男性', value: 1 },
  { label: '女性', value: 2 },
  { label: '未说明的性别', value: 9 },
];

const nationOptions = [
  '汉族',
  '蒙古族',
  '回族',
  '藏族',
  '维吾尔族',
  '苗族',
  '彝族',
  '壮族',
  '布依族',
  '朝鲜族',
].map((name) => ({ label: name, value: name }));

const orgMenuItems = [
  { icon: 'lucide:chevrons-down-up', key: 'expandAll', label: '全部展开' },
  { icon: 'lucide:chevrons-up-down', key: 'collapseAll', label: '全部折叠' },
  { icon: 'lucide:corner-up-left', key: 'rootNode', label: '根节点' },
  { icon: 'lucide:refresh-cw', key: 'refresh', label: '刷新' },
];

const columns: TableColumnsType<SysUserRecord> = [
  { key: 'index', title: '序号', width: 64 },
  { key: 'avatar', title: '头像', width: 72 },
  { dataIndex: 'account', key: 'account', title: '账号', width: 132 },
  { dataIndex: 'realName', key: 'realName', title: '姓名', width: 120 },
  { dataIndex: 'phone', key: 'phone', title: '手机号', width: 132 },
  { key: 'accountType', title: '账号类型', width: 112 },
  { dataIndex: 'roleName', key: 'roleName', title: '角色集合', width: 180 },
  { dataIndex: 'orgName', key: 'orgName', title: '所属机构', width: 160 },
  { dataIndex: 'posName', key: 'posName', title: '职位', width: 132 },
  { key: 'status', title: '状态', width: 88 },
  { dataIndex: 'orderNo', key: 'orderNo', title: '排序', width: 80 },
  { key: 'modifyRecord', title: '修改记录', width: 116 },
  { key: 'actions', fixed: 'right', title: '操作', width: 244 },
];

const formRules: Record<string, Rule[]> = {
  account: [
    { message: '请输入账号', required: true, trigger: 'blur', type: 'string' },
  ],
  accountType: [
    { message: '请选择账号类型', required: true, trigger: 'change', type: 'number' },
  ],
  orgId: [
    { message: '请选择所属机构', required: true, trigger: 'change', type: 'number' },
  ],
  phone: [
    { message: '请输入手机号', required: true, trigger: 'blur', type: 'string' },
  ],
  posId: [
    { message: '请选择职位', required: true, trigger: 'change', type: 'number' },
  ],
  realName: [
    { message: '请输入真实姓名', required: true, trigger: 'blur', type: 'string' },
  ],
};

const posOptions = computed(() =>
  posList.value.map((item) => ({
    label: item.name,
    value: item.id,
  })),
);

const tenantOptions = computed(() =>
  tenantList.value.map((item) => ({
    label: item.host ? `${item.label} (${item.host})` : item.label,
    value: item.value,
  })),
);

const roleTransferData = computed(() =>
  roleList.value.map((item) => ({
    key: String(item.id),
    title: item.name,
  })),
);

const roleTransferTargetKeys = computed<string[]>({
  get() {
    return (formState.roleIdList ?? []).map(String);
  },
  set(keys) {
    formState.roleIdList = keys.map(Number);
  },
});

function getAllOrgKeys(items: SysOrg[] = []): Array<number | string> {
  return items.flatMap((item) => [
    item.id,
    ...getAllOrgKeys(item.children ?? []),
  ]);
}

function filterOrgTree(items: SysOrg[] = [], keyword = ''): SysOrg[] {
  const normalizedKeyword = keyword.trim().toLowerCase();
  if (!normalizedKeyword) {
    return items;
  }

  return items
    .map((item) => {
      const children = filterOrgTree(item.children ?? [], normalizedKeyword);
      const matched = item.name.toLowerCase().includes(normalizedKeyword);
      if (!matched && children.length === 0) {
        return undefined;
      }
      return { ...item, children };
    })
    .filter(Boolean) as SysOrg[];
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

function toOrgTreeData(
  items: SysOrg[] = [],
  level = 1,
): TreeProps['treeData'] {
  return items.map((item) => ({
    children: toOrgTreeData(item.children, level + 1),
    icon: getOrgIcon(level),
    key: item.id,
    level,
    title: item.name,
    value: item.id,
    raw: item,
  }));
}

const filteredOrgList = computed(() =>
  filterOrgTree(orgList.value, orgFilterText.value),
);

const orgTreeData = computed<TreeProps['treeData']>(() =>
  toOrgTreeData(orgList.value),
);

const filteredOrgTreeData = computed<TreeProps['treeData']>(() =>
  toOrgTreeData(filteredOrgList.value),
);

function can(code: string) {
  return hasAccessByCodes([code]);
}

function getAccountTypeLabel(value?: number) {
  return (
    accountTypeDisplayOptions.find((item) => item.value === value)?.label ||
    `类型 ${value ?? '-'}`
  );
}

function getAccountTypeColor(value?: number) {
  if (value === MEMBER_ACCOUNT) {
    return 'purple';
  }
  if (value === SYS_ADMIN_ACCOUNT) {
    return 'blue';
  }
  if (value === SUPER_ADMIN_ACCOUNT) {
    return 'red';
  }
  return 'default';
}

function getInitial(record: LooseUserRecord) {
  return (
    record.nickName?.slice(0, 1) ||
    record.realName?.slice(0, 1) ||
    record.account?.slice(0, 1) ||
    'U'
  );
}

function getValueText(value?: null | number | string) {
  return value === undefined || value === null || value === '' ? '无' : value;
}

function resetFormState(values: UserFormState) {
  for (const key of Object.keys(formState)) {
    delete formState[key as keyof UserFormState];
  }
  Object.assign(formState, values);
}

function makeDefaultUser(): UserFormState {
  return {
    account: '',
    accountType: NORMAL_ACCOUNT,
    avatar: '',
    extOrgIdList: [],
    nickName: '',
    orderNo: 100,
    orgId: query.orgId > 0 ? query.orgId : undefined,
    phone: '',
    posId: undefined,
    realName: '',
    remark: '',
    roleIdList: [],
    sex: 1,
    status: ENABLED,
    tenantId: query.tenantId,
  };
}

async function loadOptions() {
  optionLoading.value = true;
  try {
    const [tenants, positions, roles] = await Promise.allSettled([
      getTenantListApi(),
      getPosListApi(),
      getRoleListApi(),
    ]);
    tenantList.value = tenants.status === 'fulfilled' ? tenants.value : [];
    posList.value = positions.status === 'fulfilled' ? positions.value : [];
    roleList.value = roles.status === 'fulfilled' ? roles.value : [];

    if ([tenants, positions, roles].some((item) => item.status === 'rejected')) {
      message.warning('部分基础选项加载失败，请刷新后重试');
    }
  } finally {
    optionLoading.value = false;
  }
}

async function loadOrgData(tenantId = selectedTenantId.value) {
  orgLoading.value = true;
  try {
    const orgs = await getOrgListApi({ id: 0, tenantId });
    orgList.value = orgs;
    expandedOrgKeys.value = getAllOrgKeys(orgs);
    autoExpandParent.value = true;
  } finally {
    orgLoading.value = false;
  }
}

async function loadUsers() {
  if (!can('sysUser:page')) {
    return;
  }
  loading.value = true;
  try {
    const data = await pageUsersApi({
      account: query.account || undefined,
      orgId: query.orgId,
      page: pagination.page,
      pageSize: pagination.pageSize,
      phone: query.phone || undefined,
      posName: query.posName || undefined,
      realName: query.realName || undefined,
      tenantId: query.tenantId,
    });
    users.value = data.items ?? [];
    pagination.total = data.total ?? 0;
  } finally {
    loading.value = false;
  }
}

async function handleSearch() {
  pagination.page = 1;
  await loadUsers();
}

async function resetQuery() {
  query.account = '';
  query.orgId = -1;
  query.phone = '';
  query.posName = '';
  query.realName = '';
  query.tenantId = undefined;
  selectedOrgKeys.value = [];
  await handleSearch();
}

async function handleTenantChange(value: unknown) {
  const tenantId = typeof value === 'number' ? value : undefined;
  selectedTenantId.value = tenantId;
  query.tenantId = tenantId;
  query.orgId = -1;
  selectedOrgKeys.value = [];
  await loadOrgData(tenantId);
  await handleSearch();
}

function handleOrgExpand(keys: Array<number | string>) {
  expandedOrgKeys.value = keys;
  autoExpandParent.value = false;
}

async function handleOrgMenuAction(key: string) {
  orgCommandOpen.value = false;
  if (key === 'expandAll') {
    expandedOrgKeys.value = getAllOrgKeys(filteredOrgList.value);
    autoExpandParent.value = true;
    return;
  }
  if (key === 'collapseAll') {
    expandedOrgKeys.value = [];
    autoExpandParent.value = false;
    return;
  }
  if (key === 'rootNode') {
    query.orgId = -1;
    query.tenantId = selectedTenantId.value;
    selectedOrgKeys.value = [];
    await handleSearch();
    return;
  }
  if (key === 'refresh') {
    await loadOrgData();
    message.success('机构树已刷新');
  }
}

async function handleOrgSelect(
  keys: Array<number | string>,
  event: { node?: any },
) {
  selectedOrgKeys.value = keys;
  const raw = event.node?.raw as SysOrg | undefined;
  query.orgId = raw?.id ?? -1;
  query.tenantId = raw?.tenantId;
  query.account = '';
  query.phone = '';
  query.realName = '';
  await handleSearch();
}

function openCreateUser() {
  drawerTitle.value = '添加账号';
  activeFormTab.value = 'basic';
  resetFormState(makeDefaultUser());
  drawerOpen.value = true;
}

async function openEditUser(record: LooseUserRecord) {
  const user = record as SysUserRecord;
  drawerTitle.value = '编辑账号';
  activeFormTab.value = 'basic';
  const [roleIds, extOrgs] = await Promise.all([
    getUserRoleIdsApi(user.id),
    getUserExtOrgsApi(user.id),
  ]);
  resetFormState({
    ...user,
    extOrgIdList: extOrgs ?? [],
    roleIdList: roleIds ?? [],
  });
  drawerOpen.value = true;
}

async function openCopyUser(record: LooseUserRecord) {
  const user = record as SysUserRecord;
  drawerTitle.value = '复制账号';
  activeFormTab.value = 'basic';
  const [roleIds, extOrgs] = await Promise.all([
    getUserRoleIdsApi(user.id),
    getUserExtOrgsApi(user.id),
  ]);
  resetFormState({
    ...user,
    account: '',
    extOrgIdList: extOrgs ?? [],
    id: undefined,
    roleIdList: roleIds ?? [],
  });
  drawerOpen.value = true;
}

function addExtOrgRow() {
  formState.extOrgIdList = [...(formState.extOrgIdList ?? []), {}];
}

function removeExtOrgRow(index: number) {
  formState.extOrgIdList = (formState.extOrgIdList ?? []).filter(
    (_item, itemIndex) => itemIndex !== index,
  );
}

function normalizeExtOrgs(items?: SysUserExtOrg[]) {
  return (items ?? [])
    .filter((item) => item.orgId && item.posId)
    .map((item) => ({
      id: item.id,
      orgId: item.orgId,
      posId: item.posId,
      tenantId: item.tenantId ?? formState.tenantId,
      userId: formState.id,
    }));
}

async function submitUser() {
  await formRef.value?.validate();
  submitLoading.value = true;
  try {
    const payload = {
      ...formState,
      extOrgIdList: normalizeExtOrgs(formState.extOrgIdList),
      roleIdList: formState.roleIdList ?? [],
    } as SaveUserParams & { id?: number };

    if (payload.id && payload.id > 0) {
      await updateUserApi(payload as SaveUserParams & { id: number });
      message.success('用户已更新');
    } else {
      delete payload.id;
      await addUserApi(payload);
      message.success('用户已新增');
    }

    drawerOpen.value = false;
    await loadUsers();
  } finally {
    submitLoading.value = false;
  }
}

function confirmDelete(record: LooseUserRecord) {
  const user = record as SysUserRecord;
  Modal.confirm({
    centered: true,
    content: `确定删除账号“${user.account}”吗？`,
    okButtonProps: { danger: true },
    okText: '删除',
    title: '删除用户',
    async onOk() {
      await deleteUserApi(user.id);
      message.success('删除成功');
      await loadUsers();
    },
  });
}

async function changeStatus(record: LooseUserRecord, checked: boolean) {
  const user = record as SysUserRecord;
  const nextStatus = checked ? ENABLED : DISABLED;
  const previousStatus = user.status;
  user.status = nextStatus;
  try {
    await setUserStatusApi(user.id, nextStatus);
    message.success('账号状态已更新');
  } catch (error) {
    user.status = previousStatus;
    throw error;
  }
}

function confirmResetPassword(record: LooseUserRecord) {
  const user = record as SysUserRecord;
  Modal.confirm({
    centered: true,
    content: `确定重置账号“${user.account}”的密码吗？`,
    okText: '重置',
    title: '重置密码',
    async onOk() {
      const password = await resetUserPasswordApi(user.id);
      Modal.success({
        centered: true,
        content: `新密码：${password}`,
        title: '密码重置成功',
      });
    },
  });
}

function confirmUnlock(record: LooseUserRecord) {
  const user = record as SysUserRecord;
  Modal.confirm({
    centered: true,
    content: `确定解除账号“${user.account}”的登录锁定吗？`,
    okText: '解除锁定',
    title: '解除登录锁定',
    async onOk() {
      await unlockUserLoginApi(user.id);
      message.success('登录锁定已解除');
    },
  });
}

function handlePageChange(page: number, pageSize: number) {
  pagination.page = page;
  pagination.pageSize = pageSize;
  void loadUsers();
}

onMounted(async () => {
  await loadOptions();
  await loadOrgData();
  await loadUsers();
});

watch(orgFilterText, (value) => {
  if (value.trim()) {
    expandedOrgKeys.value = getAllOrgKeys(filteredOrgList.value);
    autoExpandParent.value = true;
  }
});
</script>

<template>
  <div class="adminnet-user-page">
    <section class="query-panel">
      <Form :model="query" layout="inline">
        <Form.Item label="账号">
          <Input
            v-model:value="query.account"
            allow-clear
            placeholder="账号"
            @press-enter="handleSearch"
          />
        </Form.Item>
        <Form.Item label="姓名">
          <Input
            v-model:value="query.realName"
            allow-clear
            placeholder="姓名"
            @press-enter="handleSearch"
          />
        </Form.Item>
        <Form.Item label="职位">
          <Input
            v-model:value="query.posName"
            allow-clear
            placeholder="职位名称"
            @press-enter="handleSearch"
          />
        </Form.Item>
        <Form.Item label="手机号">
          <Input
            v-model:value="query.phone"
            allow-clear
            placeholder="手机号"
            @press-enter="handleSearch"
          />
        </Form.Item>
        <Form.Item>
          <Space>
            <Button
              v-if="can('sysUser:page')"
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
              v-if="can('sysUser:add')"
              type="primary"
              @click="openCreateUser"
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

    <section class="content-grid">
      <aside class="org-panel">
        <div class="org-panel-head">
          <div>
            <div class="org-panel-title">组织导航</div>
            <div class="org-panel-subtitle">按机构快速筛选账号</div>
          </div>
        </div>
        <Select
          v-if="tenantOptions.length > 0"
          v-model:value="selectedTenantId"
          :options="tenantOptions"
          allow-clear
          class="tenant-select"
          placeholder="请选择租户"
          @change="handleTenantChange"
        />
        <div class="org-toolbar">
          <Input
            v-model:value="orgFilterText"
            allow-clear
            placeholder="机构名称"
          >
            <template #prefix>
              <IconifyIcon icon="lucide:search" />
            </template>
          </Input>
          <Popover
            v-model:open="orgCommandOpen"
            overlay-class-name="org-command-popover"
            placement="bottomRight"
            trigger="click"
          >
            <template #content>
              <div class="org-command-list">
                <button
                  v-for="item in orgMenuItems"
                  :key="item.key"
                  class="org-command-item"
                  type="button"
                  @click="handleOrgMenuAction(item.key)"
                >
                  <IconifyIcon :icon="item.icon" class="org-command-icon" />
                  <span>{{ item.label }}</span>
                </button>
              </div>
            </template>
            <Button class="org-more-button" :loading="orgLoading">
              <template #icon>
                <IconifyIcon icon="lucide:ellipsis" />
              </template>
            </Button>
          </Popover>
        </div>
        <div class="org-tree-shell" :class="{ 'is-loading': orgLoading }">
          <Tree
            v-if="filteredOrgTreeData?.length"
            v-model:expanded-keys="expandedOrgKeys"
            v-model:selected-keys="selectedOrgKeys"
            :auto-expand-parent="autoExpandParent"
            :tree-data="filteredOrgTreeData"
            block-node
            @expand="handleOrgExpand"
            @select="handleOrgSelect"
          >
            <template #title="{ icon, title }">
              <span class="org-tree-node">
                <span class="org-tree-node-icon-wrap">
                  <IconifyIcon :icon="icon" class="org-tree-node-icon" />
                </span>
                <span class="org-tree-node-title">{{ title }}</span>
              </span>
            </template>
          </Tree>
          <Empty v-else :image="Empty.PRESENTED_IMAGE_SIMPLE" />
        </div>
      </aside>

      <main class="table-panel">
        <Table
          :columns="columns"
          :data-source="users"
          :loading="loading"
          :pagination="false"
          :scroll="{ x: 1280 }"
          row-key="id"
          size="small"
        >
          <template #bodyCell="{ column, index, record }">
            <template v-if="column.key === 'index'">
              {{ (pagination.page - 1) * pagination.pageSize + index + 1 }}
            </template>
            <template v-else-if="column.key === 'avatar'">
              <Avatar :src="record.avatar" size="small">
                {{ getInitial(record) }}
              </Avatar>
            </template>
            <template v-else-if="column.key === 'accountType'">
              <Tag :color="getAccountTypeColor(record.accountType)">
                {{ getAccountTypeLabel(record.accountType) }}
              </Tag>
            </template>
            <template v-else-if="column.key === 'status'">
              <Switch
                :checked="record.status === ENABLED"
                :disabled="!can('sysUser:setStatus')"
                size="small"
                @change="(checked) => changeStatus(record, Boolean(checked))"
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
                <Tooltip title="编辑">
                  <Button
                    v-if="can('sysUser:update')"
                    size="small"
                    type="link"
                    @click="openEditUser(record)"
                  >
                    <template #icon>
                      <IconifyIcon icon="lucide:square-pen" />
                    </template>
                  </Button>
                </Tooltip>
                <Tooltip title="删除">
                  <Button
                    v-if="can('sysUser:delete')"
                    danger
                    size="small"
                    type="link"
                    @click="confirmDelete(record)"
                  >
                    <template #icon>
                      <IconifyIcon icon="lucide:trash-2" />
                    </template>
                  </Button>
                </Tooltip>
                <Tooltip title="复制">
                  <Button
                    v-if="can('sysUser:add')"
                    size="small"
                    type="link"
                    @click="openCopyUser(record)"
                  >
                    <template #icon>
                      <IconifyIcon icon="lucide:copy" />
                    </template>
                  </Button>
                </Tooltip>
                <Button
                  v-if="can('sysUser:resetPwd')"
                  danger
                  size="small"
                  type="link"
                  @click="confirmResetPassword(record)"
                >
                  重置密码
                </Button>
                <Button
                  v-if="can('sysUser:unlockLogin')"
                  size="small"
                  type="link"
                  @click="confirmUnlock(record)"
                >
                  解除锁定
                </Button>
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
      </main>
    </section>

    <Drawer
      v-model:open="drawerOpen"
      :confirm-loading="submitLoading"
      :title="drawerTitle"
      destroy-on-close
      placement="right"
      width="760"
      @close="formRef?.clearValidate()"
    >
      <Form
        ref="formRef"
        :model="formState"
        :rules="formRules"
        layout="vertical"
      >
        <Tabs v-model:active-key="activeFormTab">
          <Tabs.TabPane key="basic" tab="基础信息">
        <Divider orientation="left">基础信息</Divider>
        <Row :gutter="16">
          <Col :span="12">
            <Form.Item label="账号名称" name="account">
              <Input
                v-model:value="formState.account"
                :disabled="!!formState.id"
                allow-clear
              />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="真实姓名" name="realName">
              <Input v-model:value="formState.realName" allow-clear />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="手机号" name="phone">
              <Input v-model:value="formState.phone" allow-clear />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="昵称" name="nickName">
              <Input v-model:value="formState.nickName" allow-clear />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="账号类型" name="accountType">
              <Select
                v-model:value="formState.accountType"
                :options="accountTypeOptions"
              />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="邮箱" name="email">
              <Input v-model:value="formState.email" allow-clear />
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
            <Form.Item label="域账号" name="domainAccount">
              <Input v-model:value="formState.domainAccount" allow-clear />
            </Form.Item>
          </Col>
        </Row>

        <Divider orientation="left">机构角色</Divider>
        <Row :gutter="16">
          <Col :span="12">
            <Form.Item label="所属机构" name="orgId">
              <TreeSelect
                v-model:value="formState.orgId"
                :tree-data="orgTreeData"
                allow-clear
                class="w-full"
                show-search
                tree-default-expand-all
              />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="职位" name="posId">
              <Select
                v-model:value="formState.posId"
                :loading="optionLoading"
                :options="posOptions"
                allow-clear
              />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="工号" name="jobNum">
              <Input v-model:value="formState.jobNum" allow-clear />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="状态" name="status">
              <Switch
                v-model:checked="formState.status"
                :checked-value="ENABLED"
                :un-checked-value="DISABLED"
              />
            </Form.Item>
          </Col>
        </Row>

        <Divider orientation="left">附属机构</Divider>
        <div class="ext-toolbar">
          <Button size="small" type="primary" @click="addExtOrgRow">
            <template #icon>
              <IconifyIcon icon="lucide:plus" />
            </template>
            添加附属机构
          </Button>
        </div>
        <template v-if="formState.extOrgIdList?.length">
          <Row
            v-for="(item, index) in formState.extOrgIdList"
            :key="index"
            :gutter="12"
            class="ext-row"
          >
            <Col :span="11">
              <TreeSelect
                v-model:value="item.orgId"
                :tree-data="orgTreeData"
                allow-clear
                class="w-full"
                placeholder="机构"
                show-search
                tree-default-expand-all
              />
            </Col>
            <Col :span="10">
              <Select
                v-model:value="item.posId"
                :options="posOptions"
                allow-clear
                placeholder="职位"
              />
            </Col>
            <Col :span="3">
              <Button danger block @click="removeExtOrgRow(index)">
                <template #icon>
                  <IconifyIcon icon="lucide:trash-2" />
                </template>
              </Button>
            </Col>
          </Row>
        </template>
        <Empty v-else :image="Empty.PRESENTED_IMAGE_SIMPLE" />
          </Tabs.TabPane>

          <Tabs.TabPane key="roles" tab="角色授权">
            <div class="role-transfer-wrap">
              <Transfer
                v-model:target-keys="roleTransferTargetKeys"
                :data-source="roleTransferData"
                :list-style="{ width: '220px', height: '300px' }"
                :render="(item) => item.title"
                :titles="['未授权', '已授权']"
                show-search
              />
            </div>
          </Tabs.TabPane>

          <Tabs.TabPane key="profile" tab="档案信息">
            <Row :gutter="16">
              <Col :span="12">
                <Form.Item label="证件类型" name="cardType">
                  <Select
                    v-model:value="formState.cardType"
                    :options="cardTypeOptions"
                    allow-clear
                  />
                </Form.Item>
              </Col>
              <Col :span="12">
                <Form.Item label="证件号码" name="idCardNum">
                  <Input v-model:value="formState.idCardNum" allow-clear />
                </Form.Item>
              </Col>
              <Col :span="12">
                <Form.Item label="出生日期" name="birthday">
                  <DatePicker
                    v-model:value="formState.birthday"
                    class="w-full"
                    value-format="YYYY-MM-DD"
                  />
                </Form.Item>
              </Col>
              <Col :span="12">
                <Form.Item label="性别" name="sex">
                  <Radio.Group
                    v-model:value="formState.sex"
                    :options="genderOptions"
                  />
                </Form.Item>
              </Col>
              <Col :span="12">
                <Form.Item label="年龄" name="age">
                  <InputNumber
                    v-model:value="formState.age"
                    class="w-full"
                    :min="0"
                  />
                </Form.Item>
              </Col>
              <Col :span="12">
                <Form.Item label="民族" name="nation">
                  <Select
                    v-model:value="formState.nation"
                    :options="nationOptions"
                    allow-clear
                  />
                </Form.Item>
              </Col>
              <Col :span="24">
                <Form.Item label="地址" name="address">
                  <Input.TextArea
                    v-model:value="formState.address"
                    :auto-size="{ minRows: 2, maxRows: 4 }"
                    allow-clear
                  />
                </Form.Item>
              </Col>
              <Col :span="12">
                <Form.Item label="毕业学校" name="college">
                  <Input v-model:value="formState.college" allow-clear />
                </Form.Item>
              </Col>
              <Col :span="12">
                <Form.Item label="文化程度" name="cultureLevel">
                  <Select
                    v-model:value="formState.cultureLevel"
                    :options="cultureLevelOptions"
                    allow-clear
                  />
                </Form.Item>
              </Col>
              <Col :span="12">
                <Form.Item label="政治面貌" name="politicalOutlook">
                  <Input
                    v-model:value="formState.politicalOutlook"
                    allow-clear
                  />
                </Form.Item>
              </Col>
              <Col :span="12">
                <Form.Item label="办公电话" name="officePhone">
                  <Input v-model:value="formState.officePhone" allow-clear />
                </Form.Item>
              </Col>
              <Col :span="12">
                <Form.Item label="紧急联系人" name="emergencyContact">
                  <Input
                    v-model:value="formState.emergencyContact"
                    allow-clear
                  />
                </Form.Item>
              </Col>
              <Col :span="12">
                <Form.Item label="联系人电话" name="emergencyPhone">
                  <Input v-model:value="formState.emergencyPhone" allow-clear />
                </Form.Item>
              </Col>
              <Col :span="24">
                <Form.Item label="联系人地址" name="emergencyAddress">
                  <Input.TextArea
                    v-model:value="formState.emergencyAddress"
                    :auto-size="{ minRows: 2, maxRows: 4 }"
                    allow-clear
                  />
                </Form.Item>
              </Col>
              <Col :span="24">
                <Form.Item label="备注" name="remark">
                  <Input.TextArea
                    v-model:value="formState.remark"
                    :auto-size="{ minRows: 3, maxRows: 5 }"
                    allow-clear
                  />
                </Form.Item>
              </Col>
            </Row>
          </Tabs.TabPane>
        </Tabs>
      </Form>

      <template #footer>
        <Space>
          <Button @click="drawerOpen = false">取消</Button>
          <Button :loading="submitLoading" type="primary" @click="submitUser">
            保存
          </Button>
        </Space>
      </template>
    </Drawer>
  </div>
</template>

<style scoped>
.adminnet-user-page {
  display: flex;
  min-height: 100%;
  flex-direction: column;
  gap: 12px;
  padding: 12px;
}

.query-panel,
.org-panel,
.table-panel {
  border: 1px solid hsl(var(--border));
  border-radius: 8px;
  background: hsl(var(--background));
}

.query-panel {
  padding: 12px 12px 0;
}

.content-grid {
  display: grid;
  min-height: 0;
  flex: 1;
  grid-template-columns: minmax(236px, 292px) minmax(0, 1fr);
  gap: 12px;
}

.org-panel {
  display: flex;
  min-height: 520px;
  flex-direction: column;
  overflow: auto;
  padding: 10px;
}

.org-panel-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 2px 2px 10px;
}

.org-panel-title {
  color: hsl(var(--foreground));
  font-size: 14px;
  font-weight: 650;
}

.org-panel-subtitle {
  margin-top: 2px;
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.tenant-select {
  margin-bottom: 10px;
  width: 100%;
}

.org-toolbar {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 34px;
  gap: 8px;
  margin-bottom: 12px;
}

.org-more-button {
  height: 32px;
  width: 34px;
  color: hsl(var(--muted-foreground));
}

.org-tree-shell {
  flex: 1;
  min-height: 0;
  padding: 6px;
  border: 1px solid hsl(var(--border) / 70%);
  border-radius: 8px;
  background:
    linear-gradient(180deg, hsl(var(--muted) / 25%), transparent 52px),
    hsl(var(--background));
}

.org-tree-shell.is-loading {
  opacity: 0.62;
  pointer-events: none;
}

.org-tree-node {
  display: flex;
  min-width: 0;
  align-items: center;
  gap: 8px;
  color: hsl(var(--foreground));
  font-size: 13px;
  font-weight: 500;
  line-height: 30px;
}

.org-tree-node-icon-wrap {
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

.org-tree-node-icon {
  width: 14px;
  height: 14px;
  flex: none;
  color: hsl(var(--muted-foreground));
}

.org-tree-node-title {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.table-panel {
  min-width: 0;
  padding: 12px;
}

.table-footer {
  display: flex;
  justify-content: flex-end;
  padding-top: 12px;
}

.ext-toolbar {
  margin-bottom: 10px;
}

.ext-row {
  margin-bottom: 10px;
}

.role-transfer-wrap {
  display: flex;
  justify-content: center;
  min-height: 420px;
  padding-top: 12px;
}

.modify-record {
  width: 360px;
}

:deep(.ant-form-inline .ant-form-item) {
  margin-bottom: 12px;
}

:deep(.org-toolbar .ant-input-affix-wrapper),
:deep(.tenant-select .ant-select-selector) {
  min-height: 32px;
  border-radius: 7px;
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
  height: 32px;
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

:deep(.ant-tree .ant-tree-node-selected) {
  background: hsl(var(--primary) / 10%) !important;
  box-shadow: inset 3px 0 0 hsl(var(--primary));
}

:deep(.ant-tree .ant-tree-node-selected .org-tree-node-icon),
:deep(.ant-tree .ant-tree-node-selected .org-tree-node-title) {
  color: hsl(var(--primary));
}

:deep(.ant-tree .ant-tree-node-selected .org-tree-node-icon-wrap) {
  border-color: hsl(var(--primary) / 35%);
  background: hsl(var(--primary) / 10%);
}

:deep(.ant-tree .ant-tree-switcher) {
  width: 18px;
  color: hsl(var(--muted-foreground));
}

:deep(.ant-tree .ant-tree-indent-unit) {
  width: 14px;
}

:global(.org-command-popover .ant-popover-inner) {
  padding: 6px;
  border: 1px solid hsl(var(--border));
  border-radius: 10px;
  box-shadow:
    0 12px 28px rgb(15 23 42 / 12%),
    0 2px 8px rgb(15 23 42 / 8%);
}

:global(.org-command-popover .ant-popover-inner-content) {
  padding: 0;
}

.org-command-list {
  display: grid;
  min-width: 128px;
  gap: 2px;
}

.org-command-item {
  display: inline-flex;
  height: 32px;
  align-items: center;
  gap: 8px;
  padding: 0 9px;
  border: 0;
  border-radius: 7px;
  background: transparent;
  color: hsl(var(--foreground));
  cursor: pointer;
  font-size: 13px;
  font-weight: 500;
  text-align: left;
}

.org-command-item:hover {
  background: hsl(var(--primary) / 8%);
  color: hsl(var(--primary));
}

.org-command-icon {
  width: 14px;
  height: 14px;
  color: currentColor;
}

@media (max-width: 900px) {
  .content-grid {
    grid-template-columns: 1fr;
  }

  .org-panel {
    min-height: 220px;
  }
}
</style>
