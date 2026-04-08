/**
 * 统一应用运行时上下文类型定义。
 *
 * 变量域严格对齐 docs/platform-unified-schema-and-expression.md:
 *   tenant > app > project > page > user > record > global
 *
 * 后端 ExpressionContext（Atlas.Core/Expressions/ExpressionContext.cs）
 * 支持相同六层：Global / Tenant / App / Page / User / Record。
 */

import type {
  Id,
  Key,
  StringMap,
  ValueMap,
  RuntimeEntryMode,
  RuntimePageMode,
} from "../types/base-types";

export type { RuntimePageMode };

export interface RuntimeTenantInfo {
  id: Id;
  code?: string;
  name?: string;
}

export interface RuntimeAppInfo {
  id?: Id;
  appKey: Key;
  appCode?: string;
  name?: string;
  releaseId?: Id;
  releaseVersion?: number;
}

export interface RuntimePageInfo {
  id?: Id;
  pageKey: Key;
  pageName?: string;
  title?: string;
  pageType?: string;
  mode?: RuntimePageMode;
}

export interface RuntimeUserInfo {
  id?: Id;
  name?: string;
  displayName?: string;
  roles: string[];
  permissions: string[];
  departmentIds?: Id[];
}

export interface RuntimeRouteInfo {
  path: string;
  fullPath?: string;
  params: StringMap;
  query: StringMap;
}

export interface RuntimeProjectInfo {
  id?: Id;
  code?: string;
  name?: string;
}

export interface RuntimeRecordInfo {
  id?: Id;
  entityKey?: Key;
  data?: ValueMap;
}

export interface RuntimeEnvInfo {
  entryMode?: RuntimeEntryMode;
  directRuntimeMode?: boolean;
  traceId?: string;
  runtimeExecutionId?: Id;
  releaseId?: Id;
  releaseVersion?: number;
  locale?: string;
  timezone?: string;
}

export interface RuntimeContext {
  tenant: RuntimeTenantInfo;
  app: RuntimeAppInfo;
  page: RuntimePageInfo;
  user: RuntimeUserInfo;
  route: RuntimeRouteInfo;
  project?: RuntimeProjectInfo;
  record?: RuntimeRecordInfo;
  selection?: ValueMap[];
  global: ValueMap;
  env: RuntimeEnvInfo;
}

/**
 * 将 RuntimeContext 展平为后端 ExpressionContext 需要的
 * 六层字典结构（Record/User/Page/App/Tenant/Global）。
 */
export function flattenContextForExpression(ctx: RuntimeContext): {
  record: ValueMap;
  user: ValueMap;
  page: ValueMap;
  app: ValueMap;
  tenant: ValueMap;
  global: ValueMap;
  form: ValueMap;
} {
  return {
    record: ctx.record?.data ?? {},
    user: {
      id: ctx.user.id ?? "",
      name: ctx.user.name ?? "",
      displayName: ctx.user.displayName ?? "",
      roles: ctx.user.roles,
      permissions: ctx.user.permissions,
      departmentIds: ctx.user.departmentIds ?? [],
    },
    page: {
      id: ctx.page.id ?? "",
      pageKey: ctx.page.pageKey,
      pageName: ctx.page.pageName ?? "",
      title: ctx.page.title ?? "",
      pageType: ctx.page.pageType ?? "",
      mode: ctx.page.mode ?? "view",
    },
    app: {
      id: ctx.app.id ?? "",
      appKey: ctx.app.appKey,
      appCode: ctx.app.appCode ?? "",
      name: ctx.app.name ?? "",
      releaseId: ctx.app.releaseId ?? "",
      releaseVersion: ctx.app.releaseVersion ?? 0,
    },
    tenant: {
      id: ctx.tenant.id,
      code: ctx.tenant.code ?? "",
      name: ctx.tenant.name ?? "",
    },
    global: { ...ctx.global },
    form: {},
  };
}

/**
 * 创建一个空的 RuntimeContext 占位。
 */
export function createEmptyRuntimeContext(
  appKey: string,
  pageKey: string,
  tenantId: string,
): RuntimeContext {
  return {
    tenant: { id: tenantId },
    app: { appKey },
    page: { pageKey },
    user: { roles: [], permissions: [] },
    route: { path: "", params: {}, query: {} },
    global: {},
    env: {},
  };
}
