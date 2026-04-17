/**
 * inputMapping / outputMapping 工具（M09 C09-2 / C09-5）。
 *
 * - inputMapping：把组件 BindingSchema → 工作流 inputName → JSON 值（执行 jsonata）
 * - outputMapping：把工作流 outputs → 目标变量路径或组件 prop 路径 → 产出 RuntimeStatePatch[]
 *
 * 与 docs/lowcode-binding-matrix.md 模式 A/B 黄金样本完全一致。
 */

import type { BindingSchema, JsonValue, RuntimeStatePatch } from '@atlas/lowcode-schema';
import { isStaticBinding, inferScopeRoot } from '@atlas/lowcode-schema';
import { evaluate as evalExpression } from '@atlas/lowcode-expression/jsonata';

/** 解析单个 BindingSchema → 实际值（与 dispatcher 行为一致）。*/
export async function resolveBinding(binding: BindingSchema, scope: JsonValue): Promise<JsonValue> {
  if (isStaticBinding(binding)) return binding.value;
  if (binding.sourceType === 'variable') {
    return (await evalExpression(binding.path, scope)) as JsonValue;
  }
  if (binding.sourceType === 'expression') {
    return (await evalExpression(binding.expression, scope)) as JsonValue;
  }
  // workflow_output / chatflow_output 通常作为另一个工作流的输出来源；
  // 此处保守返回 fallback。
  return (binding.fallback ?? null) as JsonValue;
}

/** 把 inputMapping(BindingSchema 字典) → 工作流 inputs(JSON 值字典)。*/
export async function buildInputs(
  inputMapping: Record<string, BindingSchema> | undefined,
  scope: JsonValue
): Promise<Record<string, JsonValue>> {
  const out: Record<string, JsonValue> = {};
  if (!inputMapping) return out;
  for (const [key, binding] of Object.entries(inputMapping)) {
    out[key] = await resolveBinding(binding, scope);
  }
  return out;
}

/**
 * 将工作流 outputs 按 outputMapping(jsonata path → 目标路径) 转为 RuntimeStatePatch[]。
 *
 * outputMapping 例：
 *   { 'users': 'page.tableData', 'count': 'app.userCount' }
 * 含义：把 outputs.users 写到 page.tableData，outputs.count 写到 app.userCount。
 *
 * 当目标路径以 component.<id>.<prop> 开头时，op=set 写组件 prop（与 update_component 等价语义）。
 */
export async function applyOutputMapping(
  outputs: Record<string, JsonValue> | undefined,
  outputMapping: Record<string, string> | undefined
): Promise<RuntimeStatePatch[]> {
  if (!outputs || !outputMapping) return [];
  const patches: RuntimeStatePatch[] = [];
  for (const [outputJsonataPath, targetPath] of Object.entries(outputMapping)) {
    const value = (await evalExpression(outputJsonataPath, outputs as JsonValue)) as JsonValue;
    const scope = inferScopeRoot(targetPath);
    if (!scope) continue;
    if (scope === 'component') {
      // component.<id>.<prop>
      const rest = targetPath.slice('component.'.length);
      const idx = rest.indexOf('.');
      const id = idx === -1 ? rest : rest.slice(0, idx);
      patches.push({ scope: 'component', componentId: id, path: targetPath, op: 'set', value });
    } else if (scope === 'page' || scope === 'app') {
      patches.push({ scope, path: targetPath, op: 'set', value });
    }
    // 其它只读作用域：忽略（应在编辑期被属性面板拒绝）
  }
  return patches;
}
