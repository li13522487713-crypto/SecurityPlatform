import { createObjectFromRegistry, createSequenceFlow, createAnnotationFlow } from "../adapters";
import type {
  MicroflowActionActivity,
  MicroflowCaseValue,
  MicroflowFlow,
  MicroflowObject,
  MicroflowParameter,
  MicroflowPoint,
  MicroflowSequenceFlow
} from "../schema";
import { defaultMicroflowActionRegistry, microflowActionRegistryByKind } from "./action-registry";
import { defaultMicroflowEdgeRegistry, type MicroflowEditorEdgeKind } from "./edge-registry";
import { microflowNodeRegistryByKey, type MicroflowNodeRegistryEntry } from "./registry";

export interface CreateRegistryResult<TObject> {
  object: TObject;
  parameter?: MicroflowParameter;
  warnings: string[];
}

export interface CreateObjectFromNodeRegistryInput {
  registryKey: string;
  position: MicroflowPoint;
  id?: string;
  overrides?: Partial<MicroflowObject>;
}

export function createObjectFromNodeRegistry(input: CreateObjectFromNodeRegistryInput): CreateRegistryResult<MicroflowObject> {
  const entry = microflowNodeRegistryByKey.get(input.registryKey);
  if (!entry) {
    throw new Error(`Unknown microflow node registry key: ${input.registryKey}`);
  }
  const object = { ...createObjectFromRegistry(entry, input.position, input.id), ...input.overrides } as MicroflowObject;
  const parameter = object.kind === "parameterObject"
    ? {
        id: object.parameterId,
        name: object.parameterName ?? "input",
        dataType: { kind: "unknown", reason: "registry default" },
        required: true
      }
    : undefined;
  return { object, parameter, warnings: [] };
}

export interface CreateActionActivityFromActionRegistryInput {
  actionRegistryKey: string;
  position: MicroflowPoint;
  id?: string;
  overrides?: Partial<MicroflowActionActivity>;
}

export function createActionActivityFromActionRegistry(input: CreateActionActivityFromActionRegistryInput): MicroflowActionActivity {
  const entry = microflowActionRegistryByKind.get(input.actionRegistryKey as typeof defaultMicroflowActionRegistry[number]["key"]);
  if (!entry) {
    throw new Error(`Unknown microflow action registry key: ${input.actionRegistryKey}`);
  }
  const id = input.id ?? `activity-${entry.key}-${Date.now()}`;
  const action = entry.createAction({ id: `action-${id}`, config: entry.defaultConfig, caption: entry.defaultCaption });
  return {
    id,
    stableId: id,
    kind: "actionActivity",
    officialType: "Microflows$ActionActivity",
    caption: entry.defaultCaption,
    documentation: entry.description,
    relativeMiddlePoint: input.position,
    size: { width: 178, height: 76 },
    autoGenerateCaption: false,
    backgroundColor: "default",
    disabled: entry.availability === "nanoflowOnlyDisabled" || entry.availability === "requiresConnector",
    editor: {
      colorToken: entry.colorToken,
      iconKey: entry.iconKey
    },
    action,
    ...input.overrides
  };
}

export interface CreateFlowFromEdgeRegistryInput {
  edgeKind: MicroflowEditorEdgeKind;
  originObjectId: string;
  destinationObjectId: string;
  originConnectionIndex?: number;
  destinationConnectionIndex?: number;
  caseValues?: MicroflowCaseValue[];
  isErrorHandler?: boolean;
  label?: string;
  description?: string;
}

export function createFlowFromEdgeRegistry(input: CreateFlowFromEdgeRegistryInput): MicroflowFlow {
  const entry = defaultMicroflowEdgeRegistry.find(item => item.edgeKind === input.edgeKind);
  if (!entry) {
    throw new Error(`Unknown microflow edge kind: ${input.edgeKind}`);
  }
  if (input.edgeKind === "annotation") {
    return createAnnotationFlow({
      originObjectId: input.originObjectId,
      destinationObjectId: input.destinationObjectId,
      label: input.label,
      description: input.description
    });
  }
  return createSequenceFlow({
    originObjectId: input.originObjectId,
    destinationObjectId: input.destinationObjectId,
    originConnectionIndex: input.originConnectionIndex,
    destinationConnectionIndex: input.destinationConnectionIndex,
    caseValues: input.caseValues ?? [],
    isErrorHandler: input.edgeKind === "errorHandler" || input.isErrorHandler,
    edgeKind: input.edgeKind as MicroflowSequenceFlow["editor"]["edgeKind"],
    label: input.label,
    description: input.description
  });
}

export function createActionActivityFromNodePanelItem(item: MicroflowNodeRegistryEntry, position: MicroflowPoint): MicroflowObject {
  return item.actionKind
    ? createActionActivityFromActionRegistry({ actionRegistryKey: item.actionKind, position })
    : createObjectFromNodeRegistry({ registryKey: item.key ?? item.type, position }).object;
}
