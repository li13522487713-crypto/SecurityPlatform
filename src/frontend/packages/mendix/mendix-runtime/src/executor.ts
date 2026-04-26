import type {
  ExecuteActionRequest,
  ExecuteActionResponse,
  FlowExecutionTraceSchema,
  LowCodeAppSchema,
  RuntimeUiCommand
} from "@atlas/mendix-schema";

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
  return {
    traceId: `trace_${Date.now()}_${Math.random().toString(36).slice(2, 8)}`,
    flowType: "microflow",
    flowId,
    startedAt: new Date().toISOString(),
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
          [],
          "succeeded"
        );
        traces.set(trace.traceId, trace);
        return {
          success: true,
          traceId: trace.traceId,
          uiCommands: [{ type: "showMessage", level: "info", message: "工作流已触发" }]
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
        if (amount <= 0) {
          commands.push({
            type: "validationFeedback",
            targetPath: "Amount",
            message: "金额必须大于 0"
          });
          const trace = createTrace(microflow.microflowId, { Amount: amount }, steps, "failed");
          traces.set(trace.traceId, trace);
          return {
            success: false,
            traceId: trace.traceId,
            uiCommands: commands
          };
        }
        context.objectState.Status = amount > 50000 ? "NeedFinanceApproval" : "NeedManagerApproval";
        commands.push({
          type: "showMessage",
          level: "info",
          message: amount > 50000 ? "已提交，进入财务审批" : "已提交，进入主管审批"
        });
        commands.push({ type: "refreshObject", objectPath: "Request" });
      }

      const trace = createTrace(microflow.microflowId, { Amount: amount }, steps, "succeeded");
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
