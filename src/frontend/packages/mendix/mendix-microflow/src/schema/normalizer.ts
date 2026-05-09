import type {
  MicroflowCaseValue,
  MicroflowFlow,
  MicroflowObject,
  MicroflowObjectCollection,
  MicroflowSchema,
  MicroflowSequenceFlow,
} from "./types";

const CURRENT_AUTHORING_SCHEMA_VERSION = "1.0.0";

export interface MicroflowSchemaNormalizeChange {
  type: "schemaVersionRepair" | "loopCollectionIdRepair" | "decisionCaseRepair" | "edgeKindRepair" | "flowCollectionRepair";
  objectId?: string;
  flowId?: string;
  before?: unknown;
  after?: unknown;
}

export interface MicroflowSchemaNormalizeIssue {
  code: "MF_FLOW_INVALID_TARGET" | "MF_FLOW_ENDPOINT_MISSING" | "MF_OBJECT_ID_DUPLICATED" | "MF_FLOW_ID_DUPLICATED";
  severity: "error";
  objectId?: string;
  flowId?: string;
  fieldPath?: string;
  message: string;
}

export interface MicroflowSchemaNormalizeReport {
  repaired: boolean;
  changes: MicroflowSchemaNormalizeChange[];
  blockingIssues: MicroflowSchemaNormalizeIssue[];
}

export interface MicroflowSchemaNormalizeResult {
  schema: MicroflowSchema;
  report: MicroflowSchemaNormalizeReport;
}

interface ObjectLocation {
  object: MicroflowObject;
  collectionId: string;
  parentLoopObjectId?: string;
}

interface FlowLocation {
  flow: MicroflowFlow;
  collectionId: string;
}

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

function booleanCase(value: boolean): MicroflowCaseValue {
  return {
    kind: "boolean",
    officialType: "Microflows$EnumerationCase",
    value,
    persistedValue: value ? "true" : "false",
  };
}

function isBooleanDecision(object: MicroflowObject | undefined): boolean {
  return object?.kind === "exclusiveSplit"
    && object.splitCondition.kind === "expression"
    && object.splitCondition.resultType === "boolean";
}

function collectLocations(collection: MicroflowObjectCollection, parentLoopObjectId?: string): Map<string, ObjectLocation> {
  const locations = new Map<string, ObjectLocation>();
  for (const object of collection.objects) {
    locations.set(object.id, { object, collectionId: collection.id, parentLoopObjectId });
    if (object.kind === "loopedActivity") {
      for (const [id, location] of collectLocations(object.objectCollection, object.id)) {
        locations.set(id, location);
      }
    }
  }
  return locations;
}

function collectObjectLocations(collection: MicroflowObjectCollection, parentLoopObjectId?: string): ObjectLocation[] {
  return [
    ...collection.objects.map(object => ({ object, collectionId: collection.id, parentLoopObjectId })),
    ...collection.objects.flatMap(object => object.kind === "loopedActivity"
      ? collectObjectLocations(object.objectCollection, object.id)
      : []),
  ];
}

function collectFlows(collection: MicroflowObjectCollection, rootFlows: MicroflowFlow[]): FlowLocation[] {
  return [
    ...rootFlows.map(flow => ({ flow, collectionId: collection.id })),
    ...collectNestedFlows(collection),
  ];
}

function collectDuplicateIds<T>(items: T[], idOf: (item: T) => string): Set<string> {
  const seen = new Set<string>();
  const duplicates = new Set<string>();
  for (const item of items) {
    const id = idOf(item);
    if (seen.has(id)) {
      duplicates.add(id);
    }
    seen.add(id);
  }
  return duplicates;
}

function collectNestedFlows(collection: MicroflowObjectCollection): FlowLocation[] {
  return [
    ...(collection.flows ?? []).map(flow => ({ flow, collectionId: collection.id })),
    ...collection.objects.flatMap(object => object.kind === "loopedActivity" ? collectNestedFlows(object.objectCollection) : []),
  ];
}

function normalizeCollectionIds(collection: MicroflowObjectCollection, changes: MicroflowSchemaNormalizeChange[]): MicroflowObjectCollection {
  return {
    ...collection,
    objects: collection.objects.map(object => {
      if (object.kind !== "loopedActivity") {
        return object;
      }
      const collectionId = object.objectCollection.id?.trim() || `${object.id}-collection`;
      if (collectionId !== object.objectCollection.id) {
        changes.push({
          type: "loopCollectionIdRepair",
          objectId: object.id,
          before: object.objectCollection.id,
          after: collectionId,
        });
      }
      return {
        ...object,
        objectCollection: normalizeCollectionIds(
          { ...object.objectCollection, id: collectionId },
          changes,
        ),
      };
    }),
  };
}

