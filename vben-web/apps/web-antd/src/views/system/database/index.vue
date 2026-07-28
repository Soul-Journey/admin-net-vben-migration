<script setup lang="ts">
import type { TableColumnsType } from 'ant-design-vue';

import type {
  DatabaseColumnRecord,
  DatabaseTableRecord,
  SaveDatabaseColumnParams,
} from '#/api';

import { computed, onMounted, reactive, ref } from 'vue';

import { IconifyIcon } from '@vben/icons';
import { useUserStore } from '@vben/stores';

import {
  Alert,
  Button,
  Checkbox,
  Empty,
  Form,
  Input,
  InputNumber,
  message,
  Modal,
  Radio,
  Select,
  Space,
  Table,
  Tag,
  Tooltip,
} from 'ant-design-vue';

import {
  addDatabaseColumnApi,
  addDatabaseTableApi,
  createDatabaseEntityApi,
  createDatabaseSeedApi,
  deleteDatabaseColumnApi,
  listBackendNamespacesApi,
  listDatabaseColumnsApi,
  listDatabasesApi,
  listDatabaseTablesApi,
  listDatabaseTypesApi,
  listEntityBaseClassesApi,
  updateDatabaseColumnApi,
  updateDatabaseTableApi,
} from '#/api';

defineOptions({ name: 'AdminNetSystemDatabase' });

type BuilderPreset = 'base' | 'normal' | 'primary' | 'tenant';
type ColumnFormState = SaveDatabaseColumnParams & { oldColumnName?: string };
type GenerateMode = 'entity' | 'seed';
type TableMode = 'add' | 'edit';

const SUPER_ADMIN_ACCOUNT = 999;
const identifierPattern = /^[\p{L}_][\p{L}\p{N}_]{0,127}$/u;
const userStore = useUserStore();

const loading = ref(false);
const tableLoading = ref(false);
const columnLoading = ref(false);
const submitLoading = ref(false);
const accessDenied = ref(false);
const guideOpen = ref(false);
const tableDialogOpen = ref(false);
const columnDialogOpen = ref(false);
const generateDialogOpen = ref(false);
const dangerDialogOpen = ref(false);
const tableMode = ref<TableMode>('add');
const columnMode = ref<'add' | 'edit'>('add');
const generateMode = ref<GenerateMode>('entity');
const selectedDatabase = ref('');
const selectedTableName = ref('');
const databases = ref<string[]>([]);
const tables = ref<DatabaseTableRecord[]>([]);
const columns = ref<DatabaseColumnRecord[]>([]);
const databaseTypes = ref<string[]>([]);
const backendNamespaces = ref<string[]>([]);
const baseClasses = ref<Array<{ label: string; value: string }>>([]);
const tableBuilderColumns = ref<SaveDatabaseColumnParams[]>([]);
const deletingColumn = ref<DatabaseColumnRecord>();
const dangerConfirmation = ref('');

const tableForm = reactive({
  description: '',
  oldTableName: '',
  tableName: '',
});
const columnForm = reactive<ColumnFormState>({
  columnDescription: '',
  dataType: 'varchar',
  dbColumnName: '',
  decimalDigits: 0,
  isIdentity: 0,
  isNullable: 1,
  isPrimarykey: 0,
  length: 32,
});
const generateForm = reactive({
  baseClassName: 'EntityBase',
  entityName: '',
  filterExistingData: true,
  position: '',
  suffix: '',
});

const isSuperAdmin = computed(
  () =>
    Number((userStore.userInfo as any)?.accountType) === SUPER_ADMIN_ACCOUNT,
);
const selectedTable = computed(() =>
  tables.value.find((item) => item.name === selectedTableName.value),
);
const primaryKeyCount = computed(
  () => columns.value.filter((item) => item.isPrimarykey).length,
);
const nullableCount = computed(
  () => columns.value.filter((item) => item.isNullable).length,
);
const tableOptions = computed(() =>
  tables.value.map((item) => ({
    label: item.description ? `${item.name}（${item.description}）` : item.name,
    value: item.name,
  })),
);
const typeOptions = computed(() =>
  databaseTypes.value.map((value) => ({ label: value, value })),
);
const tableDialogTitle = computed(() =>
  tableMode.value === 'add' ? '新建数据表' : '编辑数据表',
);
const columnDialogTitle = computed(() =>
  columnMode.value === 'add' ? '新增字段' : '编辑字段',
);
const generateDialogTitle = computed(() =>
  generateMode.value === 'entity' ? '生成实体类' : '生成种子数据',
);

