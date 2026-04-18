import { describe, expect, it } from "vitest";
import { createMockLibraryApi } from "../adapter";
import { DEFAULT_PARSING_STRATEGY } from "../../types";

function makeFile(name: string, content = "hello"): File {
  // Vitest 默认 happy-dom/jsdom 环境通常已注入 File；node 环境则使用 globalThis.File
  const FileCtor = (globalThis as { File?: typeof File }).File;
  if (FileCtor) {
    return new FileCtor([content], name, { type: "text/plain" });
  }
  // 退化路径：构造最小可用对象
  return {
    name,
    size: content.length,
    type: "text/plain",
    arrayBuffer: async () => new TextEncoder().encode(content).buffer,
    slice: () => new Blob([content]),
    stream: () => new ReadableStream(),
    text: async () => content
  } as unknown as File;
}

describe("createMockLibraryApi", () => {
  it("seeds three knowledge bases (text/table/image)", async () => {
    const api = createMockLibraryApi();
    const list = await api.listKnowledgeBases({ pageIndex: 1, pageSize: 20 });
    expect(list.total).toBe(3);
    const kinds = list.items.map(item => item.kind).sort();
    expect(kinds).toEqual(["image", "table", "text"]);
  });

  it("creates a new knowledge base with explicit kind", async () => {
    const api = createMockLibraryApi();
    const id = await api.createKnowledgeBase({
      name: "新建文本库",
      type: 0,
      kind: "text",
      tags: ["test"]
    });
    const dto = await api.getKnowledgeBase(id);
    expect(dto.name).toBe("新建文本库");
    expect(dto.kind).toBe("text");
    expect(dto.chunkingProfile?.mode).toBe("fixed");
    expect(dto.retrievalProfile?.topK).toBeGreaterThan(0);
  });

  it("uploadDocument enqueues parse + chunking + index three-stage chain and materializes chunks", async () => {
    const api = createMockLibraryApi({ tickIntervalMs: 0 });
    const list = await api.listKnowledgeBases({ pageIndex: 1, pageSize: 5 });
    const textKb = list.items.find(item => item.kind === "text");
    expect(textKb).toBeTruthy();

    const docId = await api.uploadDocument(textKb!.id, makeFile("doc.txt", "hello atlas"), {
      parsingStrategy: { ...DEFAULT_PARSING_STRATEGY, parsingType: "precise" }
    });

    const before = await api.getDocumentProgress(textKb!.id, docId);
    expect(before.lifecycleStatus).toBe("Uploaded");
    expect(typeof before.parseJobId).toBe("number");

    // v5 §35 / 计划 G9：三段链 parse → chunking → index
    api.__scheduler.advanceUntilStable();

    const after = await api.getDocumentProgress(textKb!.id, docId);
    expect(after.lifecycleStatus).toBe("Ready");
    expect(after.status).toBe(2);

    // 验证 parse 完成时为新文档生成 3-6 个 mock chunk
    const chunks = await api.listChunks(textKb!.id, docId, { pageIndex: 1, pageSize: 50 });
    expect(chunks.items.length).toBeGreaterThanOrEqual(3);
    expect(chunks.items.length).toBeLessThanOrEqual(6);

    // 验证三种类型的 job 都至少出现一次
    const jobs = await api.listJobs!(textKb!.id, { pageIndex: 1, pageSize: 50 });
    const types = new Set(jobs.items.filter(j => j.documentId === docId).map(j => j.type));
    expect(types.has("parse")).toBe(true);
    expect(types.has("chunking")).toBe(true);
    expect(types.has("index")).toBe(true);
  });

  it("table KB has columns + rows", async () => {
    const api = createMockLibraryApi();
    const list = await api.listKnowledgeBases({ pageIndex: 1, pageSize: 10 });
    const tableKb = list.items.find(item => item.kind === "table");
    expect(tableKb).toBeTruthy();
    const docs = await api.listDocuments(tableKb!.id, { pageIndex: 1, pageSize: 10 });
    expect(docs.items.length).toBeGreaterThan(0);
    const docId = docs.items[0].id;
    const cols = await api.listTableColumns!(tableKb!.id, docId);
    expect(cols.length).toBe(5);
    const rows = await api.listTableRows!(tableKb!.id, docId, { pageIndex: 1, pageSize: 10 });
    expect(rows.items.length).toBe(8);
  });

  it("image KB has image items with annotations", async () => {
    const api = createMockLibraryApi();
    const list = await api.listKnowledgeBases({ pageIndex: 1, pageSize: 10 });
    const imageKb = list.items.find(item => item.kind === "image");
    expect(imageKb).toBeTruthy();
    const docs = await api.listDocuments(imageKb!.id, { pageIndex: 1, pageSize: 10 });
    const docId = docs.items[0].id;
    const items = await api.listImageItems!(imageKb!.id, docId, { pageIndex: 1, pageSize: 10 });
    expect(items.items.length).toBe(4);
    const annotations = items.items.flatMap(item => item.annotations);
    expect(annotations.some(a => a.type === "caption")).toBe(true);
    expect(annotations.some(a => a.type === "ocr")).toBe(true);
    expect(annotations.some(a => a.type === "tag")).toBe(true);
  });

  it("retrieval test returns hits with rerankScore when profile enables rerank", async () => {
    const api = createMockLibraryApi();
    const list = await api.listKnowledgeBases({ pageIndex: 1, pageSize: 10 });
    const textKb = list.items.find(item => item.kind === "text")!;
    await api.updateRetrievalProfile!(textKb.id, {
      ...textKb.retrievalProfile!,
      enableRerank: true,
      enableQueryRewrite: true
    });
    const result = await api.runRetrievalTest!(textKb.id, { query: "等保 加密" });
    expect(result.length).toBeGreaterThan(0);
    expect(result[0].rerankScore ?? 0).toBeGreaterThan(0);
    const log = await api.listRetrievalLogs!(textKb.id, { pageIndex: 1, pageSize: 5 });
    expect(log.items.length).toBeGreaterThan(0);
    expect(log.items[0].rewrittenQuery).toContain("rewritten by mock");
  });

  it("dead-letter retry transitions status back to Retrying then Succeeded", async () => {
    const api = createMockLibraryApi({ tickIntervalMs: 0, withFailures: true });
    const allJobs = await api.listJobsAcrossKnowledgeBases!({ pageIndex: 1, pageSize: 50, status: "DeadLetter" });
    expect(allJobs.items.length).toBeGreaterThan(0);
    const dead = allJobs.items[0];
    await api.retryDeadLetter!(dead.knowledgeBaseId, dead.id);
    api.__scheduler.advanceUntilStable();
    const after = await api.getJob!(dead.knowledgeBaseId, dead.id);
    expect(after.status).toBe("Succeeded");
  });

  it("delete knowledge base is blocked when bindings exist", async () => {
    const api = createMockLibraryApi();
    const list = await api.listKnowledgeBases({ pageIndex: 1, pageSize: 10 });
    const textKb = list.items.find(item => item.kind === "text")!;
    expect(textKb.bindingCount ?? 0).toBeGreaterThan(0);
    await expect(api.deleteKnowledgeBase(textKb.id)).rejects.toThrow(/still bound by/);
  });

  it("permission grant/revoke roundtrip", async () => {
    const api = createMockLibraryApi();
    const list = await api.listKnowledgeBases({ pageIndex: 1, pageSize: 10 });
    const kb = list.items[0];
    const id = await api.grantPermission!(kb.id, {
      scope: "kb",
      scopeId: String(kb.id),
      knowledgeBaseId: kb.id,
      subjectType: "user",
      subjectId: "u_test",
      subjectName: "Test User",
      actions: ["view", "retrieve"]
    });
    const list1 = await api.listPermissions!(kb.id, { pageIndex: 1, pageSize: 50 });
    expect(list1.items.some(p => p.id === id)).toBe(true);
    await api.revokePermission!(kb.id, id);
    const list2 = await api.listPermissions!(kb.id, { pageIndex: 1, pageSize: 50 });
    expect(list2.items.some(p => p.id === id)).toBe(false);
  });

  it("version snapshot/release/diff (deep diff)", async () => {
    const api = createMockLibraryApi();
    const list = await api.listKnowledgeBases({ pageIndex: 1, pageSize: 10 });
    const kb = list.items[0];
    const versions = await api.listVersions!(kb.id, { pageIndex: 1, pageSize: 50 });
    const baseline = versions.items.find(v => v.status === "released")!;
    const newVersionId = await api.createVersionSnapshot!(kb.id, { label: "v-test", note: "unit test" });
    await api.releaseVersion!(kb.id, newVersionId);
    const diff = await api.diffVersions!(kb.id, baseline.id, newVersionId);
    // v5 §40 / 计划 G8：真 deepDiff 按字段对比；至少有 label/snapshotRef/status 等差异
    expect(diff.entries.length).toBeGreaterThanOrEqual(2);
    expect(diff.entries.some(entry => entry.kind === "label")).toBe(true);
  });

  it("provider configs cover all five roles", async () => {
    const api = createMockLibraryApi();
    const configs = await api.listProviderConfigs!();
    const roles = Array.from(new Set(configs.map(c => c.role))).sort();
    expect(roles).toEqual(["embedding", "generation", "storage", "upload", "vector"]);
  });
});
