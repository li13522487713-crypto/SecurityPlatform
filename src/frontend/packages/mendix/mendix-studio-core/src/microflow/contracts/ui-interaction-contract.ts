export type InlineEditState = "idle" | "editing" | "paused-edit" | "blocked";

export type PanelSyncEvent =
  | { type: "inline-edit"; nodeId?: string; flowId?: string; fieldPath?: string }
  | { type: "property-edit"; nodeId?: string; flowId?: string; fieldPath?: string }
  | { type: "problem-fix"; issueId?: string; nodeId?: string; flowId?: string }
  | { type: "trace-focus"; nodeId?: string; flowId?: string; frameId?: string };

export interface CommandPaletteAction {
  id: string;
  label: string;
  disabled?: boolean;
  run: () => void;
}
