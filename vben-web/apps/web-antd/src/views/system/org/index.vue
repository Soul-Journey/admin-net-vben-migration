<script setup lang="ts">
import type { FormInstance, TableColumnsType, TreeProps } from 'ant-design-vue';
import type { Rule } from 'ant-design-vue/es/form';

import type {
  SaveOrgParams,
  SysDictDataRecord,
  SysOrgRecord,
  SysTenantOption,
} from '#/api';

import { computed, onMounted, reactive, ref } from 'vue';

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
  Popover,
  Radio,
  Row,
  Select,
  Space,
  Table,
  Tag,
  Tooltip,
  Tree,
  TreeSelect,
} from 'ant-design-vue';

import {
  addOrgApi,
  deleteOrgApi,
  getDictDataByCodeApi,
  getTenantListApi,
  listOrgsApi,
  updateOrgApi,
} from '#/api';

defineOptions({ name: 'AdminNetSystemOrg' });

type OrgFormState = Partial<SaveOrgParams> & { id?: number };

const ENABLED = 1;
const DISABLED = 2;
const SUPER_ADMIN_ACCOUNT = 999;

const { hasAccessByCodes } = useAccess();
const userStore = useUserStore();

const loading = ref(false);
const treeLoading = ref(false);
const optionLoading = ref(false);
const submitLoading = ref(false);
const modalOpen = ref(false);
const modalTitle = ref('新增机构');
const formRef = ref<FormInstance>();

const orgs = ref<SysOrgRecord[]>([]);
const orgTree = ref<SysOrgRecord[]>([]);
const orgTypeList = ref<SysDictDataRecord[]>([]);
const tenantList = ref<SysTenantOption[]>([]);
const expandedRowKeys = ref<Array<number | string>>([]);
const expandedTreeKeys = ref<Array<number | string>>([]);
const selectedTreeKeys = ref<Array<number | string>>([]);
const orgFilterText = ref('');
const formState = reactive<OrgFormState>({});

const query = reactive({
  code: '',
  id: 0,
  name: '',
  tenantId: undefined as number | undefined,
  type: undefined as string | undefined,
});

const columns: TableColumnsType<SysOrgRecord> = [
  { dataIndex: 'name', key: 'name', title: '机构名称', width: 220 },
  { dataIndex: 'code', key: 'code', title: '机构编码', width: 150 },
  { dataIndex: 'level', key: 'level', title: '级别', width: 72 },
  { key: 'type', title: '机构类型', width: 120 },
  { dataIndex: 'orderNo', key: 'orderNo', title: '排序', width: 72 },
  { key: 'status', title: '状态', width: 76 },
  { key: 'modifyRecord', title: '修改记录', width: 108 },
  { key: 'actions', fixed: 'right', title: '操作', width: 188 },
];

const formRules: Record<string, Rule[]> = {
  code: [
    {
      message: '请输入机构编码',
      required: true,
      trigger: 'blur',
      type: 'string',
    },
  ],
  name: [
    {
      message: '请输入机构名称',
      required: true,
      trigger: 'blur',
      type: 'string',
    },
  ],
};

const orgTypeOptions = computed(() =>
  orgTypeList.value.map((item) => ({
    label: item.label,
    value: item.value,
  })),
);

const isSuperAdmin = computed(
  () =>
    Number((userStore.userInfo as any)?.accountType) === SUPER_ADMIN_ACCOUNT,
);

const tenantOptions = computed(() =>
  tenantList.value.map((item) => ({
    label: item.host ? `${item.label} (${item.host})` : item.label,
    value: item.value,
  })),
);

const parentOrgTreeData = computed<TreeProps['treeData']>(() => [
  { key: 0, title: '根节点', value: 0 },
  ...(toOrgTreeSelectData(orgTree.value, formState.id) ?? []),
]);

const filteredOrgTree = computed(() =>
  filterOrgTree(orgTree.value, orgFilterText.value),
);

const filteredOrgTreeData = computed<TreeProps['treeData']>(() =>
  toOrgTreeData(filteredOrgTree.value),
);

function can(code: string) {
  return hasAccessByCodes([code]);
}

function asOrg(record: unknown) {
  return record as SysOrgRecord;
}

function getValueText(value?: null | number | string) {
  return value === undefined || value === null || value === '' ? '无' : value;
}

function getStatusMeta(status?: number) {
  return status === ENABLED
    ? { color: 'success', label: '启用' }
    : { color: 'default', label: '禁用' };
}

