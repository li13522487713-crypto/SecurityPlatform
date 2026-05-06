import { useCallback, useState } from "react";

import { Button, Input, Popover } from "@douyinfe/semi-ui";
import { IconClose } from "@douyinfe/semi-icons";
import {
  type LineRenderProps,
  usePlaygroundReadonlyState,
} from "@flowgram-adapter/free-layout-editor";

import type { MicroflowAuthoringSchema, MicroflowCaseValue } from "../schema";
import { updateFlow } from "../property-panel/utils/schema-patch";
import type { FlowGramMicroflowEdgeData } from "./FlowGramMicroflowTypes";
import { useFlowGramMicroflowContext } from "./inline/useFlowGramMicroflowContext";

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
  const [hovered, setHovered] = useState(false);
  const [editingExpression, setEditingExpression] = useState(false);
  const [expressionValue, setExpressionValue] = useState("");
  const readonly = usePlaygroundReadonlyState();
  const ctx = useFlowGramMicroflowContext();

  const data = edgeDataFromLine(line);
  if (!data) {
    return null;
  }

  const firstCase = data.caseValues[0];
  const isExpressionCase = firstCase?.kind === "expression";
  const label = lineLabelFromEdgeData(data);

  const handleExpressionEdit = useCallback(() => {
    setExpressionValue((firstCase as any)?.expression ?? "");
    setEditingExpression(true);
  }, [firstCase]);

  const handleExpressionSave = useCallback(() => {
    if (!isExpressionCase || !ctx) {
      setEditingExpression(false);
      return;
    }

    const { schema, onSchemaChange } = ctx;
    const updatedCaseValue: MicroflowCaseValue = {
      ...(firstCase as Extract<MicroflowCaseValue, { kind: "expression" }>),
      expression: expressionValue,
    };
    // cast DesignSchema → AuthoringSchema for updateFlow (shares flow data at runtime)
    const nextSchema = updateFlow(schema as unknown as MicroflowAuthoringSchema, data.flowId, flow => ({
      ...flow,
      caseValues: [updatedCaseValue, ...(flow.caseValues?.slice(1) ?? [])],
    } as typeof flow));
    onSchemaChange(nextSchema as unknown as typeof schema, "inlineExpressionEdit");
    setEditingExpression(false);
  }, [isExpressionCase, ctx, firstCase, expressionValue, data]);

  const expressionLabel = isExpressionCase ? (
    readonly ? (
      <span>{label || "expression"}</span>
    ) : (
      <Popover
        content={
          <div
            style={{ width: 200, padding: "8px" }}
            onClick={e => e.stopPropagation()}
            draggable={false}
          >
            <Input
              value={expressionValue}
              onChange={setExpressionValue}
              placeholder="输入条件表达式"
              size="small"
              style={{ marginBottom: 8 }}
            />
            <div style={{ display: "flex", gap: 6 }}>
              <Button size="small" onClick={() => setEditingExpression(false)}>
                取消
              </Button>
              <Button size="small" type="primary" onClick={handleExpressionSave}>
                保存
              </Button>
            </div>
          </div>
        }
        visible={editingExpression}
        onVisibleChange={setEditingExpression}
        trigger="custom"
        position="top"
      >
        <span
          style={{ cursor: "pointer", textDecoration: "underline" }}
          onClick={handleExpressionEdit}
        >
          {label || "expression"}
        </span>
      </Popover>
    )
  ) : (
    label
  );

  return (
    <span
      className={lineClassNameFromEdgeData(data)}
      data-testid="microflow-flowgram-line-label"
      data-flow-id={data.flowId}
      data-edge-kind={data.edgeKind}
      data-runtime-state={data.runtimeState ?? "idle"}
      data-validation-state={data.validationState}
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
    >
      {expressionLabel}
      {hovered && !readonly ? (
        <Button
          icon={<IconClose />}
          size="small"
          type="danger"
          theme="borderless"
          className="microflow-flowgram-line__delete-btn"
          aria-label="删除连线"
          onClick={e => {
            e.stopPropagation();
            (line as unknown as { dispose?: () => void }).dispose?.();
          }}
        />
      ) : null}
    </span>
  );
}
