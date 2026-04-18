import { useEffect, useMemo, useState } from "react";
import {
  Banner,
  Button,
  Empty,
  Select,
  Space,
  Spin,
  Toast,
  Typography
} from "@douyinfe/semi-ui";
import {
  DEFAULT_PARSING_STRATEGY,
  type DocumentChunkDto,
  type KnowledgeBaseDto,
  type KnowledgeDocumentDto,
  type LibraryKnowledgeApi,
  type ParsingStrategy,
  type SupportedLocale
} from "../types";
import { getLibraryCopy } from "../copy";
import { ParsingStrategyForm } from "./parsing-strategy-form";

export interface ParsingStrategyComparePanelProps {
  api: LibraryKnowledgeApi;
  locale: SupportedLocale;
  knowledge: KnowledgeBaseDto;
  /** 默认对比的文档 id；若不传则自动取首个 */
  documentId?: number;
}

interface SnapshotResult {
  strategy: ParsingStrategy;
  chunks: DocumentChunkDto[];
}

export function ParsingStrategyComparePanel({
  api,
  locale,
  knowledge,
  documentId
}: ParsingStrategyComparePanelProps) {
  const copy = getLibraryCopy(locale);
  const kind = knowledge.kind ?? "text";
  const [docs, setDocs] = useState<KnowledgeDocumentDto[]>([]);
  const [activeDocId, setActiveDocId] = useState<number | null>(documentId ?? null);
  const [strategyA, setStrategyA] = useState<ParsingStrategy>({ ...DEFAULT_PARSING_STRATEGY });
  const [strategyB, setStrategyB] = useState<ParsingStrategy>({ ...DEFAULT_PARSING_STRATEGY, parsingType: "precise", extractTable: true, extractImage: kind !== "text" });
  const [snapshotA, setSnapshotA] = useState<SnapshotResult | null>(null);
  const [snapshotB, setSnapshotB] = useState<SnapshotResult | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    let disposed = false;
    void api.listDocuments(knowledge.id, { pageIndex: 1, pageSize: 50 }).then(response => {
      if (disposed) return;
      setDocs(response.items);
      if (!activeDocId && response.items.length > 0) {
        setActiveDocId(response.items[0].id);
      }
    }).catch(error => {
      if (!disposed) Toast.error((error as Error).message);
    });
    return () => {
      disposed = true;
    };
  }, [api, knowledge.id]);

  async function captureSnapshot(strategy: ParsingStrategy): Promise<SnapshotResult> {
    if (!activeDocId) {
      throw new Error("Pick a document first");
    }
    if (!api.rerunParseJob) {
      throw new Error("rerunParseJob unsupported by current adapter");
    }
    await api.rerunParseJob(knowledge.id, activeDocId, strategy);
    // mock 同步推进
    if (api.subscribeJobs && (api as { __scheduler?: { advanceUntilStable?: () => void } }).__scheduler) {
      (api as { __scheduler: { advanceUntilStable: () => void } }).__scheduler.advanceUntilStable();
    }
    const response = await api.listChunks(knowledge.id, activeDocId, { pageIndex: 1, pageSize: 50 });
    return { strategy, chunks: response.items };
  }

  async function runCompare() {
    if (!activeDocId) {
      Toast.warning(copy.selectDocumentHint);
      return;
    }
    setBusy(true);
    try {
      const a = await captureSnapshot(strategyA);
      setSnapshotA(a);
      const b = await captureSnapshot(strategyB);
      setSnapshotB(b);
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setBusy(false);
    }
  }

  const docOptions = useMemo(() => docs.map(doc => ({ label: doc.fileName, value: doc.id })), [docs]);

  return (
    <div className="atlas-table-card semi-card semi-card-bordered semi-card-shadow">
      <div className="semi-card-body">
        <Typography.Title heading={5}>{copy.parsingCompareTitle}</Typography.Title>
        <Banner type="info" description={copy.parsingCompareTitle} />
        <Space vertical align="start" style={{ width: "100%", marginTop: 12 }}>
          <Select
            value={activeDocId ?? undefined}
            style={{ width: "100%" }}
            onChange={value => {
              setActiveDocId(Number(value) || null);
              setSnapshotA(null);
              setSnapshotB(null);
            }}
            optionList={docOptions}
            placeholder={copy.selectDocumentHint}
          />
        </Space>
        <div className="atlas-knowledge-grid" style={{ marginTop: 16 }}>
          <div className="atlas-summary-card semi-card semi-card-bordered">
            <div className="semi-card-body">
              <Typography.Title heading={6}>{copy.parsingCompareLeft}</Typography.Title>
              <ParsingStrategyForm locale={locale} kind={kind} value={strategyA} onChange={setStrategyA} />
            </div>
          </div>
          <div className="atlas-summary-card semi-card semi-card-bordered">
            <div className="semi-card-body">
              <Typography.Title heading={6}>{copy.parsingCompareRight}</Typography.Title>
              <ParsingStrategyForm locale={locale} kind={kind} value={strategyB} onChange={setStrategyB} />
            </div>
          </div>
        </div>
        <Button type="primary" style={{ marginTop: 12 }} loading={busy} onClick={runCompare}>
          {copy.parsingCompareRun}
        </Button>

        {busy ? (
          <div style={{ padding: 24, textAlign: "center" }}>
            <Spin size="large" />
          </div>
        ) : null}

        {!busy && (snapshotA || snapshotB) ? (
          <div className="atlas-knowledge-grid" style={{ marginTop: 16 }}>
            <div className="atlas-table-card semi-card semi-card-bordered">
              <div className="semi-card-body">
                <Typography.Title heading={6}>{copy.parsingCompareLeft}</Typography.Title>
                {snapshotA && snapshotA.chunks.length > 0 ? (
                  snapshotA.chunks.slice(0, 6).map(chunk => (
                    <Typography.Paragraph key={`a-${chunk.id}`}>
                      [#{chunk.chunkIndex}] {chunk.content}
                    </Typography.Paragraph>
                  ))
                ) : (
                  <Empty description={copy.slicesEmpty} />
                )}
              </div>
            </div>
            <div className="atlas-table-card semi-card semi-card-bordered">
              <div className="semi-card-body">
                <Typography.Title heading={6}>{copy.parsingCompareRight}</Typography.Title>
                {snapshotB && snapshotB.chunks.length > 0 ? (
                  snapshotB.chunks.slice(0, 6).map(chunk => (
                    <Typography.Paragraph key={`b-${chunk.id}`}>
                      [#{chunk.chunkIndex}] {chunk.content}
                    </Typography.Paragraph>
                  ))
                ) : (
                  <Empty description={copy.slicesEmpty} />
                )}
              </div>
            </div>
          </div>
        ) : null}
      </div>
    </div>
  );
}
