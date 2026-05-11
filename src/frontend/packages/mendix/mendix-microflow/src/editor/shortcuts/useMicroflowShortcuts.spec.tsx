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
  onFitView?: () => void;
  onFocusMode?: () => void;
  onSearchAll?: () => void;
  onSearch?: () => void;
  onGoTo?: () => void;
  onOpenProperties?: () => void;
}) {
  const ref = useRef<HTMLDivElement>(null);
  useMicroflowShortcuts({
    containerRef: ref,
    onUndo: vi.fn(),
    onRedo: vi.fn(),
    onSave: vi.fn(),
    onSearch: props.onSearch ?? vi.fn(),
    onSearchAll: props.onSearchAll,
    onDeleteSelection: vi.fn(),
    onEscape: vi.fn(),
    onFitView: props.onFitView,
    onFocusMode: props.onFocusMode,
    onStepInto: props.onStepInto,
    onStepOver: props.onStepOver,
    onStepOut: props.onStepOut,
    onContinue: props.onContinue,
    onGoTo: props.onGoTo,
    onOpenProperties: props.onOpenProperties,
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

  it("maps Ctrl+Shift+F to the global search command", () => {
    const onSearchAll = vi.fn();
    const onSearch = vi.fn();
    const { getByTestId } = render(<ShortcutHarness onSearch={onSearch} onSearchAll={onSearchAll} />);
    const target = getByTestId("shortcut-target");

    fireEvent.keyDown(target, { key: "F", ctrlKey: true, shiftKey: true });

    expect(onSearchAll).toHaveBeenCalledTimes(1);
    expect(onSearch).not.toHaveBeenCalled();
  });

  it("maps Ctrl+F to node search when global search callback is not used", () => {
    const onSearch = vi.fn();
    const { getByTestId } = render(<ShortcutHarness onSearch={onSearch} />);
    const target = getByTestId("shortcut-target");

    fireEvent.keyDown(target, { key: "F", ctrlKey: true, shiftKey: false });

    expect(onSearch).toHaveBeenCalledTimes(1);
  });

  it("falls back to node search for Ctrl+Shift+F when global search callback is absent", () => {
    const onSearch = vi.fn();
    const { getByTestId } = render(<ShortcutHarness onSearch={onSearch} />);
    const target = getByTestId("shortcut-target");

    fireEvent.keyDown(target, { key: "F", ctrlKey: true, shiftKey: true });

    expect(onSearch).toHaveBeenCalledTimes(1);
  });

  it("maps Ctrl+Shift+H to fit-view command", () => {
    const onFitView = vi.fn();
    const { getByTestId } = render(<ShortcutHarness onFitView={onFitView} />);
    const target = getByTestId("shortcut-target");

    fireEvent.keyDown(target, { key: "H", ctrlKey: true, shiftKey: true });

    expect(onFitView).toHaveBeenCalledTimes(1);
  });

  it("maps Ctrl+G to go-to command", () => {
    const onGoTo = vi.fn();
    const { getByTestId } = render(<ShortcutHarness onGoTo={onGoTo} />);
    const target = getByTestId("shortcut-target");

    fireEvent.keyDown(target, { key: "G", ctrlKey: true });

    expect(onGoTo).toHaveBeenCalledTimes(1);
  });

  it("maps Enter to open properties command", () => {
    const onOpenProperties = vi.fn();
    const { getByTestId } = render(<ShortcutHarness onOpenProperties={onOpenProperties} />);
    const target = getByTestId("shortcut-target");

    fireEvent.keyDown(target, { key: "Enter" });

    expect(onOpenProperties).toHaveBeenCalledTimes(1);
  });

  it("does not open properties while typing in editable target", () => {
    const onOpenProperties = vi.fn();
    const { getByTestId } = render(<ShortcutHarness onOpenProperties={onOpenProperties} />);
    const input = getByTestId("editable-target");

    fireEvent.keyDown(input, { key: "Enter" });

    expect(onOpenProperties).not.toHaveBeenCalled();
  });

  it("maps F11 to focus mode toggle", () => {
    const onFocusMode = vi.fn();
    const { getByTestId } = render(<ShortcutHarness onFocusMode={onFocusMode} />);
    const target = getByTestId("shortcut-target");

    fireEvent.keyDown(target, { key: "F11" });

    expect(onFocusMode).toHaveBeenCalledTimes(1);
  });
});
