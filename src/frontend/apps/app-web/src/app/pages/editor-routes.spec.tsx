// @vitest-environment jsdom

import type { ReactNode } from "react";
import { act } from "react-dom/test-utils";
import ReactDOM from "react-dom/client";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AppEditorRoute, CanonicalLowcodeStudioRoute } from "./editor-routes";

const { navigateMock, lowcodeStudioPropsMock } = vi.hoisted(() => ({
  navigateMock: vi.fn(),
  lowcodeStudioPropsMock: vi.fn()
}));

vi.mock("../lazy-named", () => ({
  lazyNamed: (_loader: unknown, name: string) => {
    if (name === "LowcodeStudioApp") {
      return (props: Record<string, unknown>) => {
        lowcodeStudioPropsMock(props);
        return (
          <div
            data-testid="mock-lowcode-studio-app"
            data-app-id={String(props.appId ?? "")}
            data-locale={String(props.locale ?? "")}
            data-workspace-id={String(props.workspaceId ?? "")}
            data-workspace-label={String(props.workspaceLabel ?? "")}
            data-has-host={props.host ? "true" : "false"}
          />
        );
      };
    }
    return () => null;
  }
}));

vi.mock("../i18n", () => ({
  useAppI18n: () => ({
    locale: "zh-CN",
    t: (key: string) => key
  })
}));

vi.mock("../workspace-context", () => ({
  useWorkspaceContext: () => ({
    id: "ws-2001",
    name: "工作空间 A",
    appKey: "atlas-app"
  })
}));

vi.mock("../app", () => ({
  useAppApis: () => ({
    studioApi: {}
  })
}));

vi.mock("../workflow-runtime-boundary", () => ({
  WorkflowRuntimeBoundary: ({ children }: { children: ReactNode }) => <>{children}</>
}));

vi.mock("../components/testset-drawer", () => ({
  TestsetDrawer: () => null
}));

vi.mock("../_shared", () => ({
  PageShell: () => <div data-testid="mock-page-shell" />
}));

vi.mock("@douyinfe/semi-ui", () => ({
  Button: ({ children }: { children?: ReactNode }) => <button type="button">{children}</button>
}));

vi.mock("react-router-dom", () => ({
  useNavigate: () => navigateMock,
  useParams: vi.fn(() => ({ projectId: "app-1001", id: "app-2002" })),
  useSearchParams: () => [new URLSearchParams()]
}));

describe("editor-routes lowcode", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("AppEditorRoute 在 app-web 壳内直接渲染共享 lowcode studio", () => {
    const container = document.createElement("div");
    const root = ReactDOM.createRoot(container);

    act(() => {
      root.render(<AppEditorRoute />);
    });

    const studio = container.querySelector('[data-testid="mock-lowcode-studio-app"]');
    expect(studio?.getAttribute("data-app-id")).toBe("app-1001");
    expect(studio?.getAttribute("data-locale")).toBe("zh-CN");
    expect(studio?.getAttribute("data-workspace-id")).toBe("ws-2001");
    expect(studio?.getAttribute("data-workspace-label")).toBe("工作空间 A");
    expect(studio?.getAttribute("data-has-host")).toBe("true");
    expect(lowcodeStudioPropsMock).toHaveBeenCalledWith(expect.objectContaining({
      host: expect.objectContaining({
        api: expect.any(Object),
        auth: expect.objectContaining({
          accessTokenFactory: expect.any(Function),
          tenantIdFactory: expect.any(Function),
          userIdFactory: expect.any(Function)
        })
      })
    }));
  });

  it("CanonicalLowcodeStudioRoute 使用 canonical /apps/lowcode/:id/studio 参数", () => {
    const container = document.createElement("div");
    const root = ReactDOM.createRoot(container);

    act(() => {
      root.render(<CanonicalLowcodeStudioRoute />);
    });

    const studio = container.querySelector('[data-testid="mock-lowcode-studio-app"]');
    expect(studio?.getAttribute("data-app-id")).toBe("app-2002");
  });
});
