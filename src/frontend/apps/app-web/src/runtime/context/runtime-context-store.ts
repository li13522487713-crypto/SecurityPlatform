import { defineStore } from "pinia";
import { getTenantId } from "@atlas/shared-core";
import type {
  RuntimeContext,
  RuntimeAppInfo,
  RuntimePageInfo,
  RuntimeUserInfo,
  RuntimeRouteInfo,
  RuntimeEnvInfo,
} from "./runtime-context-types";
import {
  createEmptyRuntimeContext,
  flattenContextForExpression,
} from "./runtime-context-types";

interface RuntimeContextState {
  context: RuntimeContext;
  initialized: boolean;
}

export const useRuntimeContextStore = defineStore("runtime-context", {
  state: (): RuntimeContextState => ({
    context: createEmptyRuntimeContext("", "", getTenantId() ?? ""),
    initialized: false,
  }),

  getters: {
    appKey: (state) => state.context.app.appKey,
    pageKey: (state) => state.context.page.pageKey,
    tenantId: (state) => state.context.tenant.id,
    executionId: (state) => state.context.env.runtimeExecutionId,
    releaseId: (state) => state.context.env.releaseId,
  },

  actions: {
    /**
     * 初始化运行时上下文（进入页面时调用）。
     */
    initContext(params: {
      app: RuntimeAppInfo;
      page: RuntimePageInfo;
      user: RuntimeUserInfo;
      route: RuntimeRouteInfo;
      env: RuntimeEnvInfo;
      global?: Record<string, unknown>;
    }) {
      this.context = {
        tenant: { id: getTenantId() ?? "" },
        app: params.app,
        page: params.page,
        user: params.user,
        route: params.route,
        global: params.global ?? {},
        env: params.env,
      };
      this.initialized = true;
    },

    /**
     * 更新当前记录数据（form 场景）。
     */
    setRecord(data: Record<string, unknown>, id?: string) {
      this.context.record = { id, data };
    },

    /**
     * 更新当前选中行（crud 多选场景）。
     */
    setSelection(rows: Array<Record<string, unknown>>) {
      this.context.selection = rows;
    },

    /**
     * 更新全局变量。
     */
    setGlobalVar(name: string, value: unknown) {
      this.context.global[name] = value;
    },

    /**
     * 更新页面模式（view/edit/create）。
     */
    setPageMode(mode: "view" | "edit" | "create") {
      this.context.page.mode = mode;
    },

    /**
     * 返回后端 CEL 表达式求值所需的扁平字典。
     */
    getExpressionVariables() {
      return flattenContextForExpression(this.context);
    },

    /**
     * 重置上下文（离开页面时调用）。
     */
    resetContext() {
      this.context = createEmptyRuntimeContext("", "", getTenantId() ?? "");
      this.initialized = false;
    },
  },
});
