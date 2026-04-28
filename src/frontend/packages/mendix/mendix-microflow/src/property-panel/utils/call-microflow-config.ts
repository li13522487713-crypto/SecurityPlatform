import type {
  MicroflowCallMicroflowAction,
  MicroflowDataType,
  MicroflowExpression,
  MicroflowParameterMapping,
} from "../../schema";
import type { MetadataMicroflowParameter, MetadataMicroflowRef } from "../../metadata";

export interface CallMicroflowReferenceDescriptor {
  actionId: string;
  targetMicroflowId: string;
  targetMicroflowQualifiedName?: string;
}

function expression(raw = "", inferredType?: MicroflowDataType): MicroflowExpression {
  return {
    raw,
    inferredType,
    references: { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] },
    diagnostics: [],
  };
}

export function isVoidMicroflowReturn(type?: MicroflowDataType): boolean {
  return !type || type.kind === "void";
}

export function rebuildCallMicroflowMappings(
  targetParameters: readonly MetadataMicroflowParameter[],
  existingMappings: readonly MicroflowParameterMapping[],
): MicroflowParameterMapping[] {
  return targetParameters.map(parameter => {
    const existing = existingMappings.find(mapping =>
      (parameter.id && mapping.targetParameterId === parameter.id) ||
      mapping.targetParameterName === parameter.name ||
      mapping.parameterName === parameter.name
    );
    return {
      targetParameterId: parameter.id,
      targetParameterName: parameter.name,
      parameterName: parameter.name,
      parameterType: parameter.type,
      targetType: parameter.type,
      argumentExpression: existing?.argumentExpression ?? existing?.expression ?? expression(parameter.defaultValueExpression ?? parameter.defaultValue ?? ""),
      expression: existing?.expression ?? existing?.argumentExpression,
      sourceVariableName: existing?.sourceVariableName,
      sourceVariableId: existing?.sourceVariableId,
    };
  });
}

export function clearCallMicroflowTarget(action: MicroflowCallMicroflowAction): MicroflowCallMicroflowAction {
  return {
    ...action,
    targetMicroflowId: "",
    targetMicroflowName: "",
    targetMicroflowDisplayName: "",
    targetMicroflowQualifiedName: "",
    targetModuleId: "",
    targetVersion: undefined,
    targetSchemaId: undefined,
    parameterMappings: [],
    returnValue: { storeResult: false },
  };
}

export function updateCallMicroflowTarget(
  action: MicroflowCallMicroflowAction,
  target: MetadataMicroflowRef | undefined,
): MicroflowCallMicroflowAction {
  if (!target) {
    return clearCallMicroflowTarget(action);
  }
  const returnType = target.returnType;
  const hasReturnValue = !isVoidMicroflowReturn(returnType);
  return {
    ...action,
    targetMicroflowId: target.id,
    targetMicroflowName: target.name,
    targetMicroflowDisplayName: target.displayName ?? target.name,
    targetMicroflowQualifiedName: target.qualifiedName,
    targetModuleId: target.moduleId,
    targetVersion: target.version,
    targetSchemaId: target.schemaId,
    parameterMappings: rebuildCallMicroflowMappings(target.parameters, action.parameterMappings),
    returnValue: {
      ...action.returnValue,
      dataType: returnType,
      storeResult: hasReturnValue ? action.returnValue.storeResult : false,
      outputVariableId: hasReturnValue ? action.returnValue.outputVariableId : undefined,
      outputVariableName: hasReturnValue ? action.returnValue.outputVariableName : undefined,
      resultVariableName: hasReturnValue ? action.returnValue.resultVariableName ?? action.returnValue.outputVariableName : undefined,
    },
  };
}

export function updateCallMicroflowParameterMapping(
  action: MicroflowCallMicroflowAction,
  index: number,
  mapping: Partial<MicroflowParameterMapping>,
): MicroflowCallMicroflowAction {
  return {
    ...action,
    parameterMappings: action.parameterMappings.map((row, rowIndex) => rowIndex === index ? { ...row, ...mapping } : row),
  };
}

export function updateCallMicroflowReturnBinding(
  action: MicroflowCallMicroflowAction,
  outputVariableName: string | undefined,
): MicroflowCallMicroflowAction {
  return {
    ...action,
    returnValue: {
      ...action.returnValue,
      storeResult: Boolean(outputVariableName),
      outputVariableName,
      resultVariableName: outputVariableName,
    },
  };
}

export function getCallMicroflowReferenceDescriptor(action: MicroflowCallMicroflowAction): CallMicroflowReferenceDescriptor | undefined {
  if (!action.targetMicroflowId.trim() && !action.targetMicroflowQualifiedName?.trim()) {
    return undefined;
  }
  return {
    actionId: action.id,
    targetMicroflowId: action.targetMicroflowId,
    targetMicroflowQualifiedName: action.targetMicroflowQualifiedName,
  };
}
