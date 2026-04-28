import { createElement, Fragment, type FC, type PropsWithChildren, type ReactElement } from "react";

/**
 * 包级 tsc 用桩：不跟随 workspace 中 `@coze-workflow/render` 的完整源码图（其 tsconfig/装饰器/less 与本包独立检查不兼容）。
 * 运行时代码仍解析到真实包。
 */
export const WorkflowPortRender: FC<Record<string, unknown>> = () => null;

export function WorkflowRenderProvider({
  children,
  containerModules: _containerModules
}: PropsWithChildren<{
  containerModules?: readonly unknown[];
}>): ReactElement {
  return createElement(Fragment, null, children) as ReactElement;
}
