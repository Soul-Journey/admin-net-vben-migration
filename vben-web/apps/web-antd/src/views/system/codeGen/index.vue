<script setup lang="ts">
import type { FormInstance, TableColumnsType } from 'ant-design-vue';
import type { Rule } from 'ant-design-vue/es/form';

import type {
  CodeGenColumnRecord,
  CodeGenDatabaseRecord,
  CodeGenFieldConfig,
  CodeGenTableRecord,
  SaveCodeGenParams,
  SysCodeGenRecord,
  SysDictTypeRecord,
  SysPrintRecord,
  TableUniqueConfigItem,
} from '#/api';

import { computed, onMounted, reactive, ref } from 'vue';

import { IconifyIcon } from '@vben/icons';
import { useUserStore } from '@vben/stores';

import {
  Alert,
  Button,
  Checkbox,
  Dropdown,
  Form,
  Input,
  InputNumber,
  Menu,
  message,
  Modal,
  Select,
  Space,
  Switch,
  Table,
  Tabs,
  Tag,
  Tooltip,
} from 'ant-design-vue';

import {
  addCodeGenApi,
  deleteCodeGenApi,
  getCodeGenDetailApi,
  getDictDataByCodeApi,
  listCodeGenColumnsApi,
  listCodeGenDatabasesApi,
  listCodeGenFieldConfigsApi,
  listCodeGenNamespacesApi,
  listCodeGenTablesApi,
  listDictTypesApi,
  pageCodeGenApi,
  pagePrintsApi,
  previewCodeGenApi,
  runCodeGenApi,
  syncCodeGenApi,
  updateCodeGenApi,
  updateCodeGenFieldConfigsApi,
} from '#/api';
import { ADMIN_PAGINATION_PROPS } from '#/utils/pagination';

defineOptions({ name: 'AdminNetSystemCodeGen' });

type CodeGenFormState = Partial<SaveCodeGenParams> & { id?: number };

const SUPER_ADMIN_ACCOUNT = 999;
const SAFE_GENERATE_TYPES = new Set(['121', '221']);
const FRONTEND_GENERATE_TYPES = new Set(['100', '111', '200', '211']);
const identifierPattern = /^[A-Za-z_][A-Za-z0-9_]{0,127}$/;
const pagePathPattern = /^[A-Za-z][A-Za-z0-9/_-]{0,31}$/;
const userStore = useUserStore();

const loading = ref(false);
const submitLoading = ref(false);
const editorOpen = ref(false);
const guideOpen = ref(false);
const fieldsOpen = ref(false);
const fieldsLoading = ref(false);
const previewOpen = ref(false);
const previewLoading = ref(false);
const generationOpen = ref(false);
const relationOpen = ref(false);
const formRef = ref<FormInstance>();
const records = ref<SysCodeGenRecord[]>([]);
const databases = ref<CodeGenDatabaseRecord[]>([]);
const tables = ref<CodeGenTableRecord[]>([]);
const entityColumns = ref<CodeGenColumnRecord[]>([]);
const namespaces = ref<string[]>([]);
const generationTypes = ref<Array<{ label: string; value: string }>>([]);
const printTypes = ref<Array<{ label: string; value: string }>>([]);
const printTemplates = ref<SysPrintRecord[]>([]);
const dictTypes = ref<SysDictTypeRecord[]>([]);
const effectTypes = ref<Array<{ label: string; value: string }>>([]);
const queryTypes = ref<Array<{ label: string; value: string }>>([]);
const fieldConfigs = ref<CodeGenFieldConfig[]>([]);
const currentRecord = ref<SysCodeGenRecord>();
const previewFiles = ref<Record<string, string>>({});
const activePreviewFile = ref('');
const generationConfirmation = ref('');
const relationIndex = ref(-1);
const relationTables = ref<CodeGenTableRecord[]>([]);
const relationColumns = ref<CodeGenColumnRecord[]>([]);

const query = reactive({
  busName: '',
  page: 1,
  pageSize: 50,
  tableName: '',
  total: 0,
});

const formState = reactive<CodeGenFormState>({});
const relationForm = reactive<Partial<CodeGenFieldConfig>>({});

const isSuperAdmin = computed(
  () =>
    Number((userStore.userInfo as any)?.accountType) === SUPER_ADMIN_ACCOUNT,
);
const editorTitle = computed(() =>
  formState.id ? '编辑代码生成配置' : '新增代码生成配置',
);
const selectedDatabase = computed(() =>
  databases.value.find((item) => item.configId === formState.configId),
);
const tableOptions = computed(() =>
  tables.value.map((item) => ({
    label: `${item.entityName}（${item.tableName}${item.tableComment ? ` · ${item.tableComment}` : ''}）`,
    value: item.entityName,
  })),
);
const fieldOptions = computed(() =>
  entityColumns.value
    .filter((item) => item.propertyName)
    .map((item) => ({
      label: `${item.propertyName}（${item.columnComment || item.columnName}）`,
      value: item.propertyName ?? '',
    })),
);
const relationColumnOptions = computed(() =>
  relationColumns.value.map((item) => ({
    label: `${item.columnName}${item.columnComment ? `（${item.columnComment}）` : ''}`,
    value: item.columnName,
  })),
);
const previewNames = computed(() => Object.keys(previewFiles.value));
const generationExpectedText = computed(() =>
  currentRecord.value?.generateType === '221'
    ? currentRecord.value?.tableName || ''
    : '下载',
);
const canRunGeneration = computed(
  () =>
    !!currentRecord.value?.generateType &&
    SAFE_GENERATE_TYPES.has(currentRecord.value.generateType),
);

