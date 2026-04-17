/**
 * action-runtime scope-guard（M03 C03-5）。
 *
 * 与 M02 表达式引擎 ensureWritablePath 双层校验：
 * - 表达式引擎在编辑期阻止 set_variable.targetPath 引用只读作用域。
 * - 动作运行时在执行期再次校验，作为第二道防线。
 */

export { ensureWritablePath, ScopeViolationError } from '@atlas/lowcode-expression/scope';
