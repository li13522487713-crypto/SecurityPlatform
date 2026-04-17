import { useEffect, useState } from "react";
import { Button, Empty, Form, Input, Modal, SideSheet, Spin, Tag, Toast, Typography } from "@douyinfe/semi-ui";
import { IconPlus } from "@douyinfe/semi-icons";
import { useAppI18n } from "../i18n";
import {
  createTestset,
  listTestsets,
  type TestsetItem
} from "../../services/mock";

interface TestsetDrawerProps {
  visible: boolean;
  workspaceId: string;
  workflowId: string;
  /**
   * 当前工作流开始节点的输入变量定义。
   * 用于动态生成"节点数据"录入表单。
   * 第二批仅传 [{ key, type:"string" }] 占位即可。
   */
  startNodeFields?: Array<{ key: string; type: "string" | "number" | "boolean" | "object" }>;
  onClose: () => void;
}

interface CreateFormValues {
  name: string;
  description?: string;
}

/**
 * 测试集抽屉（PRD 05-4.8）。
 *
 * 列表展示当前工作流的已存测试集（mock listTestsets），
 * 新建按钮打开模态，按 startNodeFields 动态生成"节点数据"录入表单，
 * 对接 mock createTestset。
 */
export function TestsetDrawer({
  visible,
  workspaceId,
  workflowId,
  startNodeFields,
  onClose
}: TestsetDrawerProps) {
  const { t } = useAppI18n();
  const [items, setItems] = useState<TestsetItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [createOpen, setCreateOpen] = useState(false);

  const refresh = () => {
    setLoading(true);
    listTestsets(workspaceId, { pageIndex: 1, pageSize: 50 })
      .then(result => {
        setItems(result.items.filter(item => !item.workflowId || item.workflowId === workflowId));
      })
      .catch(() => setItems([]))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    if (visible) {
      refresh();
    }
  }, [visible, workflowId, workspaceId]);

  return (
    <SideSheet
      title={t("cozeTestsetDrawerTitle")}
      visible={visible}
      onCancel={onClose}
      width={420}
      mask={false}
      headerStyle={{ display: "flex", alignItems: "center" }}
      data-testid="coze-testset-drawer"
    >
      <Typography.Text type="tertiary" size="small">{t("cozeTestsetDrawerSubtitle")}</Typography.Text>

      <div style={{ display: "flex", justifyContent: "flex-end", margin: "12px 0" }}>
        <Button icon={<IconPlus />} theme="solid" type="primary" onClick={() => setCreateOpen(true)} data-testid="coze-testset-drawer-create">
          {t("cozeTestsetCreate")}
        </Button>
      </div>

      {loading ? (
        <div className="coze-page__loading"><Spin /></div>
      ) : items.length === 0 ? (
        <Empty description={t("cozeTestsetEmpty")} />
      ) : (
        <ul className="coze-list">
          {items.map(item => (
            <li key={item.id} className="coze-list__item">
              <div>
                <strong>{item.name}</strong>
                <span>{item.description ?? ""}</span>
              </div>
              <Tag size="small" color="blue">{item.rowCount}</Tag>
            </li>
          ))}
        </ul>
      )}

      <CreateTestsetModal
        visible={createOpen}
        workspaceId={workspaceId}
        workflowId={workflowId}
        startNodeFields={startNodeFields}
        onClose={() => setCreateOpen(false)}
        onCreated={() => {
          setCreateOpen(false);
          refresh();
        }}
      />
    </SideSheet>
  );
}

interface CreateTestsetModalProps {
  visible: boolean;
  workspaceId: string;
  workflowId: string;
  startNodeFields?: Array<{ key: string; type: "string" | "number" | "boolean" | "object" }>;
  onClose: () => void;
  onCreated: () => void;
}

function CreateTestsetModal({
  visible,
  workspaceId,
  workflowId,
  startNodeFields,
  onClose,
  onCreated
}: CreateTestsetModalProps) {
  const { t } = useAppI18n();
  const fields = startNodeFields ?? [{ key: "input", type: "string" as const }];
  const [submitting, setSubmitting] = useState(false);
  const [values, setValues] = useState<CreateFormValues>({ name: "", description: "" });
  const [rowValues, setRowValues] = useState<Record<string, string>>({});

  const handleSubmit = async () => {
    const name = values.name.trim();
    if (!name) {
      Toast.warning(t("cozeTestsetNamePlaceholder"));
      return;
    }
    setSubmitting(true);
    try {
      await createTestset(workspaceId, {
        name,
        description: values.description?.trim() || undefined,
        workflowId,
        rows: [castRowToObject(fields, rowValues)]
      });
      Toast.success(t("cozeCreateSuccess"));
      setValues({ name: "", description: "" });
      setRowValues({});
      onCreated();
    } catch (error) {
      Toast.error((error as Error).message || t("cozeCreateFailed"));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Modal
      title={t("cozeTestsetCreateTitle")}
      visible={visible}
      onCancel={() => {
        if (!submitting) {
          onClose();
        }
      }}
      onOk={() => void handleSubmit()}
      okText={t("cozeTestsetSubmit")}
      confirmLoading={submitting}
      maskClosable={!submitting}
      width={560}
      data-testid="coze-create-testset-modal"
    >
      <Form
        labelPosition="top"
        labelWidth="100%"
        initValues={values}
        onValueChange={next => setValues(next as CreateFormValues)}
      >
        <Form.Input
          field="name"
          label={t("cozeTestsetNameLabel")}
          placeholder={t("cozeTestsetNamePlaceholder")}
          maxLength={50}
          showClear
          required
        />
        <Form.TextArea
          field="description"
          label={t("cozeTestsetDescLabel")}
          placeholder={t("cozeTestsetDescPlaceholder")}
          maxLength={200}
          rows={3}
        />
      </Form>

      <Typography.Title heading={6} style={{ marginTop: 12 }}>
        {t("cozeTestsetNodeDataLabel")}
      </Typography.Title>
      <Typography.Text type="tertiary" size="small">{t("cozeTestsetNodeDataHint")}</Typography.Text>

      <div className="coze-testset-fields">
        {fields.map(field => (
          <div key={field.key} className="coze-testset-field">
            <label>
              <span className="coze-testset-field__name">{field.key}</span>
              <Tag size="small" color="grey">{field.type}</Tag>
            </label>
            <Input
              value={rowValues[field.key] ?? ""}
              onChange={value => setRowValues(current => ({ ...current, [field.key]: value }))}
              placeholder={`请输入 ${field.key}`}
            />
          </div>
        ))}
      </div>
    </Modal>
  );
}

function castRowToObject(
  fields: Array<{ key: string; type: "string" | "number" | "boolean" | "object" }>,
  rowValues: Record<string, string>
): Record<string, unknown> {
  const result: Record<string, unknown> = {};
  for (const field of fields) {
    const raw = rowValues[field.key] ?? "";
    if (field.type === "number") {
      const value = Number(raw);
      result[field.key] = Number.isFinite(value) ? value : 0;
    } else if (field.type === "boolean") {
      result[field.key] = raw === "true";
    } else if (field.type === "object") {
      try {
        result[field.key] = raw ? JSON.parse(raw) : {};
      } catch {
        result[field.key] = {};
      }
    } else {
      result[field.key] = raw;
    }
  }
  return result;
}
