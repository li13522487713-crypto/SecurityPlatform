import { useEffect, useState } from "react";
import { Banner, Empty, SideSheet, Space, Tag, Toast, Typography } from "@douyinfe/semi-ui";
import type {
  KnowledgeBaseDto,
  LibraryKnowledgeApi,
  RetrievalLog,
  SupportedLocale
} from "../../types";
import { getLibraryCopy } from "../../copy";
import { formatDateTime } from "../../utils";

export interface RetrievalLogsPanelProps {
  api: LibraryKnowledgeApi;
  locale: SupportedLocale;
  knowledge: KnowledgeBaseDto;
  /** 可选：当外部检索完成后通知刷新（refreshKey 变化时重新拉取） */
  refreshKey?: number;
  /** 用户点击日志条目时回调；retrieval-tab 使用此回调把 activeLog 同步到调试面板 */
  onPick?: (log: RetrievalLog) => void;
}

/**
 * 检索日志列表面板（v5 §38 / 计划 G8 抽出）。
 * 从 RetrievalTab 中分离，便于在任务中心 / 全局检索调试入口等多处复用。
 */
export function RetrievalLogsPanel({ api, locale, knowledge, refreshKey, onPick }: RetrievalLogsPanelProps) {
  const copy = getLibraryCopy(locale);
  const [logs, setLogs] = useState<RetrievalLog[]>([]);
  const [activeLog, setActiveLog] = useState<RetrievalLog | null>(null);
  const [sheetVisible, setSheetVisible] = useState(false);

  useEffect(() => {
    let cancelled = false;
    async function refresh() {
      if (!api.listRetrievalLogs) return;
      try {
        const response = await api.listRetrievalLogs(knowledge.id, { pageIndex: 1, pageSize: 20 });
        if (!cancelled) setLogs(response.items);
      } catch (error) {
        Toast.error((error as Error).message);
      }
    }
    void refresh();
    return () => {
      cancelled = true;
    };
  }, [api, knowledge.id, refreshKey]);

  return (
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
                  setSheetVisible(true);
                  onPick?.(log);
                }}
                style={{ cursor: "pointer", width: "100%" }}
              >
                <div className="semi-card-body">
                  <Space vertical align="start" style={{ width: "100%" }}>
                    <Space spacing={6}>
                      <Tag color="cyan">{log.callerContext.callerType}</Tag>
                      {log.callerContext.preset !== undefined ? (
                        <Tag color="violet">preset:{log.callerContext.preset}</Tag>
                      ) : null}
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

      <SideSheet
        title={`Trace ${activeLog?.traceId ?? ""}`}
        visible={sheetVisible && !!activeLog}
        width={520}
        onCancel={() => setSheetVisible(false)}
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
