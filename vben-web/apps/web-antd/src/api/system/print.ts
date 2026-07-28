import type { AdminNetPagedList } from './user';

import { requestClient } from '#/api/request';

type RawRecord = Record<string, unknown>;

export interface PrintQuery {
  name?: string;
  page: number;
  pageSize: number;
  tenantId?: number;
}

export interface PrintTenantOption {
  host?: string;
  label: string;
  value: number;
}

export interface SysPrintRecord {
  clientServiceAddress?: string;
  createTime?: string;
  createUserName?: string;
  id: number;
  name: string;
  orderNo: number;
  printDataDemo?: string;
  printParam?: string;
  printType: number;
  remark?: string;
  status: number;
  template: string;
  tenantId?: number;
  updateTime?: string;
  updateUserName?: string;
}

export type SavePrintParams = Omit<
  SysPrintRecord,
  'createTime' | 'createUserName' | 'id' | 'updateTime' | 'updateUserName'
> & {
  id?: number;
};

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

function normalizePrint(value: unknown): SysPrintRecord {
  const item = recordOf(value);
  return {
    clientServiceAddress: textOf(
      item.clientServiceAddress ?? item.ClientServiceAddress,
    ),
    createTime: textOf(item.createTime ?? item.CreateTime),
    createUserName: textOf(item.createUserName ?? item.CreateUserName),
    id: numberOf(item.id ?? item.Id),
    name: textOf(item.name ?? item.Name) ?? '',
    orderNo: numberOf(item.orderNo ?? item.OrderNo, 100),
    printDataDemo: textOf(item.printDataDemo ?? item.PrintDataDemo),
    printParam: textOf(item.printParam ?? item.PrintParam),
    printType: numberOf(item.printType ?? item.PrintType, 1),
    remark: textOf(item.remark ?? item.Remark),
    status: numberOf(item.status ?? item.Status, 1),
    template: textOf(item.template ?? item.Template) ?? '',
    tenantId: numberOf(item.tenantId ?? item.TenantId) || undefined,
    updateTime: textOf(item.updateTime ?? item.UpdateTime),
    updateUserName: textOf(item.updateUserName ?? item.UpdateUserName),
  };
}

export async function pagePrintsApi(params: PrintQuery) {
  const data = await requestClient.post<AdminNetPagedList<unknown>>(
    '/sysPrint/page',
    params,
  );
  return {
    ...data,
    items: Array.isArray(data.items)
      ? data.items.map((item) => normalizePrint(item))
      : [],
  } as AdminNetPagedList<SysPrintRecord>;
}

export async function listPrintTenantsApi() {
  const data = await requestClient.get<unknown[]>('/sysTenant/list');
  return (Array.isArray(data) ? data : []).map((value) => {
    const item = recordOf(value);
    return {
      host: textOf(item.host ?? item.Host),
      label: textOf(item.label ?? item.Label) ?? '',
      value: numberOf(item.value ?? item.Value),
    } satisfies PrintTenantOption;
  });
}

export function addPrintApi(params: SavePrintParams) {
  return requestClient.post<unknown>('/sysPrint/add', params);
}

export function updatePrintApi(params: SavePrintParams & { id: number }) {
  return requestClient.post<unknown>('/sysPrint/update', params);
}

export function deletePrintApi(id: number) {
  return requestClient.post<unknown>('/sysPrint/delete', { id });
}
