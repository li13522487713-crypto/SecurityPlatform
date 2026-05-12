import type {
  MicroflowAction,
  MicroflowAuthoringSchema,
  MicroflowExpression,
  MicroflowObject,
  MicroflowObjectCollection,
  MicroflowVariableIndex,
  MicroflowVariableSymbol,
} from "../schema/types";
import { EMPTY_MICROFLOW_METADATA_CATALOG } from "../metadata/metadata-catalog";
import { buildVariableIndex } from "./variable-index";
import { getVariablesBeforeObject } from "./variable-scope-engine";

export interface MicroflowNodeUsageHighlights {
  selectedObjectId?: string;
  sourceNodeIds: string[];
  consumerNodeIds: string[];
  usedVariableNames: string[];
  outputVariableNames: string[];
}

type VariableReferenceEntry = {
  fieldPath: string;
  variableName: string;
};

const directActionVariableFields: Record<string, string[]> = {
  retrieve: ["startVariableName"],
  changeMembers: ["changeVariableName"],
  commit: ["objectOrListVariableName"],
  delete: ["objectOrListVariableName"],
  rollback: ["objectOrListVariableName"],
  changeVariable: ["targetVariableName"],
  aggregateList: ["listVariableName", "sourceListVariableName"],
  changeList: ["targetListVariableName", "sourceListVariableName"],
  listOperation: ["leftListVariableName", "rightListVariableName", "sourceListVariableName"],
  importXml: ["sourceVariableName"],
  exportXml: ["sourceVariableName"],
  downloadFile: ["fileDocumentVariableName"],
  validationFeedback: ["targetObjectVariableName"],
  changeWorkflowState: ["workflowInstanceVariableName"],
  applyJumpToOption: ["workflowInstanceVariableName"],
  generateJumpToOptions: ["workflowInstanceVariableName"],
  retrieveWorkflowActivityRecords: ["workflowInstanceVariableName"],
  retrieveWorkflowContext: ["workflowInstanceVariableName"],
  showWorkflowAdminPage: ["workflowInstanceVariableName"],
  lockWorkflow: ["workflowInstanceVariableName"],
  unlockWorkflow: ["workflowInstanceVariableName"],
  notifyWorkflow: ["workflowInstanceVariableName"],
  completeUserTask: ["userTaskVariableName"],
  showUserTaskPage: ["userTaskVariableName"],
  deleteExternalObject: ["externalObjectVariableName"],
  sendExternalObject: ["externalObjectVariableName"],
};

function flattenObjects(collection: MicroflowObjectCollection): MicroflowObject[] {
  return collection.objects.flatMap(object => object.kind === "loopedActivity"
    ? [object, ...flattenObjects(object.objectCollection)]
    : [object]);
}

function normalizeVariableName(name: string | undefined): string {
  if (!name) {
    return "";
  }
  const trimmed = String(name).trim();
  return trimmed.startsWith("$.")
    ? trimmed.slice(2)
    : trimmed.startsWith("$")
      ? trimmed.slice(1)
      : trimmed;
}

function parseVariableNamesFromExpressionRaw(raw: string | undefined): string[] {
  const normalized = String(raw ?? "");
  const names = new Set<string>();
  const regex = /\$\.?[A-Za-z_][A-Za-z0-9_]*(?:\/[A-Za-z0-9_.]+)*/g;
  for (const match of normalized.matchAll(regex)) {
    const token = String(match[0] ?? "");
    const variableName = normalizeVariableName(token).split("/")[0] ?? "";
    if (variableName) {
      names.add(variableName);
    }
  }
  return [...names];
}

function collectExpressionEntries(value: unknown, fieldPath: string, sink: Array<{ fieldPath: string; expression: MicroflowExpression }>): void {
  if (!value || typeof value !== "object") {
    return;
  }
  const record = value as Record<string, unknown>;
  if (typeof record.raw === "string") {
    sink.push({ fieldPath, expression: record as unknown as MicroflowExpression });
    return;
  }
  for (const [key, nested] of Object.entries(record)) {
    const nextPath = fieldPath ? `${fieldPath}.${key}` : key;
    if (Array.isArray(nested)) {
      nested.forEach((item, index) => collectExpressionEntries(item, `${nextPath}.${index}`, sink));
      continue;
    }
    collectExpressionEntries(nested, nextPath, sink);
  }
}

