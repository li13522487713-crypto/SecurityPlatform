import { Button, Dropdown, Space, Table, Tag, Tooltip, Typography } from "@douyinfe/semi-ui";
import { IconMore, IconStar, IconStarStroked } from "@douyinfe/semi-icons";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";

import type { MicroflowResource } from "./resource-types";
import type { MicroflowResourceAction } from "./MicroflowResourceCard";
import { canRunMicroflowAction, formatMicroflowDate, microflowStatusColor, microflowStatusLabel } from "./resource-utils";

const { Text } = Typography;

interface MicroflowResourceTableProps {
  resources: MicroflowResource[];
  readonly?: boolean;
  onAction: (action: MicroflowResourceAction, resource: MicroflowResource) => void;
}

export function MicroflowResourceTable({ resources, readonly, onAction }: MicroflowResourceTableProps) {
  const columns: ColumnProps<MicroflowResource>[] = [
    {
      title: "名称",
      dataIndex: "displayName",
      render: (_value, record) => (
        <div style={{ display: "flex", flexDirection: "column" }}>
          <Text strong>{record.displayName || record.name}</Text>
          <Text type="tertiary" size="small">{record.description || record.name}</Text>
        </div>
      )
    },
    {
      title: "状态",
      dataIndex: "status",
      width: 100,
      render: (_value, record) => <Tag color={microflowStatusColor(record.status)}>{microflowStatusLabel(record.status)}</Tag>
    },
    { title: "版本", dataIndex: "version", width: 90, render: value => <Text>{String(value)}</Text> },
    { title: "模块", dataIndex: "moduleName", width: 120, render: (_value, record) => <Text>{record.moduleName || record.moduleId}</Text> },
    {
      title: "标签",
      dataIndex: "tags",
      render: (_value, record) => <Space wrap>{record.tags.slice(0, 3).map(tag => <Tag key={tag} size="small">{tag}</Tag>)}</Space>
    },
    { title: "引用数", dataIndex: "referenceCount", width: 90 },
    { title: "最近运行", dataIndex: "lastRunStatus", width: 110, render: value => <Text>{String(value || "neverRun")}</Text> },
    { title: "修改人", dataIndex: "updatedBy", width: 120, render: (_value, record) => <Text>{record.updatedBy || record.ownerName || "-"}</Text> },
    { title: "更新时间", dataIndex: "updatedAt", width: 150, render: value => <Text>{formatMicroflowDate(String(value))}</Text> },
    {
      title: "操作",
      width: 120,
      render: (_value, record) => {
        const menu = (
          <Dropdown.Menu>
            <Dropdown.Item onClick={() => onAction("open", record)}>编辑</Dropdown.Item>
            <Dropdown.Item disabled={readonly || !canRunMicroflowAction(record, "canDuplicate")} onClick={() => onAction("duplicate", record)}>复制</Dropdown.Item>
            <Dropdown.Item disabled={readonly} onClick={() => onAction("rename", record)}>重命名</Dropdown.Item>
            <Dropdown.Item disabled={readonly || !canRunMicroflowAction(record, "canPublish")} onClick={() => onAction("publish", record)}>发布</Dropdown.Item>
            <Dropdown.Item onClick={() => onAction("references", record)}>查看引用</Dropdown.Item>
            <Dropdown.Item onClick={() => onAction("versions", record)}>查看版本</Dropdown.Item>
            {record.archived ? (
              <Dropdown.Item disabled={readonly || !canRunMicroflowAction(record, "canArchive")} onClick={() => onAction("restore", record)}>恢复</Dropdown.Item>
            ) : (
              <Dropdown.Item disabled={readonly || !canRunMicroflowAction(record, "canArchive")} onClick={() => onAction("archive", record)}>归档</Dropdown.Item>
            )}
            <Dropdown.Item type="danger" disabled={readonly || !canRunMicroflowAction(record, "canDelete")} onClick={() => onAction("delete", record)}>删除</Dropdown.Item>
          </Dropdown.Menu>
        );
        return (
          <Space onClick={event => event.stopPropagation()}>
            <Button theme="borderless" type="tertiary" icon={record.favorite ? <IconStar /> : <IconStarStroked />} onClick={() => onAction("favorite", record)} />
            <Tooltip content={readonly ? "当前为只读模式" : undefined}>
              <Dropdown trigger="click" position="bottomRight" render={menu}>
                <Button theme="borderless" type="tertiary" icon={<IconMore />} />
              </Dropdown>
            </Tooltip>
          </Space>
        );
      }
    }
  ];

  return (
    <Table
      rowKey="id"
      columns={columns}
      dataSource={resources}
      pagination={false}
      onRow={record => ({
        onClick: () => onAction("open", record),
        style: { cursor: "pointer" }
      })}
    />
  );
}
