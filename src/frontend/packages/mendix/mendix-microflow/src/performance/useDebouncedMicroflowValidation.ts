import { useCallback, useEffect, useRef, useState } from "react";

import { validateMicroflowSchema } from "../schema/validator";
import type { MicroflowSchema, MicroflowValidationIssue } from "../schema/types";
import type { MicroflowMetadataCatalog } from "../metadata";

export type MicroflowValidationStatus = "idle" | "validating" | "valid" | "invalid" | "failed";

export interface UseDebouncedMicroflowValidationOptions {
  schema: MicroflowSchema;
  metadata: MicroflowMetadataCatalog | null;
  trigger: number;
  delayMs?: number;
  initialIssues?: MicroflowValidationIssue[];
}

export function useDebouncedMicroflowValidation({
  schema,
  metadata,
  trigger,
  delayMs = 400,
  initialIssues = schema.validation.issues ?? [],
}: UseDebouncedMicroflowValidationOptions) {
  const [issues, setIssues] = useState<MicroflowValidationIssue[]>(initialIssues);
  const [status, setStatus] = useState<MicroflowValidationStatus>("idle");
  const [lastValidatedAt, setLastValidatedAt] = useState<Date>();
  const latestSchemaRef = useRef(schema);
  latestSchemaRef.current = schema;

  const runNow = useCallback((targetSchema: MicroflowSchema = latestSchemaRef.current) => {
    setStatus("validating");
    try {
      const result = validateMicroflowSchema({ schema: targetSchema, metadata });
      const nextIssues = result.issues;
      setIssues(nextIssues);
      setLastValidatedAt(new Date());
      setStatus(nextIssues.some(issue => issue.severity === "error") ? "invalid" : "valid");
      return nextIssues;
    } catch (error) {
      const failureIssue: MicroflowValidationIssue = {
        id: `MF_VALIDATOR_FAILED:${Date.now()}`,
        code: "MF_VALIDATOR_FAILED",
        severity: "error",
        source: "root",
        message: error instanceof Error ? error.message : "Validator failed while checking this microflow schema.",
      };
      setIssues([failureIssue]);
      setLastValidatedAt(new Date());
      setStatus("failed");
      return [failureIssue];
    }
  }, [metadata]);

  useEffect(() => {
    if (trigger === 0) {
      return undefined;
    }
    setStatus("validating");
    const timer = window.setTimeout(() => {
      runNow(latestSchemaRef.current);
    }, delayMs);
    return () => window.clearTimeout(timer);
  }, [delayMs, runNow, trigger]);

  return {
    issues,
    setIssues,
    validationStatus: status,
    lastValidatedAt,
    runValidationNow: runNow,
  };
}
