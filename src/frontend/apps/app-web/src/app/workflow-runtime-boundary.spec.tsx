// @vitest-environment jsdom

import { act } from "react-dom/test-utils";
import ReactDOM from "react-dom/client";
import { describe, expect, it, vi } from "vitest";
import { WorkflowRuntimeBoundary } from "./workflow-runtime-boundary";

const startupState = {
  bootstrapReady: true,
  platformReady: true,
  appReady: true,
  featureFlagsReady: true,
  featureFlagsLoading: false,
  spaceReady: true,
  workflowAllowed: true,
  featureFlagsError: null,
  refreshFeatureFlags: vi.fn(async () => undefined),
};

const bootstrapState = {
  loading: false,
  platformReady: true,
  appReady: true,
  spaceId: "workspace-1",
  refresh: vi.fn(async () => undefined),
};

const workspaceState = {
  id: "workspace-1",
  appKey: "atlas-app",
  loading: false,
};

vi.mock("./i18n", () => ({
  useAppI18n: () => ({
    t: (key: string) => key,
  }),
}));

vi.mock("./startup-kernel", () => ({
  useAppStartup: () => startupState,
}));

vi.mock("./bootstrap-context", () => ({
  useBootstrap: () => bootstrapState,
}));

vi.mock("./workspace-context", () => ({
  useOptionalWorkspaceContext: () => workspaceState,
}));

vi.mock("./organization-context", () => ({
  useOptionalOrganizationContext: () => ({ orgId: "org-1" }),
}));

describe("WorkflowRuntimeBoundary", () => {
  it("未准备完成时只显示加载态，不渲染子树", () => {
    const container = document.createElement("div");
    const root = ReactDOM.createRoot(container);
    startupState.featureFlagsReady = false;
    startupState.featureFlagsLoading = true;

    act(() => {
      root.render(
        <WorkflowRuntimeBoundary>
          <div data-testid="workflow-ready">ready</div>
        </WorkflowRuntimeBoundary>
      );
    });

    expect(container.textContent).toContain("workflowRuntimePreparing");
    expect(container.querySelector('[data-testid="workflow-ready"]')).toBeNull();
  });

  it("依赖失败时显示降级页", () => {
    const container = document.createElement("div");
    const root = ReactDOM.createRoot(container);
    startupState.featureFlagsLoading = false;
    startupState.featureFlagsReady = true;
    startupState.featureFlagsError = new Error("boom");

    act(() => {
      root.render(
        <WorkflowRuntimeBoundary>
          <div data-testid="workflow-ready">ready</div>
        </WorkflowRuntimeBoundary>
      );
    });

    expect(container.textContent).toContain("workflowRuntimeUnavailableTitle");
    expect(container.querySelector('[data-testid="workflow-ready"]')).toBeNull();
  });

  it("依赖就绪后才渲染工作流子树", () => {
    const container = document.createElement("div");
    const root = ReactDOM.createRoot(container);
    startupState.featureFlagsError = null;
    startupState.featureFlagsReady = true;
    startupState.featureFlagsLoading = false;

    act(() => {
      root.render(
        <WorkflowRuntimeBoundary>
          <div data-testid="workflow-ready">ready</div>
        </WorkflowRuntimeBoundary>
      );
    });

    expect(container.querySelector('[data-testid="workflow-ready"]')?.textContent).toBe("ready");
  });
});
