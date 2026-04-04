// 前端流程设计器/运行时共用的类型定义（强类型约束，避免 any/unknown）
// 与后端契约对应，后续契约变更需同步更新 docs/contracts.md

export type TenantId = string; // GUID 字符串

// 基础流程定义
export interface FlowDefinition {
  id?: string;
  code: string;
  name: string;
  formCode?: string;
  type?: number; // 业务自定义分类
  isLowCodeFlow?: boolean;
  isOutSideProcess?: boolean;
  remark?: string;
  effectiveStatus?: number; // 0 未生效, 1 已生效
  extraFlags?: number;
  nodes: FlowNode[];
  createdAt?: string;
  updatedAt?: string;
}

// 节点类型枚举（与后端保持一致）
export type NodeType =
  | "start"
  | "end"
  | "approve"
  | "condition"
  | "parallel"
  | "parallel-join"
  | "copy"
  | "task";

// 审批方式
export type SignType = 1 | 2 | 3; // 1:会签,2:或签,3:顺序会签

// 按钮类型（示例，后端可扩展）
export interface NodeButton {
  id?: string;
  pageType?: number; // 1:提交页,2:处理中页,3:查看页...
  buttonType: number;
  name: string;
  remark?: string;
}

// 条件参数
export interface ConditionItem {
  field: string; // 字段名
  op: string; // 运算符，如 eq, ne, gt, lt, between, in
  value?: string | number | Array<string | number>;
  multiple?: boolean;
  group?: number; // 分组（支持多组 OR）
}

export interface ConditionGroup {
  isDefault?: boolean;
  relation?: "AND" | "OR";
  items: ConditionItem[];
}

// 审批人规则
export type ApproverRuleType =
  | "fixedUser"
  | "role"
  | "departmentLeader"
  | "selfSelect"
  | "hrbp"
  | "formField"
  | "outsideApi";

export interface ApproverRule {
  type: ApproverRuleType;
  signType?: SignType;
  userIds?: string[];
  roleCodes?: string[];
  deptIds?: string[];
  apiUrl?: string;
  extra?: Record<string, unknown>;
}

// 抄送人规则
export interface CopyRule {
  userIds?: string[];
  roleCodes?: string[];
  deptIds?: string[];
}

// 节点定义
export interface FlowNode {
  id: string;
  type: NodeType;
  name: string;
  sort?: number;
  parentId?: string;
  children?: FlowNode[];
  // 审批节点专用
  approverRule?: ApproverRule;
  copyRule?: CopyRule;
  buttons?: NodeButton[];
  // 条件节点
  conditions?: ConditionGroup[];
  // 并行/聚合
  parallelGroupId?: string;
  isJoinNode?: boolean;
  // 自定义属性
  ext?: Record<string, unknown>;
}

// 校验结果
export interface FlowValidationResult {
  isValid: boolean;
  errors: string[];
  warnings?: string[];
}

// 设计器保存/加载 DTO
export interface FlowSaveRequest {
  tenantId: TenantId;
  definition: FlowDefinition;
}

export interface FlowSaveResponse {
  id: string;
  version?: number;
}

export interface FlowLoadResponse {
  definition: FlowDefinition;
}

export interface FlowPublishResponse {
  success: boolean;
  version: number;
}
