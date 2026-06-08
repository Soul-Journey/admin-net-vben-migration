import type { RequestClientOptions } from '@vben/request';

import { useAppConfig } from '@vben/hooks';
import { preferences } from '@vben/preferences';
import {
  defaultResponseInterceptor,
  errorMessageResponseInterceptor,
  RequestClient,
} from '@vben/request';
import { useAccessStore } from '@vben/stores';

import { message } from 'ant-design-vue';

import { useAuthStore } from '#/store';
import {
  getStoredAccessToken,
  getStoredRefreshToken,
  isJwtExpired,
  syncAdminNetTokensFromHeaders,
} from '#/utils/adminnet/token';
import { unwrapAdminNetResponse } from '#/utils/adminnet/response';

const { apiURL } = useAppConfig(import.meta.env, import.meta.env.PROD);

function createRequestClient(baseURL: string, options?: RequestClientOptions) {
  const client = new RequestClient({
    ...options,
    baseURL,
    timeout: 50_000,
  });

  async function doReAuthenticate() {
    const accessStore = useAccessStore();
    const authStore = useAuthStore();
    accessStore.setAccessToken(null);
    if (
      preferences.app.loginExpiredMode === 'modal' &&
      accessStore.isAccessChecked
    ) {
      accessStore.setLoginExpired(true);
    } else {
      await authStore.logout();
    }
  }

  function formatToken(token: null | string) {
    return token ? `Bearer ${token}` : null;
  }

  client.addRequestInterceptor({
    fulfilled: async (config) => {
      const accessStore = useAccessStore();
      const accessToken = accessStore.accessToken || getStoredAccessToken();
      const refreshToken = getStoredRefreshToken();

      config.headers.Authorization = formatToken(accessToken);
      if (accessToken && refreshToken && isJwtExpired(accessToken)) {
        config.headers['X-Authorization'] = formatToken(refreshToken);
      }
      config.headers['Accept-Language'] = preferences.app.locale;
      return config;
    },
  });

  client.addResponseInterceptor({
    fulfilled: (response) => {
      const accessStore = useAccessStore();
      const tokens = syncAdminNetTokensFromHeaders(response.headers);
      if (tokens.invalid) {
        void doReAuthenticate();
      } else if (tokens.accessToken) {
        accessStore.setAccessToken(tokens.accessToken);
      }
      return response;
    },
  });

  client.addResponseInterceptor(
    defaultResponseInterceptor({
      codeField: 'code',
      dataField: unwrapAdminNetResponse,
      successCode: 200,
    }),
  );

  client.addResponseInterceptor({
    rejected: async (error) => {
      if (error?.response?.status === 401 || error?.code === 401) {
        await doReAuthenticate();
      }
      throw error;
    },
  });

  client.addResponseInterceptor(
    errorMessageResponseInterceptor((msg: string, error) => {
      const responseData = error?.response?.data ?? error ?? {};
      const errorMessage =
        responseData?.error ?? responseData?.message ?? responseData?.title ?? '';
      message.error(errorMessage || msg);
    }),
  );

  return client;
}

export const requestClient = createRequestClient(apiURL, {
  responseReturn: 'data',
});

export const baseRequestClient = new RequestClient({ baseURL: apiURL });
