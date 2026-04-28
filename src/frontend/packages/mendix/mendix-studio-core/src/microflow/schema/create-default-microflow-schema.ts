import {
  createDefaultEditorState,
  microflowSampleSchemas,
  sampleMicroflowSchema,
  type MicroflowAuthoringSchema,
  type MicroflowDataType,
  type MicroflowExpression,
  type MicroflowFlow,
  type MicroflowLine,
  type MicroflowObject,
  type MicroflowObjectBase,
  type MicroflowParameter,
  type MicroflowVariableIndex
} from "@atlas/microflow";

import type { MicroflowCreateInput } from "../resource/resource-types";

export interface CreateDefaultMicroflowSchemaInput extends MicroflowCreateInput {
  id?: string;
  ownerName?: string;
}

function cloneSchema(schema: MicroflowAuthoringSchema): MicroflowAuthoringSchema {
  return JSON.parse(JSON.stringify(schema)) as MicroflowAuthoringSchema;
}

function nowIso(): string {
  return new Date().toISOString();
}

function makeId(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
}

function expression(raw: string, inferredType?: MicroflowDataType): MicroflowExpression {
  return {
    id: makeId("expr"),
    raw,
    inferredType,
    references: { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] },
    diagnostics: []
  };
}

function line(): MicroflowLine {
  return {
    kind: "orthogonal",
    points: [],
    routing: { mode: "auto", bendPoints: [] },
    style: { strokeType: "solid", strokeWidth: 2, arrow: "target" }
  };
}

function baseObject(id: string, kind: MicroflowObject["kind"], officialType: string, caption: string, x: number, y: number, width = 144, height = 72): MicroflowObjectBase {
  return {
    id,
    stableId: id,
    kind,
    officialType,
    caption,
    documentation: "",
    relativeMiddlePoint: { x, y },
    size: { width, height },
    editor: { iconKey: kind }
  };
}

function sequence(id: string, originObjectId: string, destinationObjectId: string): Extract<MicroflowFlow, { kind: "sequence" }> {
  return {
    id,
    stableId: id,
    kind: "sequence",
    officialType: "Microflows$SequenceFlow",
    originObjectId,
    destinationObjectId,
    originConnectionIndex: 0,
    destinationConnectionIndex: 0,
    caseValues: [],
    isErrorHandler: false,
    line: line(),
    editor: { edgeKind: "sequence" }
  };
}

function emptyVariableIndex(schemaId: string, parameters: MicroflowParameter[]): MicroflowVariableIndex {
  return {
    schemaId,
    builtAt: nowIso(),
    all: parameters.map(parameter => ({
      name: parameter.name,
      dataType: parameter.dataType,
      type: parameter.type,
      source: { kind: "parameter", parameterId: parameter.id },
      scope: { collectionId: "root-collection" },
      readonly: true,
      documentation: parameter.documentation
    })),
    parameters: Object.fromEntries(parameters.map(parameter => [
      parameter.name,
      {
        name: parameter.name,
        dataType: parameter.dataType,
        type: parameter.type,
        source: { kind: "parameter", parameterId: parameter.id },
        scope: { collectionId: "root-collection" },
        readonly: true,
        documentation: parameter.documentation
      }
    ])),
    localVariables: {},
    objectOutputs: {},
    listOutputs: {},
    loopVariables: {},
    errorVariables: {},
    systemVariables: {}
  };
}

function normalizeParameters(input: MicroflowParameter[]): MicroflowParameter[] {
  return input.map((parameter, index) => ({
    ...parameter,
    id: parameter.id || makeId(`param-${index}`),
    stableId: parameter.stableId || parameter.id || makeId(`param-${index}`),
    required: parameter.required ?? false
  }));
}

