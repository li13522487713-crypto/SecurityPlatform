import { toRuntimeDto } from "@atlas/microflow/adapters/runtime";
import { authoringToFlowGram } from "@atlas/microflow/flowgram/authoring-to-flowgram";
import { getDefaultMockMetadataCatalog } from "@atlas/microflow/metadata";
import { isMicroflowP0ActionStronglyTyped } from "@atlas/microflow/schema/authoring";
import { normalizeMicroflowSchema } from "@atlas/microflow/schema/legacy";
import type { MicroflowAction } from "@atlas/microflow/schema/types";
import { collectObjectsRecursive } from "@atlas/microflow/schema/utils";
import { validateMicroflowSchema } from "@atlas/microflow/validators";
import { buildVariableIndex } from "@atlas/microflow/variables";

import { toExecutionPlan, toExecutionPlanFromSchema } from "./runtime-semantics";
import { microflowSampleManifest } from "./sample-manifest";

export interface MicroflowContractVerificationResult {
  ok: boolean;
  errors: string[];
  sampleKeys: string[];
}

const p0FieldPaths: Record<string, string[]> = {
  retrieve: ["action.retrieveSource.entityQualifiedName", "action.retrieveSource.associationQualifiedName", "action.outputVariableName"],
  createObject: ["action.entityQualifiedName", "action.outputVariableName", "action.memberChanges.0.memberQualifiedName", "action.memberChanges.0.valueExpression"],
  changeMembers: ["action.changeVariableName", "action.memberChanges.0.memberQualifiedName", "action.memberChanges.0.valueExpression"],
  commit: ["action.objectOrListVariableName"],
  delete: ["action.objectOrListVariableName"],
  rollback: ["action.objectOrListVariableName"],
  createVariable: ["action.variableName", "action.dataType", "action.initialValue"],
  changeVariable: ["action.targetVariableName", "action.newValueExpression"],
  callMicroflow: ["action.targetMicroflowId", "action.parameterMappings.0.argumentExpression", "action.returnValue.outputVariableName"],
  restCall: ["action.request.method", "action.request.urlExpression", "action.response.handling.outputVariableName", "action.timeoutSeconds"],
  logMessage: ["action.level", "action.template.text"],
};

function p0OutputNames(action: MicroflowAction): string[] {
  if (action.kind === "retrieve" || action.kind === "createObject") {
    return [action.outputVariableName].filter(Boolean);
  }
  if (action.kind === "createVariable") {
    return [action.variableName].filter(Boolean);
  }
  if (action.kind === "callMicroflow" && action.returnValue.storeResult && action.returnValue.outputVariableName) {
    return [action.returnValue.outputVariableName];
  }
  if (action.kind === "restCall") {
    return [
      action.response.handling.kind === "ignore" ? undefined : action.response.handling.outputVariableName,
      action.response.statusCodeVariableName,
      action.response.headersVariableName,
    ].filter((name): name is string => Boolean(name));
  }
  return [];
}

/**
 * 纯函数验收：样例可校验、可转 FlowGram、可转 Runtime DTO、变量索引可建；不访问网络。
 */
