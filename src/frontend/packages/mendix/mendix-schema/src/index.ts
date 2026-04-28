export * from "./types";
export * from "./zod";

import type { LowCodeAppSchema } from "./types";
import { LowCodeAppSchemaZod } from "./zod";

export function isLowCodeAppSchema(input: unknown): input is LowCodeAppSchema {
  return LowCodeAppSchemaZod.safeParse(input).success;
}
