import type {
  MicroflowFlow,
  MicroflowExpression,
  MicroflowObject,
  MicroflowAuthoringSchema,
  MicroflowValidationIssue,
  MicroflowVariableSymbol
} from "../schema";
import type { MicroflowTraceFrame } from "../debug/trace-types";

export type MicroflowPropertyTabKey = "properties" | "documentation" | "errorHandling" | "output" | "advanced";

export interface MicroflowPropertyChangePayload {
  node?: unknown;
  config?: Record<string, unknown>;
  documentation?: unknown;
  outputs?: unknown;
  advanced?: unknown;
  object?: Partial<MicroflowObject>;
  flow?: Partial<MicroflowFlow>;
  fieldPath?: string;
  value?: unknown;
}

export type MicroflowNodePatch = MicroflowPropertyChangePayload;
export type MicroflowEdgePatch = Partial<MicroflowFlow>;

export interface MicroflowPropertyPanelProps {
  selectedObject: MicroflowObject | null;
  selectedFlow?: MicroflowFlow | null;
  schema: MicroflowAuthoringSchema;
  validationIssues: MicroflowValidationIssue[];
  traceFrames?: MicroflowTraceFrame[];
  readonly?: boolean;
  onSchemaChange?: (nextSchema: MicroflowAuthoringSchema, reason: string) => void;
  onObjectChange: (objectId: string, patch: MicroflowNodePatch) => void;
  onFlowChange?: (flowId: string, patch: MicroflowEdgePatch) => void;
  onClose: () => void;
  onLocateObject?: (objectId: string) => void;
  onDuplicateObject?: (objectId: string) => void;
  onDeleteObject?: (objectId: string) => void;
  onDeleteFlow?: (flowId: string) => void;
}

export interface MicroflowNodeFormProps<TObject = MicroflowObject> {
  object: TObject;
  node: TObject;
  schema: MicroflowAuthoringSchema;
  variables: MicroflowVariableSymbol[];
  flows: MicroflowFlow[];
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
  variables: MicroflowVariableSymbol[];
  required?: boolean;
  readonly?: boolean;
  placeholder?: string;
  issues?: string[];
  onChange: (value: MicroflowExpression) => void;
}

export interface MicroflowVariableSelectorProps {
  value?: string;
  variables: MicroflowVariableSymbol[];
  readonly?: boolean;
  placeholder?: string;
  onChange: (value: string) => void;
}

export interface MicroflowEntitySelectorProps {
  value?: string;
  readonly?: boolean;
  onChange: (value: string) => void;
}

export type MicroflowConcreteNode = MicroflowObject;
export type MicroflowActivityFormProps = MicroflowNodeFormProps<Extract<MicroflowObject, { kind: "actionActivity" }>>;
export type MicroflowConfigPatch = Partial<MicroflowObject> & Record<string, unknown>;
