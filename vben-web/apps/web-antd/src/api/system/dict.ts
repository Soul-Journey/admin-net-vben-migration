import type { AdminNetPagedList } from './user';

import { requestClient } from '#/api/request';

type RawRecord = Record<string, unknown>;

export interface PageDictTypeParams {
  code?: string;
  name?: string;
  page: number;
  pageSize: number;
}

export interface PageDictDataParams {
  dictTypeId: number;
  label?: string;
  page: number;
  pageSize: number;
}

export interface SysDictTypeRecord {
  code: string;
  createTime?: string;
  createUserName?: string;
  id: number;
  name: string;
  orderNo?: number;
  remark?: string;
  status?: number;
  sysFlag?: number;
  updateTime?: string;
  updateUserName?: string;
}

export interface SysDictDataRecord {
  classSetting?: string;
  code?: string;
  createTime?: string;
  createUserName?: string;
  dictType?: SysDictTypeRecord;
  dictTypeId?: number;
  extData?: string;
  id: number;
  label: string;
  orderNo?: number;
  remark?: string;
  status?: number;
  styleSetting?: string;
  tagType?: string;
  updateTime?: string;
  updateUserName?: string;
  value: string;
}

export type SaveDictTypeParams = Partial<SysDictTypeRecord> & {
  code: string;
  name: string;
  orderNo: number;
  status: number;
  sysFlag: number;
};

export type SaveDictDataParams = Partial<SysDictDataRecord> & {
  dictTypeId: number;
  label: string;
  orderNo: number;
  status: number;
  value: string;
};

function toRecord(value: unknown): RawRecord {
  return value && typeof value === 'object' ? (value as RawRecord) : {};
}

function toNumber(value: unknown) {
  if (typeof value === 'number') {
    return value;
  }
  if (typeof value === 'string' && value.trim()) {
    const numberValue = Number(value);
    return Number.isNaN(numberValue) ? undefined : numberValue;
  }
  return undefined;
}

function toStringValue(value: unknown) {
  return typeof value === 'string' ? value : undefined;
}

function normalizeDictType(item: unknown): SysDictTypeRecord {
  const record = toRecord(item);
  return {
    code: toStringValue(record.code ?? record.Code) ?? '',
    createTime: toStringValue(record.createTime ?? record.CreateTime),
    createUserName: toStringValue(record.createUserName ?? record.CreateUserName),
    id: toNumber(record.id ?? record.Id) ?? 0,
    name: toStringValue(record.name ?? record.Name) ?? '',
    orderNo: toNumber(record.orderNo ?? record.OrderNo),
    remark: toStringValue(record.remark ?? record.Remark),
    status: toNumber(record.status ?? record.Status),
    sysFlag: toNumber(record.sysFlag ?? record.SysFlag),
    updateTime: toStringValue(record.updateTime ?? record.UpdateTime),
    updateUserName: toStringValue(record.updateUserName ?? record.UpdateUserName),
  };
}

function normalizeDictData(item: unknown): SysDictDataRecord {
  const record = toRecord(item);
  return {
    classSetting: toStringValue(record.classSetting ?? record.ClassSetting),
    code: toStringValue(record.code ?? record.Code),
    createTime: toStringValue(record.createTime ?? record.CreateTime),
    createUserName: toStringValue(record.createUserName ?? record.CreateUserName),
    dictTypeId: toNumber(record.dictTypeId ?? record.DictTypeId),
    extData: toStringValue(record.extData ?? record.ExtData),
    id: toNumber(record.id ?? record.Id) ?? 0,
    label: toStringValue(record.label ?? record.Label) ?? '',
    orderNo: toNumber(record.orderNo ?? record.OrderNo),
    remark: toStringValue(record.remark ?? record.Remark),
    status: toNumber(record.status ?? record.Status),
    styleSetting: toStringValue(record.styleSetting ?? record.StyleSetting),
    tagType: toStringValue(record.tagType ?? record.TagType),
    updateTime: toStringValue(record.updateTime ?? record.UpdateTime),
    updateUserName: toStringValue(record.updateUserName ?? record.UpdateUserName),
    value: toStringValue(record.value ?? record.Value) ?? '',
  };
}

function normalizePagedList<T>(
  data: AdminNetPagedList<unknown>,
  normalize: (item: unknown) => T,
): AdminNetPagedList<T> {
  return {
    ...data,
    items: Array.isArray(data.items)
      ? data.items.map((item) => normalize(item))
      : [],
  };
}

export async function pageDictTypesApi(params: PageDictTypeParams) {
  const data = await requestClient.post<AdminNetPagedList<unknown>>(
    '/sysDictType/page',
    params,
  );
  return normalizePagedList(data, normalizeDictType);
}

export function addDictTypeApi(params: SaveDictTypeParams) {
  return requestClient.post<unknown>('/sysDictType/add', params);
}

export function updateDictTypeApi(params: SaveDictTypeParams & { id: number }) {
  return requestClient.post<unknown>('/sysDictType/update', params);
}

export function deleteDictTypeApi(id: number) {
  return requestClient.post<unknown>('/sysDictType/delete', { id });
}

export async function pageDictDataApi(params: PageDictDataParams) {
  const data = await requestClient.post<AdminNetPagedList<unknown>>(
    '/sysDictData/page',
    params,
  );
  return normalizePagedList(data, normalizeDictData);
}

export function addDictDataApi(params: SaveDictDataParams) {
  return requestClient.post<unknown>('/sysDictData/add', params);
}

export function updateDictDataApi(params: SaveDictDataParams & { id: number }) {
  return requestClient.post<unknown>('/sysDictData/update', params);
}

export function deleteDictDataApi(id: number) {
  return requestClient.post<unknown>('/sysDictData/delete', { id });
}

export async function getDictDataByCodeApi(code: string, status?: number) {
  const data = await requestClient.get<unknown[]>('/sysDictData/dataList', {
    params: {
      Code: code,
      Status: status,
    },
  });
  return Array.isArray(data) ? data.map((item) => normalizeDictData(item)) : [];
}
