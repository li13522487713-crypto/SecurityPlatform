export interface DesignerCommand {
  id: string;
  description: string;
  execute: () => void;
  undo?: () => void;
}
