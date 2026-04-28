// @vitest-environment jsdom

import type { ReactNode, CSSProperties } from "react";
import { act } from "react-dom/test-utils";
import ReactDOM from "react-dom/client";
import { describe, expect, it, vi } from "vitest";

vi.mock("@douyinfe/semi-ui", () => {
  const Card = ({
    children,
    bodyStyle,
    "data-testid": testId
  }: {
    children: ReactNode;
    bodyStyle?: CSSProperties;
    "data-testid"?: string;
  }) => (
    <div data-testid={testId ?? "mock-card"} data-padding={String(bodyStyle?.padding ?? "")}>
      {children}
    </div>
  );

  return {
    Card,
    Typography: {
      Title: ({ children }: { children: ReactNode }) => <h3 data-testid="mock-title">{children}</h3>,
      Text: ({ children }: { children: ReactNode }) => <span data-testid="mock-text">{children}</span>
    }
  };
});

import { FormCard } from "./semi-form-card";

describe("FormCard", () => {
  it("渲染 title / subtitle / headerExtra / actions / children", () => {
    const container = document.createElement("div");
    const root = ReactDOM.createRoot(container);

    act(() => {
      root.render(
        <FormCard
          title="标题"
          subtitle="副标题"
          headerExtra={<span data-testid="extra">extra</span>}
          actions={<button data-testid="submit">submit</button>}
          testId="form-card"
        >
          <div data-testid="body">body</div>
        </FormCard>
      );
    });

    expect(container.querySelector('[data-testid="form-card"]')).not.toBeNull();
    expect(container.querySelector('[data-testid="mock-title"]')?.textContent).toBe("标题");
    expect(container.querySelector('[data-testid="mock-text"]')?.textContent).toBe("副标题");
    expect(container.querySelector('[data-testid="extra"]')).not.toBeNull();
    expect(container.querySelector('[data-testid="submit"]')).not.toBeNull();
    expect(container.querySelector('[data-testid="body"]')).not.toBeNull();
  });

  it("无 title/subtitle/headerExtra 时不渲染 header 区，无 actions 时不渲染 actions 区", () => {
    const container = document.createElement("div");
    const root = ReactDOM.createRoot(container);

    act(() => {
      root.render(
        <FormCard testId="form-card-bare">
          <div data-testid="body">body</div>
        </FormCard>
      );
    });

    expect(container.querySelector('[data-testid="mock-title"]')).toBeNull();
    expect(container.querySelector('[data-testid="mock-text"]')).toBeNull();
    expect(container.querySelector('[data-testid="body"]')).not.toBeNull();
  });
});
