import type { DesignerCommand } from "../commands/index";

const undoStack: DesignerCommand[] = [];
const redoStack: DesignerCommand[] = [];

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
