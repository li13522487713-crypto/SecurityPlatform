import { useEffect, useMemo, useRef, useState } from "react";
import { Banner, Button, Drawer, Empty, Input, Select, Space, Spin, Tag, Typography } from "@douyinfe/semi-ui";
import { IconRefresh, IconSearch } from "@douyinfe/semi-icons";
import type { MicroflowAuthoringSchema } from "@atlas/microflow";

import type { MicroflowResourceAdapter } from "../adapter/microflow-resource-adapter";
import { MicroflowErrorState } from "../components/error";
import type { StudioMicroflowDefinitionView } from "../studio/studio-microflow-types";
import type { MicroflowImpactLevel, MicroflowReference } from "./microflow-reference-types";
import { MicroflowReferenceImpactTag } from "./MicroflowReferenceImpactTag";
import {
  buildStaleCallReferenceWarnings,
  getReferenceImpactSummary,
  getReferenceKindLabel,
  getReferenceTypeLabel,
  groupReferencesBySourceType,
  isMicroflowReferenced,
  parseMicroflowCallees,
  resolveReferenceDisplayName
} from "./microflow-reference-utils";

const { Text } = Typography;

export interface MicroflowReferencesResource {
  id: string;
  name: string;
  displayName?: string;
  qualifiedName?: string;
  moduleId?: string;
  moduleName?: string;
  referenceCount?: number;
  schema?: MicroflowAuthoringSchema;
}

export interface MicroflowReferencesDrawerProps {
  visible: boolean;
  resource?: MicroflowReferencesResource;
  adapter: MicroflowResourceAdapter;
  resourceIndex?: Record<string, StudioMicroflowDefinitionView>;
  getCurrentSchema?: () => MicroflowAuthoringSchema | undefined;
  onOpenMicroflow?: (microflowId: string) => void;
  onRefreshResourceList?: () => void | Promise<void>;
  onClose: () => void;
}

