import type { AdminNetPagedList } from './user';

import { requestClient } from '#/api/request';

type RawRecord = Record<string, unknown>;

export interface OpenAccessQuery {
  accessKey?: string;
  page: number;
  pageSize: number;
}

export interface OpenAccessRecord {
  accessKey: string;
  bindTenantId: number;
  bindTenantName?: string;
  bindUserAccount?: string;
  bindUserId: number;
  createTime?: string;
  createUserName?: string;
  id: number;
  updateTime?: string;
  updateUserName?: string;
}

export interface OpenAccessUserOption {
  account: string;
  id: number;
  realName?: string;
}

export interface AddOpenAccessParams {
  accessKey: string;
  accessSecret: string;
  bindTenantId: number;
  bindUserId: number;
}

export interface UpdateOpenAccessParams {
  accessKey: string;
  bindTenantId: number;
  bindUserId: number;
  id: number;
}

export interface StoredSignatureParams {
  id: number;
  method: number;
  nonce: string;
  timestamp: number;
  url: string;
}

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

function normalizeOpenAccess(value: unknown): OpenAccessRecord {
  const item = recordOf(value);
  return {
    accessKey: textOf(item.accessKey ?? item.AccessKey) ?? '',
    bindTenantId: numberOf(item.bindTenantId ?? item.BindTenantId),
    bindTenantName: textOf(item.bindTenantName ?? item.BindTenantName),
    bindUserAccount: textOf(item.bindUserAccount ?? item.BindUserAccount),
    bindUserId: numberOf(item.bindUserId ?? item.BindUserId),
    createTime: textOf(item.createTime ?? item.CreateTime),
    createUserName: textOf(item.createUserName ?? item.CreateUserName),
    id: numberOf(item.id ?? item.Id),
    updateTime: textOf(item.updateTime ?? item.UpdateTime),
    updateUserName: textOf(item.updateUserName ?? item.UpdateUserName),
  };
}

function normalizeUser(value: unknown): OpenAccessUserOption {
  const item = recordOf(value);
  return {
    account: textOf(item.account ?? item.Account) ?? '',
    id: numberOf(item.id ?? item.Id),
    realName: textOf(item.realName ?? item.RealName),
  };
}

export async function pageOpenAccessApi(params: OpenAccessQuery) {
  const data = await requestClient.post<AdminNetPagedList<unknown>>(
    '/sysOpenAccess/pageSafe',
    params,
  );
  return {
    ...data,
    items: Array.isArray(data.items)
      ? data.items.map((item) => normalizeOpenAccess(item))
      : [],
  } as AdminNetPagedList<OpenAccessRecord>;
}

export function addOpenAccessApi(params: AddOpenAccessParams) {
  return requestClient.post<unknown>('/sysOpenAccess/add', params);
}

export function updateOpenAccessApi(params: UpdateOpenAccessParams) {
  return requestClient.post<unknown>('/sysOpenAccess/safe', params);
}

export function deleteOpenAccessApi(id: number) {
  return requestClient.post<unknown>('/sysOpenAccess/delete', { id });
}

export function createOpenAccessSecretApi() {
  return requestClient.post<string>('/sysOpenAccess/secret');
}

export function rotateOpenAccessSecretApi(id: number) {
  return requestClient.post<string>('/sysOpenAccess/rotateSecret', { id });
}

export function generateStoredSignatureApi(params: StoredSignatureParams) {
  return requestClient.post<string>(
    '/sysOpenAccess/generateStoredSignature',
    params,
  );
}

export async function listOpenAccessUsersApi(tenantId: number) {
  const data = await requestClient.post<unknown[]>('/sysTenant/userList', {
    tenantId,
  });
  return Array.isArray(data) ? data.map((item) => normalizeUser(item)) : [];
}
