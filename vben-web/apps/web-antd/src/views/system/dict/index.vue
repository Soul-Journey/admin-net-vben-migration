<script setup lang="ts">
import type { FormInstance, TableColumnsType } from 'ant-design-vue';
import type { Rule } from 'ant-design-vue/es/form';

import type {
  SaveDictDataParams,
  SaveDictTypeParams,
  SysDictDataRecord,
  SysDictTypeRecord,
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
  Pagination,
  Popover,
  Radio,
  Row,
  Space,
  Table,
  Tag,
  Tooltip,
} from 'ant-design-vue';

import {
  addDictDataApi,
  addDictTypeApi,
  deleteDictDataApi,
  deleteDictTypeApi,
  pageDictDataApi,
  pageDictTypesApi,
  updateDictDataApi,
  updateDictTypeApi,
} from '#/api';
import { ADMIN_PAGINATION_PROPS } from '#/utils/pagination';

defineOptions({ name: 'AdminNetSystemDict' });

type DictTypeFormState = Partial<SaveDictTypeParams> & { id?: number };
type DictDataFormState = Partial<SaveDictDataParams> & { id?: number };

const ENABLED = 1;
const DISABLED = 2;
const YES = 1;
const NO = 2;
const SUPER_ADMIN_ACCOUNT = 999;

const { hasAccessByCodes } = useAccess();
const userStore = useUserStore();

const typeLoading = ref(false);
const dataLoading = ref(false);
const typeSubmitLoading = ref(false);
const dataSubmitLoading = ref(false);
const typeModalOpen = ref(false);
const dataModalOpen = ref(false);
const typeModalTitle = ref('新增字典');
const dataModalTitle = ref('新增字典值');
const typeFormRef = ref<FormInstance>();
const dataFormRef = ref<FormInstance>();

const dictTypes = ref<SysDictTypeRecord[]>([]);
const dictData = ref<SysDictDataRecord[]>([]);
const selectedDict = ref<SysDictTypeRecord>();
const typeFormState = reactive<DictTypeFormState>({});
const dataFormState = reactive<DictDataFormState>({});

const typeQuery = reactive({
  code: '',
  name: '',
});

const dataQuery = reactive({
  label: '',
});

const typePagination = reactive({
  page: 1,
  pageSize: 50,
  total: 0,
});

const dataPagination = reactive({
  page: 1,
  pageSize: 50,
  total: 0,
});

const typeColumns: TableColumnsType<SysDictTypeRecord> = [
  { key: 'index', title: '序号', width: 52 },
  { dataIndex: 'name', key: 'name', title: '字典名称', width: 150 },
  { dataIndex: 'code', key: 'code', title: '字典编码', width: 160 },
  { key: 'sysFlag', title: '系统内置', width: 86 },
  { key: 'status', title: '状态', width: 76 },
  { dataIndex: 'orderNo', key: 'orderNo', title: '排序', width: 62 },
  { key: 'actions', fixed: 'right', title: '操作', width: 116 },
];

const dataColumns: TableColumnsType<SysDictDataRecord> = [
  { key: 'index', title: '序号', width: 52 },
  { key: 'label', title: '显示文本', width: 140 },
  { dataIndex: 'value', key: 'value', title: '字典值', width: 120 },
  { dataIndex: 'code', key: 'code', title: '编码', width: 120 },
  { key: 'extData', title: '扩展数据', width: 84 },
  { key: 'status', title: '状态', width: 76 },
  { dataIndex: 'orderNo', key: 'orderNo', title: '排序', width: 62 },
  { key: 'actions', fixed: 'right', title: '操作', width: 144 },
];

const typeRules: Record<string, Rule[]> = {
  code: [
    {
      message: '请输入字典编码',
      required: true,
      trigger: 'blur',
      type: 'string',
    },
  ],
  name: [
    {
      message: '请输入字典名称',
      required: true,
      trigger: 'blur',
      type: 'string',
    },
  ],
  sysFlag: [
    {
      message: '请选择是否内置',
      required: true,
      trigger: 'change',
      type: 'number',
    },
  ],
};

const dataRules: Record<string, Rule[]> = {
  label: [
    {
      message: '请输入显示文本',
      required: true,
      trigger: 'blur',
      type: 'string',
    },
  ],
  value: [
    {
      message: '请输入字典值',
      required: true,
      trigger: 'blur',
      type: 'string',
    },
  ],
};

