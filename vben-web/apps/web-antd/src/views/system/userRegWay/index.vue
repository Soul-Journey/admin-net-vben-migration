<script setup lang="ts">
import type { FormInstance, TableColumnsType } from 'ant-design-vue';
import type { Rule } from 'ant-design-vue/es/form';

import type {
  SaveRegWayParams,
  SysOrgRecord,
  SysPosRecord,
  SysRoleRecord,
  SysTenantOption,
  SysUserRegWayRecord,
} from '#/api';

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
  Row,
  Select,
  Space,
  Table,
  Tag,
  TreeSelect,
} from 'ant-design-vue';

import {
  addRegWayApi,
  deleteRegWayApi,
  getDictDataByCodeApi,
  getRoleListApi,
  getTenantListApi,
  listOrgsApi,
  listPositionsApi,
  listRegWaysApi,
  updateRegWayApi,
} from '#/api';

defineOptions({ name: 'AdminNetSystemUserRegWay' });

type RegWayFormState = Partial<SaveRegWayParams> & { id?: number };
type OrgOption = {
  children?: OrgOption[];
  label: string;
  value: number;
};

const MEMBER_ACCOUNT = 666;
const NORMAL_ACCOUNT = 777;
const SYS_ADMIN_ACCOUNT = 888;
const SUPER_ADMIN_ACCOUNT = 999;

const { hasAccessByCodes } = useAccess();
const userStore = useUserStore();

const loading = ref(false);
const optionLoading = ref(false);
const tenantLoading = ref(false);
const submitLoading = ref(false);
const modalOpen = ref(false);
const modalTitle = ref('新增注册方案');
const formRef = ref<FormInstance>();
const regWays = ref<SysUserRegWayRecord[]>([]);
const tenants = ref<SysTenantOption[]>([]);
const roles = ref<SysRoleRecord[]>([]);
const positions = ref<SysPosRecord[]>([]);
const orgTree = ref<SysOrgRecord[]>([]);
const accountTypeDict = ref<
  Array<{ code?: string; label: string; tagType?: string; value: string }>
>([]);
const formState = reactive<RegWayFormState>({});

const query = reactive({
  keyword: '',
  name: '',
  tenantId: undefined as number | undefined,
});

const columns: TableColumnsType<SysUserRegWayRecord> = [
  { key: 'index', title: '序号', width: 58 },
  { dataIndex: 'name', key: 'name', title: '方案名称', width: 190 },
  { key: 'accountType', title: '账号类型', width: 112 },
  { dataIndex: 'orgName', key: 'orgName', title: '机构', width: 150 },
  { dataIndex: 'roleName', key: 'roleName', title: '角色', width: 150 },
  { dataIndex: 'posName', key: 'posName', title: '职位', width: 150 },
  { dataIndex: 'orderNo', key: 'orderNo', title: '排序', width: 76 },
  { key: 'modifyRecord', title: '修改记录', width: 112 },
  { key: 'actions', fixed: 'right', title: '操作', width: 156 },
];

const formRules: Record<string, Rule[]> = {
  accountType: [
    {
      message: '请选择账号类型',
      required: true,
      trigger: 'change',
      type: 'number',
    },
  ],
  name: [
    {
      message: '请输入方案名称',
      required: true,
      trigger: 'blur',
      type: 'string',
    },
  ],
  orgId: [
    {
      message: '请选择绑定机构',
      required: true,
      trigger: 'change',
      type: 'number',
    },
  ],
  posId: [
    {
      message: '请选择绑定职位',
      required: true,
      trigger: 'change',
      type: 'number',
    },
  ],
  roleId: [
    {
      message: '请选择绑定角色',
      required: true,
      trigger: 'change',
      type: 'number',
    },
  ],
};

const fallbackAccountTypeOptions: Array<{
  code?: string;
  label: string;
  tagType?: string;
  value: number;
}> = [
  { code: 'Member', label: '会员', value: MEMBER_ACCOUNT },
  { code: 'NormalUser', label: '普通账号', value: NORMAL_ACCOUNT },
  { code: 'SysAdmin', label: '系统管理员', value: SYS_ADMIN_ACCOUNT },
  { code: 'SuperAdmin', label: '超级管理员', value: SUPER_ADMIN_ACCOUNT },
];

const isSuperAdmin = computed(
  () =>
    Number((userStore.userInfo as any)?.accountType) === SUPER_ADMIN_ACCOUNT,
);

const tenantOptions = computed(() =>
  tenants.value.map((item) => ({
    label: item.host ? `${item.label} (${item.host})` : item.label,
    value: item.value,
  })),
);

