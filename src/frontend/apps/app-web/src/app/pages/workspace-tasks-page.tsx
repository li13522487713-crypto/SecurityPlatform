import { useEffect, useState } from "react";
import { Empty, Spin, Table, Tag, Typography } from "@douyinfe/semi-ui";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import { useAppI18n } from "../i18n";
import { useWorkspaceContext } from "../workspace-context";
import { listTasks, type TaskItem, type TaskStatus } from "../../services/mock";

const STATUS_COLOR: Record<TaskStatus, "blue" | "green" | "red" | "amber"> = {
  pending: "amber",
  running: "blue",
  succeeded: "green",
  failed: "red"
};

export function WorkspaceTasksPage() {
  const { t } = useAppI18n();
  const workspace = useWorkspaceContext();
  const [items, setItems] = useState<TaskItem[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!workspace.id) {
      return;
    }
    let cancelled = false;
    setLoading(true);
    listTasks(workspace.id, { pageIndex: 1, pageSize: 20 })
      .then(result => {
        if (!cancelled) {
          setItems(result.items);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setItems([]);
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

  const columns: ColumnProps<TaskItem>[] = [
    { title: t("cozeTasksColumnName"), dataIndex: "name" },
    { title: t("cozeTasksColumnType"), dataIndex: "type" },
    {
      title: t("cozeTasksColumnStatus"),
      dataIndex: "status",
      render: (value: TaskStatus) => <Tag color={STATUS_COLOR[value]}>{t(`cozeTasksStatus${capitalize(value)}` as never)}</Tag>
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
