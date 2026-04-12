import clsx from "classnames";

interface WrapperProps {
  selected: boolean;
  children: React.ReactNode;
  onClick?: React.MouseEventHandler<HTMLDivElement>;
}

export function NodeRenderWrapper(props: WrapperProps) {
  return (
    <div
      className={clsx("wf-node-render-wrapper", props.selected && "wf-node-render-wrapper-selected")}
      onClick={props.onClick}
    >
      {props.children}
    </div>
  );
}
