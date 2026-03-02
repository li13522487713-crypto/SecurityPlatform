import type { RouteRecordRaw } from "vue-router";
import type { RouterVo } from "@/types/api";

const pageModules = import.meta.glob("../pages/**/*.vue");

function resolveComponent(component?: string, path?: string) {
  if (!component) {
    if (path === "/") {
      return pageModules["../pages/HomePage.vue"];
    }
    return () => import("@/pages/NotFoundPage.vue");
  }

  if (component === "Layout" || component === "ParentView") {
    return () => import("@/components/layout/RouterContainer.vue");
  }

  const candidates = [
    `../pages/${component}.vue`,
    `../pages/${component}/index.vue`
  ];
  for (const c of candidates) {
    if (pageModules[c]) return pageModules[c];
  }

  return () => import("@/pages/NotFoundPage.vue");
}

export function buildRoutesFromRouters(
  routers: RouterVo[],
  lastRouter: RouterVo | false = false,
  type = false
): RouteRecordRaw[] {
  return routers
    .filter((item) => {
      if (type && item.children) {
        item.children = filterChildren(item.children, item);
      }
      return item.path && item.name;
    })
    .map((item) => toRouteRecord(item, type))
    .filter((item): item is RouteRecordRaw => !!item);
}

function filterChildren(childrenMap: RouterVo[], lastRouter: RouterVo | false = false): RouterVo[] {
  let children: RouterVo[] = [];
  childrenMap.forEach((el) => {
    if (el.children && el.children.length) {
      if (el.component === "ParentView") {
        el.children.forEach((c) => {
          c.path = el.path + "/" + c.path;
          if (c.children && c.children.length) {
            children = children.concat(filterChildren(c.children, c));
            return;
          }
          children.push(c);
        });
        return;
      }
    }
    if (lastRouter) {
      el.path = lastRouter.path + "/" + el.path;
    }
    children = children.concat(el);
  });
  return children;
}

function toRouteRecord(item: RouterVo, type: boolean): RouteRecordRaw | null {
  const route: RouteRecordRaw = {
    path: item.path,
    name: item.name,
    component: resolveComponent(item.component, item.path),
    meta: {
      title: item.meta?.title ?? item.name,
      icon: item.meta?.icon,
      requiresAuth: true,
      requiresPermission: item.meta?.permi,
      hidden: item.hidden,
      noCache: item.meta?.noCache,
      breadcrumb: item.meta?.link ? false : true
    }
  };

  if (item.redirect) {
    route.redirect = item.redirect;
  }

  if (item.children && item.children.length > 0) {
    route.children = buildRoutesFromRouters(item.children, item, type);
  } else {
    // 叶子节点删除不必要的属性
    delete route.children;
  }

  return route;
}
