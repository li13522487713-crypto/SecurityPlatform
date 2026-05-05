import type { MicroflowSchemaNormalizeIssue } from "../schema/normalizer";
import type { MicroflowValidationIssue } from "../schema/types";

export function createNormalizerIssues(
  microflowId: string,
  blockingIssues: MicroflowSchemaNormalizeIssue[],
): MicroflowValidationIssue[] {
  return blockingIssues.map(item => ({
    id: `${microflowId}:normalizer:${item.code}:${item.flowId ?? item.objectId ?? "schema"}:${item.fieldPath ?? "schema"}`,
    microflowId,
    code: item.code,
    severity: item.severity,
    source: "schema",
    message: item.message,
    objectId: item.objectId,
    flowId: item.flowId,
    edgeId: item.flowId,
    fieldPath: item.fieldPath ?? (item.flowId ? `flows.${item.flowId}` : "objectCollection"),
    blockSave: true,
    blockPublish: true,
  }));
}
