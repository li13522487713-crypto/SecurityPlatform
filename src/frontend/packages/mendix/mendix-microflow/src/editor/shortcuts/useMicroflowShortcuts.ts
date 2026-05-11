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
  onStepInto?: () => void;
  onStepOver?: () => void;
  onStepOut?: () => void;
  onContinue?: () => void;
  onCopySelection?: () => void;
  onPasteSelection?: () => void;
  onDeleteSelection: () => void;
  onEscape: () => void;
  onFocusMode?: () => void;
  onSelectAll?: () => void;
  onDuplicateSelection?: () => void;
  onFitView?: () => void;
  onMoveSelection?: (dx: number, dy: number) => void;
}

export function useMicroflowShortcuts({
  containerRef,
  active = true,
  readonly,
  onUndo,
  onRedo,
  onSave,
  onSearch,
  onStepInto,
  onStepOver,
  onStepOut,
  onContinue,
  onCopySelection,
  onPasteSelection,
  onDeleteSelection,
  onEscape,
  onFocusMode,
  onSelectAll,
  onDuplicateSelection,
  onFitView,
  onMoveSelection,
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

      if (key === "f11" && onFocusMode) {
        event.preventDefault();
        onFocusMode();
        return;
      }

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

      if (!isEditableShortcutTarget(event.target)) {
        if (key === "f5" && onStepInto) {
          event.preventDefault();
          onStepInto();
          return;
        }
        if (key === "f6" && onStepOver) {
          event.preventDefault();
          onStepOver();
          return;
        }
        if (key === "f7" && onStepOut) {
          event.preventDefault();
          onStepOut();
          return;
        }
        if (key === "f8" && onContinue) {
          event.preventDefault();
          onContinue();
          return;
        }
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

      if (!readonly && commandKey && key === "c" && onCopySelection) {
        event.preventDefault();
        onCopySelection();
        return;
      }

      if (!readonly && commandKey && key === "v" && onPasteSelection) {
        event.preventDefault();
        onPasteSelection();
        return;
      }

      if (!readonly && (key === "delete" || key === "backspace")) {
        event.preventDefault();
        onDeleteSelection();
        return;
      }

      // Ctrl+A：全选
      if (commandKey && key === "a" && onSelectAll) {
        event.preventDefault();
        onSelectAll();
        return;
      }

      // Ctrl+D：复制当前选中节点
      if (!readonly && commandKey && key === "d" && onDuplicateSelection) {
        event.preventDefault();
        onDuplicateSelection();
        return;
      }

      // Ctrl+0 / Ctrl+Shift+H：适应视图
      if (commandKey && (key === "0" || (event.shiftKey && key === "h")) && onFitView) {
        event.preventDefault();
        onFitView();
        return;
      }

      // 方向键微移选中节点（普通 1格，Shift+方向键 8格）
      if (!readonly && onMoveSelection && ["arrowleft", "arrowright", "arrowup", "arrowdown"].includes(key)) {
        // 只在有选中节点时生效，避免影响滚动
        event.preventDefault();
        const step = event.shiftKey ? 8 : 1;
        if (key === "arrowleft") onMoveSelection(-step, 0);
        else if (key === "arrowright") onMoveSelection(step, 0);
        else if (key === "arrowup") onMoveSelection(0, -step);
        else if (key === "arrowdown") onMoveSelection(0, step);
        return;
      }

      if (key === "escape") {
        event.preventDefault();
        onEscape();
      }
    };

    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [active, containerRef, onContinue, onCopySelection, onDeleteSelection, onDuplicateSelection, onEscape, onFitView, onFocusMode, onMoveSelection, onPasteSelection, onRedo, onSave, onSearch, onSelectAll, onStepInto, onStepOut, onStepOver, onUndo, readonly]);
}
