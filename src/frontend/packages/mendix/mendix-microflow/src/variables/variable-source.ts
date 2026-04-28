export type MicroflowVariableAvailability = "definite" | "maybe" | "unavailable";

export type MicroflowVariableSourceKind =
  | "parameter"
  | "actionOutput"
  | "createVariable"
  | "localVariable"
  | "loopIterator"
  | "errorContext"
  | "system"
  | "microflowReturn"
  | "restResponse";
