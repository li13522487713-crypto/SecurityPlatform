import { useEffect, useMemo, useState } from "react";
import {
  Banner,
  Button,
  Empty,
  Input,
  Modal,
  Select,
  Space,
  Table,
  Tag,
  Toast,
  Typography
} from "@douyinfe/semi-ui";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import { IconRefresh } from "@douyinfe/semi-icons";
import type {
  ChunkingProfile,
  DocumentChunkDto,
  KnowledgeBaseDto,
  KnowledgeDocumentDto,
  KnowledgeImageItem,
  KnowledgeTableColumn,
  KnowledgeTableRow,
  LibraryKnowledgeApi,
  RetrievalProfile,
  SupportedLocale
} from "../../types";
import { getLibraryCopy } from "../../copy";
import { ChunkingProfileEditor } from "./chunking-profile-editor";
import { RetrievalProfileEditor } from "./retrieval-profile-editor";

export interface SlicesTabProps {
  api: LibraryKnowledgeApi;
  locale: SupportedLocale;
  knowledge: KnowledgeBaseDto;
  selectedDocumentId: number | null;
  onSelectDocument: (documentId: number | null) => void;
}

export function SlicesTab({ api, locale, knowledge, selectedDocumentId, onSelectDocument }: SlicesTabProps) {
  const copy = getLibraryCopy(locale);
  const kind = knowledge.kind ?? "text";
  const [docs, setDocs] = useState<KnowledgeDocumentDto[]>([]);
  const [chunks, setChunks] = useState<DocumentChunkDto[]>([]);
  const [tableRows, setTableRows] = useState<KnowledgeTableRow[]>([]);
  const [tableColumns, setTableColumns] = useState<KnowledgeTableColumn[]>([]);
  const [imageItems, setImageItems] = useState<KnowledgeImageItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [chunkingDraft, setChunkingDraft] = useState<ChunkingProfile | null>(null);
  const [retrievalDraft, setRetrievalDraft] = useState<RetrievalProfile | null>(null);

  useEffect(() => {
    let disposed = false;
    void api.listDocuments(knowledge.id, { pageIndex: 1, pageSize: 50 }).then(response => {
      if (disposed) return;
      setDocs(response.items);
      if (!selectedDocumentId && response.items.length > 0) {
        onSelectDocument(response.items[0].id);
      }
    }).catch(error => {
      if (!disposed) Toast.error((error as Error).message);
    });
    return () => {
      disposed = true;
    };
  }, [api, knowledge.id]);

  useEffect(() => {
    if (!selectedDocumentId) {
      setChunks([]);
      setTableRows([]);
      setTableColumns([]);
      setImageItems([]);
      return;
    }

    let disposed = false;
    setLoading(true);
    Promise.all([
      api.listChunks(knowledge.id, selectedDocumentId, { pageIndex: 1, pageSize: 100 }),
      kind === "table" && api.listTableColumns
        ? api.listTableColumns(knowledge.id, selectedDocumentId)
        : Promise.resolve<KnowledgeTableColumn[]>([]),
      kind === "table" && api.listTableRows
        ? api.listTableRows(knowledge.id, selectedDocumentId, { pageIndex: 1, pageSize: 100 })
        : Promise.resolve({ items: [] as KnowledgeTableRow[], total: 0, pageIndex: 1, pageSize: 100 }),
      kind === "image" && api.listImageItems
        ? api.listImageItems(knowledge.id, selectedDocumentId, { pageIndex: 1, pageSize: 50 })
        : Promise.resolve({ items: [] as KnowledgeImageItem[], total: 0, pageIndex: 1, pageSize: 50 })
    ]).then(([chunkRes, columns, rowRes, imageRes]) => {
      if (disposed) return;
      setChunks(chunkRes.items);
      setTableColumns(columns);
      setTableRows(rowRes.items);
      setImageItems(imageRes.items);
    }).catch(error => {
      if (!disposed) Toast.error((error as Error).message);
    }).finally(() => {
      if (!disposed) setLoading(false);
    });
    return () => {
      disposed = true;
    };
  }, [api, kind, knowledge.id, selectedDocumentId]);

  const docOptions = useMemo(() => docs.map(doc => ({ label: doc.fileName, value: doc.id })), [docs]);

  return (
    <div className="atlas-knowledge-grid" style={{ alignItems: "stretch" }}>
      <div className="atlas-table-card semi-card semi-card-bordered semi-card-shadow">
        <div className="semi-card-header">
          <div className="semi-card-header-wrapper">
            <div>
              <Typography.Title heading={5} style={{ margin: 0 }}>
                {kind === "table"
                  ? copy.slicesTabTableHeader
                  : kind === "image"
                    ? copy.slicesTabImageHeader
                    : copy.slicesTabTextHeader}
              </Typography.Title>
              <Typography.Text type="tertiary">{copy.selectDocumentHint}</Typography.Text>
            </div>
            <Space spacing={8}>
              <Select
                value={selectedDocumentId ?? undefined}
                style={{ width: 240 }}
                placeholder={copy.selectDocumentHint}
                optionList={docOptions}
                onChange={value => onSelectDocument(Number(value) || null)}
              />
              <Button
                icon={<IconRefresh />}
                onClick={async () => {
                  if (!api.rebuildIndex) return;
                  const confirmed = await new Promise<boolean>(resolve => {
                    Modal.confirm({
                      title: copy.rebuildIndex,
                      content: copy.rebuildIndexConfirm,
                      onOk: () => resolve(true),
                      onCancel: () => resolve(false)
                    });
                  });
                  if (!confirmed) return;
                  try {
                    await api.rebuildIndex(knowledge.id, selectedDocumentId ?? undefined);
                    Toast.success(copy.rebuildIndex);
                  } catch (error) {
                    Toast.error((error as Error).message);
                  }
                }}
              >
                {copy.rebuildIndex}
              </Button>
            </Space>
          </div>
        </div>
        <div className="semi-card-body" style={{ padding: 0 }}>
          {!selectedDocumentId ? (
            <div style={{ padding: 32 }}><Empty description={copy.selectDocumentHint} /></div>
          ) : kind === "table" ? (
            <TableRowsView columns={tableColumns} rows={tableRows} loading={loading} emptyText={copy.slicesEmpty} />
          ) : kind === "image" ? (
            <ImageItemsView items={imageItems} loading={loading} copy={copy} />
          ) : (
            <TextSlicesView chunks={chunks} loading={loading} emptyText={copy.slicesEmpty} />
          )}
        </div>
      </div>

      <Space vertical align="start" style={{ width: "100%" }}>
        <ChunkingProfileEditor
          api={api}
          locale={locale}
          knowledge={knowledge}
          onUpdated={value => setChunkingDraft(value)}
        />
        <RetrievalProfileEditor
          api={api}
          locale={locale}
          knowledge={knowledge}
          onUpdated={value => setRetrievalDraft(value)}
        />
        {chunkingDraft || retrievalDraft ? (
          <Banner type="info" description={copy.rebuildIndexHint} />
        ) : null}
      </Space>
    </div>
  );
}

