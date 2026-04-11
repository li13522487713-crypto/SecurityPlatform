interface PortsProps {
  inputs: Array<string | number>;
  outputs: Array<string | number>;
}

function renderPort(kind: "input" | "output", portId: string | number) {
  return (
    <span
      key={`${kind}-${String(portId)}`}
      className={`wf-node-render-port wf-node-render-port-${kind}`}
      data-port-id={String(portId)}
      data-port-key={String(portId)}
      data-port-type={kind}
    />
  );
}

export function NodeRenderPorts(props: PortsProps) {
  return (
    <>
      <div className="wf-node-render-inputs">{props.inputs.map((port) => renderPort("input", port))}</div>
      <div className="wf-node-render-outputs">{props.outputs.map((port) => renderPort("output", port))}</div>
    </>
  );
}
