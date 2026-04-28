import { useMemo, useState, type ReactNode } from "react";
import { Button, Input, InputNumber, Select, Space, Toast, Typography } from "@douyinfe/semi-ui";
import { evaluateExpression } from "@atlas/mendix-expression";
import type {
  ExecuteActionRequest,
  ExecuteActionResponse,
  FlowExecutionTraceSchema,
  LowCodeAppSchema,
  RuntimeUiCommand,
  WidgetSchema
} from "@atlas/mendix-schema";

const { Text } = Typography;

export type RuntimeContextValue = {
  app: LowCodeAppSchema;
  pageId: string;
  objectState: Record<string, unknown>;
};

export interface RuntimeExecutor {
  executeAction(request: ExecuteActionRequest, context: RuntimeContextValue): ExecuteActionResponse;
  getTrace(traceId: string): FlowExecutionTraceSchema | undefined;
}

function findMicroflow(app: LowCodeAppSchema, microflowId: string) {
  return app.modules.flatMap(module => module.microflows).find(microflow => microflow.microflowId === microflowId);
}

function createTrace(
  flowId: string,
  inputArguments: Record<string, unknown>,
  steps: FlowExecutionTraceSchema["steps"],
  status: FlowExecutionTraceSchema["status"]
): FlowExecutionTraceSchema {
  const startedAt = new Date().toISOString();
  return {
    traceId: `trace_${Date.now()}_${Math.random().toString(36).slice(2, 8)}`,
    flowType: "microflow",
    flowId,
    startedAt,
    endedAt: new Date().toISOString(),
    status,
    inputArguments,
    steps
  };
}

export function createRuntimeExecutor(): RuntimeExecutor {
  const traces = new Map<string, FlowExecutionTraceSchema>();
  return {
    executeAction(request, context) {
      if (request.actionType === "showMessage") {
        return {
          success: true,
          uiCommands: [{ type: "showMessage", level: "info", message: request.message ?? "消息" }]
        };
      }

      if (request.actionType === "callWorkflow") {
        const trace = createTrace(
          request.workflowRef?.id ?? "workflow",
          Object.fromEntries((request.arguments ?? []).map(arg => [arg.name, arg.value])),
          [
            {
              stepId: "wf_start",
              nodeId: "start",
              nodeType: "startEvent",
              expressionResults: [],
              permissionChecks: [],
              databaseQueries: [],
              uiCommands: [{ type: "openTaskPage", workflowTaskId: "mock_task_1" }],
              inputSnapshot: context.objectState,
              outputSnapshot: context.objectState
            }
          ],
          "succeeded"
        );
        traces.set(trace.traceId, trace);
        return {
          success: true,
          traceId: trace.traceId,
          uiCommands: [
            { type: "showMessage", level: "info", message: "工作流已触发" },
            { type: "openTaskPage", workflowTaskId: "mock_task_1" }
          ]
        };
      }

      const microflow = request.microflowRef ? findMicroflow(context.app, request.microflowRef.id) : undefined;
      if (!microflow) {
        return {
          success: false,
          uiCommands: [{ type: "showMessage", level: "error", message: "未找到 Microflow" }]
        };
      }

      const amount = Number(context.objectState.Amount ?? 0);
      const steps: FlowExecutionTraceSchema["steps"] = [];
      const commands: RuntimeUiCommand[] = [];
      if (microflow.name === "MF_SubmitPurchaseRequest") {
        steps.push({
          stepId: "step_read_amount",
          nodeId: "retrieve_amount",
          nodeType: "retrieveObject",
          expressionResults: [],
          permissionChecks: [{ check: "applyEntityAccess", allowed: true }],
          databaseQueries: ["mock: select Request.Amount"],
          uiCommands: [],
          inputSnapshot: { ...context.objectState },
          outputSnapshot: { ...context.objectState }
        });

        if (amount <= 0) {
          commands.push({
            type: "validationFeedback",
            targetPath: "Amount",
            message: "金额必须大于 0"
          });
          commands.push({ type: "showMessage", level: "warning", message: "提交失败：金额必须大于 0" });
          const trace = createTrace(microflow.microflowId, { Amount: amount }, steps, "failed");
          traces.set(trace.traceId, trace);
          return {
            success: false,
            traceId: trace.traceId,
            uiCommands: commands
          };
        }

        const status = amount > 50000 ? "NeedFinanceApproval" : "NeedManagerApproval";
        context.objectState.Status = status;
        context.objectState.SubmitTime = new Date().toISOString();

        steps.push({
          stepId: "step_decision_status",
          nodeId: "decision_amount",
          nodeType: "decision",
          expressionResults: [
            {
              expression: "$Request/Amount > 50000",
              result: amount > 50000
            }
          ],
          permissionChecks: [{ check: "microflowAccess", allowed: true }],
          databaseQueries: [],
          uiCommands: [],
          inputSnapshot: { Amount: amount },
          outputSnapshot: { Status: status }
        });

        commands.push({
          type: "showMessage",
          level: "info",
          message: amount > 50000 ? "已提交，进入财务审批" : "已提交，进入主管审批"
        });
        commands.push({ type: "refreshObject", objectPath: "Request" });
      }

      const trace = createTrace(
        microflow.microflowId,
        Object.fromEntries((request.arguments ?? []).map(arg => [arg.name, arg.value])),
        steps,
        "succeeded"
      );
      traces.set(trace.traceId, trace);
      return {
        success: true,
        traceId: trace.traceId,
        returnValue: context.objectState,
        uiCommands: commands
      };
    },
    getTrace(traceId) {
      return traces.get(traceId);
    }
  };
}

