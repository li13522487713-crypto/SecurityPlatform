import { lowcodeAppStudioPath } from "@atlas/app-shell-shared";

type NavigatorEnv = Record<string, string | undefined>;

const LOWCODE_STUDIO_ORIGIN_KEY = "VITE_LOWCODE_STUDIO_ORIGIN";

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

export function resolveLowcodeStudioOrigin(env?: NavigatorEnv): string | null {
  const source =
    env ??
    ((import.meta as ImportMeta & {
      env?: NavigatorEnv;
    }).env ?? {});

  const raw = source[LOWCODE_STUDIO_ORIGIN_KEY];
  if (!raw) {
    return null;
  }

  return normalizeOrigin(raw);
}

export function buildLowcodeStudioUrl(appId: string, env?: NavigatorEnv): string {
  const path = lowcodeAppStudioPath(appId);
  const origin = resolveLowcodeStudioOrigin(env);
  if (!origin) {
    return path;
  }

  return new URL(path, origin).toString();
}

export function navigateToLowcodeStudio(
  appId: string,
  env?: NavigatorEnv,
  locationLike?: Pick<Location, "assign"> | null
): string {
  const target = buildLowcodeStudioUrl(appId, env);
  const resolvedLocation = locationLike ?? (typeof window !== "undefined" ? window.location : null);
  resolvedLocation?.assign(target);
  return target;
}
