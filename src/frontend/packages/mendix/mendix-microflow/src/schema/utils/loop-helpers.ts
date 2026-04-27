import { buildMicroflowVariableIndex } from "../../variables/microflow-variable-foundation";
import type {
  MicroflowBreakEvent,
  MicroflowContinueEvent,
  MicroflowDataType,
  MicroflowFlow,
  MicroflowIterableListLoopSource,
  MicroflowLoopedActivity,
  MicroflowObject,
  MicroflowObjectCollection,
  MicroflowSchema,
  MicroflowSequenceFlow,
  MicroflowVariableIndex,
  MicroflowWhileLoopCondition,
} from "../types";
import { collectFlowsRecursive, findObjectWithCollection } from "./object-utils";

export type MicroflowLoopType = "forEach" | "while";
export type MicroflowLoopFlowKind = "incoming" | "body" | "exit" | "bodyReturn" | "none";

const loopExitConnectionIndex = 1;
const loopBodyConnectionIndex = 2;
const loopBodyReturnConnectionIndex = 3;

function expression(raw = "") {
  return {
    raw,
    inferredType: { kind: "boolean" as const },
    references: { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] },
    diagnostics: [],
  };
}

function mapObjects(schema: MicroflowSchema, objectId: string, mapper: (object: MicroflowObject) => MicroflowObject): MicroflowSchema {
  const visit = (object: MicroflowObject): MicroflowObject => {
    const mapped = object.id === objectId ? mapper(object) : object;
    return mapped.kind === "loopedActivity"
      ? { ...mapped, objectCollection: { ...mapped.objectCollection, objects: mapped.objectCollection.objects.map(visit) } }
      : mapped;
  };
  return {
    ...schema,
    objectCollection: {
      ...schema.objectCollection,
      objects: schema.objectCollection.objects.map(visit),
    },
  };
}

function refreshLoopVariables(schema: MicroflowSchema): MicroflowSchema {
  return { ...schema, variables: buildMicroflowVariableIndex(schema) };
}

export function getLoopType(loop: MicroflowLoopedActivity): MicroflowLoopType {
  return loop.loopSource.kind === "whileCondition" ? "while" : "forEach";
}

export function updateLoopType(schema: MicroflowSchema, loopObjectId: string, loopType: MicroflowLoopType): MicroflowSchema {
  return refreshLoopVariables(mapObjects(schema, loopObjectId, object => {
    if (object.kind !== "loopedActivity" || getLoopType(object) === loopType) {
      return object;
    }
    const loopSource: MicroflowIterableListLoopSource | MicroflowWhileLoopCondition = loopType === "while"
      ? { kind: "whileCondition", officialType: "Microflows$WhileLoopCondition", expression: expression("") }
      : { kind: "iterableList", officialType: "Microflows$IterableList", listVariableName: "", iteratorVariableName: "", currentIndexVariableName: "$currentIndex" };
    return { ...object, loopSource };
  }));
}

export function updateLoopIterableExpression(schema: MicroflowSchema, loopObjectId: string, listVariableName: string): MicroflowSchema {
  return refreshLoopVariables(mapObjects(schema, loopObjectId, object => object.kind === "loopedActivity" && object.loopSource.kind === "iterableList"
    ? { ...object, loopSource: { ...object.loopSource, listVariableName } }
    : object));
}

export function updateLoopConditionExpression(schema: MicroflowSchema, loopObjectId: string, condition: string): MicroflowSchema {
  return refreshLoopVariables(mapObjects(schema, loopObjectId, object => object.kind === "loopedActivity" && object.loopSource.kind === "whileCondition"
    ? { ...object, loopSource: { ...object.loopSource, expression: { ...object.loopSource.expression, raw: condition, text: condition } } }
    : object));
}

export function upsertLoopVariable(schema: MicroflowSchema, loopObjectId: string, variable: { name: string; dataType?: MicroflowDataType }): MicroflowSchema {
  return refreshLoopVariables(mapObjects(schema, loopObjectId, object => object.kind === "loopedActivity" && object.loopSource.kind === "iterableList"
    ? {
        ...object,
        loopSource: {
          ...object.loopSource,
          iteratorVariableName: variable.name,
          iteratorVariableDataType: variable.dataType ?? object.loopSource.iteratorVariableDataType,
        },
      }
    : object));
}

