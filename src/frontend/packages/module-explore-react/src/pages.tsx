import { useDeferredValue, useEffect, useState, startTransition } from "react";
import type { ReactNode } from "react";
import { Button, Empty, Input, Space, Tag, Toast, Typography } from "@douyinfe/semi-ui";
import { IconSearch } from "@douyinfe/semi-icons";
import type {
  ExplorePageProps,
  MarketplacePluginDetail,
  MarketplacePluginItem,
  RecentEditItem,
  SearchItem,
  TemplateDetail,
  TemplateItem
} from "./types";

const IMPORTED_PLUGIN_STORAGE_KEY = "atlas_explore_imported_plugins";
const CREATED_TEMPLATE_STORAGE_KEY = "atlas_explore_created_templates";

interface ImportedPluginState {
  route: string;
  importedPluginId: number;
  sourceProductId: number;
  sourceName: string;
  importedAt: string;
}

interface CreatedTemplateState {
  route: string;
  workflowId: string;
  mode: "workflow" | "chatflow";
  templateId: number;
  templateName: string;
  createdAt: string;
}

function readStoredObject<T extends Record<string, unknown>>(storageKey: string): T | Record<string, never> {
  if (typeof window === "undefined") {
    return {};
  }

  const raw = window.localStorage.getItem(storageKey);
  if (!raw) {
    return {};
  }

  try {
    const parsed = JSON.parse(raw) as T;
    return parsed && typeof parsed === "object" ? parsed : {};
  } catch {
    return {};
  }
}

function readImportedPluginStateMap(): Record<string, ImportedPluginState> {
  const raw = readStoredObject<Record<string, ImportedPluginState | string>>(IMPORTED_PLUGIN_STORAGE_KEY);
  return Object.fromEntries(
    Object.entries(raw).map(([key, value]) => {
      if (typeof value === "string") {
        return [key, {
          route: value,
          importedPluginId: 0,
          sourceProductId: Number(key),
          sourceName: "",
          importedAt: ""
        } satisfies ImportedPluginState];
      }

      return [key, value];
    })
  );
}

function readCreatedTemplateStateMap(): Record<string, CreatedTemplateState> {
  const raw = readStoredObject<Record<string, CreatedTemplateState | string>>(CREATED_TEMPLATE_STORAGE_KEY);
  return Object.fromEntries(
    Object.entries(raw).map(([key, value]) => {
      if (typeof value === "string") {
        return [key, {
          route: value,
          workflowId: "",
          mode: "workflow",
          templateId: Number(key),
          templateName: "",
          createdAt: ""
        } satisfies CreatedTemplateState];
      }

      return [key, value];
    })
  );
}

function writeStoredObject(storageKey: string, value: Record<string, unknown>) {
  if (typeof window === "undefined") {
    return;
  }

  window.localStorage.setItem(storageKey, JSON.stringify(value));
}

function Surface({
  title,
  subtitle,
  testId,
  toolbar,
  children
}: {
  title: string;
  subtitle: string;
  testId: string;
  toolbar?: ReactNode;
  children: ReactNode;
}) {
  return (
    <section className="module-explore__page" data-testid={testId}>
      <div className="module-explore__header">
        <div>
          <Typography.Title heading={4} style={{ margin: 0 }}>{title}</Typography.Title>
          <Typography.Text type="tertiary">{subtitle}</Typography.Text>
        </div>
        {toolbar ? <div className="module-explore__toolbar">{toolbar}</div> : null}
      </div>
      <div className="module-explore__surface">{children}</div>
    </section>
  );
}

function CardGrid<T>({
  testId,
  items,
  emptyText,
  render
}: {
  testId: string;
  items: T[];
  emptyText: string;
  render: (item: T) => ReactNode;
}) {
  if (items.length === 0) {
    return <div data-testid={testId}><Empty title={emptyText} image={null} /></div>;
  }

  return <div className="module-explore__grid" data-testid={testId}>{items.map(render)}</div>;
}