export function MicroflowReferencesDrawer({
  visible,
  resource,
  adapter,
  resourceIndex,
  getCurrentSchema,
  onOpenMicroflow,
  onRefreshResourceList,
  onClose
}: MicroflowReferencesDrawerProps) {
  const requestSeqRef = useRef(0);
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
    const requestSeq = requestSeqRef.current + 1;
    requestSeqRef.current = requestSeq;
    setReferences([]);
    setLoading(true);
    setError(undefined);
    try {
      const nextReferences = await adapter.getMicroflowReferences(resource.id, {
        includeInactive,
        sourceType: sourceType === "all" ? undefined : [sourceType],
        impactLevel: impact === "all" ? undefined : [impact],
      });
      if (requestSeqRef.current === requestSeq) {
        setReferences(nextReferences);
      }
    } catch (caught) {
      if (requestSeqRef.current === requestSeq) {
        setError(caught);
      }
    } finally {
      if (requestSeqRef.current === requestSeq) {
        setLoading(false);
      }
    }
  }

  async function refreshAll() {
    await loadReferences();
    await onRefreshResourceList?.();
  }

  useEffect(() => {
    if (visible) {
      void loadReferences();
    }
  }, [visible, resource?.id, sourceType, impact, includeInactive]);

  const filtered = useMemo(() => {
    const normalized = keyword.trim().toLowerCase();
    return references.filter(reference => {
      const displayName = resolveReferenceDisplayName(reference, resourceIndex);
      const matchesKeyword = !normalized || displayName.toLowerCase().includes(normalized);
      return matchesKeyword;
    });
  }, [keyword, references, resourceIndex]);

  const grouped = useMemo(() => groupReferencesBySourceType(filtered), [filtered]);
  const summary = useMemo(() => getReferenceImpactSummary(references), [references]);
  const currentSchema = getCurrentSchema?.() ?? resource?.schema;
  const callees = useMemo(
    () => parseMicroflowCallees(currentSchema, resource?.id ?? "", resourceIndex),
    [currentSchema, resource?.id, resourceIndex]
  );
  const calleeWarnings = useMemo(() => buildStaleCallReferenceWarnings(callees), [callees]);
  const displayName = resource ? (resource.displayName || resource.name) : "";
  const qualifiedName = resource
    ? resource.qualifiedName ?? `${resource.moduleName || resource.moduleId || "-"}.${resource.name}`
    : "";
  const activeCallerCount = references.filter(reference => reference.active !== false).length;

  return (
    <Drawer visible={visible} title="引用关系" width={680} onCancel={onClose} footer={null}>
      {!resource ? (
        <Empty title="未选择微流" />
      ) : (
        <Space vertical align="start" spacing={14} style={{ width: "100%" }}>
          <Space vertical align="start" spacing={4} style={{ width: "100%" }}>
            <Text strong>{displayName}</Text>
            <Text type="tertiary" size="small">{qualifiedName}</Text>
            <Space wrap>
              <Tag color={isMicroflowReferenced(references) ? "red" : "grey"}>Callers {summary.total}</Tag>
              {typeof resource.referenceCount === "number" ? <Tag>referenceCount {resource.referenceCount}</Tag> : null}
              <Tag color={callees.length > 0 ? "blue" : "grey"}>Callees {callees.length}</Tag>
              <Button size="small" icon={<IconRefresh />} onClick={() => void refreshAll()}>Refresh</Button>
            </Space>
          </Space>
          <Banner
            type={activeCallerCount > 0 ? "danger" : "info"}
            fullMode={false}
            description={
              activeCallerCount > 0
                ? "该微流存在 active callers，删除会被阻止；最终以后端删除保护为准。"
                : "Callers 来自已保存的后端引用索引；Callees 从当前编辑器 schema 或已加载 schema 解析。"
            }
          />
          <Space wrap>
            <Tag color="blue">Callers {summary.total}</Tag>
            <Tag color="red">高 {summary.high}</Tag>
            <Tag color="orange">中 {summary.medium}</Tag>
            <Tag color="blue">低 {summary.low}</Tag>
            <Tag color="grey">无 {summary.none}</Tag>
          </Space>
          <Text strong>Callers · 谁引用当前微流</Text>
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
            <Space><Spin /><Text type="tertiary">Loading references...</Text></Space>
          ) : error ? (
            <MicroflowErrorState error={error} title="引用服务不可用" compact onRetry={() => void loadReferences()} />
          ) : filtered.length === 0 ? (
            <Empty title="No callers" description="当前筛选条件下没有引用当前微流的对象。" />
          ) : (
            Object.entries(grouped).filter(([, items]) => items.length > 0).map(([sourceType, items]) => (
              <div key={sourceType} style={{ width: "100%" }}>
                <Text strong>{getReferenceTypeLabel(sourceType as MicroflowReference["sourceType"])} · {items.length}</Text>
                <Space vertical align="start" spacing={8} style={{ width: "100%", marginTop: 8 }}>
                  {items.map(reference => (
                    <div key={reference.id} style={{ width: "100%", border: "1px solid var(--semi-color-border)", borderRadius: 8, padding: 12 }}>
                      <Space vertical align="start" spacing={6} style={{ width: "100%" }}>
                        <Space wrap>
                          <Text strong>{resolveReferenceDisplayName(reference, resourceIndex)}</Text>
                          <Tag>{getReferenceKindLabel(reference.referenceKind)}</Tag>
                          <MicroflowReferenceImpactTag level={reference.impactLevel} />
                          {reference.active === false ? <Tag color="grey">inactive / stale</Tag> : <Tag color="green">active</Tag>}
                        </Space>
                        <Text type="tertiary" size="small">
                          {reference.sourceType} · {reference.sourceId ?? "-"} · 来源版本 {reference.sourceVersion ?? "-"} · 引用版本 {reference.referencedVersion ?? "-"} · {reference.sourcePath ?? "无路径"}
                        </Text>
                        {reference.description ? <Text size="small">{reference.description}</Text> : null}
                        <Space>
                          <Button
                            size="small"
                            disabled={reference.sourceType !== "microflow" || !reference.sourceId || !reference.canNavigate}
                            onClick={() => reference.sourceId ? onOpenMicroflow?.(reference.sourceId) : undefined}
                          >
                            打开来源微流
                          </Button>
                        </Space>
                      </Space>
                    </div>
                  ))}
                </Space>
              </div>
            ))
          )}
          <Text strong>Callees · 当前微流调用了谁</Text>
          {calleeWarnings.length > 0 ? (
            <Banner type="warning" fullMode={false} description={calleeWarnings.join("；")} />
          ) : null}
          {callees.length === 0 ? (
            <Empty title="No callees" description="当前 schema 中没有 Call Microflow 动作。" />
          ) : (
            <Space vertical align="start" spacing={8} style={{ width: "100%" }}>
              {callees.map(callee => (
                <div key={`${callee.sourceMicroflowId}:${callee.sourceNodeId}`} style={{ width: "100%", border: "1px solid var(--semi-color-border)", borderRadius: 8, padding: 12 }}>
                  <Space vertical align="start" spacing={6} style={{ width: "100%" }}>
                    <Space wrap>
                      <Text strong>{callee.targetMicroflowName || callee.targetMicroflowQualifiedName || callee.targetMicroflowId || "Incomplete Call Microflow"}</Text>
                      <Tag>{getReferenceKindLabel(callee.referenceKind)}</Tag>
                      {callee.stale ? <Tag color={callee.staleReason === "selfCall" ? "red" : "orange"}>{callee.staleReason}</Tag> : <Tag color="green">resolved</Tag>}
                    </Space>
                    <Text type="tertiary" size="small">
                      Node {callee.sourceNodeName || callee.sourceNodeId} · targetId {callee.targetMicroflowId ?? "-"} · {callee.targetMicroflowQualifiedName ?? "无 qualifiedName"}
                    </Text>
                    <Button
                      size="small"
                      disabled={!callee.targetMicroflowId || !resourceIndex?.[callee.targetMicroflowId]}
                      onClick={() => callee.targetMicroflowId ? onOpenMicroflow?.(callee.targetMicroflowId) : undefined}
                    >
                      打开目标微流
                    </Button>
                  </Space>
                </div>
              ))}
            </Space>
          )}
        </Space>
      )}
    </Drawer>
  );
}
