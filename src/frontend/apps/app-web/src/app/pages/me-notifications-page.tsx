import { useEffect, useState } from "react";
import { Button, Empty, Spin, Tag, Typography } from "@douyinfe/semi-ui";
import { useAppI18n } from "../i18n";
import {
  getNotifications,
  markAllAsRead,
  markAsRead,
  type UserNotificationItem
} from "../../services/api-notifications";

export function MeNotificationsPage() {
  const { t } = useAppI18n();
  const [items, setItems] = useState<UserNotificationItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);

  const refresh = () => {
    setLoading(true);
    getNotifications(1, 20)
      .then(result => {
        setItems(result.items);
      })
      .catch(() => {
        setItems([]);
      })
      .finally(() => {
        setLoading(false);
      });
  };

  useEffect(() => {
    refresh();
  }, []);

  const handleRead = async (id: string) => {
    setSubmitting(true);
    try {
      await markAsRead(id);
      refresh();
    } finally {
      setSubmitting(false);
    }
  };

  const handleReadAll = async () => {
    setSubmitting(true);
    try {
      await markAllAsRead();
      refresh();
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="coze-page" data-testid="coze-me-notifications-page">
      <header className="coze-page__header">
        <Typography.Title heading={3} style={{ margin: 0 }}>{t("cozeMeNotificationsTitle")}</Typography.Title>
        <Typography.Text type="tertiary">{t("cozeMeNotificationsSubtitle")}</Typography.Text>
      </header>
      <section className="coze-page__toolbar">
        <Button theme="light" type="primary" onClick={() => void handleReadAll()} loading={submitting}>
          {t("cozeMeNotificationsMarkAllRead")}
        </Button>
      </section>
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
                  <span>{item.content}</span>
                </div>
                <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
                  <Tag size="small" color={item.isRead ? "grey" : "blue"}>
                    {item.isRead ? t("cozeMeNotificationsRead") : t("cozeMeNotificationsUnread")}
                  </Tag>
                  {!item.isRead ? (
                    <Button theme="borderless" type="primary" onClick={() => void handleRead(item.id)} loading={submitting}>
                      {t("cozeMeNotificationsMarkRead")}
                    </Button>
                  ) : null}
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}
