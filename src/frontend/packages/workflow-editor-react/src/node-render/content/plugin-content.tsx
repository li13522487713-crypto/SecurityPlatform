interface PluginContentProps {
  pluginId?: string;
  action?: string;
}

export function PluginContent(props: PluginContentProps) {
  return (
    <div className="wf-node-render-content">
      <div className="wf-node-render-kv">插件: {props.pluginId || "-"}</div>
      <div className="wf-node-render-kv">动作: {props.action || "-"}</div>
    </div>
  );
}

