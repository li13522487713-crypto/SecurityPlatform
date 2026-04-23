// @vitest-environment jsdom

import type { ReactNode } from "react";
import { act } from "react-dom/test-utils";
import ReactDOM from "react-dom/client";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { WorkspaceSwitcher } from "./workspace-switcher";

const {
  getWorkspacesMock,
  navigateMock
} = vi.hoisted(() => ({
  getWorkspacesMock: vi.fn(),
  navigateMock: vi.fn()
}));

vi.mock("@douyinfe/semi-ui", () => ({
  Dropdown: ({ children, render }: { children: ReactNode; render: ReactNode }) => (
    <div>
      {children}
      {render}
    </div>
  ),
  Input: ({
    value,
    onChange,
    placeholder
  }: {
    value?: string;
    onChange?: (value: string) => void;
    placeholder?: string;
  }) => (
    <input
      data-testid="mock-input"
      value={value ?? ""}
      placeholder={placeholder}
      onChange={(event) => onChange?.(event.target.value)}
    />
  ),
  Spin: () => <div data-testid="mock-spin">loading</div>,
  Tag: ({ children }: { children?: ReactNode }) => <span>{children}</span>
}));

vi.mock("@douyinfe/semi-icons", () => ({
  IconChevronDown: () => <span>v</span>,
  IconPlus: () => <span>+</span>,
  IconSearch: () => <span>s</span>,
  IconArrowUp: () => <span>u</span>
}));

vi.mock("react-router-dom", () => ({
  useNavigate: () => navigateMock
}));

vi.mock("@atlas/shared-react-core/utils", () => ({
  getTenantId: () => "tenant-1"
}));

vi.mock("@atlas/app-shell-shared", () => ({
  selectWorkspacePath: () => "/console",
  workspaceHomePath: (workspaceId: string) => `/workspace/${workspaceId}/home`
}));

vi.mock("../../services/api-org-workspaces", () => ({
  getWorkspaces: getWorkspacesMock
}));

vi.mock("../i18n", () => ({
  useAppI18n: () => ({
    t: (key: string) => key
  })
}));

vi.mock("./create-workspace-modal", () => ({
  CreateWorkspaceModal: () => null
}));

describe("WorkspaceSwitcher", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    getWorkspacesMock.mockResolvedValue([
      { id: "ws-1", name: "空间一", appKey: "space-one" },
      { id: "ws-2", name: "空间二", appKey: "space-two" }
    ]);
  });

  it("点选工作空间时优先走 onSelectWorkspace，而不是默认跳首页", async () => {
    const onSelectWorkspace = vi.fn();
    const container = document.createElement("div");
    document.body.appendChild(container);
    const root = ReactDOM.createRoot(container);

    await act(async () => {
      root.render(
        <WorkspaceSwitcher
          workspaceId="ws-1"
          workspaceLabel="空间一"
          onSelectWorkspace={onSelectWorkspace}
        />
      );
    });

    await act(async () => {
      await Promise.resolve();
    });

    const targetButton = container.querySelector('[data-testid="coze-workspace-switcher-item-ws-2"]');
    expect(targetButton).not.toBeNull();

    await act(async () => {
      targetButton?.dispatchEvent(new MouseEvent("click", { bubbles: true }));
    });

    expect(onSelectWorkspace).toHaveBeenCalledWith("ws-2");
    expect(navigateMock).not.toHaveBeenCalled();
  });
});
