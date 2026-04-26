export type MicroflowVariableAvailability = "definite" | "maybe" | "unavailable";

export type MicroflowVariableSourceKind =
  | "parameter"
  | "actionOutput"
  | "localVariable"
  | "loopIterator"
  | "errorContext"
  | "system";
