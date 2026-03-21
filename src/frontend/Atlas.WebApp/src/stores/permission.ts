import { defineStore } from "pinia";
import type { RouteRecordRaw, Router } from "vue-router";
import type { RouterVo } from "@/types/api";
import { getRouters } from "@/services/api";
import { buildRoutesFromRouters } from "@/utils/dynamic-router";

const ROUTER_CACHE_KEY = "atlas_routers_cache";

function normalizeRouters(nodes: RouterVo[] | null | undefined): RouterVo[] {
  if (!Array.isArray(nodes)) {
    return [];
  }

  return nodes.map((node) => ({
    ...node,
    children: normalizeRouters(node.children)
  }));
}

// 平台最小可用菜单（getRouters 失败时的终极兜底）
function buildMinimalFallbackRouters(): RouterVo[] {
  return [
    {
      name: "首页",
      path: "/",
      component: "Layout",
      meta: { title: "首页", icon: "home" },
      children: [
        {
          name: "工作台",
          path: "index",
          component: "IndexPage",
          meta: { title: "工作台", icon: "home" }
        }
      ]
    }
  ] as RouterVo[];
}

interface PermissionState {
  routes: RouteRecordRaw[];
  addRoutes: RouteRecordRaw[];
  defaultRoutes: RouteRecordRaw[];
  topbarRouters: RouteRecordRaw[];
  sidebarRouters: RouterVo[];
  routeLoaded: boolean;
  routerLoadFailed: boolean;
}

export const usePermissionStore = defineStore("permission", {
  state: (): PermissionState => ({
    routes: [],
    addRoutes: [],
    defaultRoutes: [],
    topbarRouters: [],
    sidebarRouters: [],
    routeLoaded: false,
    routerLoadFailed: false
  }),
  actions: {
    reset() {
      this.routes = [];
      this.addRoutes = [];
      this.defaultRoutes = [];
      this.topbarRouters = [];
      this.sidebarRouters = [];
      this.routeLoaded = false;
      this.routerLoadFailed = false;
    },
    async generateRoutes() {
      let routers: RouterVo[] | null = null;

      try {
        routers = await getRouters();
        // 成功后缓存到 localStorage
        try {
          localStorage.setItem(ROUTER_CACHE_KEY, JSON.stringify(routers));
        } catch {
          // 存储失败不影响正常流程
        }
        this.routerLoadFailed = false;
      } catch (err) {
        console.error("[permission] getRouters 失败，尝试使用本地缓存或最小菜单兜底", err);
        this.routerLoadFailed = true;

        // 优先使用上次成功的路由缓存
        try {
          const cached = localStorage.getItem(ROUTER_CACHE_KEY);
          if (cached) {
            routers = JSON.parse(cached) as RouterVo[];
            console.warn("[permission] 使用本地缓存路由兜底");
          }
        } catch {
          routers = null;
        }

        // 无缓存时使用平台最小可用菜单
        if (!routers || !Array.isArray(routers) || routers.length === 0) {
          routers = buildMinimalFallbackRouters();
          console.warn("[permission] 无路由缓存，使用最小菜单兜底（首页+工作台）");
        }
      }

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
      this.routes = rewriteRoutes;
      this.defaultRoutes = sidebarRoutes;
      this.topbarRouters = sidebarRoutes;

      this.routeLoaded = true;
      return rewriteRoutes;
    },
    registerRoutes(router: Router) {
      // 收集已注册路由的 path 集合，防止动态路由与静态路由产生 path 冲突
      const existingPaths = new Set(router.getRoutes().map((r) => r.path));

      for (const route of this.addRoutes) {
        const name = route.name;
        if (typeof name === "string" && router.hasRoute(name)) {
          continue;
        }
        if (route.path && existingPaths.has(route.path)) {
          // path 冲突：动态路由与已注册路由的 path 相同，跳过注册并输出警告
          console.warn(
            `[route-conflict] 动态路由 path 与已注册路由冲突，已跳过注册: path="${route.path}" name="${String(name)}"`
          );
          continue;
        }
        router.addRoute(route);
        if (route.path) {
          existingPaths.add(route.path);
        }
      }
    }
  }
});
