import type { MicroflowVariableIndex } from "@atlas/microflow/schema";

/**
 * 与 runtime-variable-scope-contract.md 对齐的静态索引引用（不替代 VariableIndex 本体）。
 */
export interface RuntimeVariableScopeContractRefs {
  /** Authoring 侧已构建的变量索引；运行时以「存在性检查 + 源类型」为主。 */
  variableIndex: MicroflowVariableIndex;
}