const accountTypeOptions = computed(() => {
  const source =
    accountTypeDict.value.length > 0
      ? accountTypeDict.value.map((item) => ({
          code: item.code,
          label: item.label,
          tagType: item.tagType,
          value: Number(item.value),
        }))
      : fallbackAccountTypeOptions;

  return source
    .filter((item) => Number.isFinite(item.value))
    .map((item) => {
      const forbidden =
        item.value === SYS_ADMIN_ACCOUNT ||
        item.value === SUPER_ADMIN_ACCOUNT ||
        item.code === 'SysAdmin' ||
        item.code === 'SuperAdmin';

      return {
        disabled: forbidden,
        label: item.label,
        title: forbidden ? '后端禁止注册方案分配管理员账号' : undefined,
        value: item.value,
      };
    });
});

const accountTypeDisplayOptions = computed(() =>
  accountTypeOptions.value.map((item) => ({
    label: item.label,
    value: item.value,
  })),
);

const roleOptions = computed(() =>
  roles.value
    .filter(
      (item) => !formState.tenantId || item.tenantId === formState.tenantId,
    )
    .map((item) => ({ label: item.name, value: item.id })),
);

const positionOptions = computed(() =>
  positions.value.map((item) => ({ label: item.name, value: item.id })),
);

const orgOptions = computed<OrgOption[]>(() => toOrgOptions(orgTree.value));

function can(code: string) {
  return hasAccessByCodes([code]);
}

function asRegWay(record: unknown) {
  return record as SysUserRegWayRecord;
}

function getValueText(value?: null | number | string) {
  return value === undefined || value === null || value === '' ? '无' : value;
}

function getAccountTypeLabel(type?: number) {
  return (
    accountTypeDisplayOptions.value.find((item) => item.value === type)
      ?.label ?? `类型 ${type ?? '未知'}`
  );
}

function getAccountTypeColor(type?: number) {
  if (type === MEMBER_ACCOUNT) return 'purple';
  if (type === NORMAL_ACCOUNT) return 'blue';
  if (type === SYS_ADMIN_ACCOUNT) return 'red';
  return 'default';
}

function toOrgOptions(items: SysOrgRecord[] = []): OrgOption[] {
  return items.map((item) => ({
    children: item.children?.length ? toOrgOptions(item.children) : undefined,
    label: item.name,
    value: item.id,
  }));
}

function resetFormState(values: RegWayFormState) {
  for (const key of Object.keys(formState)) {
    delete formState[key as keyof RegWayFormState];
  }
  Object.assign(formState, values);
}

async function loadTenants() {
  if (!isSuperAdmin.value) {
    return;
  }
  tenantLoading.value = true;
  try {
    tenants.value = await getTenantListApi();
    if (!query.tenantId && tenants.value[0]?.value) {
      query.tenantId = tenants.value[0].value;
    }
  } finally {
    tenantLoading.value = false;
  }
}

async function loadAccountTypes() {
  const data = await getDictDataByCodeApi('AccountTypeEnum', 1);
  accountTypeDict.value = data.map((item) => ({
    code: item.code,
    label: item.label,
    tagType: item.tagType,
    value: item.value,
  }));
}

async function loadRegWays() {
  if (!can('sysUserRegWay:list')) {
    return;
  }
  loading.value = true;
  try {
    regWays.value = await listRegWaysApi({
      keyword: query.keyword || undefined,
      name: query.name || undefined,
      tenantId: query.tenantId,
    });
  } finally {
    loading.value = false;
  }
}

async function loadFormOptions(tenantId?: number) {
  optionLoading.value = true;
  try {
    const [roleList, positionList, orgList] = await Promise.all([
      getRoleListApi(),
      listPositionsApi({ tenantId }),
      listOrgsApi({ id: 0, tenantId }),
    ]);
    roles.value = roleList as SysRoleRecord[];
    positions.value = positionList;
    orgTree.value = orgList;
  } finally {
    optionLoading.value = false;
  }
}

async function handleQuery() {
  await loadRegWays();
}

async function resetQuery() {
  query.keyword = '';
  query.name = '';
  await loadRegWays();
}

async function openCreateRegWay() {
  if (isSuperAdmin.value && !query.tenantId) {
    message.warning('请先选择租户');
    return;
  }
  modalTitle.value = '新增注册方案';
  resetFormState({
    accountType: NORMAL_ACCOUNT,
    orderNo: 100,
    tenantId: query.tenantId,
  });
  await loadFormOptions(formState.tenantId);
  modalOpen.value = true;
}

