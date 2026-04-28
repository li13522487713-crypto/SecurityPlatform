import { useEffect, useState } from "react";
import { Button, Drawer, Empty, Modal, Space, Spin, Tag, Toast, Typography } from "@douyinfe/semi-ui";

import { getMicroflowErrorUserMessage } from "../adapter/http/microflow-api-error";
import type { MicroflowResourceAdapter } from "../adapter/microflow-resource-adapter";
import { MicroflowErrorState } from "../components/error";
import type { MicroflowResource } from "../resource/resource-types";
import type { MicroflowVersionDetail, MicroflowVersionSummary } from "./microflow-version-types";
import { formatVersionStatus, versionStatusColor } from "./microflow-version-utils";
import { MicroflowVersionDetailDrawer } from "./MicroflowVersionDetailDrawer";

const { Text } = Typography;

export interface MicroflowVersionsDrawerProps {
  visible: boolean;
  resource?: MicroflowResource;
  adapter: MicroflowResourceAdapter;
  onClose: () => void;
  onResourceChange: (resource: MicroflowResource) => void;
  onCreated?: (resource: MicroflowResource) => void;
}

export function MicroflowVersionsDrawer({ visible, resource, adapter, onClose, onResourceChange, onCreated }: MicroflowVersionsDrawerProps) {
  const [versions, setVersions] = useState<MicroflowVersionSummary[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<unknown>();
  const [detail, setDetail] = useState<MicroflowVersionDetail>();
  const [detailOpen, setDetailOpen] = useState(false);

  async function loadVersions() {
    if (!resource) {
      return;
    }
    setLoading(true);
    setError(undefined);
    try {
      setVersions(await adapter.getMicroflowVersions(resource.id));
    } catch (caught) {
      setError(caught);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    if (visible) {
      void loadVersions();
    }
  }, [visible, resource?.id]);

  async function openDetail(version: MicroflowVersionSummary) {
    if (!resource) {
      return;
    }
    try {
      const next = await adapter.getMicroflowVersionDetail(resource.id, version.id);
      if (next) {
        setDetail(next);
        setDetailOpen(true);
      }
    } catch (caught) {
      Toast.error(getMicroflowErrorUserMessage(caught));
    }
  }

  async function duplicateVersion(version: MicroflowVersionSummary | MicroflowVersionDetail) {
    if (!resource) {
      return;
    }
    try {
      const created = await adapter.duplicateMicroflowVersion(resource.id, version.id, {
        displayName: `${resource.displayName || resource.name} (${version.version}) Copy`
      });
      Toast.success("已复制为新草稿");
      onCreated?.(created);
      await loadVersions();
    } catch (caught) {
      Toast.error(getMicroflowErrorUserMessage(caught));
    }
  }

  function rollbackVersion(version: MicroflowVersionSummary | MicroflowVersionDetail) {
    if (!resource) {
      return;
    }
    Modal.confirm({
      title: "确认回滚",
      content: `确认将当前微流回滚到 ${version.version}？回滚后资源会进入草稿状态。`,
      onOk: async () => {
        try {
          const next = await adapter.rollbackMicroflowVersion(resource.id, version.id);
          Toast.success("已回滚到历史版本");
          onResourceChange(next);
          await loadVersions();
        } catch (caught) {
          Toast.error(getMicroflowErrorUserMessage(caught));
          Modal.error({
            title: "回滚失败",
            width: 560,
            content: <MicroflowErrorState error={caught} compact />
          });
        }
      }
    });
  }

  async function compareVersion(version: MicroflowVersionSummary) {
    if (!resource) {
      return;
    }
    try {
      const diff = await adapter.compareMicroflowVersion(resource.id, version.id);
      Modal.info({
        title: `比较当前版本与 ${version.version}`,
        width: 680,
        content: diff.addedParameters.length === 0
          && diff.removedParameters.length === 0
          && diff.changedParameters.length === 0
          && !diff.returnTypeChanged
          && diff.addedObjects.length === 0
          && diff.removedObjects.length === 0
          && diff.changedObjects.length === 0
          && diff.addedFlows.length === 0
          && diff.removedFlows.length === 0
          && diff.breakingChanges.length === 0
          ? "无差异"
          : (
            <pre style={{ whiteSpace: "pre-wrap", maxHeight: 420, overflow: "auto" }}>
              {JSON.stringify(diff, null, 2)}
            </pre>
          )
      });
    } catch (caught) {
      Toast.error(getMicroflowErrorUserMessage(caught));
    }
  }

  return (
    <>
      <Drawer visible={visible} title="版本历史" width={620} onCancel={onClose} footer={null}>
        {!resource ? (
          <Empty title="未选择微流" />
        ) : loading ? (
          <Spin />
        ) : error ? (
          <MicroflowErrorState error={error} title="版本服务不可用" compact onRetry={() => void loadVersions()} />
        ) : versions.length === 0 ? (
          <Empty title="暂无版本" description="发布后会生成历史版本和快照。" />
        ) : (
          <Space vertical align="start" spacing={10} style={{ width: "100%" }}>
            {versions.map(version => (
              <div key={version.id} style={{ width: "100%", border: "1px solid var(--semi-color-border)", borderRadius: 8, padding: 12 }}>
                <Space vertical align="start" spacing={8} style={{ width: "100%" }}>
                  <Space wrap>
                    <Text strong>{version.version}</Text>
                    <Tag color={versionStatusColor(version.status)}>{formatVersionStatus(version.status)}</Tag>
                    {version.isLatestPublished ? <Tag color="green">最新发布</Tag> : null}
                    <Tag>引用 {version.referenceCount ?? 0}</Tag>
                  </Space>
                  <Text type="tertiary" size="small">{version.createdAt} · {version.createdBy ?? "-"} · {version.description ?? "无说明"}</Text>
                  <Space wrap>
                    <Tag color={version.validationSummary?.errorCount ? "red" : "green"}>错误 {version.validationSummary?.errorCount ?? 0}</Tag>
                    <Tag color="orange">警告 {version.validationSummary?.warningCount ?? 0}</Tag>
                    <Tag color="blue">提示 {version.validationSummary?.infoCount ?? 0}</Tag>
                  </Space>
                  <Space>
                    <Button size="small" onClick={() => void openDetail(version)}>查看详情</Button>
                    <Button size="small" onClick={() => void duplicateVersion(version)}>复制为新草稿</Button>
                    <Button size="small" type="warning" onClick={() => rollbackVersion(version)}>回滚</Button>
                    <Button size="small" onClick={() => void compareVersion(version)}>比较当前版本</Button>
                  </Space>
                </Space>
              </div>
            ))}
          </Space>
        )}
      </Drawer>
      <MicroflowVersionDetailDrawer
        visible={detailOpen}
        detail={detail}
        onClose={() => setDetailOpen(false)}
        onDuplicate={version => void duplicateVersion(version)}
        onRollback={rollbackVersion}
      />
    </>
  );
}
