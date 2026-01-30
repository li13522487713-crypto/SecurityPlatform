<template>
  <a-card title="登录" class="page-card login-card">
    <a-form layout="vertical" :model="form" @finish="onFinish">
      <a-form-item label="租户ID" name="tenantId" :rules="[{ required: true, message: '请输入租户ID' }]">
        <a-input v-model:value="form.tenantId" placeholder="GUID" />
      </a-form-item>
      <a-form-item label="用户名" name="username" :rules="[{ required: true, message: '请输入用户名' }]">
        <a-input v-model:value="form.username" />
      </a-form-item>
      <a-form-item label="密码" name="password" :rules="[{ required: true, message: '请输入密码' }]">
        <a-input-password v-model:value="form.password" />
      </a-form-item>
      <a-button type="primary" html-type="submit" :loading="loading">登录</a-button>
    </a-form>
  </a-card>
</template>

<script setup lang="ts">
import { message } from "ant-design-vue";
import { reactive, ref } from "vue";
import { useRouter } from "vue-router";
import { createToken, getCurrentUser } from "@/services/api";

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
    const profile = await getCurrentUser();
    localStorage.setItem("auth_profile", JSON.stringify(profile));
    router.push("/");
  } catch (error) {
    localStorage.removeItem("access_token");
    localStorage.removeItem("tenant_id");
    localStorage.removeItem("auth_profile");
    message.error((error as Error).message || "登录失败");
  } finally {
    loading.value = false;
  }
};
</script>
