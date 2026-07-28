import type { AdminNetPagedList } from './user';

import { requestClient } from '#/api/request';

type RawRecord = Record<string, unknown>;

export interface RegionQuery {
  code?: string;
  name?: string;
  page: number;
  pageSize: number;
  pid?: number;
}

export interface SaveRegionParams {
  cityCode?: string;
  code: string;
  id?: number;
  name: string;
  orderNo: number;
  pid: number;
  remark?: string;
}

export interface SysRegionRecord {
  children?: SysRegionRecord[];
  cityCode?: string;
  code: string;
  id: number;
  level: number;
  name: string;
  orderNo: number;
  pid: number;
  remark?: string;
}

export interface RegionSyncResult {
  cityCount: number;
  countyCount: number;
  provinceCount: number;
  source: string;
  total: number;
  version: string;
}

function toRecord(value: unknown): RawRecord {
  return value && typeof value === 'object' ? (value as RawRecord) : {};
}

function toNumber(value: unknown) {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : 0;
}

function toText(value: unknown) {
  return value === undefined || value === null ? undefined : String(value);
}

function normalizeRegion(value: unknown): SysRegionRecord {
  const item = toRecord(value);
  const children = item.children ?? item.Children;
  return {
    children: Array.isArray(children)
      ? children.map((child) => normalizeRegion(child))
      : undefined,
    cityCode: toText(item.cityCode ?? item.CityCode),
    code: toText(item.code ?? item.Code) ?? '',
    id: toNumber(item.id ?? item.Id),
    level: toNumber(item.level ?? item.Level),
    name: toText(item.name ?? item.Name) ?? '',
    orderNo: toNumber(item.orderNo ?? item.OrderNo),
    pid: toNumber(item.pid ?? item.Pid),
    remark: toText(item.remark ?? item.Remark),
  };
}

export async function pageRegionsApi(params: RegionQuery) {
  const data = await requestClient.post<AdminNetPagedList<unknown>>(
    '/sysRegion/page',
    params,
  );
  return {
    ...data,
    items: Array.isArray(data.items)
      ? data.items.map((item) => normalizeRegion(item))
      : [],
  } as AdminNetPagedList<SysRegionRecord>;
}

export async function listRegionChildrenApi(id = 0) {
  const data = await requestClient.get<unknown[]>('/sysRegion/list', {
    params: { id },
  });
  return Array.isArray(data) ? data.map((item) => normalizeRegion(item)) : [];
}

export function addRegionApi(params: SaveRegionParams) {
  return requestClient.post<number>('/sysRegion/add', params);
}

export function updateRegionApi(params: SaveRegionParams & { id: number }) {
  return requestClient.post<unknown>('/sysRegion/update', params);
}

export function deleteRegionApi(id: number) {
  return requestClient.post<number>('/sysRegion/delete', { id });
}

export function syncRegionsApi() {
  return requestClient.post<RegionSyncResult>('/sysRegion/sync');
}