function directVariableReferencesForAction(action: MicroflowAction): VariableReferenceEntry[] {
  const fields = directActionVariableFields[action.kind] ?? [];
  const references: VariableReferenceEntry[] = [];
  for (const field of fields) {
    const value = (action as unknown as Record<string, unknown>)[field];
    if (typeof value === "string" && value.trim()) {
      references.push({ fieldPath: `action.${field}`, variableName: normalizeVariableName(value) });
    }
  }
  if (action.kind === "callMicroflow") {
    action.parameterMappings.forEach((mapping, index) => {
      if (mapping.sourceVariableName?.trim()) {
        references.push({
          fieldPath: `action.parameterMappings.${index}.sourceVariableName`,
          variableName: normalizeVariableName(mapping.sourceVariableName),
        });
      }
    });
  }
  return references;
}

function collectObjectVariableReferences(object: MicroflowObject): VariableReferenceEntry[] {
  const references: VariableReferenceEntry[] = [];
  const expressions: Array<{ fieldPath: string; expression: MicroflowExpression }> = [];
  collectExpressionEntries(object, "", expressions);
  for (const entry of expressions) {
    for (const variableName of parseVariableNamesFromExpressionRaw(entry.expression.raw)) {
      references.push({ fieldPath: entry.fieldPath, variableName });
    }
  }
  if (object.kind === "loopedActivity" && object.loopSource.kind === "iterableList" && object.loopSource.listVariableName.trim()) {
    references.push({
      fieldPath: "loopSource.listVariableName",
      variableName: normalizeVariableName(object.loopSource.listVariableName),
    });
  }
  if (object.kind === "actionActivity") {
    references.push(...directVariableReferencesForAction(object.action));
  }
  return references.filter(entry => Boolean(entry.variableName));
}

function producerObjectIdForSymbol(schema: MicroflowAuthoringSchema, symbol: MicroflowVariableSymbol): string | undefined {
  if ("objectId" in symbol.source && symbol.source.objectId) {
    return symbol.source.objectId;
  }
  if (symbol.source.kind === "loopIterator") {
    return symbol.source.loopObjectId;
  }
  if (symbol.source.kind === "parameter") {
    return flattenObjects(schema.objectCollection)
      .find(object => object.kind === "parameterObject" && object.parameterId === symbol.source.parameterId)
      ?.id;
  }
  if (symbol.source.kind === "system" && symbol.loopObjectId && symbol.name === "$currentIndex") {
    return symbol.loopObjectId;
  }
  return undefined;
}

function outputVariableNamesForObject(schema: MicroflowAuthoringSchema, index: MicroflowVariableIndex, object: MicroflowObject): string[] {
  const names = new Set(
    (index.byObjectId?.[object.id] ?? [])
      .map(symbol => normalizeVariableName(symbol.name))
      .filter(Boolean),
  );

  if (object.kind === "parameterObject") {
    const parameter = schema.parameters.find(item => item.id === object.parameterId);
    if (parameter?.name) {
      names.add(normalizeVariableName(parameter.name));
    }
  }

  if (object.kind === "loopedActivity") {
    if (object.loopSource.kind === "iterableList" && object.loopSource.iteratorVariableName.trim()) {
      names.add(normalizeVariableName(object.loopSource.iteratorVariableName));
    }
    names.add("currentIndex");
  }

  return [...names];
}

function emptyUsageHighlights(selectedObjectId?: string): MicroflowNodeUsageHighlights {
  return {
    selectedObjectId,
    sourceNodeIds: [],
    consumerNodeIds: [],
    usedVariableNames: [],
    outputVariableNames: [],
  };
}

