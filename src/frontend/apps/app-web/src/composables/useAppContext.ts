import { useAppContext as useSharedAppContext } from "@atlas/shared-core/composables";
import { getAppInstanceIdByAppKey, getLowCodeAppByKey } from "@/services/api-lowcode-runtime";

async function sleep(ms: number): Promise<void> {
  await new Promise((resolve) => {
    window.setTimeout(resolve, ms);
  });
}

export function useAppContext() {
  return useSharedAppContext(async (key) => {
    const deadline = Date.now() + 30_000;
    let lastError: unknown = null;

    while (Date.now() < deadline) {
      try {
        const appInstanceId = await getAppInstanceIdByAppKey(key);
        if (appInstanceId) {
          return appInstanceId;
        }

        const detail = await getLowCodeAppByKey(key);
        if (detail.id) {
          return detail.id;
        }
      } catch (error) {
        lastError = error;
      }

      await sleep(500);
    }

    if (lastError instanceof Error) {
      throw lastError;
    }

    throw new Error(`应用上下文初始化超时，未能解析 appId: ${key}`);
  });
}
