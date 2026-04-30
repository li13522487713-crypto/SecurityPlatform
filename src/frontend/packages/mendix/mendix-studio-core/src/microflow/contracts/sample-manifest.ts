import type { MicroflowDesignSchema } from "@atlas/microflow/schema";

export interface MicroflowSampleManifestItem {
  key: string;
  title: string;
  description: string;
  category: "basic" | "integration" | "loop" | "workflow" | "validation" | "large";
  createSchema: () => MicroflowDesignSchema;
}

function createDesignSample(key: string, nodeCount = 2): MicroflowDesignSchema {
  const nodes = Array.from({ length: Math.max(2, nodeCount) }, (_, index) => {
    const isStart = index === 0;
    const isEnd = index === Math.max(2, nodeCount) - 1;
    const id = isStart ? "start" : isEnd ? "end" : `action-${index}`;
    const type = isStart ? "startEvent" : isEnd ? "endEvent" : "actionActivity";
    return {
      id,
      type,
      data: {
        objectId: id,
        objectKind: type,
        officialType: `Microflows$${type}`,
        title: isStart ? "Start" : isEnd ? "End" : `Action ${index}`,
        collectionId: "root-collection",
        ...(type === "actionActivity"
          ? {
              actionKind: "logMessage",
              action: {
                id: `${id}-action`,
                kind: "logMessage",
                officialType: "Microflows$LogMessageAction",
                level: "info",
                template: { text: `"${id}"`, arguments: [] }
              }
            }
          : {})
      },
      meta: {
        nodeDTOType: "microflow",
        collectionId: "root-collection",
        position: { x: 160 + index * 180, y: 160 },
        size: { width: 180, height: 56 }
      }
    };
  });

  const edges = nodes.slice(0, -1).map((node, index) => ({
    id: `edge-${index + 1}`,
    sourceNodeID: node.id,
    targetNodeID: nodes[index + 1].id,
    data: {
      flowId: `edge-${index + 1}`,
      flowKind: "sequence",
      edgeKind: "sequence",
      collectionId: "root-collection",
      isErrorHandler: false,
      caseValues: []
    }
  }));

  return {
    schemaVersion: "flowgram.microflow.v1",
    id: key,
    name: key,
    displayName: key,
    moduleId: "sample-module",
    moduleName: "Sample",
    parameters: [],
    returnType: { kind: "void" },
    workflow: { nodes, edges },
    editor: { viewport: { x: 0, y: 0, zoom: 1 }, zoom: 1, selection: {} },
    variables: [],
    validation: { issues: [] },
    audit: { version: "0.1.0", status: "draft" }
  } as MicroflowDesignSchema;
}

/** 可验收的新版设计态样例集合。 */
export const microflowSampleManifest: MicroflowSampleManifestItem[] = [
  {
    key: "sample-basic-design",
    title: "Basic Design",
    description: "新版 workflow.nodes/workflow.edges 基础样例。",
    category: "basic",
    createSchema: () => createDesignSample("sample-basic-design")
  },
  {
    key: "sample-action-chain-design",
    title: "Action Chain Design",
    description: "新版节点 data.action 直接承载动作配置。",
    category: "workflow",
    createSchema: () => createDesignSample("sample-action-chain-design", 5)
  },
  {
    key: "sample-validation-design",
    title: "Validation Design",
    description: "新版端点引用校验样例。",
    category: "validation",
    createSchema: () => createDesignSample("sample-validation-design", 3)
  },
  {
    key: "sample-integration-design",
    title: "Integration Design",
    description: "新版动作配置直写节点数据样例。",
    category: "integration",
    createSchema: () => createDesignSample("sample-integration-design", 4)
  },
  {
    key: "sample-loop-design",
    title: "Loop Design",
    description: "新版循环节点字段样例。",
    category: "loop",
    createSchema: () => createDesignSample("sample-loop-design", 4)
  },
  {
    key: "sample-workflow-design",
    title: "Workflow Design",
    description: "新版工作流链路样例。",
    category: "workflow",
    createSchema: () => createDesignSample("sample-workflow-design", 6)
  },
  {
    key: "sample-large-design",
    title: "Large Design",
    description: "新版协议大画布样例。",
    category: "large",
    createSchema: () => createDesignSample("sample-large-design", 120)
  }
];