const tagTypeOptions = [
  { color: 'blue', label: '主题色', value: 'primary' },
  { color: 'green', label: '成功', value: 'success' },
  { color: 'default', label: '信息', value: 'info' },
  { color: 'orange', label: '警告', value: 'warning' },
  { color: 'red', label: '危险', value: 'danger' },
];

const statusOptions = [
  { label: '启用', value: ENABLED },
  { label: '禁用', value: DISABLED },
];

const yesNoOptions = [
  { label: '是', value: YES },
  { label: '否', value: NO },
];

const isSuperAdmin = computed(
  () =>
    Number((userStore.userInfo as any)?.accountType) === SUPER_ADMIN_ACCOUNT,
);

const selectedEditable = computed(() =>
  selectedDict.value ? hasDictPermission(selectedDict.value) : false,
);

function can(code: string) {
  return hasAccessByCodes([code]);
}

function asDictType(record: unknown) {
  return record as SysDictTypeRecord;
}

function asDictData(record: unknown) {
  return record as SysDictDataRecord;
}

function getValueText(value?: null | number | string) {
  return value === undefined || value === null || value === '' ? '无' : value;
}

function hasDictPermission(row?: SysDictTypeRecord) {
  if (!row) {
    return false;
  }
  if (row.code?.toLowerCase().endsWith('enum')) {
    return false;
  }
  return row.sysFlag === NO || isSuperAdmin.value;
}

function getStatusMeta(status?: number) {
  return status === ENABLED
    ? { color: 'success', label: '启用' }
    : { color: 'default', label: '禁用' };
}

function getYesNoMeta(value?: number) {
  return value === YES
    ? { color: 'blue', label: '是' }
    : { color: 'default', label: '否' };
}

function getAntTagColor(tagType?: string) {
  return tagTypeOptions.find((item) => item.value === tagType)?.color ?? 'blue';
}

function resetTypeFormState(values: DictTypeFormState) {
  for (const key of Object.keys(typeFormState)) {
    delete typeFormState[key as keyof DictTypeFormState];
  }
  Object.assign(typeFormState, values);
}

function resetDataFormState(values: DictDataFormState) {
  for (const key of Object.keys(dataFormState)) {
    delete dataFormState[key as keyof DictDataFormState];
  }
  Object.assign(dataFormState, values);
}

async function loadDictTypes() {
  if (!can('sysDictType:page')) {
    return;
  }
  typeLoading.value = true;
  try {
    const data = await pageDictTypesApi({
      code: typeQuery.code || undefined,
      name: typeQuery.name || undefined,
      page: typePagination.page,
      pageSize: typePagination.pageSize,
    });
    dictTypes.value = data.items ?? [];
    typePagination.total = data.total ?? 0;
    if (!selectedDict.value && dictTypes.value[0]) {
      await selectDictType(dictTypes.value[0]);
    }
  } finally {
    typeLoading.value = false;
  }
}

async function loadDictData() {
  if (!selectedDict.value) {
    dictData.value = [];
    dataPagination.total = 0;
    return;
  }
  dataLoading.value = true;
  try {
    const data = await pageDictDataApi({
      dictTypeId: selectedDict.value.id,
      label: dataQuery.label || undefined,
      page: dataPagination.page,
      pageSize: dataPagination.pageSize,
    });
    dictData.value = data.items ?? [];
    dataPagination.total = data.total ?? 0;
  } finally {
    dataLoading.value = false;
  }
}

async function selectDictType(record: SysDictTypeRecord) {
  selectedDict.value = record;
  dataPagination.page = 1;
  dataQuery.label = '';
  await loadDictData();
}

async function handleTypeSearch() {
  typePagination.page = 1;
  selectedDict.value = undefined;
  dictData.value = [];
  await loadDictTypes();
}

async function resetTypeQuery() {
  typeQuery.code = '';
  typeQuery.name = '';
  await handleTypeSearch();
}

async function handleDataSearch() {
  dataPagination.page = 1;
  await loadDictData();
}

async function resetDataQuery() {
  dataQuery.label = '';
  await handleDataSearch();
}

