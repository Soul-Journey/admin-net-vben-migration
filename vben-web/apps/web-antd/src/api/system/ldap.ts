import type { AdminNetPagedList, SysTenantOption } from './user';

import { requestClient } from '#/api/request';

export interface PageLdapParams {
  host?: string;
  keyword?: string;
  page: number;
  pageSize: number;
  tenantId?: number;
}

export interface SysLdapRecord {
  authFilter: string;
  baseDn: string;
  bindAttrAccount: string;
  bindAttrCode: string;
  bindAttrEmployeeId: string;
  bindDn: string;
  createTime?: string;
  createUserName?: string;
  hasBindPass?: boolean;
  host: string;
  id: number;
  port: number;
  status: number;
  tenantId?: number;
  updateTime?: string;
  updateUserName?: string;
  version: number;
}

export type SaveLdapParams = Omit<
  SysLdapRecord,
  | 'createTime'
  | 'createUserName'
  | 'hasBindPass'
  | 'id'
  | 'updateTime'
  | 'updateUserName'
> & {
  bindPass?: string;
  id?: number;
};

export interface SyncLdapResult {
  added: number;
  total: number;
  updated: number;
}

export function pageLdapApi(params: PageLdapParams) {
  return requestClient.post<AdminNetPagedList<SysLdapRecord>>(
    '/sysLdap/page',
    params,
  );
}

export function addLdapApi(params: SaveLdapParams) {
  return requestClient.post<number>('/sysLdap/add', params);
}

export function updateLdapApi(params: SaveLdapParams & { id: number }) {
  return requestClient.post<unknown>('/sysLdap/update', params);
}

export function deleteLdapApi(id: number) {
  return requestClient.post<unknown>('/sysLdap/delete', { id });
}

export function syncLdapUsersApi(id: number) {
  return requestClient.post<SyncLdapResult>('/sysLdap/syncUser', { id });
}

export function syncLdapOrgsApi(id: number) {
  return requestClient.post<SyncLdapResult>('/sysLdap/syncDept', { id });
}

export type { SysTenantOption };
