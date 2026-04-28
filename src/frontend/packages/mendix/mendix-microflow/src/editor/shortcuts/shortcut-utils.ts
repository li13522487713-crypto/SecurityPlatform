export function isEditableShortcutTarget(target: EventTarget | null): boolean {
  return target instanceof HTMLElement && Boolean(target.closest("input, textarea, select, [contenteditable='true']"));
}

export function isEditorShortcutEvent(event: KeyboardEvent): boolean {
  return !event.defaultPrevented && !isEditableShortcutTarget(event.target);
}