const tableColumns: TableColumnsType<DatabaseColumnRecord> = [
  { key: 'index', title: '序号', width: 58 },
  {
    dataIndex: 'dbColumnName',
    key: 'dbColumnName',
    title: '字段名',
    width: 190,
  },
  {
    dataIndex: 'columnDescription',
    key: 'columnDescription',
    title: '中文说明',
    width: 210,
  },
  { dataIndex: 'dataType', key: 'dataType', title: '数据类型', width: 128 },
  { key: 'rules', title: '字段约束', width: 230 },
  { key: 'size', title: '长度 / 小数位', width: 130 },
  {
    dataIndex: 'defaultValue',
    key: 'defaultValue',
    title: '默认值',
    width: 150,
  },
  { fixed: 'right', key: 'actions', title: '操作', width: 96 },
];

function isRequestForbidden(error: unknown) {
  const value = error as { response?: { status?: number }; status?: number };
  return value?.response?.status === 403 || value?.status === 403;
}

function asDatabaseColumn(value: unknown) {
  return value as DatabaseColumnRecord;
}

function assertIdentifier(value: string, label: string) {
  if (!identifierPattern.test(value.trim())) {
    message.warning(`${label}只能包含字母、数字、下划线，且不能以数字开头`);
    return false;
  }
  return true;
}

function resetColumnForm() {
  Object.assign(columnForm, {
    columnDescription: '',
    dataType: databaseTypes.value.includes('varchar')
      ? 'varchar'
      : databaseTypes.value[0] || 'varchar',
    dbColumnName: '',
    decimalDigits: 0,
    isIdentity: 0,
    isNullable: 1,
    isPrimarykey: 0,
    length: 32,
    oldColumnName: undefined,
  });
}

function makeBuilderColumn(preset: BuilderPreset): SaveDatabaseColumnParams {
  if (preset === 'primary') {
    return {
      columnDescription: '主键Id',
      dataType: 'bigint',
      dbColumnName: 'Id',
      decimalDigits: 0,
      isIdentity: 0,
      isNullable: 0,
      isPrimarykey: 1,
      length: 0,
    };
  }
  if (preset === 'tenant') {
    return {
      columnDescription: '租户Id',
      dataType: 'bigint',
      dbColumnName: 'TenantId',
      decimalDigits: 0,
      isIdentity: 0,
      isNullable: 1,
      isPrimarykey: 0,
      length: 0,
    };
  }
  return {
    columnDescription: '',
    dataType: 'varchar',
    dbColumnName: '',
    decimalDigits: 0,
    isIdentity: 0,
    isNullable: 1,
    isPrimarykey: 0,
    length: 32,
  };
}

function addBuilderColumn(preset: BuilderPreset) {
  if (preset === 'base') {
    const baseFields: Array<[string, string, string, number, number]> = [
      ['CreateTime', '创建时间', 'datetime', 0, 1],
      ['UpdateTime', '更新时间', 'datetime', 0, 1],
      ['CreateUserId', '创建者Id', 'bigint', 0, 1],
      ['CreateUserName', '创建者姓名', 'varchar', 64, 1],
      ['UpdateUserId', '修改者Id', 'bigint', 0, 1],
      ['UpdateUserName', '修改者姓名', 'varchar', 64, 1],
      ['CreateOrgId', '创建者部门Id', 'bigint', 0, 1],
      ['CreateOrgName', '创建者部门名称', 'varchar', 64, 1],
      ['IsDelete', '软删除', 'bit', 0, 0],
    ];
    const existing = new Set(
      tableBuilderColumns.value.map((item) => item.dbColumnName.toLowerCase()),
    );
    for (const [
      dbColumnName,
      columnDescription,
      dataType,
      length,
      isNullable,
    ] of baseFields) {
      if (existing.has(dbColumnName.toLowerCase())) continue;
      tableBuilderColumns.value.push({
        columnDescription,
        dataType,
        dbColumnName,
        decimalDigits: 0,
        isIdentity: 0,
        isNullable,
        isPrimarykey: 0,
        length,
      });
    }
    return;
  }
  tableBuilderColumns.value.push(makeBuilderColumn(preset));
}

function moveBuilderColumn(index: number, offset: number) {
  const target = index + offset;
  if (target < 0 || target >= tableBuilderColumns.value.length) return;
  const [item] = tableBuilderColumns.value.splice(index, 1);
  if (item) tableBuilderColumns.value.splice(target, 0, item);
}

async function loadColumns() {
  columns.value = [];
  if (!selectedDatabase.value || !selectedTableName.value) return;
  columnLoading.value = true;
  try {
    columns.value = await listDatabaseColumnsApi(
      selectedTableName.value,
      selectedDatabase.value,
    );
  } finally {
    columnLoading.value = false;
  }
}

async function loadTables(preferredTable?: string) {
  tables.value = [];
  columns.value = [];
  selectedTableName.value = '';
  if (!selectedDatabase.value) return;
  tableLoading.value = true;
  try {
    const [tableList, typeList] = await Promise.all([
      listDatabaseTablesApi(selectedDatabase.value),
      listDatabaseTypesApi(selectedDatabase.value),
    ]);
    tables.value = (tableList ?? []).filter(
      (item) => !item.name.startsWith('zero_'),
    );
    databaseTypes.value = typeList ?? [];
    selectedTableName.value =
      tables.value.find((item) => item.name === preferredTable)?.name ||
      tables.value[0]?.name ||
      '';
    await loadColumns();
  } finally {
    tableLoading.value = false;
  }
}