function formatDate(value?: string) {
  if (!value) {
    return "-";
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}

function inferTemplateMode(detail: TemplateDetail): "workflow" | "chatflow" {
  const evidence = `${detail.name} ${detail.description} ${detail.tags} ${detail.schemaJson ?? ""}`.toLowerCase();
  return evidence.includes("chatflow") || evidence.includes("chat_flow") || evidence.includes("对话")
    ? "chatflow"
    : "workflow";
}

function getResourceTag(resourceType: string, path: string): { label: string; color: "cyan" | "green" | "grey" | "purple" | "orange" } {
  if (path.includes("/chat_flow/")) {
    return { label: "对话流", color: "purple" };
  }

  if (path.includes("/work_flow/")) {
    return { label: "工作流", color: "cyan" };
  }

  switch (resourceType) {
    case "agent":
      return { label: "智能体", color: "green" };
    case "app":
      return { label: "应用", color: "orange" };
    case "plugin":
      return { label: "插件", color: "cyan" };
    case "knowledge-base":
      return { label: "知识库", color: "green" };
    case "database":
      return { label: "数据库", color: "grey" };
    case "workflow":
      return { label: "工作流", color: "cyan" };
    default:
      return { label: resourceType || "资源", color: "grey" };
  }
}

function getMarketOriginTag(path: string): string | null {
  const importedPlugins = readImportedPluginStateMap();
  const createdTemplates = readCreatedTemplateStateMap();
  const importedPlugin = Object.values(importedPlugins).find(item => item.route === path);
  if (importedPlugin) {
    return `来自插件市场 · ${importedPlugin.sourceName || `商品#${importedPlugin.sourceProductId}`}`;
  }

  const createdTemplate = Object.values(createdTemplates).find(item => item.route === path);
  if (createdTemplate) {
    return `来自模板市场 · ${createdTemplate.templateName || `模板#${createdTemplate.templateId}`}`;
  }

  return null;
}

export function ExplorePluginsPage({
  api,
  onOpenDetail,
  onOpenImported
}: ExplorePageProps & {
  onOpenDetail: (productId: number) => void;
  onOpenImported: (route: string) => void;
}) {
  const [keyword, setKeyword] = useState("");
  const deferredKeyword = useDeferredValue(keyword);
  const [items, setItems] = useState<MarketplacePluginItem[]>([]);
  const [importingProductId, setImportingProductId] = useState<number | null>(null);
  const [togglingProductId, setTogglingProductId] = useState<number | null>(null);
  const [importedPlugins, setImportedPlugins] = useState<Record<string, ImportedPluginState>>(() => readImportedPluginStateMap());

  async function load() {
    try {
      const result = await api.listPlugins(
        { pageIndex: 1, pageSize: 20, keyword: deferredKeyword },
        deferredKeyword
      );
      setItems(result.items);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "加载插件市场失败。");
    }
  }

  useEffect(() => {
    void load();
  }, [api, deferredKeyword]);

  const handleToggleFavorite = async (item: MarketplacePluginItem) => {
    setTogglingProductId(item.id);
    try {
      if (item.isFavorited) {
        await api.unfavoritePlugin(item.id);
      } else {
        await api.favoritePlugin(item.id);
      }
      await load();
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "更新收藏状态失败。");
    } finally {
      setTogglingProductId(null);
    }
  };

  const handleImport = async (item: MarketplacePluginItem) => {
    setImportingProductId(item.id);
    try {
      const result = await api.importPluginToStudio(item.id);
      const nextImportedPlugins = {
        ...readImportedPluginStateMap(),
        [String(item.id)]: {
          route: result.route,
          importedPluginId: result.importedPluginId,
          sourceProductId: item.id,
          sourceName: item.name,
          importedAt: new Date().toISOString()
        } satisfies ImportedPluginState
      };
      writeStoredObject(IMPORTED_PLUGIN_STORAGE_KEY, nextImportedPlugins);
      setImportedPlugins(nextImportedPlugins);
      Toast.success(`插件已导入，正在打开 Studio 详情页。`);
      onOpenImported(result.route);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "导入插件失败。");
    } finally {
      setImportingProductId(null);
    }
  };

  return (
    <Surface
      title="Plugin Store"
      subtitle="插件市场商品已接入收藏、导入和 Studio 回流。"
      testId="app-explore-plugins-page"
      toolbar={<Input value={keyword} onChange={value => startTransition(() => setKeyword(value))} prefix={<IconSearch />} />}
    >
      <CardGrid
        testId="app-explore-plugins-grid"
        items={items}
        emptyText="No plugins"
        render={(item: MarketplacePluginItem) => (
          <article key={item.id} className="module-explore__card">
            <div className="module-explore__card-head">
              <strong>{item.name}</strong>
              <Tag color={item.status === 1 ? "green" : "blue"}>{item.status === 1 ? "Published" : "Draft"}</Tag>
            </div>
            <p>{item.description || item.categoryName || "-"}</p>
            <Space wrap>
              <Tag color="cyan">{item.categoryName || "Plugin"}</Tag>
              <Tag color="grey">v{item.version}</Tag>
              <Tag color="violet">Fav {item.favoriteCount}</Tag>
              <Tag color="amber">DL {item.downloadCount}</Tag>
              {item.isFavorited ? <Tag color="pink">已收藏</Tag> : null}
              {importedPlugins[String(item.id)] ? <Tag color="green">已导入 Studio</Tag> : null}
            </Space>
            {importedPlugins[String(item.id)] ? (
              <Typography.Text type="tertiary">
                最近导入：插件 #{importedPlugins[String(item.id)]!.importedPluginId || "-"}，时间 {formatDate(importedPlugins[String(item.id)]!.importedAt)}
              </Typography.Text>
            ) : null}
            <Space wrap>
              <Button theme="solid" type="primary" onClick={() => onOpenDetail(item.id)}>
                查看详情
              </Button>
              <Button loading={togglingProductId === item.id} onClick={() => void handleToggleFavorite(item)}>
                {item.isFavorited ? "取消收藏" : "收藏"}
              </Button>
              <Button loading={importingProductId === item.id} onClick={() => void handleImport(item)}>
                一键导入到 Studio
              </Button>
              {importedPlugins[String(item.id)] ? (
                <Button onClick={() => onOpenImported(importedPlugins[String(item.id)]!.route)}>
                  打开已导入资源
                </Button>
              ) : null}
            </Space>
          </article>
        )}
      />
    </Surface>
  );
}

