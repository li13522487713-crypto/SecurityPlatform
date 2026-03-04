import { createRouter, createWebHistory } from "vue-router";
import { getAccessToken, getTenantId } from "@/utils/auth";
import { useUserStore } from "@/stores/user";
import { usePermissionStore } from "@/stores/permission";
import { message } from "ant-design-vue";
import NProgress from "nprogress";
import "nprogress/nprogress.css";

NProgress.configure({ showSpinner: false });

const LoginPage = () => import("@/pages/LoginPage.vue");
const RegisterPage = () => import("@/pages/RegisterPage.vue");
const ProfilePage = () => import("@/pages/ProfilePage.vue");
const NotFoundPage = () => import("@/pages/NotFoundPage.vue");
const AppListPage = () => import("@/pages/lowcode/AppListPage.vue");
const AppBuilderPage = () => import("@/pages/lowcode/AppBuilderPage.vue");
const FormListPage = () => import("@/pages/lowcode/FormListPage.vue");
const FormDesignerPage = () => import("@/pages/lowcode/FormDesignerPage.vue");

declare module "vue-router" {
  interface RouteMeta {
    requiresAuth?: boolean;
    requiresPermission?: string;
    title?: string;
  }
}

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: "/login", name: "login", component: LoginPage, meta: { title: "登录" } },
    { path: "/register", name: "register", component: RegisterPage, meta: { title: "注册" } },
    { path: "/profile", name: "profile", component: ProfilePage, meta: { requiresAuth: true, title: "个人中心" } },
    { path: "/lowcode/apps", name: "app-list", component: AppListPage, meta: { requiresAuth: true, title: "低代码应用", requiresPermission: "apps:view" } },
    { path: "/lowcode/apps/:id/builder", name: "app-builder", component: AppBuilderPage, meta: { requiresAuth: true, title: "应用设计器", requiresPermission: "apps:update" } },
    { path: "/lowcode/forms", name: "apps-form-list", component: FormListPage, meta: { requiresAuth: true, title: "表单管理", requiresPermission: "apps:view" } },
    { path: "/lowcode/forms/:id/designer", name: "apps-form-designer", component: FormDesignerPage, meta: { requiresAuth: true, title: "表单设计器", requiresPermission: "apps:update" } },
    { path: "/:pathMatch(.*)*", name: "not-found", component: NotFoundPage }
  ]
});

const whiteList = ["/login", "/register"];

router.beforeEach(async (to, from, next) => {
  NProgress.start();
  if (to.meta.title) {
    document.title = `${to.meta.title} - Atlas Security Platform`;
  }

  const token = getAccessToken();
  const tenantId = getTenantId();
  const userStore = useUserStore();
  const permissionStore = usePermissionStore();

  if (token && tenantId) {
    if (to.path === "/login") {
      next({ path: "/" });
      NProgress.done();
      return;
    }

    if (!permissionStore.routeLoaded || userStore.roles.length === 0) {
      try {
        await userStore.getInfo();
        await permissionStore.generateRoutes();
        permissionStore.registerRoutes(router);
        next({ ...to, replace: true });
        return;
      } catch (err) {
        console.error(err);
        await userStore.logout();
        message.error((err as Error)?.message || "登录失败，请重新登录");
        next({ path: "/login" });
        NProgress.done();
        return;
      }
    }

    if (to.meta.requiresPermission && typeof to.meta.requiresPermission === "string") {
      const has = userStore.permissions.includes(to.meta.requiresPermission)
        || userStore.permissions.includes("*:*:*")
        || userStore.roles.some((role: string) => ["admin", "superadmin"].includes(role.toLowerCase()));
      if (!has) {
        next({ path: "/" });
        NProgress.done();
        return;
      }
    }

    next();
  } else {
    if (whiteList.includes(to.path)) {
      next();
    } else {
      next(`/login?redirect=${encodeURIComponent(to.fullPath)}`);
      NProgress.done();
    }
  }
});

router.afterEach(() => {
  NProgress.done();
});

export default router;
