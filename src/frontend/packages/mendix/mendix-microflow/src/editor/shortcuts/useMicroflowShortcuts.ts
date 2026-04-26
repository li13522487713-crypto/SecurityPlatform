import { useEffect, type RefObject } from "react";

import { isEditableShortcutTarget, isEditorShortcutEvent } from "./shortcut-utils";

export interface UseMicroflowShortcutsOptions {
  containerRef: RefObject<HTMLElement>;
  active?: boolean;
  readonly?: boolean;
  onUndo: () => void;
  onRedo: () => void;
  onSave: () => void;
  onSearch: () => void;
  onDeleteSelection: () => void;
  onEscape: () => void;
}

export function useMicroflowShortcuts({
  containerRef,
  active = true,
  readonly,
  onUndo,
  onRedo,
  onSave,
  onSearch,
  onDeleteSelection,
  onEscape,
}: UseMicroflowShortcutsOptions) {
  useEffect(() => {
    if (!active) {
      return undefined;
    }
    const handleKeyDown = (event: KeyboardEvent) => {
      const container = containerRef.current;
      const target = event.target;
      if (!container || !(target instanceof Node) || !container.contains(target)) {
        return;
      }
      const key = event.key.toLowerCase();
      const commandKey = event.ctrlKey || event.metaKey;

      if (commandKey && key === "s") {
        event.preventDefault();
        onSave();
        return;
      }

      if (commandKey && key === "f") {
        event.preventDefault();
        onSearch();
        return;
      }

      if (!isEditorShortcutEvent(event)) {
        if (key === "escape" && !isEditableShortcutTarget(event.target)) {
          event.preventDefault();
          onEscape();
        }
        return;
      }

      if (commandKey && key === "z" && !event.shiftKey) {
        event.preventDefault();
        onUndo();
        return;
      }

      if (commandKey && (key === "y" || (key === "z" && event.shiftKey))) {
        event.preventDefault();
        onRedo();
        return;
      }

      if (!readonly && (key === "delete" || key === "backspace")) {
        event.preventDefault();
        onDeleteSelection();
        return;
      }

      if (key === "escape") {
        event.preventDefault();
        onEscape();
      }
    };

    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [active, containerRef, onDeleteSelection, onEscape, onRedo, onSave, onSearch, onUndo, readonly]);
}
