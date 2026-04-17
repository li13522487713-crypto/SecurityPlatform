import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Banner, Empty, Spin, Typography } from "@douyinfe/semi-ui";
import { useAppI18n } from "../i18n";
import {
  listPlatformNotices,
  type PlatformNoticeItem
} from "../../services/mock";

export function PlatformGeneralPage() {
  const { t } = useAppI18n();
  const [items, setItems] = useState<PlatformNoticeItem[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    listPlatformNotices()
      .then(notices => {
        if (!cancelled) {
          setItems(notices);
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
    <div className="coze-page" data-testid="coze-platform-general-page">
      <header className="coze-page__header">
        <Typography.Title heading={3} style={{ margin: 0 }}>{t("cozePlatformTitle")}</Typography.Title>
        <Typography.Text type="tertiary">{t("cozePlatformSubtitle")}</Typography.Text>
      </header>
      <section className="coze-page__body">
        {/* M10/D7：系统初始化与迁移控制台入口（已登录场景从平台管理进入；仍需控制台二次认证） */}
        <Banner type="info" style={{ marginBottom: 12 }} closeIcon={null}>
          <strong>{t("setupConsoleTitle")}</strong>
          <p style={{ margin: "4px 0 8px" }}>{t("setupConsoleSubtitle")}</p>
          <Link
            to="/setup-console"
            data-testid="platform-general-setup-console-entry"
            style={{ color: "var(--semi-color-primary, #1677ff)" }}
          >
            {t("setupConsoleSystemReopen")} →
          </Link>
        </Banner>

        {loading ? (
          <div className="coze-page__loading"><Spin /></div>
        ) : items.length === 0 ? (
          <Empty description={t("cozePlatformEmpty")} />
        ) : (
          <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
            {items.map(item => (
              <Banner key={item.id} type={item.level === "error" ? "danger" : item.level === "warning" ? "warning" : "info"}>
                <strong>{item.title}</strong>
                <p style={{ margin: 0 }}>{item.message}</p>
              </Banner>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}
