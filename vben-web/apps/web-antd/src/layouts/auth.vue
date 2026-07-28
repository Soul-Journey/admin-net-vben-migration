<script lang="ts" setup>
import { computed } from 'vue';

import { AuthPageLayout } from '@vben/layouts';
import { preferences } from '@vben/preferences';

import { $t } from '#/locales';
import { loadSystemBranding } from '#/store';

void loadSystemBranding();

const appName = computed(() => preferences.app.name);
const logo = computed(() => preferences.logo.source);
const logoDark = computed(() => preferences.logo.sourceDark);
</script>

<template>
  <AuthPageLayout
    :app-name="appName"
    :logo="logo"
    :logo-dark="logoDark"
    :page-description="$t('authentication.pageDesc')"
    :page-title="$t('authentication.pageTitle')"
  >
    <!-- 自定义工具栏 -->
    <!-- <template #toolbar></template> -->
  </AuthPageLayout>
</template>

<style scoped>
:global(.slide-right-enter-active),
:global(.slide-right-leave-active) {
  transition: none !important;
  animation: none !important;
}

:global(.slide-right-enter-from),
:global(.slide-right-leave-to) {
  opacity: 1 !important;
  transform: none !important;
}

:global(.animate-float) {
  animation: none !important;
}
</style>
