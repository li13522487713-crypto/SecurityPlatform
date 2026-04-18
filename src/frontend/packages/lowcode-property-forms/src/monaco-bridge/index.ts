/**
 * Monaco LSP 适配桥（M05 C05-6）。
 *
 * 把 lowcode-expression/monaco 的 4 能力（diagnostics / completion / hover / write-warnings）
 * 暴露给属性面板内的"表达式输入框"控件。
 *
 * 实际 monaco-editor 实例 attach 由 M07 lowcode-studio-web 在 Studio 内统一注册（避免每个属性面板各自加载 monaco）。
 */

export {
  computeDiagnostics,
  computeCompletionItems,
  computeHover,
  computeWriteWarnings,
  type MonacoMarker,
  type MonacoCompletionItem,
  type MonacoHover
} from '@atlas/lowcode-expression/monaco';
export type { ExpressionIndex } from '@atlas/lowcode-expression/inference';
