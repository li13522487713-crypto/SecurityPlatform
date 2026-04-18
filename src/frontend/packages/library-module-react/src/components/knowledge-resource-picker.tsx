import { useEffect, useMemo, useState } from "react";
import { Button, Empty, Input, Modal, Space, Spin, Table, Tag, Toast, Typography } from "@douyinfe/semi-ui";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import { IconSearch } from "@douyinfe/semi-icons";
import type {
  KnowledgeBaseDto,
  KnowledgeBaseKind,
  LibraryKnowledgeApi,
  SupportedLocale
} from "../types";
import { getLibraryCopy } from "../copy";

export interface KnowledgeResourcePickerProps {
  api: LibraryKnowledgeApi;
  locale: SupportedLocale;
  visible: boolean;
  /** 已选 KB id（受控） */
  value: number[];
  /** 是否多选；默认 true */
  multiple?: boolean;
  /** 仅展示某些 kind */
  filterKinds?: KnowledgeBaseKind[];
  onChange: (next: number[], items: KnowledgeBaseDto[]) => void;
  onCancel: () => void;
}

/**
 * 资源选择器：被工作流知识检索节点 / Agent 技能装配 / App 页面绑定复用。
 * 单测通过 KnowledgeDetailPage > Bindings Tab 间接覆盖；本身只是无状态组合。
 */
export function KnowledgeResourcePicker({
  api,
  locale,
  visible,
  value,
  multiple = true,
  filterKinds,
  onChange,
  onCancel
}: KnowledgeResourcePickerProps) {
  const copy = getLibraryCopy(locale);
  const [keyword, setKeyword] = useState("");
  const [items, setItems] = useState<KnowledgeBaseDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [selected, setSelected] = useState<number[]>(value);

  useEffect(() => {
    setSelected(value);
  }, [value]);

  useEffect(() => {
    if (!visible) {
      return;
    }
    let disposed = false;
    setLoading(true);
    api
      .listKnowledgeBases({ pageIndex: 1, pageSize: 50, keyword: keyword.trim() })
      .then(response => {
        if (disposed) return;
        const filtered = filterKinds && filterKinds.length > 0
          ? response.items.filter(item => (item.kind ? filterKinds.includes(item.kind) : false))
          : response.items;
        setItems(filtered);
      })
      .catch((error: unknown) => {
        if (!disposed) {
          Toast.error((error as Error).message ?? String(error));
        }
      })
      .finally(() => {
        if (!disposed) {
          setLoading(false);
        }
      });
    return () => {
      disposed = true;
    };
  }, [api, visible, keyword, filterKinds]);

  const columns = useMemo<ColumnProps<KnowledgeBaseDto>[]>(() => [
    {
      title: copy.knowledgeBase,
      dataIndex: "name",
      render: (_value: unknown, record: KnowledgeBaseDto) => (
        <div>
          <div style={{ fontWeight: 600 }}>{record.name}</div>
          {record.description ? (
            <Typography.Text type="tertiary" size="small">
              {record.description}
            </Typography.Text>
          ) : null}
        </div>
      )
    },
    {
      title: copy.resourceType,
      dataIndex: "kind",
      width: 120,
      render: (_value: unknown, record: KnowledgeBaseDto) => (
        <Tag color="light-blue">{record.kind ?? copy.typeLabels[record.type]}</Tag>
      )
    },
    {
      title: copy.documents,
      dataIndex: "documentCount",
      width: 90
    },
    {
      title: copy.chunks,
      dataIndex: "chunkCount",
      width: 90
    }
  ], [copy]);

  const toggleSelected = (id: number) => {
    if (multiple) {
      setSelected(current => (current.includes(id) ? current.filter(x => x !== id) : [...current, id]));
      return;
    }
    setSelected([id]);
  };

  return (
    <Modal
      title={copy.resourcePickerTitle}
      visible={visible}
      onCancel={onCancel}
      width={680}
      footer={(
        <Space>
          <Button onClick={onCancel}>{copy.cancel}</Button>
          <Button
            type="primary"
            disabled={selected.length === 0}
            onClick={() => {
              const picked = items.filter(item => selected.includes(item.id));
              onChange(selected, picked);
            }}
          >
            {copy.resourcePickerSelect}
          </Button>
        </Space>
      )}
    >
      <Space vertical align="start" style={{ width: "100%" }}>
        <Input
          prefix={<IconSearch />}
          placeholder={copy.searchPlaceholder}
          value={keyword}
          onChange={value => setKeyword(value)}
        />
        {loading ? (
          <div style={{ width: "100%", textAlign: "center", padding: "24px 0" }}>
            <Spin size="large" />
          </div>
        ) : items.length === 0 ? (
          <Empty description={copy.resourcePickerEmpty} />
        ) : (
          <Table
            rowKey="id"
            columns={columns}
            dataSource={items}
            rowSelection={{
              selectedRowKeys: selected,
              type: multiple ? "checkbox" : "radio",
              onChange: keys => setSelected((keys as number[]) ?? [])
            }}
            onRow={(record?: KnowledgeBaseDto) => ({
              onClick: () => {
                if (!record) return;
                toggleSelected(record.id);
              }
            })}
            pagination={false}
            scroll={{ y: 320 }}
          />
        )}
      </Space>
    </Modal>
  );
}
