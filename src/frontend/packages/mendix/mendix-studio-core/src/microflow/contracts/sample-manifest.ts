import { createLargeMicroflowSample } from "@atlas/microflow/performance";
import { sampleMicroflowSchema, type MicroflowAuthoringSchema } from "@atlas/microflow/schema";
import {
  microflowSampleSchemas,
  sampleApprovalFlowMicroflowSchema,
  sampleListProcessingMicroflowSchema,
  sampleLoopProcessingMicroflowSchema,
  sampleObjectTypeDecisionMicroflowSchema,
  sampleRestErrorHandlingMicroflowSchema
} from "@atlas/microflow/schema/samples";

export interface MicroflowSampleManifestItem {
  key: string;
  title: string;
  description: string;
  category: "basic" | "integration" | "loop" | "workflow" | "validation" | "large";
  createSchema: () => MicroflowAuthoringSchema;
  expectedValidation?: { errors: number; warnings: number };
}

const order = microflowSampleSchemas.find(item => item.key === "orderProcessing");
const approval = microflowSampleSchemas.find(item => item.key === "approval");
const rest = microflowSampleSchemas.find(item => item.key === "restErrorHandling");
const loop = microflowSampleSchemas.find(item => item.key === "loopProcessing");
const otd = microflowSampleSchemas.find(item => item.key === "objectTypeDecision");
const list = microflowSampleSchemas.find(item => item.key === "listProcessing");

if (!order || !approval || !rest || !loop || !otd || !list) {
  throw new Error("microflow: microflowSampleSchemas 缺少内置样例项");
}

/** 可验收的样例集合（与 microflow 包内 microflowSampleSchemas 一致，并含大规模生成器）。 */
export const microflowSampleManifest: MicroflowSampleManifestItem[] = [
  {
    key: "sample-order-processing",
    title: order.title,
    description: "Order Processing：主路径覆盖检索、判断、循环、错误分支与注释线。",
    category: "basic",
    createSchema: () => sampleMicroflowSchema
  },
  {
    key: "sample-approval-flow",
    title: approval.title,
    description: "审批类工作流动作串联示例。",
    category: "workflow",
    createSchema: () => sampleApprovalFlowMicroflowSchema
  },
  {
    key: "sample-rest-error-handling",
    title: rest.title,
    description: "REST 与日志、错误线示例。",
    category: "integration",
    createSchema: () => sampleRestErrorHandlingMicroflowSchema
  },
  {
    key: "sample-loop-processing",
    title: loop.title,
    description: "含循环子画布与 list 变量路径。",
    category: "loop",
    createSchema: () => sampleLoopProcessingMicroflowSchema
  },
  {
    key: "sample-object-type-decision",
    title: otd.title,
    description: "对象类型分支与 cast。",
    category: "validation",
    createSchema: () => sampleObjectTypeDecisionMicroflowSchema
  },
  {
    key: "sample-list-processing",
    title: list.title,
    description: "列表类动作聚合与运算。",
    category: "basic",
    createSchema: () => sampleListProcessingMicroflowSchema
  },
  {
    key: "sample-large-100-nodes",
    title: "Large 100+ nodes",
    description: "性能与缩放验收用，约 120 个节点串联。",
    category: "large",
    createSchema: () => createLargeMicroflowSample(120)
  }
];
