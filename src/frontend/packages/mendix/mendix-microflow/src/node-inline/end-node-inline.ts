import type { MicroflowNodeInlineConfig } from "../flowgram/FlowGramMicroflowTypes";
import { createDefaultInlineConfig, type DeriveNodeInlineInput } from "./default-node-inline";

function readReturnValue(node: DeriveNodeInlineInput["node"]): string | undefined {
  const returnValue = (node.data as { returnValue?: unknown } | undefined)?.returnValue;
  if (typeof returnValue === "string") {
    return returnValue.trim() || undefined;
  }
  if (returnValue && typeof returnValue === "object") {
    const raw = (returnValue as { raw?: unknown; text?: unknown }).raw ?? (returnValue as { text?: unknown }).text;
    return typeof raw === "string" && raw.trim() ? raw.trim() : undefined;
  }
  return undefined;
}

export function deriveEndNodeInline(input: DeriveNodeInlineInput): MicroflowNodeInlineConfig {
  const base = createDefaultInlineConfig(input);
  const returnValue = readReturnValue(input.node);
  const summaryLines: MicroflowNodeInlineConfig["summaryLines"] = [
    ...(returnValue ? [{ id: "return", label: "return", value: returnValue, kind: "output" as const, fieldPath: "data.returnValue.raw", editable: true }] : []),
    { id: "status", value: "status: success", kind: "text" },
    ...(base.runtime?.outputPreview ? [{ id: "out", label: "output", value: base.runtime.outputPreview, kind: "runtime" as const }] : []),
  ];
  return {
    ...base,
    summaryLines: summaryLines.slice(0, 3),
    sections: base.sections.filter(section => section.kind === "errors"),
  };
}