export function ExplorePluginDetailPage({
  api,
  productId,
  onOpenImported
}: ExplorePageProps & {
  productId: number;
  onOpenImported: (route: string) => void;
}) {
  const [detail, setDetail] = useState<MarketplacePluginDetail | null>(null);
  const [favoriteLoading, setFavoriteLoading] = useState(false);
  const [importLoading, setImportLoading] = useState(false);
  const [importedPlugin, setImportedPlugin] = useState<ImportedPluginState | null>(() => readImportedPluginStateMap()[String(productId)] ?? null);

  async function load() {
    try {
      setDetail(await api.getPluginDetail(productId));
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "加载插件市场详情失败。");
    }
  }

  useEffect(() => {
    void load();
  }, [api, productId]);

  const handleToggleFavorite = async () => {
    if (!detail) {
      return;
    }

    setFavoriteLoading(true);
    try {
      if (detail.isFavorited) {
        await api.unfavoritePlugin(detail.id);
      } else {
        await api.favoritePlugin(detail.id);
      }
      await load();
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "更新收藏状态失败。");
    } finally {
      setFavoriteLoading(false);
    }
  };

  const handleImport = async () => {
    if (!detail) {
      return;
    }

    setImportLoading(true);
    try {
      const result = await api.importPluginToStudio(detail.id);
      const nextImportedPlugins = {
        ...readImportedPluginStateMap(),
        [String(detail.id)]: {
          route: result.route,
          importedPluginId: result.importedPluginId,
          sourceProductId: detail.id,
          sourceName: detail.name,
          importedAt: new Date().toISOString()
        } satisfies ImportedPluginState
      };
      writeStoredObject(IMPORTED_PLUGIN_STORAGE_KEY, nextImportedPlugins);
      setImportedPlugin(nextImportedPlugins[String(detail.id)]!);
      Toast.success("插件已导入到 Studio。");
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "导入插件失败。");
    } finally {
      setImportLoading(false);
    }
  };

  return (
    <Surface title="Plugin Detail" subtitle="查看插件商品摘要、来源资源与导入回流。" testId="app-explore-plugin-detail-page">
      {detail ? (
        <div className="module-explore__grid">
          <article className="module-explore__card">
            <div className="module-explore__card-head">
              <strong>{detail.name}</strong>
              <Tag color={detail.status === 1 ? "green" : "blue"}>{detail.status === 1 ? "Published" : "Draft"}</Tag>
            </div>
            <p>{detail.description || detail.summary || "-"}</p>
            <Space wrap>
              <Tag color="cyan">{detail.categoryName}</Tag>
              <Tag color="grey">v{detail.version}</Tag>
              <Tag color="violet">Fav {detail.favoriteCount}</Tag>
              <Tag color="amber">DL {detail.downloadCount}</Tag>
            </Space>
            <Typography.Paragraph type="tertiary">
              来源插件资源：{detail.sourcePluginName || detail.sourceResourceId || "-"}，发布时间：{formatDate(detail.publishedAt)}
            </Typography.Paragraph>
            {importedPlugin ? (
              <Tag color="green">该商品已导入到当前 Studio</Tag>
            ) : null}
            {importedPlugin ? (
              <Typography.Text type="tertiary">
                导入结果：插件 #{importedPlugin.importedPluginId || "-"}，导入时间 {formatDate(importedPlugin.importedAt)}
              </Typography.Text>
            ) : null}
            <Space wrap>
              <Button loading={favoriteLoading} onClick={() => void handleToggleFavorite()}>
                {detail.isFavorited ? "取消收藏" : "收藏"}
              </Button>
              <Button theme="solid" type="primary" loading={importLoading} onClick={() => void handleImport()}>
                一键导入到 Studio
              </Button>
              {importedPlugin ? (
                <Button onClick={() => onOpenImported(importedPlugin.route)}>
                  打开已导入资源
                </Button>
              ) : null}
            </Space>
          </article>
          <article className="module-explore__card">
            <Typography.Title heading={6}>标签与来源</Typography.Title>
            <Space wrap>
              {detail.tags.length > 0 ? detail.tags.map(tag => (
                <Tag key={tag} color="light-blue">{tag}</Tag>
              )) : <Tag color="grey">无标签</Tag>}
            </Space>
            <Typography.Paragraph type="tertiary">
              商品类型：插件，来源分类：{detail.sourcePluginCategory || "-"}，来源 API 数：{detail.sourcePluginApiCount ?? "-"}，发布人：{detail.publisherUserId}
            </Typography.Paragraph>
          </article>
        </div>
      ) : (
        <Empty title="未找到插件市场商品" image={null} />
      )}
    </Surface>
  );
}

