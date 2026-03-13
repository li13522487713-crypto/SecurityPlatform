<template>
  <div class="register-page">
    <!-- 左侧品牌面板（与 LoginPage 一致） -->
    <aside class="brand-panel">
      <div class="brand-content">
        <div class="brand-logo">
          <div class="logo-icon">
            <svg viewBox="0 0 32 32" fill="none" xmlns="http://www.w3.org/2000/svg">
              <path d="M16 2L28 9v14l-12 7L4 23V9l12-7z" fill="rgba(255,255,255,0.15)" stroke="rgba(255,255,255,0.6)" stroke-width="1.5"/>
              <path d="M16 8l7 4v8l-7 4-7-4v-8l7-4z" fill="rgba(255,255,255,0.25)" stroke="#fff" stroke-width="1.5"/>
              <circle cx="16" cy="16" r="3" fill="#fff"/>
            </svg>
          </div>
          <div class="brand-text">
            <h1>Atlas 安全平台</h1>
            <p>Security Platform</p>
          </div>
        </div>
        <div class="brand-desc">
          <h2>统一安全管理与运维管控</h2>
          <ul>
            <li>统一身份认证 · 多租户组织管理</li>
            <li>实时审计日志 · 风险策略自动落地</li>
            <li>资产清点盘查 · 合规告警可追溯</li>
          </ul>
        </div>
      </div>
      <div class="brand-footer">
        <span>符合等保 2.0 三级要求</span>
      </div>
      <div class="decor decor-1" aria-hidden="true"></div>
      <div class="decor decor-2" aria-hidden="true"></div>
      <div class="decor decor-3" aria-hidden="true"></div>
    </aside>

    <!-- 右侧注册表单 -->
    <main class="form-panel">
      <div class="form-wrapper">
        <div class="mobile-logo">
          <div class="logo-icon logo-icon--sm">
            <svg viewBox="0 0 32 32" fill="none" xmlns="http://www.w3.org/2000/svg">
              <path d="M16 2L28 9v14l-12 7L4 23V9l12-7z" fill="rgba(22,119,255,0.1)" stroke="var(--color-primary)" stroke-width="1.5"/>
              <path d="M16 8l7 4v8l-7 4-7-4v-8l7-4z" fill="rgba(22,119,255,0.15)" stroke="var(--color-primary)" stroke-width="1.5"/>
              <circle cx="16" cy="16" r="3" fill="var(--color-primary)"/>
            </svg>
          </div>
          <span>Atlas 安全平台</span>
        </div>

        <h3 class="form-title">注册账号</h3>
        <p class="form-subtitle">创建您的平台账号</p>

        <a-form
          layout="vertical"
          :model="form"
          :rules="rules"
          class="register-form"
          @finish="onSubmit"
        >
          <a-form-item label="租户 ID" name="tenantId">
            <a-input
              v-model:value="form.tenantId"
              placeholder="请输入租户 ID（GUID 格式）"
              autocomplete="off"
            />
          </a-form-item>

          <a-form-item label="账号" name="username">
            <a-input
              v-model:value="form.username"
              placeholder="请输入用户名（2-64 位）"
              allow-clear
              autocomplete="username"
            />
          </a-form-item>

          <a-form-item label="密码" name="password">
            <a-input-password
              v-model:value="form.password"
              placeholder="请输入密码（至少 8 位）"
              autocomplete="new-password"
            />
          </a-form-item>

          <a-form-item label="确认密码" name="confirmPassword">
            <a-input-password
              v-model:value="form.confirmPassword"
              placeholder="请再次输入密码"
              autocomplete="new-password"
            />
          </a-form-item>

          <a-form-item label="验证码（可选）" name="captchaCode">
            <a-input v-model:value="form.captchaCode" placeholder="如需要请输入验证码" />
          </a-form-item>

          <a-button
            type="primary"
            block
            html-type="submit"
            size="large"
            :loading="loading"
            class="submit-btn"
          >
            注册
          </a-button>

          <div class="login-link">
            已有账号？<router-link to="/login">返回登录</router-link>
          </div>
        </a-form>
      </div>

      <footer class="form-footer">
        <span>隐私政策</span>
        <span class="sep">·</span>
        <span>用户协议</span>
        <span class="sep">·</span>
        <span>v1.0.2</span>
      </footer>
    </main>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref } from "vue";
