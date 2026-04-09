import { computed } from "vue";
import type { Ref } from "vue";
import { getTenantId } from "../utils/index";

export function useTenantContext(tenantIdRef?: Ref<string | null | undefined>) {
  const tenantId = computed(() => tenantIdRef?.value ?? getTenantId() ?? "");
  const hasTenant = computed(() => tenantId.value.length > 0);

  return {
    tenantId,
    hasTenant
  };
}
