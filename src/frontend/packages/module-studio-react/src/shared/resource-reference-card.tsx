import { useEffect, useMemo, useState } from "react";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import { Card, Empty, Spin, Table, Toast, Typography } from "@douyinfe/semi-ui";
import type { ResourceReference, StudioLocale, StudioModuleApi } from "../types";
import { getStudioCopy } from "../copy";

export interface ResourceReferenceCardProps {
  api: Pick<StudioModuleApi, "getResourceReferences">;
  locale: StudioLocale;
  resourceType: string;
  resourceId: string;
  title?: string;
  testId?: string;
}

function referrerLabel(locale: StudioLocale, type: ResourceReference["referrerType"]): string {
  const copy = getStudioCopy(locale);
  switch (type) {
    case "agent":
      return copy.resourceReference.referrerAgent;
    case "app":
      return copy.resourceReference.referrerApp;
    case "workflow":
      return copy.resourceReference.referrerWorkflow;
    default:
      return type;
  }
}

export function ResourceReferenceCard({
  api,
  locale,
  resourceType,
  resourceId,
  title,
  testId = "studio-resource-reference-card"
}: ResourceReferenceCardProps) {
  const copy = getStudioCopy(locale);
  const [loading, setLoading] = useState(true);
  const [rows, setRows] = useState<ResourceReference[]>([]);

  const heading = title ?? copy.resourceReference.defaultHeading;

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
          Toast.error(error instanceof Error ? error.message : copy.resourceReference.loadFailed);
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
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [api, resourceType, resourceId]);

  const columns = useMemo<ColumnProps<ResourceReference>[]>(
    () => [
      {
        title: copy.resourceReference.columnReferrerType,
        dataIndex: "referrerType",
        width: 120,
        render: (value: unknown) => referrerLabel(locale, value as ResourceReference["referrerType"])
      },
      {
        title: copy.resourceReference.columnName,
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
        title: copy.resourceReference.columnBinding,
        dataIndex: "bindingField",
        render: (value: unknown) => String(value ?? "-")
      }
    ],
    [locale, copy]
  );

  return (
    <Card data-testid={testId} title={heading} bordered>
      <Typography.Paragraph type="tertiary" style={{ marginTop: 0 }}>
        {copy.resourceReference.bodyHint}
      </Typography.Paragraph>
      {loading ? (
        <Spin />
      ) : rows.length === 0 ? (
        <Empty title={copy.resourceReference.emptyTitle} description={copy.resourceReference.emptyDescription} />
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
