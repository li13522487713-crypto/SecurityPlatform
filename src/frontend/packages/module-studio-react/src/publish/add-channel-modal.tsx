import { useEffect, useMemo, useState } from "react";
import {
  Banner,
  Button,
  Card,
  Empty,
  Form,
  Modal,
  Space,
  Spin,
  Steps,
  Tag,
  Toast,
  Typography
} from "@douyinfe/semi-ui";
import type { StudioLocale } from "../types";
import { getStudioCopy } from "../copy";

export interface PublishChannelCatalogItem {
  channelKey: string;
  displayName: string;
  publishChannelType?: string | null;
  credentialKind?: string | null;
  allowDraft: boolean;
  allowOnline: boolean;
}

export interface AddChannelModalProps {
  visible: boolean;
  locale: StudioLocale;
  catalogLoader: () => Promise<PublishChannelCatalogItem[]>;
  createChannel: (input: {
    name: string;
    type: string;
    supportedTargets: Array<"agent" | "app" | "workflow">;
  }) => Promise<{ channelId: string }>;
  onCreated: (result: { channelId: string; type: string; catalogItem: PublishChannelCatalogItem }) => void;
  onCancel: () => void;
}

export function AddChannelModal({
  visible,
  locale,
  catalogLoader,
  createChannel,
  onCreated,
  onCancel
}: AddChannelModalProps) {
  const copy = getStudioCopy(locale);
  const [step, setStep] = useState(0);
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [catalog, setCatalog] = useState<PublishChannelCatalogItem[]>([]);
  const [selected, setSelected] = useState<PublishChannelCatalogItem | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);

  useEffect(() => {
    if (!visible) {
      setStep(0);
      setSelected(null);
      setLoadError(null);
      return;
    }
    setLoading(true);
    setLoadError(null);
    void catalogLoader()
      .then((items) => {
        setCatalog(items.filter((item) => item.publishChannelType));
      })
      .catch((error) => {
        setCatalog([]);
        setLoadError(error instanceof Error ? error.message : copy.addChannelModal.loadFailed);
      })
      .finally(() => {
        setLoading(false);
      });
  }, [visible, catalogLoader, copy.addChannelModal.loadFailed]);

  const grouped = useMemo(() => {
    const map = new Map<string, PublishChannelCatalogItem[]>();
    for (const item of catalog) {
      const key = item.credentialKind || "builtin";
      const current = map.get(key) ?? [];
      current.push(item);
      map.set(key, current);
    }
    return Array.from(map.entries());
  }, [catalog]);

  return (
    <Modal
      title={copy.addChannelModal.title}
      visible={visible}
      footer={null}
      width={760}
      onCancel={onCancel}
      destroyOnClose
    >
      <Space vertical spacing={16} style={{ width: "100%" }}>
        <Steps current={step}>
          <Steps.Step title={copy.addChannelModal.stepChooseType} />
          <Steps.Step title={copy.addChannelModal.stepBasicInfo} />
        </Steps>

        {loadError ? <Banner type="danger" description={loadError} closeIcon={null} fullMode={false} /> : null}

        {step === 0 ? (
          loading ? (
            <Spin tip={copy.addChannelModal.loadFailed} />
          ) : catalog.length === 0 ? (
            <Empty title={copy.addChannelModal.emptyTitle} description={copy.addChannelModal.emptyHint} />
          ) : (
            <Space vertical spacing={12} style={{ width: "100%" }}>
              <Typography.Paragraph type="tertiary">{copy.addChannelModal.chooseTypeHint}</Typography.Paragraph>
              {grouped.map(([groupKey, items]) => (
                <div key={groupKey}>
                  <Typography.Title heading={6} style={{ marginBottom: 8 }}>
                    {copy.addChannelModal.credentialKindLabel}: {groupKey}
                  </Typography.Title>
                  <div style={{ display: "grid", gridTemplateColumns: "repeat(2, minmax(0, 1fr))", gap: 12 }}>
                    {items.map((item) => {
                      const isSelected = selected?.channelKey === item.channelKey;
                      return (
                        <Card
                          key={item.channelKey}
                          bordered
                          shadows={isSelected ? "hover" : "never"}
                          bodyStyle={{ cursor: "pointer" }}
                          style={isSelected ? { borderColor: "var(--semi-color-primary)" } : undefined}
                          onClick={() => setSelected(item)}
                        >
                          <Space vertical spacing={6} align="start">
                            <Typography.Text strong>{item.displayName}</Typography.Text>
                            <Space wrap>
                              <Tag>{item.channelKey}</Tag>
                              {item.publishChannelType ? <Tag color="light-blue">{item.publishChannelType}</Tag> : null}
                            </Space>
                          </Space>
                        </Card>
                      );
                    })}
                  </div>
                </div>
              ))}
              <Space style={{ justifyContent: "flex-end", width: "100%" }}>
                <Button theme="borderless" onClick={onCancel}>
                  {copy.common.cancel}
                </Button>
                <Button type="primary" disabled={!selected} onClick={() => setStep(1)}>
                  {copy.addChannelModal.next}
                </Button>
              </Space>
            </Space>
          )
        ) : selected ? (
          <Form
            layout="vertical"
            onSubmit={async (values) => {
              if (!selected.publishChannelType) {
                return;
              }
              setSubmitting(true);
              try {
                const result = await createChannel({
                  name: String(values.name ?? "").trim(),
                  type: selected.publishChannelType,
                  supportedTargets: ["agent", "app", "workflow"]
                });
                Toast.success(copy.addChannelModal.created);
                onCreated({ channelId: result.channelId, type: selected.publishChannelType, catalogItem: selected });
              } catch (error) {
                Toast.error(error instanceof Error ? error.message : copy.common.unknownError);
              } finally {
                setSubmitting(false);
              }
            }}
          >
            <Typography.Paragraph type="tertiary">{copy.addChannelModal.channelDescription}</Typography.Paragraph>
            <Form.Input
              field="name"
              label={copy.addChannelModal.channelNameLabel}
              initValue={selected.displayName}
              placeholder={copy.addChannelModal.channelNamePlaceholder}
              rules={[{ required: true, message: "required" }]}
            />
            <Space vertical spacing={6} align="start" style={{ marginBottom: 16 }}>
              <Typography.Text type="tertiary">
                {copy.addChannelModal.selectedTypeLabel}: {selected.displayName}
              </Typography.Text>
              <Typography.Text type="tertiary">
                {copy.addChannelModal.publishTypeLabel}: {selected.publishChannelType}
              </Typography.Text>
              <Space wrap>
                <Tag>{copy.addChannelModal.targetAgent}</Tag>
                <Tag>{copy.addChannelModal.targetApp}</Tag>
                <Tag>{copy.addChannelModal.targetWorkflow}</Tag>
              </Space>
            </Space>
            <Space style={{ justifyContent: "flex-end", width: "100%" }}>
              <Button onClick={() => setStep(0)}>{copy.addChannelModal.back}</Button>
              <Button type="primary" htmlType="submit" loading={submitting}>
                {copy.addChannelModal.create}
              </Button>
            </Space>
          </Form>
        ) : null}
      </Space>
    </Modal>
  );
}
