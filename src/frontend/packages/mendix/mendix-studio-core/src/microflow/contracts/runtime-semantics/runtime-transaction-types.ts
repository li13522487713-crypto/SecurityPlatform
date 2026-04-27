/**
 * 事务执行期视图；不承载真实 ORM/连接状态，仅供后端按契约实现。
 * 与 runtime-transaction-contract.md 一致。
 */
export interface MicroflowRuntimeTransactionContext {
  id: string;
  mode: "none" | "singleRunTransaction" | "actionScoped" | "custom";
  status: "none" | "active" | "committed" | "rolledBack" | "failed";
  changedObjects: MicroflowRuntimeChangedObject[];
  committedObjects: MicroflowRuntimeCommittedObject[];
  rolledBackObjects: MicroflowRuntimeRolledBackObject[];
  deletedObjects: MicroflowRuntimeChangedObject[];
  savepoints: MicroflowRuntimeSavepoint[];
  logs: MicroflowRuntimeTransactionLogEntry[];
  diagnostics: MicroflowRuntimeTransactionDiagnostic[];
}

export interface MicroflowRuntimeChangedObject {
  id: string;
  variableName?: string;
  entityQualifiedName?: string;
  objectId: string;
  operation: "create" | "update" | "delete" | "rollback" | "commit";
  status: "staged" | "committed" | "rolledBack" | "failed";
  preview?: string;
}

export interface MicroflowRuntimeCommittedObject {
  changeId: string;
  operation: string;
  objectId: string;
  variableName?: string;
  preview?: string;
}

export interface MicroflowRuntimeRolledBackObject {
  changeId: string;
  operation: string;
  objectId: string;
  variableName?: string;
  preview?: string;
}

export interface MicroflowRuntimeSavepoint {
  id: string;
  name: string;
  createdAt: string;
  operationIndex: number;
  changedObjectCount: number;
  variableSnapshotId?: string;
  transactionStatus: string;
}

export interface MicroflowRuntimeTransactionLogEntry {
  id: string;
  timestamp: string;
  level: "trace" | "debug" | "info" | "warning" | "error";
  operation: string;
  objectId?: string;
  actionId?: string;
  entityQualifiedName?: string;
  runtimeObjectId?: string;
  message: string;
}

export interface MicroflowRuntimeTransactionDiagnostic {
  code: string;
  severity: string;
  message: string;
  objectId?: string;
  actionId?: string;
  transactionId?: string;
}
