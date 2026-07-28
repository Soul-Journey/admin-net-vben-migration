import type { AdminNetPagedList } from './user';

import { requestClient } from '#/api/request';

type RawRecord = Record<string, unknown>;

export interface PageTenantParams {
  name?: string;
  page: number;
  pageSize: number;
  phone?: string;
}

export interface TenantLoginResult {
  accessToken?: string;
  refreshToken?: string;
}

export interface UserRegWayOption {
  id: number;
  name: string;
  orgName?: string;
  posName?: string;
  roleName?: string;
  tenantId?: number;
}

export interface SysTenantRecord {
  adminAccount?: string;
  appId?: number;
  configId?: string;
  connection?: string;
  copyright?: string;
  createTime?: string;
  createUserName?: string;
  dbType?: number;
  email?: string;
  enableReg?: number;
  host?: string;
  icp?: string;
  icpUrl?: string;
  id: number;
  logo?: string;
  logoBase64?: string;
  logoFileName?: string;
  name: string;
  orderNo?: number;
  phone?: string;
  regWayId?: number;
  remark?: string;
  slaveConnections?: string;
  status?: number;
  tenantType?: number;
  title?: string;
  updateTime?: string;
  updateUserName?: string;
  userId?: number;
  viceDesc?: string;
  viceTitle?: string;
  watermark?: string;
}

export type SaveTenantParams = Partial<SysTenantRecord> & {
  adminAccount: string;
  copyright: string;
  icp: string;
  icpUrl: string;
  name: string;
  tenantType: number;
  title: string;
  viceDesc: string;
  viceTitle: string;
};

export interface GrantTenantMenuParams {
  id: number;
  menuIdList: number[];
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

function normalizeTenant(item: unknown): SysTenantRecord {
  const record = toRecord(item);
  return {
    adminAccount: toStringValue(record.adminAccount ?? record.AdminAccount),
    appId: toNumber(record.appId ?? record.AppId),
    configId: toStringValue(record.configId ?? record.ConfigId),
    connection: toStringValue(record.connection ?? record.Connection),
    copyright: toStringValue(record.copyright ?? record.Copyright),
    createTime: toStringValue(record.createTime ?? record.CreateTime),
    createUserName: toStringValue(
      record.createUserName ?? record.CreateUserName,
    ),
    dbType: toNumber(record.dbType ?? record.DbType),
    email: toStringValue(record.email ?? record.Email),
    enableReg: toNumber(record.enableReg ?? record.EnableReg),
    host: toStringValue(record.host ?? record.Host),
    icp: toStringValue(record.icp ?? record.Icp),
    icpUrl: toStringValue(record.icpUrl ?? record.IcpUrl),
    id: toNumber(record.id ?? record.Id) ?? 0,
    logo: toStringValue(record.logo ?? record.Logo),
    name: toStringValue(record.name ?? record.Name) ?? '',
    orderNo: toNumber(record.orderNo ?? record.OrderNo),
    phone: toStringValue(record.phone ?? record.Phone),
    regWayId: toNumber(record.regWayId ?? record.RegWayId),
    remark: toStringValue(record.remark ?? record.Remark),
    slaveConnections: toStringValue(
      record.slaveConnections ?? record.SlaveConnections,
    ),
    status: toNumber(record.status ?? record.Status),
    tenantType: toNumber(record.tenantType ?? record.TenantType),
    title: toStringValue(record.title ?? record.Title),
    updateTime: toStringValue(record.updateTime ?? record.UpdateTime),
    updateUserName: toStringValue(
      record.updateUserName ?? record.UpdateUserName,
    ),
    userId: toNumber(record.userId ?? record.UserId),
    viceDesc: toStringValue(record.viceDesc ?? record.ViceDesc),
    viceTitle: toStringValue(record.viceTitle ?? record.ViceTitle),
    watermark: toStringValue(record.watermark ?? record.Watermark),
  };
}

function normalizeUserRegWay(item: unknown): UserRegWayOption {
  const record = toRecord(item);
  return {
    id: toNumber(record.id ?? record.Id) ?? 0,
    name: toStringValue(record.name ?? record.Name) ?? '',
    orgName: toStringValue(record.orgName ?? record.OrgName),
    posName: toStringValue(record.posName ?? record.PosName),
    roleName: toStringValue(record.roleName ?? record.RoleName),
    tenantId: toNumber(record.tenantId ?? record.TenantId),
  };
}

export async function pageTenantsApi(params: PageTenantParams) {
  const data = await requestClient.post<AdminNetPagedList<unknown>>(
    '/sysTenant/page',
    params,
  );
  return {
    ...data,
    items: Array.isArray(data.items)
      ? data.items.map((item) => normalizeTenant(item))
      : [],
  } as AdminNetPagedList<SysTenantRecord>;
}

export function addTenantApi(params: SaveTenantParams) {
  return requestClient.post<unknown>('/sysTenant/add', params);
}

export function updateTenantApi(params: SaveTenantParams & { id: number }) {
  return requestClient.post<unknown>('/sysTenant/update', params);
}

export function deleteTenantApi(id: number) {
  return requestClient.post<unknown>('/sysTenant/delete', { id });
}

export function setTenantStatusApi(id: number, status: number) {
  return requestClient.post<number>('/sysTenant/setStatus', { id, status });
}

export function createTenantDbApi(id: number) {
  return requestClient.post<unknown>('/sysTenant/createDb', { id });
}

export function resetTenantPasswordApi(userId: number) {
  return requestClient.post<string>('/sysTenant/resetPwd', { userId });
}

export function syncTenantGrantMenuApi(id: number) {
  return requestClient.post<unknown>('/sysTenant/syncGrantMenu', { id });
}

export function getTenantMenuIdsApi(id: number) {
  return requestClient.get<number[]>('/sysTenant/tenantMenuList', {
    params: { id },
  });
}

export function grantTenantMenuApi(params: GrantTenantMenuParams) {
  return requestClient.post<unknown>('/sysTenant/grantMenu', params);
}

export function changeTenantApi(id: number) {
  return requestClient.post<TenantLoginResult>('/sysTenant/changeTenant', {
    id,
  });
}

export function goTenantApi(id: number) {
  return requestClient.post<TenantLoginResult>('/sysTenant/goTenant', { id });
}

export async function listUserRegWaysApi(
  params: {
    keyword?: string;
    name?: string;
    tenantId?: number;
  } = {},
) {
  const data = await requestClient.post<unknown[]>(
    '/sysUserRegWay/list',
    params,
  );
  return Array.isArray(data)
    ? data.map((item) => normalizeUserRegWay(item))
    : [];
}
