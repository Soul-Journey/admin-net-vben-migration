import type { SystemInfoRecord } from '#/api';

import { preferences, updatePreferences } from '@vben/preferences';

import { getSystemInfoApi } from '#/api';

let loadingPromise: null | Promise<SystemInfoRecord> = null;

export function applySystemBranding(info: SystemInfoRecord) {
  const logo = info.logo?.trim() || preferences.logo.source;
  const title = info.title?.trim() || preferences.app.name;
  const watermark = info.watermark?.trim() || '';

  updatePreferences({
    app: {
      name: title,
      watermark: Boolean(watermark),
      watermarkContent: watermark,
    },
    copyright: {
      companyName: info.copyright?.trim() || title,
      companySiteLink:
        info.icpUrl?.trim() || preferences.copyright.companySiteLink,
    },
    logo: {
      source: logo,
      sourceDark: logo,
    },
  });
}

export async function loadSystemBranding(force = false) {
  if (loadingPromise && !force) return loadingPromise;

  loadingPromise = getSystemInfoApi()
    .then((info) => {
      applySystemBranding(info);
      return info;
    })
    .finally(() => {
      loadingPromise = null;
    });

  return loadingPromise;
}
