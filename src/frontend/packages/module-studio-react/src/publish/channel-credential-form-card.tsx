import type { ReactNode } from "react";
import { useEffect, useMemo, useState } from "react";
import { Banner, Button, Card, Form, Space, Spin, Toast, Typography } from "@douyinfe/semi-ui";

export interface ChannelCredentialField<TCredential> {
  readonly field: string;
  readonly label: string;
  readonly type?: "text" | "password";
  readonly required?: boolean;
  readonly initValue?: (credential: TCredential | null) => string;
}

export interface ChannelCredentialSummaryItem<TCredential> {
  readonly key: string;
  readonly render: (credential: TCredential) => ReactNode;
}

export interface ChannelCredentialFormCardProps<TCredential> {
  readonly title: string;
  readonly hint: string;
  readonly loadingText: string;
  readonly saveSuccessText: string;
  readonly clearSuccessText: string;
  readonly saveButtonText: string;
  readonly clearButtonText: string;
  readonly baseUrl: string;
  readonly fetcher: <T = unknown>(input: { url: string; method: string; body?: unknown }) => Promise<T>;
  readonly fields: ReadonlyArray<ChannelCredentialField<TCredential>>;
  readonly summaries?: ReadonlyArray<ChannelCredentialSummaryItem<TCredential>>;
  readonly buildSubmitBody: (values: Record<string, unknown>) => unknown;
  readonly webhookUrl?: string;
  readonly webhookHint?: string;
  readonly testId?: string;
}

export function ChannelCredentialFormCard<TCredential>({
  title,
  hint,
  loadingText,
  saveSuccessText,
  clearSuccessText,
  saveButtonText,
  clearButtonText,
  baseUrl,
  fetcher,
  fields,
  summaries = [],
  buildSubmitBody,
  webhookUrl,
  webhookHint,
  testId = "studio-publish-channel-credential"
}: ChannelCredentialFormCardProps<TCredential>) {
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [credential, setCredential] = useState<TCredential | null>(null);
  const [error, setError] = useState<string | null>(null);

  const normalizedFields = useMemo(
    () =>
      fields.map((field) => ({
        ...field,
        type: field.type ?? "text",
        required: field.required ?? false
      })),
    [fields]
  );

  async function load() {
    setLoading(true);
    setError(null);
    try {
      const dto = await fetcher<TCredential | null>({ url: baseUrl, method: "GET" });
      setCredential(dto ?? null);
    } catch (e) {
      setError(e instanceof Error ? e.message : "load failed");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void load();
  }, [baseUrl]);

  async function handleSubmit(values: Record<string, unknown>) {
    setSubmitting(true);
    setError(null);
    try {
      const dto = await fetcher<TCredential>({
        url: baseUrl,
        method: "PUT",
        body: buildSubmitBody(values)
      });
      setCredential(dto);
      Toast.success(saveSuccessText);
    } catch (e) {
      const msg = e instanceof Error ? e.message : "save failed";
      setError(msg);
      Toast.error(msg);
    } finally {
      setSubmitting(false);
    }
  }

  async function handleDelete() {
    setSubmitting(true);
    setError(null);
    try {
      await fetcher({ url: baseUrl, method: "DELETE" });
      setCredential(null);
      Toast.success(clearSuccessText);
    } catch (e) {
      const msg = e instanceof Error ? e.message : "delete failed";
      setError(msg);
      Toast.error(msg);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Card data-testid={testId} title={title} bordered>
      <Typography.Paragraph type="tertiary">{hint}</Typography.Paragraph>
      {error ? <Banner type="danger" description={error} closeIcon={null} fullMode={false} /> : null}
      {loading ? (
        <Spin size="middle" tip={loadingText} />
      ) : (
        <>
          {credential && summaries.length > 0 ? (
            <Space vertical align="start" spacing={6} style={{ marginBottom: 12, width: "100%" }}>
              {summaries.map((item) => (
                <div key={item.key}>{item.render(credential)}</div>
              ))}
            </Space>
          ) : null}

          {webhookUrl && webhookHint ? (
            <Banner
              type="info"
              fullMode={false}
              description={
                <Typography.Text size="small">
                  {webhookHint}
                  <code>{webhookUrl}</code>
                </Typography.Text>
              }
              closeIcon={null}
            />
          ) : null}

          <Form
            key={`${baseUrl}:${credential ? "loaded" : "empty"}:${credential ? JSON.stringify(credential) : ""}`}
            layout="vertical"
            onSubmit={handleSubmit}
          >
            {normalizedFields.map((field) => (
              <Form.Input
                key={field.field}
                field={field.field}
                label={field.label}
                type={field.type}
                initValue={field.initValue ? field.initValue(credential) : ""}
                rules={field.required ? [{ required: true, message: "required" }] : undefined}
              />
            ))}
            <Space>
              <Button type="primary" htmlType="submit" loading={submitting}>
                {saveButtonText}
              </Button>
              {credential ? (
                <Button type="danger" loading={submitting} onClick={handleDelete}>
                  {clearButtonText}
                </Button>
              ) : null}
            </Space>
          </Form>
        </>
      )}
    </Card>
  );
}