export function ExploreTemplatesPage({
  api,
  onOpenDetail,
  onOpenCreated
}: ExplorePageProps & {
  onOpenDetail: (templateId: number) => void;
  onOpenCreated: (route: string) => void;
}) {
  const [keyword, setKeyword] = useState("");
  const deferredKeyword = useDeferredValue(keyword);
  const [items, setItems] = useState<TemplateItem[]>([]);
  const [creatingTemplateId, setCreatingTemplateId] = useState<number | null>(null);
  const [createdTemplates, setCreatedTemplates] = useState<Record<string, CreatedTemplateState>>(() => readCreatedTemplateStateMap());

  async function load() {
    try {
      const result = await api.listTemplates(
        { pageIndex: 1, pageSize: 20, keyword: deferredKeyword },
        { keyword: deferredKeyword, category: 2 }
      );
      setItems(result.items);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "加载模板市场失败。");
    }
  }

  useEffect(() => {
    void load();
  }, [api, deferredKeyword]);

  const handleCreate = async (item: TemplateItem) => {
    setCreatingTemplateId(item.id);
    try {
      const result = await api.createWorkflowFromTemplate(item.id);
      const nextCreatedTemplates = {
        ...readCreatedTemplateStateMap(),
        [String(item.id)]: {
          route: result.route,
          workflowId: result.workflowId,
          mode: result.mode,
          templateId: item.id,
          templateName: item.name,
          createdAt: new Date().toISOString()
        } satisfies CreatedTemplateState
      };
      writeStoredObject(CREATED_TEMPLATE_STORAGE_KEY, nextCreatedTemplates);
      setCreatedTemplates(nextCreatedTemplates);
      Toast.success(`模板已创建为${result.mode === "chatflow" ? "对话流" : "工作流"}，正在打开画布。`);
      onOpenCreated(result.route);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "从模板创建工作流失败。");
    } finally {
      setCreatingTemplateId(null);
    }
  };

  return (
    <Surface
      title="Template Store"
      subtitle="模板市场支持直接创建 workflow / chatflow。"
      testId="app-explore-templates-page"
      toolbar={<Input value={keyword} onChange={value => startTransition(() => setKeyword(value))} prefix={<IconSearch />} />}
    >
      <CardGrid
        testId="app-explore-templates-grid"
        items={items}
        emptyText="No templates"
        render={(item: TemplateItem) => (
          <article key={item.id} className="module-explore__card">
            <div className="module-explore__card-head">
              <strong>{item.name}</strong>
              <Tag color="cyan">v{item.version}</Tag>
            </div>
            <p>{item.description || item.tags || "-"}</p>
            <Space wrap>
              {item.tags.split(",").map(tag => tag.trim()).filter(Boolean).map(tag => (
                <Tag key={tag} color="light-blue">{tag}</Tag>
              ))}
              {createdTemplates[String(item.id)] ? <Tag color="green">已创建画布</Tag> : null}
            </Space>
            {createdTemplates[String(item.id)] ? (
              <Typography.Text type="tertiary">
                最近创建：{createdTemplates[String(item.id)]!.mode === "chatflow" ? "对话流" : "工作流"} #{createdTemplates[String(item.id)]!.workflowId || "-"}
              </Typography.Text>
            ) : null}
            <Space wrap>
              <Button theme="solid" type="primary" onClick={() => onOpenDetail(item.id)}>
                查看详情
              </Button>
              <Button loading={creatingTemplateId === item.id} onClick={() => void handleCreate(item)}>
                一键创建工作流
              </Button>
              {createdTemplates[String(item.id)] ? (
                <Button onClick={() => onOpenCreated(createdTemplates[String(item.id)]!.route)}>
                  打开已创建画布
                </Button>
              ) : null}
            </Space>
          </article>
        )}
      />
    </Surface>
  );
}

