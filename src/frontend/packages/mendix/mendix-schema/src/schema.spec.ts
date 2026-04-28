import { describe, expect, it } from "vitest";
import { isLowCodeAppSchema, LowCodeAppSchemaZod } from "./index";

describe("mendix-schema", () => {
  it("should validate minimal schema", () => {
    const schema = {
      appId: "app_1",
      name: "Demo",
      version: "1.0.0",
      modules: [],
      navigation: [],
      security: {
        securityLevel: "off",
        userRoles: [],
        moduleRoles: [],
        pageAccessRules: [],
        microflowAccessRules: [],
        nanoflowAccessRules: [],
        entityAccessRules: []
      },
      extensions: []
    };

    expect(() => LowCodeAppSchemaZod.parse(schema)).not.toThrow();
    expect(isLowCodeAppSchema(schema)).toBe(true);
  });
});