export function verifyMicroflowContracts(): MicroflowContractVerificationResult {
  const errors: string[] = [];
  const sampleKeys: string[] = [];

  for (const item of microflowSampleManifest) {
    sampleKeys.push(item.key);
    try {
      const schema = item.createSchema();
      const normalized = normalizeMicroflowSchema(schema);
      if (normalized.id !== schema.id || normalized.stableId !== schema.stableId) {
        errors.push(`${item.key}: normalizeMicroflowSchema 改变了 id/stableId`);
      }
      const validation = validateMicroflowSchema({
        schema,
        metadata: getDefaultMockMetadataCatalog(),
        options: { mode: "save", includeWarnings: true, includeInfo: true },
      });
      if (item.expectedValidation) {
        const errCount = validation.issues.filter(i => i.severity === "error").length;
        const warnCount = validation.issues.filter(i => i.severity === "warning").length;
        if (errCount !== item.expectedValidation.errors || warnCount !== item.expectedValidation.warnings) {
          errors.push(`${item.key}: 期望 errors=${item.expectedValidation.errors} warnings=${item.expectedValidation.warnings}，实际 ${errCount}/${warnCount}`);
        }
      }
      const fg = authoringToFlowGram(schema, validation.issues, schema.debug?.lastTrace ?? []);
      if (typeof fg !== "object" || fg == null) {
        errors.push(`${item.key}: authoringToFlowGram 未返回对象`);
      }
      const dto = toRuntimeDto(schema);
      if (dto.microflowId !== schema.id) {
        errors.push(`${item.key}: toRuntimeDto.microflowId 与 schema.id 不一致`);
      }
      if (!dto.objectCollection || !Array.isArray(dto.flows)) {
        errors.push(`${item.key}: toRuntimeDto 缺少 objectCollection 或 flows`);
      }
      if (!Array.isArray((dto as { p0RuntimeActionBlocks?: unknown }).p0RuntimeActionBlocks)) {
        errors.push(`${item.key}: toRuntimeDto 缺少 p0RuntimeActionBlocks 数组`);
      } else {
        const blocks = (dto as { p0RuntimeActionBlocks: Array<{ supportLevel: string; objectId?: string; action?: { supportLevel?: string; actionKind?: string; config?: unknown } }> }).p0RuntimeActionBlocks;
        const badP0 = blocks
          .filter(b => b.supportLevel === "supported" && b.action && b.action.supportLevel !== "supported");
        if (badP0.length > 0) {
          errors.push(`${item.key}: p0RuntimeActionBlocks 中含非法 supportLevel`);
        }
        for (const object of collectObjectsRecursive(schema.objectCollection)) {
          if (object.kind !== "actionActivity" || !isMicroflowP0ActionStronglyTyped(object.action)) {
            continue;
          }
          const block = blocks.find(candidate => candidate.objectId === object.id);
          if (!block?.action?.config) {
            errors.push(`${item.key}: P0 ${object.action.kind} 缺少 runtime block`);
            continue;
          }
          if (!p0FieldPaths[object.action.kind]?.length) {
            errors.push(`${item.key}: P0 ${object.action.kind} 缺少 fieldPath 契约`);
          }
        }
      }
      const plan = toExecutionPlan(dto);
      if (plan.schemaId !== schema.id || plan.nodes.length < 1) {
        errors.push(`${item.key}: toExecutionPlan 与 schema 不一致或 nodes 为空`);
      }
      if (JSON.stringify(plan).includes("flowGram") || JSON.stringify(plan).toLowerCase().includes("flowgram")) {
        errors.push(`${item.key}: execution plan 不应包含 FlowGram 关键字`);
      }
      const plan2 = toExecutionPlanFromSchema(schema);
      if (plan2.startNodeId !== plan.startNodeId) {
        errors.push(`${item.key}: toExecutionPlanFromSchema.startNodeId 与 toExecutionPlan 不一致`);
      }
      const variableIndex = buildVariableIndex(schema, getDefaultMockMetadataCatalog());
      for (const object of collectObjectsRecursive(schema.objectCollection)) {
        if (object.kind !== "actionActivity" || !isMicroflowP0ActionStronglyTyped(object.action)) {
          continue;
        }
        for (const outputName of p0OutputNames(object.action)) {
          if (!variableIndex.byName?.[outputName]?.length) {
            errors.push(`${item.key}: P0 输出变量 ${outputName} 未进入 VariableIndex`);
          }
        }
      }
    } catch (caught) {
      errors.push(`${item.key}: ${caught instanceof Error ? caught.message : String(caught)}`);
    }
  }

  return { ok: errors.length === 0, errors, sampleKeys };
}
