import { useEffect, useMemo, useState } from "react";
import {
  Banner,
  Button,
  Empty,
  Input,
  Progress,
  Select,
  SideSheet,
  Space,
  Switch,
  Tag,
  Toast,
  Typography
} from "@douyinfe/semi-ui";
import {
  DEFAULT_RETRIEVAL_PROFILE,
  type KnowledgeBaseDto,
  type LibraryKnowledgeApi,
  type RetrievalCallerContext,
  type RetrievalCandidate,
  type RetrievalLog,
  type RetrievalProfile,
  type SupportedLocale
} from "../../types";
import { getLibraryCopy } from "../../copy";
import { formatDateTime } from "../../utils";

export interface RetrievalTabProps {
  api: LibraryKnowledgeApi;
  locale: SupportedLocale;
  knowledge: KnowledgeBaseDto;
}

const CALLER_OPTIONS: Array<{ value: RetrievalCallerContext["callerType"] }> = [
  { value: "studio" },
  { value: "agent" },
  { value: "workflow" },
  { value: "app" },
  { value: "chatflow" }
];

export function RetrievalTab({ api, locale, knowledge }: RetrievalTabProps) {
  const copy = getLibraryCopy(locale);
  const [query, setQuery] = useState("");
  const [callerType, setCallerType] = useState<RetrievalCallerContext["callerType"]>("studio");
  const [debug, setDebug] = useState<boolean>(true);
  const [profileOverride, setProfileOverride] = useState<RetrievalProfile>(
    knowledge.retrievalProfile ?? DEFAULT_RETRIEVAL_PROFILE
  );
  const [busy, setBusy] = useState(false);
  const [activeLog, setActiveLog] = useState<RetrievalLog | null>(null);
  const [logs, setLogs] = useState<RetrievalLog[]>([]);
  const [logSheetVisible, setLogSheetVisible] = useState(false);

  useEffect(() => {
    setProfileOverride(knowledge.retrievalProfile ?? DEFAULT_RETRIEVAL_PROFILE);
  }, [knowledge.retrievalProfile]);

  async function refreshLogs() {
    if (!api.listRetrievalLogs) return;
    try {
      const response = await api.listRetrievalLogs(knowledge.id, { pageIndex: 1, pageSize: 20 });
      setLogs(response.items);
    } catch (error) {
      Toast.error((error as Error).message);
    }
  }

  useEffect(() => {
    void refreshLogs();
    return undefined;
  }, [knowledge.id]);

  async function handleRun() {
    if (!query.trim()) {
      Toast.warning(copy.retrievalQueryPlaceholder);
      return;
    }
    if (!api.runRetrieval) {
      Toast.warning(copy.noTestResult);
      return;
    }
    setBusy(true);
    try {
      const callerContext: RetrievalCallerContext = {
        callerType,
        callerId: callerType === "studio" ? "studio-debug" : `caller_${callerType}_${Date.now()}`,
        callerName: copy.retrievalCallerStudio,
        tenantId: "00000000-0000-0000-0000-000000000001",
        userId: "admin"
      };
      const response = await api.runRetrieval({
        query: query.trim(),
        knowledgeBaseIds: [knowledge.id],
        topK: profileOverride.topK,
        minScore: profileOverride.minScore,
        retrievalProfile: profileOverride,
        callerContext,
        debug
      });
      setActiveLog(response.log);
      await refreshLogs();
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setBusy(false);
    }
  }

  const candidatesContent = useMemo(() => {
    if (!activeLog) return null;
    const max = Math.max(0.0001, ...activeLog.reranked.map(c => c.rerankScore ?? c.score));
    return activeLog.reranked.map(item => (
      <CandidateCard key={item.chunkId} item={item} maxScore={max} copy={copy} />
    ));
  }, [activeLog, copy]);

  return (
    <div className="atlas-knowledge-grid">
      <div className="atlas-retrieval-card semi-card semi-card-bordered semi-card-shadow">
        <div className="semi-card-body">
          <Typography.Title heading={5}>{copy.retrievalDebugTitle}</Typography.Title>
          <Typography.Text type="tertiary">{copy.retrievalDebugHint}</Typography.Text>
          <Space vertical align="start" style={{ width: "100%", marginTop: 12 }}>
            <Input value={query} onChange={value => setQuery(value)} placeholder={copy.retrievalQueryPlaceholder} />
            <Space spacing={8} wrap>
              <div>
                <Typography.Text type="tertiary" size="small">{copy.retrievalCallerType}</Typography.Text>
                <Select
                  value={callerType}
                  style={{ width: 160 }}
                  onChange={value => setCallerType(value as RetrievalCallerContext["callerType"])}
                  optionList={CALLER_OPTIONS.map(option => ({
                    label: callerLabel(option.value, copy),
                    value: option.value
                  }))}
                />
              </div>
              <div>
                <Typography.Text type="tertiary" size="small">{copy.retrievalProfileTopK}</Typography.Text>
                <Input
                  type="number"
                  value={String(profileOverride.topK)}
                  style={{ width: 100 }}
                  onChange={value => setProfileOverride(prev => ({ ...prev, topK: Math.max(1, Number(value) || 1) }))}
                />
              </div>
              <div>
                <Typography.Text type="tertiary" size="small">{copy.wizardEnableRerank}</Typography.Text>
                <Switch
                  checked={profileOverride.enableRerank}
                  onChange={value => setProfileOverride(prev => ({ ...prev, enableRerank: value }))}
                />
              </div>
              <div>
                <Typography.Text type="tertiary" size="small">{copy.wizardEnableQueryRewrite}</Typography.Text>
                <Switch
                  checked={profileOverride.enableQueryRewrite}
                  onChange={value => setProfileOverride(prev => ({ ...prev, enableQueryRewrite: value }))}
                />
              </div>
              <div>
                <Typography.Text type="tertiary" size="small">{copy.retrievalEnableDebug}</Typography.Text>
                <Switch checked={debug} onChange={setDebug} />
              </div>
            </Space>
            <Button type="primary" loading={busy} onClick={handleRun}>{copy.runTest}</Button>
          </Space>

          {activeLog ? (
            <div style={{ marginTop: 16 }}>
              <Banner
                type="info"
                description={`traceId=${activeLog.traceId} · ${copy.retrievalLatency}=${activeLog.latencyMs}ms`}
              />
              <Typography.Text strong style={{ display: "block", marginTop: 12 }}>{copy.retrievalRawQuery}</Typography.Text>
              <Typography.Paragraph>{activeLog.rawQuery}</Typography.Paragraph>
              {activeLog.rewrittenQuery ? (
                <>
                  <Typography.Text strong>{copy.retrievalRewrittenQuery}</Typography.Text>
                  <Typography.Paragraph>{activeLog.rewrittenQuery}</Typography.Paragraph>
                </>
              ) : null}
              <Typography.Text strong>{copy.retrievalReranked}</Typography.Text>
              <div className="atlas-test-result-list">
                {candidatesContent}
              </div>
              <Typography.Text strong style={{ display: "block", marginTop: 12 }}>{copy.retrievalFinalContext}</Typography.Text>
              <Typography.Paragraph type="secondary" style={{ whiteSpace: "pre-wrap" }}>
                {activeLog.finalContext || "-"}
              </Typography.Paragraph>
            </div>
          ) : (
            <Empty description={copy.noTestResult} />
          )}
        </div>
      </div>

      <div className="atlas-table-card semi-card semi-card-bordered semi-card-shadow">
        <div className="semi-card-body">
          <Typography.Title heading={5}>{copy.retrievalLogsTitle}</Typography.Title>
          {logs.length === 0 ? (
            <Empty description={copy.retrievalLogsEmpty} />
          ) : (
            <Space vertical align="start" style={{ width: "100%" }}>
              {logs.map(log => (
                <div
                  key={log.traceId}
                  className="atlas-test-result semi-card semi-card-bordered"
                  onClick={() => {
                    setActiveLog(log);
                    setLogSheetVisible(true);
                  }}
                  style={{ cursor: "pointer", width: "100%" }}
                >
                  <div className="semi-card-body">
                    <Space vertical align="start" style={{ width: "100%" }}>
                      <Space spacing={6}>
                        <Tag color="cyan">{log.callerContext.callerType}</Tag>
                        <Typography.Text strong>{log.rawQuery}</Typography.Text>
                      </Space>
                      <Typography.Text type="tertiary" size="small">
                        traceId={log.traceId} · hits={log.reranked.length} · {formatDateTime(log.createdAt)}
                      </Typography.Text>
                    </Space>
                  </div>
                </div>
              ))}
            </Space>
          )}
        </div>
      </div>

      <SideSheet
        title={`Trace ${activeLog?.traceId ?? ""}`}
        visible={logSheetVisible && !!activeLog}
        width={520}
        onCancel={() => setLogSheetVisible(false)}
      >
        {activeLog ? (
          <Space vertical align="start" style={{ width: "100%" }}>
            <Banner type="info" description={`${formatDateTime(activeLog.createdAt)} · ${activeLog.embeddingModel} · ${activeLog.vectorStore}`} />
            <Typography.Text strong>{copy.retrievalRawQuery}</Typography.Text>
            <Typography.Paragraph>{activeLog.rawQuery}</Typography.Paragraph>
            {activeLog.rewrittenQuery ? (
              <>
                <Typography.Text strong>{copy.retrievalRewrittenQuery}</Typography.Text>
                <Typography.Paragraph>{activeLog.rewrittenQuery}</Typography.Paragraph>
              </>
            ) : null}
            <Typography.Text strong>{copy.retrievalCandidates}</Typography.Text>
            {activeLog.candidates.map(c => (
              <Typography.Paragraph key={`c-${c.chunkId}`}>
                [{c.source}] doc#{c.documentId} chunk#{c.chunkId} score={c.score.toFixed(3)} — {c.content.slice(0, 80)}
              </Typography.Paragraph>
            ))}
            <Typography.Text strong>{copy.retrievalFinalContext}</Typography.Text>
            <Typography.Paragraph type="secondary" style={{ whiteSpace: "pre-wrap" }}>
              {activeLog.finalContext}
            </Typography.Paragraph>
          </Space>
        ) : null}
      </SideSheet>
    </div>
  );
}

