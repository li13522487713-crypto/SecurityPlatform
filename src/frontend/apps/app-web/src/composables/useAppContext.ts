import { useAppContext as useSharedAppContext } from "@atlas/shared-core/composables";
import { getLowCodeAppByKey } from "@/services/api-lowcode-runtime";

export function useAppContext() {
  return useSharedAppContext(async (key) => {
    const detail = await getLowCodeAppByKey(key);
    return detail.id;
  });
}
