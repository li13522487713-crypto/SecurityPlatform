import { useDeferredValue, useEffect, useState, startTransition } from "react";
import type { ReactNode } from "react";
import { Empty, Input, Typography } from "@douyinfe/semi-ui";
import { IconSearch } from "@douyinfe/semi-icons";
import type { ExplorePageProps, PluginItem, TemplateItem, SearchItem, RecentEditItem } from "./types";

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

export function ExplorePluginsPage({ api }: ExplorePageProps) {
  const [keyword, setKeyword] = useState("");
  const deferredKeyword = useDeferredValue(keyword);
  const [items, setItems] = useState<PluginItem[]>([]);

  useEffect(() => {
    void api.listPlugins({ pageIndex: 1, pageSize: 20, keyword: deferredKeyword }, deferredKeyword).then(result => setItems(result.items));
  }, [api, deferredKeyword]);

  return (
    <Surface
      title="Plugin Store"
      subtitle="应用级插件市场"
      testId="app-explore-plugins-page"
      toolbar={<Input value={keyword} onChange={value => startTransition(() => setKeyword(value))} prefix={<IconSearch />} />}
    >
      <CardGrid
        testId="app-explore-plugins-grid"
        items={items}
        emptyText="No plugins"
        render={(item: PluginItem) => (
          <article key={item.id} className="module-explore__card">
            <strong>{item.name}</strong>
            <p>{item.description || item.category || "-"}</p>
          </article>
        )}
      />
    </Surface>
  );
}

export function ExploreTemplatesPage({ api }: ExplorePageProps) {
  const [keyword, setKeyword] = useState("");
  const deferredKeyword = useDeferredValue(keyword);
  const [items, setItems] = useState<TemplateItem[]>([]);

  useEffect(() => {
    void api.listTemplates({ pageIndex: 1, pageSize: 20, keyword: deferredKeyword }, { keyword: deferredKeyword }).then(result => setItems(result.items));
  }, [api, deferredKeyword]);

  return (
    <Surface
      title="Template Store"
      subtitle="应用级模板市场"
      testId="app-explore-templates-page"
      toolbar={<Input value={keyword} onChange={value => startTransition(() => setKeyword(value))} prefix={<IconSearch />} />}
    >
      <CardGrid
        testId="app-explore-templates-grid"
        items={items}
        emptyText="No templates"
        render={(item: TemplateItem) => (
          <article key={item.id} className="module-explore__card">
            <strong>{item.name}</strong>
            <p>{item.description || item.tags || "-"}</p>
          </article>
        )}
      />
    </Surface>
  );
}

export function ExploreSearchPage({ api, keyword }: ExplorePageProps & { keyword: string }) {
  const [results, setResults] = useState<SearchItem[]>([]);
  const [recent, setRecent] = useState<RecentEditItem[]>([]);

  useEffect(() => {
    void api.search(keyword, 20).then(result => {
      setResults(result.items);
      setRecent(result.recentEdits);
    });
  }, [api, keyword]);

  return (
    <Surface title="Search" subtitle="统一搜索入口" testId="app-explore-search-page">
      <Typography.Title heading={6}>Recent</Typography.Title>
      <CardGrid
        testId="app-explore-recent-grid"
        items={recent}
        emptyText="No recent edits"
        render={(item: RecentEditItem) => <article key={item.id} className="module-explore__card"><strong>{item.title}</strong><p>{item.resourceType}</p></article>}
      />
      <Typography.Title heading={6}>Results</Typography.Title>
      <CardGrid
        testId="app-explore-search-grid"
        items={results}
        emptyText="No search results"
        render={(item: SearchItem) => <article key={`${item.resourceType}-${item.resourceId}`} className="module-explore__card"><strong>{item.title}</strong><p>{item.description || item.path}</p></article>}
      />
    </Surface>
  );
}
