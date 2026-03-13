<template>
  <a-space direction="vertical" style="width: 100%" :size="16">
    <a-card title="Open Platform（PAT）" :bordered="false">
      <a-alert
        type="info"
        show-icon
        message="先在“个人访问令牌”页面创建 PAT，并赋予 open:* 或对应 open:xxx scope。"
      />

      <a-form layout="vertical" style="margin-top: 16px">
        <a-form-item label="PAT Token">
          <a-input-password v-model:value="patToken" placeholder="pat_xxx..." />
        </a-form-item>
        <a-form-item label="Agent ID（用于 Chat 示例）">
          <a-input-number v-model:value="agentId" :min="1" style="width: 200px" />
        </a-form-item>
        <a-form-item>
          <a-space>
            <a-button :loading="loadingBots" @click="loadBots">请求 /open/bots</a-button>
            <a-button type="primary" :loading="loadingChat" @click="sendChat">请求 /open/chat/completions</a-button>
          </a-space>
        </a-form-item>
      </a-form>

      <a-divider orientation="left">示例代码（cURL）</a-divider>
      <pre class="code-block">{{ curlExample }}</pre>

      <a-divider orientation="left">请求结果</a-divider>
      <pre class="code-block">{{ responseText }}</pre>
    </a-card>
  </a-space>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import { message } from "ant-design-vue";
import { API_BASE } from "@/services/api-core";
import { getTenantId } from "@/utils/auth";

const patToken = ref("");
const agentId = ref<number | undefined>(undefined);
const responseText = ref("");
const loadingBots = ref(false);
const loadingChat = ref(false);

const curlExample = computed(() => {
  const token = patToken.value || "<PAT_TOKEN>";
  const tenantId = getTenantId() || "<TENANT_ID>";
  return [
    "curl -X POST \\",
    `  '${window.location.origin}${API_BASE}/open/chat/completions' \\`,
    `  -H 'Authorization: Bearer ${token}' \\`,
    `  -H 'X-Tenant-Id: ${tenantId}' \\`,
    "  -H 'Content-Type: application/json' \\",
    `  -d '{"agentId": ${agentId.value ?? 1}, "message": "你好", "conversationId": null, "enableRag": false}'`
  ].join("\n");
});

async function callOpenApi(path: string, init: RequestInit) {
  const tenantId = getTenantId();
  if (!tenantId) {
    throw new Error("缺少租户ID，请先登录");
  }

  if (!patToken.value) {
    throw new Error("请先输入 PAT Token");
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
    throw new Error(text || `请求失败（${response.status}）`);
  }

  responseText.value = text || "<empty>";
}

async function loadBots() {
  loadingBots.value = true;
  try {
    await callOpenApi("/open/bots?PageIndex=1&PageSize=20", { method: "GET" });
  } catch (error: unknown) {
    message.error((error as Error).message || "调用 open bots 失败");
  } finally {
    loadingBots.value = false;
  }
}

async function sendChat() {
  if (!agentId.value) {
    message.warning("请先输入 Agent ID");
    return;
  }

  loadingChat.value = true;
  try {
    await callOpenApi("/open/chat/completions", {
      method: "POST",
      body: JSON.stringify({
        agentId: agentId.value,
        message: "你好，请简单介绍你的能力",
        conversationId: null,
        enableRag: false
      })
    });
  } catch (error: unknown) {
    message.error((error as Error).message || "调用 open chat 失败");
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