export function ExploreTemplateDetailPage({
  api,
  templateId,
  onOpenCreated
}: ExplorePageProps & {
  templateId: number;
  onOpenCreated: (route: string) => void;
}) {
  const [detail, setDetail] = useState<TemplateDetail | null>(null);
  const [creating, setCreating] = useState(false);
  const [createdTemplate, setCreatedTemplate] = useState<CreatedTemplateState | null>(() => readCreatedTemplateStateMap()[String(templateId)] ?? null);

  async function load() {
    try {
      setDetail(await api.getTemplateDetail(templateId));
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "加载模板详情失败。");
    }
  }

  useEffect(() => {
    void load();
  }, [api, templateId]);

  const handleCreate = async () => {
    if (!detail) {
      return;
    }

    setCreating(true);
    try {
      const result = await api.createWorkflowFromTemplate(detail.id);
      const nextCreatedTemplates = {
        ...readCreatedTemplateStateMap(),
        [String(detail.id)]: {
          route: result.route,
          workflowId: result.workflowId,
          mode: result.mode,
          templateId: detail.id,
          templateName: detail.name,
          createdAt: new Date().toISOString()
        } satisfies CreatedTemplateState
      };
      writeStoredObject(CREATED_TEMPLATE_STORAGE_KEY, nextCreatedTemplates);
      setCreatedTemplate(nextCreatedTemplates[String(detail.id)]!);
      Toast.success(`模板已创建为${result.mode === "chatflow" ? "对话流" : "工作流"}。`);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "从模板创建工作流失败。");
    } finally {
      setCreating(false);
    }
  };

  return (
    <Surface title="Template Detail" subtitle="查看模板 schema 摘要并直接创建画布。" testId="app-explore-template-detail-page">
      {detail ? (
        <div className="module-explore__grid">
          <article className="module-explore__card">
            <div className="module-explore__card-head">
              <strong>{detail.name}</strong>
              <Tag color="cyan">v{detail.version}</Tag>
            </div>
            <p>{detail.description || "-"}</p>
            <Space wrap>
              <Tag color="light-blue">category:{detail.category}</Tag>
              <Tag color={inferTemplateMode(detail) === "chatflow" ? "purple" : "cyan"}>
                {inferTemplateMode(detail) === "chatflow" ? "对话流" : "标准工作流"}
              </Tag>
              {detail.isBuiltIn ? <Tag color="green">Built-in</Tag> : null}
              {createdTemplate ? <Tag color="green">已创建画布</Tag> : null}
            </Space>
            {createdTemplate ? (
              <Typography.Text type="tertiary">
                创建结果：{createdTemplate.mode === "chatflow" ? "对话流" : "工作流"} #{createdTemplate.workflowId || "-"}，时间 {formatDate(createdTemplate.createdAt)}
              </Typography.Text>
            ) : null}
            <Space wrap>
              <Button theme="solid" type="primary" loading={creating} onClick={() => void handleCreate()}>
                一键创建工作流
              </Button>
              {createdTemplate ? (
                <Button onClick={() => onOpenCreated(createdTemplate.route)}>
                  打开已创建画布
                </Button>
              ) : null}
            </Space>
          </article>
          <article className="module-explore__card">
            <Typography.Title heading={6}>Schema 摘要</Typography.Title>
            <pre className="module-explore__schema-preview">{detail.schemaJson}</pre>
          </article>
        </div>
      ) : (
        <Empty title="未找到模板详情" image={null} />
      )}
    </Surface>
  );
}