function getOrgTypeLabel(type?: string) {
  return (
    orgTypeList.value.find((item) => item.value === type)?.label || type || '无'
  );
}

function getAllOrgKeys(items: SysOrgRecord[] = []): Array<number | string> {
  return items.flatMap((item) => [
    item.id,
    ...getAllOrgKeys(item.children ?? []),
  ]);
}

function getRootOrgKeys(items: SysOrgRecord[] = []): Array<number | string> {
  return items.map((item) => item.id);
}

function filterOrgTree(
  items: SysOrgRecord[] = [],
  keyword = '',
): SysOrgRecord[] {
  const normalizedKeyword = keyword.trim().toLowerCase();
  if (!normalizedKeyword) {
    return items;
  }

  return items
    .map((item) => {
      const children = filterOrgTree(item.children ?? [], normalizedKeyword);
      const matched = [item.name, item.code, item.type]
        .filter(Boolean)
        .some((value) =>
          String(value).toLowerCase().includes(normalizedKeyword),
        );
      if (!matched && children.length === 0) {
        return undefined;
      }
      return { ...item, children };
    })
    .filter(Boolean) as SysOrgRecord[];
}

function getOrgIcon(level?: number) {
  if (!level || level <= 1) {
    return 'lucide:building-2';
  }
  if (level === 2) {
    return 'lucide:house';
  }
  return 'lucide:tag';
}

function toOrgTreeData(items: SysOrgRecord[] = []): TreeProps['treeData'] {
  return items.map((item) => ({
    children: toOrgTreeData(item.children),
    icon: getOrgIcon(item.level),
    key: item.id,
    title: item.name,
  }));
}

function toOrgTreeSelectData(
  items: SysOrgRecord[] = [],
  excludeId?: number,
): TreeProps['treeData'] {
  return items
    .filter((item) => item.id !== excludeId)
    .map((item) => ({
      children: toOrgTreeSelectData(item.children, excludeId),
      disabled: item.disabled,
      key: item.id,
      title: item.name,
      value: item.id,
    }));
}

function resetFormState(values: OrgFormState) {
  for (const key of Object.keys(formState)) {
    delete formState[key as keyof OrgFormState];
  }
  Object.assign(formState, values);
}

function makeDefaultOrg(): OrgFormState {
  return {
    code: '',
    level: 1,
    name: '',
    orderNo: 100,
    pid: query.id || 0,
    remark: '',
    status: ENABLED,
    tenantId: query.tenantId,
    type: undefined,
  };
}

async function loadOptions() {
  optionLoading.value = true;
  try {
    const [types, tenants] = await Promise.all([
      getDictDataByCodeApi('org_type', ENABLED),
      isSuperAdmin.value ? getTenantListApi() : Promise.resolve([]),
    ]);
    orgTypeList.value = types;
    tenantList.value = tenants;
    if (isSuperAdmin.value && !query.tenantId && tenants[0]?.value) {
      query.tenantId = tenants[0].value;
    }
  } finally {
    optionLoading.value = false;
  }
}

async function loadOrgTree() {
  treeLoading.value = true;
  try {
    orgTree.value = await listOrgsApi({ id: 0, tenantId: query.tenantId });
    expandedTreeKeys.value = getRootOrgKeys(orgTree.value);
  } finally {
    treeLoading.value = false;
  }
}

async function loadOrgs(updateTree = false) {
  loading.value = true;
  try {
    const data = await listOrgsApi({
      code: query.code || undefined,
      id: query.id,
      name: query.name || undefined,
      tenantId: query.tenantId,
      type: query.type,
    });
    orgs.value = data;
    expandedRowKeys.value =
      query.name || query.code || query.type
        ? getAllOrgKeys(data)
        : getRootOrgKeys(data);
    if (
      updateTree ||
      (query.id === 0 && !query.name && !query.code && !query.type)
    ) {
      orgTree.value = data;
      expandedTreeKeys.value = getRootOrgKeys(data);
    }
  } finally {
    loading.value = false;
  }
}

async function handleSearch() {
  await loadOrgs();
}

async function resetQuery() {
  query.code = '';
  query.id = 0;
  query.name = '';
  query.type = undefined;
  selectedTreeKeys.value = [];
  await loadOrgs();
}

async function handleTenantChange() {
  query.id = 0;
  query.name = '';
  query.code = '';
  query.type = undefined;
  selectedTreeKeys.value = [];
  await loadOrgTree();
  await loadOrgs();
}

