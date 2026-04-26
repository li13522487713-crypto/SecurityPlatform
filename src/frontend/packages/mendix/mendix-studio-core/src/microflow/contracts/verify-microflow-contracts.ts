import { toRuntimeDto } from "@atlas/microflow/adapters";
import { authoringToFlowGram } from "@atlas/microflow/flowgram/authoring-to-flowgram";
import { mockMicroflowMetadataCatalog } from "@atlas/microflow/metadata";
import { validateMicroflowSchema } from "@atlas/microflow/validators";
import { buildVariableIndex } from "@atlas/microflow/variables";

import { toExecutionPlan, toExecutionPlanFromSchema } from "./runtime-semantics";
import { microflowSampleManifest } from "./sample-manifest";

export interface MicroflowContractVerificationResult {
  ok: boolean;
  errors: string[];
  sampleKeys: string[];
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
      const validation = validateMicroflowSchema({ schema, options: { mode: "save", includeWarnings: true, includeInfo: true } });
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
      buildVariableIndex(schema, mockMicroflowMetadataCatalog);
    } catch (caught) {
      errors.push(`${item.key}: ${caught instanceof Error ? caught.message : String(caught)}`);
    }
  }

  return { ok: errors.length === 0, errors, sampleKeys };
}