type RuntimeRendererProps = {
  app: LowCodeAppSchema;
  pageId: string;
  objectState: Record<string, unknown>;
  onStateChange?: (next: Record<string, unknown>) => void;
  executor: RuntimeExecutor;
  onActionResponse?: (response: ExecuteActionResponse) => void;
};

function resolveBoundValue(widget: WidgetSchema, objectState: Record<string, unknown>) {
  const binding = "fieldBinding" in widget ? widget.fieldBinding : undefined;
  if (binding?.source === "attribute" && binding.attributeRef) {
    return objectState[binding.attributeRef.id] ?? objectState[binding.attributeRef.name ?? ""];
  }
  if (binding?.source === "expression" && binding.expression) {
    return evaluateExpression(binding.expression, { variables: { $Request: objectState } });
  }
  if (binding?.source === "static") {
    return binding.staticValue;
  }
  return undefined;
}

function renderWidgetTree(
  widget: WidgetSchema,
  ctx: {
    state: Record<string, unknown>;
    setState: (updater: (prev: Record<string, unknown>) => Record<string, unknown>) => void;
    runtime: RuntimeContextValue;
    executor: RuntimeExecutor;
    onActionResponse?: (response: ExecuteActionResponse) => void;
  }
): ReactNode {
  if (widget.visibility?.expression) {
    const result = evaluateExpression(widget.visibility.expression, { variables: { $Request: ctx.state } });
    if (!result) {
      return null;
    }
  }

  const children = (
    <>
      {widget.children?.map(child => (
        <div key={child.widgetId}>{renderWidgetTree(child, ctx)}</div>
      ))}
    </>
  );

  if (widget.widgetType === "container" || widget.widgetType === "dataView") {
    return (
      <div style={{ padding: 8, border: "1px solid var(--semi-color-border)", borderRadius: 6 }}>
        {children}
      </div>
    );
  }

  if (widget.widgetType === "label") {
    return <Text>{String(widget.props.text ?? widget.props.caption ?? "Label")}</Text>;
  }

  if (widget.widgetType === "textBox" || widget.widgetType === "textArea") {
    const value = resolveBoundValue(widget, ctx.state);
    const attrId = "fieldBinding" in widget ? widget.fieldBinding?.attributeRef?.id : undefined;
    return (
      <Input
        value={String(value ?? "")}
        onChange={next => {
          if (!attrId) {
            return;
          }
          ctx.setState(prev => ({ ...prev, [attrId]: next }));
        }}
      />
    );
  }

  if (widget.widgetType === "numberInput") {
    const value = resolveBoundValue(widget, ctx.state);
    const attrId = "fieldBinding" in widget ? widget.fieldBinding?.attributeRef?.id : undefined;
    return (
      <InputNumber
        value={typeof value === "number" ? value : Number(value ?? 0)}
        onNumberChange={next => {
          if (!attrId) {
            return;
          }
          ctx.setState(prev => ({ ...prev, [attrId]: Number(next ?? 0) }));
        }}
      />
    );
  }

  if (widget.widgetType === "dropDown") {
    const value = resolveBoundValue(widget, ctx.state);
    const attrId = "fieldBinding" in widget ? widget.fieldBinding?.attributeRef?.id : undefined;
    const options = (widget.props.options as Array<{ label: string; value: string }> | undefined) ?? [];
    return (
      <Select
        value={String(value ?? "")}
        optionList={options}
        onChange={next => {
          if (!attrId) {
            return;
          }
          ctx.setState(prev => ({ ...prev, [attrId]: String(next) }));
        }}
      />
    );
  }

  if (widget.widgetType === "button") {
    return (
      <Button
        onClick={() => {
          if (!widget.action) {
            return;
          }
          const response = ctx.executor.executeAction(widget.action, {
            ...ctx.runtime,
            objectState: ctx.state
          });
          response.uiCommands.forEach(command => {
            if (command.type === "showMessage") {
              if (command.level === "error") {
                Toast.error(command.message);
              } else if (command.level === "warning") {
                Toast.warning(command.message);
              } else {
                Toast.info(command.message);
              }
            }
          });
          ctx.onActionResponse?.(response);
        }}
      >
        {String(widget.props.caption ?? "Button")}
      </Button>
    );
  }

  return <div>{children}</div>;
}

export function RuntimeRenderer({
  app,
  pageId,
  objectState,
  onStateChange,
  executor,
  onActionResponse
}: RuntimeRendererProps) {
  const page = useMemo(
    () => app.modules.flatMap(module => module.pages).find(candidate => candidate.pageId === pageId),
    [app, pageId]
  );
  const [localState, setLocalState] = useState<Record<string, unknown>>(objectState);

  if (!page) {
    return <Text type="danger">未找到页面 {pageId}</Text>;
  }

  return (
    <Space vertical style={{ width: "100%" }}>
      {renderWidgetTree(page.rootWidget, {
        state: localState,
        setState: updater => {
          const next = updater(localState);
          setLocalState(next);
          onStateChange?.(next);
        },
        runtime: { app, pageId, objectState: localState },
        executor,
        onActionResponse
      })}
    </Space>
  );
}