const columns: TableColumnsType<SysCodeGenRecord> = [
  { key: 'index', title: '序号', width: 58 },
  { dataIndex: 'tableName', key: 'tableName', title: '实体名称', width: 180 },
  {
    dataIndex: 'busName',
    ellipsis: true,
    key: 'busName',
    title: '业务名称',
    width: 190,
  },
  { dataIndex: 'configId', key: 'configId', title: '数据库', width: 150 },
  {
    dataIndex: 'nameSpace',
    ellipsis: true,
    key: 'nameSpace',
    title: '后端项目',
    width: 210,
  },
  {
    dataIndex: 'generateType',
    key: 'generateType',
    title: '生成方式',
    width: 150,
  },
  { dataIndex: 'authorName', key: 'authorName', title: '作者', width: 110 },
  { fixed: 'right', key: 'actions', title: '操作', width: 250 },
];

const fieldColumns: TableColumnsType<CodeGenFieldConfig> = [
  { fixed: 'left', key: 'index', title: '序号', width: 54 },
  {
    dataIndex: 'propertyName',
    fixed: 'left',
    key: 'propertyName',
    title: '实体属性',
    width: 150,
  },
  {
    dataIndex: 'columnComment',
    key: 'columnComment',
    title: '中文说明',
    width: 170,
  },
  { dataIndex: 'netType', key: 'netType', title: '.NET 类型', width: 110 },
  { dataIndex: 'effectType', key: 'effectType', title: '页面控件', width: 170 },
  {
    dataIndex: 'dictTypeCode',
    key: 'dictTypeCode',
    title: '字典 / 枚举编码',
    width: 190,
  },
  { key: 'whetherTable', title: '列表', width: 66 },
  { key: 'whetherAddUpdate', title: '增改', width: 66 },
  { key: 'whetherImport', title: '导入', width: 66 },
  { key: 'whetherRequired', title: '必填', width: 66 },
  { key: 'whetherSortable', title: '排序', width: 66 },
  { key: 'whetherQuery', title: '查询', width: 66 },
  { dataIndex: 'queryType', key: 'queryType', title: '查询方式', width: 130 },
  { dataIndex: 'orderNo', key: 'orderNo', title: '顺序', width: 90 },
  { fixed: 'right', key: 'relation', title: '关联', width: 88 },
];

const rules: Record<string, Rule[]> = {
  authorName: [{ message: '请输入作者', required: true, trigger: 'blur' }],
  busName: [{ message: '请输入业务名称', required: true, trigger: 'blur' }],
  configId: [{ message: '请选择数据库', required: true, trigger: 'change' }],
  generateType: [
    { message: '请选择生成方式', required: true, trigger: 'change' },
  ],
  nameSpace: [{ message: '请选择后端项目', required: true, trigger: 'change' }],
  pagePath: [{ message: '请输入页面目录', required: true, trigger: 'blur' }],
  tableName: [{ message: '请选择生成实体', required: true, trigger: 'change' }],
};

function generateTypeLabel(value?: string) {
  return (
    generationTypes.value.find((item) => item.value === value)?.label ||
    value ||
    '-'
  );
}

function asCodeGen(value: unknown) {
  return value as SysCodeGenRecord;
}

function asFieldConfig(value: unknown) {
  return value as CodeGenFieldConfig;
}

function isYes(value: unknown, key: string) {
  return (value as Record<string, unknown>)[key] === 'Y';
}

function setYesNo(value: unknown, key: string, checked: boolean) {
  (value as Record<string, unknown>)[key] = checked ? 'Y' : 'N';
}

function dbTypeLabel(value?: number | string) {
  const labels: Record<string, string> = {
    '0': 'MySQL',
    '1': 'SQL Server',
    '2': 'SQLite',
    '4': 'Oracle',
    '5': 'PostgreSQL',
  };
  return labels[String(value)] || String(value ?? '-');
}

function clearForm() {
  for (const key of Object.keys(formState)) {
    delete formState[key as keyof CodeGenFormState];
  }
}

async function loadRecords() {
  if (!isSuperAdmin.value) return;
  loading.value = true;
  try {
    const result = await pageCodeGenApi({
      busName: query.busName.trim() || undefined,
      page: query.page,
      pageSize: query.pageSize,
      tableName: query.tableName.trim() || undefined,
    });
    records.value = result.items ?? [];
    query.total = Number(result.total ?? 0);
  } finally {
    loading.value = false;
  }
}

async function loadEditorTables(configId?: string, preferredTable?: string) {
  tables.value = [];
  entityColumns.value = [];
  if (!configId) return;
  tables.value = await listCodeGenTablesApi(configId);
  if (
    preferredTable &&
    tables.value.some((item) => item.entityName === preferredTable)
  ) {
    await loadEntityColumns(preferredTable);
  }
}

async function loadEntityColumns(entityName?: string) {
  entityColumns.value = [];
  if (!formState.configId || !entityName) return;
  entityColumns.value =
    (await listCodeGenColumnsApi(entityName, formState.configId)) ?? [];
}

async function handleDatabaseChange() {
  formState.tableName = undefined;
  const db = databases.value.find(
    (item) => item.configId === formState.configId,
  );
  formState.dbType = db ? String(db.dbType) : undefined;
  formState.connectionString = db?.connectionString;
  await loadEditorTables(formState.configId);
}

