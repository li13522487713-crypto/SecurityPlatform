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

function getValidationServiceCode(error: unknown): string {
  const apiError = typeof error === "object" && error !== null && "apiError" in error
    ? (error as { apiError?: { code?: string; httpStatus?: number } }).apiError
    : undefined;
  if (apiError?.httpStatus === 401 || apiError?.code === "MICROFLOW_UNAUTHORIZED") {
    return "MICROFLOW_VALIDATION_UNAUTHORIZED";
  }
  if (apiError?.httpStatus === 403 || apiError?.code === "MICROFLOW_PERMISSION_DENIED") {
    return "MICROFLOW_VALIDATION_FORBIDDEN";
  }
  if (apiError?.httpStatus && apiError.httpStatus >= 500) {
    return "MICROFLOW_VALIDATION_SERVER_ERROR";
  }
  if (apiError?.httpStatus === 404) {
    return "MICROFLOW_VALIDATE_API_NOT_FOUND";
  }
  return "MICROFLOW_VALIDATION_API_FAILED";
}

function createValidationServiceIssue(error: unknown, microflowId: string, severity: MicroflowValidationIssue["severity"] = "warning"): MicroflowValidationIssue {
  const code = getValidationServiceCode(error);
  return {
    id: `${microflowId}:server:${code}:validation`,
    microflowId,
    code,
    severity,
    source: "server",
    fieldPath: "validation",
    message: error instanceof Error ? `后端校验失败：${error.message}` : "后端校验失败，请检查网络、权限或服务状态。",
    blockSave: severity === "error",
    blockPublish: severity === "error",
  };
}

export function useDebouncedMicroflowValidation({
  schema,
  metadata,
  trigger,
  delayMs = 650,
  initialIssues = schema.validation.issues ?? [],
  validationAdapter,
  resourceId,
}: UseDebouncedMicroflowValidationOptions) {
  const activeMicroflowId = resourceId ?? schema.id;
  const [issuesByMicroflowId, setIssuesByMicroflowId] = useState<Record<string, MicroflowValidationIssue[]>>({ [activeMicroflowId]: initialIssues });
  const [statusByMicroflowId, setStatusByMicroflowId] = useState<Record<string, MicroflowValidationStatus>>({ [activeMicroflowId]: "idle" });
  const [lastValidatedAtByMicroflowId, setLastValidatedAtByMicroflowId] = useState<Record<string, Date | undefined>>({});
  const [lastErrorByMicroflowId, setLastErrorByMicroflowId] = useState<Record<string, unknown>>({});
  const latestSchemaRef = useRef(schema);
  const requestIdsByMicroflowIdRef = useRef<Record<string, number>>({});
  const issuesByMicroflowIdRef = useRef(issuesByMicroflowId);
  latestSchemaRef.current = schema;
  issuesByMicroflowIdRef.current = issuesByMicroflowId;
  const issues = useMemo(() => issuesByMicroflowId[activeMicroflowId] ?? [], [activeMicroflowId, issuesByMicroflowId]);
  const status = statusByMicroflowId[activeMicroflowId] ?? "idle";
  const lastValidatedAt = lastValidatedAtByMicroflowId[activeMicroflowId];
  const lastError = lastErrorByMicroflowId[activeMicroflowId];

  const setIssues = useCallback((nextIssues: MicroflowValidationIssue[]) => {
    const targetId = resourceId ?? latestSchemaRef.current.id;
    setIssuesByMicroflowId(current => ({ ...current, [targetId]: nextIssues }));
  }, [resourceId]);

  const runNow = useCallback(async (targetSchema: MicroflowSchema = latestSchemaRef.current) => {
    const targetId = resourceId ?? targetSchema.id;
    const requestId = (requestIdsByMicroflowIdRef.current[targetId] ?? 0) + 1;
    requestIdsByMicroflowIdRef.current[targetId] = requestId;
    setStatusByMicroflowId(current => ({ ...current, [targetId]: "validating" }));
    setLastErrorByMicroflowId(current => ({ ...current, [targetId]: undefined }));
    try {
      const localResult = validateMicroflowSchema({
        schema: targetSchema,
        metadata,
        options: { mode: "edit", includeWarnings: true, includeInfo: true },
      });
      let nextIssues = localResult.issues;
      let serverError: unknown;
      if (validationAdapter) {
        try {
          const serverResult = await validationAdapter.validate({
            resourceId: targetId,
            schema: targetSchema,
            metadata,
            mode: "edit",
            includeWarnings: true,
            includeInfo: true,
          });
          nextIssues = [
            ...localResult.issues,
            ...serverResult.issues.map(issue => ({
              ...issue,
              id: issue.id.startsWith(`${targetId}:server:`) ? issue.id : `${targetId}:server:${issue.id}`,
              microflowId: targetId,
              source: issue.source ?? "server",
              blockSave: issue.blockSave ?? issue.severity === "error",
              blockPublish: issue.blockPublish ?? issue.severity === "error",
            })),
          ];
        } catch (error) {
          serverError = error;
          nextIssues = [...localResult.issues, createValidationServiceIssue(error, targetId, "warning")];
        }
      }
      const validatedAt = new Date();
      if (requestIdsByMicroflowIdRef.current[targetId] !== requestId || latestSchemaRef.current.id !== targetSchema.id) {
        return issuesByMicroflowIdRef.current[targetId] ?? [];
      }
      setIssuesByMicroflowId(current => ({ ...current, [targetId]: nextIssues }));
      setLastValidatedAtByMicroflowId(current => ({ ...current, [targetId]: validatedAt }));
      if (serverError) {
        setLastErrorByMicroflowId(current => ({ ...current, [targetId]: serverError }));
      }
      setStatusByMicroflowId(current => ({ ...current, [targetId]: serverError ? "failed" : nextIssues.some(issue => issue.severity === "error") ? "invalid" : "valid" }));
      return nextIssues;
    } catch (error) {
      const failureIssue = createValidationServiceIssue(error, targetId, "warning");
      if (requestIdsByMicroflowIdRef.current[targetId] !== requestId || latestSchemaRef.current.id !== targetSchema.id) {
        return issuesByMicroflowIdRef.current[targetId] ?? [];
      }
      setIssuesByMicroflowId(current => ({ ...current, [targetId]: [failureIssue] }));
      setLastValidatedAtByMicroflowId(current => ({ ...current, [targetId]: new Date() }));
      setLastErrorByMicroflowId(current => ({ ...current, [targetId]: error }));
      setStatusByMicroflowId(current => ({ ...current, [targetId]: "failed" }));
      return [failureIssue];
    }
  }, [metadata, resourceId, validationAdapter]);

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
    lastError,
    runValidationNow: runNow,
  };
}
