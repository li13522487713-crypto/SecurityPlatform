import { applyEditorGraphPatchToAuthoring } from "../adapters/authoring-operations";
import type { MicroflowFlow, MicroflowSchema } from "../schema/types";
import { collectFlowsRecursive } from "../schema/utils/object-utils";
import { boundsFromLayoutPatch, createBusinessAutoLayoutPatch } from "./auto-layout-engine";
import type { MicroflowAutoLayoutInput, MicroflowAutoLayoutResult } from "./auto-layout-types";

export function applyAutoLayout(input: MicroflowAutoLayoutInput): MicroflowAutoLayoutResult {
  const beforeHash = getFlowSemanticHashForSchema(input.schema);
  const patch = createBusinessAutoLayoutPatch(input);
  const nextSchema = applyEditorGraphPatchToAuthoring(input.schema, patch) as MicroflowSchema;
  const afterHash = getFlowSemanticHashForSchema(nextSchema);
  if (beforeHash !== afterHash) {
    throw new Error("AutoLayout must not change microflow semantic flow fields.");
  }
  return {
    nextSchema,
    patch,
    changedObjectIds: (patch.movedNodes ?? []).map(node => node.objectId),
    bounds: boundsFromLayoutPatch(input.schema, patch),
  };
}

export function getFlowSemanticHash(flow: MicroflowFlow): string {
  return JSON.stringify({
    id: flow.id,
    kind: flow.kind,
    originObjectId: flow.originObjectId,
    destinationObjectId: flow.destinationObjectId,
    originConnectionIndex: flow.originConnectionIndex ?? 0,
    destinationConnectionIndex: flow.destinationConnectionIndex ?? 0,
    edgeKind: flow.kind === "annotation" ? "annotation" : flow.editor.edgeKind,
    caseValues: flow.caseValues ?? [],
    isErrorHandler: flow.kind === "sequence" ? flow.isErrorHandler : false,
  });
}

export function getFlowSemanticHashForSchema(schema: MicroflowSchema): string {
  return collectFlowsRecursive(schema)
    .map(getFlowSemanticHash)
    .sort()
    .join("|");
}
