export type ApprovalNodeType =
  | 'start'
  | 'approve'
  | 'copy'
  | 'condition'
  | 'parallel'
  | 'dynamicCondition'
  | 'parallelCondition'
  | 'inclusive'
  | 'route'
  | 'callProcess'
  | 'timer'
  | 'trigger'
  | 'end';

export type JsonValue =
  | string
  | number
  | boolean
  | null
  | JsonValue[]
  | { [key: string]: JsonValue };

export type ConditionOperator =
  | 'equals'
  | 'notEquals'
  | 'greaterThan'
  | 'lessThan'
  | 'greaterThanOrEqual'
  | 'lessThanOrEqual'
  | 'in'
  | 'contains'
  | 'startsWith'
  | 'endsWith';

export interface VisibilityScope {
  scopeType: 'All' | 'Department' | 'Role' | 'User';
  departmentIds?: number[];
  roleCodes?: string[];
  userIds?: number[];
}

export interface ApprovalDefinitionMeta {
  flowName: string;
  description?: string;
  category?: string;
  visibilityScope?: VisibilityScope;
  isQuickEntry?: boolean;
  isLowCodeFlow?: boolean;
}

export interface LfFormField {
  fieldId: string;
  fieldName: string;
  fieldType: string;
  valueType: string;
  options?: Array<{ key: string; value: string }>;
  // 兼容旧前端字段命名
  id?: string;
  label?: string;
  widgetType?: string;
}

export interface FormWidgetOptions {
  name?: string;
  label?: string;
  fieldType?: string;
  options?: Array<{ key: string; value: string }>;
}

export interface FormWidget {
  id?: string;
  type?: string;
  label?: string;
  options?: FormWidgetOptions;
  widgetList?: FormWidget[];
}

export interface FormJson {
  widgetList?: FormWidget[];
  formConfig?: { [key: string]: JsonValue };
}

export interface LfFormPayload {
  formJson: FormJson;
  formFields: LfFormField[];
}

export interface ButtonPermissionConfig {
  startPage?: number[];
  approvalPage?: number[];
  viewPage?: number[];
}

export interface FormPermissionConfig {
  fields: Array<{ fieldId: string; perm: 'R' | 'E' | 'H' }>;
}

export interface NoticeConfig {
  channelIds: number[];
  templateId?: string;
}

export interface ApproverConfig {
  setType: number;
  signType: number;
  noHeaderAction: number;
  nodeApproveList: Array<{ targetId: string; name: string }>;
}

export interface CopyConfig {
  nodeApproveList: Array<{ targetId: string; name: string }>;
}

export interface ConditionItem {
  fieldId: string;
  fieldType: string;
  operator: string;
  value: string | number | boolean | string[];
}

export interface ConditionGroup {
  condRelation: boolean;
  items: ConditionItem[];
}

export interface ConditionConfig {
  isDefault?: boolean;
  groupRelation?: boolean;
  conditionGroups: ConditionGroup[];
}

export interface ParallelConfig {
  parallelNodes: ApprovalNode[];
}

export interface ApprovalNode {
  nodeId: string;
  nodeType: ApprovalNodeType;
  nodeName: string;
  childNode?: ApprovalNode;
  conditionNodes?: ConditionBranch[];
  parallelNodes?: ApprovalNode[];
  approverConfig?: ApproverConfig;
  copyConfig?: CopyConfig;
  conditionConfig?: ConditionConfig;
  parallelConfig?: ParallelConfig;
  noticeConfig?: NoticeConfig;
  formPermissionConfig?: FormPermissionConfig;
  buttonPermissionConfig?: ButtonPermissionConfig;
  
  // 高级设置
  timeoutEnabled?: boolean;
  timeoutHours?: number;
  timeoutMinutes?: number;
  timeoutAction?: 'none' | 'autoApprove' | 'autoReject' | 'autoSkip';
  reminderIntervalHours?: number;
  maxReminderCount?: number;
  deduplicationType?: 'none' | 'skipSame' | 'global';
  excludeUserIds?: string[];
  excludeRoleCodes?: string[];

  // 扩展节点属性
  callAi?: boolean;
  aiConfig?: string;
  callProcessId?: number;
  callAsync?: boolean;
  timerConfig?: string;
  triggerType?: string;
}

export interface ConditionRule {
  field: string;
  operator: ConditionOperator;
  value: JsonValue;
}

export interface ConditionBranch {
  id: string;
  branchName: string;
  conditionRule?: JsonValue; // 支持简单规则或复杂条件组
  childNode?: ApprovalNode;
  isDefault?: boolean;
}

export interface ApprovalDefinitionJson {
  meta: ApprovalDefinitionMeta;
  lfForm?: LfFormPayload;
  nodes: { rootNode: ApprovalNode };
}
