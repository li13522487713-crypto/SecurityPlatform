import type {
  MicroflowActivityConfig,
  MicroflowActivityNode,
  MicroflowAnnotationNode,
  MicroflowDecisionNode,
  MicroflowEdge,
  MicroflowEventNode,
  MicroflowExpression,
  MicroflowLoopNode,
  MicroflowMergeNode,
  MicroflowNode,
  MicroflowNodeAdvancedConfig,
  MicroflowNodeDocumentation,
  MicroflowNodeOutput,
  MicroflowParameterNode,
  MicroflowSchema,
  MicroflowValidationIssue,
  MicroflowVariable
} from "../schema";

export type MicroflowPropertyTabKey = "properties" | "documentation" | "errorHandling" | "output" | "advanced";

export interface MicroflowPropertyChangePayload {
  node?: Partial<MicroflowNode>;
  config?: Record<string, unknown>;
  documentation?: MicroflowNodeDocumentation;
  advanced?: MicroflowNodeAdvancedConfig;
  outputs?: MicroflowNodeOutput[];
}

export interface MicroflowNodePatch extends MicroflowPropertyChangePayload {}

export interface MicroflowPropertyPanelProps {
  selectedNode: MicroflowNode | null;
  schema: MicroflowSchema;
  validationIssues: MicroflowValidationIssue[];
  traceFrames?: Array<{ nodeId: string; status: string; durationMs: number; error?: string }>;
  readonly?: boolean;
  onNodeChange: (nodeId: string, patch: MicroflowNodePatch) => void;
  onClose: () => void;
  onLocateNode?: (nodeId: string) => void;
  onDuplicateNode?: (nodeId: string) => void;
  onDeleteNode?: (nodeId: string) => void;
}

export interface MicroflowNodeFormProps<TNode extends MicroflowNode = MicroflowNode> {
  node: TNode;
  schema: MicroflowSchema;
  variables: MicroflowVariable[];
  edges: MicroflowEdge[];
  issues: MicroflowValidationIssue[];
  readonly: boolean;
  onPatch: (patch: MicroflowNodePatch) => void;
}

export interface MicroflowNodeFormRegistryItem {
  tabs: MicroflowPropertyTabKey[];
  renderProperties: (props: MicroflowNodeFormProps) => JSX.Element;
}

export type MicroflowNodeFormRegistry = Record<string, MicroflowNodeFormRegistryItem>;

export interface MicroflowExpressionEditorProps {
  value?: MicroflowExpression;
  variables: MicroflowVariable[];
  required?: boolean;
  readonly?: boolean;
  placeholder?: string;
  issues?: string[];
  onChange: (value: MicroflowExpression) => void;
}

export interface MicroflowVariableSelectorProps {
  value?: string;
  variables: MicroflowVariable[];
  readonly?: boolean;
  placeholder?: string;
  onChange: (value: string) => void;
}

export interface MicroflowEntitySelectorProps {
  value?: string;
  readonly?: boolean;
  onChange: (value: string) => void;
}

export type MicroflowConcreteNode =
  | MicroflowActivityNode
  | MicroflowAnnotationNode
  | MicroflowDecisionNode
  | MicroflowEventNode
  | MicroflowLoopNode
  | MicroflowMergeNode
  | MicroflowParameterNode;

export type MicroflowActivityFormProps = MicroflowNodeFormProps<MicroflowActivityNode>;
export type MicroflowConfigPatch = Partial<MicroflowActivityConfig> & Record<string, unknown>;
