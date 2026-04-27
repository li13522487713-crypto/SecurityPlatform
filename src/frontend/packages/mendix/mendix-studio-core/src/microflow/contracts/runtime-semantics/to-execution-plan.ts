import { collectRuntimeFlows, collectRuntimeObjects, getStartEvent } from "@atlas/microflow/debug/trace-utils";
import { toRuntimeDto } from "@atlas/microflow/adapters/runtime";
import { tryMapP0ActionToDiscriminatedDto } from "@atlas/microflow/runtime";
import type {
  MicroflowAction,
  MicroflowAnnotationFlow,
  MicroflowAuthoringSchema,
  MicroflowObject,
  MicroflowObjectCollection,
  MicroflowRuntimeDto,
  MicroflowSequenceFlow,
} from "@atlas/microflow/schema/types";
import type { MicroflowRuntimeMetadataRefDto } from "../runtime-dto-contract";
import { resolveActionRuntimeSupportLevel } from "./runtime-action-support";
import type {
  MicroflowExecutionFlow,
  MicroflowExecutionNode,
  MicroflowExecutionParameter,
  MicroflowExecutionPlan,
  MicroflowUnsupportedActionDescriptor,
} from "./runtime-execution-plan";

function addUniqueRef(refs: MicroflowRuntimeMetadataRefDto[], ref: MicroflowRuntimeMetadataRefDto): void {
  if (!ref.qualifiedName) {
    return;
  }
  if (refs.some(r => r.refKind === ref.refKind && r.qualifiedName === ref.qualifiedName)) {
    return;
  }
  refs.push(ref);
}

function collectRefsFromAction(action: MicroflowAction, refs: MicroflowRuntimeMetadataRefDto[]): void {
  switch (action.kind) {
    case "retrieve": {
      if (action.retrieveSource.kind === "database" && action.retrieveSource.entityQualifiedName) {
        addUniqueRef(refs, { refKind: "entity", qualifiedName: action.retrieveSource.entityQualifiedName });
      }
      if (action.retrieveSource.kind === "association" && action.retrieveSource.associationQualifiedName) {
        addUniqueRef(refs, { refKind: "association", qualifiedName: action.retrieveSource.associationQualifiedName });
      }
      break;
    }
    case "createObject": {
      addUniqueRef(refs, { refKind: "entity", qualifiedName: action.entityQualifiedName });
      break;
    }
    case "changeMembers": {
      for (const change of action.memberChanges) {
        if (change.memberQualifiedName) {
          addUniqueRef(refs, { refKind: "attribute", qualifiedName: change.memberQualifiedName });
        }
      }
      break;
    }
    case "callMicroflow": {
      addUniqueRef(refs, { refKind: "microflow", qualifiedName: action.targetMicroflowId || "invalid" });
      break;
    }
    default:
      break;
  }
}

function collectMetadataRefsFromObjects(objects: MicroflowObject[], refs: MicroflowRuntimeMetadataRefDto[]): void {
  for (const object of objects) {
    if (object.kind === "actionActivity") {
      collectRefsFromAction(object.action, refs);
    }
    if (object.kind === "loopedActivity") {
      const nested = collectRuntimeObjects(object.objectCollection);
      collectMetadataRefsFromObjects(nested, refs);
    }
  }
}

function mapFlowKind(f: MicroflowSequenceFlow | MicroflowAnnotationFlow, isErrorHandler: boolean): MicroflowExecutionFlow["edgeKind"] {
  if (f.kind === "annotation") {
    return "annotation";
  }
  if (isErrorHandler) {
    return "errorHandler";
  }
  const edge = f.editor.edgeKind;
  if (edge === "decisionCondition" || edge === "objectTypeCondition" || edge === "errorHandler") {
    return edge;
  }
  return "sequence";
}

function toExecutionFlow(f: MicroflowSequenceFlow | MicroflowAnnotationFlow): MicroflowExecutionFlow {
  if (f.kind === "annotation") {
    return {
      flowId: f.id,
      kind: "annotation",
      edgeKind: "annotation",
      originObjectId: f.originObjectId,
      destinationObjectId: f.destinationObjectId,
      caseValues: f.caseValues ?? [],
      isErrorHandler: false
    };
  }
  const isErrorHandler = f.isErrorHandler;
  return {
    flowId: f.id,
    kind: "sequence",
    edgeKind: mapFlowKind(f, isErrorHandler),
    originObjectId: f.originObjectId,
    destinationObjectId: f.destinationObjectId,
    caseValues: f.caseValues,
    isErrorHandler
  };
}

