import { toRuntimeDto } from "@atlas/microflow/adapters/runtime";
import { authoringToFlowGram } from "@atlas/microflow/flowgram/authoring-to-flowgram";
import { getDefaultMockMetadataCatalog } from "@atlas/microflow/metadata";
import { isMicroflowP0ActionStronglyTyped } from "@atlas/microflow/schema/authoring";
import { normalizeMicroflowSchema } from "@atlas/microflow/schema/legacy";
import type { MicroflowAction } from "@atlas/microflow/schema/types";
import { collectFlowsRecursive, collectObjectsRecursive, parsePortId } from "@atlas/microflow/schema/utils";
import { validateMicroflowSchema } from "@atlas/microflow/validators";
import { buildVariableIndex, getVariablesAfterObject, getVariablesBeforeObject } from "@atlas/microflow/variables";
import { inferExpressionType, parseExpression, validateExpression } from "@atlas/microflow/expressions";
import { getFlowSemanticHashForSchema } from "@atlas/microflow/layout";

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

function verifyExpressionContracts(errors: string[]): void {
  const metadata = getDefaultMockMetadataCatalog();
  const schema = microflowSampleManifest.find(item => item.key === "sample-order-processing")?.createSchema() ?? microflowSampleManifest[0].createSchema();
  const variableIndex = buildVariableIndex(schema, metadata);
  const parseSamples = [
    "$order/Status = Sales.OrderStatus.New",
    "$order/TotalAmount > 100",
    "not empty($order)",
    "if $order/TotalAmount > 100 then true else false",
  ];
  for (const sample of parseSamples) {
    const parsed = parseExpression(sample);
    if (parsed.ast.kind === "unknown" || parsed.diagnostics.some(item => item.severity === "error")) {
      errors.push(`expression parse failed: ${sample}`);
    }
  }
  const inferred = inferExpressionType({
    expression: "$order/TotalAmount > 100",
    schema,
    metadata,
    variableIndex,
    objectId: "change-order",
    expectedType: { kind: "boolean" },
  });
  if (inferred.inferredType.kind !== "boolean") {
    errors.push("expression infer failed: comparison should be boolean");
  }
  const unknown = validateExpression({
    expression: "$NotExist",
    schema,
    metadata,
    variableIndex,
    context: { objectId: "change-order", fieldPath: "test", expectedType: { kind: "boolean" } },
  });
  if (!unknown.diagnostics.some(item => item.code === "MF_EXPR_UNKNOWN_VARIABLE")) {
    errors.push("expression validate failed: unknown variable should report MF_EXPR_UNKNOWN_VARIABLE");
  }
  const badMember = validateExpression({
    expression: "$order/BadField",
    schema,
    metadata,
    variableIndex,
    context: { objectId: "change-order", fieldPath: "test" },
  });
  if (!badMember.diagnostics.some(item => item.code === "MF_EXPR_MEMBER_NOT_FOUND")) {
    errors.push("expression validate failed: unknown member should report MF_EXPR_MEMBER_NOT_FOUND");
  }
  const loopOut = validateExpression({
    expression: "$currentIndex > 0",
    schema,
    metadata,
    variableIndex,
    context: { objectId: "change-order", fieldPath: "test", expectedType: { kind: "boolean" } },
  });
  if (!loopOut.diagnostics.some(item => item.code === "MF_EXPR_LOOP_VARIABLE_OUT_OF_SCOPE")) {
    errors.push("expression validate failed: $currentIndex outside loop should report scope error");
  }
}

/**
 * 纯函数验收：样例可校验、可转 FlowGram、可转 Runtime DTO、变量索引可建；不访问网络。
 */