async function handleTableChange(entityName: string) {
  const item = tables.value.find((table) => table.entityName === entityName);
  formState.busName = item?.tableComment || item?.entityName || '';
  formState.tableUniqueList = [];
  await loadEntityColumns(entityName);
}

async function handleTableSelection(value: unknown) {
  if (typeof value === 'string') await handleTableChange(value);
}

async function openEditor(record?: SysCodeGenRecord, copy = false) {
  clearForm();
  const detail = record ? await getCodeGenDetailApi(record.id) : undefined;
  Object.assign(
    formState,
    detail
      ? {
          ...detail,
          generateMenu: false,
          id: copy ? undefined : detail.id,
          tableName: copy ? undefined : detail.tableName,
          tableUniqueList: copy ? [] : detail.tableUniqueList || [],
        }
      : {
          authorName: 'Admin.NET',
          generateMenu: false,
          generateType: '121',
          nameSpace: namespaces.value[0],
          pagePath: 'develop',
          printType: 'off',
          tableUniqueList: [],
        },
  );
  await loadEditorTables(formState.configId, formState.tableName);
  editorOpen.value = true;
  requestAnimationFrame(() => formRef.value?.clearValidate());
}

function addUniqueConstraint() {
  formState.tableUniqueList ??= [];
  if (formState.tableUniqueList.length >= 8) {
    message.warning('最多配置 8 组唯一约束');
    return;
  }
  formState.tableUniqueList.push({ columns: [], message: '' });
}

function handleUniqueColumnsChange(item: TableUniqueConfigItem) {
  if (item.columns.length === 1 && !item.message) {
    item.message =
      entityColumns.value.find(
        (column) => column.propertyName === item.columns[0],
      )?.columnComment || '';
  }
}

async function saveCodeGen() {
  await formRef.value?.validate();
  const authorName = formState.authorName?.trim();
  const busName = formState.busName?.trim();
  const pagePath = formState.pagePath?.trim();
  if (!authorName || !busName || !pagePath) {
    message.warning('请补全作者、业务名称和页面目录');
    return;
  }
  if (!identifierPattern.test(formState.tableName || '')) {
    message.warning('实体名称格式无效');
    return;
  }
  if (!pagePathPattern.test(formState.pagePath || '')) {
    message.warning('页面目录只能使用字母、数字、斜杠、下划线和短横线');
    return;
  }
  const uniqueList = formState.tableUniqueList || [];
  if (
    uniqueList.some((item) => item.columns.length === 0 || !item.message.trim())
  ) {
    message.warning('请补全唯一约束的字段和提示文字');
    return;
  }

  submitLoading.value = true;
  try {
    const payload = {
      ...formState,
      authorName,
      busName,
      connectionString: undefined,
      generateMenu: false,
      pagePath,
      tableUniqueList: uniqueList,
    } as SaveCodeGenParams & { id?: number };
    await (payload.id
      ? updateCodeGenApi({ ...payload, id: payload.id })
      : addCodeGenApi(payload));
    message.success(payload.id ? '配置更新成功' : '配置创建成功');
    editorOpen.value = false;
    await loadRecords();
  } finally {
    submitLoading.value = false;
  }
}

function removeRecord(record: SysCodeGenRecord) {
  Modal.confirm({
    cancelText: '取消',
    centered: true,
    content: `将删除 ${record.tableName} 的代码生成配置和字段配置，不会删除数据表和已有源码。`,
    okButtonProps: { danger: true },
    okText: '确认删除配置',
    title: '删除代码生成配置？',
    async onOk() {
      await deleteCodeGenApi([record.id]);
      message.success('配置已删除');
      await loadRecords();
    },
  });
}

async function openFields(record: SysCodeGenRecord) {
  currentRecord.value = record;
  fieldsOpen.value = true;
  fieldsLoading.value = true;
  try {
    fieldConfigs.value = await listCodeGenFieldConfigsApi(record.id);
  } finally {
    fieldsLoading.value = false;
  }
}

async function saveFieldConfigs() {
  submitLoading.value = true;
  try {
    await updateCodeGenFieldConfigsApi(fieldConfigs.value);
    message.success('字段配置已保存');
    fieldsOpen.value = false;
  } finally {
    submitLoading.value = false;
  }
}

function isLockedField(record: CodeGenFieldConfig) {
  return record.whetherCommon === 'Y' || record.columnKey === 'True';
}

function changeEffectType(record: CodeGenFieldConfig, index: number) {
  if (
    record.effectType === 'ForeignKey' ||
    record.effectType === 'ApiTreeSelector'
  ) {
    openRelation(record, index);
  } else if (
    record.effectType === 'DictSelector' ||
    record.effectType === 'EnumSelector' ||
    record.effectType === 'ConstSelector'
  ) {
    record.dictTypeCode = undefined;
  }
}

async function openRelation(record: CodeGenFieldConfig, index: number) {
  relationIndex.value = index;
  Object.assign(relationForm, structuredClone(record));
  relationTables.value = [];
  relationColumns.value = [];
  relationOpen.value = true;
  if (relationForm.fkConfigId) {
    await loadRelationTables(relationForm.fkConfigId);
    if (relationForm.fkTableName)
      await loadRelationColumns(relationForm.fkTableName);
  }
}

