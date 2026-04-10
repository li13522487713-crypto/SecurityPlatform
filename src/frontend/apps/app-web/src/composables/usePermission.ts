import { useAppUserStore } from "@/stores/user";
import { hasPermission as hasPermissionWithProfile, isAdminRole } from "@atlas/shared-core";

export function usePermission() {
  const userStore = useAppUserStore();

  function isPrivilegedUser(): boolean {
    if (userStore.permissions.includes("*:*:*")) return true;
    return isAdminRole(userStore.profile);
  }

  function hasPermission(code?: string): boolean {
    if (!code) return true;
    if (isPrivilegedUser()) return true;
    return hasPermissionWithProfile(userStore.profile, code);
  }

  return { hasPermission, isPrivilegedUser };
}
