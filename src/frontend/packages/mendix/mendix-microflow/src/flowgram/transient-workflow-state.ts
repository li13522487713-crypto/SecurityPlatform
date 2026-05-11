import type { WorkflowJSON } from "@flowgram-adapter/free-layout-editor";

import type {
  MicroflowDesignSchema,
  MicroflowWorkflowEdgeJSON,
  MicroflowWorkflowJSON,
  MicroflowWorkflowNodeJSON,
} from "../schema/types";

const transientNodeDataKeys = [
  "inlineConfig",
  "runtimeState",
  "runtimeErrorCode",
  "runtimeErrorMessage",
  "validationState",
  "issueCount",
  "usageSourceHighlight",
  "usageConsumerHighlight",
] as const;

const transientEdgeDataKeys = [
  "runtimeState",
  "validationState",
  "sourceNodeId",
  "sourceObjectKind",
  "sourceActionKind",
  "sourcePortId",
  "targetNodeId",
  "targetObjectKind",
  "targetActionKind",
  "targetPortId",
] as const;

function stripKeys<T extends Record<string, unknown> | undefined>(
  data: T,
  keys: readonly string[],
): Record<string, unknown> | undefined {
  if (!data) {
    return data;
  }
  const next = { ...data };
  for (const key of keys) {
    delete next[key];
  }
  return next;
}

export function stripTransientWorkflowState(workflow: WorkflowJSON | MicroflowWorkflowJSON): WorkflowJSON {
  return {
    ...workflow,
    nodes: ((workflow.nodes ?? []) as MicroflowWorkflowNodeJSON[]).map(node => ({
      ...node,
      data: stripKeys(node.data, transientNodeDataKeys) as MicroflowWorkflowNodeJSON["data"],
    })) as WorkflowJSON["nodes"],
    edges: ((workflow.edges ?? []) as MicroflowWorkflowEdgeJSON[]).map(edge => ({
      ...edge,
      data: stripKeys(edge.data, transientEdgeDataKeys) as MicroflowWorkflowEdgeJSON["data"],
    })) as WorkflowJSON["edges"],
  };
}

export function stripTransientDesignSchema(schema: MicroflowDesignSchema): MicroflowDesignSchema {
  return {
    ...schema,
    workflow: stripTransientWorkflowState(schema.workflow) as MicroflowWorkflowJSON,
  };
}

export function persistentWorkflowSignature(workflow: WorkflowJSON | MicroflowWorkflowJSON): string {
  return JSON.stringify(stripTransientWorkflowState(workflow));
}
