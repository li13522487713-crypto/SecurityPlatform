import { useEffect, useState } from "react";
import { Button, Empty, Spin, Switch, Table, Tag, Toast, Typography } from "@douyinfe/semi-ui";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import { useAppI18n } from "../i18n";
import { useWorkspaceContext } from "../workspace-context";
import {
  getModelConfigsPaged,
  updateModelConfig,
  type ModelConfigDto
} from "../../services/api-model-config";

export function WorkspaceSettingsModelsPage() {
  const { t } = useAppI18n();
  const workspace = useWorkspaceContext();
  const [items, setItems] = useState<ModelConfigDto[]>([]);
  const [loading, setLoading] = useState(true);

  const refresh = () => {
    setLoading(true);
    getModelConfigsPaged({ pageIndex: 1, pageSize: 50 })
      .then(result => setItems(result.items))
      .catch(() => setItems([]))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    refresh();
  }, []);

  const handleToggle = async (item: ModelConfigDto, nextEnabled: boolean) => {
    try {
      await updateModelConfig(item.id, {
        name: item.name,
        apiKey: "",
        baseUrl: item.baseUrl,
        defaultModel: item.defaultModel,
        isEnabled: nextEnabled,
        supportsEmbedding: item.supportsEmbedding,
        modelId: item.modelId,
        systemPrompt: item.systemPrompt,
        enableStreaming: item.enableStreaming,
        enableReasoning: item.enableReasoning,
        enableTools: item.enableTools,
        enableVision: item.enableVision,
        enableJsonMode: item.enableJsonMode,
        temperature: item.temperature,
        maxTokens: item.maxTokens,
        topP: item.topP,
        frequencyPenalty: item.frequencyPenalty,
        presencePenalty: item.presencePenalty
      });
      Toast.success(t("cozeCreateSuccess"));
      refresh();
    } catch (error) {
      Toast.error((error as Error).message || t("cozeCreateFailed"));
    }
  };

  const columns: ColumnProps<ModelConfigDto>[] = [
    { title: t("cozeSettingsPublishColumnName"), dataIndex: "name" },
    { title: t("cozeSettingsPublishColumnType"), dataIndex: "providerType", render: (value: string) => <Tag color="blue">{value}</Tag> },
    { title: "Model", dataIndex: "defaultModel" },
    {
      title: t("cozeSettingsPublishColumnStatus"),
      dataIndex: "isEnabled",
      render: (value: boolean, record) => (
        <Switch checked={value} onChange={next => void handleToggle(record, next)} />
      )
    },
    { title: t("cozeSettingsPublishColumnUpdatedAt"), dataIndex: "createdAt" }
  ];

  return (
    <div className="coze-page coze-settings-page" data-testid="coze-settings-models-page">
      <header className="coze-page__header">
        <Typography.Text type="tertiary">{t("cozeSettingsKicker")}</Typography.Text>
        <Typography.Title heading={3} style={{ margin: "8px 0 4px" }}>{t("cozeSettingsModelsTitle")}</Typography.Title>
        <Typography.Text type="tertiary">{t("cozeSettingsModelsSubtitle")}</Typography.Text>
      </header>

      <section className="coze-page__toolbar">
        <Button onClick={refresh}>{t("cozeCommonRefresh")}</Button>
      </section>

      <section className="coze-page__body">
        {loading ? (
          <div className="coze-page__loading"><Spin /></div>
        ) : items.length === 0 ? (
          <Empty description={t("cozeSettingsModelsEmpty")} />
        ) : (
          <Table columns={columns} dataSource={items} rowKey="id" pagination={false} />
        )}
      </section>

      <footer className="coze-page__footer">
        <Typography.Text type="tertiary">
          Workspace: {workspace.name || workspace.appKey}
        </Typography.Text>
      </footer>
    </div>
  );
}