async function loadDatabases() {
  if (!isSuperAdmin.value) return;
  loading.value = true;
  try {
    databases.value = await listDatabasesApi();
    selectedDatabase.value = databases.value[0] || '';
    await loadTables();
  } catch (error) {
    accessDenied.value = isRequestForbidden(error);
  } finally {
    loading.value = false;
  }
}

async function handleRefresh() {
  await loadTables(selectedTableName.value);
  message.success('库表结构已刷新');
}

function openAddTable() {
  tableMode.value = 'add';
  Object.assign(tableForm, {
    description: '',
    oldTableName: '',
    tableName: '',
  });
  tableBuilderColumns.value = [
    makeBuilderColumn('primary'),
    makeBuilderColumn('normal'),
  ];
  tableDialogOpen.value = true;
}

function openEditTable() {
  if (!selectedTable.value) return message.warning('请先选择数据表');
  tableMode.value = 'edit';
  Object.assign(tableForm, {
    description: selectedTable.value.description || '',
    oldTableName: selectedTable.value.name,
    tableName: selectedTable.value.name,
  });
  tableDialogOpen.value = true;
}

function validateBuilderColumns() {
  if (tableBuilderColumns.value.length === 0) {
    message.warning('至少添加一个字段');
    return false;
  }
  const names = new Set<string>();
  for (const item of tableBuilderColumns.value) {
    if (!assertIdentifier(item.dbColumnName, '字段名')) return false;
    const key = item.dbColumnName.toLowerCase();
    if (names.has(key)) {
      message.warning(`字段 ${item.dbColumnName} 重复`);
      return false;
    }
    names.add(key);
    if (!item.dataType) {
      message.warning(`请为字段 ${item.dbColumnName} 选择数据类型`);
      return false;
    }
    if (item.isIdentity === 1 && item.isNullable === 1) {
      message.warning(`自增字段 ${item.dbColumnName} 不能设置为可空`);
      return false;
    }
  }
  return true;
}

async function saveTable() {
  const tableName = tableForm.tableName.trim();
  if (!assertIdentifier(tableName, '表名')) return;
  if (tableMode.value === 'add' && !validateBuilderColumns()) return;
  submitLoading.value = true;
  try {
    if (tableMode.value === 'add') {
      await addDatabaseTableApi({
        configId: selectedDatabase.value,
        dbColumnInfoList: tableBuilderColumns.value,
        description: tableForm.description.trim(),
        tableName,
      });
      message.success('数据表创建成功');
    } else {
      await updateDatabaseTableApi({
        configId: selectedDatabase.value,
        description: tableForm.description.trim(),
        oldTableName: tableForm.oldTableName,
        tableName,
      });
      message.success('数据表信息已更新');
    }
    tableDialogOpen.value = false;
    await loadTables(tableName);
  } finally {
    submitLoading.value = false;
  }
}

function openAddColumn() {
  if (!selectedTableName.value) return message.warning('请先选择数据表');
  columnMode.value = 'add';
  resetColumnForm();
  columnDialogOpen.value = true;
}

function openEditColumn(record: DatabaseColumnRecord) {
  columnMode.value = 'edit';
  Object.assign(columnForm, {
    columnDescription: record.columnDescription || '',
    dataType: record.dataType,
    dbColumnName: record.dbColumnName,
    decimalDigits: record.decimalDigits,
    isIdentity: record.isIdentity ? 1 : 0,
    isNullable: record.isNullable ? 1 : 0,
    isPrimarykey: record.isPrimarykey ? 1 : 0,
    length: record.length,
    oldColumnName: record.dbColumnName,
  });
  columnDialogOpen.value = true;
}

async function saveColumn() {
  const columnName = columnForm.dbColumnName.trim();
  if (!assertIdentifier(columnName, '字段名')) return;
  submitLoading.value = true;
  try {
    if (columnMode.value === 'add') {
      if (!columnForm.dataType) return message.warning('请选择数据类型');
      if (columnForm.isIdentity === 1 && columnForm.isNullable === 1) {
        return message.warning('自增字段不能设置为可空');
      }
      await addDatabaseColumnApi({
        ...columnForm,
        columnDescription: columnForm.columnDescription?.trim(),
        configId: selectedDatabase.value,
        dbColumnName: columnName,
        tableName: selectedTableName.value,
      });
      message.success('字段添加成功');
    } else {
      await updateDatabaseColumnApi({
        columnName,
        configId: selectedDatabase.value,
        description: columnForm.columnDescription?.trim(),
        oldColumnName: columnForm.oldColumnName || columnName,
        tableName: selectedTableName.value,
      });
      message.success('字段信息已更新');
    }
    columnDialogOpen.value = false;
    await loadColumns();
  } finally {
    submitLoading.value = false;
  }
}

