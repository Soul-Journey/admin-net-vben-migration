import { requestClient } from '#/api/request';

type RawRecord = Record<string, unknown>;

export interface ListOrgParams {
  code?: string;
  id?: number;
  name?: string;
  tenantId?: number;
  type?: string;
}

export interface SysOrgRecord {
  children?: SysOrgRecord[];
  code?: string;
  createTime?: string;
  createUserName?: string;
  disabled?: boolean;
  directorId?: number;
  id: number;
  level?: number;
  name: string;
  orderNo?: number;
  pid?: number;
  remark?: string;
  status?: number;
  tenantId?: number;
  type?: string;
  updateTime?: string;
  updateUserName?: string;
}

export type SaveOrgParams = Partial<SysOrgRecord> & {
  code: string;
  name: string;
  orderNo: number;
  status: number;
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

function normalizeOrg(item: unknown): SysOrgRecord {
  const record = toRecord(item);
  const children = record.children ?? record.Children;
  const normalizedChildren = Array.isArray(children)
    ? children.map((child) => normalizeOrg(child))
    : undefined;

  return {
    children: normalizedChildren,
    code: toStringValue(record.code ?? record.Code),
    createTime: toStringValue(record.createTime ?? record.CreateTime),
    createUserName: toStringValue(record.createUserName ?? record.CreateUserName),
    directorId: toNumber(record.directorId ?? record.DirectorId),
    disabled: toBoolean(record.disabled ?? record.Disabled),
    id: toNumber(record.id ?? record.Id) ?? 0,
    level: toNumber(record.level ?? record.Level),
    name: toStringValue(record.name ?? record.Name) ?? '',
    orderNo: toNumber(record.orderNo ?? record.OrderNo),
    pid: toNumber(record.pid ?? record.Pid),
    remark: toStringValue(record.remark ?? record.Remark),
    status: toNumber(record.status ?? record.Status),
    tenantId: toNumber(record.tenantId ?? record.TenantId),
    type: toStringValue(record.type ?? record.Type),
    updateTime: toStringValue(record.updateTime ?? record.UpdateTime),
    updateUserName: toStringValue(record.updateUserName ?? record.UpdateUserName),
  };
}

export async function listOrgsApi(params: ListOrgParams = {}) {
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

export function addOrgApi(params: SaveOrgParams) {
  return requestClient.post<number>('/sysOrg/add', params);
}

export function updateOrgApi(params: SaveOrgParams & { id: number }) {
  return requestClient.post<unknown>('/sysOrg/update', params);
}

export function deleteOrgApi(id: number) {
  return requestClient.post<unknown>('/sysOrg/delete', { id });
}
