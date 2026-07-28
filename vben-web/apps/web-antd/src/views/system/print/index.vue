<script setup lang="ts">
import type { TableColumnsType } from 'ant-design-vue';

import type { PrintTenantOption, SavePrintParams, SysPrintRecord } from '#/api';

import { computed, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';
import { IconifyIcon } from '@vben/icons';
import { useUserStore } from '@vben/stores';

import {
  Button,
  Empty,
  Form,
  Input,
  message,
  Modal,
  Popover,
  Select,
  Space,
  Table,
  Tag,
} from 'ant-design-vue';

import {
  addPrintApi,
  deletePrintApi,
  listPrintTenantsApi,
  pagePrintsApi,
  updatePrintApi,
} from '#/api';
import { ADMIN_PAGINATION_PROPS } from '#/utils/pagination';

import PrintDesigner from './designer.vue';

defineOptions({ name: 'AdminNetSystemPrint' });

type DesignerExpose = {
  finishSave: (success: boolean) => void;
  openDesigner: (record?: SysPrintRecord, tenantId?: number) => void;
};

const SUPER_ADMIN = 999;
const { hasAccessByCodes } = useAccess();
const userStore = useUserStore();
const loading = ref(false);
const records = ref<SysPrintRecord[]>([]);
const tenants = ref<PrintTenantOption[]>([]);
const designerRef = ref<DesignerExpose>();

const query = reactive({
  name: '',
  page: 1,
  pageSize: 50,
  tenantId: undefined as number | undefined,
  total: 0,
});

const columns: TableColumnsType<SysPrintRecord> = [
  { key: 'index', title: '序号', width: 66 },
  { dataIndex: 'name', key: 'name', title: '模板名称', width: 240 },
  { key: 'printType', title: '打印方式', width: 120 },
  { dataIndex: 'orderNo', key: 'orderNo', title: '排序', width: 80 },
  { key: 'status', title: '状态', width: 90 },
  { key: 'modifyRecord', title: '修改记录', width: 100 },
  { fixed: 'right', key: 'actions', title: '操作', width: 160 },
];

const isSuperAdmin = computed(
  () =>
    Number(
      (userStore.userInfo as null | Record<string, unknown>)?.accountType,
    ) === SUPER_ADMIN,
);

const tenantOptions = computed(() =>
  tenants.value.map((item) => ({
    label: item.host ? `${item.label} (${item.host})` : item.label,
    value: item.value,
  })),
);

function can(code: string) {
  return hasAccessByCodes([code]);
}

function asPrint(value: unknown) {
  return value as SysPrintRecord;
}

function dateText(value?: string) {
  if (!value) return '无';
  return value.replace('T', ' ').slice(0, 19);
}

async function loadRecords() {
  loading.value = true;
  try {
    const result = await pagePrintsApi({
      name: query.name.trim() || undefined,
      page: query.page,
      pageSize: query.pageSize,
      tenantId: query.tenantId,
    });
    records.value = result.items ?? [];
    query.total = result.total ?? 0;
  } finally {
    loading.value = false;
  }
}

async function handleQuery() {
  query.page = 1;
  await loadRecords();
}

async function loadTenants() {
  if (!isSuperAdmin.value) return;
  tenants.value = await listPrintTenantsApi();
  if (!query.tenantId && tenants.value.length > 0) {
    query.tenantId = tenants.value[0]?.value;
  }
}

function resetQuery() {
  query.name = '';
  query.page = 1;
  void loadRecords();
}

function openCreate() {
  designerRef.value?.openDesigner(undefined, query.tenantId);
}

function openEdit(record: SysPrintRecord) {
  designerRef.value?.openDesigner(record, query.tenantId);
}

async function savePrint(value: SavePrintParams) {
  try {
    if (value.id) {
      await updatePrintApi(value as SavePrintParams & { id: number });
      message.success('打印模板已更新');
    } else {
      await addPrintApi(value);
      message.success('打印模板已新增');
    }
    designerRef.value?.finishSave(true);
    await loadRecords();
  } catch {
    designerRef.value?.finishSave(false);
  }
}

function confirmDelete(record: SysPrintRecord) {
  Modal.confirm({
    centered: true,
    content: `删除后使用“${record.name}”的业务打印入口将无法继续获取该模板。`,
    okButtonProps: { danger: true },
    okText: '确认删除',
    async onOk() {
      await deletePrintApi(record.id);
      message.success('打印模板已删除');
      if (records.value.length === 1 && query.page > 1) query.page -= 1;
      await loadRecords();
    },
    title: `删除打印模板“${record.name}”？`,
  });
}

function changePage(page: number, pageSize: number) {
  query.page = page;
  query.pageSize = pageSize;
  void loadRecords();
}

async function changeTenant(value: unknown) {
  const tenantId = Number(value);
  if (!Number.isFinite(tenantId) || tenantId <= 0) return;
  query.tenantId = tenantId;
  query.page = 1;
  await loadRecords();
}

onMounted(async () => {
  await loadTenants();
  await loadRecords();
});
</script>

<template>
  <div class="print-page">
    <header class="page-head">
      <div>
        <h1>打印模板</h1>
        <p>设计、预览并维护业务单据的打印版式</p>
      </div>
      <Button v-if="can('sysPrint:add')" type="primary" @click="openCreate">
        <template #icon><IconifyIcon icon="lucide:plus" /></template>
        新增模板
      </Button>
    </header>

    <Form :model="query" class="query-form" layout="inline">
      <Form.Item v-if="isSuperAdmin" label="租户">
        <Select
          :options="tenantOptions"
          placeholder="选择租户"
          style="width: 240px"
          :value="query.tenantId"
          @change="changeTenant"
        />
      </Form.Item>
      <Form.Item label="模板名称">
        <Input
          v-model:value="query.name"
          allow-clear
          placeholder="输入模板名称"
          @press-enter="handleQuery"
        />
      </Form.Item>
      <Form.Item>
        <Space>
          <Button
            v-if="can('sysPrint:page')"
            :loading="loading"
            type="primary"
            @click="handleQuery"
          >
            <template #icon><IconifyIcon icon="lucide:search" /></template>
            查询
          </Button>
          <Button @click="resetQuery">
            <template #icon><IconifyIcon icon="lucide:rotate-ccw" /></template>
            重置
          </Button>
        </Space>
      </Form.Item>
    </Form>

    <Table
      :columns="columns"
      :data-source="records"
      :loading="loading"
      :pagination="{
        ...ADMIN_PAGINATION_PROPS,
        current: query.page,
        pageSize: query.pageSize,
        showTotal: (total: number) => `共 ${total} 条`,
        total: query.total,
      }"
      row-key="id"
      :scroll="{ x: 900 }"
      size="small"
      @change="
        (page: any) => changePage(page.current || 1, page.pageSize || 50)
      "
    >
      <template #emptyText>
        <Empty :image="Empty.PRESENTED_IMAGE_SIMPLE" description="暂无打印模板">
          <Button
            v-if="can('sysPrint:add')"
            size="small"
            type="primary"
            @click="openCreate"
          >
            新增第一个模板
          </Button>
        </Empty>
      </template>
      <template #bodyCell="{ column, index, record }">
        <template v-if="column.key === 'index'">
          {{ (query.page - 1) * query.pageSize + index + 1 }}
        </template>
        <template v-else-if="column.key === 'name'">
          <div class="template-name">
            <span class="template-icon"
              ><IconifyIcon icon="lucide:printer"
            /></span>
            <div>
              <strong>{{ asPrint(record).name }}</strong>
              <small>{{ asPrint(record).remark || '未填写备注' }}</small>
            </div>
          </div>
        </template>
        <template v-else-if="column.key === 'printType'">
          <Tag :color="asPrint(record).printType === 2 ? 'purple' : 'blue'">
            {{ asPrint(record).printType === 2 ? '客户端打印' : '浏览器打印' }}
          </Tag>
        </template>
        <template v-else-if="column.key === 'status'">
          <Tag :color="asPrint(record).status === 1 ? 'green' : 'default'">
            {{ asPrint(record).status === 1 ? '启用' : '禁用' }}
          </Tag>
        </template>
        <template v-else-if="column.key === 'modifyRecord'">
          <Popover placement="bottomRight" trigger="click">
            <template #content>
              <div class="record-grid">
                <span>创建者</span>
                <strong>{{ asPrint(record).createUserName || '无' }}</strong>
                <span>创建时间</span>
                <strong>{{ dateText(asPrint(record).createTime) }}</strong>
                <span>修改者</span>
                <strong>{{ asPrint(record).updateUserName || '无' }}</strong>
                <span>修改时间</span>
                <strong>{{ dateText(asPrint(record).updateTime) }}</strong>
              </div>
            </template>
            <Button size="small" type="link">
              <template #icon><IconifyIcon icon="lucide:clock-3" /></template>
              详情
            </Button>
          </Popover>
        </template>
        <template v-else-if="column.key === 'actions'">
          <Space :size="4">
            <Button
              v-if="can('sysPrint:update')"
              size="small"
              type="link"
              @click="openEdit(asPrint(record))"
            >
              <template #icon>
                <IconifyIcon icon="lucide:square-pen" />
              </template>
              设计
            </Button>
            <Button
              v-if="can('sysPrint:delete')"
              danger
              size="small"
              type="link"
              @click="confirmDelete(asPrint(record))"
            >
              <template #icon><IconifyIcon icon="lucide:trash-2" /></template>
              删除
            </Button>
          </Space>
        </template>
      </template>
    </Table>

    <PrintDesigner ref="designerRef" @save="savePrint" />
  </div>
