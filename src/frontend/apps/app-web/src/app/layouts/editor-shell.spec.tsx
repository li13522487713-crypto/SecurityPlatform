// @vitest-environment jsdom

import type { ReactNode } from "react";
import { act } from "react-dom/test-utils";
import ReactDOM from "react-dom/client";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { EditorWorkspaceResolutionError } from "../../services/api-editor-context";
import { EditorShellLayout, resolveEditorRouteResource } from "./editor-shell";

const {
  locationState,
  navigateMock,
  resolveEditorWorkspaceMock,
  rememberLastWorkspaceIdMock,
  workspaceProviderMock
} = vi.hoisted(() => ({
  locationState: { pathname: "/workflow/300/editor", search: "" },
  navigateMock: vi.fn(),
  resolveEditorWorkspaceMock: vi.fn(),
  rememberLastWorkspaceIdMock: vi.fn(),
  workspaceProviderMock: vi.fn()
}));

let currentWorkspaceId = "";

vi.mock("react-router-dom", () => ({
  Navigate: ({ to }: { to: string }) => <div data-testid="mock-navigate" data-to={to} />,
  Outlet: () => <div data-testid="mock-outlet" />,
  useLocation: () => locationState,
  useNavigate: () => navigateMock
}));

vi.mock("@douyinfe/semi-ui", () => ({
  Button: ({ children, onClick }: { children?: ReactNode; onClick?: () => void }) => (
    <button type="button" onClick={onClick}>
      {children}
    </button>
  )
}));

vi.mock("@douyinfe/semi-icons", () => ({
  IconChevronLeft: () => null
}));

vi.mock("@atlas/shared-react-core/utils", () => ({
  getTenantId: () => "org-1"
}));

vi.mock("../auth-context", () => ({
  useAuth: () => ({
    loading: false,
    isAuthenticated: true
  })
}));

vi.mock("../bootstrap-context", () => ({
  useBootstrap: () => ({
    loading: false,
    platformReady: true,
    appReady: true
  })
}));

vi.mock("../organization-context", () => ({
  OrganizationProvider: ({ children }: { children: ReactNode }) => <>{children}</>
}));

vi.mock("../permission-context", () => ({
  PermissionProvider: ({ children }: { children: ReactNode }) => <>{children}</>
}));

vi.mock("../workspace-context", () => ({
  WorkspaceProvider: ({ workspaceId, children }: { workspaceId: string; children: ReactNode }) => {
    currentWorkspaceId = workspaceId;
    workspaceProviderMock(workspaceId);
    return <>{children}</>;
  },
  useWorkspaceContext: () => ({
    id: currentWorkspaceId,
    name: `Workspace ${currentWorkspaceId}`,
    appKey: "atlas-app",
    loading: false
  })
}));

vi.mock("../i18n", () => ({
  useAppI18n: () => ({
    t: (key: string) => key
  })
}));

vi.mock("../../services/api-editor-context", () => ({
  EditorWorkspaceResolutionError: class EditorWorkspaceResolutionError extends Error {
    code: string;

    constructor(code: string, message: string) {
      super(message);
      this.code = code;
      this.name = "EditorWorkspaceResolutionError";
    }
  },
  resolveEditorWorkspace: (...args: unknown[]) => resolveEditorWorkspaceMock(...args)
}));

vi.mock("./workspace-shell", () => ({
  rememberLastWorkspaceId: (workspaceId: string) => rememberLastWorkspaceIdMock(workspaceId)
}));

vi.mock("../_shared", () => ({
  PageShell: ({ loading }: { loading?: boolean }) => (
    <div data-testid={loading ? "mock-page-shell-loading" : "mock-page-shell"} />
  )
}));

describe("resolveEditorRouteResource", () => {
  it("正确映射各类编辑器路由到资源类型", () => {
    expect(resolveEditorRouteResource("/apps/lowcode/11/studio")).toEqual({ resourceType: "app", resourceId: "11" });
    expect(resolveEditorRouteResource("/app/22/editor")).toEqual({ resourceType: "app", resourceId: "22" });
    expect(resolveEditorRouteResource("/workflow/33/editor")).toEqual({ resourceType: "workflow", resourceId: "33" });
    expect(resolveEditorRouteResource("/chatflow/44/editor")).toEqual({ resourceType: "workflow", resourceId: "44" });
    expect(resolveEditorRouteResource("/agent/55/publish")).toEqual({ resourceType: "agent", resourceId: "55" });
    expect(resolveEditorRouteResource("/not-supported")).toBeNull();
  });
});

describe("EditorShellLayout", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    currentWorkspaceId = "";
    locationState.pathname = "/workflow/300/editor";
    locationState.search = "";
    window.localStorage.setItem("atlas_last_workspace_id", "stale-workspace");
  });

  it("解析成功时使用真实 workspaceId 而不是本地缓存", async () => {
    resolveEditorWorkspaceMock.mockResolvedValue({
      resourceType: "workflow",
      resourceId: "300",
      workspaceId: "2002"
    });

    const container = document.createElement("div");
    const root = ReactDOM.createRoot(container);

    await act(async () => {
      root.render(<EditorShellLayout />);
      await Promise.resolve();
      await Promise.resolve();
    });

    expect(resolveEditorWorkspaceMock).toHaveBeenCalledWith("workflow", "300");
    expect(workspaceProviderMock).toHaveBeenCalledWith("2002");
    expect(rememberLastWorkspaceIdMock).toHaveBeenCalledWith("2002");
    expect(container.querySelector('[data-testid="coze-editor-shell"]')).not.toBeNull();
    expect(container.textContent).toContain("Workspace 2002");
  });

  it("解析失败时显示阻断页，不继续渲染编辑器", async () => {
    resolveEditorWorkspaceMock.mockRejectedValue(
      new EditorWorkspaceResolutionError(
        "EDITOR_CONTEXT_WORKSPACE_UNRESOLVED",
        "missing"
      )
    );

    const container = document.createElement("div");
    const root = ReactDOM.createRoot(container);

    await act(async () => {
      root.render(<EditorShellLayout />);
      await Promise.resolve();
      await Promise.resolve();
    });

    const failure = container.querySelector('[data-testid="coze-editor-shell-error"]');
    expect(failure).not.toBeNull();
    expect(failure?.getAttribute("data-error-code")).toBe("EDITOR_CONTEXT_WORKSPACE_UNRESOLVED");
    expect(container.querySelector('[data-testid="coze-editor-shell"]')).toBeNull();
  });
});