function openDeleteColumn(record: DatabaseColumnRecord) {
  deletingColumn.value = record;
  dangerConfirmation.value = '';
  dangerDialogOpen.value = true;
}

async function deleteColumn() {
  const record = deletingColumn.value;
  if (!record || dangerConfirmation.value !== record.dbColumnName) return;
  submitLoading.value = true;
  try {
    await deleteDatabaseColumnApi({
      configId: selectedDatabase.value,
      dbColumnName: record.dbColumnName,
      tableName: selectedTableName.value,
    });
    dangerDialogOpen.value = false;
    message.success('字段已删除');
    await loadColumns();
  } finally {
    submitLoading.value = false;
  }
}

async function loadGenerationOptions() {
  if (backendNamespaces.value.length > 0) return;
  const [namespaces, classes] = await Promise.all([
    listBackendNamespacesApi(),
    listEntityBaseClassesApi(),
  ]);
  backendNamespaces.value = namespaces ?? [];
  baseClasses.value = classes ?? [];
}

async function openGenerate(mode: GenerateMode) {
  if (!selectedTableName.value) return message.warning('请先选择数据表');
  generateMode.value = mode;
  submitLoading.value = true;
  try {
    await loadGenerationOptions();
    Object.assign(generateForm, {
      baseClassName:
        baseClasses.value.find((item) => item.value === 'EntityBase')?.value ||
        '',
      entityName: selectedTableName.value
        .split(/[_\s-]+/)
        .map((part) =>
          part ? `${part[0]?.toUpperCase()}${part.slice(1)}` : '',
        )
        .join(''),
      filterExistingData: true,
      position: backendNamespaces.value[0] || '',
      suffix: '',
    });
    generateDialogOpen.value = true;
  } finally {
    submitLoading.value = false;
  }
}

async function saveGeneration() {
  if (!generateForm.position) return message.warning('请选择代码存放位置');
  if (
    generateMode.value === 'entity' &&
    !assertIdentifier(generateForm.entityName, '实体名')
  )
    return;
  if (
    generateMode.value === 'seed' &&
    generateForm.suffix &&
    !/^[A-Za-z0-9_]+$/.test(generateForm.suffix)
  ) {
    return message.warning('种子后缀只能包含英文字母、数字和下划线');
  }
  submitLoading.value = true;
  try {
    if (generateMode.value === 'entity') {
      await createDatabaseEntityApi({
        baseClassName: generateForm.baseClassName || undefined,
        configId: selectedDatabase.value,
        entityName: generateForm.entityName,
        position: generateForm.position,
        tableName: selectedTableName.value,
      });
      message.success('实体类已生成到后台项目，请检查 Git 变更后再使用');
    } else {
      await createDatabaseSeedApi({
        configId: selectedDatabase.value,
        filterExistingData: generateForm.filterExistingData,
        position: generateForm.position,
        suffix: generateForm.suffix || undefined,
        tableName: selectedTableName.value,
      });
      message.success('种子数据文件已生成，请检查内容后再提交');
    }
    generateDialogOpen.value = false;
  } finally {
    submitLoading.value = false;
  }
}

onMounted(loadDatabases);
</script>

