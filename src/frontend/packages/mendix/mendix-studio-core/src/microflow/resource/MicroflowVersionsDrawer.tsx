import { Button, Drawer, Empty, List, Space, Tag, Typography } from "@douyinfe/semi-ui";

import type { MicroflowVersionSummary } from "./resource-types";
import { formatMicroflowDate, microflowStatusColor, microflowStatusLabel } from "./resource-utils";

const { Text } = Typography;

interface MicroflowVersionsDrawerProps {
  visible: boolean;
  versions: MicroflowVersionSummary[];
  onClose: () => void;
}

export function MicroflowVersionsDrawer({ visible, versions, onClose }: MicroflowVersionsDrawerProps) {
  return (
    <Drawer visible={visible} title={`版本记录（${versions.length}）`} onCancel={onClose} width={560}>
      {versions.length === 0 ? (
        <Empty title="暂无版本" description="发布后会在这里生成版本记录。" />
      ) : (
        <List
          dataSource={versions}
          renderItem={version => (
            <List.Item>
              <div style={{ display: "flex", flexDirection: "column", gap: 8, width: "100%" }}>
                <Space>
                  <Text strong>{version.version}</Text>
                  <Tag color={microflowStatusColor(version.status)}>{microflowStatusLabel(version.status)}</Tag>
                </Space>
                <Text type="tertiary">{version.createdBy || "-"} · {formatMicroflowDate(version.createdAt)}</Text>
                <Text>{version.description || "无发布说明"}</Text>
                <Space>
                  <Button size="small" disabled>查看</Button>
                  <Button size="small" disabled>回滚</Button>
                  <Button size="small" disabled>复制为草稿</Button>
                </Space>
              </div>
            </List.Item>
          )}
        />
      )}
    </Drawer>
  );
}
