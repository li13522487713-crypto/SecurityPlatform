import { useCallback, useEffect, useMemo, useRef, useState } from "react";

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
    id: `MICROFLOW_VALIDATION_SERVICE_UNAVAILABLE:edit`,
    code: "MICROFLOW_VALIDATION_SERVICE_UNAVAILABLE",
    severity,
    source: "server",
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
  resourceId,
}: UseDebouncedMicroflowValidationOptions) {
  const activeMicroflowId = resourceId ?? schema.id;
  const [issuesByMicroflowId, setIssuesByMicroflowId] = useState<Record<string, MicroflowValidationIssue[]>>({ [activeMicroflowId]: initialIssues });
  const [statusByMicroflowId, setStatusByMicroflowId] = useState<Record<string, MicroflowValidationStatus>>({ [activeMicroflowId]: "idle" });
  const [lastValidatedAtByMicroflowId, setLastValidatedAtByMicroflowId] = useState<Record<string, Date | undefined>>({});
  const latestSchemaRef = useRef(schema);
  const requestIdsByMicroflowIdRef = useRef<Record<string, number>>({});
  const issuesByMicroflowIdRef = useRef(issuesByMicroflowId);
  latestSchemaRef.current = schema;
  issuesByMicroflowIdRef.current = issuesByMicroflowId;
  const issues = useMemo(() => issuesByMicroflowId[activeMicroflowId] ?? [], [activeMicroflowId, issuesByMicroflowId]);
  const status = statusByMicroflowId[activeMicroflowId] ?? "idle";
  const lastValidatedAt = lastValidatedAtByMicroflowId[activeMicroflowId];

  const setIssues = useCallback((nextIssues: MicroflowValidationIssue[]) => {
    const targetId = resourceId ?? latestSchemaRef.current.id;
    setIssuesByMicroflowId(current => ({ ...current, [targetId]: nextIssues }));
  }, [resourceId]);

  const runNow = useCallback(async (targetSchema: MicroflowSchema = latestSchemaRef.current) => {
    const targetId = resourceId ?? targetSchema.id;
    const requestId = (requestIdsByMicroflowIdRef.current[targetId] ?? 0) + 1;
    requestIdsByMicroflowIdRef.current[targetId] = requestId;
    setStatusByMicroflowId(current => ({ ...current, [targetId]: "validating" }));
    try {
      const result = validateMicroflowSchema({ schema: targetSchema, metadata });
      const nextIssues = result.issues;
      const validatedAt = new Date();
      if (requestIdsByMicroflowIdRef.current[targetId] !== requestId || latestSchemaRef.current.id !== targetSchema.id) {
        return issuesByMicroflowIdRef.current[targetId] ?? [];
      }
      setIssuesByMicroflowId(current => ({ ...current, [targetId]: nextIssues }));
      setLastValidatedAtByMicroflowId(current => ({ ...current, [targetId]: validatedAt }));
      setStatusByMicroflowId(current => ({ ...current, [targetId]: nextIssues.some(issue => issue.severity === "error") ? "invalid" : "valid" }));
      return nextIssues;
    } catch (error) {
      const failureIssue = createValidationServiceIssue(error, "warning");
      if (requestIdsByMicroflowIdRef.current[targetId] !== requestId || latestSchemaRef.current.id !== targetSchema.id) {
        return issuesByMicroflowIdRef.current[targetId] ?? [];
      }
      setIssuesByMicroflowId(current => ({ ...current, [targetId]: [failureIssue] }));
      setLastValidatedAtByMicroflowId(current => ({ ...current, [targetId]: new Date() }));
      setStatusByMicroflowId(current => ({ ...current, [targetId]: "failed" }));
      return [failureIssue];
    }
  }, [metadata, resourceId]);

  useEffect(() => {
    const targetId = resourceId ?? schema.id;
    setIssuesByMicroflowId(current => current[targetId] ? current : { ...current, [targetId]: initialIssues });
    setStatusByMicroflowId(current => ({ ...current, [targetId]: "validating" }));
    const timer = window.setTimeout(() => {
      void runNow(latestSchemaRef.current);
    }, delayMs);
    return () => window.clearTimeout(timer);
  }, [activeMicroflowId, delayMs, metadata?.version, resourceId, runNow, schema.id, trigger]);

  return {
    issues,
    setIssues,
    validationStatus: status,
    lastValidatedAt,
    runValidationNow: runNow,
  };
}
