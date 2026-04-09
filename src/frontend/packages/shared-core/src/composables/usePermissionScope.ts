import { computed } from "vue";
import type { Ref } from "vue";

export interface PermissionScopeContext {
  permissions: string[];
  isPlatformAdmin?: boolean;
}

export function usePermissionScope(
  context: Ref<PermissionScopeContext> | PermissionScopeContext
) {
  const isRefInput = (
    input: Ref<PermissionScopeContext> | PermissionScopeContext
  ): input is Ref<PermissionScopeContext> => {
    return typeof input === "object" && input !== null && "value" in input;
  };

  const scopeRef = computed<PermissionScopeContext>(() => {
    if (isRefInput(context)) {
      return context.value;
    }
    return context;
  });

  const hasPermission = (permissionCode: string) => computed(() => {
    if (scopeRef.value.isPlatformAdmin) return true;
    return scopeRef.value.permissions.includes(permissionCode);
  });

  const hasAnyPermission = (permissionCodes: string[]) => computed(() => {
    if (scopeRef.value.isPlatformAdmin) return true;
    return permissionCodes.some((code) => scopeRef.value.permissions.includes(code));
  });

  return {
    hasPermission,
    hasAnyPermission
  };
}
