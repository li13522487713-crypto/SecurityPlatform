import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Button, Select, Space, Typography } from "@douyinfe/semi-ui";
import type { MicroflowDataType } from "../../schema";
import { searchMicroflows, useMicroflowMetadata, type MetadataMicroflowRef } from "../../metadata";

const { Text } = Typography;

function dataTypeKind(type: MicroflowDataType): string {
  return type.kind === "enumeration" ? `enumeration:${type.enumerationQualifiedName}` : type.kind === "object" ? `object:${type.entityQualifiedName}` : type.kind;
}

export function MicroflowSelector({
  value,
  onChange,
  expectedReturnType,
  currentMicroflowId,
  disabled,
  placeholder = "Select microflow",
}: {
  value?: string;
  onChange: (microflowId?: string) => void;
  expectedReturnType?: MicroflowDataType;
  currentMicroflowId?: string;
  disabled?: boolean;
  placeholder?: string;
}) {
  const { adapter, catalog, loading, error, reload, workspaceId, moduleId, version } = useMicroflowMetadata();
  const [targetMicroflows, setTargetMicroflows] = useState<MetadataMicroflowRef[]>([]);
  const [targetLoading, setTargetLoading] = useState(true);
  const [targetError, setTargetError] = useState<Error | null>(null);
  const [targetLoaded, setTargetLoaded] = useState(false);
  const targetRequestSeqRef = useRef(0);
  const mockCatalog = catalog?.version?.startsWith("mock") ?? false;
  const loadTargets = useCallback(async () => {
    const requestSeq = targetRequestSeqRef.current + 1;
    targetRequestSeqRef.current = requestSeq;
    if (!adapter?.getMicroflowRefs) {
      setTargetMicroflows([]);
      setTargetError(new Error("Microflow metadata target API is not configured."));
      setTargetLoaded(false);
      return;
    }
    setTargetLoading(true);
    setTargetError(null);
    try {
      const refs = await adapter.getMicroflowRefs({ workspaceId, moduleId, includeArchived: false });
      if (targetRequestSeqRef.current !== requestSeq) {
        return;
      }
      setTargetMicroflows(refs);
      setTargetLoaded(true);
    } catch (caught) {
      if (targetRequestSeqRef.current !== requestSeq) {
        return;
      }
      setTargetMicroflows([]);
      setTargetLoaded(false);
      setTargetError(caught instanceof Error ? caught : new Error(String(caught)));
    } finally {
      if (targetRequestSeqRef.current === requestSeq) {
        setTargetLoading(false);
      }
    }
  }, [adapter, moduleId, workspaceId]);
  useEffect(() => {
    if (!mockCatalog) {
      void loadTargets();
    }
  }, [loadTargets, mockCatalog, version]);
  const catalogMicroflows = catalog ? searchMicroflows(catalog) : [];
  const microflows = useMemo(() => {
    const source = targetLoaded ? targetMicroflows : catalogMicroflows;
    return source
      .filter(microflow => microflow.status !== "archived")
      .filter(microflow => !expectedReturnType || microflow.returnType.kind === expectedReturnType.kind);
  }, [catalogMicroflows, expectedReturnType, targetLoaded, targetMicroflows]);
  const errorMessage = error?.message ?? targetError?.message;
  if (error) {
    return (
      <Space vertical align="start" spacing={4}>
        <Text type="danger" size="small">元数据加载失败：{errorMessage}</Text>
        <Button size="small" onClick={() => void reload()}>Retry</Button>
      </Space>
    );
  }
  if (targetError) {
    return (
      <Space vertical align="start" spacing={4}>
        <Text type="danger" size="small">目标微流列表加载失败：{targetError.message}</Text>
        <Button size="small" onClick={() => void loadTargets()}>Retry</Button>
      </Space>
    );
  }
  if (!catalog) {
    if (loading) {
      return <Select style={{ width: "100%" }} disabled placeholder="元数据加载中…" />;
    }
    return <Select style={{ width: "100%" }} disabled placeholder="元数据未加载" />;
  }
  if (mockCatalog) {
    return <Select style={{ width: "100%" }} disabled placeholder="真实微流 metadata 未接入" />;
  }
  if (targetLoading) {
    return <Select style={{ width: "100%" }} disabled placeholder="目标微流加载中…" />;
  }
  return (
    <Select
      filter
      showClear
      disabled={disabled}
      value={value}
      key={version}
      style={{ width: "100%" }}
      placeholder={placeholder}
      emptyContent="No microflows available"
      optionList={microflows.map(microflow => ({
        label: `${microflow.displayName || microflow.name || microflow.qualifiedName} · ${microflow.name} · ${microflow.qualifiedName} · ${microflow.moduleName} (${microflow.parameters.length} params -> ${dataTypeKind(microflow.returnType)})`,
        value: microflow.id,
        disabled: microflow.id === currentMicroflowId,
        showTick: true,
        render: () => (
          <Space vertical spacing={0} align="start">
            <Text strong>{microflow.displayName || microflow.name || microflow.qualifiedName}</Text>
            <Text type="tertiary" size="small">{microflow.qualifiedName} · {microflow.moduleName} · {microflow.status ?? "draft"}</Text>
            {microflow.id === currentMicroflowId ? <Text type="warning" size="small">Cannot call itself directly in Stage 12.</Text> : null}
            {microflow.unavailableReason ? <Text type="warning" size="small">{microflow.unavailableReason}</Text> : null}
          </Space>
        ),
      }))}
      onChange={selected => {
        const next = selected ? String(selected) : undefined;
        if (next && next === currentMicroflowId) {
          return;
        }
        onChange(next);
      }}
    />
  );
}
