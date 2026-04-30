const BLOCKED_NODE_DRAG_TARGET_SELECTOR = [
  "button",
  "input",
  "textarea",
  "select",
  "[contenteditable='true']",
  "[data-flow-editor-selectable='false']",
  ".workflow-point-bg",
  ".workflow-port-render",
  ".gedit-flow-port",
].join(", ");

export function isMicroflowNodeDragBlockedTarget(target: EventTarget | null): boolean {
  return target instanceof HTMLElement && Boolean(target.closest(BLOCKED_NODE_DRAG_TARGET_SELECTOR));
}

export function focusMicroflowNodeDragRoot(root: HTMLElement): void {
  root.focus({ preventScroll: true });
}
