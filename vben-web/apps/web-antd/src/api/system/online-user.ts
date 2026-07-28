import type { AdminNetPagedList } from './user';

import { requestClient } from '#/api/request';

type RawRecord = Record<string, unknown>;

export interface OnlineUserRecord {
  browser?: string;
  connectionId: string;
  id: number;
  ip?: string;
  os?: string;
  realName?: string;
  tenantId?: number;
  time?: string;
  userId: number;
  userName: string;
}

export interface OnlineUserQuery {
  page: number;
  pageSize: number;
  realName?: string;
  tenantId?: number;
  userName?: string;
}

export interface TenantOption {
  host?: string;
  label: string;
  value: number;
}

function recordOf(value: unknown) {
  return value && typeof value === 'object' ? (value as RawRecord) : {};
}

function numberOf(value: unknown) {
  const result = Number(value);
  return Number.isFinite(result) ? result : 0;
}

function textOf(value: unknown) {
  return value === undefined || value === null ? undefined : String(value);
}

function normalizeOnlineUser(value: unknown): OnlineUserRecord {
  const item = recordOf(value);
  return {
    browser: textOf(item.browser ?? item.Browser),
    connectionId: textOf(item.connectionId ?? item.ConnectionId) ?? '',
    id: numberOf(item.id ?? item.Id),
    ip: textOf(item.ip ?? item.Ip),
    os: textOf(item.os ?? item.Os),
    realName: textOf(item.realName ?? item.RealName),
    tenantId: numberOf(item.tenantId ?? item.TenantId),
    time: textOf(item.time ?? item.Time),
    userId: numberOf(item.userId ?? item.UserId),
    userName: textOf(item.userName ?? item.UserName) ?? '',
  };
}

export async function pageOnlineUsersApi(params: OnlineUserQuery) {
  const data = await requestClient.post<AdminNetPagedList<unknown>>(
    '/sysOnlineUser/page',
    params,
  );
  return {
    ...data,
    items: Array.isArray(data.items)
      ? data.items.map((item) => normalizeOnlineUser(item))
      : [],
  } as AdminNetPagedList<OnlineUserRecord>;
}

export async function listOnlineUserTenantsApi() {
  const data = await requestClient.get<unknown[]>('/sysTenant/list');
  return Array.isArray(data)
    ? data.map((value) => {
        const item = recordOf(value);
        return {
          host: textOf(item.host ?? item.Host),
          label: textOf(item.label ?? item.Label) ?? '',
          value: numberOf(item.value ?? item.Value),
        } satisfies TenantOption;
      })
    : [];
}

export function forceOfflineApi(
  connectionId: string,
  currentConnectionId?: string,
) {
  return requestClient.post<unknown>('/sysOnlineUser/forceOffline', {
    connectionId,
    currentConnectionId,
  });
}
