import type { MicroflowDesignSchema, MicroflowWorkflowNodeJSON } from "../types";

const DEFAULT_VERTICAL_OFFSET_PX = 96;
const DEFAULT_HORIZONTAL_SPACING_PX = 120;

function nodeKind(node: MicroflowWorkflowNodeJSON): string {
  const data = node.data as { objectKind?: unknown } | undefined;
  return String(data?.objectKind ?? node.type ?? "");
}

function parentObjectId(node: MicroflowWorkflowNodeJSON): string | undefined {
  const data = node.data as { parentObjectId?: unknown } | undefined;
  const dataParent = typeof data?.parentObjectId === "string" ? data.parentObjectId : undefined;
  const metaParent = typeof node.meta?.parentObjectId === "string" ? node.meta.parentObjectId : undefined;
  return dataParent ?? metaParent;
}

function isRootNode(node: MicroflowWorkflowNodeJSON): boolean {
  return !parentObjectId(node);
}

function nodePosition(node: MicroflowWorkflowNodeJSON): { x: number; y: number } {
  return {
    x: Number(node.meta?.position?.x ?? 0),
    y: Number(node.meta?.position?.y ?? 0),
  };
}

function parameterId(node: MicroflowWorkflowNodeJSON): string | undefined {
  const data = node.data as { parameterId?: unknown } | undefined;
  return typeof data?.parameterId === "string" && data.parameterId.trim().length > 0
    ? data.parameterId
    : undefined;
}

export function alignRootDesignParameterNodesToStart(schema: MicroflowDesignSchema): MicroflowDesignSchema {
  const startNode = schema.workflow.nodes.find(node => isRootNode(node) && nodeKind(node) === "startEvent");
  if (!startNode) {
    return schema;
  }
  const parameterNodes = schema.workflow.nodes.filter(node => isRootNode(node) && nodeKind(node) === "parameterObject");
  if (parameterNodes.length === 0) {
    return schema;
  }
  const position = nodePosition(startNode);
  const parameterOrder = new Map(schema.parameters.map((parameter, index) => [parameter.id, index]));
  const sortedNodes = [...parameterNodes].sort((a, b) => {
    const aOrder = parameterOrder.get(parameterId(a) ?? "") ?? Number.MAX_SAFE_INTEGER;
    const bOrder = parameterOrder.get(parameterId(b) ?? "") ?? Number.MAX_SAFE_INTEGER;
    if (aOrder !== bOrder) {
      return aOrder - bOrder;
    }
    return nodePosition(a).x - nodePosition(b).x;
  });
  const leftX = position.x - ((sortedNodes.length - 1) * DEFAULT_HORIZONTAL_SPACING_PX) / 2;
  const y = position.y - DEFAULT_VERTICAL_OFFSET_PX;
  const desiredPositionByNodeId = new Map<string, { x: number; y: number }>();
  for (let index = 0; index < sortedNodes.length; index += 1) {
    desiredPositionByNodeId.set(sortedNodes[index].id, {
      x: leftX + index * DEFAULT_HORIZONTAL_SPACING_PX,
      y,
    });
  }

  let changed = false;
  const nextNodes = schema.workflow.nodes.map(node => {
    const desired = desiredPositionByNodeId.get(node.id);
    if (!desired) {
      return node;
    }
    const current = nodePosition(node);
    if (current.x === desired.x && current.y === desired.y) {
      return node;
    }
    changed = true;
    return {
      ...node,
      meta: {
        ...(node.meta ?? {}),
        position: desired,
      },
    };
  });
  if (!changed) {
    return schema;
  }
  return {
    ...schema,
    workflow: {
      ...schema.workflow,
      nodes: nextNodes,
    },
  };
}

export function removeStaleDesignParameters(schema: MicroflowDesignSchema): MicroflowDesignSchema {
  const referenced = new Set(
    schema.workflow.nodes
      .filter(node => nodeKind(node) === "parameterObject")
      .map(node => parameterId(node))
      .filter((value): value is string => Boolean(value)),
  );
  const nextParameters = schema.parameters.filter(parameter => referenced.has(parameter.id));
  if (nextParameters.length === schema.parameters.length) {
    return schema;
  }
  return {
    ...schema,
    parameters: nextParameters,
  };
}

