import { useEffect, useMemo, useState } from "react";
import {
  Banner,
  Button,
  Collapse,
  Empty,
  Input,
  Progress,
  Select,
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
  type RetrievalCallerPreset,
  type RetrievalCandidate,
  type RetrievalLog,
  type RetrievalProfile,
  type SupportedLocale
} from "../../types";
import { getLibraryCopy } from "../../copy";
import { RetrievalLogsPanel } from "./retrieval-logs-panel";

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

const PRESET_OPTIONS: Array<{ value: RetrievalCallerPreset; label: string }> = [
  { value: 0, label: "Assistant" },
  { value: 1, label: "WorkflowDebug" },
  { value: 2, label: "ExternalApi" },
  { value: 3, label: "System" }
];

interface FilterRow {
  key: string;
  value: string;
}

const filtersToRows = (filters?: Record<string, string>): FilterRow[] => {
  if (!filters) return [];
  return Object.entries(filters).map(([k, v]) => ({ key: k, value: v }));
};

const rowsToFilters = (rows: FilterRow[]): Record<string, string> | undefined => {
  const filtered = rows.filter(r => r.key.trim().length > 0);
  if (filtered.length === 0) return undefined;
  return filtered.reduce<Record<string, string>>((acc, r) => {
    acc[r.key.trim()] = r.value;
    return acc;
  }, {});
};

export function RetrievalTab({ api, locale, knowledge }: RetrievalTabProps) {
  const copy = getLibraryCopy(locale);
  const [query, setQuery] = useState("");
  const [callerType, setCallerType] = useState<RetrievalCallerContext["callerType"]>("studio");
  const [preset, setPreset] = useState<RetrievalCallerPreset>(0);
  const [debug, setDebug] = useState<boolean>(true);
  const [profileOverride, setProfileOverride] = useState<RetrievalProfile>(
    knowledge.retrievalProfile ?? DEFAULT_RETRIEVAL_PROFILE
  );
  const [filterRows, setFilterRows] = useState<FilterRow[]>([]);
  const [busy, setBusy] = useState(false);
  const [activeLog, setActiveLog] = useState<RetrievalLog | null>(null);
  const [refreshKey, setRefreshKey] = useState(0);

  useEffect(() => {
    setProfileOverride(knowledge.retrievalProfile ?? DEFAULT_RETRIEVAL_PROFILE);
  }, [knowledge.retrievalProfile]);

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
        userId: "admin",
        preset
      };
      const filters = rowsToFilters(filterRows);
      const response = await api.runRetrieval({
        query: query.trim(),
        knowledgeBaseIds: [knowledge.id],
        topK: profileOverride.topK,
        minScore: profileOverride.minScore,
        retrievalProfile: profileOverride,
        callerContext,
        debug,
        filters
      });
      setActiveLog(response.log);
      setRefreshKey(k => k + 1); // 通知 RetrievalLogsPanel 刷新
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

  function setFilterRow(idx: number, patch: Partial<FilterRow>): void {
    setFilterRows(prev => prev.map((row, i) => (i === idx ? { ...row, ...patch } : row)));
  }
  function addFilterRow(): void {
    setFilterRows(prev => [...prev, { key: "", value: "" }]);
  }
  function removeFilterRow(idx: number): void {
    setFilterRows(prev => prev.filter((_, i) => i !== idx));
  }

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
                  style={{ width: 140 }}
                  onChange={value => setCallerType(value as RetrievalCallerContext["callerType"])}
                  optionList={CALLER_OPTIONS.map(option => ({
                    label: callerLabel(option.value, copy),
                    value: option.value
                  }))}
                />
              </div>
              <div>
                <Typography.Text type="tertiary" size="small">callerContext.preset</Typography.Text>
                <Select
                  value={preset}
                  style={{ width: 160 }}
                  onChange={value => setPreset(value as RetrievalCallerPreset)}
                  optionList={PRESET_OPTIONS}
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

            {/* v5 §38 / 计划 G8：MetadataFilter key-value 编辑器 */}
            <Typography.Text strong>Filters (Metadata)</Typography.Text>
            <Space vertical align="start" style={{ width: "100%" }}>
              {filterRows.map((row, idx) => (
                <Space key={idx}>
                  <Input
                    placeholder="key (tag/namespace/...)"
                    value={row.key}
                    onChange={value => setFilterRow(idx, { key: value })}
                    style={{ width: 160 }}
                  />
                  <Input
                    placeholder="value"
                    value={row.value}
                    onChange={value => setFilterRow(idx, { value })}
                    style={{ width: 240 }}
                  />
                  <Button type="danger" theme="borderless" onClick={() => removeFilterRow(idx)}>移除</Button>
                </Space>
              ))}
              <Button onClick={addFilterRow}>+ 添加 filter</Button>
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

              {/* v5 §38 / 计划 G8：finalContext 用 Collapse 折叠 */}
              <Collapse style={{ marginTop: 12 }}>
                <Collapse.Panel header={copy.retrievalFinalContext} itemKey="final-context">
                  <Typography.Paragraph type="secondary" style={{ whiteSpace: "pre-wrap" }}>
                    {activeLog.finalContext || "-"}
                  </Typography.Paragraph>
                </Collapse.Panel>
              </Collapse>
            </div>
          ) : (
            <Empty description={copy.noTestResult} />
          )}
        </div>
      </div>

      <RetrievalLogsPanel
        api={api}
        locale={locale}
        knowledge={knowledge}
        refreshKey={refreshKey}
        onPick={setActiveLog}
      />
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
  const hasMetadata = item.metadata && Object.keys(item.metadata).length > 0;
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
          {/* v5 §38 / 计划 G8：metadata 展开 */}
          {hasMetadata ? (
            <Collapse>
              <Collapse.Panel header="metadata" itemKey={`meta-${item.chunkId}`}>
                <ul style={{ margin: 0, paddingLeft: 20, fontSize: 12 }}>
                  {Object.entries(item.metadata!).map(([k, v]) => (
                    <li key={k}>
                      <strong>{k}</strong>: {v}
                    </li>
                  ))}
                </ul>
              </Collapse.Panel>
            </Collapse>
          ) : null}
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
