import type { Directive } from "vue";
import { useUserStore } from "@/stores/user";

export const hasPermi: Directive<HTMLElement, string[]> = {
  mounted(el, binding) {
    const userStore = useUserStore();
    const required = binding.value ?? [];
    const ALL_PERMISSION = "*:*:*";
    const has = userStore.permissions.some(
      (p: string) => p === ALL_PERMISSION || required.includes(p)
    );
    if (!has) {
      el.parentNode?.removeChild(el);
    }
  }
};

export const hasRole: Directive<HTMLElement, string[]> = {
  mounted(el, binding) {
    const userStore = useUserStore();
    const required = binding.value ?? [];
    const requiredLower = required.map((r: string) => r.toLowerCase());
    const SUPER_ADMIN = "admin";
    const SUPER_ADMIN_2 = "superadmin";

    const has = userStore.roles.some((role: string) => {
      const roleLower = role.toLowerCase();
      return roleLower === SUPER_ADMIN || roleLower === SUPER_ADMIN_2 || requiredLower.includes(roleLower);
    });

    if (!has) {
      el.parentNode?.removeChild(el);
    }
  }
};
