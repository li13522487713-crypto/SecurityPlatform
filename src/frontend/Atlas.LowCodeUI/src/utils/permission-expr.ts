/**
 * 【高级 VII-7.4 权限控制】
 * PermissionExprHelper：基于 RBAC permissions 数组的 visibleOn/disabledOn 表达式生成函数
 */

/**
 * 生成基于单个权限的 visibleOn 表达式
 *
 * @description
 * 假设 AMIS 数据域中存在 `currentUser.permissions` 数组（由 useAmisEnv 的 data 注入）。
 * 生成的表达式在运行时检查用户是否拥有指定权限。
 *
 * @example
 * ```ts
 * permissionVisibleOn('user:create')
 * // => "data.currentUser && data.currentUser.permissions && data.currentUser.permissions.indexOf('user:create') > -1"
 * ```
 */
export function permissionVisibleOn(permission: string): Record<string, string> {
  return {
    visibleOn: `data.currentUser && data.currentUser.permissions && data.currentUser.permissions.indexOf('${permission}') > -1`,
  };
}

/** 基于权限的 disabledOn 表达式（无权限时禁用） */
export function permissionDisabledOn(permission: string): Record<string, string> {
  return {
    disabledOn: `!data.currentUser || !data.currentUser.permissions || data.currentUser.permissions.indexOf('${permission}') === -1`,
  };
}

/** 基于角色的 visibleOn */
export function roleVisibleOn(role: string): Record<string, string> {
  return {
    visibleOn: `data.currentUser && data.currentUser.roles && data.currentUser.roles.indexOf('${role}') > -1`,
  };
}

/** 基于角色的 disabledOn */
export function roleDisabledOn(role: string): Record<string, string> {
  return {
    disabledOn: `!data.currentUser || !data.currentUser.roles || data.currentUser.roles.indexOf('${role}') === -1`,
  };
}

/** 任一权限满足即可见 */
export function anyPermissionVisibleOn(...permissions: string[]): Record<string, string> {
  const conditions = permissions.map(
    (p) => `(data.currentUser.permissions && data.currentUser.permissions.indexOf('${p}') > -1)`,
  );
  return {
    visibleOn: `data.currentUser && (${conditions.join(" || ")})`,
  };
}

/** 所有权限都满足才可见 */
export function allPermissionsVisibleOn(...permissions: string[]): Record<string, string> {
  const conditions = permissions.map(
    (p) => `data.currentUser.permissions.indexOf('${p}') > -1`,
  );
  return {
    visibleOn: `data.currentUser && data.currentUser.permissions && ${conditions.join(" && ")}`,
  };
}

/** 任一角色满足即可见 */
export function anyRoleVisibleOn(...roles: string[]): Record<string, string> {
  const conditions = roles.map(
    (r) => `(data.currentUser.roles && data.currentUser.roles.indexOf('${r}') > -1)`,
  );
  return {
    visibleOn: `data.currentUser && (${conditions.join(" || ")})`,
  };
}

/** 所有角色都满足才可见 */
export function allRolesVisibleOn(...roles: string[]): Record<string, string> {
  const conditions = roles.map(
    (r) => `data.currentUser.roles.indexOf('${r}') > -1`,
  );
  return {
    visibleOn: `data.currentUser && data.currentUser.roles && ${conditions.join(" && ")}`,
  };
}

/**
 * 将权限控制混入到 Schema 中
 *
 * @example
 * ```ts
 * withPermission(
 *   ajaxAction({ label: '删除', api: 'DELETE:/api/v1/users/${id}' }),
 *   'user:delete',
 * )
 * ```
 */
export function withPermission(
  schema: Record<string, unknown>,
  permission: string,
  mode: "visible" | "disabled" = "visible",
): Record<string, unknown> {
  const control = mode === "visible"
    ? permissionVisibleOn(permission)
    : permissionDisabledOn(permission);

  return { ...schema, ...control };
}

/**
 * 将角色控制混入到 Schema 中
 */
export function withRole(
  schema: Record<string, unknown>,
  role: string,
  mode: "visible" | "disabled" = "visible",
): Record<string, unknown> {
  const control = mode === "visible"
    ? roleVisibleOn(role)
    : roleDisabledOn(role);

  return { ...schema, ...control };
}

/**
 * 超级管理员可见
 */
export function adminOnly(schema: Record<string, unknown>): Record<string, unknown> {
  return withRole(schema, "Admin");
}
