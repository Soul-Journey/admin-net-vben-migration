import type { AdminNetPagedList } from './user';

import { requestClient } from '#/api/request';

type RawRecord = Record<string, unknown>;

export interface PluginQuery {
  name?: string;
  page: number;
  pageSize: number;
  tenantId?: number;
}

export interface SysPluginRecord {
  assemblyName?: string;
  createTime?: string;
  createUserName?: string;
  csharpCode: string;
  id: number;
  name: string;
  orderNo: number;
  remark?: string;
  status: number;
  tenantId?: number;
  updateTime?: string;
  updateUserName?: string;
}

export type SavePluginParams = Omit<
  SysPluginRecord,
  'createTime' | 'createUserName' | 'id' | 'updateTime' | 'updateUserName'
> & {
  id?: number;
};

function recordOf(value: unknown): RawRecord {
  return value && typeof value === 'object' ? (value as RawRecord) : {};
}

function numberOf(value: unknown, fallback = 0) {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : fallback;
}

function textOf(value: unknown) {
  return value === undefined || value === null ? undefined : String(value);
}

function normalizePlugin(value: unknown): SysPluginRecord {
  const item = recordOf(value);
  return {
    assemblyName: textOf(item.assemblyName ?? item.AssemblyName),
    createTime: textOf(item.createTime ?? item.CreateTime),
    createUserName: textOf(item.createUserName ?? item.CreateUserName),
    csharpCode: textOf(item.csharpCode ?? item.CsharpCode) ?? '',
    id: numberOf(item.id ?? item.Id),
    name: textOf(item.name ?? item.Name) ?? '',
    orderNo: numberOf(item.orderNo ?? item.OrderNo, 100),
    remark: textOf(item.remark ?? item.Remark),
    status: numberOf(item.status ?? item.Status, 1),
    tenantId: numberOf(item.tenantId ?? item.TenantId) || undefined,
    updateTime: textOf(item.updateTime ?? item.UpdateTime),
    updateUserName: textOf(item.updateUserName ?? item.UpdateUserName),
  };
}

export async function pagePluginsApi(params: PluginQuery) {
  const data = await requestClient.post<AdminNetPagedList<unknown>>(
    '/sysPlugin/page',
    params,
  );
  return {
    ...data,
    items: Array.isArray(data.items)
      ? data.items.map((item) => normalizePlugin(item))
      : [],
  } as AdminNetPagedList<SysPluginRecord>;
}

export function addPluginApi(params: SavePluginParams) {
  return requestClient.post<unknown>('/sysPlugin/add', params);
}

export function updatePluginApi(params: SavePluginParams & { id: number }) {
  return requestClient.post<unknown>('/sysPlugin/update', params);
}

export function deletePluginApi(id: number) {
  return requestClient.post<unknown>('/sysPlugin/delete', { id });
}
