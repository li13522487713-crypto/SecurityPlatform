<template>
  <a-space direction="vertical" style="width: 100%" :size="16">
    <a-card :title="t('ai.openPlatform.pageTitle')" :bordered="false">
      <a-alert
        type="info"
        show-icon
        :message="t('ai.openPlatform.alert')"
      />

      <a-form layout="vertical" style="margin-top: 16px">
        <a-form-item :label="t('ai.openPlatform.labelPat')">
          <a-input-password v-model:value="patToken" :placeholder="t('ai.openPlatform.patPlaceholder')" />
        </a-form-item>
        <a-form-item :label="t('ai.openPlatform.labelAgentId')">
          <a-input-number v-model:value="agentId" :min="1" style="width: 200px" />
        </a-form-item>
        <a-form-item>
          <a-space>
            <a-button :loading="loadingBots" @click="loadBots">{{ t("ai.openPlatform.btnBots") }}</a-button>
            <a-button type="primary" :loading="loadingChat" @click="sendChat">{{ t("ai.openPlatform.btnChat") }}</a-button>
          </a-space>
        </a-form-item>
      </a-form>

      <a-divider orientation="left">{{ t("ai.openPlatform.dividerCurl") }}</a-divider>
      <pre class="code-block">{{ curlExample }}</pre>

      <a-divider orientation="left">{{ t("ai.openPlatform.dividerResult") }}</a-divider>
      <pre class="code-block">{{ responseText }}</pre>
    </a-card>
  </a-space>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";
import { message } from "ant-design-vue";
import { API_BASE } from "@/services/api-core";
import { getTenantId } from "@/utils/auth";

const { t } = useI18n();

const patToken = ref("");
const agentId = ref<number | undefined>(undefined);
const responseText = ref("");
const loadingBots = ref(false);
const loadingChat = ref(false);

const curlExample = computed(() => {
  const token = patToken.value || "<PAT_TOKEN>";
  const tenantId = getTenantId() || "<TENANT_ID>";
  const demoMsg = t("ai.openPlatform.curlDemoMessageShort");
  return [
    "curl -X POST \\",
    `  '${window.location.origin}${API_BASE}/open/chat/completions' \\`,
    `  -H 'Authorization: Bearer ${token}' \\`,
    `  -H 'X-Tenant-Id: ${tenantId}' \\`,
    "  -H 'Content-Type: application/json' \\",
    `  -d '{"agentId": ${agentId.value ?? 1}, "message": ${JSON.stringify(demoMsg)}, "conversationId": null, "enableRag": false}'`
  ].join("\n");
});

async function callOpenApi(path: string, init: RequestInit) {
  const tenantId = getTenantId();
  if (!tenantId) {
    throw new Error(t("ai.openPlatform.errNoTenant"));
  }

  if (!patToken.value) {
    throw new Error(t("ai.openPlatform.errNoPat"));
  }

  const headers = new Headers(init.headers ?? {});
  headers.set("Authorization", `Bearer ${patToken.value}`);
  headers.set("X-Tenant-Id", tenantId);
  if (!headers.has("Content-Type") && init.body) {
    headers.set("Content-Type", "application/json");
  }

  const response = await fetch(`${API_BASE}${path}`, {
    ...init,
    headers
  });
  const text = await response.text();
  if (!response.ok) {
    throw new Error(text || t("ai.openPlatform.errRequest", { status: response.status }));
  }

  responseText.value = text || t("ai.openPlatform.emptyResponse");
}

async function loadBots() {
  loadingBots.value = true;
  try {
    await callOpenApi("/open/bots?PageIndex=1&PageSize=20", { method: "GET" });
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.openPlatform.botsFailed"));
  } finally {
    loadingBots.value = false;
  }
}

async function sendChat() {
  if (!agentId.value) {
    message.warning(t("ai.openPlatform.warnAgentId"));
    return;
  }

  loadingChat.value = true;
  try {
    await callOpenApi("/open/chat/completions", {
      method: "POST",
      body: JSON.stringify({
        agentId: agentId.value,
        message: t("ai.openPlatform.curlDemoMessage"),
        conversationId: null,
        enableRag: false
      })
    });
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.openPlatform.chatFailed"));
  } finally {
    loadingChat.value = false;
  }
}
</script>

<style scoped>
.code-block {
  margin: 0;
  padding: 12px;
  border-radius: 8px;
  background: #fafafa;
  white-space: pre-wrap;
  word-break: break-all;
}
</style>
