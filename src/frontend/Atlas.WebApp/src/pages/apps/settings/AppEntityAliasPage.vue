<template>
  <a-card title="实体别名" :loading="loading">
    <a-table :data-source="rows" :pagination="false" row-key="entityType">
      <a-table-column title="实体类型" data-index="entityType" key="entityType" width="180px" />
      <a-table-column title="单数别名" key="singularAlias">
        <template #default="{ record }">
          <a-input v-model:value="record.singularAlias" placeholder="请输入单数别名" />
        </template>
      </a-table-column>
      <a-table-column title="复数别名" key="pluralAlias">
        <template #default="{ record }">
          <a-input v-model:value="record.pluralAlias" placeholder="请输入复数别名（可选）" />
        </template>
      </a-table-column>
    </a-table>

    <div class="alias-actions">
      <a-button type="primary" :loading="saving" @click="handleSave">保存</a-button>
    </div>
  </a-card>
</template>

<script setup lang="ts">
import { onMounted, ref } from "vue";
import { useRoute } from "vue-router";
import { message } from "ant-design-vue";
import type { AppEntityAlias } from "@/types/lowcode";
import { getAppEntityAliases, updateAppEntityAliases } from "@/services/lowcode";

const route = useRoute();
const appId = route.params.appId as string;

const loading = ref(false);
const saving = ref(false);
const rows = ref<AppEntityAlias[]>([
  { entityType: "user", singularAlias: "用户", pluralAlias: "用户列表" },
  { entityType: "role", singularAlias: "角色", pluralAlias: "角色列表" },
  { entityType: "department", singularAlias: "部门", pluralAlias: "部门列表" }
]);

const loadAliases = async () => {
  loading.value = true;
  try {
    const aliases = await getAppEntityAliases(appId);
    if (aliases.length > 0) {
      const map = new Map(aliases.map(item => [item.entityType.toLowerCase(), item]));
      rows.value = rows.value.map(row => map.get(row.entityType) ?? row);
    }
  } catch (error) {
    message.error((error as Error).message || "加载实体别名失败");
  } finally {
    loading.value = false;
  }
};

const handleSave = async () => {
  const invalid = rows.value.some(item => !item.singularAlias?.trim());
  if (invalid) {
    message.warning("请先填写所有单数别名");
    return;
  }

  saving.value = true;
  try {
    await updateAppEntityAliases(appId, rows.value.map(item => ({
      entityType: item.entityType,
      singularAlias: item.singularAlias.trim(),
      pluralAlias: item.pluralAlias?.trim() || undefined
    })));
    message.success("实体别名已保存");
  } catch (error) {
    message.error((error as Error).message || "保存失败");
  } finally {
    saving.value = false;
  }
};

onMounted(loadAliases);
</script>

<style scoped>
.alias-actions {
  margin-top: 16px;
}
</style>
