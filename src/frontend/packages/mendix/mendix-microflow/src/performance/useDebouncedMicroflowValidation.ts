import { useCallback, useEffect, useRef, useState } from "react";

import { validateMicroflowSchema } from "../schema/validator";
import type { MicroflowSchema, MicroflowValidationIssue } from "../schema/types";
import type { MicroflowMetadataCatalog } from "../metadata";

export type MicroflowValidationStatus = "idle" | "validating" | "valid" | "invalid" | "failed";
export type MicroflowValidationMode = "edit" | "save" | "publish" | "testRun";

export interface MicroflowValidationAdapterLike {
  validate(input: {
    resourceId?: string;
    schema: MicroflowSchema;
    metadata?: MicroflowMetadataCatalog | null;
    mode: MicroflowValidationMode;
    includeInfo?: boolean;
    includeWarnings?: boolean;
  }): Promise<{
    issues: MicroflowValidationIssue[];
    summary: {
      errorCount: number;
      warningCount: number;
      infoCount: number;
    };
    serverValidatedAt?: string;
  }>;
}

export interface UseDebouncedMicroflowValidationOptions {
  schema: MicroflowSchema;
  metadata: MicroflowMetadataCatalog | null;
  trigger: number;
  delayMs?: number;
  initialIssues?: MicroflowValidationIssue[];
  validationAdapter?: MicroflowValidationAdapterLike;
  resourceId?: string;
}

function createValidationServiceIssue(error: unknown, severity: MicroflowValidationIssue["severity"] = "warning"): MicroflowValidationIssue {
  return {
    id: `MICROFLOW_VALIDATION_SERVICE_UNAVAILABLE:${Date.now()}`,
    code: "MICROFLOW_VALIDATION_SERVICE_UNAVAILABLE",
    severity,
    source: "root",
    fieldPath: "validation",
    message: error instanceof Error ? `校验服务不可用：${error.message}` : "校验服务不可用，请检查后端服务或网络。",
  };
}

export function useDebouncedMicroflowValidation({
  schema,
  metadata,
  trigger,
  delayMs = 400,
  initialIssues = schema.validation.issues ?? [],
  validationAdapter,
  resourceId,
}: UseDebouncedMicroflowValidationOptions) {
  const [issues, setIssues] = useState<MicroflowValidationIssue[]>(initialIssues);
  const [status, setStatus] = useState<MicroflowValidationStatus>("idle");
  const [lastValidatedAt, setLastValidatedAt] = useState<Date>();
  const latestSchemaRef = useRef(schema);
  latestSchemaRef.current = schema;

  const runNow = useCallback(async (targetSchema: MicroflowSchema = latestSchemaRef.current) => {
    setStatus("validating");
    try {
      const result = validationAdapter
        ? await validationAdapter.validate({
            resourceId: resourceId ?? targetSchema.id,
            schema: targetSchema,
            metadata,
            mode: "edit",
            includeWarnings: true,
            includeInfo: true,
          })
        : validateMicroflowSchema({ schema: targetSchema, metadata });
      const nextIssues = result.issues;
      const validatedAt = "serverValidatedAt" in result && result.serverValidatedAt ? new Date(result.serverValidatedAt) : new Date();
      setIssues(nextIssues);
      setLastValidatedAt(validatedAt);
      setStatus(nextIssues.some(issue => issue.severity === "error") ? "invalid" : "valid");
      return nextIssues;
    } catch (error) {
      const failureIssue = createValidationServiceIssue(error, "warning");
      setIssues([failureIssue]);
      setLastValidatedAt(new Date());
      setStatus("failed");
      return [failureIssue];
    }
  }, [metadata, resourceId, validationAdapter]);

  useEffect(() => {
    if (trigger === 0) {
      return undefined;
    }
    setStatus("validating");
    const timer = window.setTimeout(() => {
      void runNow(latestSchemaRef.current);
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
