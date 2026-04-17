import { useEffect, useState } from "react";
import { Button, Empty, Spin, Typography } from "@douyinfe/semi-ui";
import { useNavigate } from "react-router-dom";
import { useAppI18n } from "../i18n";
import {
  listTemplateCategories,
  type MarketCategorySummary
} from "../../services/mock";

export function MarketTemplatesPage() {
  const { t } = useAppI18n();
  const navigate = useNavigate();
  const [items, setItems] = useState<MarketCategorySummary[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    listTemplateCategories({ pageIndex: 1, pageSize: 20 })
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
  }, []);

  return (
    <div className="coze-page" data-testid="coze-market-templates-page">
      <header className="coze-page__header">
        <Typography.Title heading={3} style={{ margin: 0 }}>{t("cozeMarketTemplatesTitle")}</Typography.Title>
        <Typography.Text type="tertiary">{t("cozeMarketTemplatesSubtitle")}</Typography.Text>
      </header>

      <section className="coze-page__body">
        {loading ? (
          <div className="coze-page__loading"><Spin /></div>
        ) : items.length === 0 ? (
          <Empty description={t("cozeCommunityEmpty")} />
        ) : (
          <div className="coze-card-grid">
            {items.map(item => (
              <div key={item.id} className="coze-card">
                <Typography.Title heading={5} style={{ margin: 0 }}>{item.name}</Typography.Title>
                <Typography.Text type="tertiary" style={{ marginTop: 6 }}>
                  {item.description ?? ""}（{item.count}）
                </Typography.Text>
              </div>
            ))}
          </div>
        )}
      </section>

      <footer className="coze-page__footer">
        <Button
          theme="solid"
          type="primary"
          onClick={() => navigate("/explore/template")}
          data-testid="coze-market-templates-go-existing"
        >
          {t("cozeMarketGoExisting")}
        </Button>
      </footer>
    </div>
  );
}
