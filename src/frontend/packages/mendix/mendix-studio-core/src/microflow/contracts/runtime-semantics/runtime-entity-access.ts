/** 与 runtime-semantics / metadata 文档中 Entity Access 判定一致。 */
export interface MicroflowRuntimeEntityAccessDecision {
  allowed: boolean;
  operation: "read" | "create" | "update" | "delete";
  entityQualifiedName: string;
  reason?: string;
}
