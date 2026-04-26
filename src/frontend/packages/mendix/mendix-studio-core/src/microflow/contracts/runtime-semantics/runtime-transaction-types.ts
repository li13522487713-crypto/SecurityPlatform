/**
 * 事务执行期视图；不承载真实 ORM/连接状态，仅供后端按契约实现。
 * 与 runtime-transaction-contract.md 一致。
 */
export interface MicroflowRuntimeTransactionContext {
  id: string;
  mode: "singleRunTransaction" | "none" | "custom";
  status: "active" | "committed" | "rolledBack" | "failed";
  changedObjects: MicroflowRuntimeChangedObject[];
  committedObjects: MicroflowRuntimeChangedObject[];
}

export interface MicroflowRuntimeChangedObject {
  variableName?: string;
  entityQualifiedName?: string;
  objectId?: string;
  changeKind: "create" | "update" | "delete" | "rollback";
  preview?: string;
}
