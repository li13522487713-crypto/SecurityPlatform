import { Button, Checkbox, Input, Select, Space } from "@douyinfe/semi-ui";
import { IconRefresh, IconSearch } from "@douyinfe/semi-icons";

import type { MicroflowResource, MicroflowResourceQuery, MicroflowResourceView } from "./resource-types";

interface MicroflowResourceToolbarProps {
  query: MicroflowResourceQuery;
  view: MicroflowResourceView;
  allItems: MicroflowResource[];
  onQueryChange: (patch: Partial<MicroflowResourceQuery>) => void;
  onViewChange: (view: MicroflowResourceView) => void;
  onRefresh: () => void;
}

export function MicroflowResourceToolbar({ query, view, allItems, onQueryChange, onViewChange, onRefresh }: MicroflowResourceToolbarProps) {
  const modules = [...new Map(allItems.map(item => [item.moduleId, item.moduleName || item.moduleId])).entries()];
  const tags = [...new Set(allItems.flatMap(item => item.tags))].sort();
  const owners = [...new Map(allItems.map(item => [item.ownerId || item.createdBy || item.ownerName || "", item.ownerName || item.createdBy || "Unknown"])).entries()].filter(([id]) => id);

  return (
    <div style={{ display: "flex", gap: 8, flexWrap: "wrap", alignItems: "center" }}>
      <Input
        prefix={<IconSearch />}
        showClear
        value={query.keyword ?? ""}
        onChange={value => onQueryChange({ keyword: value })}
        placeholder="搜索名称、描述或标签"
        style={{ width: 240 }}
      />
      <Select
        multiple
        value={query.status ?? []}
        onChange={value => onQueryChange({ status: value as MicroflowResourceQuery["status"] })}
        placeholder="状态"
        style={{ width: 180 }}
        optionList={[
          { value: "draft", label: "草稿" },
          { value: "published", label: "已发布" },
          { value: "archived", label: "已归档" }
        ]}
      />
      <Select
        value={query.moduleId ?? ""}
        onChange={value => onQueryChange({ moduleId: String(value) || undefined })}
        placeholder="模块"
        style={{ width: 140 }}
        optionList={[{ value: "", label: "全部模块" }, ...modules.map(([value, label]) => ({ value, label }))]}
      />
      <Select
        multiple
        value={query.tags ?? []}
        onChange={value => onQueryChange({ tags: value as string[] })}
        placeholder="标签"
        style={{ width: 160 }}
        optionList={tags.map(tag => ({ value: tag, label: tag }))}
      />
      <Select
        value={query.ownerId ?? ""}
        onChange={value => onQueryChange({ ownerId: String(value) || undefined })}
        placeholder="创建人"
        style={{ width: 140 }}
        optionList={[{ value: "", label: "全部创建人" }, ...owners.map(([value, label]) => ({ value, label }))]}
      />
      <Select
        value={`${query.sortBy ?? "updatedAt"}:${query.sortOrder ?? "desc"}`}
        onChange={value => {
          const [sortBy, sortOrder] = String(value).split(":");
          onQueryChange({ sortBy: sortBy as MicroflowResourceQuery["sortBy"], sortOrder: sortOrder as MicroflowResourceQuery["sortOrder"] });
        }}
        style={{ width: 180 }}
        optionList={[
          { value: "updatedAt:desc", label: "最近修改" },
          { value: "createdAt:desc", label: "最近创建" },
          { value: "name:asc", label: "名称 A-Z" },
          { value: "version:desc", label: "版本号" },
          { value: "referenceCount:desc", label: "引用数" }
        ]}
      />
      <Checkbox checked={Boolean(query.favoriteOnly)} onChange={event => onQueryChange({ favoriteOnly: Boolean(event.target.checked) })}>
        仅收藏
      </Checkbox>
      <Space>
        <Button theme={view === "card" ? "solid" : "borderless"} type={view === "card" ? "primary" : "tertiary"} onClick={() => onViewChange("card")}>卡片</Button>
        <Button theme={view === "table" ? "solid" : "borderless"} type={view === "table" ? "primary" : "tertiary"} onClick={() => onViewChange("table")}>表格</Button>
        <Button icon={<IconRefresh />} onClick={onRefresh}>刷新</Button>
      </Space>
    </div>
  );
}
