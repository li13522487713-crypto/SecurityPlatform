import type {
  MicroflowAuthoringSchema,
  MicroflowDataType,
  MicroflowExpression,
  MicroflowObject,
  MicroflowObjectCollection,
  MicroflowParameter,
  MicroflowTypeRef,
} from "../types";

const RESERVED_PARAMETER_NAMES = new Set(["currentUser", "latestError", "currentIndex"]);

function mapObjectCollection(
  collection: MicroflowObjectCollection,
  updater: (object: MicroflowObject) => MicroflowObject,
): MicroflowObjectCollection {
  return {
    ...collection,
    objects: collection.objects.map(object => {
      const next = updater(object);
      return next.kind === "loopedActivity"
        ? { ...next, objectCollection: mapObjectCollection(next.objectCollection, updater) }
        : next;
    }),
  };
}

function normalizeParameterName(name: string): string {
  return name.trim().toLocaleLowerCase();
}

export function microflowDataTypeToTypeRef(dataType: MicroflowDataType): MicroflowTypeRef {
  if (dataType.kind === "void") {
    return { kind: "void", name: "Void" };
  }
  if (dataType.kind === "object") {
    return { kind: "entity", name: dataType.entityQualifiedName || "Object", entity: dataType.entityQualifiedName };
  }
  if (dataType.kind === "list") {
    return { kind: "list", name: "List", itemType: microflowDataTypeToTypeRef(dataType.itemType) };
  }
  if (dataType.kind === "unknown") {
    return { kind: "unknown", name: dataType.reason ?? "Unknown" };
  }
  return { kind: "primitive", name: dataType.kind };
}

export function getMicroflowParameters(schema: MicroflowAuthoringSchema): MicroflowParameter[] {
  return schema.parameters ?? [];
}

export function upsertMicroflowParameter(
  schema: MicroflowAuthoringSchema,
  parameter: MicroflowParameter,
): MicroflowAuthoringSchema {
  const nextParameter = {
    ...parameter,
    type: parameter.type ?? microflowDataTypeToTypeRef(parameter.dataType),
  };
  const exists = schema.parameters.some(item => item.id === parameter.id);
  return {
    ...schema,
    parameters: exists
      ? schema.parameters.map(item => item.id === parameter.id ? { ...item, ...nextParameter } : item)
      : [...schema.parameters, nextParameter],
  };
}

export function removeMicroflowParameter(
  schema: MicroflowAuthoringSchema,
  parameterId: string,
): MicroflowAuthoringSchema {
  return {
    ...schema,
    parameters: schema.parameters.filter(parameter => parameter.id !== parameterId),
    objectCollection: mapObjectCollection(schema.objectCollection, object => object.kind === "parameterObject" && object.parameterId === parameterId
      ? { ...object, parameterName: undefined }
      : object),
  };
}

export function renameMicroflowParameter(
  schema: MicroflowAuthoringSchema,
  parameterId: string,
  nextName: string,
): MicroflowAuthoringSchema {
  return {
    ...schema,
    parameters: schema.parameters.map(parameter => parameter.id === parameterId ? { ...parameter, name: nextName } : parameter),
    objectCollection: mapObjectCollection(schema.objectCollection, object => object.kind === "parameterObject" && object.parameterId === parameterId
      ? { ...object, caption: nextName, parameterName: nextName }
      : object),
  };
}

export function updateMicroflowParameterType(
  schema: MicroflowAuthoringSchema,
  parameterId: string,
  nextType: MicroflowDataType,
): MicroflowAuthoringSchema {
  return {
    ...schema,
    parameters: schema.parameters.map(parameter => parameter.id === parameterId
      ? { ...parameter, dataType: nextType, type: microflowDataTypeToTypeRef(nextType) }
      : parameter),
  };
}

export function syncParameterObjectToDefinition(
  schema: MicroflowAuthoringSchema,
  objectId: string,
): MicroflowAuthoringSchema {
  const object = findObject(schema.objectCollection, objectId);
  if (!object || object.kind !== "parameterObject") {
    return schema;
  }
  const current = schema.parameters.find(parameter => parameter.id === object.parameterId);
  const name = object.parameterName ?? object.caption ?? current?.name ?? "parameter";
  return upsertMicroflowParameter(schema, {
    ...(current ?? {
      id: object.parameterId,
      stableId: object.parameterId,
      dataType: { kind: "string" },
      required: true,
    }),
    name,
    documentation: current?.documentation ?? object.documentation,
  });
}

export function syncParameterDefinitionToObject(
  schema: MicroflowAuthoringSchema,
  parameterId: string,
): MicroflowAuthoringSchema {
  const parameter = schema.parameters.find(item => item.id === parameterId);
  if (!parameter) {
    return schema;
  }
  return {
    ...schema,
    objectCollection: mapObjectCollection(schema.objectCollection, object => object.kind === "parameterObject" && object.parameterId === parameterId
      ? { ...object, caption: parameter.name, parameterName: parameter.name, documentation: parameter.documentation ?? object.documentation }
      : object),
  };
}

export function updateMicroflowReturnType(
  schema: MicroflowAuthoringSchema,
  returnType: MicroflowDataType,
): MicroflowAuthoringSchema {
  return {
    ...schema,
    returnType,
    objectCollection: returnType.kind === "void"
      ? mapObjectCollection(schema.objectCollection, object => object.kind === "endEvent" ? { ...object, returnValue: undefined } : object)
      : schema.objectCollection,
  };
}

export function updateEndEventReturnValue(
  schema: MicroflowAuthoringSchema,
  endObjectId: string,
  expression: MicroflowExpression | undefined,
): MicroflowAuthoringSchema {
  return {
    ...schema,
    objectCollection: mapObjectCollection(schema.objectCollection, object => object.id === endObjectId && object.kind === "endEvent"
      ? { ...object, returnValue: expression }
      : object),
  };
}

export function getParameterNameWarning(
  schema: MicroflowAuthoringSchema,
  parameterId: string,
  name: string,
): string | undefined {
  const trimmed = name.trim();
  if (!trimmed) {
    return "Parameter name is required.";
  }
  if (RESERVED_PARAMETER_NAMES.has(trimmed)) {
    return "Parameter name conflicts with a reserved system variable.";
  }
  const normalized = normalizeParameterName(trimmed);
  const duplicate = schema.parameters.some(parameter => parameter.id !== parameterId && normalizeParameterName(parameter.name) === normalized);
  if (duplicate) {
    return "Parameter name must be unique in the current microflow.";
  }
  return undefined;
}

function findObject(collection: MicroflowObjectCollection, objectId: string): MicroflowObject | undefined {
  for (const object of collection.objects) {
    if (object.id === objectId) {
      return object;
    }
    if (object.kind === "loopedActivity") {
      const nested = findObject(object.objectCollection, objectId);
      if (nested) {
        return nested;
      }
    }
  }
  return undefined;
}