async function handleTreeSelect(keys: Array<number | string>) {
  const selected = keys[0];
  query.id = typeof selected === 'number' ? selected : Number(selected || 0);
  selectedTreeKeys.value = keys;
  const node = findOrgById(orgTree.value, query.id);
  query.tenantId = node?.tenantId ?? query.tenantId;
  query.name = '';
  query.code = '';
  query.type = undefined;
  await loadOrgs();
}

function findOrgById(
  items: SysOrgRecord[],
  id: number,
): SysOrgRecord | undefined {
  for (const item of items) {
    if (item.id === id) {
      return item;
    }
    const child = findOrgById(item.children ?? [], id);
    if (child) {
      return child;
    }
  }
  return undefined;
}

function expandAllTree() {
  expandedTreeKeys.value = getAllOrgKeys(filteredOrgTree.value);
}

function collapseAllTree() {
  expandedTreeKeys.value = [];
}

function expandAllRows() {
  expandedRowKeys.value = getAllOrgKeys(orgs.value);
}

function collapseAllRows() {
  expandedRowKeys.value = [];
}

async function refreshAll() {
  await loadOrgTree();
  await loadOrgs();
  message.success('机构数据已刷新');
}

async function openCreateOrg() {
  modalTitle.value = '新增机构';
  resetFormState(makeDefaultOrg());
  if (orgTree.value.length === 0) {
    await loadOrgTree();
  }
  modalOpen.value = true;
}

async function openEditOrg(record: SysOrgRecord) {
  modalTitle.value = '编辑机构';
  resetFormState({
    ...record,
    orderNo: record.orderNo ?? 100,
    status: record.status ?? ENABLED,
  });
  if (orgTree.value.length === 0) {
    await loadOrgTree();
  }
  modalOpen.value = true;
}

async function openCopyOrg(record: SysOrgRecord) {
  modalTitle.value = '复制机构';
  resetFormState({
    ...record,
    id: undefined,
    name: '',
  });
  if (orgTree.value.length === 0) {
    await loadOrgTree();
  }
  modalOpen.value = true;
}

async function submitOrg() {
  await formRef.value?.validate();
  submitLoading.value = true;
  try {
    const payload = {
      ...formState,
      orderNo: formState.orderNo ?? 100,
      pid: formState.pid ?? 0,
      status: formState.status ?? ENABLED,
    } as SaveOrgParams & { id?: number };
    if (payload.id) {
      await updateOrgApi(payload as SaveOrgParams & { id: number });
      message.success('机构已更新');
    } else {
      await addOrgApi(payload);
      message.success('机构已新增');
    }
    modalOpen.value = false;
    await loadOrgs(true);
  } finally {
    submitLoading.value = false;
  }
}

function confirmDelete(record: SysOrgRecord) {
  Modal.confirm({
    centered: true,
    content: `确定删除机构「${record.name}」吗？如果存在用户或租户默认机构，后端会拒绝删除。`,
    okButtonProps: { danger: true },
    okText: '删除',
    title: '删除确认',
    async onOk() {
      await deleteOrgApi(record.id);
      message.success('机构已删除');
      await loadOrgs(true);
    },
  });
}

onMounted(async () => {
  await Promise.all([loadOptions(), loadOrgTree()]);
  await loadOrgs();
});
</script>

