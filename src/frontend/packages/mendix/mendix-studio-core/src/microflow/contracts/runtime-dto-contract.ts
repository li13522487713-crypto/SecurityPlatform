import type { MicroflowDesignSchema } from "@atlas/microflow/schema";

/**
 * 新版微流运行计划契约：输入仍是 MicroflowDesignSchema，执行计划是服务端按 workflow.nodes/edges 构建的派生视图。
 * 这里不再导出历史运行 DTO，也不提供设计态到历史 DTO 的桥接。
 */
export interface MicroflowRuntimePlanDto {
  schemaVersion: MicroflowDesignSchema["schemaVersion"];
  microflowId: string;
  nodes: MicroflowRuntimePlanNodeDto[];
  edges: MicroflowRuntimePlanEdgeDto[];
  parameters: MicroflowRuntimePlanParameterDto[];
}

export interface MicroflowRuntimePlanNodeDto {
  id: string;
  kind: string;
  title?: string;
  actionKind?: string;
  disabled?: boolean;
}

export interface MicroflowRuntimePlanEdgeDto {
  id: string;
  sourceNodeID: string;
  targetNodeID: string;
  edgeKind?: string;
  isErrorHandler?: boolean;
  caseValues?: unknown[];
}

export interface MicroflowRuntimePlanParameterDto {
  id: string;
  name: string;
  type?: unknown;
  required?: boolean;
}
