import type { MicroflowNodeInlineConfig } from "../flowgram/FlowGramMicroflowTypes";
import { createDefaultInlineConfig, type DeriveNodeInlineInput } from "./default-node-inline";

export function deriveAnnotationNodeInline(input: DeriveNodeInlineInput): MicroflowNodeInlineConfig {
  const base = createDefaultInlineConfig(input);
  const data = (input.node.data ?? {}) as Record<string, unknown>;
  const rawText = String(data.text ?? data.title ?? "").trim();
  const summaryText = rawText || "点击添加注释";
  return {
    ...base,
    summaryLines: [
      {
        id: "annotation-text",
        value: summaryText,
        kind: "text",
        editable: true,
        fieldPath: "data.text",
      },
    ],
    sections: [
      {
        id: "annotation-content",
        title: "注释",
        kind: "advanced",
        maxVisibleRows: 2,
        fields: [
          {
            id: "annotation-text",
            label: "内容",
            value: rawText,
            fieldPath: "data.text",
            editType: "text",
            placeholder: "输入注释内容",
          },
        ],
      },
      ...base.sections.filter(section => section.kind === "errors"),
    ],
  };
}