async function handleTypePageChange(page: number, pageSize: number) {
  typePagination.page = page;
  typePagination.pageSize = pageSize;
  await loadDictTypes();
}

async function handleDataPageChange(page: number, pageSize: number) {
  dataPagination.page = page;
  dataPagination.pageSize = pageSize;
  await loadDictData();
}

function openCreateDictType() {
  typeModalTitle.value = '新增字典';
  resetTypeFormState({
    code: '',
    name: '',
    orderNo: 100,
    remark: '',
    status: ENABLED,
    sysFlag: NO,
  });
  typeModalOpen.value = true;
}

function openEditDictType(record: SysDictTypeRecord) {
  typeModalTitle.value = '编辑字典';
  resetTypeFormState({
    ...record,
    orderNo: record.orderNo ?? 100,
    status: record.status ?? ENABLED,
    sysFlag: record.sysFlag ?? NO,
  });
  typeModalOpen.value = true;
}

function openCreateDictData() {
  if (!selectedDict.value) {
    message.warning('请先选择字典');
    return;
  }
  dataModalTitle.value = '新增字典值';
  resetDataFormState({
    dictTypeId: selectedDict.value.id,
    label: '',
    orderNo: 100,
    status: ENABLED,
    tagType: 'primary',
    value: '',
  });
  dataModalOpen.value = true;
}

function openEditDictData(record: SysDictDataRecord) {
  dataModalTitle.value = '编辑字典值';
  resetDataFormState({
    ...record,
    dictTypeId: record.dictTypeId ?? selectedDict.value?.id,
    orderNo: record.orderNo ?? 100,
    status: record.status ?? ENABLED,
  });
  dataModalOpen.value = true;
}

function openCopyDictData(record: SysDictDataRecord) {
  dataModalTitle.value = '复制字典值';
  resetDataFormState({
    ...record,
    id: undefined,
    dictTypeId: record.dictTypeId ?? selectedDict.value?.id,
  });
  dataModalOpen.value = true;
}

async function submitDictType() {
  await typeFormRef.value?.validate();
  typeSubmitLoading.value = true;
  try {
    const payload = {
      ...typeFormState,
      orderNo: typeFormState.orderNo ?? 100,
      status: typeFormState.status ?? ENABLED,
      sysFlag: typeFormState.sysFlag ?? NO,
    } as SaveDictTypeParams & { id?: number };
    if (payload.id) {
      await updateDictTypeApi(payload as SaveDictTypeParams & { id: number });
      message.success('字典已更新');
    } else {
      await addDictTypeApi(payload);
      message.success('字典已新增');
    }
    typeModalOpen.value = false;
    await handleTypeSearch();
  } finally {
    typeSubmitLoading.value = false;
  }
}

async function submitDictData() {
  await dataFormRef.value?.validate();
  if (!selectedDict.value && !dataFormState.dictTypeId) {
    message.warning('请先选择字典');
    return;
  }
  dataSubmitLoading.value = true;
  try {
    const payload = {
      ...dataFormState,
      dictTypeId: dataFormState.dictTypeId ?? selectedDict.value?.id,
      orderNo: dataFormState.orderNo ?? 100,
      status: dataFormState.status ?? ENABLED,
    } as SaveDictDataParams & { id?: number };
    if (payload.id) {
      await updateDictDataApi(payload as SaveDictDataParams & { id: number });
      message.success('字典值已更新');
    } else {
      await addDictDataApi(payload);
      message.success('字典值已新增');
    }
    dataModalOpen.value = false;
    await loadDictData();
  } finally {
    dataSubmitLoading.value = false;
  }
}

function confirmDeleteDictType(record: SysDictTypeRecord) {
  Modal.confirm({
    centered: true,
    content: `确定删除字典「${record.name}」吗？`,
    okButtonProps: { danger: true },
    okText: '删除',
    title: '删除确认',
    async onOk() {
      await deleteDictTypeApi(record.id);
      message.success('字典已删除');
      if (selectedDict.value?.id === record.id) {
        selectedDict.value = undefined;
        dictData.value = [];
      }
      await loadDictTypes();
    },
  });
}

function confirmDeleteDictData(record: SysDictDataRecord) {
  Modal.confirm({
    centered: true,
    content: `确定删除字典值「${record.label || record.value}」吗？`,
    okButtonProps: { danger: true },
    okText: '删除',
    title: '删除确认',
    async onOk() {
      await deleteDictDataApi(record.id);
      message.success('字典值已删除');
      await loadDictData();
    },
  });
}