<template>
  <div class="database-page">
    <Alert
      v-if="!isSuperAdmin || accessDenied"
      message="仅超级管理员可以使用库表管理"
      description="这里会直接读取和修改数据库结构，并能在服务器项目目录生成代码。请使用超级管理员账号进入。"
      show-icon
      type="warning"
    />

    <section v-else class="database-panel">
      <header class="page-heading">
        <div>
          <h2>库表管理</h2>
          <p>
            查看已配置数据库的表和字段结构；建表、改表与代码生成只面向开发维护
          </p>
        </div>
        <Space>
          <Button @click="guideOpen = true">
            <template #icon><IconifyIcon icon="lucide:circle-help" /></template>
            使用说明
          </Button>
          <Button :loading="loading || tableLoading" @click="handleRefresh">
            <template #icon><IconifyIcon icon="lucide:refresh-cw" /></template>
            刷新结构
          </Button>
        </Space>
      </header>

      <Alert
        class="safety-alert"
        message="结构修改会立即作用于真实数据库"
        description="本页没有模拟数据。删除字段可能永久丢失整列数据，生成代码也会写入后台项目目录；操作前请先备份，并确认当前选择的数据库。"
        show-icon
        type="warning"
      />

      <div class="selector-bar">
        <div class="selector-field">
          <span>数据库</span>
          <Select
            v-model:value="selectedDatabase"
            :loading="loading"
            :options="databases.map((value) => ({ label: value, value }))"
            placeholder="选择数据库"
            @change="loadTables()"
          />
        </div>
        <div class="selector-field table-selector">
          <span>数据表</span>
          <Select
            v-model:value="selectedTableName"
            :filter-option="
              (input, option) =>
                String(option?.label || '')
                  .toLowerCase()
                  .includes(input.toLowerCase())
            "
            :loading="tableLoading"
            :options="tableOptions"
            allow-clear
            placeholder="输入表名或中文说明查找"
            show-search
            @change="loadColumns"
          />
        </div>
        <Space class="selector-actions" wrap>
          <Button type="primary" @click="openAddTable">
            <template #icon><IconifyIcon icon="lucide:table-2" /></template>
            新建表
          </Button>
          <Button :disabled="!selectedTableName" @click="openEditTable">
            <template #icon><IconifyIcon icon="lucide:pencil" /></template>
            编辑表
          </Button>
          <Button :disabled="!selectedTableName" @click="openAddColumn">
            <template #icon><IconifyIcon icon="lucide:columns-3" /></template>
            新增字段
          </Button>
          <Button
            :disabled="!selectedTableName"
            @click="openGenerate('entity')"
          >
            <template #icon><IconifyIcon icon="lucide:file-code-2" /></template>
            生成实体
          </Button>
          <Button :disabled="!selectedTableName" @click="openGenerate('seed')">
            <template #icon><IconifyIcon icon="lucide:sprout" /></template>
            生成种子
          </Button>
        </Space>
      </div>

      <div class="metrics-strip">
        <div>
          <span>当前数据库</span
          ><strong class="text-metric">{{ selectedDatabase || '-' }}</strong>
        </div>
        <div>
          <span>数据表</span><strong>{{ tables.length }}</strong
          ><small>已排除 zero_ 临时表</small>
        </div>
        <div>
          <span>字段</span><strong>{{ columns.length }}</strong
          ><small>{{ selectedTableName || '尚未选择表' }}</small>
        </div>
        <div>
          <span>结构约束</span><strong>{{ primaryKeyCount }}</strong
          ><small>主键 · {{ nullableCount }} 个可空字段</small>
        </div>
      </div>

      <div class="table-title">
        <div>
          <h3>{{ selectedTableName || '字段结构' }}</h3>
          <p>
            {{
              selectedTable?.description ||
              '选择数据表后查看字段、类型、主键和默认值'
            }}
          </p>
        </div>
        <Tag v-if="selectedTableName" color="blue">真实数据库结构</Tag>
      </div>

      <Table
        v-if="selectedTableName"
        :columns="tableColumns"
        :data-source="columns"
        :loading="columnLoading"
        :pagination="false"
        :scroll="{ x: 1190 }"
        row-key="dbColumnName"
        size="small"
      >
        <template #bodyCell="{ column, record, index }">
          <template v-if="column.key === 'index'">{{ index + 1 }}</template>
          <template v-else-if="column.key === 'dbColumnName'">
            <span class="column-name">{{ record.dbColumnName }}</span>
          </template>
          <template v-else-if="column.key === 'columnDescription'">
            {{ record.columnDescription || '-' }}
          </template>
          <template v-else-if="column.key === 'dataType'">
            <Tag>{{ record.dataType }}</Tag>
          </template>
          <template v-else-if="column.key === 'rules'">
            <Space :size="4" wrap>
              <Tag v-if="record.isPrimarykey" color="blue">主键</Tag>
              <Tag v-if="record.isIdentity" color="purple">自增</Tag>
              <Tag :color="record.isNullable ? 'green' : 'default'">
                {{ record.isNullable ? '允许为空' : '必填' }}
              </Tag>
            </Space>
          </template>
          <template v-else-if="column.key === 'size'">
            {{ record.length || '-'
            }}<span v-if="record.decimalDigits">
              / {{ record.decimalDigits }}</span
            >
          </template>
          <template v-else-if="column.key === 'defaultValue'">
            <span class="muted-value">{{ record.defaultValue || '-' }}</span>
          </template>
          <template v-else-if="column.key === 'actions'">
            <Space :size="2">
              <Tooltip title="编辑字段名称和中文说明">
                <Button
                  size="small"
                  type="text"
                  @click="openEditColumn(asDatabaseColumn(record))"
                >
                  <template #icon>
                    <IconifyIcon icon="lucide:pencil" />
                  </template>
                </Button>
              </Tooltip>
              <Tooltip
                :title="
                  record.isPrimarykey
                    ? '主键字段不能直接删除'
                    : '删除字段及其整列数据'
                "
              >
                <Button
                  danger
                  :disabled="record.isPrimarykey"
                  size="small"
                  type="text"
                  @click="openDeleteColumn(asDatabaseColumn(record))"
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
      <Empty v-else description="请选择数据库和数据表" />
    </section>

    <Modal
      v-model:open="guideOpen"
      :footer="null"
      title="库表管理怎么用"
      width="720px"
    >
      <div class="guide-list">
        <article>
          <b>查看结构</b>
          <p>
            选择数据库和数据表后，下方显示真实字段。刷新结构只重新读取元数据，不会修改数据库。
          </p>
        </article>
        <article>
          <b>新建表 / 编辑表</b>
          <p>
            新建表会立即执行建表；编辑表用于重命名和修改说明。旧版的“删除表”本来就是禁用状态，新版也不提供快捷删表。
          </p>
        </article>
        <article>
          <b>新增 / 编辑字段</b>
          <p>
            新增字段可以设置类型、长度、主键、自增和可空；编辑字段只调整名称与说明，不偷偷改变字段类型。
          </p>
        </article>
        <article>
          <b>生成实体</b>
          <p>
            根据选中表生成 C#
            实体文件。文件已存在时后台会拒绝覆盖，生成后要先查看 Git
            变更并重新编译。
          </p>
        </article>
        <article>
          <b>生成种子</b>
          <p>
            把表内现有数据生成 C#
            初始化数据文件，可能包含业务数据或敏感字段。只应对明确可公开的基础配置表使用。
          </p>
        </article>
        <Alert
          message="建议顺序：备份数据库 → 确认所选库 → 修改结构 → 刷新核对 → 检查 Git 变更"
          show-icon
          type="info"
        />
      </div>
    </Modal>

    <Modal
      v-model:open="tableDialogOpen"
      :confirm-loading="submitLoading"
      :title="tableDialogTitle"
      :width="tableMode === 'add' ? 1040 : 600"
      ok-text="确认"
      @ok="saveTable"
    >
      <Form layout="vertical">
        <div class="form-two-columns">
          <Form.Item label="表名" required>
            <Input
              v-model:value="tableForm.tableName"
              :maxlength="128"
              placeholder="例如 SysProduct"
            />
          </Form.Item>
          <Form.Item label="中文说明">
            <Input
              v-model:value="tableForm.description"
              :maxlength="256"
              placeholder="例如 商品信息表"
            />
          </Form.Item>
        </div>
      </Form>

      <template v-if="tableMode === 'add'">
        <div class="builder-toolbar">
          <div><b>字段设计</b><span>按从上到下的顺序创建字段</span></div>
          <Space wrap>
            <Button size="small" @click="addBuilderColumn('primary')">
              主键字段
            </Button>
            <Button size="small" @click="addBuilderColumn('normal')">
              普通字段
            </Button>
            <Button size="small" @click="addBuilderColumn('tenant')">
              租户字段
            </Button>
            <Button size="small" @click="addBuilderColumn('base')">
              基础字段组
            </Button>
          </Space>
        </div>
        <div class="column-builder">
          <div
            v-for="(item, index) in tableBuilderColumns"
            :key="index"
            class="builder-row"
          >
            <Input v-model:value="item.dbColumnName" placeholder="字段名" />
            <Input
              v-model:value="item.columnDescription"
              placeholder="中文说明"
            />
            <Select
              v-model:value="item.dataType"
              :options="typeOptions"
              show-search
            />
            <InputNumber
              v-model:value="item.length"
              :min="0"
              :max="65535"
              placeholder="长度"
            />
            <Checkbox
              :checked="item.isPrimarykey === 1"
              @change="item.isPrimarykey = $event.target.checked ? 1 : 0"
            >
              主键
            </Checkbox>
            <Checkbox
              :checked="item.isNullable === 1"
              @change="item.isNullable = $event.target.checked ? 1 : 0"
            >
              可空
            </Checkbox>
            <div class="builder-actions">
              <Button
                :disabled="index === 0"
                size="small"
                type="text"
                @click="moveBuilderColumn(index, -1)"
              >
                <IconifyIcon icon="lucide:arrow-up" />
              </Button>
              <Button
                :disabled="index === tableBuilderColumns.length - 1"
                size="small"
                type="text"
                @click="moveBuilderColumn(index, 1)"
              >
                <IconifyIcon icon="lucide:arrow-down" />
              </Button>
              <Button
                danger
                size="small"
                type="text"
                @click="tableBuilderColumns.splice(index, 1)"
              >
                <IconifyIcon icon="lucide:trash-2" />
              </Button>
            </div>
          </div>
        </div>
      </template>
      <Alert
        class="modal-note"
        :message="
          tableMode === 'add'
            ? '确认后会立即在所选数据库创建真实数据表'
            : '重命名数据表可能影响现有实体和查询代码'
        "
        show-icon
        type="warning"
      />
    </Modal>

    <Modal
      v-model:open="columnDialogOpen"
      :confirm-loading="submitLoading"
      :title="columnDialogTitle"
      width="660px"
      @ok="saveColumn"
    >
      <Form layout="vertical">
        <div class="form-two-columns">
          <Form.Item label="字段名" required>
            <Input v-model:value="columnForm.dbColumnName" :maxlength="128" />
          </Form.Item>
          <Form.Item label="中文说明">
            <Input
              v-model:value="columnForm.columnDescription"
              :maxlength="256"
            />
          </Form.Item>
        </div>
        <template v-if="columnMode === 'add'">
          <div class="form-three-columns">
            <Form.Item label="数据类型" required>
              <Select
                v-model:value="columnForm.dataType"
                :options="typeOptions"
                show-search
              />
            </Form.Item>
            <Form.Item label="长度">
              <InputNumber
                v-model:value="columnForm.length"
                :max="65535"
                :min="0"
              />
            </Form.Item>
            <Form.Item label="小数位">
              <InputNumber
                v-model:value="columnForm.decimalDigits"
                :max="30"
                :min="0"
              />
            </Form.Item>
          </div>
          <div class="option-row">
            <Checkbox
              :checked="columnForm.isPrimarykey === 1"
              @change="columnForm.isPrimarykey = $event.target.checked ? 1 : 0"
            >
              主键字段
            </Checkbox>
            <Checkbox
              :checked="columnForm.isIdentity === 1"
              @change="columnForm.isIdentity = $event.target.checked ? 1 : 0"
            >
              自动增长
            </Checkbox>
            <Checkbox
              :checked="columnForm.isNullable === 1"
              @change="columnForm.isNullable = $event.target.checked ? 1 : 0"
            >
              允许为空
            </Checkbox>
          </div>
        </template>
        <Alert
          v-else
          message="为避免隐式数据转换，编辑字段只修改名称和中文说明；类型调整请通过经过评审的数据库迁移完成。"
          show-icon
          type="info"
        />
      </Form>
    </Modal>

    <Modal
      v-model:open="generateDialogOpen"
      :confirm-loading="submitLoading"
      :title="generateDialogTitle"
      width="600px"
      @ok="saveGeneration"
    >
      <Form layout="vertical">
        <div class="readonly-field">
          <span>当前数据表</span><b>{{ selectedTableName }}</b>
        </div>
        <Form.Item label="代码存放位置" required>
          <Select
            v-model:value="generateForm.position"
            :options="
              backendNamespaces.map((value) => ({ label: value, value }))
            "
            placeholder="选择后台项目"
          />
        </Form.Item>
        <template v-if="generateMode === 'entity'">
          <Form.Item label="实体类名称" required>
            <Input v-model:value="generateForm.entityName" :maxlength="128" />
          </Form.Item>
          <Form.Item label="继承的基础实体">
            <Select
              v-model:value="generateForm.baseClassName"
              :options="baseClasses"
              allow-clear
            />
          </Form.Item>
        </template>
        <template v-else>
          <Form.Item label="文件名后缀">
            <Input
              v-model:value="generateForm.suffix"
              :maxlength="64"
              placeholder="可选，例如 Application"
            />
          </Form.Item>
          <Form.Item label="重复数据处理">
            <Radio.Group v-model:value="generateForm.filterExistingData">
              <Radio :value="true">过滤其他种子文件已有的数据</Radio>
              <Radio :value="false">保留当前表全部数据</Radio>
            </Radio.Group>
          </Form.Item>
        </template>
        <Alert
          :message="
            generateMode === 'entity'
              ? '生成文件不会覆盖同名文件；完成后需要检查代码并重新编译。'
              : '种子文件会包含当前表的数据内容，生成前请确认没有密码、令牌等敏感数据。'
          "
          show-icon
          type="warning"
        />
      </Form>
    </Modal>

    <Modal
      v-model:open="dangerDialogOpen"
      :confirm-loading="submitLoading"
      :ok-button-props="{
        danger: true,
        disabled: dangerConfirmation !== deletingColumn?.dbColumnName,
      }"
      ok-text="永久删除字段"
      title="删除字段及整列数据"
      width="520px"
      @ok="deleteColumn"
    >
      <Alert
        :message="`字段 ${deletingColumn?.dbColumnName || ''} 的现有数据会被永久删除`"
        description="这个操作通常无法撤销。请先完成数据库备份，并确认没有程序仍在使用该字段。"
        show-icon
        type="error"
      />
      <label class="confirmation-field">
        <span
          >输入字段名 <b>{{ deletingColumn?.dbColumnName }}</b> 确认</span
        >
        <Input v-model:value="dangerConfirmation" autocomplete="off" />
      </label>
    </Modal>
  </div>
