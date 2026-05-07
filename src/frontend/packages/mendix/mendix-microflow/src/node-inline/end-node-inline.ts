import type { MicroflowNodeInlineConfig } from "../flowgram/FlowGramMicroflowTypes";
import { createDefaultInlineConfig, type DeriveNodeInlineInput } from "./default-node-inline";

export function deriveEndNodeInline(input: DeriveNodeInlineInput): MicroflowNodeInlineConfig {
  const base = createDefaultInlineConfig(input);
  const summaryLines: MicroflowNodeInlineConfig["summaryLines"] = [
    { id: "status", value: "status: success", kind: "text" },
    ...(base.runtime?.outputPreview ? [{ id: "out", label: "output", value: base.runtime.outputPreview, kind: "runtime" as const }] : []),
  ];
  return {
    ...base,
    summaryLines: summaryLines.slice(0, 3),
    sections: base.sections.filter(section => section.kind === "errors"),
  };
}
