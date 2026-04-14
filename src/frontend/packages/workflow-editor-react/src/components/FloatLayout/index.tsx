import { createContext, useCallback, useContext, useMemo, useState, type PropsWithChildren, type ReactNode } from "react";

export type LayoutPanelKey = "NodeForm" | "ProblemPanel" | "TracePanel" | "TestRunPanel";

interface FloatLayoutState {
  rightPanel: { key: LayoutPanelKey; payload?: unknown } | null;
  open: (key: LayoutPanelKey, payload?: unknown) => void;
  close: (key?: LayoutPanelKey) => void;
}

const FloatLayoutContext = createContext<FloatLayoutState | null>(null);

export function FloatLayoutProvider(props: PropsWithChildren) {
  const [rightPanel, setRightPanel] = useState<{ key: LayoutPanelKey; payload?: unknown } | null>(null);
  const open = useCallback((key: LayoutPanelKey, payload?: unknown) => {
    setRightPanel({ key, payload });
  }, []);
  const close = useCallback((key?: LayoutPanelKey) => {
    if (!key) {
      setRightPanel(null);
      return;
    }
    setRightPanel((prev) => (prev?.key === key ? null : prev));
  }, []);
  const value = useMemo<FloatLayoutState>(
    () => ({
      rightPanel,
      open,
      close
    }),
    [close, open, rightPanel]
  );
  return <FloatLayoutContext.Provider value={value}>{props.children}</FloatLayoutContext.Provider>;
}

export function useFloatLayoutService() {
  const context = useContext(FloatLayoutContext);
  if (!context) {
    throw new Error("FloatLayoutProvider is missing.");
  }
  return context;
}

export function FloatLayoutHolder(props: {
  nodeForm?: ReactNode;
  problemPanel?: ReactNode;
  tracePanel?: ReactNode;
  testRunPanel?: ReactNode;
}) {
  const layout = useFloatLayoutService();
  return (
    <>
      {layout.rightPanel?.key === "NodeForm" ? props.nodeForm ?? null : null}
      {layout.rightPanel?.key === "ProblemPanel" ? props.problemPanel ?? null : null}
      {layout.rightPanel?.key === "TracePanel" ? props.tracePanel ?? null : null}
      {layout.rightPanel?.key === "TestRunPanel" ? props.testRunPanel ?? null : null}
    </>
  );
}
