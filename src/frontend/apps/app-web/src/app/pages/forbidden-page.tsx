import { Card, Typography } from "@douyinfe/semi-ui";
import { useAppI18n } from "../i18n";
import { PageShell } from "../_shared";

const { Title, Text } = Typography;

export function ForbiddenPage() {
  const { t } = useAppI18n();

  return (
    <PageShell centered maxWidth={520}>
      <Card bodyStyle={{ padding: 32 }}>
        <Title heading={3}>{t("forbiddenTitle")}</Title>
        <Text type="tertiary">{t("forbiddenDesc")}</Text>
      </Card>
    </PageShell>
  );
}
