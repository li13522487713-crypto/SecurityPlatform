import { HttpResponse, type HttpHandler } from "msw";

import type { PublishMicroflowApiRequest } from "../api/microflow-publish-api-contract";
import { validatePublishVersion } from "../../publish/microflow-publish-utils";
import type { MicroflowVersionSummary } from "../../versions/microflow-version-types";
import { analyzeMockImpact, clone, createMockPublishSnapshot, createMockVersionSummary, getMockResource, saveMockResource } from "./mock-api-store";
import type { MicroflowContractMockStore } from "./mock-api-types";
import { ok, notFound, publishBlocked, versionConflict } from "./mock-api-response";
import { mockPost } from "./mock-handler-utils";
import { validateMockMicroflow } from "./mock-validation-handlers";

function normalizeVersion(version: string): string {
  return version.trim() || "1.0.0";
}

export function publishMockMicroflow(store: MicroflowContractMockStore, id: string, input: PublishMicroflowApiRequest) {
  const current = getMockResource(store, id);
  if (!current) {
    return { kind: "notFound" as const };
  }
  const version = normalizeVersion(input.version);
  const existingVersions = store.versions.get(id) ?? [];
  const versionValidation = validatePublishVersion(version, existingVersions);
  if (!versionValidation.valid) {
    return { kind: "versionConflict" as const, message: versionValidation.message };
  }
  const validation = validateMockMicroflow(store, {
    schema: current.schema,
    mode: "publish",
    includeInfo: true,
    includeWarnings: true,
  });
  if (validation.summary.errorCount > 0) {
    return { kind: "publishBlocked" as const, message: "存在错误，无法发布。", validationIssues: validation.issues };
  }
  const impactAnalysis = analyzeMockImpact(store, current, version);
  if (impactAnalysis.summary.highImpactCount > 0 && !input.confirmBreakingChanges && !input.force) {
    return { kind: "publishBlocked" as const, message: "存在高影响破坏性变更，发布前需要二次确认。", raw: impactAnalysis };
  }
  const timestamp = new Date().toISOString();
  const schema = {
    ...clone(current.schema),
    audit: { ...current.schema.audit, status: "published" as const, version, updatedAt: timestamp, updatedBy: current.updatedBy },
  };
  const snapshot = createMockPublishSnapshot(id, version, schema, {
    publishedAt: timestamp,
    publishedBy: current.updatedBy ?? current.createdBy,
    description: input.description,
  });
  const resource = saveMockResource(store, {
    ...current,
    status: "published",
    publishStatus: "published",
    latestPublishedVersion: version,
    version,
    updatedAt: timestamp,
    schema,
  });
  store.publishSnapshots.set(snapshot.id, snapshot);
  const versionSummary: MicroflowVersionSummary = createMockVersionSummary(snapshot, {
    createdBy: current.updatedBy ?? current.createdBy,
    referenceCount: resource.referenceCount,
    isLatestPublished: true,
  });
  store.versions.set(id, [
    versionSummary,
    ...existingVersions.map(item => ({ ...item, isLatestPublished: false })),
  ]);
  return {
    kind: "success" as const,
    result: {
      resource,
      version: versionSummary,
      snapshot,
      validationSummary: validation.summary,
      impactAnalysis,
    },
  };
}

export function createMicroflowPublishMockHandlers(store: MicroflowContractMockStore): HttpHandler[] {
  return [
    ...mockPost("/api/microflows/:id/publish", async ({ request, params }) => {
      const input = await request.json() as PublishMicroflowApiRequest;
      if (!input.version?.trim()) {
        return HttpResponse.json(publishBlocked({ message: "版本号必填。" }), { status: 422 });
      }
      const result = publishMockMicroflow(store, params.id, input);
      if (result.kind === "notFound") {
        return HttpResponse.json(notFound(), { status: 404 });
      }
      if (result.kind === "versionConflict") {
        return HttpResponse.json(versionConflict(result.message), { status: 409 });
      }
      if (result.kind === "publishBlocked") {
        return HttpResponse.json(publishBlocked({ message: result.message, validationIssues: result.validationIssues, raw: result.raw }), { status: 422 });
      }
      return HttpResponse.json(ok(result.result));
    }),
  ];
}
