import { useEffect, useState } from "react";
import { Empty, Input, Spin, Tag, Typography } from "@douyinfe/semi-ui";
import { useAppI18n } from "../i18n";
import {
  listCommunityWorks,
  type CommunityWorkItem
} from "../../services/api-community";

export function CommunityWorksPage() {
  const { t } = useAppI18n();
  const [items, setItems] = useState<CommunityWorkItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [keyword, setKeyword] = useState("");

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    listCommunityWorks({ pageIndex: 1, pageSize: 20, keyword: keyword.trim() || undefined })
      .then(result => {
        if (!cancelled) {
          setItems(result.items);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setItems([]);
        }
      })
      .finally(() => {
        if (!cancelled) {
          setLoading(false);
        }
      });
    return () => {
      cancelled = true;
    };
  }, [keyword]);

  return (
    <div className="coze-page" data-testid="coze-community-page">
      <header className="coze-page__header">
        <Typography.Title heading={3} style={{ margin: 0 }}>{t("cozeCommunityTitle")}</Typography.Title>
        <Typography.Text type="tertiary">{t("cozeCommunitySubtitle")}</Typography.Text>
      </header>

      <section className="coze-page__toolbar">
        <Input
          value={keyword}
          onChange={value => setKeyword(value)}
          placeholder={t("cozeProjectsSearchPlaceholder")}
          showClear
          style={{ width: 320 }}
        />
      </section>

      <section className="coze-page__body">
        {loading ? (
          <div className="coze-page__loading"><Spin /></div>
        ) : items.length === 0 ? (
          <Empty description={t("cozeCommunityEmpty")} />
        ) : (
          <ul className="coze-list">
            {items.map(item => (
              <li key={item.id} className="coze-list__item">
                <div>
                  <strong>{item.title}</strong>
                  <span>{item.summary}</span>
                </div>
                <div style={{ display: "flex", gap: 6, alignItems: "center" }}>
                  {item.tags.map(tag => <Tag key={tag} color="blue" size="small">{tag}</Tag>)}
                  <span style={{ color: "var(--semi-color-text-2)" }}>{item.authorDisplayName}</span>
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}
