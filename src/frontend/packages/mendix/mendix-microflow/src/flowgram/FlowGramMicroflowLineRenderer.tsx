import type { FlowGramMicroflowEdgeData } from "./FlowGramMicroflowTypes";

export function lineClassNameFromEdgeData(data: FlowGramMicroflowEdgeData): string {
  return [
    "microflow-flowgram-line",
    `microflow-flowgram-line--${data.edgeKind}`,
    data.validationState !== "valid" ? `is-${data.validationState}` : "",
    data.runtimeState && data.runtimeState !== "idle" ? `is-runtime-${data.runtimeState}` : "",
  ].filter(Boolean).join(" ");
}

export function lineLabelFromEdgeData(data: FlowGramMicroflowEdgeData): string | undefined {
  if (data.label) {
    return data.label;
  }
  if (data.edgeKind === "errorHandler") {
    return "Error";
  }
  return undefined;
}

