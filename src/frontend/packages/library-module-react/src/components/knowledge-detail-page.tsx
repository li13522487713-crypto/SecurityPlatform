import { Fragment, startTransition, useEffect, useMemo, useState } from "react";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import {
  Banner,
  Button,
  Empty,
  Input,
  Progress,
  SideSheet,
  Space,
  Table,
  Tag,
  TextArea,
  Toast,
  Typography
} from "@douyinfe/semi-ui";
import {
  IconArrowLeft,
  IconDelete,
  IconEdit,
  IconPlus,
  IconRefresh,
  IconUpload
} from "@douyinfe/semi-icons";
import type {
  ChunkCreateRequest,
  DocumentChunkDto,
  DocumentProcessingStatus,
  KnowledgeDetailPageProps,
  KnowledgeDocumentDto,
  KnowledgeRetrievalTestItem
} from "../types";
import { getLibraryCopy } from "../copy";
import { formatDateTime, mapKnowledgeType, resolveDocumentStatus } from "../utils";

const DEFAULT_CHUNK_FORM: ChunkCreateRequest = {
  documentId: 0,
  chunkIndex: 0,
  content: "",
  startOffset: 0,
  endOffset: 0
};

function tokenizeQuery(query: string): string[] {
  return Array.from(
    new Set(
      query
        .toLowerCase()
        .split(/[\s,，.。;；!！?？/\\()（）\[\]【】{}<>|"'`、]+/u)
        .map(token => token.trim())
        .filter(token => token.length >= 2 && token.length < 32)
    )
  );
}

function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function highlightContent(content: string, tokens: string[]) {
  if (!content || tokens.length === 0) {
    return content;
  }
  const pattern = new RegExp(`(${tokens.map(escapeRegExp).join("|")})`, "gi");
  const parts = content.split(pattern);
  return parts.map((part, index) => {
    if (!part) {
      return null;
    }
    const isHit = tokens.some(token => part.toLowerCase() === token);
    return isHit ? (
      <mark key={index} style={{ background: "#fff1c2", padding: "0 2px", borderRadius: 2 }}>{part}</mark>
    ) : (
      <Fragment key={index}>{part}</Fragment>
    );
  });
}

function parseTagsJson(tagsJson?: string | null): string[] {
  if (!tagsJson) {
    return [];
  }
  try {
    const parsed = JSON.parse(tagsJson);
    return Array.isArray(parsed) ? parsed.filter((item): item is string => typeof item === "string") : [];
  } catch {
    return [];
  }
}

function parseColumnHeaders(headersJson?: string | null): string[] {
  if (!headersJson) {
    return [];
  }
  try {
    const parsed = JSON.parse(headersJson);
    return Array.isArray(parsed) ? parsed.filter((item): item is string => typeof item === "string") : [];
  } catch {
    return [];
  }
}

export function KnowledgeDetailPage({
  api,
  locale,
  appKey,
  spaceId,
  knowledgeBaseId,
  onNavigate,
  resourceReferencesSlot
}: KnowledgeDetailPageProps) {
  const copy = getLibraryCopy(locale);
  const [loading, setLoading] = useState(false);
  const [knowledge, setKnowledge] = useState<Awaited<ReturnType<typeof api.getKnowledgeBase>> | null>(null);
  const [documents, setDocuments] = useState<KnowledgeDocumentDto[]>([]);
  const [selectedDocumentId, setSelectedDocumentId] = useState<number | null>(null);
  const [chunksLoading, setChunksLoading] = useState(false);
  const [chunks, setChunks] = useState<DocumentChunkDto[]>([]);
  const [retrievalQuery, setRetrievalQuery] = useState("");
  const [testing, setTesting] = useState(false);
  const [testResults, setTestResults] = useState<KnowledgeRetrievalTestItem[]>([]);
  const [sheetVisible, setSheetVisible] = useState(false);
  const [editingChunk, setEditingChunk] = useState<DocumentChunkDto | null>(null);
  const [chunkForm, setChunkForm] = useState<ChunkCreateRequest>(DEFAULT_CHUNK_FORM);

  async function loadKnowledgeBase() {
    setLoading(true);
    try {
      const [kb, docs] = await Promise.all([
        api.getKnowledgeBase(knowledgeBaseId),
        api.listDocuments(knowledgeBaseId, { pageIndex: 1, pageSize: 20 })
      ]);

      startTransition(() => {
        setKnowledge(kb);
        setDocuments(docs.items);
        setSelectedDocumentId((current: number | null) => current ?? docs.items[0]?.id ?? null);
      });
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void loadKnowledgeBase();
  }, [knowledgeBaseId]);

  useEffect(() => {
    let disposed = false;

    async function loadChunks() {
      if (!selectedDocumentId) {
        setChunks([]);
        return;
      }

      setChunksLoading(true);
      try {
        const response = await api.listChunks(knowledgeBaseId, selectedDocumentId, {
          pageIndex: 1,
          pageSize: 100
        });
        if (!disposed) {
          setChunks(response.items);
        }
      } catch (error) {
        if (!disposed) {
          Toast.error((error as Error).message);
        }
      } finally {
        if (!disposed) {
          setChunksLoading(false);
        }
      }
    }

    void loadChunks();
    return () => {
      disposed = true;
    };
  }, [api, knowledgeBaseId, selectedDocumentId]);

  const documentColumns = useMemo<ColumnProps<KnowledgeDocumentDto>[]>(() => [
    {
      title: copy.documentList,
      dataIndex: "fileName",
      render: (value: unknown, record: KnowledgeDocumentDto) => (
        <div>
          <div style={{ fontWeight: 600 }}>{String(value)}</div>
          <Typography.Text type="tertiary" size="small">
            {record.errorMessage || formatDateTime(record.processedAt ?? record.createdAt)}
          </Typography.Text>
          {record.tagsJson && record.tagsJson !== "[]" ? (
            <Typography.Text type="tertiary" size="small" style={{ display: "block" }}>
              tags: {record.tagsJson}
            </Typography.Text>
          ) : null}
        </div>
      )
    },
    {
      title: copy.resourceStatus,
      dataIndex: "status",
      width: 120,
      render: (value: unknown) => {
        const statusCode = Number(value) as DocumentProcessingStatus;
        const status = resolveDocumentStatus(statusCode);
        const color = status === "ready" ? "green" : status === "failed" ? "red" : status === "processing" ? "orange" : "grey";
        return <Tag color={color}>{copy.docStatusLabels[statusCode]}</Tag>;
      }
    },
    {
      title: copy.chunks,
      dataIndex: "chunkCount",
      width: 100
    },
    {
      title: copy.actions,
      width: 180,
      render: (_value: unknown, record: KnowledgeDocumentDto) => (
        <Space spacing={4}>
          <Button
            theme="borderless"
            icon={<IconRefresh />}
            onClick={async event => {
              event.stopPropagation();
              try {
                await api.resegmentDocument(knowledgeBaseId, record.id, {});
                Toast.success(copy.resegment);
                await loadKnowledgeBase();
              } catch (error) {
                Toast.error((error as Error).message);
              }
            }}
          >
            {copy.resegment}
          </Button>
          <Button
            theme="borderless"
            type="danger"
            icon={<IconDelete />}
            onClick={async event => {
              event.stopPropagation();
              if (!window.confirm(copy.delete)) {
                return;
              }

              try {
                await api.deleteDocument(knowledgeBaseId, record.id);
                Toast.success(copy.delete);
                await loadKnowledgeBase();
              } catch (error) {
                Toast.error((error as Error).message);
              }
            }}
          >
            {copy.delete}
          </Button>
        </Space>
      )
    }
  ], [copy, knowledgeBaseId]);

  const isTableKb = knowledge?.type === 1;
  const isImageKb = knowledge?.type === 2;
  const fileIdByDocument = useMemo(() => {
    const map: Record<number, number> = {};
    for (const doc of documents) {
      if (typeof doc.fileId === "number" && doc.fileId > 0) {
        map[doc.id] = doc.fileId;
      }
    }
    return map;
  }, [documents]);

  const chunkColumns = useMemo<ColumnProps<DocumentChunkDto>[]>(() => {
    const baseColumns: ColumnProps<DocumentChunkDto>[] = [
      {
        title: "#",
        dataIndex: "chunkIndex",
        width: 80
      }
    ];

    if (isTableKb) {
      baseColumns.push(
        {
          title: copy.rowIndexLabel,
          dataIndex: "rowIndex",
          width: 80,
          render: (value: unknown) => (value === undefined || value === null ? "-" : String(value))
        },
        {
          title: copy.columnHeadersLabel,
          dataIndex: "columnHeadersJson",
          width: 200,
          render: (value: unknown) => {
            const headers = parseColumnHeaders(value as string | null | undefined);
            if (headers.length === 0) {
              return "-";
            }
            return (
              <Space wrap spacing={4}>
                {headers.slice(0, 8).map(header => (
                  <Tag key={header} size="small">{header}</Tag>
                ))}
              </Space>
            );
          }
        }
      );
    }

    if (isImageKb) {
      baseColumns.push({
        title: copy.imagePreviewLabel,
        width: 120,
        render: (_value: unknown, record: DocumentChunkDto) => {
          const fileId = fileIdByDocument[record.documentId];
          if (!fileId) {
            return "-";
          }
          return (
            <img
              src={`/api/v1/files/${fileId}`}
              alt={`chunk-${record.id}`}
              style={{ maxWidth: 96, maxHeight: 64, objectFit: "cover", borderRadius: 4 }}
              loading="lazy"
            />
          );
        }
      });
    }

    baseColumns.push(
      {
        title: copy.chunkContent,
        dataIndex: "content",
        render: (value: unknown) => (
          <Typography.Text ellipsis={{ showTooltip: true }}>
            {String(value)}
          </Typography.Text>
        )
      },
      {
        title: copy.chunkStart,
        dataIndex: "startOffset",
        width: 100
      },
      {
        title: copy.chunkEnd,
        dataIndex: "endOffset",
        width: 100
      },
      {
        title: copy.actions,
        width: 160,
        render: (_value: unknown, record: DocumentChunkDto) => (
          <Space spacing={4}>
            <Button
              theme="borderless"
              icon={<IconEdit />}
              onClick={() => {
                setEditingChunk(record);
                setChunkForm({
                  documentId: record.documentId,
                  chunkIndex: record.chunkIndex,
                  content: record.content,
                  startOffset: record.startOffset,
                  endOffset: record.endOffset
                });
                setSheetVisible(true);
              }}
            >
              {copy.edit}
            </Button>
            <Button
              theme="borderless"
              type="danger"
              icon={<IconDelete />}
              onClick={async () => {
                if (!window.confirm(copy.delete)) {
                  return;
                }

                try {
                  await api.deleteChunk(knowledgeBaseId, record.id);
                  Toast.success(copy.delete);
                  const response = await api.listChunks(knowledgeBaseId, record.documentId, {
                    pageIndex: 1,
                    pageSize: 100
                  });
                  setChunks(response.items);
                } catch (error) {
                  Toast.error((error as Error).message);
                }
              }}
            >
              {copy.delete}
            </Button>
          </Space>
        )
      }
    );
    return baseColumns;
  }, [api, copy, isTableKb, isImageKb, fileIdByDocument, knowledgeBaseId]);

  async function saveChunk() {
    if (!selectedDocumentId && !chunkForm.documentId) {
      Toast.warning(copy.selectDocumentHint);
      return;
    }

    try {
      if (editingChunk) {
        await api.updateChunk(knowledgeBaseId, editingChunk.id, {
          content: chunkForm.content,
          startOffset: chunkForm.startOffset,
          endOffset: chunkForm.endOffset
        });
      } else {
        await api.createChunk(knowledgeBaseId, {
          ...chunkForm,
          documentId: selectedDocumentId ?? chunkForm.documentId
        });
      }

      setSheetVisible(false);
      setEditingChunk(null);
      setChunkForm(DEFAULT_CHUNK_FORM);
      const response = await api.listChunks(knowledgeBaseId, selectedDocumentId ?? chunkForm.documentId, {
        pageIndex: 1,
        pageSize: 100
      });
      setChunks(response.items);
      await loadKnowledgeBase();
      Toast.success(copy.save);
    } catch (error) {
      Toast.error((error as Error).message);
    }
  }

  async function runRetrievalTest() {
    if (!api.runRetrievalTest) {
      Toast.warning(copy.noTestResult);
      return;
    }

    if (!retrievalQuery.trim()) {
      Toast.warning(copy.retrievalQueryPlaceholder);
      return;
    }

    setTesting(true);
    try {
      const result = await api.runRetrievalTest(knowledgeBaseId, {
        query: retrievalQuery.trim(),
        topK: 5
      });
      setTestResults(result);
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setTesting(false);
    }
  }

  if (!loading && !knowledge) {
    return (
      <div className="atlas-table-card semi-card semi-card-bordered semi-card-shadow">
        <div className="semi-card-body">
          <Empty description={copy.detailEmpty} />
        </div>
      </div>
    );
  }

  return (
    <div className="atlas-library-page" data-testid="app-knowledge-detail-page">
      <div className="atlas-page-header">
        <Space spacing={8}>
          <Button icon={<IconArrowLeft />} onClick={() => onNavigate(`/apps/${encodeURIComponent(appKey)}/studio/knowledge-bases`)}>
            {copy.backToLibrary}
          </Button>
          <div>
            <Typography.Title heading={3} style={{ margin: 0 }}>
              {knowledge?.name}
            </Typography.Title>
            <Typography.Text type="tertiary">
              {knowledge ? copy.typeLabels[knowledge.type] : ""}
            </Typography.Text>
          </div>
        </Space>
        <Button
          type="primary"
          icon={<IconUpload />}
          onClick={() => onNavigate(`/apps/${encodeURIComponent(appKey)}/studio/knowledge-bases/${knowledgeBaseId}/upload?type=${mapKnowledgeType(knowledge?.type ?? 0)}`)}
        >
          {copy.upload}
        </Button>
      </div>

      <div className="atlas-knowledge-grid">
        <div className="atlas-summary-card semi-card semi-card-bordered semi-card-shadow">
          <div className="semi-card-body">
            <Typography.Title heading={5}>{copy.summary}</Typography.Title>
            <div className="atlas-summary-grid">
              <div className="atlas-summary-tile">
                <span>{copy.knowledgeBase}</span>
                <strong>{knowledge?.name ?? "-"}</strong>
              </div>
              <div className="atlas-summary-tile">
                <span>{copy.resourceType}</span>
                <strong>{knowledge ? copy.typeLabels[knowledge.type] : "-"}</strong>
              </div>
              <div className="atlas-summary-tile">
                <span>{copy.documents}</span>
                <strong>{knowledge?.documentCount ?? 0}</strong>
              </div>
              <div className="atlas-summary-tile">
                <span>{copy.chunks}</span>
                <strong>{knowledge?.chunkCount ?? 0}</strong>
              </div>
            </div>
            <Banner type="info" description={copy.uploadProcessingHint} />
          </div>
        </div>

        <div className="atlas-retrieval-card semi-card semi-card-bordered semi-card-shadow">
          <div className="semi-card-body">
            <Typography.Title heading={5}>{copy.retrievalTest}</Typography.Title>
            <Typography.Text type="tertiary">
              {copy.retrievalTestHint}
            </Typography.Text>
            <Space vertical align="start" style={{ width: "100%", marginTop: 12 }}>
              <Input
                value={retrievalQuery}
                placeholder={copy.retrievalQueryPlaceholder}
                onChange={setRetrievalQuery}
              />
              <Button loading={testing} type="primary" onClick={runRetrievalTest}>
                {copy.runTest}
              </Button>
              {testResults.length === 0 ? (
                <Empty description={copy.noTestResult} />
              ) : (
                <div className="atlas-test-result-list">
                  {(() => {
                    const tokens = tokenizeQuery(retrievalQuery);
                    const maxScore = testResults.reduce((max: number, item: KnowledgeRetrievalTestItem) => Math.max(max, item.score), 0) || 1;
                    return testResults.map((item: KnowledgeRetrievalTestItem) => {
                      const tags = parseTagsJson(item.tagsJson);
                      const scorePct = Math.max(0, Math.min(100, Math.round((item.score / maxScore) * 100)));
                      return (
                        <div key={item.chunkId} className="atlas-test-result semi-card semi-card-bordered">
                          <div className="semi-card-body">
                            <Space vertical align="start" style={{ width: "100%" }}>
                              <Space wrap spacing={6}>
                                <Tag color="light-blue">{item.documentName || `Doc #${item.documentId}`}</Tag>
                                {tags.length > 0 ? (
                                  <Fragment>
                                    <Typography.Text type="tertiary" size="small">{copy.hitTagsLabel}:</Typography.Text>
                                    {tags.map(tag => (
                                      <Tag key={tag} color="violet" size="small">{tag}</Tag>
                                    ))}
                                  </Fragment>
                                ) : null}
                                {(item.startOffset ?? 0) || (item.endOffset ?? 0) ? (
                                  <Typography.Text type="tertiary" size="small">
                                    {copy.hitOffsetLabel}: [{item.startOffset ?? 0}, {item.endOffset ?? 0}]
                                  </Typography.Text>
                                ) : null}
                              </Space>
                              <Typography.Text>
                                {highlightContent(item.content, tokens)}
                              </Typography.Text>
                              <div style={{ width: "100%" }}>
                                <Progress
                                  percent={scorePct}
                                  showInfo={false}
                                  size="small"
                                  stroke={item.score >= maxScore * 0.66 ? "#22c55e" : item.score >= maxScore * 0.33 ? "#f59e0b" : "#94a3b8"}
                                />
                                <Typography.Text type="tertiary" size="small">score: {item.score.toFixed(4)}</Typography.Text>
                              </div>
                            </Space>
                          </div>
                        </div>
                      );
                    });
                  })()}
                </div>
              )}
            </Space>
          </div>
        </div>
      </div>

      {resourceReferencesSlot ? (
        <div className="atlas-table-card semi-card semi-card-bordered semi-card-shadow" style={{ marginTop: 16 }}>
          <div className="semi-card-body">{resourceReferencesSlot}</div>
        </div>
      ) : null}

      <div className="atlas-knowledge-detail-grid">
        <div className="atlas-table-card semi-card semi-card-bordered semi-card-shadow">
          <div className="semi-card-body" style={{ padding: 0 }}>
            <Table
              rowKey="id"
              loading={loading}
              columns={documentColumns}
              dataSource={documents}
              empty={<Empty description={copy.listEmpty} />}
            onRow={(record?: KnowledgeDocumentDto) => ({
              onClick: () => {
                if (!record) {
                  return;
                }

                setSelectedDocumentId(record.id);
              }
            })}
            />
          </div>
        </div>

        <div className="atlas-table-card semi-card semi-card-bordered semi-card-shadow">
          <div className="semi-card-header">
            <div className="semi-card-header-wrapper">
              <Typography.Title heading={5} style={{ margin: 0 }}>
                {copy.chunkList}
              </Typography.Title>
              <Button
                icon={<IconPlus />}
                onClick={() => {
                  setEditingChunk(null);
                  setChunkForm({
                    ...DEFAULT_CHUNK_FORM,
                    documentId: selectedDocumentId ?? 0
                  });
                  setSheetVisible(true);
                }}
              >
                {copy.addChunk}
              </Button>
            </div>
          </div>
          <div className="semi-card-body" style={{ padding: 0 }}>
            {selectedDocumentId ? (
              <Table
                rowKey="id"
                loading={chunksLoading}
                columns={chunkColumns}
                dataSource={chunks}
                empty={<Empty description={copy.selectDocumentHint} />}
              />
            ) : (
              <Empty description={copy.selectDocumentHint} />
            )}
          </div>
        </div>
      </div>

      <SideSheet
        visible={sheetVisible}
        title={editingChunk ? copy.editChunk : copy.addChunk}
        onCancel={() => {
          setSheetVisible(false);
          setEditingChunk(null);
          setChunkForm(DEFAULT_CHUNK_FORM);
        }}
        footer={(
          <Space>
            <Button onClick={() => {
              setSheetVisible(false);
              setEditingChunk(null);
              setChunkForm(DEFAULT_CHUNK_FORM);
            }}>
              {copy.cancel}
            </Button>
            <Button type="primary" onClick={saveChunk}>
              {copy.save}
            </Button>
          </Space>
        )}
      >
        <Space vertical align="start" style={{ width: "100%" }}>
          <Input
            type="number"
            value={String(chunkForm.chunkIndex)}
            onChange={value => setChunkForm((current: ChunkCreateRequest) => ({ ...current, chunkIndex: Number(value) || 0 }))}
            placeholder="#"
          />
          <Input
            type="number"
            value={String(chunkForm.startOffset)}
            onChange={value => setChunkForm((current: ChunkCreateRequest) => ({ ...current, startOffset: Number(value) || 0 }))}
            placeholder={copy.chunkStart}
          />
          <Input
            type="number"
            value={String(chunkForm.endOffset)}
            onChange={value => setChunkForm((current: ChunkCreateRequest) => ({ ...current, endOffset: Number(value) || 0 }))}
            placeholder={copy.chunkEnd}
          />
          <TextArea
            autosize
            value={chunkForm.content}
            placeholder={copy.chunkContent}
            onChange={value => setChunkForm((current: ChunkCreateRequest) => ({ ...current, content: value }))}
          />
        </Space>
      </SideSheet>
    </div>
  );
}
