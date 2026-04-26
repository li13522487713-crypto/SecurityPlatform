import type { MicroflowAction, MicroflowDataType, MicroflowExpression, MicroflowDiscriminatedRuntimeP0ActionDto } from "@atlas/microflow/schema";

/**
 * 前端 v1 运行时 DTO 契约：与 toRuntimeDto() 产出的 MicroflowRuntimeDto 配套。
 * 业务主数据仍为 MicroflowAuthoringSchema；本组类型供后端按字段落地或二次映射，不包含 FlowGram JSON。
 * P0 动作为强类型：见 `MicroflowRuntimeDto.p0RuntimeActionBlocks` 与 {@link MicroflowDiscriminatedRuntimeP0ActionDto}。
 */
export type {
  MicroflowRuntimeDto,
  MicroflowRuntimeNodeDto,
  MicroflowRuntimeEdgeDto
} from "@atlas/microflow/schema";

export type { MicroflowDiscriminatedRuntimeP0ActionDto };

export { toRuntimeDto } from "@atlas/microflow/adapters";

/** 与 schema.parameters 对齐的执行期参数描述（精简版）。 */
export interface MicroflowRuntimeParameterDto {
  id: string;
  name: string;
  dataType: MicroflowDataType;
  required: boolean;
}

/** 执行期变量槽位（与 VariableIndex 条目对应的概念视图）。 */
export interface MicroflowRuntimeVariableDto {
  name: string;
  dataType: MicroflowDataType;
  source?: string;
}

/**
 * 以 objectId + actionId 为键的动作摘要；完整载荷仍以 Authoring 侧 MicroflowAction 为准，执行器按 actionKind 分支。
 * P0：retrieve、createObject、changeMembers、commit、delete、rollback、createVariable、changeVariable、callMicroflow、restCall、logMessage 等由后端逐步消费。
 */
export interface MicroflowRuntimeActionDto {
  objectId: string;
  actionId: string;
  actionKind: MicroflowAction["kind"];
  errorHandlingType?: MicroflowAction["errorHandlingType"];
  /** 表达式原文，不含画布几何。 */
  expressionFields?: Array<{ fieldPath: string; expression: MicroflowExpression }>;
}

export interface MicroflowRuntimeFlowDto {
  flowId: string;
  kind: "sequence" | "annotation";
  originObjectId: string;
  destinationObjectId: string;
  isErrorHandler?: boolean;
}

export interface MicroflowRuntimeMetadataRefDto {
  refKind: "entity" | "enumeration" | "microflow" | "page" | "workflow" | "association" | "attribute";
  qualifiedName: string;
}

export interface MicroflowRuntimeErrorHandlingDto {
  errorHandlingType: MicroflowAction["errorHandlingType"];
  scopeObjectId: string;
}
