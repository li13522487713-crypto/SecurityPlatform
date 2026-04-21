import { lowcodeAppStudioPath } from "@atlas/app-shell-shared";

type NavigateLike = (path: string) => void;
type NavigatorLocationLike = Partial<Pick<Location, "assign" | "href" | "pathname" | "search" | "hash">>;

export interface LowcodeStudioNavigationResult {
  target: string;
  redirected: boolean;
  reason?: "missing-location" | "already-at-target";
}

export function buildLowcodeStudioPath(appId: string): string {
  return lowcodeAppStudioPath(appId);
}

function getCurrentPath(locationLike: NavigatorLocationLike | null | undefined): string | null {
  if (!locationLike) {
    return null;
  }

  if (locationLike.href) {
    try {
      const parsed = new URL(locationLike.href);
      return `${parsed.pathname}${parsed.search}${parsed.hash}`;
    } catch {
      // ignore invalid href and fallback to path fragments
    }
  }

  if (!locationLike.pathname) {
    return null;
  }

  return `${locationLike.pathname}${locationLike.search ?? ""}${locationLike.hash ?? ""}`;
}

export function navigateToLowcodeStudio(
  appId: string,
  navigate?: NavigateLike,
  locationLike?: NavigatorLocationLike | null
): LowcodeStudioNavigationResult {
  const resolvedLocation = locationLike ?? (typeof window !== "undefined" ? window.location : null);
  const target = buildLowcodeStudioPath(appId);
  const currentPath = getCurrentPath(resolvedLocation);
  if (currentPath === target) {
    return { target, redirected: false, reason: "already-at-target" };
  }

  if (navigate) {
    navigate(target);
    return { target, redirected: true };
  }

  if (!resolvedLocation || typeof resolvedLocation.assign !== "function") {
    return { target, redirected: false, reason: "missing-location" };
  }

  resolvedLocation.assign(target);
  return { target, redirected: true };
}

export function resolveLowcodeStudioOrigin(): null {
  return null;
}

export function buildLowcodeStudioUrl(appId: string): string {
  return buildLowcodeStudioPath(appId);
}