function applyResourceFields(schema: MicroflowAuthoringSchema, input: CreateDefaultMicroflowSchemaInput, id: string): MicroflowAuthoringSchema {
  const timestamp = nowIso();
  const parameters = normalizeParameters(input.parameters);
  const next: MicroflowAuthoringSchema = {
    ...schema,
    id,
    stableId: id,
    name: input.name,
    displayName: input.displayName || input.name,
    description: input.description,
    moduleId: input.moduleId,
    moduleName: input.moduleName,
    parameters,
    returnType: input.returnType,
    returnVariableName: input.returnVariableName,
    security: {
      applyEntityAccess: input.security?.applyEntityAccess ?? true,
      allowedModuleRoleIds: input.security?.allowedModuleRoleIds ?? [],
      allowedRoleNames: input.security?.allowedRoleNames ?? []
    },
    concurrency: {
      allowConcurrentExecution: input.concurrency?.allowConcurrentExecution ?? true,
      errorMessage: input.concurrency?.errorMessage,
      errorMicroflowId: input.concurrency?.errorMicroflowId ?? null
    },
    exposure: {
      exportLevel: input.exposure?.exportLevel ?? "module",
      markAsUsed: input.exposure?.markAsUsed ?? true,
      asMicroflowAction: input.exposure?.asMicroflowAction ?? { enabled: false },
      asWorkflowAction: input.exposure?.asWorkflowAction ?? { enabled: false },
      url: input.exposure?.url ?? { enabled: false }
    },
    validation: { issues: [] },
    audit: {
      version: "0.1.0",
      status: "draft",
      createdBy: input.ownerName || "Current User",
      createdAt: timestamp,
      updatedBy: input.ownerName || "Current User",
      updatedAt: timestamp
    }
  };
  return next;
}

function createBlankSchema(input: CreateDefaultMicroflowSchemaInput, id: string): MicroflowAuthoringSchema {
  const parameters = normalizeParameters(input.parameters);
  const parameterObjects: MicroflowObject[] = parameters.map((parameter, index) => ({
    ...baseObject(`param-object-${parameter.id}`, "parameterObject", "Microflows$MicroflowParameterObject", `Parameter: ${parameter.name}`, 80, 80 + index * 96, 184, 70),
    kind: "parameterObject",
    officialType: "Microflows$MicroflowParameterObject",
    parameterId: parameter.id,
    parameterName: parameter.name
  }));
  const start: MicroflowObject = {
    ...baseObject("start", "startEvent", "Microflows$StartEvent", "Start", 320, 200, 132, 70),
    kind: "startEvent",
    officialType: "Microflows$StartEvent",
    trigger: { type: "manual" }
  };
  const end: MicroflowObject = {
    ...baseObject("end", "endEvent", "Microflows$EndEvent", "End", 560, 200, 132, 70),
    kind: "endEvent",
    officialType: "Microflows$EndEvent",
    returnValue: input.returnType.kind === "void" ? undefined : expression("empty", input.returnType),
    endBehavior: { type: "normalReturn" }
  };
  return applyResourceFields({
    schemaVersion: "1.0.0",
    mendixProfile: "mx10",
    id,
    stableId: id,
    name: input.name,
    displayName: input.displayName || input.name,
    description: input.description,
    moduleId: input.moduleId,
    moduleName: input.moduleName,
    parameters,
    returnType: input.returnType,
    returnVariableName: input.returnVariableName,
    objectCollection: {
      id: "root-collection",
      officialType: "Microflows$MicroflowObjectCollection",
      objects: [...parameterObjects, start, end]
    },
    flows: [sequence("flow-start-end", "start", "end")],
    security: { applyEntityAccess: true, allowedModuleRoleIds: [] },
    concurrency: { allowConcurrentExecution: true, errorMicroflowId: null },
    exposure: { exportLevel: "module", markAsUsed: true, asMicroflowAction: { enabled: false }, asWorkflowAction: { enabled: false }, url: { enabled: false } },
    variables: emptyVariableIndex(id, parameters),
    validation: { issues: [] },
    editor: createDefaultEditorState(),
    audit: { version: "0.1.0", status: "draft", createdAt: nowIso(), updatedAt: nowIso() }
  }, input, id);
}

export function createDefaultMicroflowSchema(input: CreateDefaultMicroflowSchemaInput): MicroflowAuthoringSchema {
  const id = input.id || makeId("mf");
  if (input.template === "orderProcessing") {
    return applyResourceFields(cloneSchema(sampleMicroflowSchema), input, id);
  }
  const sample = microflowSampleSchemas.find(item => item.key === input.template);
  if (sample) {
    return applyResourceFields(cloneSchema(sample.schema), input, id);
  }
  return createBlankSchema(input, id);
}
