// @vitest-environment jsdom

import type { ReactNode } from "react";
import { act } from "react-dom/test-utils";
import ReactDOM from "react-dom/client";
import { describe, expect, it, vi } from "vitest";

vi.mock("@douyinfe/semi-ui", () => ({
  Tag: ({
    children,
    color,
    "data-testid": testId
  }: {
    children: ReactNode;
    color?: string;
    "data-testid"?: string;
  }) => (
    <span data-testid={testId ?? "mock-tag"} data-color={color ?? ""}>
      {children}
    </span>
  )
}));

import { StateBadge } from "./semi-state-badge";

describe("StateBadge", () => {
  it("默认 neutral 灰色", () => {
    const container = document.createElement("div");
    const root = ReactDOM.createRoot(container);

    act(() => {
      root.render(<StateBadge testId="badge-default">未启动</StateBadge>);
    });

    const node = container.querySelector('[data-testid="badge-default"]') as HTMLElement | null;
    expect(node?.dataset.color).toBe("grey");
    expect(node?.textContent).toBe("未启动");
  });

  it.each([
    ["success", "green"],
    ["info", "blue"],
    ["warning", "amber"],
    ["danger", "red"],
    ["neutral", "grey"]
  ] as const)("variant=%s 映射 Semi color=%s", (variant, expectedColor) => {
    const container = document.createElement("div");
    const root = ReactDOM.createRoot(container);

    act(() => {
      root.render(
        <StateBadge variant={variant} testId={`badge-${variant}`}>
          {variant}
        </StateBadge>
      );
    });

    const node = container.querySelector(`[data-testid="badge-${variant}"]`) as HTMLElement | null;
    expect(node?.dataset.color).toBe(expectedColor);
  });
});
