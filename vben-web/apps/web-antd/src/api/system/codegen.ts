import type { AdminNetPagedList } from './user';

import { requestClient } from '#/api/request';

export interface CodeGenDatabaseRecord {
  configId: string;
  connectionString?: string;
  dbType: number | string;
}

export interface CodeGenTableRecord {
  configId?: string;
  entityName: string;
  tableComment?: string;
  tableName: string;
}

export interface CodeGenColumnRecord {
  columnComment?: string;
  columnKey?: string;
  columnLength?: number;
  columnName: string;
  dataType?: string;
  isNullable?: boolean;
  isPrimarykey?: boolean;
  netType?: string;
  propertyName?: string;
}

export interface TableUniqueConfigItem {
  columns: string[];
  message: string;
}

export interface SysCodeGenRecord {
  authorName?: string;
  busName?: string;
  configId?: string;
  connectionString?: string;
  createTime?: string;
  dbType?: string;
  generateMenu?: boolean;
  generateType?: string;
  id: number;
  menuIcon?: string;
  menuPid?: number;
  nameSpace?: string;
  pagePath?: string;
  printName?: string;
  printType?: string;
  tableName?: string;
  tablePrefix?: string;
  tableUniqueList?: TableUniqueConfigItem[];
}

export interface SaveCodeGenParams extends Omit<SysCodeGenRecord, 'id'> {
  authorName: string;
  busName: string;
  configId: string;
  generateMenu: boolean;
  generateType: string;
  nameSpace: string;
  pagePath: string;
  tableName: string;
  tableUniqueList: TableUniqueConfigItem[];
}

export interface CodeGenFieldConfig {
  codeGenId: number;
  columnComment?: string;
  columnKey?: string;
  columnLength?: number;
  columnName: string;
  dataType?: string;
  dictTypeCode?: string;
  effectType?: string;
  fkColumnNetType?: string;
  fkConfigId?: string;
  fkDisplayColumnList?: string[];
  fkEntityName?: string;
  fkLinkColumnName?: string;
  fkTableName?: string;
  id: number;
  netType?: string;
  orderNo?: number;
  pidColumn?: string;
  propertyName: string;
  queryType?: string;
  whetherAddUpdate?: string;
  whetherCommon?: string;
  whetherImport?: string;
  whetherQuery?: string;
  whetherRequired?: string;
  whetherRetract?: string;
  whetherSortable?: string;
  whetherTable?: string;
}

export function pageCodeGenApi(params: {
  busName?: string;
  page: number;
  pageSize: number;
  tableName?: string;
}) {
  return requestClient.post<AdminNetPagedList<SysCodeGenRecord>>(
    '/sysCodeGen/page',
    params,
  );
}

export function getCodeGenDetailApi(id: number) {
  return requestClient.get<SysCodeGenRecord>('/sysCodeGen/detail', {
    params: { id },
  });
}

export function addCodeGenApi(params: SaveCodeGenParams) {
  return requestClient.post('/sysCodeGen/add', params);
}

export function updateCodeGenApi(params: SaveCodeGenParams & { id: number }) {
  return requestClient.post('/sysCodeGen/update', params);
}

export function deleteCodeGenApi(ids: number[]) {
  return requestClient.post(
    '/sysCodeGen/delete',
    ids.map((id) => ({ id })),
  );
}

export function listCodeGenDatabasesApi() {
  return requestClient.get<CodeGenDatabaseRecord[]>('/sysCodeGen/databaseList');
}

export function listCodeGenTablesApi(configId: string) {
  return requestClient.get<CodeGenTableRecord[]>(
    `/sysCodeGen/tableList/${encodeURIComponent(configId)}`,
  );
}

export function listCodeGenColumnsApi(tableName: string, configId: string) {
  return requestClient.get<CodeGenColumnRecord[]>(
    `/sysCodeGen/columnListByTableName/${encodeURIComponent(tableName)}/${encodeURIComponent(configId)}`,
  );
}

export function listCodeGenNamespacesApi() {
  return requestClient.get<string[]>('/sysCodeGen/applicationNamespaces');
}

export function listCodeGenFieldConfigsApi(codeGenId: number) {
  return requestClient.get<CodeGenFieldConfig[]>('/sysCodeGenConfig/list', {
    params: { codeGenId },
  });
}

export function updateCodeGenFieldConfigsApi(params: CodeGenFieldConfig[]) {
  return requestClient.post('/sysCodeGenConfig/update', params);
}

export function syncCodeGenApi(id: number) {
  return requestClient.post('/sysCodeGen/sync', { id });
}

export function previewCodeGenApi(id: number) {
  return requestClient.post<Record<string, string>>('/sysCodeGen/preview', {
    id,
  });
}

export function runCodeGenApi(id: number) {
  return requestClient.post<{ url?: string }>('/sysCodeGen/runLocal', { id });
}
