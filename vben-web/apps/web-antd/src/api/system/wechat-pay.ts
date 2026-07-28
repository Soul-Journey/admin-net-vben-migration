import type { AdminNetPagedList } from './user';

import { requestClient } from '#/api/request';

export interface WechatPayPageParams {
  createTimeRange?: [string, string];
  keyword?: string;
  page: number;
  pageSize: number;
}

export interface WechatPayRecord {
  attachment?: string;
  bankType?: string;
  businessId?: number;
  createTime?: string;
  description?: string;
  goodsTag?: string;
  id: number;
  outTradeNumber: string;
  payerTotal?: number;
  qrcodeContent?: string;
  successTime?: string;
  tags?: string;
  total: number;
  tradeState?: string;
  tradeStateDescription?: string;
  tradeType?: string;
  transactionId?: string;
}

export interface WechatRefundRecord {
  channel?: string;
  createTime?: string;
  id: number;
  outRefundNumber: string;
  reason?: string;
  refund: number;
  successTime?: string;
  tradeState?: string;
  tradeStateDescription?: string;
  transactionId: string;
  userReceivedAccount?: string;
}

export interface WechatPayConfigurationStatus {
  appIdConfigured: boolean;
  certificateFileConfigured: boolean;
  certificateSerialNumberConfigured: boolean;
  merchantIdConfigured: boolean;
  merchantV3SecretConfigured: boolean;
  payCallbackConfigured: boolean;
  readyForPayment: boolean;
  readyForRefund: boolean;
  refundCallbackConfigured: boolean;
}

export interface CreateWechatNativePayParams {
  attachment?: string;
  businessId?: number;
  description: string;
  goodsTag?: string;
  tags?: string;
  total: number;
}

export interface WechatNativePayResult {
  outTradeNumber: string;
  qrcodeUrl: string;
}

export interface CreateWechatRefundParams {
  reason: string;
  refund: number;
  total: number;
  tradeId: string;
}

export function pageWechatPaysApi(params: WechatPayPageParams) {
  return requestClient.post<AdminNetPagedList<WechatPayRecord>>(
    '/sysWechatPay/page',
    params,
  );
}

export function getWechatPayConfigurationStatusApi() {
  return requestClient.get<WechatPayConfigurationStatus>(
    '/sysWechatPay/configurationStatus',
  );
}

export function createWechatNativePayApi(params: CreateWechatNativePayParams) {
  return requestClient.post<WechatNativePayResult>(
    '/sysWechatPay/payTransactionNative',
    params,
  );
}

export function listWechatRefundsApi(transactionId: string) {
  return requestClient.post<WechatRefundRecord[]>(
    '/sysWechatPay/listRefund',
    transactionId,
  );
}

export function createWechatRefundApi(params: CreateWechatRefundParams) {
  return requestClient.post<unknown>('/sysWechatPay/refundDomestic', params);
}

export function getWechatPayInfoApi(tradeId: string) {
  return requestClient.get<WechatPayRecord>(
    `/sysWechatPay/payInfo/${encodeURIComponent(tradeId)}`,
  );
}

export function syncWechatPayInfoApi(tradeId: string) {
  return requestClient.get<WechatPayRecord>(
    `/sysWechatPay/payInfoFromWechat/${encodeURIComponent(tradeId)}`,
  );
}
