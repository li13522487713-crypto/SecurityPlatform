import { useEffect, useState } from "react";
import { Empty, Spin, Tag, Typography } from "@douyinfe/semi-ui";
import { useAppI18n } from "../i18n";
import { listPlatformNotices, type PlatformNoticeItem } from "../../services/mock";

/**
 * 消息中心 - `/me/notifications`。
 *
 * 第一阶段直接复用 `listPlatformNotices` mock 当作消息源；
 * 第三阶段后端落地 `GET /api/v1/me/notifications` 后替换。
 */
export function MeNotificationsPage() {
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
    <div className="coze-page" data-testid="coze-me-notifications-page">
      <header className="coze-page__header">
        <Typography.Title heading={3} style={{ margin: 0 }}>{t("cozeMeNotificationsTitle")}</Typography.Title>
        <Typography.Text type="tertiary">{t("cozeMeNotificationsSubtitle")}</Typography.Text>
      </header>
      <section className="coze-page__body">
        {loading ? (
          <div className="coze-page__loading"><Spin /></div>
        ) : items.length === 0 ? (
          <Empty description={t("cozeMeNotificationsEmpty")} />
        ) : (
          <ul className="coze-list">
            {items.map(item => (
              <li key={item.id} className="coze-list__item">
                <div>
                  <strong>{item.title}</strong>
                  <span>{item.message}</span>
                </div>
                <Tag size="small" color={item.level === "error" ? "red" : item.level === "warning" ? "amber" : "blue"}>
                  {item.level}
                </Tag>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}
