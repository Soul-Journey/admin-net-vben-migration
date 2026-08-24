interface AccessibleRouteLike {
  children?: AccessibleRouteLike[];
  meta?: {
    disabled?: boolean;
    hideInMenu?: boolean;
    link?: string;
  };
  path?: string;
  redirect?: string;
}

const NO_ACCESS_PATH = '/no-access';

function normalizePath(path: string, parentPath = '') {
  if (path.startsWith('/')) return path;
  return `${parentPath.replace(/\/$/, '')}/${path}`.replaceAll('//', '/');
}

function isNavigable(route: AccessibleRouteLike, path: string) {
  return (
    Boolean(path) &&
    !route.meta?.disabled &&
    !route.meta?.hideInMenu &&
    !route.meta?.link &&
    !path.includes(':') &&
    !path.includes('*') &&
    !/^https?:\/\//i.test(path)
  );
}

function collectNavigablePaths(
  routes: AccessibleRouteLike[],
  parentPath = '',
): string[] {
  const paths: string[] = [];

  for (const route of routes) {
    const path = route.path ? normalizePath(route.path, parentPath) : parentPath;
    if (route.children && route.children.length > 0) {
      paths.push(...collectNavigablePaths(route.children, path));
      continue;
    }

    if (isNavigable(route, path)) {
      paths.push(path);
    }
  }

  return paths;
}

function resolveAccessibleHomePath(
  routes: AccessibleRouteLike[],
  preferredPath?: null | string,
) {
  const paths = collectNavigablePaths(routes);
  const preferred = preferredPath?.trim();
  return preferred && paths.includes(preferred)
    ? preferred
    : (paths[0] ?? NO_ACCESS_PATH);
}

export { collectNavigablePaths, NO_ACCESS_PATH, resolveAccessibleHomePath };
export type { AccessibleRouteLike };
