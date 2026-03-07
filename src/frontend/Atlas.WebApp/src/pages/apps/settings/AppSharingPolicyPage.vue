<template>
  <a-card title="共享策略" :loading="loading">
    <a-alert
      v-if="hasIndependentMode"
      type="warning"
      show-icon
      message="当前已启用独立基础数据策略，请确保应用数据源可用。"
      style="margin-bottom: 16px"
    />

    <a-form layout="vertical">
      <a-form-item label="用户账号来源">
        <a-space>
          <a-switch v-model:checked="policy.useSharedUsers" />
          <span>{{ policy.useSharedUsers ? "继承平台" : "应用独立" }}</span>
        </a-space>
      </a-form-item>
      <a-form-item label="角色权限来源">
        <a-space>
          <a-switch v-model:checked="policy.useSharedRoles" />
          <span>{{ policy.useSharedRoles ? "继承平台" : "应用独立" }}</span>
        </a-space>
      </a-form-item>
      <a-form-item label="部门组织来源">
        <a-space>
          <a-switch v-model:checked="policy.useSharedDepartments" />
          <span>{{ policy.useSharedDepartments ? "继承平台" : "应用独立" }}</span>
        </a-space>
      </a-form-item>
    </a-form>

    <a-button type="primary" :loading="saving" @click="handleSave">保存</a-button>
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import { useRoute } from "vue-router";
import { message } from "ant-design-vue";
import type { AppSharingPolicy } from "@/types/lowcode";
import { getAppSharingPolicy, updateAppSharingPolicy } from "@/services/lowcode";

const route = useRoute();
const appId = route.params.appId as string;

const loading = ref(false);
const saving = ref(false);
const policy = reactive<AppSharingPolicy>({
  useSharedUsers: true,
  useSharedRoles: true,
  useSharedDepartments: true
});

const hasIndependentMode = computed(() =>
  !policy.useSharedUsers || !policy.useSharedRoles || !policy.useSharedDepartments
);

const loadPolicy = async () => {
  loading.value = true;
  try {
    const data = await getAppSharingPolicy(appId);
    policy.useSharedUsers = data.useSharedUsers;
    policy.useSharedRoles = data.useSharedRoles;
    policy.useSharedDepartments = data.useSharedDepartments;
  } catch (error) {
    message.error((error as Error).message || "加载共享策略失败");
  } finally {
    loading.value = false;
  }
};

const handleSave = async () => {
  saving.value = true;
  try {
    await updateAppSharingPolicy(appId, {
      useSharedUsers: policy.useSharedUsers,
      useSharedRoles: policy.useSharedRoles,
      useSharedDepartments: policy.useSharedDepartments
    });
    message.success("共享策略已保存");
  } catch (error) {
    message.error((error as Error).message || "保存失败");
  } finally {
    saving.value = false;
  }
};

onMounted(loadPolicy);
</script>