</template>

<style scoped>
.database-page {
  min-height: 100%;
  padding: 12px;
}

.database-panel {
  min-height: calc(100vh - 122px);
  overflow: hidden;
  background: #fff;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
}

.page-heading {
  display: flex;
  gap: 20px;
  align-items: flex-start;
  justify-content: space-between;
  padding: 16px 18px 12px;
}

.page-heading h2,
.table-title h3 {
  margin: 0;
  font-size: 18px;
  font-weight: 650;
  color: #111827;
}

.page-heading p,
.table-title p {
  margin: 4px 0 0;
  font-size: 13px;
  color: #6b7280;
}

.safety-alert {
  margin: 0 18px 12px;
}

.selector-bar {
  display: flex;
  gap: 12px;
  align-items: flex-end;
  padding: 12px 18px;
  background: #fafbfc;
  border-block: 1px solid #eef0f3;
}

.selector-field {
  display: grid;
  flex: 0 0 210px;
  gap: 6px;
}

.selector-field > span {
  font-size: 12px;
  font-weight: 600;
  color: #4b5563;
}

.table-selector {
  flex-basis: 360px;
}

.selector-actions {
  margin-left: auto;
}

.metrics-strip {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  border-bottom: 1px solid #e8ebef;
}

.metrics-strip > div {
  display: grid;
  min-height: 74px;
  padding: 13px 18px;
  border-right: 1px solid #e8ebef;
}

