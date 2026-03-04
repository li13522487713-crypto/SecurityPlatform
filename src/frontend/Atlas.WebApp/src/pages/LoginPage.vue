<template>
  <div class="login-page">
    <header class="login-header">
      <div class="brand-area">
        <div class="logo-circle">Atlas</div>
        <div>
          <h1>安全控制台</h1>
          <p>统一安全管理 & 运维管控</p>
        </div>
      </div>
      <div class="header-links">
        <a href="https://docs.securityplatform.local" target="_blank" rel="noopener">帮助</a>
        <a href="https://docs.securityplatform.local" target="_blank" rel="noopener">文档</a>
      </div>
    </header>

    <main class="login-main">
      <section class="promo-panel">
        <h2>智控 · 守护 · 可审计</h2>
        <p>从账号到权限，从审计到告警，提供一套可复用的控制台管理体验。</p>
        <ul>
          <li>统一身份 + 租户/组织可视化管理</li>
          <li>实时审计 + 风险策略自动落地</li>
        </ul>
      </section>

      <section class="card-panel">
        <a-card title="欢迎登录" class="login-card" :bordered="false">
          <div v-if="errorMessage" class="error-banner">
            <span class="error-dot" aria-hidden="true">!</span>
            <span>{{ errorMessage }}</span>
            <span v-if="cooldownSeconds > 0" class="cooldown">（请 {{ cooldownSeconds }} 秒后再试）</span>
          </div>

          <a-form
            layout="vertical"
            :model="form"
            class="login-form"
            :disabled="loading"
            @finish="handleSubmit"
          >
            <a-form-item
              label="租户 / 组织"
              name="tenantId"
              :rules="[{ required: true, message: '请输入租户 / 组织ID' }]"
            >
              <a-input
                v-model:value="form.tenantId"
                placeholder="请输入租户 / 组织 ID"
                list="tenant-history"
                allow-clear
                autocomplete="off"
                @focus="errorMessage = ''"
              />
              <datalist id="tenant-history">
                <option
                  v-for="item in tenantHistoryOptions"
                  :key="item.id"
                  :value="item.id"
                >
                  {{ item.label }}
                </option>
              </datalist>
              <div class="tenant-tags">
                <span
                  v-for="item in tenantHistoryOptions"
                  :key="item.id"
                  class="tenant-tag"
                  @click="handleSelectTenant(item.id)"
                >
                  {{ item.label }}
                </span>
              </div>
            </a-form-item>

            <a-form-item
              label="账号"
              name="username"
              :rules="[{ required: true, message: '请输入账号' }]"
            >
              <a-input
                v-model:value="form.username"
                placeholder="请输入手机号/邮箱/用户名"
                allow-clear
                autocomplete="username"
                @focus="errorMessage = ''"
              />
            </a-form-item>

            <a-form-item
              label="密码"
              name="password"
              :rules="[{ required: true, message: '请输入密码' }]"
            >
              <a-input-password
                v-model:value="form.password"
                placeholder="请输入密码"
                autocomplete="current-password"
                @keydown="handleCapsLockEvent"
                @keyup="handleCapsLockEvent"
                @blur="capsLockOn = false"
                @focus="errorMessage = ''"
              />
              <div v-if="capsLockOn" class="caps-tip">
                Caps Lock 已开启，可能影响密码输入
              </div>
            </a-form-item>

            <a-form-item
              v-if="isCaptchaVisible"
              label="图片验证码"
              name="captcha"
              :rules="[{ required: true, message: '请输入验证码' }]"
            >
              <div class="captcha-row">
                <a-input
                  v-model:value="form.captcha"
                  placeholder="请输入验证码"
                  autocomplete="off"
                  @focus="errorMessage = ''"
                />
                <div
                  class="captcha-image"
                  @click="refreshCaptcha"
                  :style="captchaStyle"
                >
                  <span>验证码</span>
                  <span class="captcha-tips">点击刷新</span>
                </div>
              </div>
            </a-form-item>

            <a-form-item style="margin-bottom: 8px">
              <div class="remember-row">
                <a-checkbox v-model:checked="form.rememberMe">记住我（30天内保持登录）</a-checkbox>
              </div>
            </a-form-item>

            <a-form-item>
              <a-button
                type="primary"
                block
                html-type="submit"
                :loading="loading"
                :disabled="isSubmitDisabled"
              >
                <span v-if="!loading">{{ cooldownSeconds > 0 ? `请稍候 (${cooldownSeconds}s)` : "登录" }}</span>
                <span v-else>登录中</span>
              </a-button>
            </a-form-item>

            <div class="secondary-actions">
              <a href="/password-reset">忘记密码</a>
              <router-link to="/register">还没有账号？立即注册</router-link>
            </div>
          </a-form>
        </a-card>
      </section>
    </main>

    <footer class="login-footer">
      <span>隐私政策</span>
      <span>用户协议</span>
      <span>版本 v1.0.2</span>
      <span>备案：沪ICP备xxxxxx号</span>
    </footer>
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { getCaptcha } from "@/services/api";
import { useUserStore } from "@/stores/user";
import type { RequestOptions } from "@/services/api";
import {
  clearAuthStorage,
  getTenantId,
  setAccessToken,
  setAuthProfile,
  setRefreshToken,
  setTenantId
} from "@/utils/auth";