export function removeLoopVariable(schema: MicroflowSchema, loopObjectId: string): MicroflowSchema {
  return refreshLoopVariables(mapObjects(schema, loopObjectId, object => object.kind === "loopedActivity" && object.loopSource.kind === "iterableList"
    ? { ...object, loopSource: { ...object.loopSource, iteratorVariableName: "", iteratorVariableDataType: undefined } }
    : object));
}

export function buildLoopVariableIndex(schema: MicroflowSchema): MicroflowVariableIndex["loopVariables"] {
  return buildMicroflowVariableIndex(schema).loopVariables;
}

export function getLoopBodyFlows(schema: MicroflowSchema, loopObjectId: string): MicroflowSequenceFlow[] {
  return collectFlowsRecursive(schema).filter((flow): flow is MicroflowSequenceFlow =>
    flow.kind === "sequence" &&
    flow.originObjectId === loopObjectId &&
    (flow.originConnectionIndex ?? 0) === loopBodyConnectionIndex
  );
}

export function getLoopExitFlows(schema: MicroflowSchema, loopObjectId: string): MicroflowSequenceFlow[] {
  return collectFlowsRecursive(schema).filter((flow): flow is MicroflowSequenceFlow =>
    flow.kind === "sequence" &&
    flow.originObjectId === loopObjectId &&
    (flow.originConnectionIndex ?? 0) === loopExitConnectionIndex
  );
}

export function getLoopIncomingFlows(schema: MicroflowSchema, loopObjectId: string): MicroflowFlow[] {
  return collectFlowsRecursive(schema).filter(flow => flow.destinationObjectId === loopObjectId && (flow.destinationConnectionIndex ?? 0) !== loopBodyReturnConnectionIndex);
}

export function getLoopBodyReturnFlows(schema: MicroflowSchema, loopObjectId: string): MicroflowFlow[] {
  return collectFlowsRecursive(schema).filter(flow => flow.destinationObjectId === loopObjectId && (flow.destinationConnectionIndex ?? 0) === loopBodyReturnConnectionIndex);
}

export function getLoopFlowKind(schema: MicroflowSchema, flow: MicroflowFlow): MicroflowLoopFlowKind {
  const origin = findObjectWithCollection(schema, flow.originObjectId)?.object;
  const destination = findObjectWithCollection(schema, flow.destinationObjectId)?.object;
  if (origin?.kind === "loopedActivity" && flow.kind === "sequence") {
    if ((flow.originConnectionIndex ?? 0) === loopBodyConnectionIndex) {
      return "body";
    }
    if ((flow.originConnectionIndex ?? 0) === loopExitConnectionIndex) {
      return "exit";
    }
  }
  if (destination?.kind === "loopedActivity" && (flow.destinationConnectionIndex ?? 0) === loopBodyReturnConnectionIndex) {
    return "bodyReturn";
  }
  if (destination?.kind === "loopedActivity") {
    return "incoming";
  }
  return "none";
}

export function assignLoopFlowKind(schema: MicroflowSchema, flowId: string, kind: "body" | "exit"): MicroflowSchema {
  const flows = collectFlowsRecursive(schema);
  const target = flows.find(flow => flow.id === flowId);
  if (!target || target.kind !== "sequence") {
    return schema;
  }
  const nextFlow: MicroflowSequenceFlow = {
    ...target,
    originConnectionIndex: kind === "body" ? loopBodyConnectionIndex : loopExitConnectionIndex,
    editor: {
      ...target.editor,
      label: kind === "body" ? "Body" : "After",
    },
  };
  const mapCollectionFlows = (collection: MicroflowObjectCollection): MicroflowObjectCollection => ({
    ...collection,
    flows: collection.flows?.map(flow => flow.id === flowId ? nextFlow : flow),
    objects: collection.objects.map(object => object.kind === "loopedActivity"
      ? { ...object, objectCollection: mapCollectionFlows(object.objectCollection) }
      : object),
  });
  return {
    ...schema,
    flows: schema.flows.map(flow => flow.id === flowId ? nextFlow : flow),
    objectCollection: mapCollectionFlows(schema.objectCollection),
  };
}

