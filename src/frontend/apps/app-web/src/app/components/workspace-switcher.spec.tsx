// @vitest-environment jsdom

import { cloneElement, isValidElement, type ReactElement, type ReactNode } from "react";
import { act } from "react-dom/test-utils";
import ReactDOM from "react-dom/client";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { WorkspaceSwitcher } from "./workspace-switcher";

const {
  createSpaceMock,
  fetchSpacesMock,
  navigateMock
} = vi.hoisted(() => ({
  createSpaceMock: vi.fn(),
  fetchSpacesMock: vi.fn(),
  navigateMock: vi.fn()
}));

vi.mock("@coze-arch/coze-design", () => {
  const Select = ({
    children,
    onChange
  }: {
    children?: ReactNode;
    onChange?: (value: string) => void;
  }) => (
    <div>
      {Array.isArray(children)
        ? children.map((child) =>
            isValidElement(child)
              ? cloneElement(child as ReactElement<{ onChange?: (value: string) => void }>, { onChange })
              : child
          )
        : isValidElement(children)
          ? cloneElement(children as ReactElement<{ onChange?: (value: string) => void }>, { onChange })
          : children}
    </div>
  );
  Select.Option = ({
    children,
    value,
    onChange,
    ...rest
  }: {
    children?: ReactNode;
    value: string;
    onChange?: (value: string) => void;
  }) => (
    <button
      type="button"
      data-testid={(rest as { "data-testid"?: string })["data-testid"]}
      onClick={() => onChange?.(value)}
    >
      {children}
    </button>
  );

  return {
    Avatar: ({ children }: { children?: ReactNode }) => <span>{children}</span>,
    Button: ({ children, onClick }: { children?: ReactNode; onClick?: () => void }) => (
      <button type="button" onClick={onClick}>
        {children}
      </button>
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
    Modal: ({ children, visible }: { children?: ReactNode; visible?: boolean }) => visible ? <div>{children}</div> : null,
    Select,
    Spin: () => <div data-testid="mock-spin">loading</div>,
    Tag: ({ children }: { children?: ReactNode }) => <span>{children}</span>,
    TextArea: ({
      value,
      onChange
    }: {
      value?: string;
      onChange?: (value: string) => void;
    }) => (
      <textarea
        value={value ?? ""}
        onChange={(event) => onChange?.(event.target.value)}
      />
    ),
    Toast: {
      warning: vi.fn(),
      success: vi.fn(),
      error: vi.fn()
    },
    Typography: {
      Text: ({ children }: { children?: ReactNode }) => <span>{children}</span>
    }
  };
});

vi.mock("react-router-dom", () => ({
  useNavigate: () => navigateMock
}));

vi.mock("@atlas/app-shell-shared", () => ({
  selectWorkspacePath: () => "/console",
  workspaceHomePath: (workspaceId: string) => `/workspace/${workspaceId}/home`
}));

vi.mock("@coze-foundation/space-store-adapter", () => ({
  useSpaceStore: (selector?: (state: Record<string, unknown>) => unknown) => {
    const state = {
      spaceList: [
        { id: "ws-1", name: "空间一", icon_url: "", hide_operation: false },
        { id: "ws-2", name: "空间二", icon_url: "", hide_operation: false }
      ],
      loading: false,
      inited: true,
      fetchSpaces: fetchSpacesMock,
      createSpace: createSpaceMock
    };
    return selector ? selector(state) : state;
  }
}));

vi.mock("../i18n", () => ({
  useAppI18n: () => ({
    t: (key: string) => key
  })
}));

describe("WorkspaceSwitcher", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    fetchSpacesMock.mockResolvedValue(undefined);
    createSpaceMock.mockResolvedValue({ id: "ws-3" });
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
