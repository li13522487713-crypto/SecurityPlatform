import { Select, Typography } from "@douyinfe/semi-ui";
import { useMemo, useState } from "react";
import { EMPTY_MICROFLOW_METADATA_CATALOG, type MicroflowMetadataCatalog, useMicroflowMetadata } from "../../metadata";
import type { MicroflowAuthoringSchema, MicroflowDataType, MicroflowVariableIndex, MicroflowVariableSymbol } from "../../schema";
import type { ContextVariableCandidate } from "../../inline-edit/shared/ContextVariablePicker";
import {
  buildVariableIndex,
  getAvailableVariablesAtField,
  getVariableSymbols,
  resolveVariableReferenceFromIndex,
} from "../../variables";
import { VariableOptionLabel } from "./VariableOptionLabel";

const { Text } = Typography;
const RECENT_VARIABLES_STORAGE_KEY = "atlas.microflow.inline.recentVariables";

function readRecentVariables(): string[] {
  try {
    const raw = window.localStorage.getItem(RECENT_VARIABLES_STORAGE_KEY);
    if (!raw) {
      return [];
    }
    const parsed = JSON.parse(raw) as unknown;
    return Array.isArray(parsed) ? parsed.filter((item): item is string => typeof item === "string") : [];
  } catch {
    return [];
  }
}

function writeRecentVariable(name?: string) {
  if (!name) {
    return;
  }
  try {
    const prev = readRecentVariables().filter(item => item !== name);
    const next = [name, ...prev].slice(0, 8);
    window.localStorage.setItem(RECENT_VARIABLES_STORAGE_KEY, JSON.stringify(next));
  } catch {
    // no-op
  }
}

