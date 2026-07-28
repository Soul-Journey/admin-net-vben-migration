import { sm2 } from 'sm-crypto-v2';

import { requestClient } from '#/api/request';

type RawRecord = Record<string, unknown>;

export interface PersonalInfoRecord {
  account: string;
  address?: string;
  avatar?: string;
  birthday?: string;
  email?: string;
  id: number;
  introduction?: string;
  nickName?: string;
  orgId?: number;
  orgName?: string;
  phone?: string;
  posId?: number;
  posName?: string;
  realName: string;
  remark?: string;
  sex?: number;
  signature?: string;
}

export type UpdatePersonalInfoParams = Pick<
  PersonalInfoRecord,
  | 'address'
  | 'birthday'
  | 'email'
  | 'introduction'
  | 'nickName'
  | 'phone'
  | 'realName'
  | 'remark'
  | 'sex'
>;

export interface SysFileResult {
  bucketName?: string;
  filePath?: string;
  id?: number;
  suffix?: string;
  url?: string;
}

function toRecord(value: unknown): RawRecord {
  return value && typeof value === 'object' ? (value as RawRecord) : {};
}

function toNumber(value: unknown) {
  if (typeof value === 'number') return value;
  if (typeof value === 'string' && value.trim()) {
    const result = Number(value);
    return Number.isNaN(result) ? undefined : result;
  }
  return undefined;
}

function toStringValue(value: unknown) {
  return typeof value === 'string' ? value : undefined;
}

function normalizePersonalInfo(value: unknown): PersonalInfoRecord {
  const item = toRecord(value);
  return {
    account: toStringValue(item.account ?? item.Account) ?? '',
    address: toStringValue(item.address ?? item.Address),
    avatar: toStringValue(item.avatar ?? item.Avatar),
    birthday: toStringValue(item.birthday ?? item.Birthday),
    email: toStringValue(item.email ?? item.Email),
    id: toNumber(item.id ?? item.Id) ?? 0,
    introduction: toStringValue(item.introduction ?? item.Introduction),
    nickName: toStringValue(item.nickName ?? item.NickName),
    orgId: toNumber(item.orgId ?? item.OrgId),
    orgName: toStringValue(item.orgName ?? item.OrgName),
    phone: toStringValue(item.phone ?? item.Phone),
    posId: toNumber(item.posId ?? item.PosId),
    posName: toStringValue(item.posName ?? item.PosName),
    realName: toStringValue(item.realName ?? item.RealName) ?? '',
    remark: toStringValue(item.remark ?? item.Remark),
    sex: toNumber(item.sex ?? item.Sex),
    signature: toStringValue(item.signature ?? item.Signature),
  };
}

function normalizeFile(value: unknown): SysFileResult {
  const item = toRecord(value);
  return {
    bucketName: toStringValue(item.bucketName ?? item.BucketName),
    filePath: toStringValue(item.filePath ?? item.FilePath),
    id: toNumber(item.id ?? item.Id),
    suffix: toStringValue(item.suffix ?? item.Suffix),
    url: toStringValue(item.url ?? item.Url),
  };
}

export async function getPersonalInfoApi() {
  return normalizePersonalInfo(
    await requestClient.get<unknown>('/sysUser/baseInfo'),
  );
}

export function updatePersonalInfoApi(params: UpdatePersonalInfoParams) {
  return requestClient.post<number>('/sysUser/baseInfo', params);
}

export function changePersonalPasswordApi(
  passwordOld: string,
  passwordNew: string,
) {
  const publicKey = import.meta.env.VITE_SM_PUBLIC_KEY;
  const encrypt = (value: string) =>
    publicKey ? sm2.doEncrypt(value, publicKey, 1) : value;
  return requestClient.post<number>('/sysUser/changePwd', {
    passwordNew: encrypt(passwordNew),
    passwordOld: encrypt(passwordOld),
  });
}

async function uploadPersonalFile(path: string, file: File) {
  const formData = new FormData();
  formData.append('file', file);
  return normalizeFile(await requestClient.post<unknown>(path, formData));
}

export function uploadAvatarApi(file: File) {
  return uploadPersonalFile('/sysFile/uploadAvatar', file);
}

export function uploadSignatureApi(file: File) {
  return uploadPersonalFile('/sysFile/uploadSignature', file);
}

export function resolvePersonalFileUrl(file?: null | string) {
  if (!file) return '';
  if (/^(?:data:|https?:\/\/)/i.test(file) || file.startsWith('/')) return file;
  return `/${file.replace(/^\/+/, '')}`;
}