onMounted(async () => {
  await loadDictTypes();
});
</script>

<template>
  <div class="dict-page">
    <section class="dict-grid">
      <div class="panel">
        <div class="panel-head">
          <div>
            <div class="panel-title">字典</div>
            <div class="panel-subtitle">选择左侧字典后维护右侧字典值</div>
          </div>
        </div>

        <Form :model="typeQuery" layout="inline" class="query-form">
          <Form.Item label="名称">
            <Input
              v-model:value="typeQuery.name"
              allow-clear
              placeholder="字典名称"
              @press-enter="handleTypeSearch"
            />
          </Form.Item>
          <Form.Item>
            <Space :size="8">
              <Button
                v-if="can('sysDictType:page')"
                type="primary"
                @click="handleTypeSearch"
              >
                <template #icon>
                  <IconifyIcon icon="lucide:search" />
                </template>
                查询
              </Button>
              <Button @click="resetTypeQuery">
                <template #icon>
                  <IconifyIcon icon="lucide:rotate-ccw" />
                </template>
                重置
              </Button>
              <Button
                v-if="can('sysDictType:add')"
                type="primary"
                @click="openCreateDictType"
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
          :columns="typeColumns"
          :custom-row="
            (record) => ({
              class:
                selectedDict?.id === asDictType(record).id ? 'is-selected' : '',
              onClick: () => selectDictType(asDictType(record)),
            })
          "
          :data-source="dictTypes"
          :loading="typeLoading"
          :pagination="false"
          :scroll="{ x: 710 }"
          row-key="id"
          size="small"
        >
          <template #bodyCell="{ column, index, record }">
            <template v-if="column.key === 'index'">
              {{
                (typePagination.page - 1) * typePagination.pageSize + index + 1
              }}
            </template>
            <template v-else-if="column.key === 'sysFlag'">
              <Tag :color="getYesNoMeta(asDictType(record).sysFlag).color">
                {{ getYesNoMeta(asDictType(record).sysFlag).label }}
              </Tag>
            </template>
            <template v-else-if="column.key === 'status'">
              <Tag :color="getStatusMeta(asDictType(record).status).color">
                {{ getStatusMeta(asDictType(record).status).label }}
              </Tag>
            </template>
            <template v-else-if="column.key === 'actions'">
              <Space :size="4">
                <Popover
                  overlay-class-name="dict-record-popover"
                  placement="left"
                  trigger="click"
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
                        {{ getValueText(asDictType(record).createUserName) }}
                      </Descriptions.Item>
                      <Descriptions.Item label="创建时间">
                        {{ getValueText(asDictType(record).createTime) }}
                      </Descriptions.Item>
                      <Descriptions.Item label="修改者">
                        {{ getValueText(asDictType(record).updateUserName) }}
                      </Descriptions.Item>
                      <Descriptions.Item label="修改时间">
                        {{ getValueText(asDictType(record).updateTime) }}
                      </Descriptions.Item>
                      <Descriptions.Item label="备注" :span="2">
                        {{ getValueText(asDictType(record).remark) }}
                      </Descriptions.Item>
                    </Descriptions>
                  </template>
                  <Button
                    class="record-icon-button"
                    size="small"
                    type="text"
                    @click.stop
                  >
                    <template #icon>
                      <IconifyIcon icon="lucide:info" />
                    </template>
                  </Button>
                </Popover>
                <Tooltip title="编辑">
                  <Button
                    v-if="can('sysDictType:update')"
                    :disabled="!hasDictPermission(asDictType(record))"
                    size="small"
                    type="link"
                    @click.stop="openEditDictType(asDictType(record))"
                  >
                    <template #icon>
                      <IconifyIcon icon="lucide:square-pen" />
                    </template>
                  </Button>
                </Tooltip>
                <Tooltip title="删除">
                  <Button
                    v-if="can('sysDictType:delete')"
                    :disabled="
                      asDictType(record).sysFlag === YES ||
                      !hasDictPermission(asDictType(record))
                    "
                    danger
                    size="small"
                    type="link"
                    @click.stop="confirmDeleteDictType(asDictType(record))"
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
            v-bind="ADMIN_PAGINATION_PROPS"
            v-model:current="typePagination.page"
            v-model:page-size="typePagination.pageSize"
            :show-total="(total) => `共 ${total} 条`"
            :total="typePagination.total"
            size="small"
            @change="handleTypePageChange"
            @show-size-change="handleTypePageChange"
          />
        </div>
      </div>

      <div class="panel">
        <div class="panel-head">
          <div>
            <div class="panel-title">
              字典值
              <Tag v-if="selectedDict" color="blue">
                {{ selectedDict.name }}
              </Tag>
            </div>
            <div class="panel-subtitle">
              {{ selectedDict ? selectedDict.code : '请先选择一个字典' }}
            </div>
          </div>
        </div>

        <Form :model="dataQuery" layout="inline" class="query-form">
          <Form.Item label="显示文本">
            <Input
              v-model:value="dataQuery.label"
              allow-clear
              placeholder="显示文本"
              @press-enter="handleDataSearch"
            />
          </Form.Item>
          <Form.Item>
            <Space :size="8">
              <Button
                :disabled="!selectedDict"
                type="primary"
                @click="handleDataSearch"
              >
                <template #icon>
                  <IconifyIcon icon="lucide:search" />
                </template>
                查询
              </Button>
              <Button :disabled="!selectedDict" @click="resetDataQuery">
                <template #icon>
                  <IconifyIcon icon="lucide:rotate-ccw" />
                </template>
                重置
              </Button>
              <Button
                v-if="can('sysDictData:add')"
                :disabled="!selectedEditable"
                type="primary"
                @click="openCreateDictData"
              >
                <template #icon>
                  <IconifyIcon icon="lucide:plus" />
                </template>
                新增
              </Button>
            </Space>
          </Form.Item>
        </Form>

        <template v-if="selectedDict">
          <Table
            :columns="dataColumns"
            :data-source="dictData"
            :loading="dataLoading"
            :pagination="false"
            :scroll="{ x: 800 }"
            row-key="id"
            size="small"
          >
            <template #bodyCell="{ column, index, record }">
              <template v-if="column.key === 'index'">
                {{
                  (dataPagination.page - 1) * dataPagination.pageSize +
                  index +
                  1
                }}
              </template>
              <template v-else-if="column.key === 'label'">
                <Tag
                  :color="getAntTagColor(asDictData(record).tagType)"
                  :style="asDictData(record).styleSetting"
                  :class="asDictData(record).classSetting"
                >
                  {{ asDictData(record).label }}
                </Tag>
              </template>
              <template v-else-if="column.key === 'extData'">
                <Tag :color="asDictData(record).extData ? 'green' : 'orange'">
                  {{ asDictData(record).extData ? '有值' : '空' }}
                </Tag>
              </template>
              <template v-else-if="column.key === 'status'">
                <Tag :color="getStatusMeta(asDictData(record).status).color">
                  {{ getStatusMeta(asDictData(record).status).label }}
                </Tag>
              </template>
              <template v-else-if="column.key === 'actions'">
                <Space :size="4">
                  <Popover
                    overlay-class-name="dict-record-popover"
                    placement="left"
                    trigger="click"
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
                          {{ getValueText(asDictData(record).createUserName) }}
                        </Descriptions.Item>
                        <Descriptions.Item label="创建时间">
                          {{ getValueText(asDictData(record).createTime) }}
                        </Descriptions.Item>
                        <Descriptions.Item label="修改者">
                          {{ getValueText(asDictData(record).updateUserName) }}
                        </Descriptions.Item>
                        <Descriptions.Item label="修改时间">
                          {{ getValueText(asDictData(record).updateTime) }}
                        </Descriptions.Item>
                        <Descriptions.Item label="备注" :span="2">
                          {{ getValueText(asDictData(record).remark) }}
                        </Descriptions.Item>
                      </Descriptions>
                    </template>
                    <Button
                      class="record-icon-button"
                      size="small"
                      type="text"
                      @click.stop
                    >
                      <template #icon>
                        <IconifyIcon icon="lucide:info" />
                      </template>
                    </Button>
                  </Popover>
                  <Tooltip title="编辑">
                    <Button
                      v-if="can('sysDictData:update')"
                      size="small"
                      type="link"
                      @click.stop="openEditDictData(asDictData(record))"
                    >
                      <template #icon>
                        <IconifyIcon icon="lucide:square-pen" />
                      </template>
                    </Button>
                  </Tooltip>
                  <Tooltip title="删除">
                    <Button
                      v-if="can('sysDictData:delete')"
                      :disabled="!selectedEditable"
                      danger
                      size="small"
                      type="link"
                      @click.stop="confirmDeleteDictData(asDictData(record))"
                    >
                      <template #icon>
                        <IconifyIcon icon="lucide:trash-2" />
                      </template>
                    </Button>
                  </Tooltip>
                  <Tooltip title="复制">
                    <Button
                      v-if="can('sysDictData:add')"
                      :disabled="!selectedEditable"
                      size="small"
                      type="link"
                      @click.stop="openCopyDictData(asDictData(record))"
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

          <div class="table-footer">
            <Pagination
              v-bind="ADMIN_PAGINATION_PROPS"
              v-model:current="dataPagination.page"
              v-model:page-size="dataPagination.pageSize"
              :show-total="(total) => `共 ${total} 条`"
              :total="dataPagination.total"
              size="small"
              @change="handleDataPageChange"
              @show-size-change="handleDataPageChange"
            />
          </div>
        </template>
        <div v-else class="empty-panel">
          <Empty description="请选择左侧字典" />
        </div>
      </div>
    </section>

    <Modal
      v-model:open="typeModalOpen"
      :body-style="{ padding: '14px 18px' }"
      :footer="null"
      :mask-closable="false"
      :title="typeModalTitle"
      centered
      class="dict-type-modal"
      destroy-on-close
      :width="520"
      @cancel="typeFormRef?.clearValidate()"
    >
      <Form
        ref="typeFormRef"
        :model="typeFormState"
        :rules="typeRules"
        layout="vertical"
      >
        <Row :gutter="16">
          <Col :span="12">
            <Form.Item label="字典名称" name="name">
              <Input v-model:value="typeFormState.name" allow-clear />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="字典编码" name="code">
              <Input v-model:value="typeFormState.code" allow-clear />
            </Form.Item>
          </Col>
          <Col v-if="isSuperAdmin" :span="12">
            <Form.Item label="系统内置" name="sysFlag">
              <Radio.Group
                v-model:value="typeFormState.sysFlag"
                :disabled="typeFormState.sysFlag === YES && !!typeFormState.id"
                :options="yesNoOptions"
              />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="状态" name="status">
              <Radio.Group
                v-model:value="typeFormState.status"
                :options="statusOptions"
              />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="排序" name="orderNo">
              <InputNumber
                v-model:value="typeFormState.orderNo"
                class="w-full"
                :min="0"
              />
            </Form.Item>
          </Col>
          <Col :span="24">
            <Form.Item label="备注" name="remark">
              <Input.TextArea
                v-model:value="typeFormState.remark"
                :auto-size="{ minRows: 2, maxRows: 4 }"
                allow-clear
              />
            </Form.Item>
          </Col>
        </Row>
      </Form>
      <div class="modal-footer">
        <Space>
          <Button @click="typeModalOpen = false">取消</Button>
          <Button
            :loading="typeSubmitLoading"
            type="primary"
            @click="submitDictType"
          >
            确定
          </Button>
        </Space>
      </div>
    </Modal>

    <Modal
      v-model:open="dataModalOpen"
      :body-style="{ padding: '14px 18px' }"
      :footer="null"
      :mask-closable="false"
      :title="dataModalTitle"
      centered
      class="dict-data-modal"
      destroy-on-close
      :width="480"
      @cancel="dataFormRef?.clearValidate()"
    >
      <Form
        ref="dataFormRef"
        :model="dataFormState"
        :rules="dataRules"
        layout="vertical"
      >
        <Row :gutter="16">
          <Col :span="12">
            <Form.Item label="显示文本" name="label">
              <Input v-model:value="dataFormState.label" allow-clear />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="字典值" name="value">
              <Input v-model:value="dataFormState.value" allow-clear />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="编码" name="code">
              <Input v-model:value="dataFormState.code" allow-clear />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="标签类型" name="tagType">
              <Radio.Group
                v-model:value="dataFormState.tagType"
                class="tag-type-radio"
              >
                <Radio
                  v-for="item in tagTypeOptions"
                  :key="item.value"
                  :value="item.value"
                >
                  <Tag :color="item.color" class="tag-type-preview">
                    {{ item.label }}
                  </Tag>
                </Radio>
              </Radio.Group>
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="状态" name="status">
              <Radio.Group
                v-model:value="dataFormState.status"
                :options="statusOptions"
              />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="排序" name="orderNo">
              <InputNumber
                v-model:value="dataFormState.orderNo"
                class="w-full"
                :min="0"
              />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="样式 Style" name="styleSetting">
              <Input v-model:value="dataFormState.styleSetting" allow-clear />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="样式 Class" name="classSetting">
              <Input v-model:value="dataFormState.classSetting" allow-clear />
            </Form.Item>
          </Col>
          <Col :span="24">
            <Form.Item label="备注" name="remark">
              <Input.TextArea
                v-model:value="dataFormState.remark"
                :auto-size="{ minRows: 2, maxRows: 4 }"
                allow-clear
              />
            </Form.Item>
          </Col>
          <Col :span="24">
            <Form.Item label="扩展数据" name="extData">
              <Input.TextArea
                v-model:value="dataFormState.extData"
                :auto-size="{ minRows: 3, maxRows: 5 }"
                allow-clear
              />
            </Form.Item>
          </Col>
        </Row>
      </Form>
      <div class="modal-footer">
        <Space>
          <Button @click="dataModalOpen = false">取消</Button>
          <Button
            :loading="dataSubmitLoading"
            type="primary"
            @click="submitDictData"
          >
            确定
          </Button>
        </Space>
      </div>
    </Modal>
  </div>
