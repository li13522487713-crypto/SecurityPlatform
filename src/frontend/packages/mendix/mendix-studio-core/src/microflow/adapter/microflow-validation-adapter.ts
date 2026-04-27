import { validateMicroflowSchema, type MicroflowAuthoringSchema, type MicroflowValidationIssue } from "@atlas/microflow";
import type { MicroflowMetadataCatalog } from "@atlas/microflow/metadata";

import { MicroflowApiClient, type MicroflowApiClientOptions } from "./http/microflow-api-client";

function asServerValidationIssue(issue: MicroflowValidationIssue): MicroflowValidationIssue {
  return {
    ...issue,
    id: issue.id.startsWith("server:") ? issue.id : `server:${issue.id}`,
    source: "server",
  };
}

export type MicroflowValidationMode = "edit" | "save" | "publish" | "testRun";

export interface MicroflowValidationInput {
  resourceId?: string;
  schema: MicroflowAuthoringSchema;
  metadata?: MicroflowMetadataCatalog;
  mode: MicroflowValidationMode;
  includeInfo?: boolean;
  includeWarnings?: boolean;
}

export interface MicroflowValidationResult {
  issues: MicroflowValidationIssue[];
  summary: {
    errorCount: number;
    warningCount: number;
    infoCount: number;
  };
  serverValidatedAt?: string;
}

export interface MicroflowValidationAdapter {
  validate(input: MicroflowValidationInput): Promise<MicroflowValidationResult>;
}

export function createLocalMicroflowValidationAdapter(): MicroflowValidationAdapter {
  return {
    async validate(input) {
      const result = validateMicroflowSchema({
        schema: input.schema,
        metadata: input.metadata,
        options: {
          mode: input.mode,
          includeWarnings: input.includeWarnings ?? true,
          includeInfo: input.includeInfo ?? true,
        },
      });
      return {
        issues: result.issues,
        summary: result.summary,
      };
    },
  };
}

export interface HttpMicroflowValidationAdapterOptions extends MicroflowApiClientOptions {
  apiClient?: MicroflowApiClient;
}

export function createHttpMicroflowValidationAdapter(options: HttpMicroflowValidationAdapterOptions): MicroflowValidationAdapter {
  const client = options.apiClient ?? new MicroflowApiClient(options);
  return {
    async validate(input) {
      const id = input.resourceId ?? input.schema.id;
      const result = await client.post<MicroflowValidationResult>(`/api/microflows/${encodeURIComponent(id)}/validate`, {
        schema: input.schema,
        mode: input.mode,
        includeWarnings: input.includeWarnings ?? true,
        includeInfo: input.includeInfo ?? true,
      });
      const issues = result.issues.map(asServerValidationIssue);
      return {
        ...result,
        issues,
        summary: {
          errorCount: issues.filter(issue => issue.severity === "error").length,
          warningCount: issues.filter(issue => issue.severity === "warning").length,
          infoCount: issues.filter(issue => issue.severity === "info").length,
        },
      };
    },
  };
}
