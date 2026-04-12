import type { Container } from "inversify";
import { createContext, useContext, type PropsWithChildren } from "react";

const WorkflowEditorContainerContext = createContext<Container | null>(null);

export function WorkflowEditorContainerProvider(props: PropsWithChildren<{ container: Container }>) {
  return <WorkflowEditorContainerContext.Provider value={props.container}>{props.children}</WorkflowEditorContainerContext.Provider>;
}

export function useService<T>(symbol: symbol): T {
  const container = useContext(WorkflowEditorContainerContext);
  if (!container) {
    throw new Error("WorkflowEditorContainerProvider is missing.");
  }
  return container.get<T>(symbol);
}
