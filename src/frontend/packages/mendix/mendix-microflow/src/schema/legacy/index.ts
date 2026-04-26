/**
 * Legacy demo graph types and migration helpers. Do not import from app-web.
 * Production code should use {@link MicroflowAuthoringSchema} and {@link normalizeMicroflowSchema} when loading persisted JSON.
 */
export type {
  LegacyMicroflowAnnotationEdge,
  LegacyMicroflowAnnotationNode,
  LegacyMicroflowActivityConfig,
  LegacyMicroflowActivityNode,
  LegacyMicroflowDecisionConditionEdge,
  LegacyMicroflowDecisionNode,
  LegacyMicroflowEdge,
  LegacyMicroflowEdgeBase,
  LegacyMicroflowErrorHandlerEdge,
  LegacyMicroflowEventNode,
  LegacyMicroflowGraphSchema,
  LegacyMicroflowLoopNode,
  LegacyMicroflowMergeNode,
  LegacyMicroflowNode,
  LegacyMicroflowNodeBase,
  LegacyMicroflowNodeKind,
  LegacyMicroflowNodeType,
  LegacyMicroflowObjectTypeConditionEdge,
  LegacyMicroflowObjectTypeDecisionNode,
  LegacyMicroflowParameterNode,
  LegacyMicroflowSequenceEdge
} from "../types";

export {
  assertAuthoringSchema,
  CURRENT_AUTHORING_SCHEMA_VERSION,
  isLegacyMicroflowSchema,
  migrateLegacyMicroflowSchema,
  normalizeMicroflowSchema
} from "./legacy-migration";