</template>

<style scoped>
.print-page {
  min-height: 100%;
  padding: 12px;
  background: hsl(var(--background));
}

.page-head {
  display: flex;
  gap: 16px;
  align-items: flex-start;
  justify-content: space-between;
  margin-bottom: 12px;
}

.page-head h1 {
  margin: 0;
  font-size: 16px;
  font-weight: 650;
}

.page-head p {
  margin: 3px 0 0;
  font-size: 12px;
  color: hsl(var(--muted-foreground));
}

.query-form {
  padding: 10px 12px 0;
  margin-bottom: 4px;
  background: hsl(var(--muted) / 20%);
  border: 1px solid hsl(var(--border) / 70%);
  border-radius: 6px;
}

.template-name {
  display: flex;
  gap: 10px;
  align-items: center;
  min-width: 0;
}

.template-icon {
  display: grid;
  flex: none;
  place-items: center;
  width: 30px;
  height: 30px;
  color: hsl(var(--primary));
  background: hsl(var(--primary) / 8%);
  border: 1px solid hsl(var(--primary) / 20%);
  border-radius: 6px;
}

.template-name strong,
.template-name small {
  display: block;
}

.template-name small {
  max-width: 360px;
  margin-top: 2px;
  overflow: hidden;
  text-overflow: ellipsis;
  font-size: 11px;
  color: hsl(var(--muted-foreground));
  white-space: nowrap;
}

.record-grid {
  display: grid;
  grid-template-columns: 70px minmax(130px, 1fr);
  gap: 7px 12px;
  font-size: 12px;
}

.record-grid span {
  color: hsl(var(--muted-foreground));
}

.record-grid strong {
  font-weight: 500;
}

:deep(.ant-form-inline .ant-form-item) {
  margin-bottom: 10px;
}

:deep(.ant-table-thead > tr > th) {
  white-space: nowrap;
}
</style>
