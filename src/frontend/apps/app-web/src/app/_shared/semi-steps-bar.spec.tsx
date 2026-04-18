// @vitest-environment jsdom

import type { ReactNode } from "react";
import { act } from "react-dom/test-utils";
import ReactDOM from "react-dom/client";
import { describe, expect, it, vi } from "vitest";

vi.mock("@douyinfe/semi-ui", () => {
  const Step = ({ title, description }: { title: string; description?: string }) => (
    <li data-testid={`mock-step-${title}`}>
      <span>{title}</span>
      {description ? <em>{description}</em> : null}
    </li>
  );

  const Steps = ({
    children,
    current,
    status
  }: {
    children: ReactNode;
    current: number;
    status?: string;
  }) => (
    <ol data-testid="mock-steps" data-current={String(current)} data-status={status ?? ""}>
      {children}
    </ol>
  );

  (Steps as unknown as { Step: typeof Step }).Step = Step;
  return { Steps };
});

import { StepsBar } from "./semi-steps-bar";

describe("StepsBar", () => {
  it("渲染所有 step 并透传 current/status", () => {
    const container = document.createElement("div");
    const root = ReactDOM.createRoot(container);

    act(() => {
      root.render(
        <StepsBar
          steps={[{ title: "Step1" }, { title: "Step2", description: "二段" }, { title: "Step3" }]}
          current={1}
          status="process"
          testId="setup-steps"
        />
      );
    });

    const root2 = container.querySelector('[data-testid="setup-steps"]');
    expect(root2).not.toBeNull();
    const steps = container.querySelector('[data-testid="mock-steps"]') as HTMLElement | null;
    expect(steps?.dataset.current).toBe("1");
    expect(steps?.dataset.status).toBe("process");
    expect(container.querySelector('[data-testid="mock-step-Step1"]')).not.toBeNull();
    expect(container.querySelector('[data-testid="mock-step-Step2"]')?.textContent).toContain("二段");
    expect(container.querySelector('[data-testid="mock-step-Step3"]')).not.toBeNull();
  });
});
