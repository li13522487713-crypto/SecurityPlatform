<template>
  <section class="appbridge-page">
    <h2>暴露策略管理</h2>
    <a-form layout="vertical">
      <a-form-item label="暴露数据集（逗号分隔）">
        <a-input v-model:value="exposedDataSetsText" />
      </a-form-item>
      <a-form-item label="允许命令（逗号分隔）">
        <a-input v-model:value="allowedCommandsText" />
      </a-form-item>
      <a-form-item label="脱敏字段（users，逗号分隔）">
        <a-input v-model:value="userMaskFieldsText" />
      </a-form-item>
      <a-space>
        <a-button type="primary" :loading="saving" @click="save">保存</a-button>
        <a-button @click="reload">刷新</a-button>
      </a-space>
    </a-form>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useRoute } from "vue-router";
import { message } from "ant-design-vue";
import { getExposurePolicy, updateExposurePolicy } from "@/services/api-appbridge";

const route = useRoute();
const appInstanceId = computed(() => String(route.params.appInstanceId ?? ""));
const saving = ref(false);

const exposedDataSetsText = ref("");
const allowedCommandsText = ref("");
const userMaskFieldsText = ref("");

function normalizeCsv(value: string): string[] {
  return value
    .split(",")
    .map((item) => item.trim())
    .filter((item) => item.length > 0);
}

async function reload() {
  if (!appInstanceId.value) return;
  const policy = await getExposurePolicy(appInstanceId.value);
  exposedDataSetsText.value = policy.exposedDataSets.join(", ");
  allowedCommandsText.value = policy.allowedCommands.join(", ");
  userMaskFieldsText.value = (policy.maskPolicies.users ?? []).join(", ");
}

async function save() {
  if (!appInstanceId.value) return;
  saving.value = true;
  try {
    await updateExposurePolicy(appInstanceId.value, {
      exposedDataSets: normalizeCsv(exposedDataSetsText.value),
      allowedCommands: normalizeCsv(allowedCommandsText.value),
      maskPolicies: {
        users: normalizeCsv(userMaskFieldsText.value)
      }
    });
    message.success("保存成功");
    await reload();
  } catch (error) {
    message.error((error as Error).message || "保存失败");
  } finally {
    saving.value = false;
  }
}

onMounted(() => {
  void reload();
});
</script>

<style scoped>
.appbridge-page {
  display: flex;
  flex-direction: column;
  gap: 12px;
}
</style>