interface TenantHistoryItem {
  id: string;
  label: string;
}

interface LoginApiError extends Error {
  status?: number;
  payload?: {
    code?: string;
    message?: string;
    traceId?: string;
  } | null;
}

const TENANT_HISTORY_KEY = "atlas-login-tenant-history";
const DEFAULT_TENANT_ID = "00000000-0000-0000-0000-000000000001";
const CAPTCHA_THRESHOLD = 3;
const COOLDOWN_THRESHOLD = 5;
const COOLDOWN_DURATION = 30;

const userStore = useUserStore();
const route = useRoute();
const router = useRouter();
const loading = ref(false);
const errorMessage = ref("");
const failedAttempts = ref(0);
const capsLockOn = ref(false);
const cooldownSeconds = ref(0);
const captchaSeed = ref(Date.now());
const tenantHistory = ref<TenantHistoryItem[]>([]);
let cooldownTimer: number | undefined;

const REMEMBER_ME_KEY = "atlas-login-remember-me";

const form = reactive({
  tenantId: getTenantId() ?? DEFAULT_TENANT_ID,
  username: "",
  password: "",
  captcha: "",
  rememberMe: localStorage.getItem(REMEMBER_ME_KEY) === "true"
});

// 验证码 API 返回的 key 和图片
const captchaKey = ref<string>("");
const captchaImageSrc = ref<string>("");

const isCaptchaVisible = computed(() => failedAttempts.value >= CAPTCHA_THRESHOLD);
const isSubmitDisabled = computed(
  () =>
    loading.value ||
    cooldownSeconds.value > 0 ||
    !form.username.trim() ||
    !form.password ||
    (isCaptchaVisible.value && !form.captcha.trim())
);
const tenantHistoryOptions = computed(() => tenantHistory.value);
const captchaStyle = computed(() => ({
  backgroundImage: captchaImageSrc.value ? `url('${captchaImageSrc.value}')` : `url('https://dummyimage.com/120x40/ced4da/6c757d.png&text=%E9%AA%8C%E8%AF%81%E7%A0%81&seed=${captchaSeed.value}')`,
  backgroundSize: "cover"
}));

const loadTenantHistory = () => {
  const raw = localStorage.getItem(TENANT_HISTORY_KEY);
  try {
    if (raw) {
      const parsed = JSON.parse(raw) as TenantHistoryItem[];
      if (Array.isArray(parsed) && parsed.length > 0) {
        tenantHistory.value = parsed;
        if (!form.tenantId && parsed[0]) {
          form.tenantId = parsed[0].id;
        }
        return;
      }
    }
  } catch {
    // ignore malformed history
  }

  tenantHistory.value = [{ id: DEFAULT_TENANT_ID, label: "默认租户" }];
};

const persistTenantHistory = (tenantId: string) => {
  if (!tenantId) return;
  const label = tenantId === DEFAULT_TENANT_ID ? "默认租户" : tenantId;
  const existingIndex = tenantHistory.value.findIndex((item) => item.id === tenantId);
  const item = { id: tenantId, label };
  if (existingIndex >= 0) {
    tenantHistory.value.splice(existingIndex, 1);
  }
  tenantHistory.value.unshift(item);
  tenantHistory.value = tenantHistory.value.slice(0, 5);
  localStorage.setItem(TENANT_HISTORY_KEY, JSON.stringify(tenantHistory.value));
};

