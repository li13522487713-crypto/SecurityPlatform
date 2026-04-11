import clsx from "classnames";

interface WrapperProps {
  selected: boolean;
  children: React.ReactNode;
}

export function NodeRenderWrapper(props: WrapperProps) {
  return <div className={clsx("wf-node-render-wrapper", props.selected && "wf-node-render-wrapper-selected")}>{props.children}</div>;
}
