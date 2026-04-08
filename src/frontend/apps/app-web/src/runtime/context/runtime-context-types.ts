/**
 * 统一应用运行时上下文类型定义。
 *
 * 变量域严格对齐 docs/platform-unified-schema-and-expression.md:
 *   tenant > app > project > page > user > record > global
 *
 * 后端 ExpressionContext（Atlas.Core/Expressions/ExpressionContext.cs）
 * 支持相同六层：Global / Tenant / App / Page / User / Record。
 */

export interface RuntimeTenantInfo {
  id: string;
  code?: string;
}

export interface RuntimeAppInfo {
  id?: string;
  appKey: string;
  name?: string;
  releaseId?: string;
  releaseVersion?: number;
}

export interface RuntimePageInfo {
  id?: string;
  pageKey: string;
  title?: string;
  pageType?: string;
  mode?: "view" | "edit" | "create";
}

export interface RuntimeUserInfo {
  id?: string;
  name?: string;
  roles: string[];
  permissions: string[];
}

export interface RuntimeRouteInfo {
  path: string;
  params: Record<string, string>;
  query: Record<string, string>;
}

export interface RuntimeProjectInfo {
  id?: string;
  code?: string;
}

export interface RuntimeRecordInfo {
  id?: string;
  data?: Record<string, unknown>;
}

export interface RuntimeEnvInfo {
  traceId?: string;
  runtimeExecutionId?: string;
  releaseId?: string;
  releaseVersion?: number;
  directRuntimeMode?: boolean;
}

export interface RuntimeContext {
  tenant: RuntimeTenantInfo;
  app: RuntimeAppInfo;
  page: RuntimePageInfo;
  user: RuntimeUserInfo;
  route: RuntimeRouteInfo;
  project?: RuntimeProjectInfo;
  record?: RuntimeRecordInfo;
  selection?: Array<Record<string, unknown>>;
  global: Record<string, unknown>;
  env: RuntimeEnvInfo;
}

/**
 * 将 RuntimeContext 展平为后端 ExpressionContext 需要的
 * 六层字典结构（Record/User/Page/App/Tenant/Global）。
 */
export function flattenContextForExpression(ctx: RuntimeContext): {
  record: Record<string, unknown>;
  user: Record<string, unknown>;
  page: Record<string, unknown>;
  app: Record<string, unknown>;
  tenant: Record<string, unknown>;
  global: Record<string, unknown>;
} {
  return {
    record: ctx.record?.data ?? {},
    user: {
      id: ctx.user.id ?? "",
      name: ctx.user.name ?? "",
      roles: ctx.user.roles,
      permissions: ctx.user.permissions,
    },
    page: {
      id: ctx.page.id ?? "",
      pageKey: ctx.page.pageKey,
      title: ctx.page.title ?? "",
      pageType: ctx.page.pageType ?? "",
      mode: ctx.page.mode ?? "view",
    },
    app: {
      id: ctx.app.id ?? "",
      appKey: ctx.app.appKey,
      name: ctx.app.name ?? "",
      releaseId: ctx.app.releaseId ?? "",
      releaseVersion: ctx.app.releaseVersion ?? 0,
    },
    tenant: {
      id: ctx.tenant.id,
      code: ctx.tenant.code ?? "",
    },
    global: { ...ctx.global },
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
