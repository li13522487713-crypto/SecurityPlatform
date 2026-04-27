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
    const existing = existingMappings.find(mapping => mapping.parameterName === parameter.name);
    return {
      parameterName: parameter.name,
      parameterType: parameter.type,
      argumentExpression: existing?.argumentExpression ?? expression(""),
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
    targetMicroflowQualifiedName: "",
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
    targetMicroflowQualifiedName: target.qualifiedName,
    parameterMappings: rebuildCallMicroflowMappings(target.parameters, action.parameterMappings),
    returnValue: {
      ...action.returnValue,
      dataType: returnType,
      storeResult: hasReturnValue ? action.returnValue.storeResult : false,
      outputVariableName: hasReturnValue ? action.returnValue.outputVariableName : undefined,
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
