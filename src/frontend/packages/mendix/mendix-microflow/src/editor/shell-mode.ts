export type MicroflowShellMode = "legacy-host-layout" | "editor-native-layout";

export function shouldAutoOpenProblemsDock(shellMode: MicroflowShellMode): boolean {
  return shellMode === "legacy-host-layout";
}
