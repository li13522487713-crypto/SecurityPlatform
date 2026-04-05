const permissionRules: Array<{ prefix: string; permission: string }> = [
  { prefix: "/settings/org", permission: "system:admin" },
  { prefix: "/settings/auth", permission: "system:admin" },
  { prefix: "/system/", permission: "system:admin" },
  { prefix: "/settings/system/configs", permission: "config:view" },
  { prefix: "/settings/system", permission: "system:admin" },
  { prefix: "/settings/projects", permission: "system:admin" },
  { prefix: "/settings/license", permission: "system:admin" },
  { prefix: "/monitor/", permission: "system:admin" },
  { prefix: "/approval/", permission: "approval:view" },
  { prefix: "/ai/", permission: "ai:view" },
  { prefix: "/admin/ai-config", permission: "ai-admin-config:view" },
  { prefix: "/visualization/", permission: "apps:view" },
  { prefix: "/lowcode/", permission: "apps:view" },
  { prefix: "/apps/", permission: "apps:view" },
  { prefix: "/console/", permission: "apps:view" },
  { prefix: "/assets", permission: "assets:view" },
  { prefix: "/audit", permission: "audit:view" },
  { prefix: "/alert", permission: "alert:view" },
  { prefix: "/profile", permission: "profile:view" }
];

export function resolveRequiredPermission(path: string): string | undefined {
  const normalizedPath = path.length > 1 && path.endsWith("/") ? path.slice(0, -1) : path;
  const matched = permissionRules
    .filter((rule) => {
      const normalizedPrefix = rule.prefix.length > 1 && rule.prefix.endsWith("/")
        ? rule.prefix.slice(0, -1)
        : rule.prefix;
      return normalizedPath === normalizedPrefix || normalizedPath.startsWith(`${normalizedPrefix}/`);
    })
    .sort((a, b) => b.prefix.length - a.prefix.length)[0];
  return matched?.permission;
}

export function applyPermissionMetaToRoutePath(path: string, meta: Record<string, unknown>): void {
  const required = resolveRequiredPermission(path);
  if (required) {
    meta.requiresPermission = required;
  }
}