<template>
  <div class="org-page">
    <section class="content-grid">
      <aside class="org-panel">
        <div class="panel-head">
          <div>
            <div class="panel-title">机构导航</div>
            <div class="panel-subtitle">点击节点筛选右侧机构</div>
          </div>
        </div>
        <Select
          v-if="isSuperAdmin"
          v-model:value="query.tenantId"
          :loading="optionLoading"
          :options="tenantOptions"
          class="tenant-select"
          placeholder="请选择租户"
          @change="handleTenantChange"
        />
        <Input
          v-model:value="orgFilterText"
          allow-clear
          placeholder="机构名称 / 编码"
        >
          <template #prefix>
            <IconifyIcon icon="lucide:search" />
          </template>
        </Input>
        <div class="tree-tools">
          <Button size="small" @click="expandAllTree">
            <template #icon>
              <IconifyIcon icon="lucide:chevrons-down" />
            </template>
            展开
          </Button>
          <Button size="small" @click="collapseAllTree">
            <template #icon>
              <IconifyIcon icon="lucide:chevrons-up" />
            </template>
            折叠
          </Button>
          <Button size="small" :loading="treeLoading" @click="refreshAll">
            <template #icon>
              <IconifyIcon icon="lucide:refresh-cw" />
            </template>
            刷新
          </Button>
        </div>
        <div class="tree-shell" :class="{ 'is-loading': treeLoading }">
          <Tree
            v-if="filteredOrgTreeData?.length"
            v-model:expanded-keys="expandedTreeKeys"
            v-model:selected-keys="selectedTreeKeys"
            :tree-data="filteredOrgTreeData"
            block-node
            @select="handleTreeSelect"
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
      </aside>

      <main class="table-panel">
        <div class="query-head">
          <Form :model="query" layout="inline">
            <Form.Item label="机构名称">
              <Input
                v-model:value="query.name"
                allow-clear
                placeholder="机构名称"
                @press-enter="handleSearch"
              />
            </Form.Item>
            <Form.Item label="机构类型">
              <Select
                v-model:value="query.type"
                :loading="optionLoading"
                :options="orgTypeOptions"
                allow-clear
                class="type-query"
                placeholder="机构类型"
              />
            </Form.Item>
            <Form.Item>
              <Space :size="8" wrap>
                <Button type="primary" @click="handleSearch">
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
                  v-if="can('sysOrg:add')"
                  type="primary"
                  @click="openCreateOrg"
                >
                  <template #icon>
                    <IconifyIcon icon="lucide:plus" />
                  </template>
                  新增
                </Button>
              </Space>
            </Form.Item>
          </Form>
          <Space :size="6" wrap>
            <Button size="small" @click="expandAllRows">展开</Button>
            <Button size="small" @click="collapseAllRows">折叠</Button>
          </Space>
        </div>

        <Table
          v-model:expanded-row-keys="expandedRowKeys"
          :columns="columns"
          :data-source="orgs"
          :loading="loading"
          :pagination="false"
          :scroll="{ x: 1120 }"
          row-key="id"
          size="small"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'name'">
              <span class="org-name-cell">
                <span class="tree-node-icon-wrap">
                  <IconifyIcon
                    :icon="getOrgIcon(asOrg(record).level)"
                    class="tree-node-icon"
                  />
                </span>
                <span>{{ asOrg(record).name }}</span>
              </span>
            </template>
            <template v-else-if="column.key === 'type'">
              <Tag>{{ getOrgTypeLabel(asOrg(record).type) }}</Tag>
            </template>
            <template v-else-if="column.key === 'status'">
              <Tag :color="getStatusMeta(asOrg(record).status).color">
                {{ getStatusMeta(asOrg(record).status).label }}
              </Tag>
            </template>
            <template v-else-if="column.key === 'modifyRecord'">
              <Popover
                overlay-class-name="org-record-popover"
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
                        {{ getValueText(asOrg(record).createUserName) }}
                      </Tag>
                    </Descriptions.Item>
                    <Descriptions.Item label="创建时间">
                      <Tag>{{ getValueText(asOrg(record).createTime) }}</Tag>
                    </Descriptions.Item>
                    <Descriptions.Item label="修改者">
                      <Tag>
                        {{ getValueText(asOrg(record).updateUserName) }}
                      </Tag>
                    </Descriptions.Item>
                    <Descriptions.Item label="修改时间">
                      <Tag>{{ getValueText(asOrg(record).updateTime) }}</Tag>
                    </Descriptions.Item>
                    <Descriptions.Item label="备注" :span="2">
                      {{ getValueText(asOrg(record).remark) }}
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
                    v-if="can('sysOrg:update')"
                    size="small"
                    type="link"
                    @click="openEditOrg(asOrg(record))"
                  >
                    <template #icon>
                      <IconifyIcon icon="lucide:square-pen" />
                    </template>
                  </Button>
                </Tooltip>
                <Tooltip title="删除">
                  <Button
                    v-if="can('sysOrg:delete')"
                    danger
                    size="small"
                    type="link"
                    @click="confirmDelete(asOrg(record))"
                  >
                    <template #icon>
                      <IconifyIcon icon="lucide:trash-2" />
                    </template>
                  </Button>
                </Tooltip>
                <Tooltip title="复制">
                  <Button
                    v-if="can('sysOrg:add')"
                    size="small"
                    type="link"
                    @click="openCopyOrg(asOrg(record))"
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
      </main>
    </section>

    <Modal
      v-model:open="modalOpen"
      :body-style="{ padding: '16px 20px' }"
      :footer="null"
      :mask-closable="false"
      :title="modalTitle"
      centered
      destroy-on-close
      :width="680"
      @cancel="formRef?.clearValidate()"
    >
      <Form
        ref="formRef"
        :model="formState"
        :rules="formRules"
        layout="vertical"
      >
        <Row :gutter="16">
          <Col :span="24">
            <Form.Item label="上级机构" name="pid">
              <TreeSelect
                v-model:value="formState.pid"
                :tree-data="parentOrgTreeData"
                allow-clear
                show-search
                tree-default-expand-all
              />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="机构名称" name="name">
              <Input v-model:value="formState.name" allow-clear />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="机构编码" name="code">
              <Input v-model:value="formState.code" allow-clear />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="级别" name="level">
              <InputNumber
                v-model:value="formState.level"
                class="w-full"
                :min="0"
              />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="机构类型" name="type">
              <Select
                v-model:value="formState.type"
                :loading="optionLoading"
                :options="orgTypeOptions"
                allow-clear
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
              <Radio.Group v-model:value="formState.status">
                <Radio :value="ENABLED">启用</Radio>
                <Radio :value="DISABLED">禁用</Radio>
              </Radio.Group>
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
      </Form>
      <div class="modal-footer">
        <Space>
          <Button @click="modalOpen = false">取消</Button>
          <Button :loading="submitLoading" type="primary" @click="submitOrg">
            确定
          </Button>
        </Space>
      </div>
    </Modal>
  </div>
