import { useEffect, useMemo, useState } from "react";
import {
  Banner,
  Button,
  Empty,
  Input,
  Modal,
  Select,
  Space,
  Table,
  Tag,
  Toast,
  Typography
} from "@douyinfe/semi-ui";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import { IconDelete, IconPlus } from "@douyinfe/semi-icons";
import type {
  KnowledgeBaseDto,
  KnowledgeBinding,
  KnowledgeBindingCallerType,
  LibraryKnowledgeApi,
  SupportedLocale
} from "../../types";
import { getLibraryCopy } from "../../copy";
import { formatDateTime } from "../../utils";

export interface BindingsTabProps {
  api: LibraryKnowledgeApi;
  locale: SupportedLocale;
  knowledge: KnowledgeBaseDto;
}

const CALLER_COLOR: Record<KnowledgeBindingCallerType, "blue" | "violet" | "cyan" | "purple"> = {
  agent: "blue",
  app: "violet",
  workflow: "cyan",
  chatflow: "purple"
};

export function BindingsTab({ api, locale, knowledge }: BindingsTabProps) {
  const copy = getLibraryCopy(locale);
  const [items, setItems] = useState<KnowledgeBinding[]>([]);
  const [loading, setLoading] = useState(false);
  const [createVisible, setCreateVisible] = useState(false);
  const [callerType, setCallerType] = useState<KnowledgeBindingCallerType>("agent");
  const [callerId, setCallerId] = useState("");
  const [callerName, setCallerName] = useState("");

  async function refresh() {
    if (!api.listBindings) return;
    setLoading(true);
    try {
      const response = await api.listBindings(knowledge.id, { pageIndex: 1, pageSize: 50 });
      setItems(response.items);
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void refresh();
    return undefined;
  }, [knowledge.id]);

  const callerOptions = useMemo(() => ([
    { label: copy.bindingsCallerAgent, value: "agent" },
    { label: copy.bindingsCallerApp, value: "app" },
    { label: copy.bindingsCallerWorkflow, value: "workflow" },
    { label: copy.bindingsCallerChatflow, value: "chatflow" }
  ]), [copy]);

  const columns = useMemo<ColumnProps<KnowledgeBinding>[]>(() => [
    {
      title: copy.bindingsCallerType,
      dataIndex: "callerType",
      width: 140,
      render: (value: unknown) => {
        const type = value as KnowledgeBindingCallerType;
        const labelMap: Record<KnowledgeBindingCallerType, string> = {
          agent: copy.bindingsCallerAgent,
          app: copy.bindingsCallerApp,
          workflow: copy.bindingsCallerWorkflow,
          chatflow: copy.bindingsCallerChatflow
        };
        return <Tag color={CALLER_COLOR[type]}>{labelMap[type]}</Tag>;
      }
    },
    { title: copy.bindingsCallerName, dataIndex: "callerName" },
    { title: copy.bindingsCallerId, dataIndex: "callerId", width: 220 },
    {
      title: copy.updatedAt,
      dataIndex: "updatedAt",
      width: 200,
      render: (value: unknown) => formatDateTime(typeof value === "string" ? value : undefined)
    },
    {
      title: copy.actions,
      width: 150,
      render: (_value: unknown, record: KnowledgeBinding) => (
        <Button
          theme="borderless"
          type="danger"
          icon={<IconDelete />}
          onClick={async () => {
            if (!api.removeBinding) return;
            try {
              await api.removeBinding(knowledge.id, record.id);
              Toast.success(copy.bindingsActionRemove);
              await refresh();
            } catch (error) {
              Toast.error((error as Error).message);
            }
          }}
        >
          {copy.bindingsActionRemove}
        </Button>
      )
    }
  ], [api, copy, knowledge.id]);

  async function handleCreate() {
    if (!api.createBinding) return;
    if (!callerId.trim() || !callerName.trim()) {
      Toast.warning(copy.bindingsCallerName);
      return;
    }
    try {
      await api.createBinding(knowledge.id, {
        callerType,
        callerId: callerId.trim(),
        callerName: callerName.trim()
      });
      setCreateVisible(false);
      setCallerId("");
      setCallerName("");
      await refresh();
    } catch (error) {
      Toast.error((error as Error).message);
    }
  }

  return (
    <div className="atlas-table-card semi-card semi-card-bordered semi-card-shadow">
      <div className="semi-card-header">
        <div className="semi-card-header-wrapper">
          <div>
            <Typography.Title heading={5} style={{ margin: 0 }}>{copy.bindingsTitle}</Typography.Title>
            <Typography.Text type="tertiary">{copy.bindingsSubtitle}</Typography.Text>
          </div>
          <Button type="primary" icon={<IconPlus />} onClick={() => setCreateVisible(true)}>
            {copy.bindingsAddTitle}
          </Button>
        </div>
      </div>
      <div className="semi-card-body" style={{ padding: 0 }}>
        {items.length === 0 ? (
          <div style={{ padding: 32 }}>
            <Empty description={copy.bindingsEmpty} />
          </div>
        ) : (
          <Table rowKey="id" loading={loading} columns={columns} dataSource={items} pagination={false} />
        )}
      </div>

      <Modal
        title={copy.bindingsAddTitle}
        visible={createVisible}
        onOk={handleCreate}
        onCancel={() => setCreateVisible(false)}
        okText={copy.create}
        cancelText={copy.cancel}
      >
        <Space vertical align="start" style={{ width: "100%" }}>
          <Banner type="info" description={copy.bindingsSubtitle} />
          <Typography.Text strong>{copy.bindingsCallerType}</Typography.Text>
          <Select
            value={callerType}
            style={{ width: "100%" }}
            optionList={callerOptions}
            onChange={value => setCallerType(value as KnowledgeBindingCallerType)}
          />
          <Typography.Text strong>{copy.bindingsCallerName}</Typography.Text>
          <Input value={callerName} onChange={value => setCallerName(value)} />
          <Typography.Text strong>{copy.bindingsCallerId}</Typography.Text>
          <Input value={callerId} onChange={value => setCallerId(value)} />
        </Space>
      </Modal>
    </div>
  );
}