.metrics-strip > div:last-child {
  border-right: 0;
}

.metrics-strip span,
.metrics-strip small {
  overflow: hidden;
  text-overflow: ellipsis;
  font-size: 12px;
  color: #7b8494;
  white-space: nowrap;
}

.metrics-strip strong {
  font-size: 22px;
  line-height: 28px;
  color: #111827;
}

.metrics-strip .text-metric {
  overflow: hidden;
  text-overflow: ellipsis;
  font-size: 15px;
  white-space: nowrap;
}

.table-title {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 14px 18px 10px;
}

.column-name {
  font-family: Consolas, monospace;
  font-weight: 600;
  color: #1d4ed8;
}

.muted-value {
  font-family: Consolas, monospace;
  color: #6b7280;
}

.guide-list {
  display: grid;
  gap: 10px;
}

.guide-list article {
  padding: 11px 13px;
  background: #fafafa;
  border: 1px solid #e5e7eb;
  border-radius: 6px;
}

.guide-list b {
  color: #111827;
}

.guide-list p {
  margin: 4px 0 0;
  line-height: 1.65;
  color: #5f6876;
}

.form-two-columns {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 14px;
}

.form-three-columns {
  display: grid;
  grid-template-columns: 1.4fr 1fr 1fr;
  gap: 14px;
}

