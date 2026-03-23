/**
 * 【动作 V-5.4 条件控制】
 * visibleOn / disabledOn / requiredOn / hiddenOn 表达式工具函数
 */

/**
 * 构建 visibleOn 表达式
 *
 * @example
 * ```ts
 * visibleOn("data.role === 'admin'")
 * visibleOn(and("data.status === 'active'", "data.age >= 18"))
 * ```
 */
export function visibleOn(expr: string): Record<string, string> {
  return { visibleOn: expr };
}

/** 构建 hiddenOn 表达式 */
export function hiddenOn(expr: string): Record<string, string> {
  return { hiddenOn: expr };
}

/** 构建 disabledOn 表达式 */
export function disabledOn(expr: string): Record<string, string> {
  return { disabledOn: expr };
}

/** 构建 requiredOn 表达式 */
export function requiredOn(expr: string): Record<string, string> {
  return { requiredOn: expr };
}

// ========== 表达式助手 ==========

/** AND 连接多个条件 */
export function and(...conditions: string[]): string {
  return conditions.map((c) => `(${c})`).join(" && ");
}

/** OR 连接多个条件 */
export function or(...conditions: string[]): string {
  return conditions.map((c) => `(${c})`).join(" || ");
}

/** NOT 取反 */
export function not(condition: string): string {
  return `!(${condition})`;
}

/** 等于 */
export function eq(field: string, value: string | number | boolean): string {
  if (typeof value === "string") {
    return `data.${field} === '${value}'`;
  }
  return `data.${field} === ${String(value)}`;
}

/** 不等于 */
export function neq(field: string, value: string | number | boolean): string {
  if (typeof value === "string") {
    return `data.${field} !== '${value}'`;
  }
  return `data.${field} !== ${String(value)}`;
}

/** 大于 */
export function gt(field: string, value: number): string {
  return `data.${field} > ${value}`;
}

/** 大于等于 */
export function gte(field: string, value: number): string {
  return `data.${field} >= ${value}`;
}

/** 小于 */
export function lt(field: string, value: number): string {
  return `data.${field} < ${value}`;
}

/** 小于等于 */
export function lte(field: string, value: number): string {
  return `data.${field} <= ${value}`;
}

/** 字段有值（非 null/undefined/空字符串） */
export function hasValue(field: string): string {
  return `data.${field} != null && data.${field} !== ''`;
}

/** 字段无值 */
export function isEmpty(field: string): string {
  return `data.${field} == null || data.${field} === ''`;
}

/** 字段包含某值（数组或字符串） */
export function includes(field: string, value: string): string {
  return `(data.${field} && data.${field}.indexOf('${value}') > -1)`;
}

/** 字段值在指定集合中 */
export function inList(field: string, values: Array<string | number>): string {
  const valStr = values.map((v) => typeof v === "string" ? `'${v}'` : String(v)).join(", ");
  return `[${valStr}].indexOf(data.${field}) > -1`;
}

// ========== 权限与角色条件 ==========

/** 基于角色的可见性（假设 data 中有 currentUser.roles 数组） */
export function hasRole(role: string): string {
  return `data.currentUser && data.currentUser.roles && data.currentUser.roles.indexOf('${role}') > -1`;
}

/** 基于权限的可见性 */
export function hasPermission(permission: string): string {
  return `data.currentUser && data.currentUser.permissions && data.currentUser.permissions.indexOf('${permission}') > -1`;
}

/** 必须拥有所有指定角色 */
export function hasAllRoles(...roles: string[]): string {
  return and(...roles.map(hasRole));
}

/** 拥有任一指定角色 */
export function hasAnyRole(...roles: string[]): string {
  return or(...roles.map(hasRole));
}

/** 必须拥有所有指定权限 */
export function hasAllPermissions(...permissions: string[]): string {
  return and(...permissions.map(hasPermission));
}

/** 拥有任一指定权限 */
export function hasAnyPermission(...permissions: string[]): string {
  return or(...permissions.map(hasPermission));
}