function TextSlicesView({ chunks, loading, emptyText }: { chunks: DocumentChunkDto[]; loading: boolean; emptyText: string }) {
  const columns: ColumnProps<DocumentChunkDto>[] = [
    { title: "#", dataIndex: "chunkIndex", width: 80 },
    {
      title: "Content",
      dataIndex: "content",
      render: (value: unknown) => (
        <Typography.Text ellipsis={{ showTooltip: true, rows: 2 }}>{String(value)}</Typography.Text>
      )
    },
    { title: "Start", dataIndex: "startOffset", width: 100 },
    { title: "End", dataIndex: "endOffset", width: 100 }
  ];
  if (chunks.length === 0) {
    return <div style={{ padding: 32 }}><Empty description={emptyText} /></div>;
  }
  return <Table rowKey="id" loading={loading} columns={columns} dataSource={chunks} pagination={false} />;
}

function TableRowsView({
  columns,
  rows,
  loading,
  emptyText
}: {
  columns: KnowledgeTableColumn[];
  rows: KnowledgeTableRow[];
  loading: boolean;
  emptyText: string;
}) {
  const sortedColumns = [...columns].sort((a, b) => a.ordinal - b.ordinal);
  // v5 §37 / 计划 G8：按列过滤 — 选定列名 + 子串关键词
  const [filterColumn, setFilterColumn] = useState<string | undefined>(undefined);
  const [filterKeyword, setFilterKeyword] = useState<string>("");

  const filteredRows = useMemo(() => {
    if (!filterColumn || filterKeyword.trim().length === 0) return rows;
    const needle = filterKeyword.trim().toLowerCase();
    return rows.filter(row => {
      try {
        const cells = JSON.parse(row.cellsJson) as Record<string, string>;
        const value = String(cells[filterColumn] ?? "").toLowerCase();
        return value.includes(needle);
      } catch {
        return false;
      }
    });
  }, [rows, filterColumn, filterKeyword]);

  const columnDefs: ColumnProps<KnowledgeTableRow>[] = [
    { title: "#", dataIndex: "rowIndex", width: 60 },
    ...sortedColumns.map(col => ({
      title: (
        <Space spacing={4}>
          <span>{col.name}</span>
          {col.isIndexColumn ? <Tag color="cyan" size="small">idx</Tag> : null}
        </Space>
      ),
      key: `col-${col.id}`,
      dataIndex: `cells.${col.name}`,
      render: (_value: unknown, record: KnowledgeTableRow) => {
        try {
          const cells = JSON.parse(record.cellsJson) as Record<string, string>;
          return cells[col.name] ?? "";
        } catch {
          return "";
        }
      }
    }))
  ];

  return (
    <div>
      <Space style={{ marginBottom: 8 }}>
        <Select
          placeholder="按列筛选"
          style={{ width: 160 }}
          value={filterColumn}
          showClear
          onChange={value => setFilterColumn(value as string | undefined)}
          optionList={sortedColumns.map(col => ({ value: col.name, label: col.name }))}
        />
        <Input
          placeholder="关键词"
          value={filterKeyword}
          onChange={value => setFilterKeyword(value)}
          style={{ width: 240 }}
          disabled={!filterColumn}
        />
        {(filterColumn || filterKeyword) && (
          <Button onClick={() => { setFilterColumn(undefined); setFilterKeyword(""); }}>清除</Button>
        )}
      </Space>
      {filteredRows.length === 0 ? (
        <div style={{ padding: 32 }}><Empty description={emptyText} /></div>
      ) : (
        <Table rowKey="id" loading={loading} columns={columnDefs} dataSource={filteredRows} pagination={false} />
      )}
    </div>
  );
}

