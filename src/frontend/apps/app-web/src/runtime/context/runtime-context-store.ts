import { defineStore } from "pinia";
import { getTenantId } from "@atlas/shared-core";
import type {
  RuntimeContext,
  RuntimeAppInfo,
  RuntimePageInfo,
  RuntimeUserInfo,
  RuntimeRouteInfo,
  RuntimeEnvInfo,
  RuntimeRecordInfo,
} from "./runtime-context-types";
import {
  createEmptyRuntimeContext,
  flattenContextForExpression,
} from "./runtime-context-types";
import type { ValueMap, RuntimePageMode } from "../types/base-types";

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
      global?: ValueMap;
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
     * 部分更新上下文（manifest 携带 initialContextPatch 时使用）。
     */
    patchContext(patch: Partial<RuntimeContext>) {
      if (patch.tenant) Object.assign(this.context.tenant, patch.tenant);
      if (patch.app) Object.assign(this.context.app, patch.app);
      if (patch.page) Object.assign(this.context.page, patch.page);
      if (patch.user) Object.assign(this.context.user, patch.user);
      if (patch.route) Object.assign(this.context.route, patch.route);
      if (patch.project) this.context.project = { ...this.context.project, ...patch.project };
      if (patch.record !== undefined) this.context.record = patch.record;
      if (patch.selection !== undefined) this.context.selection = patch.selection;
      if (patch.global) Object.assign(this.context.global, patch.global);
      if (patch.env) Object.assign(this.context.env, patch.env);
    },

    /**
     * 更新当前记录数据（form 场景）。
     */
    setRecord(record?: RuntimeRecordInfo) {
      this.context.record = record;
    },

    /**
     * 更新当前选中行（crud 多选场景）。
     */
    setSelection(selection?: ValueMap[]) {
      this.context.selection = selection;
    },

    /**
     * 更新单个全局变量。
     */
    setGlobalVar(name: string, value: unknown) {
      this.context.global[name] = value;
    },

    /**
     * 更新页面模式（view/edit/create）。
     */
    setPageMode(mode: RuntimePageMode) {
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
