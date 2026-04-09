import type { DesignerCommand } from "../commands/index";

const undoStack: DesignerCommand[] = [];
const redoStack: DesignerCommand[] = [];

export interface SnapshotHistoryState<TSnapshot> {
  stack: TSnapshot[];
  pointer: number;
}

export interface SnapshotHistoryOptions<TSnapshot> {
  maxHistory?: number;
  clone: (snapshot: TSnapshot) => TSnapshot;
  equals?: (left: TSnapshot, right: TSnapshot) => boolean;
}

export function pushCommand(command: DesignerCommand) {
  undoStack.push(command);
  redoStack.length = 0;
}

export function undo() {
  const command = undoStack.pop();
  if (!command?.undo) return;
  command.undo();
  redoStack.push(command);
}

export function redo() {
  const command = redoStack.pop();
  if (!command) return;
  command.execute();
  undoStack.push(command);
}

export function initSnapshotHistory<TSnapshot>(
  state: SnapshotHistoryState<TSnapshot>,
  initialSnapshot: TSnapshot,
  options: SnapshotHistoryOptions<TSnapshot>
) {
  state.stack = [options.clone(initialSnapshot)];
  state.pointer = 0;
}

export function pushSnapshotHistory<TSnapshot>(
  state: SnapshotHistoryState<TSnapshot>,
  snapshot: TSnapshot,
  options: SnapshotHistoryOptions<TSnapshot>
) {
  const nextSnapshot = options.clone(snapshot);
  if (state.pointer >= 0) {
    const current = state.stack[state.pointer];
    const equals = options.equals ?? ((left, right) => JSON.stringify(left) === JSON.stringify(right));
    if (equals(current, nextSnapshot)) {
      return;
    }
  }

  if (state.pointer < state.stack.length - 1) {
    state.stack = state.stack.slice(0, state.pointer + 1);
  }

  state.stack.push(nextSnapshot);
  const maxHistory = options.maxHistory ?? 20;
  if (state.stack.length > maxHistory) {
    state.stack.shift();
  }
  state.pointer = state.stack.length - 1;
}

export function undoSnapshotHistory<TSnapshot>(
  state: SnapshotHistoryState<TSnapshot>,
  options: SnapshotHistoryOptions<TSnapshot>
) {
  if (state.pointer <= 0) {
    return null;
  }
  state.pointer -= 1;
  return options.clone(state.stack[state.pointer]);
}

export function redoSnapshotHistory<TSnapshot>(
  state: SnapshotHistoryState<TSnapshot>,
  options: SnapshotHistoryOptions<TSnapshot>
) {
  if (state.pointer < 0 || state.pointer >= state.stack.length - 1) {
    return null;
  }
  state.pointer += 1;
  return options.clone(state.stack[state.pointer]);
}
