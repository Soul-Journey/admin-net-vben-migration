import { requestClient } from '#/api/request';

export interface DatabaseTableRecord {
  description?: string;
  name: string;
}

export interface DatabaseColumnRecord {
  columnDescription?: string;
  dataType: string;
  dbColumnName: string;
  decimalDigits: number;
  defaultValue?: string;
  isIdentity: boolean;
  isNullable: boolean;
  isPrimarykey: boolean;
  length: number;
  tableName: string;
}

export interface SaveDatabaseColumnParams {
  columnDescription?: string;
  configId?: string;
  dataType: string;
  dbColumnName: string;
  decimalDigits: number;
  isIdentity: number;
  isNullable: number;
  isPrimarykey: number;
  length: number;
  tableName?: string;
}

export interface AddDatabaseTableParams {
  configId: string;
  dbColumnInfoList: SaveDatabaseColumnParams[];
  description?: string;
  tableName: string;
}

export function listDatabasesApi() {
  return requestClient.get<string[]>('/sysDatabase/list');
}

export function listDatabaseTablesApi(configId: string) {
  return requestClient.get<DatabaseTableRecord[]>(
    `/sysDatabase/tableList/${encodeURIComponent(configId)}`,
  );
}

export function listDatabaseColumnsApi(tableName: string, configId: string) {
  return requestClient.get<DatabaseColumnRecord[]>(
    `/sysDatabase/columnList/${encodeURIComponent(tableName)}/${encodeURIComponent(configId)}`,
  );
}

export function listDatabaseTypesApi(configId: string) {
  return requestClient.get<string[]>(
    `/sysDatabase/dbTypeList/${encodeURIComponent(configId)}`,
  );
}

export function addDatabaseTableApi(params: AddDatabaseTableParams) {
  return requestClient.post('/sysDatabase/addTable', params);
}

export function updateDatabaseTableApi(params: {
  configId: string;
  description?: string;
  oldTableName: string;
  tableName: string;
}) {
  return requestClient.post('/sysDatabase/updateTable', params);
}

export function addDatabaseColumnApi(params: SaveDatabaseColumnParams) {
  return requestClient.post('/sysDatabase/addColumn', params);
}

export function updateDatabaseColumnApi(params: {
  columnName: string;
  configId: string;
  description?: string;
  oldColumnName: string;
  tableName: string;
}) {
  return requestClient.post('/sysDatabase/updateColumn', params);
}

export function deleteDatabaseColumnApi(params: {
  configId: string;
  dbColumnName: string;
  tableName: string;
}) {
  return requestClient.post('/sysDatabase/deleteColumn', params);
}

export function listBackendNamespacesApi() {
  return requestClient.get<string[]>('/sysCodeGen/applicationNamespaces');
}

export function listEntityBaseClassesApi() {
  return requestClient.get<Array<{ label: string; value: string }>>(
    '/sysDictData/dataList/code_gen_base_class',
  );
}

export function createDatabaseEntityApi(params: {
  baseClassName?: string;
  configId: string;
  entityName?: string;
  position: string;
  tableName: string;
}) {
  return requestClient.post('/sysDatabase/createEntity', params);
}

export function createDatabaseSeedApi(params: {
  configId: string;
  filterExistingData: boolean;
  position: string;
  suffix?: string;
  tableName: string;
}) {
  return requestClient.post('/sysDatabase/createSeedData', params);
}
