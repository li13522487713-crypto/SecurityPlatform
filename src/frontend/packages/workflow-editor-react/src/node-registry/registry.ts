import type { NodeTemplateMetadata, NodeTypeMetadata, WorkflowNodeTypeKey } from "../types";
import { normalizeNodeTypeKey } from "../types";
import { createNodeDefinition } from "./definitions";
import type { NodeDefinition } from "./types";

export interface NodeMetadataBundle {
  nodeTypesMap: Map<WorkflowNodeTypeKey, NodeTypeMetadata>;
  templatesMap: Map<WorkflowNodeTypeKey, NodeTemplateMetadata>;
}

function createAllNodeTypes(): WorkflowNodeTypeKey[] {
  return [
    "Entry",
    "Exit",
    "Llm",
    "Plugin",
    "Agent",
    "IntentDetector",
    "QuestionAnswer",
    "Selector",
    "SubWorkflow",
    "TextProcessor",
    "Loop",
    "Batch",
    "Break",
    "Continue",
    "InputReceiver",
    "OutputEmitter",
    "AssignVariable",
    "VariableAssignerWithinLoop",
    "VariableAggregator",
    "KnowledgeRetriever",
    "KnowledgeIndexer",
    "KnowledgeDeleter",
    "Ltm",
    "DatabaseQuery",
    "DatabaseInsert",
    "DatabaseUpdate",
    "DatabaseDelete",
    "DatabaseCustomSql",
    "CreateConversation",
    "ConversationList",
    "ConversationUpdate",
    "ConversationDelete",
    "ConversationHistory",
    "ClearConversationHistory",
    "MessageList",
    "CreateMessage",
    "EditMessage",
    "DeleteMessage",
    "HttpRequester",
    "CodeRunner",
    "JsonSerialization",
    "JsonDeserialization",
    "Comment"
  ];
}

export class NodeRegistry {
  private readonly definitions = new Map<WorkflowNodeTypeKey, NodeDefinition>();

  constructor() {
    for (const nodeType of createAllNodeTypes()) {
      this.definitions.set(nodeType, createNodeDefinition(nodeType));
    }
  }

  resolve(rawType: string): NodeDefinition {
    const type = normalizeNodeTypeKey(rawType);
    const definition = this.definitions.get(type);
    if (definition) {
      return definition;
    }
    return createNodeDefinition("TextProcessor");
  }

  getAllTypes(): WorkflowNodeTypeKey[] {
    return [...this.definitions.keys()];
  }
}

export function createMetadataBundle(
  nodeTypes: NodeTypeMetadata[] | undefined,
  nodeTemplates: NodeTemplateMetadata[] | undefined
): NodeMetadataBundle {
  const nodeTypesMap = new Map<WorkflowNodeTypeKey, NodeTypeMetadata>();
  const templatesMap = new Map<WorkflowNodeTypeKey, NodeTemplateMetadata>();

  if (nodeTypes) {
    for (const item of nodeTypes) {
      const type = normalizeNodeTypeKey(String(item.key));
      nodeTypesMap.set(type, item);
    }
  }
  if (nodeTemplates) {
    for (const item of nodeTemplates) {
      const type = normalizeNodeTypeKey(String(item.key));
      templatesMap.set(type, item);
    }
  }
  return { nodeTypesMap, templatesMap };
}

export function mergeNodeDefaults(
  definition: NodeDefinition,
  template: NodeTemplateMetadata | undefined,
  existing: Record<string, unknown>
): Record<string, unknown> {
  return {
    ...definition.getFallbackDefaults(),
    ...(template?.defaultConfig ?? {}),
    ...existing
  };
}

