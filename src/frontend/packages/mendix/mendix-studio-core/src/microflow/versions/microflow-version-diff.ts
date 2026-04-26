import type { MicroflowAuthoringSchema } from "@atlas/microflow";

import { formatMicroflowDataType } from "./microflow-version-utils";
import type { MicroflowBreakingChange, MicroflowVersionDiff } from "./microflow-version-types";

function asIdMap<T extends { id: string }>(items: T[]): Map<string, T> {
  return new Map(items.map(item => [item.id, item]));
}

function createBreakingChange(input: Omit<MicroflowBreakingChange, "id">): MicroflowBreakingChange {
  return {
    id: `bc-${input.code}-${input.fieldPath ?? Math.random().toString(36).slice(2, 8)}`,
    ...input
  };
}

export function diffMicroflowSchemas(before: MicroflowAuthoringSchema, after: MicroflowAuthoringSchema): MicroflowVersionDiff {
  const beforeParameters = new Map(before.parameters.map(parameter => [parameter.name, parameter]));
  const afterParameters = new Map(after.parameters.map(parameter => [parameter.name, parameter]));
  const beforeObjects = asIdMap(before.objectCollection.objects);
  const afterObjects = asIdMap(after.objectCollection.objects);
  const beforeFlows = asIdMap(before.flows ?? before.objectCollection.flows ?? []);
  const afterFlows = asIdMap(after.flows ?? after.objectCollection.flows ?? []);
  const breakingChanges: MicroflowBreakingChange[] = [];

  const addedParameters = [...afterParameters.keys()].filter(name => !beforeParameters.has(name));
  const removedParameters = [...beforeParameters.keys()].filter(name => !afterParameters.has(name));
  removedParameters.forEach(name => {
    breakingChanges.push(createBreakingChange({
      severity: "high",
      code: "PARAMETER_REMOVED",
      message: `参数 ${name} 已删除，调用方需要调整入参。`,
      fieldPath: `parameters.${name}`,
      before: name
    }));
  });

  const changedParameters = [...beforeParameters.entries()].flatMap(([name, beforeParameter]) => {
    const afterParameter = afterParameters.get(name);
    if (!afterParameter) {
      return [];
    }
    const beforeType = formatMicroflowDataType(beforeParameter.dataType);
    const afterType = formatMicroflowDataType(afterParameter.dataType);
    if (beforeType === afterType) {
      return [];
    }
    breakingChanges.push(createBreakingChange({
      severity: "high",
      code: "PARAMETER_TYPE_CHANGED",
      message: `参数 ${name} 类型从 ${beforeType} 变更为 ${afterType}。`,
      fieldPath: `parameters.${name}.dataType`,
      before: beforeType,
      after: afterType
    }));
    return [{ name, beforeType, afterType }];
  });

  const beforeReturnType = formatMicroflowDataType(before.returnType);
  const afterReturnType = formatMicroflowDataType(after.returnType);
  const returnTypeChanged = beforeReturnType === afterReturnType ? undefined : { beforeType: beforeReturnType, afterType: afterReturnType };
  if (returnTypeChanged) {
    breakingChanges.push(createBreakingChange({
      severity: "high",
      code: "RETURN_TYPE_CHANGED",
      message: `返回类型从 ${beforeReturnType} 变更为 ${afterReturnType}。`,
      fieldPath: "returnType",
      before: beforeReturnType,
      after: afterReturnType
    }));
  }

  const beforeUrlPath = before.exposure.url?.path;
  const afterUrlPath = after.exposure.url?.path;
  if (beforeUrlPath && afterUrlPath && beforeUrlPath !== afterUrlPath) {
    breakingChanges.push(createBreakingChange({
      severity: "medium",
      code: "EXPOSED_URL_CHANGED",
      message: `公开 URL 从 ${beforeUrlPath} 变更为 ${afterUrlPath}。`,
      fieldPath: "exposure.url.path",
      before: beforeUrlPath,
      after: afterUrlPath
    }));
  }

  const publishedActionEnabled = before.exposure.asMicroflowAction?.enabled === true;
  const currentActionEnabled = after.exposure.asMicroflowAction?.enabled === true;
  if (publishedActionEnabled && !currentActionEnabled) {
    breakingChanges.push(createBreakingChange({
      severity: "medium",
      code: "EXPOSED_URL_CHANGED",
      message: "作为 Microflow Action 的暴露状态已关闭。",
      fieldPath: "exposure.asMicroflowAction.enabled",
      before: "enabled",
      after: "disabled"
    }));
  }

  const removedObjects = [...beforeObjects.keys()].filter(id => !afterObjects.has(id));
  removedObjects.forEach(id => {
    breakingChanges.push(createBreakingChange({
      severity: "low",
      code: "PUBLISHED_NODE_REMOVED",
      message: `已发布节点 ${beforeObjects.get(id)?.caption ?? id} 被删除。`,
      fieldPath: `objectCollection.objects.${id}`,
      before: id
    }));
  });

  return {
    addedParameters,
    removedParameters,
    changedParameters,
    returnTypeChanged,
    addedObjects: [...afterObjects.keys()].filter(id => !beforeObjects.has(id)),
    removedObjects,
    changedObjects: [...beforeObjects.keys()].filter(id => {
      const beforeObject = beforeObjects.get(id);
      const afterObject = afterObjects.get(id);
      return Boolean(beforeObject && afterObject && JSON.stringify(beforeObject) !== JSON.stringify(afterObject));
    }),
    addedFlows: [...afterFlows.keys()].filter(id => !beforeFlows.has(id)),
    removedFlows: [...beforeFlows.keys()].filter(id => !afterFlows.has(id)),
    breakingChanges
  };
}
