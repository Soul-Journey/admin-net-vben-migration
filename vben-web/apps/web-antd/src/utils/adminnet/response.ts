import type { AdminNetResult } from '#/api/adminnet/types';

function extractMessage(value: unknown) {
  if (!value) {
    return 'Request Error';
  }
  if (typeof value === 'string') {
    return value;
  }
  return JSON.stringify(value);
}

export function unwrapAdminNetResponse<T>(body: AdminNetResult<T> | T): T {
  if (!body || typeof body !== 'object') {
    return body as T;
  }

  const response = body as AdminNetResult<T>;

  if (response.errors) {
    throw new Error(extractMessage(response.errors));
  }

  if (response.code === undefined) {
    return body as T;
  }

  if (response.code !== 200) {
    throw new Error(extractMessage(response.message));
  }

  return response.result as T;
}
