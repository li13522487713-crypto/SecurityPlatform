import { validateMicroflowSchema, type MicroflowAuthoringSchema, type MicroflowValidationIssue } from "@atlas/microflow";
import type { MicroflowMetadataCatalog } from "@atlas/microflow/metadata";

import { MicroflowApiClient, type MicroflowApiClientOptions } from "./http/microflow-api-client";

export type MicroflowValidationMode = "edit" | "save" | "publish" | "testRun";

export interface MicroflowValidationInput {
  resourceId?: string;
  schema: MicroflowAuthoringSchema;
  metadata?: MicroflowMetadataCatalog;
  mode: MicroflowValidationMode;
}

export interface MicroflowValidationResult {
  issues: MicroflowValidationIssue[];
  summary: {
    errorCount: number;
    warningCount: number;
    infoCount: number;
  };
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
          includeWarnings: true,
          includeInfo: true,
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
    validate(input) {
      const id = input.resourceId ?? input.schema.id;
      return client.post<MicroflowValidationResult>(`/api/microflows/${encodeURIComponent(id)}/validate`, {
        schema: input.schema,
        mode: input.mode,
        includeWarnings: true,
        includeInfo: true,
      });
    },
  };
}
