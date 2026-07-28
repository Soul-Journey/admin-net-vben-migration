import { requestClient } from '#/api/request';

type RawRecord = Record<string, unknown>;

export interface ListRegWayParams {
  keyword?: string;
  name?: string;
  tenantId?: number;
}

export interface SysUserRegWayRecord {
  accountType?: number;
  createTime?: string;
  createUserName?: string;
  id: number;
  name: string;
  orderNo?: number;
  orgId?: number;
  orgName?: string;
  posId?: number;
  posName?: string;
  remark?: string;
  roleId?: number;
  roleName?: string;
  tenantId?: number;
  updateTime?: string;
  updateUserName?: string;
}

export type SaveRegWayParams = Partial<SysUserRegWayRecord> & {
  accountType: number;
  name: string;
  orgId: number;
  posId: number;
  roleId: number;
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

function toStringValue(value: unknown) {
  return typeof value === 'string' ? value : undefined;
}

function normalizeRegWay(item: unknown): SysUserRegWayRecord {
  const record = toRecord(item);

  return {
    accountType: toNumber(record.accountType ?? record.AccountType),
    createTime: toStringValue(record.createTime ?? record.CreateTime),
    createUserName: toStringValue(
      record.createUserName ?? record.CreateUserName,
    ),
    id: toNumber(record.id ?? record.Id) ?? 0,
    name: toStringValue(record.name ?? record.Name) ?? '',
    orderNo: toNumber(record.orderNo ?? record.OrderNo),
    orgId: toNumber(record.orgId ?? record.OrgId),
    orgName: toStringValue(record.orgName ?? record.OrgName),
    posId: toNumber(record.posId ?? record.PosId),
    posName: toStringValue(record.posName ?? record.PosName),
    remark: toStringValue(record.remark ?? record.Remark),
    roleId: toNumber(record.roleId ?? record.RoleId),
    roleName: toStringValue(record.roleName ?? record.RoleName),
    tenantId: toNumber(record.tenantId ?? record.TenantId),
    updateTime: toStringValue(record.updateTime ?? record.UpdateTime),
    updateUserName: toStringValue(
      record.updateUserName ?? record.UpdateUserName,
    ),
  };
}

export async function listRegWaysApi(params: ListRegWayParams = {}) {
  const data = await requestClient.post<unknown[]>(
    '/sysUserRegWay/list',
    compactParams({ ...params }),
  );
  return Array.isArray(data) ? data.map((item) => normalizeRegWay(item)) : [];
}

export function addRegWayApi(params: SaveRegWayParams) {
  return requestClient.post<number>('/sysUserRegWay/add', params);
}

export function updateRegWayApi(params: SaveRegWayParams & { id: number }) {
  return requestClient.post<unknown>('/sysUserRegWay/update', params);
}

export function deleteRegWayApi(id: number) {
  return requestClient.post<unknown>('/sysUserRegWay/delete', { id });
}
