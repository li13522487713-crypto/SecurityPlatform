import { Drawer, Empty, List, Tag, Typography } from "@douyinfe/semi-ui";

import type { MicroflowReference } from "./resource-types";

const { Text } = Typography;

interface MicroflowReferencesDrawerProps {
  visible: boolean;
  references: MicroflowReference[];
  onClose: () => void;
}

export function MicroflowReferencesDrawer({ visible, references, onClose }: MicroflowReferencesDrawerProps) {
  return (
    <Drawer visible={visible} title={`引用关系（${references.length}）`} onCancel={onClose} width={560}>
      {references.length === 0 ? (
        <Empty title="暂无引用" description="当前微流还没有被其它资源引用。" />
      ) : (
        <List
          dataSource={references}
          renderItem={reference => (
            <List.Item>
              <div style={{ display: "flex", flexDirection: "column", gap: 6, width: "100%" }}>
                <div style={{ display: "flex", justifyContent: "space-between", gap: 8 }}>
                  <Text strong>{reference.sourceName}</Text>
                  <Tag>{reference.sourceType}</Tag>
                </div>
                <Text type="tertiary">{reference.description || reference.sourcePath || reference.sourceId}</Text>
                <div>
                  <Tag color={reference.impactLevel === "high" ? "red" : reference.impactLevel === "medium" ? "orange" : "blue"}>
                    {reference.impactLevel || "none"}
                  </Tag>
                  {reference.version ? <Tag>版本 {reference.version}</Tag> : null}
                </div>
              </div>
            </List.Item>
          )}
        />
      )}
    </Drawer>
  );
}