export function ExploreSearchPage({
  api,
  keyword,
  onOpenLocal
}: ExplorePageProps & {
  keyword: string;
  onOpenLocal: (path: string) => void;
}) {
  const [results, setResults] = useState<SearchItem[]>([]);
  const [recent, setRecent] = useState<RecentEditItem[]>([]);

  useEffect(() => {
    void api.search(keyword, 20).then(result => {
      setResults(result.items);
      setRecent(result.recentEdits);
    }).catch(error => {
      Toast.error(error instanceof Error ? error.message : "加载搜索结果失败。");
    });
  }, [api, keyword]);

  return (
    <Surface title="Search" subtitle="统一搜索入口" testId="app-explore-search-page">
      <Typography.Title heading={6}>Recent</Typography.Title>
      <CardGrid
        testId="app-explore-recent-grid"
        items={recent}
        emptyText="No recent edits"
        render={(item: RecentEditItem) => (
          <article key={item.id} className="module-explore__card">
            <div className="module-explore__card-head">
              <strong>{item.title}</strong>
              <Tag color={getResourceTag(item.resourceType, item.path).color}>
                {getResourceTag(item.resourceType, item.path).label}
              </Tag>
            </div>
            <p>{item.path}</p>
            {getMarketOriginTag(item.path) ? (
              <Typography.Text type="tertiary">{getMarketOriginTag(item.path)}</Typography.Text>
            ) : null}
            <Button theme="borderless" onClick={() => onOpenLocal(item.path)}>打开</Button>
          </article>
        )}
      />
      <Typography.Title heading={6}>Results</Typography.Title>
      <CardGrid
        testId="app-explore-search-grid"
        items={results}
        emptyText="No search results"
        render={(item: SearchItem) => (
          <article key={`${item.resourceType}-${item.resourceId}`} className="module-explore__card">
            <div className="module-explore__card-head">
              <strong>{item.title}</strong>
              <Tag color={getResourceTag(item.resourceType, item.path).color}>
                {getResourceTag(item.resourceType, item.path).label}
              </Tag>
            </div>
            <p>{item.description || item.path}</p>
            {getMarketOriginTag(item.path) ? (
              <Space wrap>
                <Tag color="green">来自市场</Tag>
                <Typography.Text type="tertiary">{getMarketOriginTag(item.path)}</Typography.Text>
              </Space>
            ) : null}
            <Button theme="borderless" onClick={() => onOpenLocal(item.path)}>打开</Button>
          </article>
        )}
      />
    </Surface>
  );
}
