// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { WorkbenchTabs } from "./workbench-tabs";
import { useMendixStudioStore } from "../store";

const confirmMock = vi.hoisted(() => vi.fn());

vi.mock("@douyinfe/semi-icons", () => ({
  IconClose: () => <span aria-hidden="true">x</span>,
  IconPlus: () => <span aria-hidden="true">+</span>,
}));

vi.mock("@douyinfe/semi-ui", () => ({
  Button: ({ children, disabled, loading, onClick, type }: any) => (
    <button type="button" data-type={type} disabled={disabled || loading} onClick={onClick}>
      {children}
    </button>
  ),
  Modal: Object.assign(({ children, footer, title, visible }: any) => visible ? (
    <section aria-label={title}>
      {children}
      {footer}
    </section>
  ) : null, {
    confirm: confirmMock,
  }),
  Space: ({ children }: any) => <div>{children}</div>,
  Tag: ({ children, title }: any) => <span title={title}>{children}</span>,
  Typography: {
    Text: ({ children }: any) => <p>{children}</p>,
  },
}));

beforeEach(() => {
  confirmMock.mockReset();
  useMendixStudioStore.setState({
    activeTab: "microflowDesigner",
    activeTabId: "microflowDesigner",
    workbenchTabs: [
      {
        id: "microflow:mf-a",
        kind: "microflow",
        title: "Approve Purchase",
        resourceId: "mf-a",
        microflowId: "mf-a",
        closable: true,
        openedAt: "2026-04-28T00:00:00.000Z",
        historyKey: "microflow:mf-a",
      },
      {
        id: "microflow:mf-b",
        kind: "microflow",
        title: "Reject Purchase",
        resourceId: "mf-b",
        microflowId: "mf-b",
        closable: true,
        openedAt: "2026-04-28T00:01:00.000Z",
        historyKey: "microflow:mf-b",
      },
    ],
    activeWorkbenchTabId: "microflow:mf-a",
    activeMicroflowId: "mf-a",
    selectedExplorerNodeId: "microflow:mf-a",
    dirtyByWorkbenchTabId: {},
    saveStateByMicroflowId: {},
    pendingCloseTabId: undefined,
    tabCloseGuardOpen: false,
  });
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("WorkbenchTabs guards", () => {
  it("blocks switching away from a dirty active tab and leaves selection unchanged", () => {
    useMendixStudioStore.getState().markWorkbenchTabDirty("microflow:mf-a", true);

    render(<WorkbenchTabs />);
    fireEvent.click(screen.getByText("Reject Purchase"));

    expect(confirmMock).toHaveBeenCalledWith(expect.objectContaining({
      title: "当前微流尚未保存",
      okText: "留在当前 Tab",
    }));
    expect(useMendixStudioStore.getState().activeWorkbenchTabId).toBe("microflow:mf-a");
    expect(useMendixStudioStore.getState().selectedExplorerNodeId).toBe("microflow:mf-a");
  });

  it("dispatches a save request before force closing a guarded dirty tab", () => {
    const dispatchSpy = vi.spyOn(window, "dispatchEvent");
    useMendixStudioStore.getState().markWorkbenchTabDirty("microflow:mf-a", true);
    useMendixStudioStore.getState().closeWorkbenchTab("microflow:mf-a");

    render(<WorkbenchTabs />);
    fireEvent.click(screen.getByText("Save"));

    expect(dispatchSpy).toHaveBeenCalledWith(expect.objectContaining({
      type: "atlas:microflow-save-request",
    }));
    const event = dispatchSpy.mock.calls[0]?.[0] as CustomEvent;
    expect(event.detail.microflowId).toBe("mf-a");

    event.detail.onSaved();
    expect(useMendixStudioStore.getState().workbenchTabs.some(tab => tab.id === "microflow:mf-a")).toBe(false);
  });
});
