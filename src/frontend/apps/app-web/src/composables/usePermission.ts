import { useAppUserStore } from "@/stores/user";

export function usePermission() {
  const userStore = useAppUserStore();

  function isPrivilegedUser(): boolean {
    if (userStore.profile?.isPlatformAdmin) return true;
    if (userStore.permissions.includes("*:*:*")) return true;
    return userStore.roles.some((role) =>
      ["admin", "superadmin"].includes(role.toLowerCase())
    );
  }

  function hasPermission(code?: string): boolean {
    if (!code) return true;
    if (isPrivilegedUser()) return true;
    return userStore.permissions.includes(code);
  }

  return { hasPermission, isPrivilegedUser };
}
