import { describe, expect, it } from "vitest";
import { parseLowCodeAppSchema } from "@atlas/mendix-schema";
import { validateLowCodeAppSchema } from "@atlas/mendix-validator";
import { SAMPLE_PROCUREMENT_APP } from "./sample-app";

describe("mendix-studio-core sample", () => {
  it("should be valid schema shape", () => {
    expect(() => parseLowCodeAppSchema(SAMPLE_PROCUREMENT_APP)).not.toThrow();
  });

  it("should have no blocking validation errors", () => {
    const errors = validateLowCodeAppSchema(SAMPLE_PROCUREMENT_APP);
    expect(errors.filter(error => error.severity === "error").length).toBe(0);
  });
});
