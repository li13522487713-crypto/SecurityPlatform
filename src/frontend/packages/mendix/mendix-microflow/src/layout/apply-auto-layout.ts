import { applyEditorGraphPatchToAuthoring } from "../adapters/authoring-operations";
import type { MicroflowSchema } from "../schema/types";
import { boundsFromLayoutPatch, createBusinessAutoLayoutPatch } from "./auto-layout-engine";
import type { MicroflowAutoLayoutInput, MicroflowAutoLayoutResult } from "./auto-layout-types";

export function applyAutoLayout(input: MicroflowAutoLayoutInput): MicroflowAutoLayoutResult {
  const patch = createBusinessAutoLayoutPatch(input);
  const nextSchema = applyEditorGraphPatchToAuthoring(input.schema, patch) as MicroflowSchema;
  return {
    nextSchema,
    patch,
    changedObjectIds: (patch.movedNodes ?? []).map(node => node.objectId),
    bounds: boundsFromLayoutPatch(input.schema, patch),
  };
}
