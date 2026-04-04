import { defineStore } from "pinia";
import type { RouteRecordRaw, Router } from "vue-router";
import type { RouterVo } from "@/types/api";
import { getRouters } from "@/services/api";
import { buildRoutesFromRouters } from "@/utils/dynamic-router";

const ROUTER_CACHE_KEY = "atlas_platform_routers_cache";
let generateRoutesInflight: Promise<RouteRecordRaw[]> | null = null;

function normalizeRouters(nodes: RouterVo[] | null | undefined): RouterVo[] {
  if (!Array.isArray(nodes)) {
    return [];
  }

  return nodes.map((node) => ({
    ...node,
    children: normalizeRouters(node.children),
  }));
}

function buildMinimalFallbackRouters(): RouterVo[] {
  return [
    {
      name: "首页",
      path: "/",
      component: "Layout",
      meta: { title: "首页", titleKey: "route.home", icon: "home" },
      children: [
        {
          name: "工作台",
          path: "index",
          component: "IndexPage",
          meta: { title: "工作台", titleKey: "route.workspace", icon: "home" },
        },
      ],
    },
  ] as RouterVo[];
}

interface PlatformPermissionState {
  routes: RouteRecordRaw[];
  addRoutes: RouteRecordRaw[];
  defaultRoutes: RouteRecordRaw[];
  topbarRouters: RouteRecordRaw[];
  sidebarRouters: RouterVo[];
  routeLoaded: boolean;
  routerLoadFailed: boolean;
}

function buildRouteState(routers: RouterVo[]) {
  const normalized = normalizeRouters(routers);
  const sidebarRoutes = buildRoutesFromRouters(normalized, false, false);
  const rewriteRoutes = buildRoutesFromRouters(normalized, false, true);

  return {
    sidebarRouters: normalized,
    addRoutes: rewriteRoutes,
    routes: rewriteRoutes,
    defaultRoutes: sidebarRoutes,
    topbarRouters: sidebarRoutes,
  };
}

export const usePlatformPermissionStore = defineStore("platformPermission", {
  state: (): PlatformPermissionState => ({
    routes: [],
    addRoutes: [],
    defaultRoutes: [],
    topbarRouters: [],
    sidebarRouters: [],
    routeLoaded: false,
    routerLoadFailed: false,
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
      generateRoutesInflight = null;
    },
    hydrateFromLocalCache() {
      let routers: RouterVo[] | null = null;

      try {
        const cached = localStorage.getItem(ROUTER_CACHE_KEY);
        if (cached) {
          routers = JSON.parse(cached) as RouterVo[];
        }
      } catch {
        routers = null;
      }

      if (!routers || !Array.isArray(routers) || routers.length === 0) {
        routers = buildMinimalFallbackRouters();
      }

      const routeState = buildRouteState(routers);
      this.sidebarRouters = routeState.sidebarRouters;
      this.addRoutes = routeState.addRoutes;
      this.routes = routeState.routes;
      this.defaultRoutes = routeState.defaultRoutes;
      this.topbarRouters = routeState.topbarRouters;
      this.routeLoaded = true;
    },
    async refreshRoutes(router?: Router) {
      if (generateRoutesInflight) {
        return await generateRoutesInflight;
      }

      generateRoutesInflight = (async () => {
        try {
          const routers = await getRouters();
          try {
            localStorage.setItem(ROUTER_CACHE_KEY, JSON.stringify(routers));
          } catch {
            // 忽略缓存写入失败
          }
          const routeState = buildRouteState(routers);
          this.sidebarRouters = routeState.sidebarRouters;
          this.addRoutes = routeState.addRoutes;
          this.routes = routeState.routes;
          this.defaultRoutes = routeState.defaultRoutes;
          this.topbarRouters = routeState.topbarRouters;
          this.routeLoaded = true;
          this.routerLoadFailed = false;
          if (router) {
            this.registerRoutes(router);
          }
          return routeState.addRoutes;
        } catch (err) {
          console.error("[platformPermission] getRouters 失败，保留当前缓存路由", err);
          this.routerLoadFailed = true;
          if (!this.routeLoaded) {
            this.hydrateFromLocalCache();
            if (router) {
              this.registerRoutes(router);
            }
          }
          return this.addRoutes;
        }
      })();

      try {
        return await generateRoutesInflight;
      } finally {
        generateRoutesInflight = null;
      }
    },
    async generateRoutes(router?: Router) {
      return await this.refreshRoutes(router);
    },
    registerRoutes(router: Router) {
      const existingPaths = new Set(router.getRoutes().map((r) => r.path));

      for (const route of this.addRoutes) {
        const name = route.name;
        if (typeof name === "string" && router.hasRoute(name)) {
          continue;
        }
        if (route.path && existingPaths.has(route.path)) {
          continue;
        }
        router.addRoute(route);
        if (route.path) {
          existingPaths.add(route.path);
        }
      }
    },
  },
});
