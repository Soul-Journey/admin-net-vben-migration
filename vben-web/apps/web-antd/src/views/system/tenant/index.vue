<script setup lang="ts">
import type {
  FormInstance,
  TableColumnsType,
  UploadProps,
} from 'ant-design-vue';
import type { CheckboxChangeEvent } from 'ant-design-vue/es/checkbox/interface';
import type { Rule } from 'ant-design-vue/es/form';

import type { PropType, VNode } from 'vue';

import type {
  SaveTenantParams,
  SysMenuRecord,
  SysTenantRecord,
  TenantLoginResult,
  UserRegWayOption,
} from '#/api';

import { computed, defineComponent, h, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';
import { IconifyIcon } from '@vben/icons';
import { useAccessStore } from '@vben/stores';

import {
  Avatar,
  Button,
  Checkbox,
  Col,
  Descriptions,
  Dropdown,
  Form,
  Input,
  InputNumber,
  Menu,
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
  Upload,
} from 'ant-design-vue';

import {
  addTenantApi,
  changeTenantApi,
  createTenantDbApi,
  deleteTenantApi,
  getTenantMenuIdsApi,
  goTenantApi,
  grantTenantMenuApi,
  listMenusApi,
  listUserRegWaysApi,
  pageTenantsApi,
  resetTenantPasswordApi,
  setTenantStatusApi,
  syncTenantGrantMenuApi,
  updateTenantApi,
} from '#/api';
import { persistAdminNetTokens } from '#/utils/adminnet/token';
import { ADMIN_PAGINATION_PROPS } from '#/utils/pagination';

defineOptions({ name: 'AdminNetSystemTenant' });

type TenantFormState = Partial<SaveTenantParams> & { id?: number };

const ENABLED = 1;
const DISABLED = 2;
const YES = 1;
const NO = 2;
const ID_TENANT = 0;
const MENU_TYPE_DIR = 1;
const MENU_TYPE_BUTTON = 3;
const DEFAULT_TENANT_ID = 123_456_780_000_000;
const TENANT_SWITCH_HOME_PATH = '/dashboard/home';

const { hasAccessByCodes } = useAccess();
const accessStore = useAccessStore();

const loading = ref(false);
const submitLoading = ref(false);
const menuLoading = ref(false);
const regWayLoading = ref(false);
const grantSubmitLoading = ref(false);
const tenantActionLoading = ref('');
const modalOpen = ref(false);
const grantModalOpen = ref(false);
const tenantModalTitle = ref('新增租户');
const activeFormTab = ref('basic');
const formRef = ref<FormInstance>();
const tenants = ref<SysTenantRecord[]>([]);
const menuTree = ref<SysMenuRecord[]>([]);
const regWayList = ref<UserRegWayOption[]>([]);
const checkedMenuKeys = ref<Array<number | string>>([]);
const expandedMenuKeys = ref<Array<number | string>>([]);
const menuFilterText = ref('');
const currentGrantTenant = ref<SysTenantRecord>();
const formState = reactive<TenantFormState>({});

const query = reactive({
  name: '',
  phone: '',
});

const pagination = reactive({
  page: 1,
  pageSize: 50,
  total: 0,
});

const columns: TableColumnsType<SysTenantRecord> = [
  { key: 'index', title: '序号', width: 58 },
  { key: 'logo', title: '图标', width: 62 },
  { dataIndex: 'name', key: 'name', title: '名称', width: 170 },
  { dataIndex: 'title', key: 'title', title: '标题', width: 170 },
  {
    dataIndex: 'adminAccount',
    key: 'adminAccount',
    title: '租管账号',
    width: 118,
  },
  { dataIndex: 'phone', key: 'phone', title: '电话', width: 126 },
  { dataIndex: 'host', key: 'host', title: '域名', width: 150 },
  { key: 'tenantType', title: '租户类型', width: 96 },
  { key: 'enableReg', title: '启用注册', width: 96 },
  { key: 'dbType', title: '数据库', width: 112 },
  { key: 'siteInfo', title: '站点信息', width: 104 },
  { key: 'status', title: '状态', width: 76 },
  { dataIndex: 'orderNo', key: 'orderNo', title: '排序', width: 70 },
  { key: 'modifyRecord', title: '修改记录', width: 112 },
  { key: 'actions', fixed: 'right', title: '操作', width: 192 },
];

const formRules: Record<string, Rule[]> = {
  adminAccount: [
    {
      message: '请输入租管账号',
      required: true,
      trigger: 'blur',
      type: 'string',
    },
  ],
  copyright: [
    {
      message: '请输入版权信息',
      required: true,
      trigger: 'blur',
      type: 'string',
    },
  ],
  icp: [
    {
      message: '请输入备案号',
      required: true,
      trigger: 'blur',
      type: 'string',
    },
  ],
  icpUrl: [
    {
      message: '请输入 ICP 地址',
      required: true,
      trigger: 'blur',
      type: 'string',
    },
  ],
  name: [
    {
      message: '请输入租户名称',
      required: true,
      trigger: 'blur',
      type: 'string',
    },
  ],
  tenantType: [
    {
      message: '请选择租户类型',
      required: true,
      trigger: 'change',
      type: 'number',
    },
  ],
  title: [
    {
      message: '请输入系统主标题',
      required: true,
      trigger: 'blur',
      type: 'string',
    },
  ],
  viceDesc: [
    {
      message: '请输入系统描述',
      required: true,
      trigger: 'blur',
      type: 'string',
    },
  ],
  viceTitle: [
    {
      message: '请输入系统副标题',
      required: true,
      trigger: 'blur',
      type: 'string',
    },
  ],
};

const tenantTypeOptions = [
  { label: 'Id隔离', value: ID_TENANT },
  { label: '库隔离', value: 1 },
];

const yesNoOptions = [
  { label: '启用', value: YES },
  { label: '关闭', value: NO },
];

const dbTypeOptions = [
  { label: 'MySql', value: 0 },
  { label: 'SqlServer', value: 1 },
  { label: 'Sqlite', value: 2 },
  { label: 'Oracle', value: 3 },
  { label: 'PostgreSQL', value: 4 },
  { label: 'Dm', value: 5 },
  { label: 'Kdbndp', value: 6 },
  { label: 'Oscar', value: 7 },
  { label: 'MySqlConnector', value: 8 },
  { label: 'Access', value: 9 },
  { label: 'OpenGauss', value: 10 },
  { label: 'QuestDB', value: 11 },
  { label: 'HG', value: 12 },
  { label: 'ClickHouse', value: 13 },
  { label: 'GBase', value: 14 },
  { label: 'Odbc', value: 15 },
  { label: 'OceanBaseForOracle', value: 16 },
  { label: 'TDengine', value: 17 },
  { label: 'GaussDB', value: 18 },
  { label: 'OceanBase', value: 19 },
  { label: 'Tidb', value: 20 },
  { label: 'Vastbase', value: 21 },
  { label: 'PolarDB', value: 22 },
  { label: 'Doris', value: 23 },
  { label: 'Custom', value: 900 },
];

const filteredMenuTree = computed(() =>
  filterMenuTree(menuTree.value, menuFilterText.value),
);

const regWayOptions = computed(() =>
  regWayList.value.map((item) => ({
    label: [item.name, item.orgName, item.roleName, item.posName]
      .filter(Boolean)
      .join(' / '),
    value: item.id,
  })),
);

const isIdTenantForm = computed(() => formState.tenantType === ID_TENANT);

function can(code: string) {
  return hasAccessByCodes([code]);
}

function asTenant(record: unknown) {
  return record as SysTenantRecord;
}

function getValueText(value?: null | number | string) {
  return value === undefined || value === null || value === '' ? '无' : value;
}

function getTenantTypeMeta(type?: number) {
  return type === ID_TENANT
    ? { color: 'blue', label: 'Id隔离' }
    : { color: 'orange', label: '库隔离' };
}

function getYesNoMeta(value?: number) {
  return value === YES
    ? { color: 'success', label: '启用' }
    : { color: 'default', label: '关闭' };
}

function getDbTypeLabel(value?: number) {
  return dbTypeOptions.find((item) => item.value === value)?.label || '无';
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

function filterMenuTree(
  items: SysMenuRecord[] = [],
  keyword = '',
): SysMenuRecord[] {
  const normalizedKeyword = keyword.trim().toLowerCase();
  if (!normalizedKeyword) {
    return items;
  }

  return items
    .map((item) => {
      const children = filterMenuTree(item.children ?? [], normalizedKeyword);
      const matched = [item.title, item.name, item.path, item.permission]
        .filter(Boolean)
        .some((value) =>
          String(value).toLowerCase().includes(normalizedKeyword),
        );
      if (!matched && children.length === 0) {
        return undefined;
      }
      return { ...item, children };
    })
    .filter(Boolean) as SysMenuRecord[];
}

function resetFormState(values: TenantFormState) {
  for (const key of Object.keys(formState)) {
    delete formState[key as keyof TenantFormState];
  }
  Object.assign(formState, values);
}

function makeDefaultTenant(): TenantFormState {
  const year = new Date().getFullYear();
  return {
    copyright: `Copyright © ${year}-present Admin.NET All rights reserved.`,
    dbType: 0,
    enableReg: NO,
    icp: '省ICP备12345678号',
    icpUrl: 'https://beian.miit.gov.cn',
    orderNo: 100,
    status: ENABLED,
    tenantType: ID_TENANT,
  };
}

function fileToBase64(file: File) {
  return new Promise<string>((resolve, reject) => {
    const reader = new FileReader();
    reader.addEventListener('error', reject);
    reader.addEventListener('load', () => resolve(String(reader.result)));
    reader.readAsDataURL(file);
  });
}

const beforeLogoUpload: UploadProps['beforeUpload'] = async (file) => {
  const rawFile = file as File;
  formState.logo = URL.createObjectURL(rawFile);
  formState.logoBase64 = await fileToBase64(rawFile);
  formState.logoFileName = rawFile.name;
  return false;
};

async function loadTenants() {
  if (!can('sysTenant:page')) {
    return;
  }
  loading.value = true;
  try {
    const data = await pageTenantsApi({
      name: query.name || undefined,
      page: pagination.page,
      pageSize: pagination.pageSize,
      phone: query.phone || undefined,
    });
    tenants.value = data.items ?? [];
    pagination.total = data.total ?? 0;
  } finally {
    loading.value = false;
  }
}

async function loadRegWays(tenantId?: number) {
  if (!tenantId) {
    regWayList.value = [];
    return;
  }
  regWayLoading.value = true;
  try {
    regWayList.value = await listUserRegWaysApi({ tenantId });
  } finally {
    regWayLoading.value = false;
  }
}

async function handleSearch() {
  pagination.page = 1;
  await loadTenants();
}

async function resetQuery() {
  query.name = '';
  query.phone = '';
  await handleSearch();
}

async function handlePageChange(page: number, pageSize: number) {
  pagination.page = page;
  pagination.pageSize = pageSize;
  await loadTenants();
}

function openCreateTenant() {
  tenantModalTitle.value = '新增租户';
  activeFormTab.value = 'basic';
  regWayList.value = [];
  resetFormState(makeDefaultTenant());
  modalOpen.value = true;
}

async function openEditTenant(record: SysTenantRecord) {
  tenantModalTitle.value = '编辑租户';
  activeFormTab.value = 'basic';
  await loadRegWays(record.id);
  resetFormState({
    ...makeDefaultTenant(),
    ...record,
  });
  modalOpen.value = true;
}

async function submitTenant() {
  await formRef.value?.validate();
  submitLoading.value = true;
  try {
    const payload = {
      ...formState,
      dbType: isIdTenantForm.value ? undefined : formState.dbType,
      orderNo: formState.orderNo ?? 100,
      status: formState.status ?? ENABLED,
    } as SaveTenantParams & { id?: number };

    if (payload.enableReg !== YES) {
      payload.regWayId = undefined;
    }

    if (payload.id) {
      await updateTenantApi(payload as SaveTenantParams & { id: number });
      message.success('租户已更新');
    } else {
      await addTenantApi(payload);
      message.success('租户已新增');
    }
    modalOpen.value = false;
    await loadTenants();
  } finally {
    submitLoading.value = false;
  }
}

async function changeStatus(record: SysTenantRecord, checked: boolean) {
  const nextStatus = checked ? ENABLED : DISABLED;
  await setTenantStatusApi(record.id, nextStatus);
  message.success('租户状态已更新');
  await loadTenants();
}

function getUniqueMenuIds(keys: Array<number | string>) {
  return [...new Set(keys.map(Number).filter((id) => Number.isFinite(id)))];
}

function applyTenantLogin(
  result: TenantLoginResult,
  options: { messageText: string },
) {
  if (!result.accessToken) {
    message.warning('后端未返回登录令牌');
    return;
  }
  accessStore.setAccessToken(result.accessToken);
  persistAdminNetTokens({
    accessToken: result.accessToken,
    refreshToken: result.refreshToken,
  });
  message.success(options.messageText);
  window.setTimeout(() => {
    window.location.replace(TENANT_SWITCH_HOME_PATH);
  }, 500);
}

function getTenantActionKey(action: string, record: SysTenantRecord) {
  return `${record.id}:${action}`;
}

function isTenantActionLoading(action: string, record: SysTenantRecord) {
  return tenantActionLoading.value === getTenantActionKey(action, record);
}

function isTenantBusy(record: SysTenantRecord) {
  return tenantActionLoading.value.startsWith(`${record.id}:`);
}

async function runTenantAction(
  action: string,
  record: SysTenantRecord,
  task: () => Promise<void>,
) {
  if (isTenantBusy(record)) {
    return;
  }
  tenantActionLoading.value = getTenantActionKey(action, record);
  try {
    await task();
  } finally {
    tenantActionLoading.value = '';
  }
}

function confirmGoTenant(record: SysTenantRecord) {
  Modal.confirm({
    centered: true,
    content: h('div', { class: 'tenant-action-confirm' }, [
      h('p', `确定要进入「${record.name}」租管端吗？`),
      h('ul', [
        h(
          'li',
          `会登录为该租户绑定的租管账号：${record.adminAccount || '未配置'}`,
        ),
        h(
          'li',
          '菜单、按钮和数据权限以租管账号为准，适合代租户管理员排查问题。',
        ),
        h('li', '如果只是想用当前账号查看另一个租户，请使用“切换租户”。'),
      ]),
    ]),
    okText: '进入',
    onOk: () =>
      runTenantAction('go', record, async () => {
        applyTenantLogin(await goTenantApi(record.id), {
          messageText: `已进入「${record.name}」租管端，正在进入工作台`,
        });
      }),
    title: '进入租管端',
  });
}

function confirmChangeTenant(record: SysTenantRecord) {
  Modal.confirm({
    centered: true,
    content: h('div', { class: 'tenant-action-confirm' }, [
      h('p', `确定将当前用户切换到「${record.name}」吗？`),
      h('ul', [
        h('li', '会保留当前登录用户，只切换当前用户的租户上下文。'),
        h('li', '菜单、按钮和数据权限以当前账号在目标租户中的角色为准。'),
        h(
          'li',
          '如果当前账号在两个租户权限相近，页面看起来可能只是刷新了一次。',
        ),
      ]),
    ]),
    okText: '切换',
    onOk: () =>
      runTenantAction('change', record, async () => {
        applyTenantLogin(await changeTenantApi(record.id), {
          messageText: `已切换到「${record.name}」租户上下文，正在进入工作台`,
        });
      }),
    title: '切换租户',
  });
}

function confirmSyncGrantMenu(record: SysTenantRecord) {
  Modal.confirm({
    centered: true,
    content: h('div', { class: 'tenant-action-confirm' }, [
      h('p', `确定同步「${record.name}」的授权数据吗？`),
      h('ul', [
        h(
          'li',
          '该动作会根据当前角色菜单重新生成租户菜单授权，通常只在版本升级后补齐新菜单时使用。',
        ),
        h('li', '同步后会清理并重建租户授权关系，不是普通刷新按钮。'),
        h('li', '前后端和数据库都已加去重保护，避免再次写入重复权限记录。'),
      ]),
    ]),
    okText: '同步',
    onOk: () =>
      runTenantAction('syncGrant', record, async () => {
        await syncTenantGrantMenuApi(record.id);
        message.success(`「${record.name}」授权数据已同步`);
        await loadTenants();
      }),
    title: '同步授权',
  });
}

function confirmResetPassword(record: SysTenantRecord) {
  Modal.confirm({
    centered: true,
    content: `确定重置「${record.name}」租管账号密码吗？`,
    okText: '重置',
    onOk: () =>
      runTenantAction('resetPwd', record, async () => {
        if (!record.userId) {
          message.warning('当前租户缺少租管用户 Id');
          return;
        }
        const password = await resetTenantPasswordApi(record.userId);
        message.success(`密码已重置为：${password}`);
      }),
    title: '重置密码',
  });
}

function confirmCreateDb(record: SysTenantRecord) {
  Modal.confirm({
    centered: true,
    content: `确定创建或更新「${record.name}」租户数据库吗？请确认连接字符串配置正确。`,
    okText: '创建/更新',
    onOk: () =>
      runTenantAction('createDb', record, async () => {
        await createTenantDbApi(record.id);
        message.success('租户数据库已创建/更新');
      }),
    title: '创建租户数据库',
  });
}

function confirmDeleteTenant(record: SysTenantRecord) {
  Modal.confirm({
    centered: true,
    content: `确定删除租户「${record.name}」吗？默认租户或存在关联数据时后端会拒绝删除。`,
    okButtonProps: { danger: true },
    okText: '删除',
    onOk: () =>
      runTenantAction('delete', record, async () => {
        await deleteTenantApi(record.id);
        message.success('租户已删除');
        await loadTenants();
      }),
    title: '删除租户',
  });
}

async function openGrantMenu(record: SysTenantRecord) {
  currentGrantTenant.value = record;
  grantModalOpen.value = true;
  menuFilterText.value = '';
  menuLoading.value = true;
  try {
    const [menus, ids] = await Promise.all([
      listMenusApi({ tenantId: record.id }),
      getTenantMenuIdsApi(record.id),
    ]);
    menuTree.value = menus;
    checkedMenuKeys.value = ids;
    expandedMenuKeys.value = getAllMenuKeys(menus);
  } finally {
    menuLoading.value = false;
  }
}

async function submitGrantMenu() {
  if (!currentGrantTenant.value) {
    return;
  }
  grantSubmitLoading.value = true;
  try {
    const menuIdList = getUniqueMenuIds(checkedMenuKeys.value);
    await grantTenantMenuApi({
      id: currentGrantTenant.value.id,
      menuIdList,
    });
    checkedMenuKeys.value = menuIdList;
    message.success(`「${currentGrantTenant.value.name}」菜单授权已保存`);
    grantModalOpen.value = false;
  } finally {
    grantSubmitLoading.value = false;
  }
}

function expandAllMenus() {
  expandedMenuKeys.value = getAllMenuKeys(filteredMenuTree.value);
}

function collapseAllMenus() {
  expandedMenuKeys.value = getRootMenuKeys(filteredMenuTree.value);
}

function getMenuTitle(item: SysMenuRecord) {
  return item.title || item.name || item.path || `菜单 ${item.id}`;
}

function getMenuChildren(item: SysMenuRecord) {
  return item.children ?? [];
}

function isActionMenu(item: SysMenuRecord) {
  return item.type === MENU_TYPE_BUTTON || Boolean(item.permission);
}

function getActionChildren(item: SysMenuRecord) {
  return getMenuChildren(item).filter((child) => isActionMenu(child));
}

function getBranchChildren(item: SysMenuRecord) {
  return getMenuChildren(item).filter((child) => !isActionMenu(child));
}

function getMenuIds(item: SysMenuRecord): number[] {
  return [
    item.id,
    ...getMenuChildren(item).flatMap((child) => getMenuIds(child)),
  ];
}

function getCheckedMenuCount(item: SysMenuRecord) {
  return getMenuIds(item).filter((id) => checkedMenuKeys.value.includes(id))
    .length;
}

function isIndeterminateMenu(item: SysMenuRecord) {
  const ids = getMenuIds(item);
  const checkedCount = getCheckedMenuCount(item);
  return checkedCount > 0 && checkedCount < ids.length;
}

function setMenuChecked(item: SysMenuRecord, checked: boolean) {
  const ids = new Set(getMenuIds(item));
  const next = new Set(checkedMenuKeys.value);

  for (const id of ids) {
    if (checked) {
      next.add(id);
    } else {
      next.delete(id);
    }
  }

  checkedMenuKeys.value = [...next];
}

function toggleMenuExpand(id: number) {
  const next = new Set(expandedMenuKeys.value);
  if (next.has(id)) {
    next.delete(id);
  } else {
    next.add(id);
  }
  expandedMenuKeys.value = [...next];
}

const GrantMenuNode = defineComponent({
  name: 'GrantMenuNode',
  props: {
    checkedKeys: {
      required: true,
      type: Array as PropType<Array<number | string>>,
    },
    expandedKeys: {
      required: true,
      type: Array as PropType<Array<number | string>>,
    },
    item: {
      required: true,
      type: Object as PropType<SysMenuRecord>,
    },
    level: {
      default: 0,
      type: Number,
    },
  },
  emits: {
    check: (_item: SysMenuRecord, _checked: boolean) => true,
    toggle: (_id: number) => true,
  },
  setup(props, { emit }) {
    const isChecked = (id: number) => props.checkedKeys.includes(id);
    const isExpanded = (id: number) => props.expandedKeys.includes(id);
    const readChecked = (event: CheckboxChangeEvent) =>
      Boolean(event.target?.checked);

    const renderAction = (action: SysMenuRecord): VNode =>
      h(
        'span',
        {
          class: ['grant-action-pill', isChecked(action.id) && 'is-checked'],
          title: action.permission || getMenuTitle(action),
        },
        [
          h(Checkbox, {
            checked: isChecked(action.id),
            onChange: (event: CheckboxChangeEvent) =>
              emit('check', action, readChecked(event)),
          }),
          h('span', { class: 'grant-action-title' }, getMenuTitle(action)),
        ],
      );

    const renderNode = (item: SysMenuRecord, level: number): VNode => {
      const branchChildren = getBranchChildren(item);
      const actionChildren = getActionChildren(item);
      const expanded = isExpanded(item.id);
      const hasBranches = branchChildren.length > 0;
      const hasChildren = hasBranches || actionChildren.length > 0;

      return h(
        'div',
        {
          class: [
            'grant-node',
            item.type === MENU_TYPE_DIR && 'is-dir',
            expanded && 'is-expanded',
          ],
          style: { '--grant-level': String(level) },
        },
        [
          h('div', { class: 'grant-node-row' }, [
            h(
              'button',
              {
                class: ['grant-node-switch', !hasChildren && 'is-placeholder'],
                disabled: !hasChildren,
                type: 'button',
                onClick: () => hasChildren && emit('toggle', item.id),
              },
              [
                h(IconifyIcon, {
                  icon: expanded
                    ? 'lucide:chevron-down'
                    : 'lucide:chevron-right',
                }),
              ],
            ),
            h(Checkbox, {
              checked: isChecked(item.id),
              indeterminate: isIndeterminateMenu(item),
              onChange: (event: CheckboxChangeEvent) =>
                emit('check', item, readChecked(event)),
            }),
            h(IconifyIcon, {
              class: 'grant-node-icon',
              icon:
                item.type === MENU_TYPE_DIR
                  ? 'lucide:folder-tree'
                  : 'lucide:file-cog',
            }),
            h('span', { class: 'grant-node-title' }, getMenuTitle(item)),
            item.path
              ? h(
                  Tag,
                  { class: 'grant-path-tag', color: 'orange' },
                  () => item.path,
                )
              : null,
          ]),
          expanded && actionChildren.length > 0
            ? h(
                'div',
                { class: 'grant-action-row' },
                actionChildren.map((action) => renderAction(action)),
              )
            : null,
          expanded && branchChildren.length > 0
            ? h(
                'div',
                { class: 'grant-node-children' },
                branchChildren.map((child) => renderNode(child, level + 1)),
              )
            : null,
        ],
      );
    };

    return () => renderNode(props.item, props.level);
  },
});

async function refreshGrantMenus() {
  if (currentGrantTenant.value) {
    await openGrantMenu(currentGrantTenant.value);
  }
}

function handleTenantAction(action: string, record: SysTenantRecord) {
  const handlers: Record<string, () => void> = {
    change: () => confirmChangeTenant(record),
    delete: () => confirmDeleteTenant(record),
    go: () => confirmGoTenant(record),
    grant: () =>
      void runTenantAction('grant', record, () => openGrantMenu(record)),
    resetPwd: () => confirmResetPassword(record),
    syncGrant: () => confirmSyncGrantMenu(record),
  };
  handlers[action]?.();
}

onMounted(loadTenants);
</script>

<template>
  <div class="tenant-page">
    <section class="panel">
      <div class="panel-head">
        <div>
          <div class="panel-title">租户</div>
          <div class="panel-subtitle">
            管理租户信息、站点配置、菜单授权和租户上下文
          </div>
        </div>
      </div>

      <Form :model="query" layout="inline" class="query-form">
        <Form.Item label="租户名称">
          <Input
            v-model:value="query.name"
            allow-clear
            placeholder="租户名称"
            @press-enter="handleSearch"
          />
        </Form.Item>
        <Form.Item label="联系电话">
          <Input
            v-model:value="query.phone"
            allow-clear
            placeholder="联系电话"
            @press-enter="handleSearch"
          />
        </Form.Item>
        <Form.Item>
          <Space>
            <Button
              v-if="can('sysTenant:page')"
              :loading="loading"
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
              v-if="can('sysTenant:add')"
              type="primary"
              @click="openCreateTenant"
            >
              <template #icon>
                <IconifyIcon icon="lucide:plus" />
              </template>
              新增
            </Button>
          </Space>
        </Form.Item>
      </Form>

      <Table
        :columns="columns"
        :data-source="tenants"
        :loading="loading"
        :pagination="false"
        :scroll="{ x: 1650 }"
        row-key="id"
        size="small"
      >
        <template #bodyCell="{ column, index, record }">
          <template v-if="column.key === 'index'">
            {{ (pagination.page - 1) * pagination.pageSize + index + 1 }}
          </template>
          <template v-else-if="column.key === 'logo'">
            <Avatar shape="square" :size="28" :src="asTenant(record).logo">
              {{ asTenant(record).name?.slice(0, 1) }}
            </Avatar>
          </template>
          <template v-else-if="column.key === 'tenantType'">
            <Tag :color="getTenantTypeMeta(asTenant(record).tenantType).color">
              {{ getTenantTypeMeta(asTenant(record).tenantType).label }}
            </Tag>
          </template>
          <template v-else-if="column.key === 'enableReg'">
            <Tag :color="getYesNoMeta(asTenant(record).enableReg).color">
              {{ getYesNoMeta(asTenant(record).enableReg).label }}
            </Tag>
          </template>
          <template v-else-if="column.key === 'dbType'">
            <Tag>{{ getDbTypeLabel(asTenant(record).dbType) }}</Tag>
          </template>
          <template v-else-if="column.key === 'siteInfo'">
            <Popover
              overlay-class-name="tenant-record-popover"
              placement="bottom"
              trigger="hover"
            >
              <template #content>
                <Descriptions
                  :column="2"
                  bordered
                  class="site-record"
                  layout="vertical"
                  size="small"
                >
                  <Descriptions.Item label="副标题">
                    {{ getValueText(asTenant(record).viceTitle) }}
                  </Descriptions.Item>
                  <Descriptions.Item label="启用注册">
                    <Tag
                      :color="getYesNoMeta(asTenant(record).enableReg).color"
                    >
                      {{ getYesNoMeta(asTenant(record).enableReg).label }}
                    </Tag>
                  </Descriptions.Item>
                  <Descriptions.Item label="描述" :span="2">
                    {{ getValueText(asTenant(record).viceDesc) }}
                  </Descriptions.Item>
                  <Descriptions.Item label="水印">
                    {{ getValueText(asTenant(record).watermark) }}
                  </Descriptions.Item>
                  <Descriptions.Item label="备案号">
                    {{ getValueText(asTenant(record).icp) }}
                  </Descriptions.Item>
                  <Descriptions.Item label="版权" :span="2">
                    {{ getValueText(asTenant(record).copyright) }}
                  </Descriptions.Item>
                  <Descriptions.Item label="连接串" :span="2">
                    <span class="mono-break">
                      {{ getValueText(asTenant(record).connection) }}
                    </span>
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
          <template v-else-if="column.key === 'status'">
            <Switch
              :checked="asTenant(record).status === ENABLED"
              :disabled="
                asTenant(record).id === DEFAULT_TENANT_ID ||
                !can('sysTenant:setStatus')
              "
              size="small"
              @change="
                (checked) => changeStatus(asTenant(record), Boolean(checked))
              "
            />
          </template>
          <template v-else-if="column.key === 'modifyRecord'">
            <Popover
              overlay-class-name="tenant-record-popover"
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
                    <Tag>
                      {{ getValueText(asTenant(record).createUserName) }}
                    </Tag>
                  </Descriptions.Item>
                  <Descriptions.Item label="创建时间">
                    <Tag>{{ getValueText(asTenant(record).createTime) }}</Tag>
                  </Descriptions.Item>
                  <Descriptions.Item label="修改者">
                    <Tag>
                      {{ getValueText(asTenant(record).updateUserName) }}
                    </Tag>
                  </Descriptions.Item>
                  <Descriptions.Item label="修改时间">
                    <Tag>{{ getValueText(asTenant(record).updateTime) }}</Tag>
                  </Descriptions.Item>
                  <Descriptions.Item label="备注" :span="2">
                    {{ getValueText(asTenant(record).remark) }}
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
                v-if="can('sysTenant:createDb')"
                danger
                :disabled="
                  asTenant(record).tenantType === ID_TENANT ||
                  isTenantBusy(asTenant(record))
                "
                :loading="isTenantActionLoading('createDb', asTenant(record))"
                size="small"
                type="link"
                @click="confirmCreateDb(asTenant(record))"
              >
                <template #icon>
                  <IconifyIcon icon="lucide:database" />
                </template>
                创建库
              </Button>
              <Tooltip title="编辑">
                <Button
                  v-if="can('sysTenant:update')"
                  size="small"
                  type="link"
                  @click="openEditTenant(asTenant(record))"
                >
                  <template #icon>
                    <IconifyIcon icon="lucide:square-pen" />
                  </template>
                  编辑
                </Button>
              </Tooltip>
              <Dropdown
                :disabled="isTenantBusy(asTenant(record))"
                trigger="click"
              >
                <Button
                  :loading="isTenantBusy(asTenant(record))"
                  size="small"
                  type="link"
                >
                  <template #icon>
                    <IconifyIcon icon="lucide:ellipsis" />
                  </template>
                </Button>
                <template #overlay>
                  <Menu
                    @click="
                      ({ key }) =>
                        handleTenantAction(String(key), asTenant(record))
                    "
                  >
                    <Menu.Item v-if="can('sysTenant:goTenant')" key="go">
                      <template #icon>
                        <IconifyIcon icon="lucide:building-2" />
                      </template>
                      进入租管端
                    </Menu.Item>
                    <Menu.Item
                      v-if="can('sysTenant:changeTenant')"
                      key="change"
                    >
                      <template #icon>
                        <IconifyIcon icon="lucide:repeat-2" />
                      </template>
                      切换租户
                    </Menu.Item>
                    <Menu.Item v-if="can('sysTenant:grantMenu')" key="grant">
                      <template #icon>
                        <IconifyIcon icon="lucide:key-round" />
                      </template>
                      授权菜单
                    </Menu.Item>
                    <Menu.Item
                      v-if="can('sysTenant:syncGrantMenu')"
                      key="syncGrant"
                    >
                      <template #icon>
                        <IconifyIcon icon="lucide:refresh-cw" />
                      </template>
                      同步授权
                    </Menu.Item>
                    <Menu.Item v-if="can('sysTenant:resetPwd')" key="resetPwd">
                      <template #icon>
                        <IconifyIcon icon="lucide:rotate-ccw" />
                      </template>
                      重置密码
                    </Menu.Item>
                    <Menu.Item v-if="can('sysTenant:delete')" key="delete">
                      <template #icon>
                        <IconifyIcon icon="lucide:trash-2" />
                      </template>
                      删除租户
                    </Menu.Item>
                  </Menu>
                </template>
              </Dropdown>
            </Space>
          </template>
        </template>
      </Table>

      <div class="table-footer">
        <Pagination
          v-bind="ADMIN_PAGINATION_PROPS"
          v-model:current="pagination.page"
          v-model:page-size="pagination.pageSize"
          :show-total="(total) => `共 ${total} 条`"
          :total="pagination.total"
          size="small"
          @change="handlePageChange"
        />
      </div>
    </section>

    <Modal
      v-model:open="modalOpen"
      :body-style="{ padding: '14px 20px' }"
      :footer="null"
      :mask-closable="false"
      :title="tenantModalTitle"
      centered
      class="tenant-modal"
      destroy-on-close
      :width="760"
      @cancel="formRef?.clearValidate()"
    >
      <Form
        ref="formRef"
        :model="formState"
        :rules="formRules"
        layout="vertical"
      >
        <Tabs v-model:active-key="activeFormTab">
          <Tabs.TabPane key="basic" tab="基本信息">
            <Row :gutter="16">
              <Col :span="12">
                <Form.Item label="租户类型" name="tenantType">
                  <Radio.Group
                    v-model:value="formState.tenantType"
                    :disabled="!!formState.id"
                    :options="tenantTypeOptions"
                  />
                </Form.Item>
              </Col>
              <Col :span="12">
                <Form.Item label="租户名称" name="name">
                  <Input v-model:value="formState.name" allow-clear />
                </Form.Item>
              </Col>
              <Col :span="12">
                <Form.Item label="租管账号" name="adminAccount">
                  <Input v-model:value="formState.adminAccount" allow-clear />
                </Form.Item>
              </Col>
              <Col :span="12">
                <Form.Item label="电话" name="phone">
                  <Input v-model:value="formState.phone" allow-clear />
                </Form.Item>
              </Col>
              <Col :span="12">
                <Form.Item label="数据库类型" name="dbType">
                  <Select
                    v-model:value="formState.dbType"
                    :disabled="isIdTenantForm"
                    :options="dbTypeOptions"
                    allow-clear
                  />
                </Form.Item>
              </Col>
              <Col :span="12">
                <Form.Item label="主机 Host" name="host">
                  <Input v-model:value="formState.host" allow-clear />
                </Form.Item>
              </Col>
              <Col :span="24">
                <Form.Item label="连接字符串" name="connection">
                  <Input.TextArea
                    v-model:value="formState.connection"
                    :auto-size="{ minRows: 2, maxRows: 4 }"
                    :disabled="isIdTenantForm"
                    allow-clear
                  />
                </Form.Item>
              </Col>
              <Col :span="24">
                <Form.Item label="从库连接串" name="slaveConnections">
                  <Input.TextArea
                    v-model:value="formState.slaveConnections"
                    :auto-size="{ minRows: 2, maxRows: 4 }"
                    :disabled="isIdTenantForm"
                    allow-clear
                    placeholder="格式：[{'HitRate':10, 'ConnectionString':'xxx'},{'HitRate':10, 'ConnectionString':'xxx'}]"
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
              <Col :span="24">
                <Form.Item label="备注" name="remark">
                  <Input.TextArea
                    v-model:value="formState.remark"
                    :auto-size="{ minRows: 2, maxRows: 4 }"
                    allow-clear
                  />
                </Form.Item>
              </Col>
            </Row>
          </Tabs.TabPane>

          <Tabs.TabPane key="site" tab="站点信息">
            <Row :gutter="16">
              <Col :span="24">
                <Form.Item label="Logo" name="logo">
                  <Upload
                    accept=".jpg,.jpeg,.png"
                    :before-upload="beforeLogoUpload"
                    :max-count="1"
                    :show-upload-list="false"
                  >
                    <div class="logo-upload">
                      <Avatar
                        v-if="formState.logo"
                        shape="square"
                        :size="58"
                        :src="formState.logo"
                      />
                      <IconifyIcon v-else icon="lucide:image-plus" />
                    </div>
                  </Upload>
                </Form.Item>
              </Col>
              <Col :span="12">
                <Form.Item label="标题" name="title">
                  <Input
                    v-model:value="formState.title"
                    allow-clear
                    :maxlength="32"
                  />
                </Form.Item>
              </Col>
              <Col :span="12">
                <Form.Item label="副标题" name="viceTitle">
                  <Input
                    v-model:value="formState.viceTitle"
                    allow-clear
                    :maxlength="32"
                  />
                </Form.Item>
              </Col>
              <Col :span="24">
                <Form.Item label="副标题描述" name="viceDesc">
                  <Input
                    v-model:value="formState.viceDesc"
                    allow-clear
                    :maxlength="64"
                  />
                </Form.Item>
              </Col>
              <Col :span="12">
                <Form.Item label="启用注册" name="enableReg">
                  <Radio.Group
                    v-model:value="formState.enableReg"
                    :options="yesNoOptions"
                  />
                </Form.Item>
              </Col>
              <Col v-if="formState.enableReg === YES" :span="12">
                <Form.Item label="默认注册方案" name="regWayId">
                  <Select
                    v-model:value="formState.regWayId"
                    :loading="regWayLoading"
                    :options="regWayOptions"
                    allow-clear
                    placeholder="请选择默认注册方案"
                  />
                </Form.Item>
              </Col>
              <Col :span="12">
                <Form.Item label="水印" name="watermark">
                  <Input
                    v-model:value="formState.watermark"
                    allow-clear
                    :maxlength="32"
                  />
                </Form.Item>
              </Col>
              <Col :span="24">
                <Form.Item label="版权信息" name="copyright">
                  <Input
                    v-model:value="formState.copyright"
                    allow-clear
                    :maxlength="64"
                  />
                </Form.Item>
              </Col>
              <Col :span="12">
                <Form.Item label="备案号" name="icp">
                  <Input
                    v-model:value="formState.icp"
                    allow-clear
                    :maxlength="32"
                  />
                </Form.Item>
              </Col>
              <Col :span="12">
                <Form.Item label="ICP地址" name="icpUrl">
                  <Input
                    v-model:value="formState.icpUrl"
                    allow-clear
                    :maxlength="32"
                  />
                </Form.Item>
              </Col>
            </Row>
          </Tabs.TabPane>
        </Tabs>
      </Form>

      <div class="modal-footer">
        <Space>
          <Button @click="modalOpen = false">取消</Button>
          <Button :loading="submitLoading" type="primary" @click="submitTenant">
            确定
          </Button>
        </Space>
      </div>
    </Modal>

    <Modal
      v-model:open="grantModalOpen"
      :body-style="{ padding: '14px 20px' }"
      :footer="null"
      :mask-closable="false"
      :title="`授权菜单：${currentGrantTenant?.name ?? ''}`"
      centered
      class="grant-modal"
      destroy-on-close
      :width="920"
    >
      <div class="grant-note">
        授权菜单会真实写入租户权限表，提交前会自动去重；同步授权请仅在升级后补齐菜单时使用。
      </div>

      <div class="grant-toolbar">
        <Input
          v-model:value="menuFilterText"
          allow-clear
          placeholder="搜索菜单、路径、权限"
        >
          <template #prefix>
            <IconifyIcon icon="lucide:search" />
          </template>
        </Input>
        <Space>
          <Button size="small" @click="expandAllMenus">全部展开</Button>
          <Button size="small" @click="collapseAllMenus">全部折叠</Button>
          <Button
            size="small"
            :loading="menuLoading"
            @click="refreshGrantMenus"
          >
            刷新
          </Button>
        </Space>
      </div>

      <div class="menu-tree-shell" :class="{ 'is-loading': menuLoading }">
        <div v-if="filteredMenuTree.length > 0" class="grant-menu-grid">
          <GrantMenuNode
            v-for="rootMenu in filteredMenuTree"
            :key="rootMenu.id"
            :checked-keys="checkedMenuKeys"
            :expanded-keys="expandedMenuKeys"
            :item="rootMenu"
            :level="0"
            @check="setMenuChecked"
            @toggle="toggleMenuExpand"
          />
        </div>
        <div v-else class="grant-empty">暂无匹配菜单</div>
      </div>

      <div class="modal-footer">
        <Space>
          <Tag class="grant-selected-count" color="blue">
            已选 {{ checkedMenuKeys.length }} 项
          </Tag>
          <Button @click="grantModalOpen = false">取消</Button>
          <Button
            :loading="grantSubmitLoading"
            type="primary"
            @click="submitGrantMenu"
          >
            确定
          </Button>
        </Space>
      </div>
    </Modal>
  </div>
</template>

<style scoped>
.tenant-page {
  min-height: 100%;
  padding: 12px;
  background: hsl(var(--muted) / 35%);
}

.panel {
  min-width: 0;
  padding: 12px;
  background: hsl(var(--background));
  border: 1px solid hsl(var(--border) / 72%);
  border-radius: 8px;
}

.panel-head {
  display: flex;
  gap: 12px;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 10px;
}

.panel-title {
  font-size: 14px;
  font-weight: 650;
  color: hsl(var(--foreground));
}

.panel-subtitle {
  margin-top: 2px;
  font-size: 12px;
  color: hsl(var(--muted-foreground));
}

.query-form {
  margin-bottom: 2px;
}

.table-footer {
  display: flex;
  justify-content: flex-end;
  padding-top: 12px;
}

.modify-record {
  width: 360px;
}

.site-record {
  width: 520px;
}

.mono-break {
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
  font-size: 12px;
  word-break: break-all;
}

.logo-upload {
  display: grid;
  place-items: center;
  width: 70px;
  height: 70px;
  color: hsl(var(--muted-foreground));
  cursor: pointer;
  border: 1px dashed hsl(var(--border));
  border-radius: 8px;
}

.grant-note {
  padding: 8px 10px;
  margin-bottom: 10px;
  font-size: 12px;
  line-height: 1.6;
  color: hsl(var(--muted-foreground));
  background: hsl(var(--primary) / 6%);
  border: 1px solid hsl(var(--primary) / 18%);
  border-radius: 7px;
}

.grant-toolbar {
  display: grid;
  grid-template-columns: minmax(260px, 1fr) auto;
  gap: 10px;
  margin-bottom: 10px;
}

.menu-tree-shell {
  height: 440px;
  padding: 8px;
  overflow: auto;
  background:
    linear-gradient(180deg, hsl(var(--muted) / 30%), transparent 58px),
    hsl(var(--background));
  border: 1px solid hsl(var(--border) / 75%);
  border-radius: 8px;
}

.menu-tree-shell.is-loading {
  pointer-events: none;
  opacity: 0.62;
}

:global(.grant-modal .grant-menu-grid) {
  min-width: 820px;
  padding: 2px 0 6px;
}

:global(.grant-modal .grant-node) {
  --grant-indent: calc(var(--grant-level) * 24px);

  position: relative;
}

:global(.grant-modal .grant-node-row) {
  display: flex;
  gap: 6px;
  align-items: center;
  min-height: 30px;
  padding-left: var(--grant-indent);
  white-space: nowrap;
  border-radius: 6px;
}

:global(.grant-modal .grant-node-row:hover) {
  background: hsl(var(--muted) / 45%);
}

:global(.grant-modal .grant-node-switch) {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 18px;
  height: 18px;
  padding: 0;
  color: hsl(var(--muted-foreground));
  cursor: pointer;
  background: transparent;
  border: 0;
  border-radius: 5px;
}

:global(.grant-modal .grant-node-switch:hover) {
  color: hsl(var(--primary));
  background: hsl(var(--primary) / 10%);
}

:global(.grant-modal .grant-node-switch.is-placeholder) {
  cursor: default;
  opacity: 0;
}

:global(.grant-modal .grant-node-icon) {
  display: inline-flex;
  flex: 0 0 auto;
  font-size: 15px;
  color: hsl(var(--muted-foreground));
}

:global(.grant-modal .grant-node.is-dir > .grant-node-row .grant-node-title) {
  font-weight: 650;
}

:global(.grant-modal .grant-node-title) {
  font-size: 14px;
  color: hsl(var(--foreground));
}

:global(.grant-modal .grant-path-tag) {
  margin-inline-start: 2px;
  font-family:
    ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, 'Liberation Mono',
    monospace;
}

:global(.grant-modal .grant-action-row) {
  display: flex;
  flex-wrap: wrap;
  gap: 7px 18px;
  padding: 2px 0 8px calc(var(--grant-indent) + 58px);
}

:global(.grant-modal .grant-action-pill) {
  display: inline-flex;
  gap: 5px;
  align-items: center;
  height: 24px;
  padding: 0 7px 0 4px;
  color: hsl(var(--foreground));
  white-space: nowrap;
  cursor: pointer;
  border: 1px solid transparent;
  border-radius: 6px;
}

:global(.grant-modal .grant-action-pill .ant-checkbox-wrapper) {
  display: inline-flex;
  align-items: center;
  line-height: 1;
}

:global(.grant-modal .grant-action-pill:hover) {
  background: hsl(var(--primary) / 7%);
  border-color: hsl(var(--primary) / 22%);
}

:global(.grant-modal .grant-action-pill.is-checked) {
  color: hsl(var(--primary));
}

:global(.grant-modal .grant-action-title) {
  font-size: 13px;
  line-height: 1;
}

:global(.grant-modal .grant-node-children) {
  position: relative;
}

:global(.grant-modal .grant-empty) {
  display: grid;
  place-items: center;
  min-height: 320px;
  color: hsl(var(--muted-foreground));
}

:global(.grant-modal .grant-selected-count) {
  margin-inline-end: 4px;
  background: hsl(var(--primary) / 7%);
  border-color: hsl(var(--primary) / 22%);
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  padding: 10px 20px;
  margin: 14px -20px -14px;
  background: hsl(var(--background));
  border-top: 1px solid hsl(var(--border) / 72%);
}

:global(.tenant-modal) {
  width: min(760px, calc(100vw - 32px)) !important;
}

:global(.grant-modal) {
  width: min(920px, calc(100vw - 32px)) !important;
}

:global(.tenant-modal .ant-modal-content),
:global(.grant-modal .ant-modal-content) {
  border-radius: 8px;
}

:global(.tenant-action-confirm) {
  font-size: 13px;
  line-height: 1.7;
  color: hsl(var(--foreground));
}

:global(.tenant-action-confirm p) {
  margin: 0 0 6px;
  font-weight: 600;
}

:global(.tenant-action-confirm ul) {
  padding-left: 18px;
  margin: 0;
  color: hsl(var(--muted-foreground));
}

:global(.tenant-action-confirm li + li) {
  margin-top: 3px;
}

:global(.tenant-record-popover .ant-popover-inner) {
  padding: 8px;
  border: 1px solid hsl(var(--border));
  border-radius: 8px;
  box-shadow:
    0 12px 28px rgb(15 23 42 / 12%),
    0 2px 8px rgb(15 23 42 / 8%);
}

:global(.tenant-record-popover .ant-popover-inner-content) {
  padding: 0;
}

:global(.tenant-record-popover) {
  z-index: 1060;
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
  flex: 1;
  min-width: 0;
  height: 32px;
  padding-inline: 5px 8px;
  border-radius: 8px;
}

:deep(.ant-tree .ant-tree-node-content-wrapper:hover) {
  background: hsl(var(--accent) / 72%);
}
</style>
