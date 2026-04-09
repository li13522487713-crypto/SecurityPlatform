import { buildRuntimeRecordsUrl } from "@/services/api-runtime";
import type { AmisSchema } from "@/types/amis";
import type { DataBinding, SchemaBindingMap } from "./binding-types";
import { resolveBindings as resolveBindingsCore } from "@atlas/runtime-core";

export function resolveBindings(
  schema: AmisSchema,
  pageKey: string,
  appKey: string,
): SchemaBindingMap {
  const result = resolveBindingsCore(schema, pageKey, appKey, {
    buildRuntimeRecordsUrl,
  }) as { bindings: DataBinding[] };
  return {
    bindings: result.bindings,
  };
}
