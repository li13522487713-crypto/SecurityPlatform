import ReactDOM from "react-dom/client";
import "@douyinfe/semi-ui/lib/es/_base/base.css";
import "@atlas/coze-shell-react/styles.css";
import "@atlas/library-module-react/styles.css";
import "@atlas/module-admin-react/styles.css";
import "@atlas/module-explore-react/styles.css";
import "@atlas/module-studio-react/styles.css";
import "@atlas/module-workflow-react/styles.css";
import "./app/app.css";
import { AppRoot } from "./app/app";
import { initializeAppRuntime } from "./app/runtime-init";

initializeAppRuntime();

const container = document.getElementById("app");
if (!container) {
  throw new Error("App container '#app' was not found.");
}

ReactDOM.createRoot(container).render(<AppRoot />);
