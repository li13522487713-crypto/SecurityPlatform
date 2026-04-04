<template>
  <div class="app-login">
    <a-card class="app-login-card" :title="`应用登录 · ${appKey}`">
      <a-alert v-if="errorMessage" type="error" show-icon :message="errorMessage" class="app-login-error" />
      <a-form layout="vertical" :model="form" @finish="handleSubmit">
        <a-form-item label="租户 ID" name="tenantId" :rules="[{ required: true, message: '请输入租户 ID' }]">
          <a-input v-model:value="form.tenantId" placeholder="00000000-0000-0000-0000-000000000001" />
        </a-form-item>
        <a-form-item label="用户名" name="username" :rules="[{ required: true, message: '请输入用户名' }]">
          <a-input v-model:value="form.username" placeholder="admin" />
        </a-form-item>
        <a-form-item label="密码" name="password" :rules="[{ required: true, message: '请输入密码' }]">
          <a-input-password v-model:value="form.password" placeholder="请输入密码" />
        </a-form-item>
        <a-button type="primary" html-type="submit" block :loading="submitting">登录并进入应用</a-button>
      </a-form>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { getTenantId } from "@/utils/auth";
import { loginByAppEntry } from "@/services/auth/app-login-entry";

const route = useRoute();
const router = useRouter();
const submitting = ref(false);
const errorMessage = ref("");
const appKey = String(route.params.appKey ?? "");

const form = reactive({
  tenantId: getTenantId() ?? "",
  username: "",
  password: ""
});

async function handleSubmit() {
  submitting.value = true;
  errorMessage.value = "";
  try {
    await loginByAppEntry(form.tenantId, form.username.trim(), form.password);
    const rawRedirect = route.query.redirect;
    const redirect = typeof rawRedirect === "string" && rawRedirect.startsWith("/")
      ? rawRedirect
      : `/app-host/${encodeURIComponent(appKey)}/entry`;
    await router.replace(redirect);
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : "登录失败";
  } finally {
    submitting.value = false;
  }
}
</script>

<style scoped>
.app-login {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(180deg, #f5f7fb 0%, #e8edf7 100%);
  padding: 24px;
}

.app-login-card {
  width: min(420px, 100%);
}

.app-login-error {
  margin-bottom: 16px;
}
</style>
