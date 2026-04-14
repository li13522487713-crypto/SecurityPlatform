// @vitest-environment jsdom
import { act, useEffect } from "react";
import { createRoot, type Root } from "react-dom/client";
import { describe, expect, it } from "vitest";
import { FloatLayoutHolder, FloatLayoutProvider, useFloatLayoutService } from "./index";

interface RenderMetrics {
  renderCount: number;
  effectCount: number;
}

function LayoutEffectConsumer(props: { selectedNodeKey: string; metrics: RenderMetrics }) {
  const { rightPanel, open, close } = useFloatLayoutService();
  props.metrics.renderCount += 1;

  useEffect(() => {
    props.metrics.effectCount += 1;
    if (props.selectedNodeKey) {
      open("NodeForm", { nodeKey: props.selectedNodeKey });
      return;
    }
    close("NodeForm");
  }, [close, open, props.metrics, props.selectedNodeKey]);

  return (
    <>
      <div data-testid="layout-payload">
        {rightPanel?.key === "NodeForm" ? ((rightPanel.payload as { nodeKey?: string } | undefined)?.nodeKey ?? "") : ""}
      </div>
      <FloatLayoutHolder nodeForm={<div data-testid="node-form">node form</div>} />
    </>
  );
}

describe("FloatLayoutProvider", () => {
  it("keeps layout actions stable when a consumer syncs NodeForm with selection", async () => {
    (globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

    const metrics: RenderMetrics = { renderCount: 0, effectCount: 0 };
    const container = document.createElement("div");
    document.body.appendChild(container);
    let root: Root | undefined;

    await act(async () => {
      root = createRoot(container);
      root.render(
        <FloatLayoutProvider>
          <LayoutEffectConsumer selectedNodeKey="" metrics={metrics} />
        </FloatLayoutProvider>
      );
    });

    expect(container.querySelector('[data-testid="node-form"]')).toBeNull();
    expect(container.querySelector('[data-testid="layout-payload"]')?.textContent).toBe("");
    expect(metrics.effectCount).toBe(1);

    await act(async () => {
      root?.render(
        <FloatLayoutProvider>
          <LayoutEffectConsumer selectedNodeKey="node-1" metrics={metrics} />
        </FloatLayoutProvider>
      );
    });

    expect(container.querySelector('[data-testid="node-form"]')?.textContent).toBe("node form");
    expect(container.querySelector('[data-testid="layout-payload"]')?.textContent).toBe("node-1");
    expect(metrics.effectCount).toBe(2);
    expect(metrics.renderCount).toBeLessThan(8);

    await act(async () => {
      root?.render(
        <FloatLayoutProvider>
          <LayoutEffectConsumer selectedNodeKey="" metrics={metrics} />
        </FloatLayoutProvider>
      );
    });

    expect(container.querySelector('[data-testid="node-form"]')).toBeNull();
    expect(container.querySelector('[data-testid="layout-payload"]')?.textContent).toBe("");
    expect(metrics.effectCount).toBe(3);
    expect(metrics.renderCount).toBeLessThan(12);

    await act(async () => {
      root?.unmount();
    });
    container.remove();
  });
});