function buildErrorHandling(object: MicroflowObject, objectId: string) {
  if (object.kind === "actionActivity") {
    return { errorHandlingType: object.action.errorHandlingType, scopeObjectId: objectId };
  }
  if (object.kind === "exclusiveSplit" || object.kind === "inheritanceSplit" || object.kind === "loopedActivity") {
    return { errorHandlingType: object.errorHandlingType, scopeObjectId: objectId };
  }
  return undefined;
}

function addNodesFromCollection(
  collection: MicroflowObjectCollection,
  collectionId: string,
  parentLoopObjectId: string | undefined,
  nodes: MicroflowExecutionNode[],
  unsupported: MicroflowUnsupportedActionDescriptor[]
): void {
  for (const object of collection.objects) {
    const err = buildErrorHandling(object, object.id);
    if (object.kind === "actionActivity") {
      const r = resolveActionRuntimeSupportLevel(object.action.kind);
      if (r.supportLevel !== "supported" && r.reason) {
        unsupported.push({
          objectId: object.id,
          actionId: object.action.id,
          actionKind: object.action.kind,
          reason: r.reason,
          message: r.message,
          supportLevel: r.supportLevel
        });
      }
    }

    const p0 = object.kind === "actionActivity" ? tryMapP0ActionToDiscriminatedDto(object.action) : undefined;
    const node: MicroflowExecutionNode = {
      objectId: object.id,
      actionId: object.kind === "actionActivity" ? object.action.id : undefined,
      kind: object.kind,
      actionKind: object.kind === "actionActivity" ? object.action.kind : undefined,
      officialType: object.officialType,
      caption: "caption" in object ? object.caption : undefined,
      config: {
        objectKind: object.kind,
        officialType: object.officialType,
        caption: "caption" in object ? object.caption : undefined
      },
      p0ActionRuntime: p0 ?? undefined,
      errorHandling: err,
      collectionId,
      parentLoopObjectId
    };
    nodes.push(node);

    if (object.kind === "loopedActivity") {
      addNodesFromCollection(object.objectCollection, object.objectCollection.id, object.id, nodes, unsupported);
    }
  }
}

/**
 * 将 `MicroflowRuntimeDto` 编译为不含 FlowGram 的执行计划。后端可线性加载 nodes/flows 与 unsupportedActions。
 */
export function toExecutionPlan(
  dto: MicroflowRuntimeDto,
  options?: { resourceId?: string; version?: string }
): MicroflowExecutionPlan {
  const navigation = { flows: dto.flows, objectCollection: dto.objectCollection };
  const allObjects = collectRuntimeObjects(dto.objectCollection);
  const allFlows = collectRuntimeFlows(navigation).map(toExecutionFlow);
  const start = getStartEvent(navigation);
  const endNodeIds = allObjects.filter(o => o.kind === "endEvent").map(o => o.id);

  const parameters: MicroflowExecutionParameter[] = dto.parameters.map(p => ({
    id: p.id,
    name: p.name,
    dataType: p.dataType,
    required: p.required ?? false
  }));

  const nodes: MicroflowExecutionNode[] = [];
  const unsupported: MicroflowUnsupportedActionDescriptor[] = [];
  addNodesFromCollection(dto.objectCollection, dto.objectCollection.id, undefined, nodes, unsupported);

  const metadataRefs: MicroflowRuntimeMetadataRefDto[] = [];
  collectMetadataRefsFromObjects(allObjects, metadataRefs);

  return {
    id: `plan-${dto.microflowId}`,
    schemaId: dto.microflowId,
    resourceId: options?.resourceId,
    version: options?.version ?? dto.schemaVersion,
    parameters,
    nodes,
    flows: allFlows,
    startNodeId: start?.id ?? "missing-start",
    endNodeIds,
    metadataRefs,
    unsupportedActions: unsupported,
    createdAt: new Date().toISOString()
  };
}

/**
 * 与 `toRuntimeDto(schema)` 组合使用：从 Authoring 直转 Plan（不经过手动物料亦可）。
 */
export function toExecutionPlanFromSchema(schema: MicroflowAuthoringSchema, options?: { resourceId?: string; version?: string }): MicroflowExecutionPlan {
  return toExecutionPlan(toRuntimeDto(schema), { resourceId: options?.resourceId, version: options?.version ?? schema.schemaVersion });
}
