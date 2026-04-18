import { useMemo, useState } from "react";
import {
  Banner,
  Button,
  Empty,
  Input,
  Select,
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
  type RetrievalCallerContext,
  type RetrievalProfile,
  type SupportedLocale
} from "../types";
import { getLibraryCopy } from "../copy";
import { KnowledgeResourcePicker } from "./knowledge-resource-picker";
import { RetrievalProfileFields } from "./knowledge-detail/retrieval-profile-editor";

export interface WorkflowKnowledgeNodePanelProps {
  api: LibraryKnowledgeApi;
  locale: SupportedLocale;
  /** 当前节点已经绑定的知识库 ID 列表 */
  knowledgeBaseIds: number[];
  /** 检索策略（可选，未传则使用默认 DEFAULT_RETRIEVAL_PROFILE） */
  retrievalProfile?: RetrievalProfile;
  /** 是否启用节点级 debug 输出 */
  debug?: boolean;
  /** v5 §38 / 计划 G7：MetadataFilter（key→value） */
  filters?: Record<string, string>;
  /** v5 §38 / 计划 G7：调用方上下文覆盖（合并默认 CallerContext） */
  callerContextOverride?: RetrievalCallerContext;
  onChange: (next: {
    knowledgeBaseIds: number[];
    retrievalProfile: RetrievalProfile;
    debug: boolean;
    filters?: Record<string, string>;
    callerContextOverride?: RetrievalCallerContext;
  }) => void;
}

const PRESET_OPTIONS = [
  { value: 0, label: "Assistant" },
  { value: 1, label: "WorkflowDebug" },
  { value: 2, label: "ExternalApi" },
  { value: 3, label: "System" }
];

const CALLER_TYPE_OPTIONS = [
  { value: "studio", label: "Studio" },
  { value: "agent", label: "Agent" },
  { value: "workflow", label: "Workflow" },
  { value: "app", label: "App" },
  { value: "chatflow", label: "Chatflow" }
];

interface FilterRow {
  key: string;
  value: string;
}

const filtersToRows = (filters?: Record<string, string>): FilterRow[] => {
  if (!filters) return [];
  return Object.entries(filters).map(([key, value]) => ({ key, value }));
};

const rowsToFilters = (rows: FilterRow[]): Record<string, string> | undefined => {
  const filtered = rows.filter(row => row.key.trim().length > 0);
  if (filtered.length === 0) return undefined;
  return filtered.reduce<Record<string, string>>((acc, row) => {
    acc[row.key.trim()] = row.value;
    return acc;
  }, {});
};

/**
 * 工作流知识检索节点配置面板（v5 §38 / 计划 G7 完整版）：
 * - 复用 KnowledgeResourcePicker 选择目标知识库
 * - 嵌入 RetrievalProfileFields（topK / minScore / rerank / rerankModel / hybrid / weights / queryRewrite）
 * - 新增 filters key-value editor + callerContextOverride 表单
 */
export function WorkflowKnowledgeNodePanel({
  api,
  locale,
  knowledgeBaseIds,
  retrievalProfile,
  debug,
  filters,
  callerContextOverride,
  onChange
}: WorkflowKnowledgeNodePanelProps) {
  const copy = getLibraryCopy(locale);
  const [pickerVisible, setPickerVisible] = useState(false);
  const [pickedKbs, setPickedKbs] = useState<KnowledgeBaseDto[]>([]);
  const profile = retrievalProfile ?? DEFAULT_RETRIEVAL_PROFILE;
  const isDebug = debug ?? false;
  const filterRows = useMemo(() => filtersToRows(filters), [filters]);
  const overrideValue: Partial<RetrievalCallerContext> = callerContextOverride ?? {
    callerType: "workflow",
    preset: 1
  };

  const emit = (patch: Partial<{
    knowledgeBaseIds: number[];
    retrievalProfile: RetrievalProfile;
    debug: boolean;
    filters?: Record<string, string>;
    callerContextOverride?: RetrievalCallerContext;
  }>) => {
    onChange({
      knowledgeBaseIds,
      retrievalProfile: profile,
      debug: isDebug,
      filters,
      callerContextOverride: callerContextOverride,
      ...patch
    });
  };

  function setFilterRow(idx: number, patch: Partial<FilterRow>): void {
    const next = filterRows.map((row, i) => (i === idx ? { ...row, ...patch } : row));
    emit({ filters: rowsToFilters(next) });
  }
  function addFilterRow(): void {
    emit({ filters: rowsToFilters([...filterRows, { key: "", value: "" }]) });
  }
  function removeFilterRow(idx: number): void {
    emit({ filters: rowsToFilters(filterRows.filter((_, i) => i !== idx)) });
  }
  function patchOverride(patch: Partial<RetrievalCallerContext>): void {
    const next = { ...overrideValue, ...patch } as RetrievalCallerContext;
    emit({ callerContextOverride: next });
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
                  onClose={() => emit({ knowledgeBaseIds: knowledgeBaseIds.filter(x => x !== id) })}
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
      <RetrievalProfileFields
        locale={locale}
        value={profile}
        onChange={next => emit({ retrievalProfile: next })}
      />

      <Typography.Text strong>Filters (Metadata)</Typography.Text>
      <Space vertical align="start" style={{ width: "100%" }}>
        {filterRows.map((row, idx) => (
          <Space key={idx}>
            <Input
              placeholder="key"
              value={row.key}
              onChange={value => setFilterRow(idx, { key: value })}
              style={{ width: 140 }}
            />
            <Input
              placeholder="value"
              value={row.value}
              onChange={value => setFilterRow(idx, { value })}
              style={{ width: 220 }}
            />
            <Button type="danger" theme="borderless" onClick={() => removeFilterRow(idx)}>移除</Button>
          </Space>
        ))}
        <Button onClick={addFilterRow}>+ 添加 filter</Button>
      </Space>

      <Typography.Text strong>CallerContextOverride</Typography.Text>
      <Space wrap>
        <div>
          <Typography.Text type="tertiary" size="small">callerType</Typography.Text>
          <Select
            style={{ width: 140 }}
            value={(overrideValue.callerType as string | undefined) ?? "workflow"}
            optionList={CALLER_TYPE_OPTIONS}
            onChange={value => patchOverride({ callerType: value as RetrievalCallerContext["callerType"] })}
          />
        </div>
        <div>
          <Typography.Text type="tertiary" size="small">preset</Typography.Text>
          <Select
            style={{ width: 160 }}
            value={overrideValue.preset ?? 1}
            optionList={PRESET_OPTIONS}
            onChange={value => patchOverride({ preset: value as number })}
          />
        </div>
        <div>
          <Typography.Text type="tertiary" size="small">callerId</Typography.Text>
          <Input value={overrideValue.callerId ?? ""} onChange={value => patchOverride({ callerId: value })} style={{ width: 160 }} />
        </div>
        <div>
          <Typography.Text type="tertiary" size="small">userId</Typography.Text>
          <Input value={overrideValue.userId ?? ""} onChange={value => patchOverride({ userId: value })} style={{ width: 160 }} />
        </div>
      </Space>

      <div>
        <Typography.Text type="tertiary" size="small">{copy.retrievalEnableDebug}</Typography.Text>
        <Switch checked={isDebug} onChange={value => emit({ debug: value })} />
      </div>

      <KnowledgeResourcePicker
        api={api}
        locale={locale}
        visible={pickerVisible}
        value={knowledgeBaseIds}
        multiple
        onChange={(ids, items) => {
          setPickedKbs(items);
          emit({ knowledgeBaseIds: ids });
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
