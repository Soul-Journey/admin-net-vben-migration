import { requestClient } from '#/api/request';

type RawRecord = Record<string, unknown>;

export interface ListPosParams {
  code?: string;
  name?: string;
  tenantId?: number;
}

export interface SysPosUser {
  account?: string;
  id?: number;
  realName?: string;
}

export interface SysPosRecord {
  code?: string;
  createTime?: string;
  createUserName?: string;
  id: number;
  name: string;
  orderNo?: number;
  remark?: string;
  status?: number;
  tenantId?: number;
  updateTime?: string;
  updateUserName?: string;
  userList?: SysPosUser[];
}

export type SavePosParams = Partial<SysPosRecord> & {
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

function toStringValue(value: unknown) {
  return typeof value === 'string' ? value : undefined;
}

function normalizePosUser(item: unknown): SysPosUser {
  const record = toRecord(item);
  return {
    account: toStringValue(record.account ?? record.Account),
    id: toNumber(record.id ?? record.Id),
    realName: toStringValue(record.realName ?? record.RealName),
  };
}

function normalizePos(item: unknown): SysPosRecord {
  const record = toRecord(item);
  const userList = record.userList ?? record.UserList;

  return {
    code: toStringValue(record.code ?? record.Code),
    createTime: toStringValue(record.createTime ?? record.CreateTime),
    createUserName: toStringValue(
      record.createUserName ?? record.CreateUserName,
    ),
    id: toNumber(record.id ?? record.Id) ?? 0,
    name: toStringValue(record.name ?? record.Name) ?? '',
    orderNo: toNumber(record.orderNo ?? record.OrderNo),
    remark: toStringValue(record.remark ?? record.Remark),
    status: toNumber(record.status ?? record.Status),
    tenantId: toNumber(record.tenantId ?? record.TenantId),
    updateTime: toStringValue(record.updateTime ?? record.UpdateTime),
    updateUserName: toStringValue(
      record.updateUserName ?? record.UpdateUserName,
    ),
    userList: Array.isArray(userList)
      ? userList.map((user) => normalizePosUser(user))
      : [],
  };
}

export async function listPositionsApi(params: ListPosParams = {}) {
  const positions = await requestClient.get<unknown[]>('/sysPos/list', {
    params: compactParams({
      Code: params.code,
      Name: params.name,
      TenantId: params.tenantId,
    }),
  });
  return Array.isArray(positions)
    ? positions.map((item) => normalizePos(item))
    : [];
}

export function addPositionApi(params: SavePosParams) {
  return requestClient.post<number>('/sysPos/add', params);
}

export function updatePositionApi(params: SavePosParams & { id: number }) {
  return requestClient.post<unknown>('/sysPos/update', params);
}

export function deletePositionApi(id: number) {
  return requestClient.post<unknown>('/sysPos/delete', { id });
}
