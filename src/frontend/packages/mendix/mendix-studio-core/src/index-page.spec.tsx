// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { MendixStudioIndexPage, setMendixStudioDevSampleEnabledForTesting } from "./mendix-studio-index-page";

vi.mock("@atlas/microflow", () => ({
  sampleOrderProcessingMicroflow: { id: "stub", schemaVersion: "test", nodes: [], edges: [], name: "stub" },
  MicroflowEditor: () => null,
}));

vi.mock("@douyinfe/semi-icons", () => ({
  IconArrowRight: () => <span>-&gt;</span>,
}));

vi.mock("@douyinfe/semi-ui", async () => {
  return {
    Button: ({ children, onClick, "data-testid": testId }: any) => (
      <button type="button" data-testid={testId} onClick={onClick}>{children}</button>
    ),
    Card: ({ children }: any) => <section>{children}</section>,
    Input: ({ value, onChange, placeholder, onEnterPress, "data-testid": testId }: any) => (
      <input
        aria-label={placeholder}
        data-testid={testId}
        value={value ?? ""}
        onChange={event => onChange?.(event.target.value)}
        onKeyDown={event => {
          if (event.key === "Enter") onEnterPress?.();
        }}
      />
    ),
    Space: ({ children }: any) => <div>{children}</div>,
    Toast: { info: vi.fn(), warning: vi.fn(), success: vi.fn(), error: vi.fn() },
    Typography: {
      Text: ({ children }: any) => <p>{children}</p>,
    },
  };
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("MendixStudioIndexPage P0-2 hardening", () => {
  afterEach(() => {
    setMendixStudioDevSampleEnabledForTesting(undefined);
  });

  it("does not render the hardcoded Procurement sample card when dev-sample mode is disabled", () => {
    setMendixStudioDevSampleEnabledForTesting(false);
    const onOpen = vi.fn();
    render(<MendixStudioIndexPage workspaceId="workspace-1" onOpen={onOpen} />);

    expect(screen.queryByTestId("mendix-studio-dev-sample-card")).toBeNull();
    expect(screen.getByTestId("mendix-studio-app-id-input")).toBeTruthy();
  });

  it("renders the dev-sample card only when dev-sample mode is explicitly enabled", () => {
    setMendixStudioDevSampleEnabledForTesting(true);
    const onOpen = vi.fn();
    render(<MendixStudioIndexPage workspaceId="workspace-1" onOpen={onOpen} />);

    expect(screen.getByTestId("mendix-studio-dev-sample-card")).toBeTruthy();
  });

  it("opens the entered appId via onOpen and ignores empty input", () => {
    setMendixStudioDevSampleEnabledForTesting(false);
    const onOpen = vi.fn();
    render(<MendixStudioIndexPage workspaceId="workspace-1" onOpen={onOpen} />);

    fireEvent.click(screen.getByTestId("mendix-studio-open-app-button"));
    expect(onOpen).not.toHaveBeenCalled();

    fireEvent.change(screen.getByTestId("mendix-studio-app-id-input"), { target: { value: "  app-real-1  " } });
    fireEvent.click(screen.getByTestId("mendix-studio-open-app-button"));
    expect(onOpen).toHaveBeenCalledWith("app-real-1");
  });
});