const handleCapsLockEvent = (event: KeyboardEvent) => {
  if (typeof event.getModifierState === "function") {
    capsLockOn.value = event.getModifierState("CapsLock");
  }
};

const startCooldown = () => {
  cooldownSeconds.value = COOLDOWN_DURATION;
  window.clearInterval(cooldownTimer);
  cooldownTimer = window.setInterval(() => {
    cooldownSeconds.value -= 1;
    if (cooldownSeconds.value <= 0) {
      window.clearInterval(cooldownTimer);
      cooldownSeconds.value = 0;
    }
  }, 1000);
};

const refreshCaptcha = async () => {
  captchaSeed.value = Date.now();
  if (!form.tenantId) return;
  try {
    const result = await getCaptcha(form.tenantId);
    captchaKey.value = result.captchaKey;
    captchaImageSrc.value = result.captchaImage;
  } catch {
    // 降级显示占位图
    captchaKey.value = "";
    captchaImageSrc.value = "";
  }
  form.captcha = "";
};

const normalizeError = (error: unknown) => {
  const loginError = error as LoginApiError;
  const code = loginError?.payload?.code ?? "";
  const traceId = loginError?.payload?.traceId;
  const raw = error instanceof Error ? error.message : "登录失败";

  if (code === "INVALID_CREDENTIALS" || raw.includes("账号或密码")) {
    return "账号或密码错误，请重试";
  }
  if (code === "ACCOUNT_LOCKED") {
    return "账号已被锁定，请联系管理员或稍后再试";
  }
  if (code === "PASSWORD_EXPIRED") {
    return "密码已过期，请联系管理员重置密码后登录";
  }
  if (code === "TENANT_NOT_FOUND") {
    return "租户不存在，请检查租户ID是否正确";
  }
  if (code === "VALIDATION_ERROR") {
    return raw || "参数校验失败，请检查输入后重试";
  }
  if (raw.toLowerCase().includes("网络")) {
    return "网络异常，请稍后再试";
  }
  if (traceId) {
    return `${raw}（traceId: ${traceId}）`;
  }
  return raw;
};

const handleSelectTenant = (tenantId: string) => {
  form.tenantId = tenantId;
};

const handleSubmit = async () => {
  errorMessage.value = "";
  loading.value = true;
  try {
    clearAuthStorage();
    const tokenOptions: RequestOptions = { suppressErrorMessage: true };

    if (isCaptchaVisible.value && !captchaKey.value) {
      await refreshCaptcha();
    }

    await userStore.login(
      form.tenantId,
      form.username.trim(),
      form.password,
      tokenOptions,
      {
        captchaKey: isCaptchaVisible.value ? captchaKey.value : undefined,
        captchaCode: isCaptchaVisible.value ? form.captcha.trim() : undefined,
        rememberMe: form.rememberMe
      }
    );
    await userStore.getInfo();
    persistTenantHistory(form.tenantId);
    localStorage.setItem(REMEMBER_ME_KEY, String(form.rememberMe));
    failedAttempts.value = 0;
    cooldownSeconds.value = 0;
    errorMessage.value = "";
    const redirect = route.query.redirect as string;
    router.push(redirect || "/");
  } catch (error) {
    clearAuthStorage();
    failedAttempts.value += 1;
    errorMessage.value = normalizeError(error);
    if (failedAttempts.value >= COOLDOWN_THRESHOLD) {
      startCooldown();
    }
    if (isCaptchaVisible.value) {
      await refreshCaptcha();
    } else if (failedAttempts.value >= CAPTCHA_THRESHOLD) {
      // 刚触发验证码阈值，立即加载
      await refreshCaptcha();
    }
  } finally {
    loading.value = false;
  }
};

onMounted(loadTenantHistory);
onBeforeUnmount(() => {
  window.clearInterval(cooldownTimer);
});
</script>

<style scoped>
.login-page {
  min-height: 100vh;
  display: flex;
  flex-direction: column;
  background: var(--color-bg-base);
}

