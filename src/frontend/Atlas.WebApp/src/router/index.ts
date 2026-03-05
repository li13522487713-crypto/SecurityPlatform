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
const ApprovalInstanceDetailPage = () => import("@/pages/ApprovalInstanceDetailPage.vue");
const NotificationsPage = () => import("@/pages/system/NotificationsPage.vue");
const DictTypesPage = () => import("@/pages/system/DictTypesPage.vue");
const SystemConfigsPage = () => import("@/pages/system/SystemConfigsPage.vue");

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
    { path: "/process/instances/:id", name: "process-instance-detail", component: ApprovalInstanceDetailPage, meta: { requiresAuth: true, title: "流程详情", requiresPermission: "approval:flow:view" } },
    { path: "/system/notifications", name: "system-notifications", component: NotificationsPage, meta: { requiresAuth: true, title: "通知中心" } },
    { path: "/notifications", name: "system-notifications-legacy", redirect: "/system/notifications", meta: { requiresAuth: true, title: "通知中心" } },
    { path: "/settings/system/dict-types", name: "settings-system-dict-types", component: DictTypesPage, meta: { requiresAuth: true, title: "字典管理", requiresPermission: "dict:type:view" } },
    { path: "/settings/system/configs", name: "settings-system-configs", component: SystemConfigsPage, meta: { requiresAuth: true, title: "参数配置", requiresPermission: "config:view" } },
    { path: "/system/dict-types", name: "system-dict-types-legacy", redirect: "/settings/system/dict-types", meta: { requiresAuth: true, title: "字典管理" } },
    { path: "/system/configs", name: "system-configs-legacy", redirect: "/settings/system/configs", meta: { requiresAuth: true, title: "参数配置" } },
    { path: "/alerts", name: "alerts-legacy", redirect: "/alert", meta: { requiresAuth: true, title: "告警" } },
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
      next({ path: "/profile" });
      NProgress.done();
      return;
    }

    if (!permissionStore.routeLoaded || !userStore.profile) {
      try {
        await userStore.getInfo();
        await permissionStore.generateRoutes();
        permissionStore.registerRoutes(router);
        // 仅按 URL 重放，避免携带 not-found 名称导致动态路由注册后仍落入 404。
        next({
          path: to.path,
          query: to.query,
          hash: to.hash,
          replace: true
        });
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
        next({ path: "/profile" });
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
