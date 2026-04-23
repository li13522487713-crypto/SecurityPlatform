import { useEffect, useState } from "react";
import { Banner, Empty, Spin, Table, Tag, Typography } from "@douyinfe/semi-ui";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import { useAppI18n } from "../i18n";
import { useWorkspaceContext } from "../workspace-context";
import { listWorkspaceTasks, type WorkspaceTaskItemDto, type WorkspaceTaskStatus } from "../../services/api-workspace-runtime";

const STATUS_COLOR: Record<WorkspaceTaskStatus, "blue" | "green" | "red" | "amber"> = {
  pending: "amber",
  running: "blue",
  succeeded: "green",
  failed: "red"
};

export function WorkspaceTasksPage() {
  const { t } = useAppI18n();
  const workspace = useWorkspaceContext();
  const [items, setItems] = useState<WorkspaceTaskItemDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadFailed, setLoadFailed] = useState(false);

  useEffect(() => {
    if (!workspace.id) {
      return;
    }
    let cancelled = false;
    setLoading(true);
    setLoadFailed(false);
    listWorkspaceTasks(workspace.id, { pageIndex: 1, pageSize: 20 })
      .then(result => {
        if (!cancelled) {
          setItems(result.items);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setItems([]);
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

  const columns: ColumnProps<WorkspaceTaskItemDto>[] = [
    { title: t("cozeTasksColumnName"), dataIndex: "name" },
    { title: t("cozeTasksColumnType"), dataIndex: "type" },
    {
      title: t("cozeTasksColumnStatus"),
      dataIndex: "status",
      render: (value: WorkspaceTaskStatus) => <Tag color={STATUS_COLOR[value]}>{t(`cozeTasksStatus${capitalize(value)}` as never)}</Tag>
    },
    { title: t("cozeTasksColumnStartedAt"), dataIndex: "startedAt" },
    {
      title: t("cozeTasksColumnDuration"),
      dataIndex: "durationMs",
      render: (value: number) => `${(value / 1000).toFixed(1)}s`
    }
  ];

  return (
    <div className="coze-page" data-testid="coze-tasks-page">
      <header className="coze-page__header">
        <Typography.Title heading={3} style={{ margin: 0 }}>{t("cozeTasksTitle")}</Typography.Title>
        <Typography.Text type="tertiary">{t("cozeTasksSubtitle")}</Typography.Text>
      </header>
      <section className="coze-page__body">
        {loadFailed ? <Banner type="danger" fullMode={false} bordered description={t("cozeTasksLoadFailed")} /> : null}
        {loading ? (
          <div className="coze-page__loading"><Spin /></div>
        ) : items.length === 0 ? (
          <Empty description={t("cozeTasksEmpty")} />
        ) : (
          <Table columns={columns} dataSource={items} rowKey="id" pagination={false} />
        )}
      </section>
    </div>
  );
}

function capitalize(value: string): string {
  return value.charAt(0).toUpperCase() + value.slice(1);
}
