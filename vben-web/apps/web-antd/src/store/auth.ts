import type { Recordable, UserInfo } from '@vben/types';

import { ref } from 'vue';
import { useRouter } from 'vue-router';

import { LOGIN_PATH } from '@vben/constants';
import { preferences } from '@vben/preferences';
import { resetAllStores, useAccessStore, useUserStore } from '@vben/stores';

import { notification } from 'ant-design-vue';
import { defineStore } from 'pinia';

import {
  getAccessCodesApi,
  getUserInfoApi,
  loginApi,
  loginPhoneApi,
  logoutApi,
} from '#/api';
import { $t } from '#/locales';
import {
  clearAdminNetTokens,
  persistAdminNetTokens,
} from '#/utils/adminnet/token';

export const useAuthStore = defineStore('auth', () => {
  const accessStore = useAccessStore();
  const userStore = useUserStore();
  const router = useRouter();

  const loginLoading = ref(false);

  async function goHome(homePath?: string) {
    const target = homePath || preferences.app.defaultHomePath;
    await router.replace(target);
  }

  async function completeLogin(
    loginResult: Awaited<ReturnType<typeof loginApi>>,
    onSuccess?: () => Promise<void> | void,
  ) {
    let userInfo: null | UserInfo = null;
    const { accessToken, refreshToken } = loginResult;

    if (!accessToken) {
      return { userInfo };
    }

    accessStore.setAccessToken(accessToken);
    persistAdminNetTokens({ accessToken, refreshToken });

    const [fetchUserInfoResult, accessCodes] = await Promise.all([
      fetchUserInfo(),
      getAccessCodesApi(),
    ]);

    userInfo = fetchUserInfoResult;
    userStore.setUserInfo(userInfo);
    accessStore.setAccessCodes(accessCodes);

    accessStore.setLoginExpired(false);

    await (onSuccess ? onSuccess() : goHome(userInfo.homePath));

    if (userInfo.realName) {
      notification.success({
        description: `${$t('authentication.loginSuccessDesc')}:${userInfo.realName}`,
        duration: 3,
        message: $t('authentication.loginSuccess'),
      });
    }

    return { userInfo };
  }

  async function authLogin(
    params: Recordable<any>,
    onSuccess?: () => Promise<void> | void,
  ) {
    try {
      loginLoading.value = true;
      return await completeLogin(await loginApi(params), onSuccess);
    } finally {
      loginLoading.value = false;
    }
  }

  async function authPhoneLogin(
    params: Recordable<any>,
    onSuccess?: () => Promise<void> | void,
  ) {
    try {
      loginLoading.value = true;
      return await completeLogin(await loginPhoneApi(params), onSuccess);
    } finally {
      loginLoading.value = false;
    }
  }

  async function logout(redirect: boolean = true) {
    try {
      await logoutApi();
    } catch {
      // Ignore server-side logout errors and always clear local auth state.
    }

    resetAllStores();
    clearAdminNetTokens();
    accessStore.setLoginExpired(false);

    await router.replace({
      path: LOGIN_PATH,
      query: redirect ? {} : {},
    });
  }

  async function fetchUserInfo() {
    const userInfo = await getUserInfoApi();
    userStore.setUserInfo(userInfo);
    return userInfo;
  }

  function $reset() {
    loginLoading.value = false;
  }

  return {
    $reset,
    authLogin,
    authPhoneLogin,
    fetchUserInfo,
    loginLoading,
    logout,
  };
});
