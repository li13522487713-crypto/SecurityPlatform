import { Button, Card, Space, Tag, Typography } from "@douyinfe/semi-ui";
import { IconRefresh, IconLink } from "@douyinfe/semi-icons";
import type { StudioMicroflowDefinitionView } from "./studio-microflow-types";

export interface MicroflowWorkbenchPlaceholderProps {
  microflow: StudioMicroflowDefinitionView;
  onRefresh?: () => void;
  onOpenReferences?: () => void;
}

const { Text, Title } = Typography;

function formatValue(value?: string | number | boolean): string {
  if (value === undefined || value === null || value === "") {
    return "-";
  }
  return String(value);
}

function MetadataRow({ label, value }: { label: string; value?: string | number | boolean }) {
  return (
    <div className="studio-microflow-placeholder__meta-row">
      <Text type="tertiary" size="small">{label}</Text>
      <Text size="small" ellipsis={{ showTooltip: true }}>{formatValue(value)}</Text>
    </div>
  );
}

export function MicroflowWorkbenchPlaceholder({
  microflow,
  onRefresh,
  onOpenReferences
}: MicroflowWorkbenchPlaceholderProps) {
  return (
    <div className="studio-microflow-placeholder">
      <Card className="studio-microflow-placeholder__card" bodyStyle={{ padding: 28 }}>
        <Space vertical align="start" spacing={16} style={{ width: "100%" }}>
          <div className="studio-microflow-placeholder__header">
            <div>
              <Title heading={4} style={{ margin: 0 }}>
                {microflow.displayName || microflow.name}
              </Title>
              <Text type="tertiary" size="small">
                {microflow.qualifiedName}
              </Text>
            </div>
            <Space spacing={8}>
              <Tag color={microflow.status === "published" ? "green" : "blue"}>
                {microflow.status}
              </Tag>
              {microflow.publishStatus ? (
                <Tag color="grey">{microflow.publishStatus}</Tag>
              ) : null}
            </Space>
          </div>

          <div className="studio-microflow-placeholder__meta-grid">
            <MetadataRow label="Microflow ID" value={microflow.id} />
            <MetadataRow label="Module" value={microflow.moduleName || microflow.moduleId} />
            <MetadataRow label="Module ID" value={microflow.moduleId} />
            <MetadataRow label="Status" value={microflow.status} />
            <MetadataRow label="Publish Status" value={microflow.publishStatus} />
            <MetadataRow label="Schema ID" value={microflow.schemaId} />
            <MetadataRow label="Version" value={microflow.version} />
            <MetadataRow label="Reference Count" value={microflow.referenceCount} />
            <MetadataRow label="Updated At" value={microflow.updatedAt} />
          </div>

          <div className="studio-microflow-placeholder__notice">
            <Text>
              已打开微流文档。真实 schema 加载与编辑器宿主将在 Release 第 5 轮接入；本轮不会加载 schema、保存 schema 或渲染 MicroflowEditor。
            </Text>
          </div>

          <Space spacing={8}>
            <Button
              theme="light"
              type="primary"
              icon={<IconRefresh />}
              disabled={!onRefresh}
              onClick={() => onRefresh?.()}
            >
              Refresh Resource Info
            </Button>
            <Button
              theme="light"
              icon={<IconLink />}
              disabled={!onOpenReferences}
              onClick={() => onOpenReferences?.()}
            >
              View References
            </Button>
          </Space>
        </Space>
      </Card>
    </div>
  );
}
