import type { MicroflowTraceFrame } from "../debug/trace-types";
import type {
  MicroflowNodeInlineConfig,
  MicroflowNodeViewMode,
} from "../flowgram/FlowGramMicroflowTypes";
import type {
  MicroflowDesignSchema,
  MicroflowValidationIssue,
  MicroflowWorkflowNodeJSON,
} from "../schema/types";
import { deriveRuntimeInlineState } from "./inline-runtime";
import { nodeIssues } from "./inline-validation";

export interface DeriveNodeInlineInput {
  node: MicroflowWorkflowNodeJSON;
  schema: MicroflowDesignSchema;
  runtimeFrame?: MicroflowTraceFrame;
  issues?: MicroflowValidationIssue[];
  viewMode?: MicroflowNodeViewMode;
}

export function createDefaultInlineConfig(input: DeriveNodeInlineInput): MicroflowNodeInlineConfig {
  const localIssues = nodeIssues(input.issues, input.node.id);
  const runtime = deriveRuntimeInlineState(input.runtimeFrame, input.issues, input.node.id);
  return {
    viewMode: input.viewMode ?? "compact",
    summaryLines: [{
      id: "title",
      value: String((input.node.data as { title?: unknown } | undefined)?.title ?? input.node.type),
      kind: "text",
    }],
    sections: [
      {
        id: "advanced",
        title: "高级",
        kind: "advanced",
        collapsed: true,
        fields: [],
      },
      {
        id: "errors",
        title: "错误",
        kind: "errors",
        collapsed: false,
        fields: localIssues.slice(0, 3).map(issue => ({
          id: issue.id,
          label: issue.code,
          value: issue.message,
          fieldPath: "",
          editType: "text",
          readonly: true,
          invalid: issue.severity === "error",
          errorMessage: issue.severity === "error" ? issue.message : undefined,
        })),
      },
    ],
    runtime,
  };
}
