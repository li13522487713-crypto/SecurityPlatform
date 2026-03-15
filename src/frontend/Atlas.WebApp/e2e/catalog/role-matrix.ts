import type { RoleAccessRule } from "./seed-types";

export const roleMatrix: RoleAccessRule[] = [
  {
    role: "securityadmin",
    visibleMenuTestIds: ["e2e-menu-system-notifications"],
    deniedPaths: ["/settings/system/configs", "/settings/org/users"]
  },
  {
    role: "readonly",
    visibleMenuTestIds: [],
    deniedPaths: ["/assets", "/settings/org/users", "/settings/system/configs", "/settings/auth/roles"]
  }
];