async function loadRelationTables(configId?: string) {
  relationTables.value = [];
  relationColumns.value = [];
  if (!configId) return;
  relationTables.value = await listCodeGenTablesApi(configId);
}

async function loadRelationColumns(tableName?: string) {
  relationColumns.value = [];
  if (!relationForm.fkConfigId || !tableName) return;
  relationColumns.value = await listCodeGenColumnsApi(
    tableName,
    relationForm.fkConfigId,
  );
}

async function handleRelationDatabaseChange() {
  relationForm.fkTableName = undefined;
  relationForm.fkLinkColumnName = undefined;
  relationForm.fkDisplayColumnList = [];
  relationForm.pidColumn = undefined;
  await loadRelationTables(relationForm.fkConfigId);
}

async function handleRelationTableChange(tableName: string) {
  const table = relationTables.value.find(
    (item) => item.tableName === tableName,
  );
  relationForm.fkEntityName = table?.entityName;
  relationForm.fkLinkColumnName = undefined;
  relationForm.fkDisplayColumnList = [];
  relationForm.pidColumn = undefined;
  await loadRelationColumns(tableName);
}

async function handleRelationTableSelection(value: unknown) {
  if (typeof value === 'string') await handleRelationTableChange(value);
}

function saveRelation() {
  if (
    !relationForm.fkConfigId ||
    !relationForm.fkTableName ||
    !relationForm.fkLinkColumnName ||
    !relationForm.fkDisplayColumnList?.length
  ) {
    message.warning('请补全关联数据库、表、关联字段和显示字段');
    return;
  }
  if (
    relationForm.effectType === 'ApiTreeSelector' &&
    !relationForm.pidColumn
  ) {
    message.warning('树选择器还需要选择父级字段');
    return;
  }
  const linkedColumn = relationColumns.value.find(
    (item) => item.columnName === relationForm.fkLinkColumnName,
  );
  relationForm.fkColumnNetType = linkedColumn?.netType;
  const fieldConfig = fieldConfigs.value[relationIndex.value];
  if (!fieldConfig) {
    message.error('字段配置不存在，请刷新后重试');
    return;
  }
  Object.assign(fieldConfig, relationForm);
  relationOpen.value = false;
}

function syncRecord(record: SysCodeGenRecord) {
  Modal.confirm({
    cancelText: '取消',
    centered: true,
    content:
      '后台会重新读取实体和真实表字段，并在同一事务内替换字段配置。你手工调整过的控件、字典和查询设置会被重置。',
    okButtonProps: { danger: true },
    okText: '重新同步字段',
    title: `同步 ${record.tableName}？`,
    async onOk() {
      await syncCodeGenApi(record.id);
      message.success('字段已从实体和数据表重新同步');
    },
  });
}

async function openPreview(record: SysCodeGenRecord) {
  currentRecord.value = record;
  previewFiles.value = {};
  activePreviewFile.value = '';
  previewOpen.value = true;
  previewLoading.value = true;
  try {
    previewFiles.value = await previewCodeGenApi(record.id);
    activePreviewFile.value = Object.keys(previewFiles.value)[0] || '';
  } finally {
    previewLoading.value = false;
  }
}

async function copyPreview() {
  const content = previewFiles.value[activePreviewFile.value] || '';
  await navigator.clipboard.writeText(content);
  message.success('当前文件已复制');
}

function openGeneration(record: SysCodeGenRecord) {
  currentRecord.value = record;
  generationConfirmation.value = '';
  generationOpen.value = true;
}

async function runGeneration() {
  if (
    !currentRecord.value ||
    generationConfirmation.value !== generationExpectedText.value
  )
    return;
  submitLoading.value = true;
  try {
    const result = await runCodeGenApi(currentRecord.value.id);
    if (result?.url) {
      window.open(result.url, '_blank', 'noopener,noreferrer');
      message.success('后端代码压缩包已生成，下载已开始');
    } else {
      message.success('后端代码已写入项目目录，请立即检查 Git 变更并编译');
    }
    generationOpen.value = false;
  } finally {
    submitLoading.value = false;
  }
}

async function loadOptions() {
  if (!isSuperAdmin.value) return;
  const [
    dbList,
    namespaceList,
    generateList,
    printTypeList,
    prints,
    dictList,
    effects,
    queries,
  ] = await Promise.all([
    listCodeGenDatabasesApi(),
    listCodeGenNamespacesApi(),
    getDictDataByCodeApi('code_gen_create_type', 1),
    getDictDataByCodeApi('code_gen_print_type', 1),
    pagePrintsApi({ page: 1, pageSize: 500 }),
    listDictTypesApi(),
    getDictDataByCodeApi('code_gen_effect_type', 1),
    getDictDataByCodeApi('code_gen_query_type', 1),
  ]);
  databases.value = dbList;
  namespaces.value = namespaceList;
  generationTypes.value = generateList.map((item) => ({
    label: item.label,
    value: item.value,
  }));
  printTypes.value = printTypeList.map((item) => ({
    label: item.label,
    value: item.value,
  }));
  printTemplates.value = prints.items ?? [];
  dictTypes.value = dictList;
  effectTypes.value = effects.map((item) => ({
    label: item.label,
    value: item.value,
  }));
  queryTypes.value = queries.map((item) => ({
    label: item.label,
    value: item.value,
  }));
}

