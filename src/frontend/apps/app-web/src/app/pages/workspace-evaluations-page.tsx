import { useEffect, useState } from "react";
import { Banner, Empty, Spin, TabPane, Tabs, Typography } from "@douyinfe/semi-ui";
import { useAppI18n } from "../i18n";
import { useWorkspaceContext } from "../workspace-context";
import {
  listWorkspaceEvaluations,
  listWorkspaceTestsets,
  type WorkspaceEvaluationItemDto,
  type WorkspaceTestsetItemDto
} from "../../services/api-workspace-runtime";

export function WorkspaceEvaluationsPage() {
  const { t } = useAppI18n();
  const workspace = useWorkspaceContext();
  const [tab, setTab] = useState<"runs" | "testsets">("runs");
  const [evaluations, setEvaluations] = useState<WorkspaceEvaluationItemDto[]>([]);
  const [testsets, setTestsets] = useState<WorkspaceTestsetItemDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadFailed, setLoadFailed] = useState(false);

  useEffect(() => {
    if (!workspace.id) {
      return;
    }
    let cancelled = false;
    setLoading(true);
    setLoadFailed(false);
    Promise.all([
      listWorkspaceEvaluations(workspace.id, { pageIndex: 1, pageSize: 20 }),
      listWorkspaceTestsets(workspace.id, { pageIndex: 1, pageSize: 20 })
    ])
      .then(([evals, testsetsResult]) => {
        if (!cancelled) {
          setEvaluations(evals.items);
          setTestsets(testsetsResult.items);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setEvaluations([]);
          setTestsets([]);
          setLoadFailed(true);
        }
      })
      .finally(() => {
        if (!cancelled) {
          setLoading(false);
        }
      });
    return () => {
      cancelled = true;
    };
  }, [workspace.id]);

  return (
    <div className="coze-page" data-testid="coze-evaluations-page">
      <header className="coze-page__header">
        <Typography.Title heading={3} style={{ margin: 0 }}>{t("cozeEvaluationsTitle")}</Typography.Title>
        <Typography.Text type="tertiary">{t("cozeEvaluationsSubtitle")}</Typography.Text>
      </header>
      {loadFailed ? <Banner type="danger" fullMode={false} bordered description={t("cozeEvaluationsLoadFailed")} /> : null}
      <Tabs activeKey={tab} onChange={key => setTab((key as "runs" | "testsets") ?? "runs")}>
        <TabPane tab={t("cozeEvaluationsTabRuns")} itemKey="runs">
          {loading ? (
            <div className="coze-page__loading"><Spin /></div>
          ) : evaluations.length === 0 ? (
            <Empty description={t("cozeEvaluationsEmpty")} />
          ) : (
            <ul className="coze-list">
              {evaluations.map(item => (
                <li key={item.id} className="coze-list__item">
                  <strong>{item.name}</strong>
                  <span>{item.metricSummary}</span>
                </li>
              ))}
            </ul>
          )}
        </TabPane>
        <TabPane tab={t("cozeEvaluationsTabTestsets")} itemKey="testsets">
          {loading ? (
            <div className="coze-page__loading"><Spin /></div>
          ) : testsets.length === 0 ? (
            <Empty description={t("cozeEvaluationsEmpty")} />
          ) : (
            <ul className="coze-list">
              {testsets.map(item => (
                <li key={item.id} className="coze-list__item">
                  <strong>{item.name}</strong>
                  <span>{item.description}</span>
                </li>
              ))}
            </ul>
          )}
        </TabPane>
      </Tabs>
    </div>
  );
}
