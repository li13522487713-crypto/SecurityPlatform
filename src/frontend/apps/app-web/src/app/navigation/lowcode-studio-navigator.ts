import { lowcodeAppStudioPath } from "@atlas/app-shell-shared";

type NavigatorEnv = Record<string, string | undefined>;
type NavigatorLocationLike = Partial<Pick<Location, "assign" | "href" | "origin" | "protocol" | "hostname" | "port">>;

const LOWCODE_STUDIO_ORIGIN_KEY = "VITE_LOWCODE_STUDIO_ORIGIN";
const LOWCODE_STUDIO_PORT_KEY = "VITE_LOWCODE_STUDIO_PORT";
const DEFAULT_LOWCODE_STUDIO_PORT = "5183";

export interface LowcodeStudioNavigationResult {
  target: string;
  redirected: boolean;
  reason?: "missing-location" | "already-at-target";
}

function normalizeOrigin(origin: string): string | null {
  const trimmed = origin.trim();
  if (!trimmed) {
    return null;
  }

  try {
    const parsed = new URL(trimmed);
    return parsed.origin;
  } catch {
    return null;
  }
}

function normalizePort(port: string | undefined): string | null {
  const trimmed = port?.trim();
  if (!trimmed || !/^\d+$/.test(trimmed)) {
    return null;
  }

  const parsed = Number(trimmed);
  if (!Number.isInteger(parsed) || parsed < 1 || parsed > 65535) {
    return null;
  }

  return String(parsed);
}

function resolveEnvSource(env?: NavigatorEnv): NavigatorEnv {
  const source =
    env ??
    ((import.meta as ImportMeta & {
      env?: NavigatorEnv;
    }).env ?? {});
  return source;
}

function toUrl(locationLike: NavigatorLocationLike | null | undefined): URL | null {
  if (!locationLike) {
    return null;
  }

  const href = locationLike.href?.trim();
  if (href) {
    try {
      return new URL(href);
    } catch {
      // ignore invalid href and fallback to origin/protocol
    }
  }

  const origin = locationLike.origin?.trim();
  if (origin) {
    try {
      return new URL(origin);
    } catch {
      // ignore invalid origin and fallback to protocol/hostname
    }
  }

  const protocol = locationLike.protocol?.trim();
  const hostname = locationLike.hostname?.trim();
  if (!protocol || !hostname) {
    return null;
  }

  const port = normalizePort(locationLike.port);
  const host = port ? `${hostname}:${port}` : hostname;
  try {
    return new URL(`${protocol}//${host}`);
  } catch {
    return null;
  }
}

function inferLowcodeStudioOrigin(
  env: NavigatorEnv,
  locationLike?: NavigatorLocationLike | null
): string | null {
  const currentUrl = toUrl(locationLike);
  if (!currentUrl || (currentUrl.protocol !== "http:" && currentUrl.protocol !== "https:")) {
    return null;
  }

  const targetUrl = new URL(currentUrl.origin);
  targetUrl.port = normalizePort(env[LOWCODE_STUDIO_PORT_KEY]) ?? DEFAULT_LOWCODE_STUDIO_PORT;
  return targetUrl.origin;
}

export function resolveLowcodeStudioOrigin(
  env?: NavigatorEnv,
  locationLike?: NavigatorLocationLike | null
): string | null {
  const source = resolveEnvSource(env);

  const raw = source[LOWCODE_STUDIO_ORIGIN_KEY];
  if (raw) {
    const normalized = normalizeOrigin(raw);
    if (normalized) {
      return normalized;
    }
  }

  return inferLowcodeStudioOrigin(source, locationLike);
}

export function buildLowcodeStudioUrl(
  appId: string,
  env?: NavigatorEnv,
  locationLike?: NavigatorLocationLike | null
): string {
  const path = lowcodeAppStudioPath(appId);
  const origin = resolveLowcodeStudioOrigin(env, locationLike);
  if (!origin) {
    return path;
  }

  return new URL(path, origin).toString();
}

export function navigateToLowcodeStudio(
  appId: string,
  env?: NavigatorEnv,
  locationLike?: NavigatorLocationLike | null
): LowcodeStudioNavigationResult {
  const resolvedLocation = locationLike ?? (typeof window !== "undefined" ? window.location : null);
  const target = buildLowcodeStudioUrl(appId, env, resolvedLocation);
  if (!resolvedLocation || typeof resolvedLocation.assign !== "function") {
    return { target, redirected: false, reason: "missing-location" };
  }

  const currentUrl = toUrl(resolvedLocation);
  if (currentUrl) {
    const targetUrl = new URL(target, currentUrl.origin);
    if (targetUrl.href === currentUrl.href) {
      return { target: targetUrl.href, redirected: false, reason: "already-at-target" };
    }
  }

  resolvedLocation.assign(target);
  return { target, redirected: true };
}
