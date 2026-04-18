import { ConnectorProvidersPage } from "@atlas/external-connectors-react";
import { connectorApi } from "../../../services/api-connectors";

export default function ConnectorsPage() {
  return <ConnectorProvidersPage api={connectorApi} />;
}
