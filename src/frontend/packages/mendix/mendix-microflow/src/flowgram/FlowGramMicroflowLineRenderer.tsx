import type { LineRenderProps } from "@flowgram-adapter/free-layout-editor";

import type { FlowGramMicroflowEdgeData } from "./FlowGramMicroflowTypes";

function edgeDataFromLine(line: LineRenderProps["line"]): FlowGramMicroflowEdgeData | undefined {
  const maybeLine = line as unknown as {
    data?: FlowGramMicroflowEdgeData;
    info?: { data?: FlowGramMicroflowEdgeData };
    toJSON?: () => { data?: FlowGramMicroflowEdgeData };
  };
  const data = maybeLine.data ?? maybeLine.info?.data ?? maybeLine.toJSON?.().data;
  return typeof data?.flowId === "string" ? data : undefined;
}

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
  const firstCase = data.caseValues[0];
  if (!firstCase) {
    return undefined;
  }
  switch (firstCase.kind) {
    case "boolean":
      return String(firstCase.value);
    case "fallback":
      return "default";
    case "enumeration":
      return firstCase.value;
    case "inheritance":
      return firstCase.entityQualifiedName.split(".").at(-1) ?? firstCase.entityQualifiedName;
    case "empty":
      return "empty";
    case "noCase":
      return undefined;
    default:
      return undefined;
  }
}

export function FlowGramMicroflowLineRenderer({ line }: LineRenderProps) {
  const data = edgeDataFromLine(line);
  if (!data) {
    return null;
  }
  const label = lineLabelFromEdgeData(data);
  return (
    <span
      className={lineClassNameFromEdgeData(data)}
      data-testid="microflow-flowgram-line-label"
      data-flow-id={data.flowId}
      data-edge-kind={data.edgeKind}
      data-runtime-state={data.runtimeState ?? "idle"}
      data-validation-state={data.validationState}
    >
      {label}
    </span>
  );
}