export function getLoopWarnings(schema: MicroflowSchema, loopObjectId: string): string[] {
  const loop = findObjectWithCollection(schema, loopObjectId)?.object;
  if (!loop || loop.kind !== "loopedActivity") {
    return ["Loop object is missing."];
  }
  const warnings: string[] = [];
  if (!getLoopType(loop)) {
    warnings.push("Loop type is required.");
  }
  if (loop.loopSource.kind === "iterableList") {
    if (!loop.loopSource.listVariableName.trim()) {
      warnings.push("forEach loop requires a source list expression.");
    }
    if (!loop.loopSource.iteratorVariableName.trim()) {
      warnings.push("forEach loop requires a loop variable.");
    }
  }
  if (loop.loopSource.kind === "whileCondition" && !loop.loopSource.expression.raw.trim()) {
    warnings.push("while loop requires a condition expression.");
  }
  if (getLoopBodyFlows(schema, loopObjectId).length === 0) {
    warnings.push("Loop has no body flow.");
  }
  if (getLoopExitFlows(schema, loopObjectId).length === 0) {
    warnings.push("Loop has no exit flow.");
  }
  if (getLoopBodyFlows(schema, loopObjectId).length > 1) {
    warnings.push("Loop body currently expects a single body flow.");
  }
  if (getLoopExitFlows(schema, loopObjectId).length > 1) {
    warnings.push("Loop exit currently expects a single exit flow.");
  }
  return warnings;
}

export function updateBreakContinueTargetLoop(schema: MicroflowSchema, objectId: string, loopObjectId?: string): MicroflowSchema {
  return refreshLoopVariables(mapObjects(schema, objectId, object => {
    if (object.kind !== "breakEvent" && object.kind !== "continueEvent") {
      return object;
    }
    return { ...object, targetLoopObjectId: loopObjectId } as MicroflowBreakEvent | MicroflowContinueEvent;
  }));
}

export function getBreakContinueWarnings(schema: MicroflowSchema, objectId: string): string[] {
  const located = findObjectWithCollection(schema, objectId);
  const object = located?.object;
  if (!object || (object.kind !== "breakEvent" && object.kind !== "continueEvent")) {
    return [];
  }
  const loops = schema.variables ? Object.values(schema.variables.loopVariables) : [];
  const loopObjects = collectLoopObjects(schema);
  const warnings: string[] = [];
  if (loopObjects.length === 0 && loops.length === 0) {
    warnings.push("No Loop exists in the current microflow.");
  }
  if (!located?.parentLoopObjectId) {
    warnings.push("This control event is not inside a Loop body. Full containment validation will be completed in Stage 20.");
  }
  if (object.targetLoopObjectId && !loopObjects.some(loop => loop.id === object.targetLoopObjectId)) {
    warnings.push("Target Loop is stale or has been deleted.");
  }
  if (!object.targetLoopObjectId && loopObjects.length === 1) {
    warnings.push(`Implicit target Loop: ${loopObjects[0].caption ?? loopObjects[0].id}.`);
  }
  if (!object.targetLoopObjectId && loopObjects.length > 1) {
    warnings.push("Multiple Loops exist. Select a target Loop to avoid ambiguity.");
  }
  if (collectFlowsRecursive(schema).some(flow => flow.originObjectId === object.id)) {
    warnings.push("Break / Continue normally should not have outgoing sequence flows.");
  }
  return warnings;
}

export function collectLoopObjects(schema: MicroflowSchema): MicroflowLoopedActivity[] {
  const loops: MicroflowLoopedActivity[] = [];
  const visit = (objects: MicroflowObject[]) => {
    for (const object of objects) {
      if (object.kind === "loopedActivity") {
        loops.push(object);
        visit(object.objectCollection.objects);
      }
    }
  };
  visit(schema.objectCollection.objects);
  return loops;
}
