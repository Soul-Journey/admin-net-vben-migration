import type { AdminNetPagedList } from './user';

import { requestClient } from '#/api/request';

export interface PageConfigParams {
  code?: string;
  groupCode?: string;
  name?: string;
  page: number;
  pageSize: number;
}

export interface SysConfigRecord {
  code: string;
  createTime?: string;
  createUserName?: string;
  groupCode?: string;
  id: number;
  isSensitive?: boolean;
  name: string;
  orderNo: number;
  remark?: string;
  sysFlag: number;
  updateTime?: string;
  updateUserName?: string;
  value?: string;
}

export interface SystemRegistrationWay {
  label: string;
  value: number;
}

export interface SystemInfoRecord {
  captcha: number;
  copyright: string;
  enableReg: number;
  hideTenantForLogin: boolean;
  icp: string;
  icpUrl: string;
  logo: string;
  regWayId?: number;
  secondVer: number;
  title: string;
  viceDesc: string;
  viceTitle: string;
  watermark: string;
  wayList: SystemRegistrationWay[];
}

export interface SaveSystemInfoParams {
  captcha: number;
  copyright: string;
  enableReg: number;
  icp: string;
  icpUrl: string;
  logoBase64?: string;
  logoFileName?: string;
  regWayId?: number;
  secondVer: number;
  title: string;
  viceDesc: string;
  viceTitle: string;
  watermark?: string;
}

export type SaveConfigParams = Partial<SysConfigRecord> & {
  code: string;
  name: string;
  orderNo: number;
  sysFlag: number;
  value: string;
};

export function pageConfigsApi(params: PageConfigParams) {
  return requestClient.post<AdminNetPagedList<SysConfigRecord>>(
    '/sysConfig/page',
    params,
  );
}

export function getConfigGroupsApi() {
  return requestClient.get<string[]>('/sysConfig/groupList');
}

export function addConfigApi(params: SaveConfigParams) {
  return requestClient.post<unknown>('/sysConfig/add', params);
}

export function updateConfigApi(params: SaveConfigParams & { id: number }) {
  return requestClient.post<unknown>('/sysConfig/update', params);
}

export function deleteConfigApi(id: number) {
  return requestClient.post<unknown>('/sysConfig/delete', { id });
}

export function batchDeleteConfigsApi(ids: number[]) {
  return requestClient.post<unknown>('/sysConfig/batchDelete', ids);
}

export function getSystemInfoApi() {
  return requestClient.get<SystemInfoRecord>('/sysConfig/sysInfo');
}

export function saveSystemInfoApi(params: SaveSystemInfoParams) {
  return requestClient.post<unknown>('/sysConfig/saveSysInfo', params);
}