export function VariableSelector({
  schema,
  objectId,
  actionId,
  fieldPath = "",
  collectionId,
  value,
  onChange,
  metadata,
  variableIndex,
  allowedTypes,
  allowedTypeKinds,
  includeMaybe = true,
  includeUnavailable = false,
  includeSystem = true,
  includeErrorContext = true,
  includeReadonly = true,
  readonlyOnly = false,
  writableOnly = false,
  variableFilter,
  readonly = false,
  disabled,
  placeholder = "Select variable",
  scopeMode = "available",
  emptyMessage = "No variables available. Add a Parameter or Create Variable first.",
  inlineCandidates,
}: {
  schema: MicroflowAuthoringSchema;
  objectId?: string;
  actionId?: string;
  fieldPath?: string;
  collectionId?: string;
  value?: string;
  onChange: (variableName?: string) => void;
  metadata?: MicroflowMetadataCatalog;
  variableIndex?: MicroflowVariableIndex;
  allowedTypes?: MicroflowDataType[];
  allowedTypeKinds?: MicroflowDataType["kind"][];
  includeMaybe?: boolean;
  includeUnavailable?: boolean;
  includeSystem?: boolean;
  includeErrorContext?: boolean;
  includeReadonly?: boolean;
  readonlyOnly?: boolean;
  writableOnly?: boolean;
  variableFilter?: (symbol: MicroflowVariableSymbol) => boolean;
  readonly?: boolean;
  disabled?: boolean;
  placeholder?: string;
  scopeMode?: "available" | "index";
  emptyMessage?: string;
  inlineCandidates?: ContextVariableCandidate[];
}) {
  const { catalog, loading, error, version } = useMicroflowMetadata();
  const effectiveCatalog = metadata ?? catalog ?? EMPTY_MICROFLOW_METADATA_CATALOG;
  const resolvedVariableIndex = useMemo(
    () => variableIndex ?? buildVariableIndex(schema, effectiveCatalog),
    [effectiveCatalog, schema, variableIndex, version],
  );
  const disabledState = Boolean(readonly || disabled);
  const variables = useMemo(() => {
    if (inlineCandidates?.length) {
      return inlineCandidates.map(candidate => ({
        name: candidate.name.startsWith("$") ? candidate.name.slice(1) : candidate.name,
        dataType: { kind: candidate.type ?? "unknown" } as MicroflowDataType,
        kind: candidate.source as MicroflowVariableSymbol["kind"],
        readonly: Boolean(candidate.readonly),
      } as MicroflowVariableSymbol));
    }
    const sourceVariables = scopeMode === "index"
      ? getVariableSymbols(resolvedVariableIndex)
      : objectId
        ? getAvailableVariablesAtField(schema, resolvedVariableIndex, objectId, fieldPath, {
      allowedTypeKinds,
      allowedTypes,
      collectionId,
      includeErrorContext,
      includeMaybe,
      includeUnavailable,
      includeSystem,
      readonlyOnly,
      writableOnly: writableOnly || !includeReadonly,
        })
        : [];
    return sourceVariables
      .filter(symbol => includeUnavailable ? true : symbol.visibility !== "unavailable")
      .filter(symbol => includeSystem ? true : symbol.kind !== "system")
      .filter(symbol => includeErrorContext ? true : symbol.kind !== "errorContext" && symbol.kind !== "restResponse" && symbol.kind !== "soapFault")
      .filter(symbol => includeReadonly ? true : !symbol.readonly)
      .filter(symbol => readonlyOnly ? symbol.readonly : true)
      .filter(symbol => writableOnly ? !symbol.readonly : true)
      .filter(symbol => !allowedTypeKinds?.length || allowedTypeKinds.includes(symbol.dataType.kind))
      .filter(symbol => !allowedTypes?.length || allowedTypes.some(type => type.kind === symbol.dataType.kind))
      .filter(symbol => variableFilter ? variableFilter(symbol) : true);
  }, [allowedTypeKinds, allowedTypes, collectionId, fieldPath, includeErrorContext, includeMaybe, includeReadonly, includeSystem, includeUnavailable, inlineCandidates, objectId, readonlyOnly, resolvedVariableIndex, schema, scopeMode, variableFilter, writableOnly]);
  const [searchText, setSearchText] = useState("");
  const recentVariables = useMemo(() => readRecentVariables(), [value, variables.length]);
  const orderedVariables = useMemo(() => {
    if (!variables.length) {
      return variables;
    }
    const rank = new Map(recentVariables.map((name, index) => [name, index]));
    return [...variables].sort((a, b) => {
      const ia = rank.get(a.name);
      const ib = rank.get(b.name);
      if (ia !== undefined && ib !== undefined) {
        return ia - ib;
      }
      if (ia !== undefined) {
        return -1;
      }
      if (ib !== undefined) {
        return 1;
      }
      return a.name.localeCompare(b.name);
    });
  }, [recentVariables, variables]);

  // Group variables by scope kind for clearer attribution
  const groupedOptions = useMemo(() => {
    const filtered = searchText
      ? orderedVariables.filter(s => s.name.toLowerCase().includes(searchText.toLowerCase()))
      : orderedVariables;
    const groups = new Map<string, typeof filtered>();
    for (const symbol of filtered) {
      const groupKey = symbol.scope.kind ?? "other";
      if (!groups.has(groupKey)) {
        groups.set(groupKey, []);
      }
      groups.get(groupKey)!.push(symbol);
    }
    const groupLabel: Record<string, string> = {
      global: "全局变量",
      downstream: "上游节点输出",
      branch: "分支变量",
      loop: "循环变量",
      errorHandler: "异常处理变量",
      system: "系统变量",
    };
    return [...groups.entries()].map(([key, symbols]) => ({
      label: groupLabel[key] ?? key,
      children: symbols.map(symbol => ({
        value: symbol.name,
        label: symbol.name,
        showTick: true,
        render: () => <VariableOptionLabel symbol={symbol} />,
      })),
    }));
  }, [orderedVariables, searchText]);

  const current = value
      ? scopeMode === "index"
        ? variables.find(symbol => symbol.name === value)
      : objectId
        ? resolveVariableReferenceFromIndex(schema, resolvedVariableIndex, { objectId, actionId, fieldPath, collectionId }, value)
        : null
    : null;
  const currentVisible = !value || Boolean(current);
  if (error) {
    return <Text type="danger" size="small">元数据加载失败：{error.message}</Text>;
  }
  if (loading && !catalog) {
    return <Select style={{ width: "100%" }} disabled placeholder="元数据加载中…" />;
  }
  return (
    <div style={{ display: "grid", gap: 4, width: "100%" }}>
      <Select
        value={value}
        disabled={disabledState || !objectId}
        placeholder={placeholder}
        filter
        showClear
        style={{ width: "100%" }}
        onSearch={setSearchText}
        onClear={() => { setSearchText(""); onChange(undefined); }}
        onChange={nextValue => {
          const resolved = nextValue ? String(nextValue) : undefined;
          setSearchText("");
          writeRecentVariable(resolved);
          onChange(resolved);
        }}
        optionList={groupedOptions}
      />
      {variables.length === 0 ? <Text size="small" type="tertiary">{emptyMessage}</Text> : null}
      {!currentVisible ? <Text size="small" type="danger">Stale target: current variable is not available in this microflow scope.</Text> : null}
    </div>
  );
}
