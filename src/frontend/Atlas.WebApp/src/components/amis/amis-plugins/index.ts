export interface AmisPluginConfig {
  name: string;
  displayName: string;
  group: string;
  icon?: string;
  description?: string;
  schema: Record<string, unknown>;
  editorConfig?: Record<string, unknown>;
}

export interface AmisPluginGroup {
  key: string;
  label: string;
  plugins: AmisPluginConfig[];
}

const registeredPlugins: AmisPluginConfig[] = [];

export function registerAmisPlugin(plugin: AmisPluginConfig): void {
  const exists = registeredPlugins.findIndex((p) => p.name === plugin.name);
  if (exists >= 0) {
    registeredPlugins[exists] = plugin;
  } else {
    registeredPlugins.push(plugin);
  }
}

export function getRegisteredPlugins(): AmisPluginConfig[] {
  return [...registeredPlugins];
}

export function getPluginGroups(): AmisPluginGroup[] {
  const groupMap = new Map<string, AmisPluginConfig[]>();
  for (const plugin of registeredPlugins) {
    const group = plugin.group || "custom";
    if (!groupMap.has(group)) {
      groupMap.set(group, []);
    }
    groupMap.get(group)!.push(plugin);
  }

  return Array.from(groupMap.entries()).map(([key, plugins]) => ({
    key,
    label: key,
    plugins,
  }));
}

export function filterPlugins(keyword: string): AmisPluginConfig[] {
  if (!keyword.trim()) {
    return [...registeredPlugins];
  }
  const lowerKw = keyword.toLowerCase();
  return registeredPlugins.filter(
    (p) =>
      p.name.toLowerCase().includes(lowerKw) ||
      p.displayName.toLowerCase().includes(lowerKw) ||
      (p.description ?? "").toLowerCase().includes(lowerKw),
  );
}
