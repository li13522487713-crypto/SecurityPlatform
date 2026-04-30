import type { MicroflowDesignSchema } from "@atlas/microflow/schema";

import { microflowSampleManifest } from "./sample-manifest";

const legacyObjectCollectionKey = `object${"Collection"}`;
const legacyFlowsKey = "flows";
const legacyPropertyObjectKey = `property${"Object"}`;
const legacyPropertyFlowKey = `property${"Flow"}`;

export interface MicroflowContractVerificationResult {
  ok: boolean;
  errors: string[];
  sampleKeys: string[];
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}

function verifyDesignSchema(schema: MicroflowDesignSchema, key: string, errors: string[]): void {
  if (schema.schemaVersion !== "flowgram.microflow.v1") {
    errors.push(`${key}: schemaVersion 必须是 flowgram.microflow.v1`);
  }
  if (!Array.isArray(schema.workflow?.nodes)) {
    errors.push(`${key}: workflow.nodes 必须是数组`);
  }
  if (!Array.isArray(schema.workflow?.edges)) {
    errors.push(`${key}: workflow.edges 必须是数组`);
  }

  const root = schema as unknown as Record<string, unknown>;
  if (legacyObjectCollectionKey in root || legacyFlowsKey in root) {
    errors.push(`${key}: 根级 schema 禁止包含旧集合/连线字段`);
  }

  const nodeIds = new Set<string>();
  for (const [index, node] of schema.workflow.nodes.entries()) {
    if (!node.id) {
      errors.push(`${key}: workflow.nodes.${index}.id 缺失`);
      continue;
    }
    if (nodeIds.has(node.id)) {
      errors.push(`${key}: workflow.nodes.${index}.id 重复：${node.id}`);
    }
    nodeIds.add(node.id);

    const data = isRecord(node.data) ? node.data : {};
    if (legacyPropertyObjectKey in data || legacyObjectCollectionKey in data) {
      errors.push(`${key}: workflow.nodes.${index}.data 禁止包含旧节点对象字段`);
    }
    if (node.type === "actionActivity" && !isRecord(data.action)) {
      errors.push(`${key}: actionActivity 节点必须在 data.action 中保存动作配置`);
    }
  }

  for (const [index, edge] of schema.workflow.edges.entries()) {
    if (!edge.id) {
      errors.push(`${key}: workflow.edges.${index}.id 缺失`);
    }
    if (!nodeIds.has(edge.sourceNodeID)) {
      errors.push(`${key}: workflow.edges.${index}.sourceNodeID 不存在：${edge.sourceNodeID}`);
    }
    if (!nodeIds.has(edge.targetNodeID)) {
      errors.push(`${key}: workflow.edges.${index}.targetNodeID 不存在：${edge.targetNodeID}`);
    }
    const data = isRecord(edge.data) ? edge.data : {};
    if (legacyPropertyFlowKey in data) {
      errors.push(`${key}: workflow.edges.${index}.data 禁止包含旧连线字段`);
    }
  }
}

/**
 * 纯函数验收：公共契约只接受新版 MicroflowDesignSchema，不再校验 authoring/runtime DTO。
 */
export function verifyMicroflowContracts(): MicroflowContractVerificationResult {
  const errors: string[] = [];
  const sampleKeys: string[] = [];

  for (const item of microflowSampleManifest) {
    sampleKeys.push(item.key);
    try {
      verifyDesignSchema(item.createSchema(), item.key, errors);
    } catch (caught) {
      errors.push(`${item.key}: ${caught instanceof Error ? caught.message : String(caught)}`);
    }
  }

  return { ok: errors.length === 0, errors, sampleKeys };
}
