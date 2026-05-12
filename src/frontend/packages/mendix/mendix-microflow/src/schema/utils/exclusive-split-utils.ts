import type { MicroflowExclusiveSplit, MicroflowObject } from "../types";

export function isExclusiveSplit(object: MicroflowObject | undefined): object is MicroflowExclusiveSplit {
  return Boolean(object && object.kind === "exclusiveSplit");
}

export function isBooleanExclusiveSplit(object: MicroflowObject | undefined): object is MicroflowExclusiveSplit {
  return isExclusiveSplit(object) && object.splitCondition.resultType === "boolean";
}

export function isEnumerationExclusiveSplit(object: MicroflowObject | undefined): object is MicroflowExclusiveSplit {
  return isExclusiveSplit(object) && object.splitCondition.kind === "expression" && object.splitCondition.resultType === "enumeration";
}
