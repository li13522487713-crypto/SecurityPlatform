<template>
  <div class="register-page">
    <a-card title="立即注册" class="register-card">
      <a-form layout="vertical" :model="form" :rules="rules" @finish="onSubmit">
        <a-form-item label="租户 ID" name="tenantId">
          <a-input v-model:value="form.tenantId" />
        </a-form-item>
        <a-form-item label="账号" name="username">
          <a-input v-model:value="form.username" />
        </a-form-item>
        <a-form-item label="密码" name="password">
          <a-input-password v-model:value="form.password" />
        </a-form-item>
        <a-form-item label="确认密码" name="confirmPassword">
          <a-input-password v-model:value="form.confirmPassword" />
        </a-form-item>
        <a-form-item label="验证码（可选）" name="captchaCode">
          <a-input v-model:value="form.captchaCode" />
        </a-form-item>
        <a-form-item>
          <a-space>
            <a-button type="primary" html-type="submit" :loading="loading">注册</a-button>
            <a-button @click="router.push('/login')">返回登录</a-button>
          </a-space>
        </a-form-item>
      </a-form>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref } from "vue";
import { message } from "ant-design-vue";
import { useRouter } from "vue-router";
import { register } from "@/services/api";

const router = useRouter();
const loading = ref(false);
const form = reactive({
  tenantId: "00000000-0000-0000-0000-000000000001",
  username: "",
  password: "",
  confirmPassword: "",
  captchaKey: "",
  captchaCode: ""
});

const validateConfirmPassword = async (_rule: any, value: string) => {
  if (value === "") {
    return Promise.reject("请输入确认密码");
  } else if (value !== form.password) {
    return Promise.reject("两次输入的密码不一致");
  }
  return Promise.resolve();
};

const rules = {
  tenantId: [
    { required: true, message: "请输入租户 ID" },
    { pattern: /^[0-9a-fA-F-]{36}$/, message: "租户 ID 格式无效，请输入 GUID" }
  ],
  username: [
    { required: true, message: "请输入账号" },
    { min: 2, max: 64, message: "账号长度必须介于 2 和 64 之间" }
  ],
  password: [
    { required: true, message: "请输入密码" },
    { min: 8, max: 128, message: "密码长度不能小于 8" }
  ],
  confirmPassword: [{ required: true, validator: validateConfirmPassword }]
};

async function onSubmit() {
  loading.value = true;
  try {
    await register(form.tenantId.trim(), {
      username: form.username.trim(),
      password: form.password,
      confirmPassword: form.confirmPassword,
      captchaKey: form.captchaKey || undefined,
      captchaCode: form.captchaCode || undefined
    });
    message.success(`恭喜你，您的账号 ${form.username} 注册成功！`);
    router.push("/login");
  } catch (error) {
    message.error((error as Error).message || "注册失败");
  } finally {
    loading.value = false;
  }
}
</script>

<style scoped>
.register-page {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
}

.register-card {
  width: 460px;
}
</style>