function consumerNodeIdsForVariable(schema: MicroflowAuthoringSchema, variableName: string): string[] {
  const normalizedVariableName = normalizeVariableName(variableName);
  if (!normalizedVariableName) {
    return [];
  }
  const objects = flattenObjects(schema.objectCollection);
  const consumerNodeIds = new Set<string>();
  for (const object of objects) {
    const references = collectObjectVariableReferences(object);
    if (references.some(entry => entry.variableName === normalizedVariableName)) {
      consumerNodeIds.add(object.id);
    }
  }
  return [...consumerNodeIds];
}

export function buildVariableUsageHighlights(
  schema: MicroflowAuthoringSchema,
  variableName?: string,
  variableIndex = buildVariableIndex(schema, EMPTY_MICROFLOW_METADATA_CATALOG),
): MicroflowNodeUsageHighlights {
  const normalizedVariableName = normalizeVariableName(variableName);
  if (!normalizedVariableName) {
    return emptyUsageHighlights();
  }

  const sourceNodeIds = new Set<string>();
  for (const symbol of variableIndex.all ?? []) {
    if (normalizeVariableName(symbol.name) !== normalizedVariableName) {
      continue;
    }
    const producerObjectId = producerObjectIdForSymbol(schema, symbol);
    if (producerObjectId) {
      sourceNodeIds.add(producerObjectId);
    }
  }

  return {
    selectedObjectId: undefined,
    sourceNodeIds: [...sourceNodeIds],
    consumerNodeIds: consumerNodeIdsForVariable(schema, normalizedVariableName),
    usedVariableNames: [normalizedVariableName],
    outputVariableNames: [normalizedVariableName],
  };
}

export function buildNodeUsageHighlights(
  schema: MicroflowAuthoringSchema,
  selectedObjectId?: string,
  variableIndex = buildVariableIndex(schema, EMPTY_MICROFLOW_METADATA_CATALOG),
): MicroflowNodeUsageHighlights {
  if (!selectedObjectId) {
    return emptyUsageHighlights();
  }

  const objects = flattenObjects(schema.objectCollection);
  const selectedObject = objects.find(object => object.id === selectedObjectId);
  if (!selectedObject) {
    return emptyUsageHighlights(selectedObjectId);
  }

  const usedVariableNames = [...new Set(collectObjectVariableReferences(selectedObject).map(entry => entry.variableName))];
  const availableSymbols = getVariablesBeforeObject(schema, variableIndex, selectedObjectId, { includeMaybe: true });
  const sourceNodeIds = new Set<string>();
  for (const variableName of usedVariableNames) {
    for (const symbol of availableSymbols) {
      if (normalizeVariableName(symbol.name) !== variableName) {
        continue;
      }
      const producerObjectId = producerObjectIdForSymbol(schema, symbol);
      if (producerObjectId && producerObjectId !== selectedObjectId) {
        sourceNodeIds.add(producerObjectId);
      }
    }
  }

  const outputVariableNames = outputVariableNamesForObject(schema, variableIndex, selectedObject);
  const consumerNodeIds = new Set<string>();
  if (outputVariableNames.length > 0) {
    const outputNameSet = new Set(outputVariableNames);
    for (const object of objects) {
      if (object.id === selectedObjectId) {
        continue;
      }
      const references = collectObjectVariableReferences(object);
      const availableSymbols = getVariablesBeforeObject(schema, variableIndex, object.id, { includeMaybe: true });
      const consumesSelectedOutput = references.some(entry => {
        if (!outputNameSet.has(entry.variableName)) {
          return false;
        }
        return availableSymbols.some(symbol =>
          normalizeVariableName(symbol.name) === entry.variableName &&
          producerObjectIdForSymbol(schema, symbol) === selectedObjectId
        );
      });
      if (consumesSelectedOutput) {
        consumerNodeIds.add(object.id);
      }
    }
  }

  return {
    selectedObjectId,
    sourceNodeIds: [...sourceNodeIds],
    consumerNodeIds: [...consumerNodeIds],
    usedVariableNames,
    outputVariableNames,
  };
}
