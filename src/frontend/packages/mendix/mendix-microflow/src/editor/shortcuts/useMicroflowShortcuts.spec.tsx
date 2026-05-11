// @vitest-environment jsdom

import React, { useRef } from "react";
import { cleanup, fireEvent, render } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { useMicroflowShortcuts } from "./useMicroflowShortcuts";

afterEach(() => cleanup());

function ShortcutHarness(props: {
  onStepInto?: () => void;
  onStepOver?: () => void;
  onStepOut?: () => void;
  onContinue?: () => void;
}) {
  const ref = useRef<HTMLDivElement>(null);
  useMicroflowShortcuts({
    containerRef: ref,
    onUndo: vi.fn(),
    onRedo: vi.fn(),
    onSave: vi.fn(),
    onSearch: vi.fn(),
    onDeleteSelection: vi.fn(),
    onEscape: vi.fn(),
    onStepInto: props.onStepInto,
    onStepOver: props.onStepOver,
    onStepOut: props.onStepOut,
    onContinue: props.onContinue,
  });
  return (
    <div ref={ref}>
      <button data-testid="shortcut-target" type="button">target</button>
      <input data-testid="editable-target" />
    </div>
  );
}

describe("useMicroflowShortcuts", () => {
  it("maps F5/F6/F7/F8 to debug step commands inside the editor shell", () => {
    const onStepInto = vi.fn();
    const onStepOver = vi.fn();
    const onStepOut = vi.fn();
    const onContinue = vi.fn();
    const { getByTestId } = render(
      <ShortcutHarness
        onStepInto={onStepInto}
        onStepOver={onStepOver}
        onStepOut={onStepOut}
        onContinue={onContinue}
      />,
    );
    const target = getByTestId("shortcut-target");

    fireEvent.keyDown(target, { key: "F5" });
    fireEvent.keyDown(target, { key: "F6" });
    fireEvent.keyDown(target, { key: "F7" });
    fireEvent.keyDown(target, { key: "F8" });

    expect(onStepInto).toHaveBeenCalledTimes(1);
    expect(onStepOver).toHaveBeenCalledTimes(1);
    expect(onStepOut).toHaveBeenCalledTimes(1);
    expect(onContinue).toHaveBeenCalledTimes(1);
  });

  it("does not trigger debug shortcuts while typing in editable inputs", () => {
    const onStepInto = vi.fn();
    const { getByTestId } = render(<ShortcutHarness onStepInto={onStepInto} />);
    const input = getByTestId("editable-target");

    fireEvent.keyDown(input, { key: "F5" });

    expect(onStepInto).not.toHaveBeenCalled();
  });
});
