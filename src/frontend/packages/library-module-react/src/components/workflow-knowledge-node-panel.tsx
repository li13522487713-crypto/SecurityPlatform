import { useState } from "react";
import {
  Banner,
  Button,
  Empty,
  Space,
  Switch,
  Tag,
  Toast,
  Typography
} from "@douyinfe/semi-ui";
import { IconPlus } from "@douyinfe/semi-icons";
import {
  DEFAULT_RETRIEVAL_PROFILE,
  type KnowledgeBaseDto,
  type LibraryKnowledgeApi,
  type RetrievalProfile,
  type SupportedLocale
} from "../types";
import { getLibraryCopy } from "../copy";
import { KnowledgeResourcePicker } from "./knowledge-resource-picker";

export interface WorkflowKnowledgeNodePanelProps {
  api: LibraryKnowledgeApi;
  locale: SupportedLocale;
  /** 当前节点已经绑定的知识库 ID 列表 */
  knowledgeBaseIds: number[];
  /** 检索策略（可选，未传则使用默认 DEFAULT_RETRIEVAL_PROFILE） */
  retrievalProfile?: RetrievalProfile;
  /** 是否启用节点级 debug 输出 */
  debug?: boolean;
  onChange: (next: {
    knowledgeBaseIds: number[];
    retrievalProfile: RetrievalProfile;
    debug: boolean;
  }) => void;
}

/**
 * 工作流知识检索节点配置面板：
 * - 复用 KnowledgeResourcePicker 选择目标知识库
 * - 内嵌精简版 RetrievalProfile（topK / rerank / queryRewrite / debug）
 * - 设计用于在 packages/workflow/playground 节点属性面板内复用，避免重复实现
 */
export function WorkflowKnowledgeNodePanel({
  api,
  locale,
  knowledgeBaseIds,
  retrievalProfile,
  debug,
  onChange
}: WorkflowKnowledgeNodePanelProps) {
  const copy = getLibraryCopy(locale);
  const [pickerVisible, setPickerVisible] = useState(false);
  const [pickedKbs, setPickedKbs] = useState<KnowledgeBaseDto[]>([]);
  const profile = retrievalProfile ?? DEFAULT_RETRIEVAL_PROFILE;
  const isDebug = debug ?? false;

  function patchProfile(next: Partial<RetrievalProfile>): void {
    onChange({
      knowledgeBaseIds,
      retrievalProfile: { ...profile, ...next },
      debug: isDebug
    });
  }

  return (
    <Space vertical align="start" style={{ width: "100%" }}>
      <Typography.Text strong>{copy.detailTabBindings}</Typography.Text>
      <div style={{ width: "100%" }}>
        {knowledgeBaseIds.length === 0 ? (
          <Empty description={copy.bindingsEmpty} />
        ) : (
          <Space wrap spacing={6}>
            {knowledgeBaseIds.map(id => {
              const dto = pickedKbs.find(item => item.id === id);
              return (
                <Tag
                  key={id}
                  color="cyan"
                  closable
                  onClose={() => onChange({
                    knowledgeBaseIds: knowledgeBaseIds.filter(x => x !== id),
                    retrievalProfile: profile,
                    debug: isDebug
                  })}
                >
                  {dto?.name ?? `KB #${id}`}
                </Tag>
              );
            })}
          </Space>
        )}
      </div>
      <Button icon={<IconPlus />} onClick={() => setPickerVisible(true)}>
        {copy.resourcePickerTitle}
      </Button>

      <Banner type="info" description={copy.retrievalProfileTitle} />
      <Space spacing={8} wrap>
        <div>
          <Typography.Text type="tertiary" size="small">{copy.retrievalProfileTopK}</Typography.Text>
          <input
            type="number"
            value={profile.topK}
            min={1}
            max={50}
            style={{ width: 80 }}
            onChange={event => patchProfile({ topK: Math.max(1, Number(event.target.value) || 1) })}
          />
        </div>
        <div>
          <Typography.Text type="tertiary" size="small">{copy.wizardEnableRerank}</Typography.Text>
          <Switch checked={profile.enableRerank} onChange={value => patchProfile({ enableRerank: value })} />
        </div>
        <div>
          <Typography.Text type="tertiary" size="small">{copy.wizardEnableQueryRewrite}</Typography.Text>
          <Switch checked={profile.enableQueryRewrite} onChange={value => patchProfile({ enableQueryRewrite: value })} />
        </div>
        <div>
          <Typography.Text type="tertiary" size="small">{copy.retrievalEnableDebug}</Typography.Text>
          <Switch checked={isDebug} onChange={value => onChange({ knowledgeBaseIds, retrievalProfile: profile, debug: value })} />
        </div>
      </Space>

      <KnowledgeResourcePicker
        api={api}
        locale={locale}
        visible={pickerVisible}
        value={knowledgeBaseIds}
        multiple
        onChange={(ids, items) => {
          setPickedKbs(items);
          onChange({ knowledgeBaseIds: ids, retrievalProfile: profile, debug: isDebug });
          setPickerVisible(false);
          if (ids.length > 0) {
            Toast.success(copy.resourcePickerSelect);
          }
        }}
        onCancel={() => setPickerVisible(false)}
      />
    </Space>
  );
}
