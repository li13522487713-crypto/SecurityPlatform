export interface AppNavItem {
  labelKey:
    | "sidebarOverview"
    | "sidebarUsers"
    | "sidebarDepartments"
    | "sidebarPositions"
    | "sidebarRoles"
    | "sidebarAgents"
    | "sidebarChat"
    | "sidebarAssistant"
    | "sidebarWorkflow"
    | "sidebarLibrary"
    | "sidebarPrompts"
    | "sidebarModels"
    | "sidebarData"
    | "sidebarSettings"
    | "sidebarProfile";
  path: string;
  requiredPermission?: string;
}

export interface AppNavGroup {
  titleKey:
    | "sidebarOverview"
    | "sidebarOrganization"
    | "sidebarAI"
    | "sidebarKnowledge"
    | "sidebarData"
    | "sidebarSettings";
  items: AppNavItem[];
}