function emptyCollectionFlows(collection: MicroflowObjectCollection): MicroflowObjectCollection {
  return {
    ...collection,
    flows: [],
    objects: collection.objects.map(object => object.kind === "loopedActivity"
      ? { ...object, objectCollection: emptyCollectionFlows(object.objectCollection) }
      : object),
  };
}

function addFlowToCollection(collection: MicroflowObjectCollection, collectionId: string, flow: MicroflowFlow): MicroflowObjectCollection {
  if (collection.id === collectionId) {
    return { ...collection, flows: [...(collection.flows ?? []), flow] };
  }
  return {
    ...collection,
    objects: collection.objects.map(object => object.kind === "loopedActivity"
      ? { ...object, objectCollection: addFlowToCollection(object.objectCollection, collectionId, flow) }
      : object),
  };
}

function isLoopBodyEntry(flow: MicroflowFlow, source: ObjectLocation, target: ObjectLocation): boolean {
  return flow.kind === "sequence"
    && source.object.kind === "loopedActivity"
    && target.parentLoopObjectId === source.object.id
    && (flow.originConnectionIndex ?? 0) === 2;
}

function isLoopBodyReturn(flow: MicroflowFlow, source: ObjectLocation, target: ObjectLocation): boolean {
  return flow.kind === "sequence"
    && target.object.kind === "loopedActivity"
    && source.parentLoopObjectId === target.object.id
    && (flow.destinationConnectionIndex ?? 0) === 3;
}

function inferTargetCollectionId(
  flow: MicroflowFlow,
  rootCollectionId: string,
  source: ObjectLocation,
  target: ObjectLocation,
): string | undefined {
  if (source.collectionId === target.collectionId) {
    return source.collectionId;
  }
  if (flow.kind === "annotation") {
    return rootCollectionId;
  }
  if (isLoopBodyEntry(flow, source, target)) {
    return target.collectionId;
  }
  if (isLoopBodyReturn(flow, source, target)) {
    return source.collectionId;
  }
  return undefined;
}

function repairDecisionFlows(
  flows: FlowLocation[],
  objects: Map<string, ObjectLocation>,
  changes: MicroflowSchemaNormalizeChange[],
): Map<string, MicroflowFlow> {
  const repaired = new Map<string, MicroflowFlow>();
  const byDecision = new Map<string, MicroflowSequenceFlow[]>();
  for (const { flow } of flows) {
    if (flow.kind !== "sequence" || flow.isErrorHandler || !isBooleanDecision(objects.get(flow.originObjectId)?.object)) {
      repaired.set(flow.id, flow);
      continue;
    }
    byDecision.set(flow.originObjectId, [...(byDecision.get(flow.originObjectId) ?? []), flow]);
  }

  for (const [objectId, outgoing] of byDecision) {
    const used = new Set(
      outgoing
        .flatMap(flow => flow.caseValues)
        .filter(caseValue => caseValue.kind === "boolean")
        .map(caseValue => caseValue.value),
    );
    const sorted = [...outgoing].sort((a, b) => (a.originConnectionIndex ?? 0) - (b.originConnectionIndex ?? 0));
    for (const flow of sorted) {
      let next = flow;
      if (next.editor.edgeKind !== "decisionCondition") {
        next = {
          ...next,
          editor: { ...next.editor, edgeKind: "decisionCondition" },
        };
        changes.push({
          type: "edgeKindRepair",
          objectId,
          flowId: next.id,
          before: flow.editor.edgeKind,
          after: "decisionCondition",
        });
      }
      if (next.caseValues.length === 0) {
        const value = !used.has(true) ? true : !used.has(false) ? false : undefined;
        if (value !== undefined) {
          const caseValue = booleanCase(value);
          next = {
            ...next,
            caseValues: [caseValue],
            editor: { ...next.editor, label: value ? "true" : "false" },
          };
          used.add(value);
          changes.push({
            type: "decisionCaseRepair",
            objectId,
            flowId: next.id,
            before: [],
            after: [caseValue],
          });
        }
      }
      repaired.set(next.id, next);
    }
  }

  return repaired;
}

