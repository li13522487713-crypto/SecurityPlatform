import { useNavigate } from "react-router";
import { ConnectorProvidersPage } from "@atlas/external-connectors-react";
import { connectorApi } from "../../../services/api-connectors";
import { useAppI18n } from "../../i18n";

export default function ConnectorsPage() {
  const { t } = useAppI18n();
  const navigate = useNavigate();
  return (
    <div style={{ padding: 24 }}>
      <ConnectorProvidersPage
        api={connectorApi}
        labels={{
          title: t("connectorPageTitle"),
          add: t("connectorAdd"),
          edit: t("connectorEdit"),
          delete: t("connectorDelete"),
          enable: t("connectorEnable"),
          disable: t("connectorDisable"),
          refresh: t("connectorRefresh"),
          empty: t("connectorEmpty"),
          confirmDelete: t("connectorConfirmDelete"),
        }}
        onRowClick={(item) => navigate(`./${item.id}`)}
      />
    </div>
  );
}
