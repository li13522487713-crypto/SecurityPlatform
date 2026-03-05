import { defineStore } from "pinia";
import type { RouteRecordRaw, Router } from "vue-router";
import type { RouterVo } from "@/types/api";
import { getRouters } from "@/services/api";
import { buildRoutesFromRouters } from "@/utils/dynamic-router";

function normalizeRouters(nodes: RouterVo[] | null | undefined): RouterVo[] {
  if (!Array.isArray(nodes)) {
    return [];
  }

  return nodes.map((node) => ({
    ...node,
    children: normalizeRouters(node.children)
  }));
}

interface PermissionState {
  routes: RouteRecordRaw[];
  addRoutes: RouteRecordRaw[];
  defaultRoutes: RouteRecordRaw[];
  topbarRouters: RouteRecordRaw[];
  sidebarRouters: RouterVo[];
  routeLoaded: boolean;
}

export const usePermissionStore = defineStore("permission", {
  state: (): PermissionState => ({
    routes: [],
    addRoutes: [],
    defaultRoutes: [],
    topbarRouters: [],
    sidebarRouters: [],
    routeLoaded: false
  }),
  actions: {
    reset() {
      this.routes = [];
      this.addRoutes = [];
      this.defaultRoutes = [];
      this.topbarRouters = [];
      this.sidebarRouters = [];
      this.routeLoaded = false;
    },
    async generateRoutes() {
      const routers = await getRouters();
      // 深拷贝一份给 sidebar 使用
      const sdata = normalizeRouters(JSON.parse(JSON.stringify(routers)) as RouterVo[]);
      const rdata = normalizeRouters(JSON.parse(JSON.stringify(routers)) as RouterVo[]);

      const sidebarRoutes = buildRoutesFromRouters(sdata, false, false);
      const rewriteRoutes = buildRoutesFromRouters(rdata, false, true);

      // 追加 404
      rewriteRoutes.push({
        path: "/:pathMatch(.*)*",
        name: "not-found-dynamic",
        redirect: "/404",
        meta: { hidden: true }
      });

      this.sidebarRouters = sdata;
      this.addRoutes = rewriteRoutes;
      // 假设当前常驻路由都在 router/index.ts 中，这里用 rewriteRoutes 代表整体需要动态添加的路由
      this.routes = rewriteRoutes;
      this.defaultRoutes = sidebarRoutes;
      this.topbarRouters = sidebarRoutes;

      this.routeLoaded = true;
      return rewriteRoutes;
    },
    registerRoutes(router: Router) {
      for (const route of this.addRoutes) {
        const name = route.name;
        if (typeof name === "string" && router.hasRoute(name)) {
          continue;
        }
        router.addRoute(route);
      }
    }
  }
});
