import type { Recordable, UserInfo } from '@vben/types';

import { ref } from 'vue';
import { useRouter } from 'vue-router';

import { LOGIN_PATH } from '@vben/constants';
import { preferences } from '@vben/preferences';
import { resetAllStores, useAccessStore, useUserStore } from '@vben/stores';
import { resetStaticRoutes } from '@vben/utils';

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
import { accessRoutes, routes } from '#/router/routes';
import {
  clearAdminNetTokens,
  persistAdminNetTokens,
} from '#/utils/adminnet/token';

import { generateAccess } from '../router/access';

export const useAuthStore = defineStore('auth', () => {
  const accessStore = useAccessStore();
  const userStore = useUserStore();
  const router = useRouter();

  const loginLoading = ref(false);

  function resetDynamicRoutes() {
    resetStaticRoutes(router, routes);
  }

  async function goHome(homePath?: string) {
    const target = homePath || preferences.app.defaultHomePath;
    await router.replace(target);
  }

  async function initializeAccessRoutes(userInfo: UserInfo) {
    resetDynamicRoutes();
    accessStore.setIsAccessChecked(false);

    const { accessibleMenus, accessibleRoutes } = await generateAccess({
      roles: userInfo.roles ?? [],
      router,
      routes: accessRoutes,
    });

    accessStore.setAccessMenus(accessibleMenus);
    accessStore.setAccessRoutes(accessibleRoutes);
    accessStore.setIsAccessChecked(true);
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
    // Build the current user's backend routes before leaving the login page.
    // This avoids relying on a second navigation guard pass after logout.
    await initializeAccessRoutes(userInfo);

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
    if (loginLoading.value) return;
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
    if (loginLoading.value) return;
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

    // Dynamic routes are not Pinia state. Remove them before clearing stores so
    // the next login cannot reuse a previous user's route records.
    resetDynamicRoutes();
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
