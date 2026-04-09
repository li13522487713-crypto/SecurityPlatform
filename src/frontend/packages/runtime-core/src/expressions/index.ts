export function evaluateExpression(
  expression: string,
  context: Record<string, unknown>
): unknown {
  if (!expression) return undefined;
  return context[expression];
}
