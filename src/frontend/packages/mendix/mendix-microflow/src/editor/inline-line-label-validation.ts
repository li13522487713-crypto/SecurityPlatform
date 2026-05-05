import type { FlowGramMicroflowEdgeData } from "../flowgram/FlowGramMicroflowTypes";
import type { MicroflowWorkflowEdgeJSON, MicroflowWorkflowJSON } from "../schema/types";

const DECISION_BRANCH_LABELS = new Set(["true", "false", "else"]);
const APPROVAL_BRANCH_LABELS = new Set(["approved", "rejected", "timeout"]);
const LOOP_BRANCH_LABELS = new Set(["body", "done", "break", "continue"]);
const ERROR_BRANCH_LABELS = new Set(["error", "fallback", "rethrow"]);

type BranchLabelScope = "decision" | "approval" | "loop" | "error" | "custom";

function normalizeBranchLabel(value: string): string {
  return value.trim().toLowerCase();
}

function edgeBranchLabelScope(edge: MicroflowWorkflowEdgeJSON | undefined): BranchLabelScope {
  if (!edge) {
    return "custom";
  }
  const edgeData = (edge.data ?? {}) as Partial<FlowGramMicroflowEdgeData>;
  const sourcePort = String(edge.sourcePortID ?? edgeData.sourcePortId ?? "").toLowerCase();
  const actionKind = String(edgeData.sourceActionKind ?? "").toLowerCase();
  if (edgeData.edgeKind === "errorHandler" || sourcePort.includes("error") || sourcePort.includes("fallback") || sourcePort.includes("rethrow")) {
    return "error";
  }
  if (edgeData.edgeKind === "loopBody" || sourcePort.includes("loopbody") || sourcePort.includes("loopout") || sourcePort.includes("break") || sourcePort.includes("continue")) {
    return "loop";
  }
  if (actionKind === "completeusertask" || actionKind === "changeworkflowstate" || actionKind === "callworkflow") {
    return "approval";
  }
  const existingLabel = normalizeBranchLabel(String(edgeData.label ?? ""));
  if (existingLabel === "approved" || existingLabel === "rejected" || existingLabel === "timeout") {
    return "approval";
  }
  const caseValues = Array.isArray(edge.caseValues) ? edge.caseValues as Array<{ kind?: string }> : [];
  const hasDecisionCase = caseValues.some(item => item.kind === "boolean" || item.kind === "fallback");
  if (hasDecisionCase) {
    return "decision";
  }
  return "custom";
}

function findEdgeByFlowId(workflow: MicroflowWorkflowJSON, flowId: string): MicroflowWorkflowEdgeJSON | undefined {
  return (workflow.edges as MicroflowWorkflowEdgeJSON[]).find(edge => {
    const edgeFlowId = String((((edge.data ?? {}) as Partial<FlowGramMicroflowEdgeData>).flowId ?? edge.id));
    return edgeFlowId === String(flowId);
  });
}

function isConflictingScopedBranchLabel(input: {
  workflow: MicroflowWorkflowJSON;
  currentFlowId: string;
  scope: BranchLabelScope;
  normalizedLabel: string;
}): boolean {
  if (input.scope === "custom" || !input.normalizedLabel) {
    return false;
  }
  const currentEdge = findEdgeByFlowId(input.workflow, input.currentFlowId);
  if (!currentEdge) {
    return false;
  }
  const currentSource = currentEdge.sourceNodeID;
  return (input.workflow.edges as MicroflowWorkflowEdgeJSON[]).some(edge => {
    const flowId = String((((edge.data ?? {}) as Partial<FlowGramMicroflowEdgeData>).flowId ?? edge.id));
    if (flowId === String(input.currentFlowId)) {
      return false;
    }
    if (edge.sourceNodeID !== currentSource) {
      return false;
    }
    if (edgeBranchLabelScope(edge) !== input.scope) {
      return false;
    }
    const label = normalizeBranchLabel(String((((edge.data ?? {}) as Partial<FlowGramMicroflowEdgeData>).label ?? "")));
    return label === input.normalizedLabel;
  });
}

export function validateInlineLineLabelCommit(input: {
  workflow: MicroflowWorkflowJSON;
  flowId: string;
  nextLabel: string;
}): { ok: true; normalizedLabel: string } | { ok: false; message: string } {
  const edge = findEdgeByFlowId(input.workflow, input.flowId);
  const scope = edgeBranchLabelScope(edge);
  const normalizedLabel = normalizeBranchLabel(input.nextLabel);
  if (scope === "decision" && normalizedLabel && !DECISION_BRANCH_LABELS.has(normalizedLabel)) {
    return { ok: false, message: "Decision 分支标签仅支持 true / false / else。" };
  }
  if (scope === "approval" && normalizedLabel && !APPROVAL_BRANCH_LABELS.has(normalizedLabel)) {
    return { ok: false, message: "审批分支标签仅支持 approved / rejected / timeout。" };
  }
  if (scope === "loop" && normalizedLabel && !LOOP_BRANCH_LABELS.has(normalizedLabel)) {
    return { ok: false, message: "循环分支标签仅支持 body / done / break / continue。" };
  }
  if (scope === "error" && normalizedLabel && !ERROR_BRANCH_LABELS.has(normalizedLabel)) {
    return { ok: false, message: "错误分支标签仅支持 error / fallback / rethrow。" };
  }
  if (isConflictingScopedBranchLabel({
    workflow: input.workflow,
    currentFlowId: input.flowId,
    scope,
    normalizedLabel,
  })) {
    return { ok: false, message: "同一节点下存在重复分支标签，请修改后重试。" };
  }
  return { ok: true, normalizedLabel };
}
