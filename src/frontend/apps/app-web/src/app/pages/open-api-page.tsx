import { useEffect, useState } from "react";
import { Button, Empty, Form, Modal, Spin, Tag, Toast, Typography } from "@douyinfe/semi-ui";
import { useAppI18n } from "../i18n";
import {
  createOpenApiKey,
  deleteOpenApiKey,
  listOpenApiKeys,
  type OpenApiKeyItem
} from "../../services/api-open-api-keys";

export function OpenApiPage() {
  const { t } = useAppI18n();
  const [items, setItems] = useState<OpenApiKeyItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [createOpen, setCreateOpen] = useState(false);
  const [creating, setCreating] = useState(false);
  const [alias, setAlias] = useState("");
  const [revealedKey, setRevealedKey] = useState<string | null>(null);

  const refresh = () => {
    setLoading(true);
    listOpenApiKeys()
      .then(setItems)
      .catch(() => setItems([]))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    refresh();
  }, []);

  const handleCreate = async () => {
    const trimmed = alias.trim();
    if (!trimmed) {
      Toast.warning(t("cozeOpenApiCreateKey"));
      return;
    }
    setCreating(true);
    try {
      const result = await createOpenApiKey(trimmed);
      Toast.success(t("cozeCreateSuccess"));
      setRevealedKey(result.key);
      setAlias("");
      setCreateOpen(false);
      refresh();
    } catch (error) {
      Toast.error((error as Error).message || t("cozeCreateFailed"));
    } finally {
      setCreating(false);
    }
  };

  const handleDelete = async (id: string) => {
    await deleteOpenApiKey(id);
    refresh();
  };

  return (
    <div className="coze-page" data-testid="coze-open-api-page">
      <header className="coze-page__header">
        <Typography.Title heading={3} style={{ margin: 0 }}>{t("cozeOpenApiTitle")}</Typography.Title>
        <Typography.Text type="tertiary">{t("cozeOpenApiSubtitle")}</Typography.Text>
      </header>

      <section className="coze-page__toolbar">
        <Button theme="solid" type="primary" onClick={() => setCreateOpen(true)}>
          {t("cozeOpenApiCreateKey")}
        </Button>
      </section>

      <section className="coze-page__body">
        {loading ? (
          <div className="coze-page__loading"><Spin /></div>
        ) : items.length === 0 ? (
          <Empty description={t("cozeOpenApiEmpty")} />
        ) : (
          <ul className="coze-list">
            {items.map(item => (
              <li key={item.id} className="coze-list__item">
                <div>
                  <strong>{item.alias}</strong>
                  <span>{item.prefix}.****</span>
                </div>
                <div style={{ display: "flex", gap: 6, alignItems: "center" }}>
                  {item.scopes.map(scope => <Tag key={scope} size="small">{scope}</Tag>)}
                  <Button type="danger" theme="borderless" onClick={() => void handleDelete(item.id)}>
                    {t("cozeSettingsChannelDelete")}
                  </Button>
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>

      <Modal
        title={t("cozeOpenApiCreateKey")}
        visible={createOpen}
        onCancel={() => setCreateOpen(false)}
        onOk={() => void handleCreate()}
        confirmLoading={creating}
      >
        <Form labelPosition="top" labelWidth="100%">
          <Form.Input
            field="alias"
            label="Alias"
            placeholder="my-key"
            value={alias}
            onChange={value => setAlias(value)}
          />
        </Form>
      </Modal>

      {revealedKey ? (
        <Modal title="API Key" visible onCancel={() => setRevealedKey(null)} footer={null}>
          <Typography.Text copyable>{revealedKey}</Typography.Text>
        </Modal>
      ) : null}
    </div>
  );
}