async function handleMoreAction(key: string, record: SysCodeGenRecord) {
  switch (key) {
    case 'copy': {
      await openEditor(record, true);
      break;
    }
    case 'delete': {
      removeRecord(record);
      break;
    }
    case 'generate': {
      openGeneration(record);
      break;
    }
    case 'preview': {
      await openPreview(record);
      break;
    }
    case 'sync': {
      syncRecord(record);
      break;
    }
  }
}

onMounted(async () => {
  await Promise.all([loadOptions(), loadRecords()]);
});
</script>

<template>
  <div class="codegen-page">
    <Alert
      v-if="!isSuperAdmin"
      message="仅超级管理员可以使用代码生成"
      description="此模块能读取数据库元数据、下载后端代码或写入后台项目目录，普通管理员不开放。"
      show-icon
      type="warning"
    />

    <section v-else class="codegen-panel">
      <header class="page-heading">
        <div>
          <h2>代码生成</h2>
          <p>
            维护实体生成规则、字段控件和查询配置；预览不会写文件，正式生成需要二次确认
          </p>
        </div>
        <Space>
          <Button @click="guideOpen = true">
            <template #icon><IconifyIcon icon="lucide:circle-help" /></template>
            使用说明
          </Button>
          <Button type="primary" @click="openEditor()">
            <template #icon><IconifyIcon icon="lucide:plus" /></template>
            新增配置
          </Button>
        </Space>
      </header>

      <Alert
        class="migration-alert"
        message="前端模板迁移期间，只开放后端代码生成"
        description="旧模板生成的是 Element Plus 页面，且目标目录指向只读参考项目 Web。新版已在后台阻止前端生成，也不会自动改写菜单；后端 ZIP 和后端本地生成仍可使用。"
        show-icon
        type="info"
      />

      <div class="filter-bar">
        <label
          ><span>实体名称</span
          ><Input
            v-model:value="query.tableName"
            allow-clear
            placeholder="例如 SysUser"
            @press-enter="
              query.page = 1;
              loadRecords();
            "
        /></label>
        <label
          ><span>业务名称</span
          ><Input
            v-model:value="query.busName"
            allow-clear
            placeholder="例如 账号"
            @press-enter="
              query.page = 1;
              loadRecords();
            "
        /></label>
        <Space>
          <Button
            type="primary"
            @click="
              query.page = 1;
              loadRecords();
            "
          >
            <template #icon><IconifyIcon icon="lucide:search" /></template>查询
          </Button>
          <Button
            @click="
              query.tableName = '';
              query.busName = '';
              query.page = 1;
              loadRecords();
            "
          >
            <template #icon><IconifyIcon icon="lucide:rotate-ccw" /></template
            >重置
          </Button>
        </Space>
      </div>

      <Table
        :columns="columns"
        :data-source="records"
        :loading="loading"
        :pagination="{
          ...ADMIN_PAGINATION_PROPS,
          current: query.page,
          pageSize: query.pageSize,
          total: query.total,
          showTotal: (total: number) => `共 ${total} 条`,
        }"
        :scroll="{ x: 1298 }"
        row-key="id"
        size="small"
        @change="
          (pagination) => {
            query.page = pagination.current || 1;
            query.pageSize = pagination.pageSize || 50;
            loadRecords();
          }
        "
      >
        <template #bodyCell="{ column, record, index }">
          <template v-if="column.key === 'index'">
            {{ (query.page - 1) * query.pageSize + index + 1 }}
          </template>
          <template v-else-if="column.key === 'tableName'">
            <span class="entity-name">{{ record.tableName }}</span>
          </template>
          <template v-else-if="column.key === 'configId'">
            <Tag>{{ record.configId }}</Tag>
          </template>
          <template v-else-if="column.key === 'generateType'">
            <Tag
              :color="
                SAFE_GENERATE_TYPES.has(record.generateType || '')
                  ? 'green'
                  : 'orange'
              "
            >
              {{ generateTypeLabel(record.generateType) }}
            </Tag>
          </template>
          <template v-else-if="column.key === 'actions'">
            <Space :size="4">
              <Button
                size="small"
                type="link"
                @click="openEditor(asCodeGen(record))"
              >
                <template #icon><IconifyIcon icon="lucide:pencil" /></template
                >编辑
              </Button>
              <Button
                size="small"
                type="link"
                @click="openFields(asCodeGen(record))"
              >
                <template #icon>
                  <IconifyIcon icon="lucide:sliders-horizontal" /> </template
                >字段配置
              </Button>
              <Dropdown trigger="click">
                <Button size="small" type="text">
                  <IconifyIcon icon="lucide:ellipsis" />
                </Button>
                <template #overlay>
                  <Menu
                    @click="
                      ({ key }) =>
                        handleMoreAction(String(key), asCodeGen(record))
                    "
                  >
                    <Menu.Item key="preview">
                      <IconifyIcon icon="lucide:eye" /> 预览代码
                    </Menu.Item>
                    <Menu.Item key="copy">
                      <IconifyIcon icon="lucide:copy" /> 复制配置
                    </Menu.Item>
                    <Menu.Item key="sync">
                      <IconifyIcon icon="lucide:refresh-cw" />
                      同步字段
                    </Menu.Item>
                    <Menu.Item key="generate">
                      <IconifyIcon icon="lucide:package" /> 生成代码
                    </Menu.Item>
                    <Menu.Divider />
                    <Menu.Item key="delete" danger>
                      <IconifyIcon icon="lucide:trash-2" /> 删除配置
                    </Menu.Item>
                  </Menu>
                </template>
              </Dropdown>
            </Space>
          </template>
        </template>
      </Table>
    </section>

    <Modal
      v-model:open="guideOpen"
      :footer="null"
      title="代码生成怎么用"
      width="760px"
    >
      <div class="guide-list">
        <article>
          <b>1. 新增配置</b>
          <p>
            选择服务端已配置的数据库和实体，设置业务名称、后端项目、页面目录与生成方式。连接串只显示“服务端托管”，不会下发真实密码。
          </p>
        </article>
        <article>
          <b>2. 字段配置</b>
          <p>
            决定每个实体属性在列表、表单、导入和查询中的行为。外键与树选择器还需要配置关联表、关联字段和显示字段。
          </p>
        </article>
        <article>
          <b>3. 同步字段</b>
          <p>
            实体或数据表字段变化后使用。同步会重建字段配置，因此原有控件和字典设置会被重置；后台使用事务，不会出现只删一半的状态。
          </p>
        </article>
        <article>
          <b>4. 预览与生成</b>
          <p>
            预览只在内存中渲染。后端 ZIP
            会下载压缩包；后端本地会写入项目目录且拒绝覆盖同名源码。生成后必须检查
            Git 变更并重新编译。
          </p>
        </article>
        <Alert
          message="自动化验收只读取列表、详情和预览，永远不会自动点击同步、删除或正式生成。"
          show-icon
          type="warning"
        />
      </div>
    </Modal>

    <Modal
      v-model:open="editorOpen"
      :confirm-loading="submitLoading"
      :title="editorTitle"
      width="920px"
      @ok="saveCodeGen"
    >
      <Form ref="formRef" :model="formState" :rules="rules" layout="vertical">
        <div class="form-grid">
          <Form.Item label="数据库" name="configId">
            <Select
              v-model:value="formState.configId"
              :options="
                databases.map((item) => ({
                  label: `${item.configId} · ${dbTypeLabel(item.dbType)}`,
                  value: item.configId,
                }))
              "
              placeholder="选择服务端数据库配置"
              @change="handleDatabaseChange"
            />
          </Form.Item>
          <Form.Item label="连接信息">
            <Input
              :value="
                selectedDatabase?.connectionString || formState.connectionString
              "
              disabled
            />
          </Form.Item>
          <Form.Item label="生成实体" name="tableName">
            <Select
              v-model:value="formState.tableName"
              :filter-option="
                (input, option) =>
                  String(option?.label || '')
                    .toLowerCase()
                    .includes(input.toLowerCase())
              "
              :options="tableOptions"
              placeholder="输入实体或表名查找"
              show-search
              @change="handleTableSelection"
            />
          </Form.Item>
          <Form.Item label="业务名称" name="busName">
            <Input
              v-model:value="formState.busName"
              :maxlength="128"
              placeholder="页面和注释使用的中文名称"
            />
          </Form.Item>
          <Form.Item label="后端项目" name="nameSpace">
            <Select
              v-model:value="formState.nameSpace"
              :options="namespaces.map((value) => ({ label: value, value }))"
            />
          </Form.Item>
          <Form.Item label="页面目录" name="pagePath">
            <Input
              v-model:value="formState.pagePath"
              :maxlength="32"
              placeholder="例如 develop"
            />
          </Form.Item>
          <Form.Item label="作者" name="authorName">
            <Input v-model:value="formState.authorName" :maxlength="32" />
          </Form.Item>
          <Form.Item label="生成方式" name="generateType">
            <Select v-model:value="formState.generateType">
              <Select.Option
                v-for="item in generationTypes"
                :key="item.value"
                :disabled="FRONTEND_GENERATE_TYPES.has(item.value)"
                :value="item.value"
              >
                {{ item.label
                }}<span v-if="FRONTEND_GENERATE_TYPES.has(item.value)"
                  >（前端模板迁移中）</span
                >
              </Select.Option>
            </Select>
          </Form.Item>
          <Form.Item label="打印支持">
            <Select
              v-model:value="formState.printType"
              :options="printTypes"
              allow-clear
            />
          </Form.Item>
          <Form.Item label="打印模板">
            <Select
              v-model:value="formState.printName"
              :disabled="formState.printType !== 'custom'"
              :options="
                printTemplates.map((item) => ({
                  label: item.name,
                  value: item.name,
                }))
              "
              allow-clear
            />
          </Form.Item>
        </div>
        <div class="menu-safety-row">
          <div>
            <b>自动生成菜单</b
            ><span>迁移期固定关闭，避免旧模板路由写入 Vben 菜单树</span>
          </div>
          <Switch :checked="false" disabled />
        </div>
        <div class="unique-heading">
          <div>
            <b>唯一约束提示</b><span>用于生成新增、编辑时的重复数据校验</span>
          </div>
          <Button size="small" @click="addUniqueConstraint">
            <template #icon><IconifyIcon icon="lucide:plus" /></template
            >添加约束
          </Button>
        </div>
        <div v-if="formState.tableUniqueList?.length" class="unique-list">
          <div
            v-for="(item, index) in formState.tableUniqueList"
            :key="index"
            class="unique-row"
          >
            <Select
              v-model:value="item.columns"
              :options="fieldOptions"
              mode="multiple"
              placeholder="选择一到多个实体属性"
              @change="handleUniqueColumnsChange(item)"
            />
            <Input
              v-model:value="item.message"
              :maxlength="128"
              placeholder="重复时显示的中文提示"
            />
            <Tooltip title="移除这组约束">
              <Button
                danger
                type="text"
                @click="formState.tableUniqueList?.splice(index, 1)"
              >
                <IconifyIcon icon="lucide:trash-2" />
              </Button>
            </Tooltip>
          </div>
        </div>
      </Form>
    </Modal>

    <Modal
      v-model:open="fieldsOpen"
      :confirm-loading="submitLoading"
      :title="`字段配置：${currentRecord?.tableName || ''}`"
      width="1180px"
      @ok="saveFieldConfigs"
    >
      <Alert
        class="field-alert"
        message="常用字段和主键不允许配置为增改或必填；横向滚动只用于字段设置区，属性名和关联操作始终固定。"
        show-icon
        type="info"
      />
      <Table
        :columns="fieldColumns"
        :data-source="fieldConfigs"
        :loading="fieldsLoading"
        :pagination="false"
        :scroll="{ x: 1750, y: 500 }"
        row-key="id"
        size="small"
      >
        <template #bodyCell="{ column, record, index }">
          <template v-if="column.key === 'index'">{{ index + 1 }}</template>
          <template v-else-if="column.key === 'propertyName'">
            <span class="entity-name">{{ record.propertyName }}</span>
          </template>
          <template v-else-if="column.key === 'columnComment'">
            <Input
              v-model:value="record.columnComment"
              :maxlength="128"
              size="small"
            />
          </template>
          <template v-else-if="column.key === 'effectType'">
            <Select
              v-model:value="record.effectType"
              :disabled="isLockedField(asFieldConfig(record))"
              :options="effectTypes"
              size="small"
              @change="changeEffectType(asFieldConfig(record), index)"
            />
          </template>
          <template v-else-if="column.key === 'dictTypeCode'">
            <Select
              v-if="
                record.effectType === 'DictSelector' ||
                record.effectType === 'EnumSelector'
              "
              v-model:value="record.dictTypeCode"
              :options="
                dictTypes
                  .filter((item) =>
                    record.effectType === 'EnumSelector'
                      ? item.code.endsWith('Enum')
                      : !item.code.endsWith('Enum'),
                  )
                  .map((item) => ({
                    label: `${item.name} · ${item.code}`,
                    value: item.code,
                  }))
              "
              allow-clear
              show-search
              size="small"
            />
            <Input
              v-else-if="record.effectType === 'ConstSelector'"
              v-model:value="record.dictTypeCode"
              placeholder="常量类型名称"
              size="small"
            />
            <span v-else class="muted">-</span>
          </template>
          <template
            v-else-if="
              [
                'whetherTable',
                'whetherAddUpdate',
                'whetherImport',
                'whetherRequired',
                'whetherSortable',
                'whetherQuery',
              ].includes(String(column.key))
            "
          >
            <Checkbox
              :checked="isYes(record, String(column.key))"
              :disabled="
                [
                  'whetherAddUpdate',
                  'whetherImport',
                  'whetherRequired',
                ].includes(String(column.key)) &&
                isLockedField(asFieldConfig(record))
              "
              @change="
                setYesNo(record, String(column.key), $event.target.checked)
              "
            />
          </template>
          <template v-else-if="column.key === 'queryType'">
            <Select
              v-model:value="record.queryType"
              :disabled="record.whetherQuery !== 'Y'"
              :options="queryTypes"
              size="small"
            />
          </template>
          <template v-else-if="column.key === 'orderNo'">
            <InputNumber
              v-model:value="record.orderNo"
              :max="100000"
              :min="0"
              size="small"
            />
          </template>
          <template v-else-if="column.key === 'relation'">
            <Button
              v-if="
                record.effectType === 'ForeignKey' ||
                record.effectType === 'ApiTreeSelector'
              "
              size="small"
              type="link"
              @click="openRelation(asFieldConfig(record), index)"
            >
              设置
            </Button>
            <span v-else class="muted">-</span>
          </template>
        </template>
      </Table>
    </Modal>

    <Modal
      v-model:open="relationOpen"
      title="关联字段设置"
      width="680px"
      @ok="saveRelation"
    >
      <Form layout="vertical">
        <div class="form-grid">
          <Form.Item label="关联数据库" required>
            <Select
              v-model:value="relationForm.fkConfigId"
              :options="
                databases.map((item) => ({
                  label: item.configId,
                  value: item.configId,
                }))
              "
              @change="handleRelationDatabaseChange"
            />
          </Form.Item>
          <Form.Item label="关联数据表" required>
            <Select
              v-model:value="relationForm.fkTableName"
              :options="
                relationTables.map((item) => ({
                  label: `${item.tableName}（${item.tableComment || item.entityName}）`,
                  value: item.tableName,
                }))
              "
              show-search
              @change="handleRelationTableSelection"
            />
          </Form.Item>
          <Form.Item label="关联字段" required>
            <Select
              v-model:value="relationForm.fkLinkColumnName"
              :options="relationColumnOptions"
              show-search
            />
          </Form.Item>
          <Form.Item label="显示字段" required>
            <Select
              v-model:value="relationForm.fkDisplayColumnList"
              :options="relationColumnOptions"
              :max-tag-count="2"
              mode="multiple"
              show-search
            />
          </Form.Item>
          <Form.Item
            v-if="relationForm.effectType === 'ApiTreeSelector'"
            label="父级字段"
            required
          >
            <Select
              v-model:value="relationForm.pidColumn"
              :options="relationColumnOptions"
              show-search
            />
          </Form.Item>
        </div>
      </Form>
    </Modal>

    <Modal
      v-model:open="previewOpen"
      :footer="null"
      title="代码预览"
      width="1100px"
    >
      <div v-if="previewLoading" class="preview-loading">
        正在根据当前配置渲染代码…
      </div>
      <template v-else>
        <div class="preview-toolbar">
          <Tag>{{ currentRecord?.tableName }}</Tag
          ><Button size="small" @click="copyPreview">
            <template #icon><IconifyIcon icon="lucide:copy" /></template
            >复制当前文件
          </Button>
        </div>
        <Tabs v-model:active-key="activePreviewFile" size="small">
          <Tabs.TabPane v-for="name in previewNames" :key="name" :tab="name" />
        </Tabs>
        <pre
          class="code-preview"
        ><code>{{ previewFiles[activePreviewFile] || '没有可预览的模板内容' }}</code></pre>
      </template>
    </Modal>

    <Modal
      v-model:open="generationOpen"
      :confirm-loading="submitLoading"
      :ok-button-props="{
        danger: currentRecord?.generateType === '221',
        disabled:
          !canRunGeneration ||
          generationConfirmation !== generationExpectedText,
      }"
      ok-text="确认生成"
      title="正式生成代码"
      width="600px"
      @ok="runGeneration"
    >
      <Alert
        v-if="!canRunGeneration"
        message="当前生成方式包含旧版前端模板，已被安全策略阻止"
        description="请先编辑配置，将生成方式改为“下载压缩包（后端）”或“生成到本项目（后端）”。"
        show-icon
        type="warning"
      />
      <Alert
        v-else
        :message="
          currentRecord?.generateType === '221'
            ? '代码将写入后台项目目录'
            : '将生成只包含后台代码的 ZIP 压缩包'
        "
        :description="
          currentRecord?.generateType === '221'
            ? '后台会拒绝覆盖已经存在的同名文件。完成后请立即检查 Git 变更并编译，自动菜单生成已关闭。'
            : '压缩包只写入服务端临时下载目录，不修改源码和菜单。'
        "
        show-icon
        :type="currentRecord?.generateType === '221' ? 'error' : 'info'"
      />
      <label class="confirmation-field">
        <span
          >输入 <b>{{ generationExpectedText }}</b> 确认</span
        >
        <Input
          v-model:value="generationConfirmation"
          :disabled="!canRunGeneration"
          autocomplete="off"
        />
      </label>
    </Modal>
  </div>
