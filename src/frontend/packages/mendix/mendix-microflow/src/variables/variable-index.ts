import type { MicroflowMetadataCatalog } from "../metadata";
import type {
  MicroflowDataType,
  MicroflowObject,
  MicroflowObjectCollection,
  MicroflowSchema,
  MicroflowSequenceFlow,
  MicroflowVariableIndex,
  MicroflowVariableSymbol
} from "../schema/types";
import { mockMicroflowMetadataCatalog } from "../metadata";

function emptyIndex(): MicroflowVariableIndex {
  return {
    parameters: {},
    localVariables: {},
    objectOutputs: {},
    listOutputs: {},
    loopVariables: {},
    errorVariables: {},
    systemVariables: {
      $currentUser: {
        name: "$currentUser",
        dataType: { kind: "object", entityQualifiedName: "System.User" },
        source: { kind: "system", name: "$currentUser" },
        scope: { collectionId: "root" },
        readonly: true
      }
    }
  };
}

function flattenObjects(collection: MicroflowObjectCollection, parentLoopId?: string): Array<{ object: MicroflowObject; collectionId: string; loopObjectId?: string }> {
  return collection.objects.flatMap(object => {
    if (object.kind !== "loopedActivity") {
      return [{ object, collectionId: collection.id, loopObjectId: parentLoopId }];
    }
    return [
      { object, collectionId: collection.id, loopObjectId: parentLoopId },
      ...flattenObjects(object.objectCollection, object.id)
    ];
  });
}

function retrieveOutputType(object: Extract<MicroflowObject, { kind: "actionActivity" }>): MicroflowDataType {
  const action = object.action;
  if (action.kind !== "retrieve") {
    return { kind: "unknown", reason: "not retrieve" };
  }
  if (action.retrieveSource.kind === "database") {
    const itemType: MicroflowDataType = action.retrieveSource.entityQualifiedName
      ? { kind: "object", entityQualifiedName: action.retrieveSource.entityQualifiedName }
      : { kind: "unknown", reason: "retrieve entity missing" };
    return action.retrieveSource.range.kind === "first" ? itemType : { kind: "list", itemType };
  }
  return { kind: "unknown", reason: action.retrieveSource.associationQualifiedName ?? "association retrieve" };
}

function addOutput(index: MicroflowVariableIndex, symbol: MicroflowVariableSymbol): void {
  if (symbol.dataType.kind === "list") {
    index.listOutputs[symbol.name] = symbol;
    return;
  }
  if (symbol.dataType.kind === "object") {
    index.objectOutputs[symbol.name] = symbol;
    return;
  }
  index.localVariables[symbol.name] = symbol;
}

export function buildVariableIndex(schema: MicroflowSchema, _metadata: MicroflowMetadataCatalog = mockMicroflowMetadataCatalog): MicroflowVariableIndex {
  const index = emptyIndex();
  index.systemVariables.$currentUser.scope.collectionId = schema.objectCollection.id;
  for (const parameter of schema.parameters) {
    index.parameters[parameter.name] = {
      name: parameter.name,
      dataType: parameter.dataType,
      source: { kind: "parameter", parameterId: parameter.id },
      scope: { collectionId: schema.objectCollection.id },
      readonly: true
    };
  }
  for (const { object, collectionId } of flattenObjects(schema.objectCollection)) {
    if (object.kind === "loopedActivity" && object.loopSource.kind === "iterableList") {
      const loopSource = object.loopSource;
      const listSymbol = Object.values(index.listOutputs).find(symbol => symbol.name === loopSource.listVariableName);
      index.loopVariables[loopSource.iteratorVariableName] = {
        name: loopSource.iteratorVariableName,
        dataType: listSymbol?.dataType.kind === "list" ? listSymbol.dataType.itemType : { kind: "unknown", reason: loopSource.listVariableName },
        source: { kind: "loopIterator", loopObjectId: object.id },
        scope: { collectionId: object.objectCollection.id, loopObjectId: object.id },
        readonly: true
      };
      index.systemVariables.$currentIndex = {
        name: "$currentIndex",
        dataType: { kind: "integer" },
        source: { kind: "system", name: "$currentIndex" },
        scope: { collectionId: object.objectCollection.id, loopObjectId: object.id },
        readonly: true
      };
    }
    if (object.kind !== "actionActivity") {
      continue;
    }
    const action = object.action;
    if (action.kind === "retrieve") {
      addOutput(index, {
        name: action.outputVariableName,
        dataType: retrieveOutputType(object),
        source: { kind: "actionOutput", objectId: object.id, actionId: action.id },
        scope: { collectionId, startObjectId: object.id },
        readonly: false
      });
    }
    if (action.kind === "createObject") {
      addOutput(index, {
        name: action.outputVariableName,
        dataType: { kind: "object", entityQualifiedName: action.entityQualifiedName },
        source: { kind: "actionOutput", objectId: object.id, actionId: action.id },
        scope: { collectionId, startObjectId: object.id },
        readonly: false
      });
    }
    if (action.kind === "createVariable" || action.kind === "changeVariable") {
      const name = typeof action.variableName === "string" ? action.variableName : undefined;
      if (name) {
        addOutput(index, {
          name,
          dataType: { kind: "unknown", reason: action.kind },
          source: { kind: "localVariable", objectId: object.id, actionId: action.id },
          scope: { collectionId, startObjectId: object.id },
          readonly: false
        });
      }
    }
    if (action.kind === "callMicroflow" && action.returnValue.storeResult && action.returnValue.outputVariableName) {
      addOutput(index, {
        name: action.returnValue.outputVariableName,
        dataType: action.returnValue.dataType ?? { kind: "unknown", reason: "microflow return" },
        source: { kind: "actionOutput", objectId: object.id, actionId: action.id },
        scope: { collectionId, startObjectId: object.id },
        readonly: false
      });
    }
    if (action.kind === "restCall" && action.response.handling.kind !== "ignore") {
      addOutput(index, {
        name: action.response.handling.outputVariableName,
        dataType: action.response.handling.kind === "string" ? { kind: "string" } : { kind: "json" },
        source: { kind: "actionOutput", objectId: object.id, actionId: action.id },
        scope: { collectionId, startObjectId: object.id },
        readonly: false
      });
    }
  }
  for (const flow of schema.flows.filter((item): item is MicroflowSequenceFlow => item.kind === "sequence" && item.isErrorHandler)) {
    for (const name of ["$latestError", "$latestHttpResponse", "$latestSoapFault"] as const) {
      index.errorVariables[name] = {
        name,
        dataType: { kind: "object", entityQualifiedName: name === "$latestError" ? "System.Error" : name === "$latestHttpResponse" ? "System.HttpResponse" : "System.SoapFault" },
        source: { kind: "errorContext", flowId: flow.id },
        scope: { collectionId: schema.objectCollection.id, errorHandlerFlowId: flow.id, startObjectId: flow.destinationObjectId },
        readonly: true
      };
    }
  }
  return index;
}