</template>

<style scoped>
.dict-page {
  min-height: 100%;
  padding: 12px;
  background: hsl(var(--muted) / 35%);
}

.dict-grid {
  display: grid;
  grid-template-columns: minmax(420px, 1fr) minmax(480px, 1fr);
  gap: 12px;
  min-height: 0;
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
  display: flex;
  gap: 8px;
  align-items: center;
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
  width: 340px;
}

.record-icon-button {
  width: 26px;
  height: 26px;
  color: hsl(var(--muted-foreground));
}

.record-icon-button:hover {
  color: hsl(var(--primary));
  background: hsl(var(--primary) / 8%);
}

.empty-panel {
  display: grid;
  place-items: center;
  min-height: 420px;
  background: hsl(var(--muted) / 18%);
  border: 1px dashed hsl(var(--border));
  border-radius: 8px;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  padding: 10px 18px;
  margin: 14px -18px -14px;
  background: hsl(var(--background));
  border-top: 1px solid hsl(var(--border) / 72%);
}

.tag-type-radio {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 8px 10px;
}

.tag-type-preview {
  min-width: 56px;
  margin-inline-end: 0;
  text-align: center;
}

:global(.dict-type-modal) {
  width: min(520px, calc(100vw - 32px)) !important;
}

:global(.dict-data-modal) {
  width: min(480px, calc(100vw - 32px)) !important;
}

:global(.dict-type-modal .ant-modal-content),
:global(.dict-data-modal .ant-modal-content) {
  border-radius: 8px;
}

:global(.dict-record-popover .ant-popover-inner) {
  padding: 8px;
  border: 1px solid hsl(var(--border));
  border-radius: 8px;
  box-shadow:
    0 12px 28px rgb(15 23 42 / 12%),
    0 2px 8px rgb(15 23 42 / 8%);
}

:global(.dict-record-popover .ant-popover-inner-content) {
  padding: 0;
}

:global(.dict-record-popover) {
  z-index: 1060;
}

:deep(.ant-form-inline .ant-form-item) {
  margin-bottom: 12px;
}

:deep(.tag-type-radio .ant-radio-wrapper) {
  align-items: center;
  margin-inline-end: 0;
}

:deep(.ant-table-thead > tr > th) {
  white-space: nowrap;
}

:deep(.ant-table-row.is-selected > td) {
  background: hsl(var(--primary) / 8%) !important;
}

:deep(.ant-table-row.is-selected td:first-child) {
  box-shadow: inset 3px 0 0 hsl(var(--primary));
}

@media (max-width: 1100px) {
  .dict-grid {
    grid-template-columns: 1fr;
  }
}
</style>