function ImageItemsView({
  items,
  loading,
  copy
}: {
  items: KnowledgeImageItem[];
  loading: boolean;
  copy: ReturnType<typeof getLibraryCopy>;
}) {
  // v5 §37 / 计划 G8：按标注类别 + 文本关键词过滤
  const [annotationType, setAnnotationType] = useState<"all" | "caption" | "ocr" | "tag" | "vlm">("all");
  const [annotationKeyword, setAnnotationKeyword] = useState<string>("");

  const filteredItems = useMemo(() => {
    return items.filter(item => {
      if (annotationType === "all" && annotationKeyword.trim().length === 0) return true;
      const matchesType = annotationType === "all"
        ? item.annotations.length > 0
        : item.annotations.some(a => a.type === annotationType);
      if (!matchesType) return false;
      if (annotationKeyword.trim().length === 0) return true;
      const needle = annotationKeyword.trim().toLowerCase();
      return item.annotations.some(a => a.text.toLowerCase().includes(needle));
    });
  }, [items, annotationType, annotationKeyword]);

  return (
    <div>
      <Space style={{ marginBottom: 8 }}>
        <Select
          style={{ width: 160 }}
          value={annotationType}
          onChange={value => setAnnotationType(value as typeof annotationType)}
          optionList={[
            { value: "all", label: "全部标注" },
            { value: "caption", label: copy.imageItemAnnotationCaption },
            { value: "ocr", label: copy.imageItemAnnotationOcr },
            { value: "tag", label: copy.imageItemAnnotationTag },
            { value: "vlm", label: copy.imageItemAnnotationVlm }
          ]}
        />
        <Input
          placeholder="标注关键词"
          value={annotationKeyword}
          onChange={value => setAnnotationKeyword(value)}
          style={{ width: 240 }}
        />
        {(annotationType !== "all" || annotationKeyword) && (
          <Button onClick={() => { setAnnotationType("all"); setAnnotationKeyword(""); }}>清除</Button>
        )}
      </Space>
      {filteredItems.length === 0 ? (
        <div style={{ padding: 32 }}><Empty description={copy.slicesEmpty} /></div>
      ) : (
        <div className="atlas-image-grid">
          {filteredItems.map(item => (
            <div key={item.id} className="atlas-image-card semi-card semi-card-bordered">
              <div className="semi-card-body">
                {item.thumbnailUrl ? (
                  <img
                    src={item.thumbnailUrl}
                    alt={item.fileName}
                    style={{ width: "100%", height: 120, objectFit: "cover", borderRadius: 8, background: "#f1f5f9" }}
                    loading="lazy"
                  />
                ) : (
                  <div style={{ width: "100%", height: 120, borderRadius: 8, background: "#f1f5f9" }} />
                )}
                <Typography.Text strong style={{ display: "block", marginTop: 8 }}>{item.fileName}</Typography.Text>
                <Typography.Text type="tertiary" size="small">
                  {item.width}×{item.height}
                </Typography.Text>
                <Space wrap spacing={4} style={{ marginTop: 8 }}>
                  {item.annotations.map(annotation => {
                    const labelMap = {
                      caption: copy.imageItemAnnotationCaption,
                      ocr: copy.imageItemAnnotationOcr,
                      tag: copy.imageItemAnnotationTag,
                      vlm: copy.imageItemAnnotationVlm
                    } as const;
                    return (
                      <Tag key={annotation.id} color={annotation.type === "tag" ? "blue" : "violet"} size="small">
                        {labelMap[annotation.type]}: {annotation.text}
                      </Tag>
                    );
                  })}
                </Space>
                {loading ? <Typography.Text type="tertiary" size="small">loading…</Typography.Text> : null}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
