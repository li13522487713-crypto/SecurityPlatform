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
import { clearAuthStorage, getTenantId, setAccessToken, setAuthProfile, setTenantId } from "@/utils/auth";

const router = useRouter();
const loading = ref(false);

const defaultTenantId = "00000000-0000-0000-0000-000000000001";
const form = reactive({
  tenantId: getTenantId() ?? defaultTenantId,
  username: "",
  password: ""
});

const onFinish = async () => {
  loading.value = true;
  try {
    const result = await createToken(form.tenantId, form.username, form.password);
    setAccessToken(result.accessToken);
    setTenantId(form.tenantId);
    const profile = await getCurrentUser();
    setAuthProfile(profile);
    router.push("/");
  } catch (error) {
    clearAuthStorage();
    message.error((error as Error).message || "登录失败");
  } finally {
    loading.value = false;
  }
};
</script>
