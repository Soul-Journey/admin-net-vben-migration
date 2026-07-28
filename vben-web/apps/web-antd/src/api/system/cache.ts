import { requestClient } from '#/api/request';

export function getCacheKeysApi() {
  return requestClient.get<string[]>('/sysCache/keyList');
}

export function getCacheValueApi(key: string) {
  return requestClient.get<unknown>(
    `/sysCache/value/${encodeURIComponent(key)}`,
  );
}

export function deleteCacheApi(key: string) {
  return requestClient.post<number>(
    `/sysCache/delete/${encodeURIComponent(key)}`,
  );
}

export function deleteCachePrefixApi(prefixKey: string) {
  return requestClient.post<number>(
    `/sysCache/deleteByPreKey/${encodeURIComponent(prefixKey)}`,
  );
}

export function clearCachesApi() {
  return requestClient.post<unknown>('/sysCache/clear');
}