export function normalizeMicroflowAuthoringSchemaForRuntime(input: MicroflowSchema): MicroflowSchemaNormalizeResult {
  const changes: MicroflowSchemaNormalizeChange[] = [];
  const blockingIssues: MicroflowSchemaNormalizeIssue[] = [];
  let schema = clone(input);
  if (!schema.schemaVersion) {
    schema = { ...schema, schemaVersion: CURRENT_AUTHORING_SCHEMA_VERSION };
    changes.push({ type: "schemaVersionRepair", before: input.schemaVersion, after: CURRENT_AUTHORING_SCHEMA_VERSION });
  }
  const rootCollectionId = schema.objectCollection.id?.trim() || "root-collection";
  if (rootCollectionId !== schema.objectCollection.id) {
    changes.push({ type: "loopCollectionIdRepair", before: schema.objectCollection.id, after: rootCollectionId });
  }
  schema = {
    ...schema,
    objectCollection: normalizeCollectionIds({ ...schema.objectCollection, id: rootCollectionId }, changes),
  };

  const objectLocations = collectLocations(schema.objectCollection);
  const allObjectLocations = collectObjectLocations(schema.objectCollection);
  const originalFlows = collectFlows(schema.objectCollection, schema.flows);
  const duplicateObjectIds = collectDuplicateIds(allObjectLocations, location => location.object.id);
  const duplicateFlowIds = collectDuplicateIds(originalFlows, location => location.flow.id);
  for (const objectId of duplicateObjectIds) {
    blockingIssues.push({
      code: "MF_OBJECT_ID_DUPLICATED",
      severity: "error",
      objectId,
      fieldPath: `objectCollection.objects.${objectId}.id`,
      message: `Object id ${objectId} is duplicated in the microflow schema.`,
    });
  }
  for (const flowId of duplicateFlowIds) {
    blockingIssues.push({
      code: "MF_FLOW_ID_DUPLICATED",
      severity: "error",
      flowId,
      fieldPath: `flows.${flowId}.id`,
      message: `Flow id ${flowId} is duplicated in the microflow schema.`,
    });
  }
  const repairedFlows = repairDecisionFlows(originalFlows, objectLocations, changes);
  let rebuiltCollection = emptyCollectionFlows(schema.objectCollection);
  const rootFlows: MicroflowFlow[] = [];

  for (const location of originalFlows) {
    const flow = duplicateFlowIds.has(location.flow.id) ? location.flow : repairedFlows.get(location.flow.id) ?? location.flow;
    const source = objectLocations.get(flow.originObjectId);
    const target = objectLocations.get(flow.destinationObjectId);
    if (!source || !target) {
      blockingIssues.push({
        code: "MF_FLOW_ENDPOINT_MISSING",
        severity: "error",
        flowId: flow.id,
        fieldPath: `flows.${flow.id}`,
        message: `Flow ${flow.id} has missing source or target object.`,
      });
      if (location.collectionId === rootCollectionId) {
        rootFlows.push(flow);
      } else {
        rebuiltCollection = addFlowToCollection(rebuiltCollection, location.collectionId, flow);
      }
      continue;
    }

    const targetCollectionId = inferTargetCollectionId(flow, rootCollectionId, source, target);
    if (!targetCollectionId) {
      blockingIssues.push({
        code: "MF_FLOW_INVALID_TARGET",
        severity: "error",
        objectId: flow.originObjectId,
        flowId: flow.id,
        fieldPath: `flows.${flow.id}`,
        message: `Flow ${flow.id} crosses object collection boundary from ${source.collectionId} to ${target.collectionId}.`,
      });
      const fallbackCollectionId = location.collectionId || rootCollectionId;
      if (fallbackCollectionId === rootCollectionId) {
        rootFlows.push(flow);
      } else {
        rebuiltCollection = addFlowToCollection(rebuiltCollection, fallbackCollectionId, flow);
      }
      continue;
    }

    if (targetCollectionId !== location.collectionId) {
      changes.push({
        type: "flowCollectionRepair",
        flowId: flow.id,
        before: location.collectionId,
        after: targetCollectionId,
      });
    }
    if (targetCollectionId === rootCollectionId) {
      rootFlows.push(flow);
    } else {
      rebuiltCollection = addFlowToCollection(rebuiltCollection, targetCollectionId, flow);
    }
  }

  return {
    schema: {
      ...schema,
      flows: rootFlows,
      objectCollection: rebuiltCollection,
    },
    report: {
      repaired: changes.length > 0,
      changes,
      blockingIssues,
    },
  };
}
