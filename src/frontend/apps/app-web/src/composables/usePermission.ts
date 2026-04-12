import { useAppUserStore } from "@/stores/user";
import { getAuthProfile, hasPermission as hasPermissionWithProfile, isAdminRole } from "@atlas/shared-core";

const privilegedRoleCodes = new Set(["admin", "superadmin", "securityadmin", "systemadmin"]);

export function usePermission() {
  const userStore = useAppUserStore();

  function resolveProfile() {
    const storedProfile = getAuthProfile();
    if (!storedProfile) {
      return userStore.profile;
    }

    if (!userStore.profile) {
      return storedProfile;
    }

    if (userStore.permissions.length === 0 && (storedProfile.permissions?.length ?? 0) > 0) {
      return {
        ...userStore.profile,
        permissions: storedProfile.permissions
      };
    }

    return userStore.profile;
  }

  function isPrivilegedUser(): boolean {
    const profile = resolveProfile();
    const permissions = profile?.permissions ?? userStore.permissions;
    if (permissions.includes("*:*:*")) return true;
    const roles = profile?.roles ?? userStore.roles;
    if (roles.some((role) => privilegedRoleCodes.has(role.trim().toLowerCase()))) {
      return true;
    }
    return isAdminRole(profile);
  }

  function hasPermission(code?: string): boolean {
    if (!code) return true;
    const profile = resolveProfile();
    if (isPrivilegedUser()) return true;
    return hasPermissionWithProfile(profile, code);
  }

  return { hasPermission, isPrivilegedUser };
}