</template>

<style scoped>
.org-page {
  min-height: 100%;
  padding: 12px;
  background: hsl(var(--muted) / 35%);
}

.content-grid {
  display: grid;
  grid-template-columns: minmax(250px, 300px) minmax(0, 1fr);
  gap: 12px;
}

.org-panel,
.table-panel {
  min-width: 0;
  padding: 12px;
  background: hsl(var(--background));
  border: 1px solid hsl(var(--border) / 72%);
  border-radius: 8px;
}

.panel-head {
  display: flex;
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

.tree-tools {
  display: flex;
  gap: 6px;
  margin: 10px 0;
}

.tenant-select {
  width: 100%;
  margin-bottom: 8px;
}

.tree-shell {
  min-height: 520px;
  padding: 6px;
  background:
    linear-gradient(180deg, hsl(var(--muted) / 25%), transparent 52px),
    hsl(var(--background));
  border: 1px solid hsl(var(--border) / 70%);
  border-radius: 8px;
}

.tree-shell.is-loading {
  pointer-events: none;
  opacity: 0.62;
}

.tree-node,
.org-name-cell {
  display: inline-flex;
  gap: 8px;
  align-items: center;
  min-width: 0;
}

.tree-node {
  width: 100%;
  font-size: 13px;
  font-weight: 500;
  line-height: 30px;
}

.tree-node-icon-wrap {
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

.tree-node-icon {
  width: 14px;
  height: 14px;
  color: hsl(var(--muted-foreground));
}

.tree-node-title {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.query-head {
  display: flex;
  gap: 12px;
  align-items: flex-start;
  justify-content: space-between;
  margin-bottom: 10px;
}

.type-query {
  width: 160px;
}

.modify-record {
  width: 360px;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  padding: 12px 20px;
  margin: 18px -20px -16px;
  background: hsl(var(--background));
  border-top: 1px solid hsl(var(--border) / 72%);
}

:global(.org-record-popover .ant-popover-inner) {
  padding: 8px;
  border: 1px solid hsl(var(--border));
  border-radius: 8px;
  box-shadow:
    0 12px 28px rgb(15 23 42 / 12%),
    0 2px 8px rgb(15 23 42 / 8%);
}

:global(.org-record-popover .ant-popover-inner-content) {
  padding: 0;
}

:global(.org-record-popover) {
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

:deep(.ant-tree .ant-tree-node-selected) {
  background: hsl(var(--primary) / 10%) !important;
  box-shadow: inset 3px 0 0 hsl(var(--primary));
}

:deep(.ant-tree .ant-tree-switcher) {
  width: 18px;
  color: hsl(var(--muted-foreground));
}

:deep(.ant-tree .ant-tree-indent-unit) {
  width: 14px;
}

@media (max-width: 1000px) {
  .content-grid {
    grid-template-columns: 1fr;
  }

  .query-head {
    flex-direction: column;
  }
}
</style>