function callerLabel(callerType: RetrievalCallerContext["callerType"], copy: ReturnType<typeof getLibraryCopy>): string {
  switch (callerType) {
    case "agent":
      return copy.retrievalCallerAgent;
    case "workflow":
      return copy.retrievalCallerWorkflow;
    case "app":
      return copy.retrievalCallerApp;
    case "chatflow":
      return copy.retrievalCallerChatflow;
    default:
      return copy.retrievalCallerStudio;
  }
}

function CandidateCard({
  item,
  maxScore,
  copy
}: {
  item: RetrievalCandidate;
  maxScore: number;
  copy: ReturnType<typeof getLibraryCopy>;
}) {
  const finalScore = item.rerankScore ?? item.score;
  const pct = Math.max(0, Math.min(100, Math.round((finalScore / maxScore) * 100)));
  const sourceLabel = sourceLabelFrom(item.source, copy);
  return (
    <div className="atlas-test-result semi-card semi-card-bordered">
      <div className="semi-card-body">
        <Space vertical align="start" style={{ width: "100%" }}>
          <Space spacing={6} wrap>
            <Tag color="light-blue">{item.documentName ?? `Doc #${item.documentId}`}</Tag>
            <Tag color="cyan" size="small">{copy.retrievalSourceLabel}: {sourceLabel}</Tag>
            <Typography.Text type="tertiary" size="small">
              chunk #{item.chunkId} · score={item.score.toFixed(3)}
              {item.rerankScore ? ` · rerank=${item.rerankScore.toFixed(3)}` : ""}
            </Typography.Text>
          </Space>
          <Typography.Text>{item.content}</Typography.Text>
          <Progress
            percent={pct}
            size="small"
            showInfo={false}
            stroke={pct >= 66 ? "#22c55e" : pct >= 33 ? "#f59e0b" : "#94a3b8"}
          />
        </Space>
      </div>
    </div>
  );
}

function sourceLabelFrom(source: RetrievalCandidate["source"], copy: ReturnType<typeof getLibraryCopy>): string {
  switch (source) {
    case "bm25":
      return copy.retrievalSourceBm25;
    case "table":
      return copy.retrievalSourceTable;
    case "image":
      return copy.retrievalSourceImage;
    default:
      return copy.retrievalSourceVector;
  }
}
