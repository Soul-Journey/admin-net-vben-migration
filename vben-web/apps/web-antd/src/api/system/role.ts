import type { AdminNetPagedList, SysOrg } from './user';

import { requestClient } from '#/api/request';

interface RawRecord {
  [key: string]: unknown;
}

export interface PageRoleParams {
  code?: string;
  name?: string;
  page: number;
  pageSize: number;
  tenantId?: number;
}

export interface SysMenuTree {
  children?: SysMenuTree[];
  code?: string;
  component?: string;
  id: number;
  name?: string;
  orderNo?: number;
  path?: string;
  pid?: number;
  tenantId?: number;
  title: string;
  type?: number;
}

export interface SysRoleRecord {
  code: string;
  createTime?: string;
  createUserName?: string;
  dataScope?: number;
  id: number;
  menuIdList?: number[];
  name: string;
  orderNo?: number;
  orgIdList?: number[];
  remark?: string;
  status?: number;
  tenantId?: number;
  updateTime?: string;
  updateUserName?: string;
}

export type SaveRoleParams = Partial<SysRoleRecord> & {
  code: string;
  menuIdList: number[];
  name: string;
  orderNo: number;
  status: number;
};

export interface GrantRoleDataScopeParams {
  dataScope: number;
  id: number;
  orgIdList?: number[];
  tenantId?: number;
}

function compactParams(params: RawRecord) {
  return Object.fromEntries(
    Object.entries(params).filter(
      ([, value]) => value !== undefined && value !== null && value !== '',
    ),
  );
}

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

function normalizeMenu(item: unknown): SysMenuTree {
  const record = toRecord(item);
  const children = record.children ?? record.Children;
  const normalizedChildren = Array.isArray(children)
    ? children.map((child) => normalizeMenu(child))
    : undefined;

  return {
    ...(record as Partial<SysMenuTree>),
    children: normalizedChildren,
    code: toStringValue(record.code ?? record.Code),
    component: toStringValue(record.component ?? record.Component),
    id: toNumber(record.id ?? record.Id) ?? 0,
    name: toStringValue(record.name ?? record.Name),
    orderNo: toNumber(record.orderNo ?? record.OrderNo),
    path: toStringValue(record.path ?? record.Path),
    pid: toNumber(record.pid ?? record.Pid),
    tenantId: toNumber(record.tenantId ?? record.TenantId),
    title:
      toStringValue(record.title ?? record.Title ?? record.name ?? record.Name) ??
      '',
    type: toNumber(record.type ?? record.Type),
  };
}

export function pageRolesApi(params: PageRoleParams) {
  return requestClient.post<AdminNetPagedList<SysRoleRecord>>(
    '/sysRole/page',
    params,
  );
}

export function addRoleApi(params: SaveRoleParams) {
  return requestClient.post<unknown>('/sysRole/add', params);
}

export function updateRoleApi(params: SaveRoleParams & { id: number }) {
  return requestClient.post<unknown>('/sysRole/update', params);
}

export function deleteRoleApi(id: number) {
  return requestClient.post<unknown>('/sysRole/delete', { id });
}

export function setRoleStatusApi(id: number, status: number) {
  return requestClient.post<number>('/sysRole/setStatus', { id, status });
}

export function getRoleOwnMenuIdsApi(id: number) {
  return requestClient.get<number[]>('/sysRole/ownMenuList', {
    params: { Id: id },
  });
}

export function getRoleOwnOrgIdsApi(id: number) {
  return requestClient.get<number[]>('/sysRole/ownOrgList', {
    params: { Id: id },
  });
}

export function grantRoleDataScopeApi(params: GrantRoleDataScopeParams) {
  return requestClient.post<unknown>('/sysRole/grantDataScope', params);
}

export async function getMenuListApi(params: {
  tenantId?: number;
  title?: string;
  type?: number;
} = {}) {
  const menus = await requestClient.get<unknown[]>('/sysMenu/list', {
    params: compactParams({
      TenantId: params.tenantId,
      Title: params.title,
      Type: params.type,
    }),
  });
  return Array.isArray(menus) ? menus.map((item) => normalizeMenu(item)) : [];
}

export function flattenOrgIds(items: SysOrg[] = []): Array<number | string> {
  return items.flatMap((item) => [
    item.id,
    ...flattenOrgIds(item.children ?? []),
  ]);
}
