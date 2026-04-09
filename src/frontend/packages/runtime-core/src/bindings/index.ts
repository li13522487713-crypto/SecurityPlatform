export interface RuntimeBinding {
  source: string;
  target: string;
  expression?: string;
}

export function resolveBindings(bindings: RuntimeBinding[], payload: Record<string, unknown>) {
  return bindings.map((binding) => ({
    ...binding,
    value: payload[binding.source]
  }));
}