async function openEditRegWay(record: SysUserRegWayRecord) {
  modalTitle.value = '编辑注册方案';
  resetFormState({
    ...record,
    accountType: record.accountType ?? NORMAL_ACCOUNT,
    orderNo: record.orderNo ?? 100,
    tenantId: record.tenantId ?? query.tenantId,
  });
  await loadFormOptions(formState.tenantId);
  modalOpen.value = true;
}

async function handleFormTenantChange(value: unknown) {
  const tenantId = typeof value === 'number' ? value : undefined;
  formState.tenantId = tenantId;
  formState.roleId = undefined;
  formState.orgId = undefined;
  formState.posId = undefined;
  await loadFormOptions(tenantId);
}

async function submitRegWay() {
  await formRef.value?.validate();
  submitLoading.value = true;
  try {
    const payload = {
      ...formState,
      orderNo: formState.orderNo ?? 100,
      tenantId: formState.tenantId ?? query.tenantId,
    } as SaveRegWayParams & { id?: number };

    if (payload.id) {
      await updateRegWayApi(payload as SaveRegWayParams & { id: number });
      message.success('注册方案已更新');
    } else {
      await addRegWayApi(payload);
      message.success('注册方案已新增');
    }
    modalOpen.value = false;
    await loadRegWays();
  } finally {
    submitLoading.value = false;
  }
}

function confirmDelete(record: SysUserRegWayRecord) {
  Modal.confirm({
    centered: true,
    content: `确定删除注册方案「${record.name}」吗？如果租户正在使用该方案，后端会同步关闭这些租户的注册功能并清空默认注册方案。`,
    okButtonProps: { danger: true },
    okText: '删除',
    onOk: async () => {
      await deleteRegWayApi(record.id);
      message.success('注册方案已删除');
      await loadRegWays();
    },
    title: '删除注册方案',
  });
}

onMounted(async () => {
  await Promise.all([loadAccountTypes(), loadTenants()]);
  await loadRegWays();
});
</script>