import { message } from "ant-design-vue";
import { useRouter } from "vue-router";
import { register } from "@/services/api";
import type { RuleObject } from "ant-design-vue/es/form";

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

const validateConfirmPassword = async (_rule: RuleObject, value: string) => {
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
    message.success(`恭喜你，账号 ${form.username} 注册成功！`);
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
  background: #fff;
}

/* ── 左侧品牌面板（与 LoginPage 共用设计语言） ── */
.brand-panel {
  width: 440px;
  min-height: 100vh;
  background: linear-gradient(160deg, #0d47a1 0%, #1565c0 40%, #1e88e5 100%);
  color: #fff;
  display: flex;
  flex-direction: column;
  padding: 48px 40px;
  position: relative;
  overflow: hidden;
  flex-shrink: 0;
}

.brand-content {
  flex: 1;
  display: flex;
  flex-direction: column;
  justify-content: center;
  position: relative;
  z-index: 1;
}

.brand-logo {
  display: flex;
  align-items: center;
  gap: 14px;
  margin-bottom: 48px;
}

.logo-icon {
  width: 44px;
  height: 44px;
  flex-shrink: 0;
}

.logo-icon svg {
  width: 100%;
  height: 100%;
}

.brand-text h1 {
  margin: 0;
  font-size: 22px;
  font-weight: 600;
  letter-spacing: 0.5px;
}

.brand-text p {
  margin: 2px 0 0;
  font-size: 12px;
  opacity: 0.65;
  letter-spacing: 1px;
}

.brand-desc h2 {
  font-size: 26px;
  font-weight: 600;
  margin: 0 0 24px;
  line-height: 1.4;
}

.brand-desc ul {
  list-style: none;
  padding: 0;
  margin: 0;
}

.brand-desc li {
  padding: 8px 0;
  font-size: 14px;
  opacity: 0.85;
  position: relative;
  padding-left: 20px;
}

.brand-desc li::before {
  content: '';
  position: absolute;
  left: 0;
  top: 50%;
  transform: translateY(-50%);
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.6);
}

.brand-footer {
  position: relative;
  z-index: 1;
  font-size: 12px;
  opacity: 0.5;
}

.decor {
  position: absolute;
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.04);
}

.decor-1 { width: 300px; height: 300px; bottom: -80px; right: -80px; }
.decor-2 { width: 180px; height: 180px; top: -40px; right: 60px; }
.decor-3 { width: 100px; height: 100px; bottom: 120px; left: -30px; }

/* ── 右侧表单面板 ── */
.form-panel {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 40px;
  min-height: 100vh;
  background: #fff;
}

.form-wrapper {
  width: 100%;
  max-width: 400px;
}

.mobile-logo {
  display: none;
  align-items: center;
  gap: 10px;
  margin-bottom: 32px;
  font-size: 18px;
  font-weight: 600;
  color: var(--color-text-primary);
}

.logo-icon--sm {
  width: 32px;
  height: 32px;
}

.form-title {
  font-size: 24px;
  font-weight: 600;
  color: var(--color-text-primary);
  margin: 0 0 4px;
}

.form-subtitle {
  margin: 0 0 24px;
  font-size: 14px;
  color: var(--color-text-tertiary);
}

/* ── 注册表单 ── */
.register-form :deep(.ant-form-item) {
  margin-bottom: 18px;
}

.register-form :deep(.ant-input),
.register-form :deep(.ant-input-password .ant-input) {
  height: 40px;
}

.submit-btn {
  height: 44px;
  font-size: 16px;
  border-radius: var(--border-radius-md);
  margin-top: 8px;
}

.login-link {
  text-align: center;
  margin-top: 16px;
  font-size: 14px;
  color: var(--color-text-secondary);
}

.login-link a {
  color: var(--color-primary);
}

/* ── 页脚 ── */
.form-footer {
  margin-top: 48px;
  text-align: center;
  font-size: 12px;
  color: var(--color-text-quaternary);
}

.form-footer .sep {
  margin: 0 6px;
}

/* ── 响应式 ── */
@media screen and (max-width: 960px) {
  .brand-panel {
    display: none;
  }

  .mobile-logo {
    display: flex;
  }

  .form-panel {
    padding: 32px 24px;
  }
}

@media screen and (max-width: 480px) {
  .form-panel {
    padding: 24px 16px;
  }

  .form-title {
    font-size: 20px;
  }

  .form-wrapper {
    max-width: 100%;
  }
}
</style>
