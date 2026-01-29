<template>
  <a-card title="登录" class="page-card login-card">
    <a-form layout="vertical" @finish="onFinish">
      <a-form-item label="租户ID" name="tenantId" :rules="[{ required: true, message: '请输入租户ID' }]">
        <a-input v-model:value="form.tenantId" placeholder="GUID" />
      </a-form-item>
      <a-form-item label="用户名" name="username" :rules="[{ required: true, message: '请输入用户名' }]">
        <a-input v-model:value="form.username" />
      </a-form-item>
      <a-form-item label="密码" name="password" :rules="[{ required: true, message: '请输入密码' }]">
        <a-input-password v-model:value="form.password" />
      </a-form-item>
      <a-space style="width: 100%">
        <a-button type="primary" html-type="submit" :loading="loading">登录</a-button>
        <a-button @click="goToWorkflowDesigner">工作流设计器</a-button>
      </a-space>
    </a-form>
  </a-card>
</template>

<script setup lang="ts">
import { message } from "ant-design-vue";
import { reactive, ref } from "vue";
import { useRouter } from "vue-router";
import { createToken } from "@/services/api";

const router = useRouter();
const loading = ref(false);

const form = reactive({
  tenantId: localStorage.getItem("tenant_id") ?? "",
  username: "",
  password: ""
});

const onFinish = async () => {
  loading.value = true;
  try {
    const result = await createToken(form.tenantId, form.username, form.password);
    localStorage.setItem("access_token", result.accessToken);
    localStorage.setItem("tenant_id", form.tenantId);
    router.push("/");
  } catch (error) {
    message.error((error as Error).message || "登录失败");
  } finally {
    loading.value = false;
  }
};

const goToWorkflowDesigner = () => {
  if (!form.tenantId.trim()) {
    message.warning("请输入租户ID");
    return;
  }
  // 允许不登录进入工作流页面，但必须先有租户ID（用于后续 API Header）
  localStorage.setItem("tenant_id", form.tenantId.trim());
  router.push("/workflow/designer");
};
</script>