.login-header {
  padding: var(--spacing-lg) 64px;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.brand-area {
  display: flex;
  align-items: center;
  gap: var(--spacing-md);
}

.logo-circle {
  width: 48px;
  height: 48px;
  border-radius: var(--border-radius-round);
  background: var(--color-primary);
  color: var(--color-text-white);
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 600;
}

.brand-area h1 {
  margin: 0;
  font-size: 22px;
  font-weight: 600;
}

.brand-area p {
  margin: var(--spacing-xs) 0 0;
  color: var(--color-text-tertiary);
  font-size: 14px;
}

.header-links a {
  margin-left: var(--spacing-lg);
  color: var(--color-text-secondary);
  font-size: 14px;
}

.login-main {
  flex: 1;
  display: flex;
  gap: var(--spacing-xl);
  padding: 0 64px var(--spacing-xxl);
  align-items: stretch;
}

.promo-panel {
  flex: 1;
  background: linear-gradient(135deg, var(--color-bg-container) 0%, var(--color-primary-bg) 100%);
  border-radius: var(--border-radius-lg);
  padding: 40px var(--spacing-xxl);
  box-shadow: var(--shadow-md);
}

.promo-panel h2 {
  margin-top: 0;
  font-size: 28px;
  font-weight: 600;
}

.promo-panel p {
  margin: var(--spacing-md) 0;
  color: var(--color-text-secondary);
}

.promo-panel ul {
  padding-left: 20px;
  color: var(--color-text-primary);
  margin: 0;
}

.card-panel {
  width: 420px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.login-card {
  width: 100%;
  border-radius: var(--border-radius-lg);
  box-shadow: var(--shadow-lg);
}

.error-banner {
  display: flex;
  align-items: center;
  gap: 6px;
  background: var(--color-error-bg);
  border: 1px solid var(--color-error-border);
  color: var(--color-error-text);
  padding: 10px var(--spacing-md);
  border-radius: 6px;
  margin-bottom: var(--spacing-md);
  font-size: 14px;
}

.error-dot {
  width: 22px;
  height: 22px;
  border-radius: var(--border-radius-round);
  background: var(--color-error-text);
  color: var(--color-text-white);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 14px;
}

.cooldown {
  margin-left: auto;
  font-size: 12px;
  color: var(--color-text-secondary);
}

.tenant-tags {
  margin-top: var(--spacing-sm);
  display: flex;
  flex-wrap: wrap;
  gap: var(--spacing-sm);
}

.tenant-tag {
  border: 1px dashed var(--color-border-dashed);
  padding: 2px var(--spacing-sm);
  border-radius: 12px;
  font-size: 12px;
  cursor: pointer;
  color: var(--color-text-secondary);
}

.tenant-tag:hover {
  border-color: var(--color-primary);
  color: var(--color-primary);
}

.caps-tip {
  margin-top: var(--spacing-xs);
  color: var(--color-warning);
  font-size: 12px;
}

.captcha-row {
  display: flex;
  align-items: center;
  gap: 12px;
}

.captcha-image {
  width: 120px;
  height: 44px;
  border-radius: var(--border-radius-md);
  background: var(--color-bg-hover);
  display: flex;
  flex-direction: column;
  justify-content: center;
  align-items: center;
  font-size: 12px;
  color: var(--color-text-secondary);
  cursor: pointer;
}

.remember-row {
  display: flex;
  align-items: center;
}

.captcha-tips {
  margin-top: var(--spacing-xs);
  font-size: 10px;
  color: var(--color-text-tertiary);
}

.secondary-actions {
  display: flex;
  justify-content: space-between;
  margin-top: var(--spacing-sm);
}

.secondary-actions a {
  color: var(--color-text-secondary);
  font-size: 14px;
}

.login-footer {
  padding: var(--spacing-md) 64px;
  display: flex;
  gap: var(--spacing-md);
  flex-wrap: wrap;
  font-size: 12px;
  color: var(--color-text-tertiary);
  border-top: 1px solid var(--color-bg-hover);
}

@media screen and (max-width: 1024px) {
  .login-main {
    flex-direction: column;
    padding: 0 var(--spacing-lg) var(--spacing-xxl);
  }

  .promo-panel {
    order: 2;
  }

  .card-panel {
    width: 100%;
    order: 1;
  }
}

@media screen and (max-width: 720px) {
  .login-header,
  .login-footer {
    flex-direction: column;
    align-items: flex-start;
    padding: var(--spacing-md);
  }

  .brand-area {
    gap: 12px;
  }

  .login-card {
    box-shadow: none;
  }

  .card-panel {
    padding: 0;
  }
}
</style>
