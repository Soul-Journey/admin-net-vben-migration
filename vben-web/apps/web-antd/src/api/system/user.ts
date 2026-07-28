import { requestClient } from '#/api/request';

export interface AdminNetPagedList<T> {
  hasNextPage?: boolean;
  hasPrevPage?: boolean;
  items: T[];
  page: number;
  pageSize: number;
  total: number;
  totalPages?: number;
}

export interface SysOrg {
  children?: SysOrg[];
  code?: string;
  id: number;
  name: string;
  pid?: number;
  tenantId?: number;
}

export interface SysPos {
  code?: string;
  id: number;
  name: string;
  tenantId?: number;
}

export interface SysRole {
  code?: string;
  id: number;
  name: string;
  tenantId?: number;
}

export interface SysTenantOption {
  host?: string;
  label: string;
  value: number;
}

export interface SysUserExtOrg {
  id?: number;
  orgId?: number;
  posId?: number;
  tenantId?: number;
  userId?: number;
}

export interface SysUserRecord {
  account: string;
  accountType?: number;
  address?: string;
  age?: number;
  avatar?: string;
  birthday?: string;
  cardType?: number;
  college?: string;
  createTime?: string;
  createUserName?: string;
  cultureLevel?: number;
  domainAccount?: string;
  emergencyAddress?: string;
  emergencyContact?: string;
  emergencyPhone?: string;
  idCardNum?: string;
  introduction?: string;
  joinDate?: string;
  jobNum?: string;
  nation?: string;
  officePhone?: string;
  politicalOutlook?: string;
  email?: string;
  extOrgIdList?: SysUserExtOrg[];
  id: number;
  nickName?: string;
  orderNo?: number;
  orgId?: number;
  orgName?: string;
  phone?: string;
  posId?: number;
  posName?: string;
  realName?: string;
  remark?: string;
  roleIdList?: number[];
  roleName?: string;
  sex?: number;
  status?: number;
  tenantId?: number;
  updateTime?: string;
  updateUserName?: string;
}

type RawRecord = Record<string, unknown>;

export interface PageUserParams {
  account?: string;
  orgId?: number;
  page: number;
  pageSize: number;
  phone?: string;
  posName?: string;
  realName?: string;
  tenantId?: number;
}

export type SaveUserParams = Partial<SysUserRecord> & {
  account: string;
  accountType: number;
  extOrgIdList: SysUserExtOrg[];
  orgId: number;
  phone: string;
  posId: number;
  realName: string;
  roleIdList: number[];
};

export function pageUsersApi(params: PageUserParams) {
  return requestClient.post<AdminNetPagedList<SysUserRecord>>(
    '/sysUser/page',
    params,
  );
}

export function addUserApi(params: SaveUserParams) {
  return requestClient.post<number>('/sysUser/add', params);
}

export function updateUserApi(params: SaveUserParams & { id: number }) {
  return requestClient.post<unknown>('/sysUser/update', params);
}

export function deleteUserApi(id: number) {
  return requestClient.post<unknown>('/sysUser/delete', { id });
}

export function setUserStatusApi(id: number, status: number) {
  return requestClient.post<number>('/sysUser/setStatus', { id, status });
}

export function resetUserPasswordApi(id: number) {
  return requestClient.post<string>('/sysUser/resetPwd', { id });
}

export function unlockUserLoginApi(id: number) {
  return requestClient.post<unknown>('/sysUser/unlockLogin', { id });
}

export function getUserRoleIdsApi(userId: number) {
  return requestClient.get<number[]>(`/sysUser/ownRoleList/${userId}`);
}

export function getUserExtOrgsApi(userId: number) {
  return requestClient.get<SysUserExtOrg[]>(`/sysUser/ownExtOrgList/${userId}`);
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

function normalizeOrg(item: unknown): SysOrg {
  const record = toRecord(item);
  const children = record.children ?? record.Children;
  const normalizedChildren = Array.isArray(children)
    ? children.map((child) => normalizeOrg(child))
    : undefined;

  return {
    ...(record as Partial<SysOrg>),
    children: normalizedChildren,
    code: toStringValue(record.code ?? record.Code),
    id: toNumber(record.id ?? record.Id) ?? 0,
    name: toStringValue(record.name ?? record.Name) ?? '',
    pid: toNumber(record.pid ?? record.Pid),
    tenantId: toNumber(record.tenantId ?? record.TenantId),
  };
}

export async function getOrgListApi(
  params: {
    code?: string;
    id?: number;
    name?: string;
    tenantId?: number;
    type?: string;
  } = {},
) {
  const orgs = await requestClient.get<unknown[]>('/sysOrg/list', {
    params: compactParams({
      Code: params.code,
      Id: params.id ?? 0,
      Name: params.name,
      TenantId: params.tenantId,
      Type: params.type,
    }),
  });
  return Array.isArray(orgs) ? orgs.map((item) => normalizeOrg(item)) : [];
}

export function getPosListApi(
  params: {
    code?: string;
    name?: string;
    tenantId?: number;
  } = {},
) {
  return requestClient.get<SysPos[]>('/sysPos/list', {
    params: compactParams(params),
  });
}

export function getRoleListApi() {
  return requestClient.get<SysRole[]>('/sysRole/list');
}

export function getTenantListApi() {
  return requestClient.get<SysTenantOption[]>('/sysTenant/list');
}