export function verifyMicroflowContracts(): MicroflowContractVerificationResult {
  const errors: string[] = [];
  const sampleKeys: string[] = [];
  verifyExpressionContracts(errors);

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
      for (const edge of fg.edges ?? []) {
        if (!edge.data?.flowId || edge.id !== edge.data.flowId) {
          errors.push(`${item.key}: FlowGram edge.id / data.flowId 不稳定`);
        }
        if (!parsePortId(String(edge.sourcePortID ?? "")) || !parsePortId(String(edge.targetPortID ?? ""))) {
          errors.push(`${item.key}: FlowGram edge portId 不可 parse`);
        }
      }
      const dto = toRuntimeDto(schema);
      if (dto.microflowId !== schema.id) {
        errors.push(`${item.key}: toRuntimeDto.microflowId 与 schema.id 不一致`);
      }
      if (!dto.objectCollection || !Array.isArray(dto.flows)) {
        errors.push(`${item.key}: toRuntimeDto 缺少 objectCollection 或 flows`);
      }
      if (dto.flows.some(flow => flow.kind === "annotation")) {
        errors.push(`${item.key}: toRuntimeDto control flows 不应包含 AnnotationFlow`);
      }
      const authoringFlows = collectFlowsRecursive(schema);
      for (const flow of authoringFlows.filter(flow => flow.kind === "sequence")) {
        const dtoFlow = dto.flows.find(candidate => candidate.id === flow.id);
        if (!dtoFlow) {
          errors.push(`${item.key}: Runtime DTO 缺少 SequenceFlow ${flow.id}`);
          continue;
        }
        if (flow.editor.edgeKind !== "sequence" && JSON.stringify(dtoFlow.caseValues) !== JSON.stringify(flow.caseValues)) {
          errors.push(`${item.key}: Runtime DTO 丢失 caseValues ${flow.id}`);
        }
        if (flow.isErrorHandler && !dtoFlow.isErrorHandler) {
          errors.push(`${item.key}: Runtime DTO 丢失 isErrorHandler ${flow.id}`);
        }
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
      if (!dto.variables.all?.length || !plan.variableDeclarations.length) {
        errors.push(`${item.key}: Runtime DTO / ExecutionPlan 缺少变量声明`);
      }
      if (plan.variableDeclarations.length !== (dto.variables.all?.length ?? 0)) {
        errors.push(`${item.key}: ExecutionPlan variableDeclarations 与 Runtime DTO variables 不一致`);
      }
      if (JSON.stringify(plan).includes("flowGram") || JSON.stringify(plan).toLowerCase().includes("flowgram")) {
        errors.push(`${item.key}: execution plan 不应包含 FlowGram 关键字`);
      }
      if (plan.flows.some(flow => flow.kind === "annotation" || flow.edgeKind === "annotation")) {
        errors.push(`${item.key}: ExecutionPlan control flows 不应包含 AnnotationFlow`);
      }
      if (!Array.isArray(plan.normalFlows) || !Array.isArray(plan.decisionFlows) || !Array.isArray(plan.errorHandlerFlows)) {
        errors.push(`${item.key}: ExecutionPlan 缺少 flow 分组`);
      }
      if (getFlowSemanticHashForSchema(schema) !== getFlowSemanticHashForSchema(schema)) {
        errors.push(`${item.key}: flow semantic hash 不稳定`);
      }
      const plan2 = toExecutionPlanFromSchema(schema);
      if (plan2.startNodeId !== plan.startNodeId) {
        errors.push(`${item.key}: toExecutionPlanFromSchema.startNodeId 与 toExecutionPlan 不一致`);
      }
      const variableIndex = buildVariableIndex(schema, getDefaultMockMetadataCatalog());
      for (const object of collectObjectsRecursive(schema.objectCollection)) {
        if (object.kind === "loopedActivity" && object.loopSource.kind === "iterableList") {
          const loopVariables = variableIndex.all?.filter(symbol => symbol.scope.loopObjectId === object.id) ?? [];
          if (!loopVariables.some(symbol => symbol.name === object.loopSource.iteratorVariableName)) {
            errors.push(`${item.key}: Loop iterator 未进入 loop scope`);
          }
          if (!loopVariables.some(symbol => symbol.name === object.loopSource.currentIndexVariableName)) {
            errors.push(`${item.key}: $currentIndex 未进入 loop scope`);
          }
          const inner = object.objectCollection.objects.find(candidate => candidate.kind !== "startEvent");
          if (inner) {
            const innerVars = getVariablesBeforeObject(schema, variableIndex, inner.id, { includeSystem: true });
            if (!innerVars.some(symbol => symbol.name === object.loopSource.iteratorVariableName)) {
              errors.push(`${item.key}: Loop iterator 在循环内部不可见`);
            }
            if (!innerVars.some(symbol => symbol.name === object.loopSource.currentIndexVariableName)) {
              errors.push(`${item.key}: $currentIndex 在循环内部不可见`);
            }
          }
          const outerVars = getVariablesAfterObject(schema, variableIndex, object.id, { includeSystem: true });
          if (outerVars.some(symbol => symbol.name === object.loopSource.iteratorVariableName || symbol.name === object.loopSource.currentIndexVariableName)) {
            errors.push(`${item.key}: Loop 内变量泄漏到循环外`);
          }
        }
        if (object.kind !== "actionActivity" || !isMicroflowP0ActionStronglyTyped(object.action)) {
          continue;
        }
        for (const outputName of p0OutputNames(object.action)) {
          if (!variableIndex.byName?.[outputName]?.length) {
            errors.push(`${item.key}: P0 输出变量 ${outputName} 未进入 VariableIndex`);
          }
          const before = getVariablesBeforeObject(schema, variableIndex, object.id);
          const after = getVariablesAfterObject(schema, variableIndex, object.id);
          if (before.some(symbol => symbol.name === outputName)) {
            errors.push(`${item.key}: P0 输出变量 ${outputName} 在节点前错误可见`);
          }
          if (!after.some(symbol => symbol.name === outputName)) {
            errors.push(`${item.key}: P0 输出变量 ${outputName} 在节点后不可见`);
          }
        }
      }
      if (item.key === "sample-rest-error-handling" && !variableIndex.all?.some(symbol => symbol.name === "$latestError" && symbol.scope.kind === "errorHandler")) {
        errors.push(`${item.key}: ErrorHandler 内缺少 $latestError`);
      }
      if (item.key === "sample-rest-error-handling" && !variableIndex.all?.some(symbol => symbol.name === "$latestHttpResponse" && symbol.scope.kind === "errorHandler")) {
        errors.push(`${item.key}: RestCall ErrorHandler 内缺少 $latestHttpResponse`);
      }
    } catch (caught) {
      errors.push(`${item.key}: ${caught instanceof Error ? caught.message : String(caught)}`);
    }
  }

  return { ok: errors.length === 0, errors, sampleKeys };
}
