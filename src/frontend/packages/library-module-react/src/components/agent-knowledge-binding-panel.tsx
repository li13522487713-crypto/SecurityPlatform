import { useEffect, useState } from "react";
import {
  Banner,
  Button,
  Empty,
  Modal,
  Space,
  Tag,
  Toast,
  Typography
} from "@douyinfe/semi-ui";
import { IconDelete, IconPlus, IconSetting } from "@douyinfe/semi-icons";
import {
  DEFAULT_RETRIEVAL_PROFILE,
  type KnowledgeBaseDto,
  type LibraryKnowledgeApi,
  type RetrievalProfile,
  type SupportedLocale
} from "../types";
import { getLibraryCopy } from "../copy";
import { KnowledgeResourcePicker } from "./knowledge-resource-picker";
import { RetrievalProfileFields } from "./knowledge-detail/retrieval-profile-editor";

export interface AgentKnowledgeBindingPanelProps {
  api: LibraryKnowledgeApi;
  locale: SupportedLocale;
  /** Agent / App / Chatflow 标识，写入 binding.callerId */
  callerId: string;
  callerName: string;
  callerType: "agent" | "app" | "chatflow";
}

/**
 * Agent / App / Chatflow 装配面板（v5 §39 / 计划 G7 完整版）：
 * - 拉取当前 caller 已绑定的知识库（基于 listAllBindings 过滤）
 * - 嵌入 RetrievalProfileFields，允许配置"默认 retrievalProfile override"
 * - 新增绑定时把该 override 透传到 createBinding，写入 retrievalProfileOverrideJson
 * - 支持解除绑定（removeBinding）
 *
 * 该组件适合内嵌到 module-studio-react 的 Agent 编辑页 / App 编辑页等装配点。
 */
export function AgentKnowledgeBindingPanel({
  api,
  locale,
  callerId,
  callerName,
  callerType
}: AgentKnowledgeBindingPanelProps) {
  const copy = getLibraryCopy(locale);
  const [linkedKbs, setLinkedKbs] = useState<KnowledgeBaseDto[]>([]);
  const [pickerVisible, setPickerVisible] = useState(false);
  const [profileModalVisible, setProfileModalVisible] = useState(false);
  const [profile, setProfile] = useState<RetrievalProfile>(DEFAULT_RETRIEVAL_PROFILE);
  const [busy, setBusy] = useState(false);

  async function refresh() {
    if (!api.listAllBindings) return;
    setBusy(true);
    try {
      const response = await api.listAllBindings({ pageIndex: 1, pageSize: 200 });
      const myBindings = response.items.filter(item => item.callerId === callerId && item.callerType === callerType);
      const kbDtos = await Promise.all(myBindings.map(item => api.getKnowledgeBase(item.knowledgeBaseId).catch(() => null)));
      setLinkedKbs(kbDtos.filter((dto): dto is KnowledgeBaseDto => dto !== null));
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setBusy(false);
    }
  }

  useEffect(() => {
    void refresh();
    return undefined;
  }, [api, callerId, callerType]);

  async function handlePicked(ids: number[]): Promise<void> {
    if (!api.createBinding || !api.listAllBindings) {
      setPickerVisible(false);
      return;
    }
    const existing = new Set(linkedKbs.map(kb => kb.id));
    const toAdd = ids.filter(id => !existing.has(id));
    setPickerVisible(false);
    setBusy(true);
    try {
      for (const id of toAdd) {
        // v5 §39 / 计划 G7：把当前 profile 作为 retrievalProfileOverride 写入 binding
        await api.createBinding(id, {
          callerType,
          callerId,
          callerName,
          retrievalProfileOverride: profile
        });
      }
      await refresh();
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setBusy(false);
    }
  }

  async function handleRemove(kbId: number): Promise<void> {
    if (!api.listAllBindings || !api.removeBinding) return;
    setBusy(true);
    try {
      const response = await api.listAllBindings({ pageIndex: 1, pageSize: 200 });
      const target = response.items.find(item =>
        item.knowledgeBaseId === kbId && item.callerId === callerId && item.callerType === callerType
      );
      if (target) {
        await api.removeBinding(kbId, target.id);
      }
      await refresh();
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <Space vertical align="start" style={{ width: "100%" }}>
      <Banner type="info" description={copy.bindingsSubtitle} />
      <Space spacing={6} wrap>
        {linkedKbs.length === 0 ? (
          <Empty description={copy.bindingsEmpty} />
        ) : (
          linkedKbs.map(kb => (
            <div key={kb.id} className="atlas-summary-tile" style={{ padding: 8 }}>
              <Space spacing={6}>
                <Typography.Text strong>{kb.name}</Typography.Text>
                <Tag color="cyan" size="small">{kb.kind ?? copy.typeLabels[kb.type]}</Tag>
                <Button theme="borderless" type="danger" icon={<IconDelete />} size="small" onClick={() => void handleRemove(kb.id)}>
                  {copy.bindingsActionRemove}
                </Button>
              </Space>
            </div>
          ))
        )}
      </Space>
      <Space>
        <Button type="primary" icon={<IconPlus />} loading={busy} onClick={() => setPickerVisible(true)}>
          {copy.bindingsAddTitle}
        </Button>
        <Button icon={<IconSetting />} onClick={() => setProfileModalVisible(true)}>
          {copy.retrievalProfileTitle}
        </Button>
      </Space>

      <Modal
        title={copy.retrievalProfileTitle}
        visible={profileModalVisible}
        onOk={() => setProfileModalVisible(false)}
        onCancel={() => setProfileModalVisible(false)}
        okText={copy.save}
        cancelText={copy.close}
        width={520}
      >
        <Banner
          type="info"
          description={copy.bindingsRetrievalProfileOverrideHint}
          style={{ marginBottom: 12 }}
        />
        <RetrievalProfileFields locale={locale} value={profile} onChange={setProfile} />
      </Modal>

      <KnowledgeResourcePicker
        api={api}
        locale={locale}
        visible={pickerVisible}
        value={linkedKbs.map(kb => kb.id)}
        onChange={ids => void handlePicked(ids)}
        onCancel={() => setPickerVisible(false)}
      />
    </Space>
  );
}
