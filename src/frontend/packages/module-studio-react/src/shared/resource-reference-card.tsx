import { useEffect, useMemo, useState } from "react";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import { Card, Empty, Spin, Table, Toast, Typography } from "@douyinfe/semi-ui";
import type { ResourceReference, StudioLocale, StudioModuleApi } from "../types";

export interface ResourceReferenceCardProps {
  api: Pick<StudioModuleApi, "getResourceReferences">;
  locale: StudioLocale;
  resourceType: string;
  resourceId: string;
  title?: string;
  testId?: string;
}

function referrerLabel(locale: StudioLocale, type: ResourceReference["referrerType"]): string {
  if (locale === "en-US") {
    const map: Record<ResourceReference["referrerType"], string> = {
      agent: "Agent",
      app: "App",
      workflow: "Workflow"
    };
    return map[type] ?? type;
  }
  const map: Record<ResourceReference["referrerType"], string> = {
    agent: "智能体",
    app: "应用",
    workflow: "工作流"
  };
  return map[type] ?? type;
}

export function ResourceReferenceCard({
  api,
  locale,
  resourceType,
  resourceId,
  title,
  testId = "studio-resource-reference-card"
}: ResourceReferenceCardProps) {
  const [loading, setLoading] = useState(true);
  const [rows, setRows] = useState<ResourceReference[]>([]);

  const heading =
    title ??
    (locale === "en-US" ? "Inbound references" : "引用本资源的实体");

  useEffect(() => {
    let disposed = false;
    setLoading(true);
    void api
      .getResourceReferences(resourceType, resourceId)
      .then((list) => {
        if (!disposed) {
          setRows(list);
        }
      })
      .catch((error: unknown) => {
        if (!disposed) {
          Toast.error(error instanceof Error ? error.message : "加载引用关系失败。");
          setRows([]);
        }
      })
      .finally(() => {
        if (!disposed) {
          setLoading(false);
        }
      });

    return () => {
      disposed = true;
    };
  }, [api, resourceType, resourceId]);

  const columns = useMemo<ColumnProps<ResourceReference>[]>(
    () => [
      {
        title: locale === "en-US" ? "Referrer type" : "引用方类型",
        dataIndex: "referrerType",
        width: 120,
        render: (value: unknown) => referrerLabel(locale, value as ResourceReference["referrerType"])
      },
      {
        title: locale === "en-US" ? "Name" : "名称",
        dataIndex: "referrerName",
        render: (value: unknown) => String(value ?? "-")
      },
      {
        title: "ID",
        dataIndex: "referrerId",
        width: 200,
        render: (value: unknown) => (
          <Typography.Text copyable={{ content: String(value ?? "") }} ellipsis={{ showTooltip: true }}>
            {String(value ?? "-")}
          </Typography.Text>
        )
      },
      {
        title: locale === "en-US" ? "Binding" : "绑定字段",
        dataIndex: "bindingField",
        render: (value: unknown) => String(value ?? "-")
      }
    ],
    [locale]
  );

  return (
    <Card data-testid={testId} title={heading} bordered>
      <Typography.Paragraph type="tertiary" style={{ marginTop: 0 }}>
        {locale === "en-US"
          ? "Entities that depend on this resource (agents, apps, or workflows)."
          : "依赖此资源的智能体、应用或工作流（用于影响分析与变更评估）。"}
      </Typography.Paragraph>
      {loading ? (
        <Spin />
      ) : rows.length === 0 ? (
        <Empty
          title={locale === "en-US" ? "No references" : "暂无引用"}
          description={
            locale === "en-US"
              ? "No other entities reference this resource yet."
              : "当前没有其他实体引用该资源。"
          }
        />
      ) : (
        <Table<ResourceReference>
          rowKey={(row) => (row ? `${row.referrerType}:${row.referrerId}:${row.bindingField}` : "row")}
          columns={columns}
          dataSource={rows}
          pagination={false}
          size="small"
        />
      )}
    </Card>
  );
}
