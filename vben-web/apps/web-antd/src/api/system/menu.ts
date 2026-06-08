import type { AdminNetPagedList } from './user';

import { requestClient } from '#/api/request';

type RawRecord = Record<string, unknown>;

export interface SysMenuRecord {
  children?: SysMenuRecord[];
  component?: string;
  createTime?: string;
  createUserName?: string;
  icon?: string;
  id: number;
  isAffix?: boolean;
  isHide?: boolean;
  isIframe?: boolean;
  isKeepAlive?: boolean;
  name?: string;
  orderNo?: number;
  outLink?: string;
  path?: string;
  permission?: string;
  pid?: number;
  redirect?: string;
  remark?: string;
  status?: number;
  tenantId?: number;
  title: string;
  type?: number;
  updateTime?: string;
  updateUserName?: string;
}

export interface ListMenuParams {
  tenantId?: number;
  title?: string;
  type?: number;
}

export type SaveMenuParams = Partial<SysMenuRecord> & {
  status: number;
  tenantId?: number;
  title: string;
  type: number;
};

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

function toBoolean(value: unknown) {
  if (typeof value === 'boolean') {
    return value;
  }
  if (typeof value === 'string') {
    return value.toLowerCase() === 'true';
  }
  return undefined;
}

function toStringValue(value: unknown) {
  return typeof value === 'string' ? value : undefined;
}

function normalizeMenu(item: unknown): SysMenuRecord {
  const record = toRecord(item);
  const children = record.children ?? record.Children;
  const normalizedChildren = Array.isArray(children)
    ? children.map((child) => normalizeMenu(child))
    : undefined;

  return {
    children: normalizedChildren,
    component: toStringValue(record.component ?? record.Component),
    createTime: toStringValue(record.createTime ?? record.CreateTime),
    createUserName: toStringValue(record.createUserName ?? record.CreateUserName),
    icon: toStringValue(record.icon ?? record.Icon),
    id: toNumber(record.id ?? record.Id) ?? 0,
    isAffix: toBoolean(record.isAffix ?? record.IsAffix),
    isHide: toBoolean(record.isHide ?? record.IsHide),
    isIframe: toBoolean(record.isIframe ?? record.IsIframe),
    isKeepAlive: toBoolean(record.isKeepAlive ?? record.IsKeepAlive),
    name: toStringValue(record.name ?? record.Name),
    orderNo: toNumber(record.orderNo ?? record.OrderNo),
    outLink: toStringValue(record.outLink ?? record.OutLink),
    path: toStringValue(record.path ?? record.Path),
    permission: toStringValue(record.permission ?? record.Permission),
    pid: toNumber(record.pid ?? record.Pid),
    redirect: toStringValue(record.redirect ?? record.Redirect),
    remark: toStringValue(record.remark ?? record.Remark),
    status: toNumber(record.status ?? record.Status),
    tenantId: toNumber(record.tenantId ?? record.TenantId),
    title: toStringValue(record.title ?? record.Title) ?? '',
    type: toNumber(record.type ?? record.Type),
    updateTime: toStringValue(record.updateTime ?? record.UpdateTime),
    updateUserName: toStringValue(record.updateUserName ?? record.UpdateUserName),
  };
}

export async function listMenusApi(params: ListMenuParams = {}) {
  const menus = await requestClient.get<unknown[]>('/sysMenu/list', {
    params: compactParams({
      TenantId: params.tenantId,
      Title: params.title,
      Type: params.type,
    }),
  });
  return Array.isArray(menus) ? menus.map((item) => normalizeMenu(item)) : [];
}

export function addMenuApi(params: SaveMenuParams) {
  return requestClient.post<number>('/sysMenu/add', params);
}

export function updateMenuApi(params: SaveMenuParams & { id: number }) {
  return requestClient.post<unknown>('/sysMenu/update', params);
}

export function deleteMenuApi(id: number) {
  return requestClient.post<unknown>('/sysMenu/delete', { id });
}

export type MenuPagedList<T> = AdminNetPagedList<T>;
