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
import { unwrapAdminNetResponse } from '#/utils/adminnet/response';
import {
  getStoredAccessToken,
  getStoredRefreshToken,
  isJwtExpired,
  syncAdminNetTokensFromHeaders,
} from '#/utils/adminnet/token';

const { apiURL } = useAppConfig(import.meta.env, import.meta.env.PROD);

export function shouldReauthenticateForResponse(
  url: string | undefined,
  invalid: boolean,
  requestToken?: null | string,
  currentToken?: null | string,
) {
  if (!invalid || url?.includes('/sysAuth/logout')) return false;
  return !requestToken || !currentToken || requestToken === currentToken;
}

function readBearerToken(value: unknown) {
  if (typeof value !== 'string') return null;
  const match = /^Bearer\s+(.+)$/i.exec(value.trim());
  return match?.[1] ?? null;
}

function createRequestClient(baseURL: string, options?: RequestClientOptions) {
  const client = new RequestClient({
    ...options,
    baseURL,
    timeout: 50_000,
  });
  let reauthenticationPromise: null | Promise<void> = null;

  async function doReAuthenticate() {
    if (reauthenticationPromise) return reauthenticationPromise;
    reauthenticationPromise = (async () => {
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
    })().finally(() => {
      reauthenticationPromise = null;
    });
    return reauthenticationPromise;
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
      // Logout deliberately returns the invalid-token header. It must clear the
      // current session only, not schedule a second logout that can race with
      // the next successful login and erase its new token.
      const requestToken = readBearerToken(
        response.config.headers?.Authorization,
      );
      const currentToken =
        accessStore.accessToken || getStoredAccessToken();
      if (
        shouldReauthenticateForResponse(
          response.config.url,
          tokens.invalid,
          requestToken,
          currentToken,
        )
      ) {
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
        const requestToken = readBearerToken(
          error?.config?.headers?.Authorization,
        );
        const accessStore = useAccessStore();
        const currentToken =
          accessStore.accessToken || getStoredAccessToken();
        if (
          shouldReauthenticateForResponse(
            error?.config?.url,
            true,
            requestToken,
            currentToken,
          )
        ) {
          await doReAuthenticate();
        }
      }
      throw error;
    },
  });

  client.addResponseInterceptor(
    errorMessageResponseInterceptor((msg: string, error) => {
      const responseData = error?.response?.data ?? error ?? {};
      const errorMessage =
        responseData?.error ??
        responseData?.message ??
        responseData?.title ??
        '';
      message.error(errorMessage || msg);
    }),
  );

  return client;
}

export const requestClient = createRequestClient(apiURL, {
  responseReturn: 'data',
});

export const baseRequestClient = new RequestClient({ baseURL: apiURL });
