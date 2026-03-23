import { createApp } from "vue";
import Antd from "ant-design-vue";
import "ant-design-vue/dist/reset.css";
import EmbedChat from "./EmbedChat.vue";

type EmbedMountOptions = {
  apiBaseUrl?: string;
  tenantId?: string;
  embedToken: string;
  externalUserId?: string;
  title?: string;
  placeholder?: string;
  sendText?: string;
};

type EmbedMountApi = {
  mount: (selectorOrElement: string | Element, options: EmbedMountOptions) => void;
};

function mountEmbed(selectorOrElement: string | Element, options: EmbedMountOptions) {
  const container = typeof selectorOrElement === "string"
    ? document.querySelector(selectorOrElement)
    : selectorOrElement;
  if (!container) {
    throw new Error(`Embed target not found: ${selectorOrElement.toString()}`);
  }

  const app = createApp(EmbedChat, options);
  app.use(Antd);
  app.mount(container);
}

function tryAutoMount() {
  const autoContainer = document.querySelector("[data-atlas-embed-chat]");
  if (!autoContainer) {
    return;
  }

  const url = new URL(window.location.href);
  const embedToken = url.searchParams.get("embedToken") ?? "";
  const tenantId = url.searchParams.get("tenantId") ?? "";
  const apiBaseUrl = url.searchParams.get("apiBaseUrl") ?? `${window.location.origin}/api/v1`;
  if (!embedToken) {
    return;
  }

  mountEmbed(autoContainer, {
    embedToken,
    tenantId,
    apiBaseUrl,
    externalUserId: url.searchParams.get("externalUserId") ?? undefined
  });
}

const globalApi: EmbedMountApi = {
  mount: mountEmbed
};

(window as Window & { AtlasEmbedChat?: EmbedMountApi }).AtlasEmbedChat = globalApi;
tryAutoMount();
