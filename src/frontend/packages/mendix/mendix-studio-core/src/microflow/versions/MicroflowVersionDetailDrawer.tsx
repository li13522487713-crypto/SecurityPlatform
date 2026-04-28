import { Drawer, Empty, Space, Tag, Typography } from "@douyinfe/semi-ui";
import { Button } from "@douyinfe/semi-ui";

import type { MicroflowVersionDetail } from "./microflow-version-types";
import { formatMicroflowDataType, formatVersionStatus, versionStatusColor } from "./microflow-version-utils";

const { Text, Title } = Typography;

export function MicroflowVersionDetailDrawer({
  visible,
  detail,
  onClose,
  onDuplicate,
  onRollback
}: {
  visible: boolean;
  detail?: MicroflowVersionDetail;
  onClose: () => void;
  onDuplicate: (detail: MicroflowVersionDetail) => void;
  onRollback: (detail: MicroflowVersionDetail) => void;
}) {
  const schema = detail?.snapshot.schema;
  const diff = detail?.diffFromCurrent;

  return (
    <Drawer visible={visible} title="版本详情" width={560} onCancel={onClose} footer={null}>
      {!detail || !schema ? (
        <Empty title="未选择版本" />
      ) : (
        <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
          <Space wrap>
            <Title heading={5} style={{ margin: 0 }}>{detail.version}</Title>
            <Tag color={versionStatusColor(detail.status)}>{formatVersionStatus(detail.status)}</Tag>
            {detail.isLatestPublished ? <Tag color="green">最新发布</Tag> : null}
          </Space>
          <Text type="tertiary">{detail.description || "无发布说明"} · {detail.createdAt} · {detail.createdBy ?? "-"}</Text>
          <Space wrap>
            <Tag color={detail.validationSummary?.errorCount ? "red" : "green"}>错误 {detail.validationSummary?.errorCount ?? 0}</Tag>
            <Tag color="orange">警告 {detail.validationSummary?.warningCount ?? 0}</Tag>
            <Tag color="blue">提示 {detail.validationSummary?.infoCount ?? 0}</Tag>
          </Space>
          <div style={{ width: "100%", border: "1px solid var(--semi-color-border)", borderRadius: 8, padding: 12 }}>
            <Space vertical align="start">
              <Text strong>Schema 基础信息</Text>
              <Text>参数 {schema.parameters.length} · 节点 {schema.objectCollection.objects.length} · 连线 {(schema.flows ?? schema.objectCollection.flows ?? []).length}</Text>
              <Text>返回类型 {formatMicroflowDataType(schema.returnType)}</Text>
            </Space>
          </div>
          {diff ? (
            <Space vertical align="start" spacing={8} style={{ width: "100%" }}>
              <Text strong>Diff Summary</Text>
              <Text size="small">新增参数：{diff.addedParameters.join(", ") || "-"}</Text>
              <Text size="small">删除参数：{diff.removedParameters.join(", ") || "-"}</Text>
              <Text size="small">变更参数：{diff.changedParameters.map(item => `${item.name}: ${item.beforeType} -> ${item.afterType}`).join(", ") || "-"}</Text>
              <Text size="small">返回类型：{diff.returnTypeChanged ? `${diff.returnTypeChanged.beforeType} -> ${diff.returnTypeChanged.afterType}` : "-"}</Text>
              <Text size="small">新增节点：{diff.addedObjects.length} · 删除节点：{diff.removedObjects.length} · 变更节点：{diff.changedObjects.length}</Text>
              <Text size="small">新增连线：{diff.addedFlows.length} · 删除连线：{diff.removedFlows.length}</Text>
              <Text size="small">破坏性变更：{diff.breakingChanges.length}</Text>
            </Space>
          ) : null}
          <Space>
            <Button onClick={() => onDuplicate(detail)}>复制为新草稿</Button>
            <Button type="warning" onClick={() => onRollback(detail)}>回滚到此版本</Button>
            <Button onClick={onClose}>关闭</Button>
          </Space>
        </Space>
      )}
    </Drawer>
  );
}
