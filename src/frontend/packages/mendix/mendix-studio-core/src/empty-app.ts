import type { LowCodeAppSchema } from "@atlas/mendix-schema";

export const EMPTY_APP_SCHEMA: LowCodeAppSchema = {
  appId: "",
  name: "",
  version: "",
  modules: [],
  navigation: [],
  security: {
    securityLevel: "production",
    userRoles: [],
    moduleRoles: [],
    pageAccessRules: [],
    microflowAccessRules: [],
    nanoflowAccessRules: [],
    entityAccessRules: [],
  },
  extensions: [],
};