<template>
  <div class="reg-way-page">
    <section class="panel">
      <div class="panel-head">
        <div>
          <div class="panel-title">注册方案</div>
          <div class="panel-subtitle">
            配置用户注册后的账号类型、角色、机构和职位
          </div>
        </div>
      </div>

      <Form :model="query" class="query-form" layout="inline">
        <Form.Item v-if="isSuperAdmin" label="租户">
          <Select
            v-model:value="query.tenantId"
            :loading="tenantLoading"
            :options="tenantOptions"
            allow-clear
            class="tenant-select"
            placeholder="租户"
            @change="handleQuery"
          />
        </Form.Item>
        <Form.Item label="关键字">
          <Input
            v-model:value="query.keyword"
            allow-clear
            placeholder="关键字"
            @press-enter="handleQuery"
          />
        </Form.Item>
        <Form.Item label="名称">
          <Input
            v-model:value="query.name"
            allow-clear
            placeholder="方案名称"
            @press-enter="handleQuery"
          />
        </Form.Item>
        <Form.Item>
          <Space>
            <Button
              v-if="can('sysUserRegWay:list')"
              :loading="loading"
              type="primary"
              @click="handleQuery"
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
              v-if="can('sysUserRegWay:add')"
              type="primary"
              @click="openCreateRegWay"
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
        :data-source="regWays"
        :loading="loading"
        :pagination="false"
        :scroll="{ x: 1090 }"
        row-key="id"
        size="small"
      >
        <template #bodyCell="{ column, index, record }">
          <template v-if="column.key === 'index'">
            {{ index + 1 }}
          </template>
          <template v-else-if="column.key === 'accountType'">
            <Tag :color="getAccountTypeColor(asRegWay(record).accountType)">
              {{ getAccountTypeLabel(asRegWay(record).accountType) }}
            </Tag>
          </template>
          <template v-else-if="column.key === 'modifyRecord'">
            <Popover
              overlay-class-name="reg-way-popover"
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
                      {{ getValueText(asRegWay(record).createUserName) }}
                    </Tag>
                  </Descriptions.Item>
                  <Descriptions.Item label="创建时间">
                    <Tag>{{ getValueText(asRegWay(record).createTime) }}</Tag>
                  </Descriptions.Item>
                  <Descriptions.Item label="修改者">
                    <Tag>
                      {{ getValueText(asRegWay(record).updateUserName) }}
                    </Tag>
                  </Descriptions.Item>
                  <Descriptions.Item label="修改时间">
                    <Tag>{{ getValueText(asRegWay(record).updateTime) }}</Tag>
                  </Descriptions.Item>
                  <Descriptions.Item label="备注" :span="2">
                    {{ getValueText(asRegWay(record).remark) }}
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
            <Space :size="4">
              <Button
                v-if="can('sysUserRegWay:update')"
                size="small"
                type="link"
                @click="openEditRegWay(asRegWay(record))"
              >
                <template #icon>
                  <IconifyIcon icon="lucide:square-pen" />
                </template>
                编辑
              </Button>
              <Button
                v-if="can('sysUserRegWay:delete')"
                danger
                size="small"
                type="link"
                @click="confirmDelete(asRegWay(record))"
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
    </section>

    <Modal
      v-model:open="modalOpen"
      :body-style="{ padding: '14px 20px' }"
      :footer="null"
      :mask-closable="false"
      :title="modalTitle"
      centered
      class="reg-way-modal"
      destroy-on-close
      :width="640"
      @cancel="formRef?.clearValidate()"
    >
      <div class="form-note">
        该方案会作为租户注册入口的默认分配规则，保存前请确认角色、机构和职位属于同一租户。账号类型来自
        AccountTypeEnum
        字典；系统管理员和超级管理员会显示但不可选择，因为后端禁止注册方案分配管理员账号。
      </div>
      <Form
        ref="formRef"
        :model="formState"
        :rules="formRules"
        layout="vertical"
      >
        <Row :gutter="16">
          <Col v-if="isSuperAdmin" :span="24">
            <Form.Item label="租户" name="tenantId">
              <Select
                v-model:value="formState.tenantId"
                :disabled="!!formState.id"
                :loading="tenantLoading"
                :options="tenantOptions"
                allow-clear
                placeholder="请选择租户"
                @change="handleFormTenantChange"
              />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="方案名称" name="name">
              <Input
                v-model:value="formState.name"
                allow-clear
                :maxlength="32"
              />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="账号类型" name="accountType">
              <Select
                v-model:value="formState.accountType"
                :options="accountTypeOptions"
                placeholder="请选择账号类型"
              />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="绑定角色" name="roleId">
              <Select
                v-model:value="formState.roleId"
                :loading="optionLoading"
                :options="roleOptions"
                allow-clear
                placeholder="绑定角色"
              />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="绑定职位" name="posId">
              <Select
                v-model:value="formState.posId"
                :loading="optionLoading"
                :options="positionOptions"
                allow-clear
                placeholder="绑定职位"
              />
            </Form.Item>
          </Col>
          <Col :span="24">
            <Form.Item label="绑定机构" name="orgId">
              <TreeSelect
                v-model:value="formState.orgId"
                :loading="optionLoading"
                :tree-data="orgOptions"
                allow-clear
                class="w-full"
                placeholder="绑定机构"
                show-search
                tree-default-expand-all
                tree-node-filter-prop="label"
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
          <Col :span="24">
            <Form.Item label="备注" name="remark">
              <Input.TextArea
                v-model:value="formState.remark"
                :auto-size="{ minRows: 2, maxRows: 4 }"
                allow-clear
                placeholder="请输入备注内容"
              />
            </Form.Item>
          </Col>
        </Row>
      </Form>

      <div class="modal-footer">
        <Space>
          <Button @click="modalOpen = false">取消</Button>
          <Button :loading="submitLoading" type="primary" @click="submitRegWay">
            确定
          </Button>
        </Space>
      </div>
    </Modal>
  </div>
</template>

<style scoped>
.reg-way-page {
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
  font-weight: 700;
  color: hsl(var(--foreground));
}

.panel-subtitle {
  margin-top: 4px;
  font-size: 12px;
  color: hsl(var(--muted-foreground));
}

.query-form {
  gap: 8px 0;
  margin-bottom: 10px;
}

.tenant-select {
  width: 260px;
}

.modify-record {
  width: 390px;
}

.form-note {
  padding: 8px 10px;
  margin-bottom: 12px;
  font-size: 12px;
  line-height: 1.6;
  color: hsl(var(--muted-foreground));
  background: hsl(var(--primary) / 6%);
  border: 1px solid hsl(var(--primary) / 18%);
  border-radius: 7px;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  padding: 10px 20px;
  margin: 14px -20px -14px;
  background: hsl(var(--background));
  border-top: 1px solid hsl(var(--border) / 72%);
}

:global(.reg-way-modal) {
  width: min(640px, calc(100vw - 32px)) !important;
}

:global(.reg-way-modal .ant-modal-content) {
  border-radius: 8px;
}

:global(.reg-way-popover .ant-popover-inner) {
  padding: 8px;
  border: 1px solid hsl(var(--border));
  border-radius: 8px;
  box-shadow:
    0 12px 28px rgb(15 23 42 / 12%),
    0 2px 8px rgb(15 23 42 / 8%);
}

:global(.reg-way-popover .ant-popover-inner-content) {
  padding: 0;
}

@media (max-width: 720px) {
  .tenant-select {
    width: 100%;
  }
}
</style>
