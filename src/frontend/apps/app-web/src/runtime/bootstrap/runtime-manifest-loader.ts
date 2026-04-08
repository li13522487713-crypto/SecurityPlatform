/**
 * 运行时 Manifest 加载器：拉取已发布 schema + menu，组装 RuntimeManifest。
 */

import { getRuntimeMenu, getRuntimePageSchema } from "@/services/api-runtime";
import type { RuntimeManifest } from "../release/runtime-release-types";

export async function loadRuntimeManifest(
  appKey: string,
  pageKey: string,
): Promise<RuntimeManifest> {
  const [runtimeSchema, runtimeMenu] = await Promise.all([
    getRuntimePageSchema(pageKey, appKey),
    getRuntimeMenu(appKey),
  ]);

  const matchedPage = runtimeMenu.items.find((item) => item.pageKey === pageKey);

  return {
    appKey,
    pageKey,
    schemaJson: runtimeSchema.schemaJson,
    pageTitle: matchedPage?.title ?? `${appKey} / ${pageKey}`,
    pageType: runtimeSchema.mode,
    releaseId: undefined,
    releaseVersion: runtimeSchema.version,
    menu: runtimeMenu.items,
  };
}
