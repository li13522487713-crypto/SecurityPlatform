import { useEffect, useMemo, useState } from "react";
import { Button, Drawer, Empty, Input, Select, Space, Spin, Tag, Typography } from "@douyinfe/semi-ui";
import { IconExport, IconSearch } from "@douyinfe/semi-icons";

import type { MicroflowResourceAdapter } from "../adapter/microflow-resource-adapter";
import { MicroflowErrorState } from "../components/error";
import type { MicroflowResource } from "../resource/resource-types";
import type { MicroflowImpactLevel, MicroflowReference } from "./microflow-reference-types";
import { MicroflowReferenceImpactTag } from "./MicroflowReferenceImpactTag";
import {
  getReferenceImpactSummary,
  getReferenceKindLabel,
  getReferenceTypeLabel,
  groupReferencesBySourceType
} from "./microflow-reference-utils";

const { Text } = Typography;

export interface MicroflowReferencesDrawerProps {
  visible: boolean;
  resource?: MicroflowResource;
  adapter: MicroflowResourceAdapter;
  onClose: () => void;
}

export function MicroflowReferencesDrawer({ visible, resource, adapter, onClose }: MicroflowReferencesDrawerProps) {
  const [references, setReferences] = useState<MicroflowReference[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<unknown>();
  const [keyword, setKeyword] = useState("");
  const [impact, setImpact] = useState<"all" | MicroflowImpactLevel>("all");
  const [sourceType, setSourceType] = useState<"all" | MicroflowReference["sourceType"]>("all");
  const [includeInactive, setIncludeInactive] = useState(false);

  async function loadReferences() {
    if (!resource) {
      return;
    }
    setLoading(true);
    setError(undefined);
    adapter.getMicroflowReferences(resource.id, {
      includeInactive,
      sourceType: sourceType === "all" ? undefined : [sourceType],
      impactLevel: impact === "all" ? undefined : [impact],
    })
      .then(setReferences)
      .catch(setError)
      .finally(() => setLoading(false));
  }

  useEffect(() => {
    if (visible) {
      void loadReferences();
    }
  }, [visible, resource?.id, sourceType, impact, includeInactive]);

  const filtered = useMemo(() => {
    const normalized = keyword.trim().toLowerCase();
    return references.filter(reference => {
      const matchesKeyword = !normalized || reference.sourceName.toLowerCase().includes(normalized);
      return matchesKeyword;
    });
  }, [keyword, references]);

  const grouped = useMemo(() => groupReferencesBySourceType(filtered), [filtered]);
  const summary = useMemo(() => getReferenceImpactSummary(references), [references]);

  return (
    <Drawer visible={visible} title="引用关系" width={560} onCancel={onClose} footer={null}>
      {!resource ? (
        <Empty title="未选择微流" />
      ) : (
        <Space vertical align="start" spacing={14} style={{ width: "100%" }}>
          <Space wrap>
            <Tag color="blue">引用 {summary.total}</Tag>
            <Tag color="red">高 {summary.high}</Tag>
            <Tag color="orange">中 {summary.medium}</Tag>
            <Tag color="blue">低 {summary.low}</Tag>
            <Tag color="grey">无 {summary.none}</Tag>
          </Space>
          <Space style={{ width: "100%" }}>
            <Input prefix={<IconSearch />} showClear value={keyword} onChange={setKeyword} placeholder="搜索引用来源" style={{ flex: 1 }} />
            <Select
              value={impact}
              onChange={value => setImpact(value as "all" | MicroflowImpactLevel)}
              style={{ width: 130 }}
              optionList={[
                { value: "all", label: "全部影响" },
                { value: "high", label: "高影响" },
                { value: "medium", label: "中影响" },
                { value: "low", label: "低影响" },
                { value: "none", label: "无影响" }
              ]}
            />
            <Select
              value={sourceType}
              onChange={value => setSourceType(value as "all" | MicroflowReference["sourceType"])}
              style={{ width: 140 }}
              optionList={[
                { value: "all", label: "全部来源" },
                { value: "microflow", label: "微流" },
                { value: "workflow", label: "工作流" },
                { value: "page", label: "页面" },
                { value: "form", label: "表单" },
                { value: "button", label: "按钮" },
                { value: "schedule", label: "定时任务" },
                { value: "api", label: "API" },
                { value: "unknown", label: "未知" }
              ]}
            />
            <Button size="small" type={includeInactive ? "primary" : "tertiary"} onClick={() => setIncludeInactive(current => !current)}>
              {includeInactive ? "含 inactive" : "仅 active"}
            </Button>
          </Space>
          {loading ? (
            <Spin />
          ) : error ? (
            <MicroflowErrorState error={error} title="引用服务不可用" compact onRetry={() => void loadReferences()} />
          ) : filtered.length === 0 ? (
            <Empty title="暂无引用" description="当前筛选条件下没有引用来源。" />
          ) : (
            Object.entries(grouped).filter(([, items]) => items.length > 0).map(([sourceType, items]) => (
              <div key={sourceType} style={{ width: "100%" }}>
                <Text strong>{getReferenceTypeLabel(sourceType as MicroflowReference["sourceType"])} · {items.length}</Text>
                <Space vertical align="start" spacing={8} style={{ width: "100%", marginTop: 8 }}>
                  {items.map(reference => (
                    <div key={reference.id} style={{ width: "100%", border: "1px solid var(--semi-color-border)", borderRadius: 8, padding: 12 }}>
                      <Space vertical align="start" spacing={6} style={{ width: "100%" }}>
                        <Space wrap>
                          <Text strong>{reference.sourceName}</Text>
                          <Tag>{getReferenceKindLabel(reference.referenceKind)}</Tag>
                          <MicroflowReferenceImpactTag level={reference.impactLevel} />
                        </Space>
                        <Text type="tertiary" size="small">
                          来源版本 {reference.sourceVersion ?? "-"} · 引用版本 {reference.referencedVersion ?? "-"} · {reference.sourcePath ?? "无路径"}
                        </Text>
                        {reference.description ? <Text size="small">{reference.description}</Text> : null}
                        <Space>
                          <Button size="small" disabled={!reference.canNavigate}>打开来源</Button>
                          <Button size="small" icon={<IconExport />} disabled>导出预留</Button>
                        </Space>
                      </Space>
                    </div>
                  ))}
                </Space>
              </div>
            ))
          )}
        </Space>
      )}
    </Drawer>
  );
}