.builder-toolbar {
  display: flex;
  gap: 12px;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 10px;
}

.builder-toolbar > div {
  display: grid;
}

.builder-toolbar span {
  font-size: 12px;
  color: #7b8494;
}

.column-builder {
  display: grid;
  gap: 8px;
  max-height: 380px;
  padding: 2px 4px 2px 0;
  overflow-y: auto;
}

.builder-row {
  display: grid;
  grid-template-columns: 1.25fr 1.25fr 1fr 88px 62px 62px 104px;
  gap: 8px;
  align-items: center;
  padding: 8px;
  border: 1px solid #e5e7eb;
  border-radius: 6px;
}

.builder-actions {
  display: flex;
  justify-content: flex-end;
}

.modal-note {
  margin-top: 14px;
}

.option-row {
  display: flex;
  gap: 24px;
  padding: 12px;
  margin: 2px 0 18px;
  background: #f6f8fa;
  border-radius: 6px;
}

.readonly-field {
  display: flex;
  justify-content: space-between;
  padding: 11px 13px;
  margin-bottom: 16px;
  background: #fafafa;
  border: 1px solid #e5e7eb;
  border-radius: 6px;
}

.readonly-field span {
  color: #6b7280;
}

.confirmation-field {
  display: grid;
  gap: 8px;
  margin-top: 16px;
}

.confirmation-field span {
  color: #4b5563;
}

@media (max-width: 1100px) {
  .selector-bar {
    flex-wrap: wrap;
    align-items: stretch;
  }

  .selector-field,
  .table-selector {
    flex: 1 1 260px;
  }

  .selector-actions {
    flex: 1 1 100%;
    margin-left: 0;
  }

  .metrics-strip {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .metrics-strip > div:nth-child(2) {
    border-right: 0;
  }

  .builder-row {
    grid-template-columns: 1fr 1fr 1fr 80px;
  }

  .builder-row > :nth-child(5),
  .builder-row > :nth-child(6),
  .builder-row > :nth-child(7) {
    grid-column: auto;
  }
}

@media (max-width: 700px) {
  .database-page {
    padding: 6px;
  }

  .page-heading {
    flex-direction: column;
  }

  .metrics-strip,
  .form-two-columns,
  .form-three-columns {
    grid-template-columns: 1fr;
  }

  .metrics-strip > div {
    border-right: 0;
  }

  .builder-row {
    grid-template-columns: 1fr;
  }
}
</style>
