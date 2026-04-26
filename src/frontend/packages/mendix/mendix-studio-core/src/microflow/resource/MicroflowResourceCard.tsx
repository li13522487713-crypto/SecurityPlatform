import { Button, Card, Dropdown, Space, Tag, Tooltip, Typography } from "@douyinfe/semi-ui";
import { IconCode, IconMore, IconStar, IconStarStroked } from "@douyinfe/semi-icons";

import type { MicroflowResource } from "./resource-types";
import { canRunMicroflowAction, formatMicroflowDate, microflowPublishStatusLabel, microflowStatusColor, microflowStatusLabel } from "./resource-utils";

const { Text, Title } = Typography;

export type MicroflowResourceAction = "open" | "duplicate" | "rename" | "publish" | "references" | "versions" | "favorite" | "archive" | "restore" | "delete";

interface MicroflowResourceCardProps {
  resource: MicroflowResource;
  readonly?: boolean;
  onAction: (action: MicroflowResourceAction, resource: MicroflowResource) => void;
}

function disabledText(disabled: boolean): string | undefined {
  return disabled ? "当前资源无权限或处于只读状态" : undefined;
}

export function MicroflowResourceCard({ resource, readonly, onAction }: MicroflowResourceCardProps) {
  const publishDisabled = readonly || !canRunMicroflowAction(resource, "canPublish");
  const duplicateDisabled = readonly || !canRunMicroflowAction(resource, "canDuplicate");
  const archiveDisabled = readonly || !canRunMicroflowAction(resource, "canArchive");
  const deleteDisabled = readonly || !canRunMicroflowAction(resource, "canDelete");

  const menu = (
    <Dropdown.Menu>
      <Dropdown.Item onClick={() => onAction("open", resource)}>编辑</Dropdown.Item>
      <Dropdown.Item disabled={duplicateDisabled} onClick={() => onAction("duplicate", resource)}>复制</Dropdown.Item>
      <Dropdown.Item disabled={readonly} onClick={() => onAction("rename", resource)}>重命名</Dropdown.Item>
      <Dropdown.Item disabled={publishDisabled} onClick={() => onAction("publish", resource)}>发布</Dropdown.Item>
      <Dropdown.Item onClick={() => onAction("references", resource)}>查看引用</Dropdown.Item>
      <Dropdown.Item onClick={() => onAction("versions", resource)}>查看版本</Dropdown.Item>
      {resource.archived ? (
        <Dropdown.Item disabled={archiveDisabled} onClick={() => onAction("restore", resource)}>恢复</Dropdown.Item>
      ) : (
        <Dropdown.Item disabled={archiveDisabled} onClick={() => onAction("archive", resource)}>归档</Dropdown.Item>
      )}
      <Dropdown.Item type="danger" disabled={deleteDisabled} onClick={() => onAction("delete", resource)}>删除</Dropdown.Item>
    </Dropdown.Menu>
  );

  return (
    <Card
      shadows="hover"
      style={{ height: "100%", cursor: "pointer" }}
      bodyStyle={{ display: "flex", flexDirection: "column", gap: 12, height: "100%" }}
      onClick={() => onAction("open", resource)}
    >
      <div style={{ display: "flex", justifyContent: "space-between", gap: 12 }}>
        <Space align="start" style={{ minWidth: 0 }}>
          <div style={{ width: 42, height: 42, borderRadius: 12, background: "var(--semi-color-primary-light-default)", display: "flex", alignItems: "center", justifyContent: "center", color: "var(--semi-color-primary)" }}>
            <IconCode />
          </div>
          <div style={{ minWidth: 0 }}>
            <Title heading={6} style={{ margin: 0 }} ellipsis={{ showTooltip: true }}>{resource.displayName || resource.name}</Title>
            <Text type="tertiary" size="small">{resource.name}</Text>
          </div>
        </Space>
        <Space onClick={event => event.stopPropagation()}>
          <Button theme="borderless" type="tertiary" icon={resource.favorite ? <IconStar /> : <IconStarStroked />} onClick={() => onAction("favorite", resource)} />
          <Tooltip content={disabledText(Boolean(readonly))}>
            <Dropdown trigger="click" position="bottomRight" render={menu}>
              <Button theme="borderless" type="tertiary" icon={<IconMore />} />
            </Dropdown>
          </Tooltip>
        </Space>
      </div>
      <Text type="tertiary" ellipsis={{ rows: 2, showTooltip: true }} style={{ minHeight: 40 }}>
        {resource.description || "暂无描述"}
      </Text>
      <Space wrap>
        <Tag color={microflowStatusColor(resource.status)}>{microflowStatusLabel(resource.status)}</Tag>
        <Tag>{microflowPublishStatusLabel(resource.publishStatus)}</Tag>
        <Tag>v{resource.version}</Tag>
        <Tag>{resource.moduleName || resource.moduleId}</Tag>
      </Space>
      <Space wrap>
        {resource.tags.slice(0, 4).map(tag => <Tag key={tag} size="small">{tag}</Tag>)}
      </Space>
      <div style={{ marginTop: "auto", display: "flex", justifyContent: "space-between", gap: 8 }}>
        <Text type="tertiary" size="small">引用 {resource.referenceCount}</Text>
        <Text type="tertiary" size="small">{resource.lastRunStatus || "neverRun"} · {formatMicroflowDate(resource.updatedAt)}</Text>
      </div>
    </Card>
  );
}