</template>

<style scoped>
.codegen-page {
  min-height: 100%;
  padding: 12px;
}

.codegen-panel {
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

.page-heading h2 {
  margin: 0;
  font-size: 18px;
  font-weight: 650;
  color: #111827;
}

.page-heading p {
  margin: 4px 0 0;
  font-size: 13px;
  color: #6b7280;
}

.migration-alert {
  margin: 0 18px 12px;
}

.filter-bar {
  display: flex;
  gap: 12px;
  align-items: flex-end;
  padding: 12px 18px;
  background: #fafbfc;
  border-block: 1px solid #eef0f3;
}

.filter-bar label {
  display: grid;
  gap: 6px;
  width: 240px;
}

.filter-bar label > span {
  font-size: 12px;
  font-weight: 600;
  color: #4b5563;
}

.entity-name {
  font-family: Consolas, monospace;
  font-weight: 600;
  color: #1d4ed8;
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

.form-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0 16px;
}

.menu-safety-row,
.unique-heading {
  display: flex;
  gap: 16px;
  align-items: center;
  justify-content: space-between;
  padding: 11px 13px;
  margin-bottom: 14px;
  background: #fafafa;
  border: 1px solid #e5e7eb;
  border-radius: 6px;
}

.menu-safety-row > div,
.unique-heading > div {
  display: grid;
}

.menu-safety-row span,
.unique-heading span {
  font-size: 12px;
  color: #7b8494;
}

.unique-heading {
  padding-inline: 0;
  margin-bottom: 8px;
  background: transparent;
  border: 0;
  border-radius: 0;
}

.unique-list {
  display: grid;
  gap: 8px;
}

.unique-row {
  display: grid;
  grid-template-columns: 1.2fr 1fr 36px;
  gap: 8px;
  align-items: center;
}

.field-alert {
  margin-bottom: 10px;
}

.muted {
  color: #9ca3af;
}

.preview-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.preview-loading {
  display: grid;
  place-items: center;
  min-height: 320px;
  color: #6b7280;
}

.code-preview {
  max-height: 62vh;
  padding: 16px;
  margin: 0;
  overflow: auto;
  font:
    12px/1.65 Consolas,
    monospace;
  color: #dbeafe;
  white-space: pre;
  background: #111827;
  border: 1px solid #263244;
  border-radius: 6px;
}

.confirmation-field {
  display: grid;
  gap: 8px;
  margin-top: 16px;
}

.confirmation-field span {
  color: #4b5563;
}

@media (max-width: 800px) {
  .codegen-page {
    padding: 6px;
  }

  .page-heading {
    flex-direction: column;
  }

  .filter-bar {
    flex-wrap: wrap;
    align-items: stretch;
  }

  .filter-bar label {
    flex: 1 1 220px;
    width: auto;
  }

  .form-grid,
  .unique-row {
    grid-template-columns: 1fr;
  }
}
</style>
