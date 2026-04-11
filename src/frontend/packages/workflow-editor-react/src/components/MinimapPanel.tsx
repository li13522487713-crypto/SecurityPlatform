interface MinimapPanelProps {
  visible: boolean;
  nodes: Array<{ key: string; x: number; y: number }>;
  selectedNodeKey?: string;
}

export function MinimapPanel(props: MinimapPanelProps) {
  if (!props.visible) {
    return null;
  }

  const maxX = Math.max(...props.nodes.map((item) => item.x), 1);
  const maxY = Math.max(...props.nodes.map((item) => item.y), 1);
  return (
    <div className="wf-react-minimap-panel">
      <div className="wf-react-minimap-title">Minimap</div>
      <div className="wf-react-minimap-canvas">
        {props.nodes.map((node) => (
          <span
            key={node.key}
            className={`wf-react-minimap-node${props.selectedNodeKey === node.key ? " wf-react-minimap-node-active" : ""}`}
            style={{
              left: `${(node.x / (maxX + 200)) * 100}%`,
              top: `${(node.y / (maxY + 200)) * 100}%`
            }}
          />
        ))}
      </div>
    </div>
  );
}
