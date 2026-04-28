import { Navigate, useParams, useSearchParams } from "react-router-dom";

const RESOURCE_LEAF_TO_QUERY: Record<string, { tab: string; subType?: string }> = {
  workflows: { tab: "workflow", subType: "workflow" },
  chatflows: { tab: "workflow", subType: "chatflow" },
  plugins: { tab: "plugin" },
  knowledge: { tab: "knowledge-base" },
  databases: { tab: "database" },
  /** 新版资源库无「变量」独立 tab，落回全部 */
  variables: { tab: "all" },
  prompts: { tab: "prompt" }
};

const VALID_LEAVES = new Set<string>(Object.keys(RESOURCE_LEAF_TO_QUERY));

/**
 * 将旧版 `/space/:id/resources[/:type]` 重定向到统一资源库 `/space/:id/library`，
 * 并映射路径上的 `type` 为 `?tab=` / `?subType=`（保留原有 query 其它参数）。
 */
export function WorkspaceResourcesRedirect() {
  const { space_id = "", type } = useParams<{ space_id?: string; type?: string }>();
  const [search] = useSearchParams();
  const next = new URLSearchParams(search);
  if (type && VALID_LEAVES.has(type)) {
    const m = RESOURCE_LEAF_TO_QUERY[type]!;
    if (m.tab === "all") {
      next.delete("tab");
    } else {
      next.set("tab", m.tab);
    }
    if (m.subType) {
      next.set("subType", m.subType);
    } else {
      next.delete("subType");
    }
  } else {
    if (!next.get("tab")) {
      next.delete("tab");
    }
  }
  const q = next.toString();
  const to = q ? `/space/${encodeURIComponent(space_id)}/library?${q}` : `/space/${encodeURIComponent(space_id)}/library`;
  return <Navigate to={to} replace />;
}
