import { selectWorkspacePath, signPath } from "@atlas/app-shell-shared";

export interface StartupBootstrapSnapshot {
  loading: boolean;
  platformReady: boolean;
  appReady: boolean;
}

export interface StartupAuthSnapshot {
  loading: boolean;
  isAuthenticated: boolean;
}

export interface ResolveStartupRedirectOptions {
  pathname: string;
  bootstrap: StartupBootstrapSnapshot;
  auth: StartupAuthSnapshot;
}

export const STARTUP_ROUTE_PATHS = {
  platformNotReady: "/platform-not-ready",
  appSetup: "/app-setup",
  sign: signPath(),
  selectWorkspace: selectWorkspacePath()
} as const;

function isCurrentStartupPath(pathname: string, targetPath: string): boolean {
  return pathname === targetPath || pathname.startsWith(`${targetPath}/`);
}

export function resolveStartupRedirectTarget({
  pathname,
  bootstrap,
  auth
}: ResolveStartupRedirectOptions): string | null {
  if (bootstrap.loading || auth.loading) {
    return null;
  }

  if (!bootstrap.platformReady) {
    return isCurrentStartupPath(pathname, STARTUP_ROUTE_PATHS.platformNotReady)
      ? null
      : STARTUP_ROUTE_PATHS.platformNotReady;
  }

  if (!bootstrap.appReady) {
    return isCurrentStartupPath(pathname, STARTUP_ROUTE_PATHS.appSetup)
      ? null
      : STARTUP_ROUTE_PATHS.appSetup;
  }

  if (!auth.isAuthenticated) {
    return isCurrentStartupPath(pathname, STARTUP_ROUTE_PATHS.sign)
      ? null
      : STARTUP_ROUTE_PATHS.sign;
  }

  return isCurrentStartupPath(pathname, STARTUP_ROUTE_PATHS.selectWorkspace)
    ? null
    : STARTUP_ROUTE_PATHS.selectWorkspace;
}
