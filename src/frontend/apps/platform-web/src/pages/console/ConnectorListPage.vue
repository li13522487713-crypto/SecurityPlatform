<template>
  <PageContainer title="连接器列表">
    <ConnectorConfigForm :items="rows" :loading="loading" />
  </PageContainer>
</template>

<script setup lang="ts">
import { onMounted, ref } from "vue";
import { message } from "ant-design-vue";
import { PageContainer } from "@atlas/shared-ui";
import { createConnectorRequestContext, ConnectorConfigForm, listConnectors, type ConnectorRecord } from "@atlas/connector-core";
import { requestApi } from "@/services/api-core";

const rows = ref<ConnectorRecord[]>([]);
const loading = ref(false);
const context = createConnectorRequestContext({ requestApi });

async function loadConnectors() {
  loading.value = true;
  try {
    rows.value = await listConnectors(context);
  } catch (error) {
    message.error((error as Error).message || "加载连接器失败");
    rows.value = [];
  } finally {
    loading.value = false;
  }
}

onMounted(() => {
  void loadConnectors();
});
</script>
