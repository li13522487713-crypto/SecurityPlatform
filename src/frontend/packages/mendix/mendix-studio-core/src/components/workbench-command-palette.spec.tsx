// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { WorkbenchCommandPalette } from "./workbench-command-palette";
import type { MicroflowWorkbenchCommandBus } from "../microflow/workbench/microflow-workbench-command-bus";

vi.mock("@douyinfe/semi-ui", () => ({
  Button: ({ children, disabled, onClick }: any) => (
    <button type="button" disabled={disabled} onClick={onClick}>{children}</button>
  ),
  Empty: ({ title, description }: any) => (
    <div>
      <p>{title}</p>
      <p>{description}</p>
    </div>
  ),
  Input: ({ value, placeholder, onChange, onEnterPress }: any) => (
    <input
      aria-label={placeholder}
      value={value ?? ""}
      onChange={event => onChange?.(event.target.value)}
      onKeyDown={event => {
        if (event.key === "Enter") {
          onEnterPress?.();
        }
      }}
    />
  ),
  Modal: ({ visible, title, children }: any) => visible ? (
    <section>
      <h1>{title}</h1>
      {children}
    </section>
  ) : null,
  Space: ({ children }: any) => <div>{children}</div>,
  Tag: ({ children }: any) => <span>{children}</span>,
  Typography: {
    Text: ({ children }: any) => <span>{children}</span>,
  },
}));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("WorkbenchCommandPalette", () => {
  it("routes resource search results through the guarded open resource callback", () => {
    const execute = vi.fn();
    const onOpenResource = vi.fn();

    render(
      <WorkbenchCommandPalette
        visible
        status={{ dirty: false, running: false, canUndo: false, canRedo: false, errorCount: 0 } as any}
        commandBus={{ execute } as unknown as MicroflowWorkbenchCommandBus}
        modules={[
          {
            moduleId: "mod_procurement",
            name: "Procurement",
            qualifiedName: "Procurement",
            pages: [
              {
                id: "page_order_edit",
                name: "Order Edit",
                qualifiedName: "Procurement.OrderEdit",
                description: "Edit purchase order",
              },
            ],
          },
        ]}
        onOpenResource={onOpenResource}
        onClose={vi.fn()}
      />,
    );

    fireEvent.change(screen.getByRole("textbox"), { target: { value: "Order" } });
    fireEvent.click(screen.getByText(/Order Edit/u));

    expect(onOpenResource).toHaveBeenCalledWith(expect.objectContaining({
      kind: "page",
      resourceId: "page_order_edit",
      moduleId: "mod_procurement",
    }));
    expect(execute).not.toHaveBeenCalled();
  });

  it("executes the first enabled command when Enter is pressed", () => {
    const execute = vi.fn(async () => undefined);
    const onClose = vi.fn();

    render(
      <WorkbenchCommandPalette
        visible
        status={{ dirty: true, running: false, canUndo: false, canRedo: false, errorCount: 0 } as any}
        commandBus={{ execute } as unknown as MicroflowWorkbenchCommandBus}
        onClose={onClose}
      />,
    );

    fireEvent.change(screen.getByRole("textbox"), { target: { value: "save" } });
    fireEvent.keyDown(screen.getByRole("textbox"), { key: "Enter" });

    expect(execute).toHaveBeenCalledWith("microflow.save");
  });
});
