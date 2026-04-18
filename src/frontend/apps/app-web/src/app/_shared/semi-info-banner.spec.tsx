// @vitest-environment jsdom

import type { ReactNode } from "react";
import { act } from "react-dom/test-utils";
import ReactDOM from "react-dom/client";
import { describe, expect, it, vi } from "vitest";

vi.mock("@douyinfe/semi-ui", () => ({
  Banner: ({
    type,
    title,
    description,
    closeIcon,
    bordered,
    "data-testid": testId
  }: {
    type?: string;
    title?: ReactNode;
    description?: ReactNode;
    closeIcon?: ReactNode;
    bordered?: boolean;
    "data-testid"?: string;
  }) => (
    <div
      data-testid={testId ?? "mock-banner"}
      data-type={type ?? ""}
      data-closable={String(closeIcon !== null)}
      data-bordered={String(Boolean(bordered))}
    >
      {title ? <strong>{title}</strong> : null}
      {description ? <p>{description}</p> : null}
    </div>
  )
}));

import { InfoBanner } from "./semi-info-banner";

describe("InfoBanner", () => {
  it.each([
    ["info", "info"],
    ["warning", "warning"],
    ["danger", "danger"],
    ["success", "success"]
  ] as const)("variant=%s 直接透传给 Semi Banner type=%s", (variant, expectedType) => {
    const container = document.createElement("div");
    const root = ReactDOM.createRoot(container);

    act(() => {
      root.render(
        <InfoBanner variant={variant} title="t" description="d" testId={`banner-${variant}`} />
      );
    });

    const node = container.querySelector(`[data-testid="banner-${variant}"]`) as HTMLElement | null;
    expect(node?.dataset.type).toBe(expectedType);
    expect(node?.textContent).toContain("t");
    expect(node?.textContent).toContain("d");
  });

  it("closable=false（默认）时关闭图标置空", () => {
    const container = document.createElement("div");
    const root = ReactDOM.createRoot(container);

    act(() => {
      root.render(<InfoBanner testId="b1">x</InfoBanner>);
    });

    const node = container.querySelector('[data-testid="b1"]') as HTMLElement | null;
    expect(node?.dataset.closable).toBe("false");
  });

  it("compact=true 时关闭 bordered", () => {
    const container = document.createElement("div");
    const root = ReactDOM.createRoot(container);

    act(() => {
      root.render(
        <InfoBanner compact testId="b2">
          x
        </InfoBanner>
      );
    });

    const node = container.querySelector('[data-testid="b2"]') as HTMLElement | null;
    expect(node?.dataset.bordered).toBe("false");
  });

  it("description 不传时回退到 children", () => {
    const container = document.createElement("div");
    const root = ReactDOM.createRoot(container);

    act(() => {
      root.render(<InfoBanner testId="b3">回退内容</InfoBanner>);
    });

    expect(container.querySelector('[data-testid="b3"]')?.textContent).toContain("回退内容");
  });
});
