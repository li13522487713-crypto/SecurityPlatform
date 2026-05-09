import type {
  MicroflowDataType,
  MicroflowDesignSchema,
  MicroflowTypeRef,
  MicroflowVariable,
  MicroflowVariableIndex,
  MicroflowVariableSymbol,
} from "../types";

function isVariableIndex(value: unknown): value is MicroflowVariableIndex {
  return Boolean(value && typeof value === "object" && !Array.isArray(value) && "parameters" in value);
}

function dataTypeToTypeRef(dataType: MicroflowDataType | undefined): MicroflowTypeRef {
  if (!dataType) {
    return { kind: "unknown", name: "Unknown" };
  }
  if (dataType.kind === "void") {
    return { kind: "void", name: "Void" };
  }
  if (dataType.kind === "object") {
    return { kind: "entity", name: dataType.entityQualifiedName || "Object", entity: dataType.entityQualifiedName };
  }
  if (dataType.kind === "list") {
    return { kind: "list", name: "List", itemType: dataTypeToTypeRef(dataType.itemType) };
  }
  if (dataType.kind === "unknown") {
    return { kind: "unknown", name: dataType.reason ?? "Unknown" };
  }
  return { kind: "primitive", name: dataType.kind };
}

function symbolScope(symbol: MicroflowVariableSymbol): MicroflowVariable["scope"] {
  if (symbol.source.kind === "errorContext") {
    return "latestError";
  }
  if ("objectId" in symbol.source || "loopObjectId" in symbol.source) {
    return "node";
  }
  return "microflow";
}

function symbolToDesignVariable(symbol: MicroflowVariableSymbol): MicroflowVariable {
  const sourceId = Object.values(symbol.source)
    .filter((value): value is string => typeof value === "string" && value.length > 0)
    .join("-");
  return {
    id: `variable-${symbol.name}-${sourceId || symbol.scope.collectionId || "global"}`,
    name: symbol.name,
    type: dataTypeToTypeRef(symbol.dataType),
    scope: symbolScope(symbol),
  };
}

export function normalizeDesignVariables(value: unknown): MicroflowVariable[] {
  if (Array.isArray(value)) {
    return value.filter((item): item is MicroflowVariable => Boolean(item && typeof item === "object" && "name" in item));
  }
  if (!isVariableIndex(value)) {
    return [];
  }
  const symbols = value.all?.length
    ? value.all
    : [
      ...Object.values(value.parameters ?? {}),
      ...Object.values(value.localVariables ?? {}),
      ...Object.values(value.objectOutputs ?? {}),
      ...Object.values(value.listOutputs ?? {}),
      ...Object.values(value.loopVariables ?? {}),
      ...Object.values(value.errorVariables ?? {}),
      ...Object.values(value.systemVariables ?? {}),
    ];
  const seen = new Set<string>();
  return symbols
    .map(symbolToDesignVariable)
    .filter(variable => {
      const key = `${variable.scope}:${variable.name}:${variable.id}`;
      if (seen.has(key)) {
        return false;
      }
      seen.add(key);
      return true;
    });
}

export function normalizeDesignSchemaVariables<T extends Pick<MicroflowDesignSchema, "variables">>(schema: T): T {
  return {
    ...schema,
    variables: normalizeDesignVariables(schema.variables),
  };
}
