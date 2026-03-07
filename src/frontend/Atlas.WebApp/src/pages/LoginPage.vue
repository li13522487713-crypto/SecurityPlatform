<template>
  <div class="login-page">
    <header class="login-header">
      <div class="brand-area">
        <div class="logo-circle">Atlas</div>
        <div>
          <h1>安全控制台</h1>
          <p>统一安全管理 &amp; 运维管控</p>
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

          <!-- ① 授权证书区（始终可见，在登录表单上方） -->
          <div class="license-section">
            <div v-if="licenseLoading" class="license-loading">
              <a-spin size="small" />
              <span style="margin-left: 8px">正在验证授权证书…</span>
            </div>

            <!-- 已激活 -->
            <template v-else-if="licenseStatusCode === 'Active'">
              <div class="license-active-header">
                <span class="license-badge license-badge--active">✓ 已授权</span>
                <span v-if="licenseStatusInfo?.tenantName" class="license-org">
                  {{ licenseStatusInfo.tenantName }}
                </span>
                <a-tag color="blue" style="margin: 0">{{ licenseStatusInfo?.edition }}</a-tag>
                <span class="license-expire">{{ licenseExpireText }}</span>
                <a class="license-renew-link" @click="showRenewArea = !showRenewArea">
                  {{ showRenewArea ? '收起' : '更换证书' }}
                </a>
              </div>
              <div v-if="showRenewArea" class="license-upload-area">
                <div v-if="licenseActivateResult" style="margin-bottom: 8px">
                  <a-alert
                    :type="licenseActivateResult.success ? 'success' : 'error'"
                    :message="licenseActivateResult.message"
                    closable
                    @close="licenseActivateResult = null"
                  />
                </div>
                <a-upload
                  :before-upload="handleLicenseFileSelect"
                  :show-upload-list="false"
                  accept=".atlaslicense,.lic,.txt"
                >
                  <a-button size="small" :loading="licenseActivating">
                    <template #icon><upload-outlined /></template>
                    选择证书文件
                  </a-button>
                </a-upload>
              </div>
            </template>

            <!-- 未激活 / 已过期 -->
            <template v-else>
              <div class="license-inactive-header">
                <span
                  class="license-badge"
                  :class="licenseStatusCode === 'Expired' ? 'license-badge--expired' : 'license-badge--none'"
                >
                  {{ licenseStatusCode === 'Expired' ? '⚠ 授权已过期' : '未激活' }}
                </span>
                <span class="license-hint">
                  {{ licenseStatusCode === 'Expired' ? '请续签证书后方可使用' : '请上传授权证书以启用平台' }}
                </span>
              </div>
              <div v-if="licenseActivateResult" style="margin: 8px 0">
                <a-alert
                  :type="licenseActivateResult.success ? 'success' : 'error'"
                  :message="licenseActivateResult.message"
                  closable
                  @close="licenseActivateResult = null"
                />
              </div>
              <a-upload
                :before-upload="handleLicenseFileSelect"
                :show-upload-list="false"
                accept=".atlaslicense,.lic,.txt"
              >
                <a-button type="primary" size="small" :loading="licenseActivating">
                  <template #icon><upload-outlined /></template>
                  {{ licenseActivating ? '激活中…' : '上传证书' }}
                </a-button>
              </a-upload>
              <a-typography-paragraph
                type="secondary"
                style="font-size: 12px; margin-top: 8px; margin-bottom: 0"
              >
                上传 <code>.atlaslicense</code> 证书文件以激活平台授权
              </a-typography-paragraph>
            </template>
          </div>

          <a-divider style="margin: 12px 0" />

          <!-- ② 登录表单（仅证书有效时可用） -->
          <template v-if="licenseStatusCode === 'Active'">
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
                :rules="[
                  { required: true, message: '授权证书未提供租户 / 组织ID' },
                  { pattern: /^[0-9a-fA-F-]{36}$/, message: '证书租户ID格式无效，请重新激活证书' }
                ]"
              >
                <a-input
                  v-model:value="form.tenantId"
                  placeholder="租户 / 组织 ID 来自授权证书"
                  readonly
                  autocomplete="off"
                  @focus="errorMessage = ''"
                />
                <div class="tenant-readonly-tip">租户由证书自动绑定，如需变更请更换证书</div>
                <div v-if="!hasValidTenantId(form.tenantId.trim())" class="tenant-readonly-error">
                  当前证书未提供有效租户ID（GUID），请更换正确证书后登录
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
          </template>

          <template v-else-if="!licenseLoading">
            <div class="login-locked-hint">
              <a-typography-text type="secondary">请先激活有效的授权证书，方可登录系统</a-typography-text>
            </div>
          </template>

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
import { UploadOutlined } from "@ant-design/icons-vue";
import { getLicenseStatus, activateLicense } from "@/services/api";
import { useUserStore } from "@/stores/user";
import { usePermissionStore } from "@/stores/permission";
import type { RequestOptions } from "@/services/api";
import type { LicenseStatus } from "@/types/api";
import {
  clearAuthStorage,
  getTenantId
} from "@/utils/auth";

interface LoginApiError extends Error {
  status?: number;
  payload?: {
    code?: string;
    message?: string;
    traceId?: string;
  } | null;
}

const TENANT_ID_REGEX =
  /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;
const COOLDOWN_THRESHOLD = 5;
const COOLDOWN_DURATION = 30;

const userStore = useUserStore();
const permissionStore = usePermissionStore();
const route = useRoute();
const router = useRouter();
const loading = ref(false);
const errorMessage = ref("");
const failedAttempts = ref(0);
const capsLockOn = ref(false);
const cooldownSeconds = ref(0);
let cooldownTimer: number | undefined;

const REMEMBER_ME_KEY = "atlas-login-remember-me";

// 租户 ID 初始值：优先取 localStorage 已有的（返回用户），否则留空等待证书填充
const form = reactive({
  tenantId: getTenantId() ?? "",
  username: "",
  password: "",
  rememberMe: localStorage.getItem(REMEMBER_ME_KEY) === "true"
});

// 授权证书相关
const licenseLoading = ref(true);
const licenseActivating = ref(false);
const licenseActivateResult = ref<{ success: boolean; message: string } | null>(null);
const licenseStatusCode = ref<string>("None");
const licenseStatusInfo = ref<LicenseStatus | null>(null);
const showRenewArea = ref(false);

const licenseExpireText = computed(() => {
  const info = licenseStatusInfo.value;
  if (!info) return "";
  if (info.isPermanent) return "永久授权";
  if (info.remainingDays !== null && info.remainingDays !== undefined) {
    return `剩余 ${info.remainingDays} 天`;
  }
  if (info.expiresAt) {
    return `到期：${info.expiresAt.substring(0, 10)}`;
  }
  return "";
});

const isSubmitDisabled = computed(
  () =>
    loading.value ||
    cooldownSeconds.value > 0 ||
    !hasValidTenantId(form.tenantId.trim()) ||
    !form.username.trim() ||
    !form.password ||
    licenseStatusCode.value !== "Active"
);

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

const hasValidTenantId = (tenantId: string) => TENANT_ID_REGEX.test(tenantId);

const handleSubmit = async () => {
  errorMessage.value = "";
  loading.value = true;
  try {
    clearAuthStorage();
    const tenantId = form.tenantId.trim();
    const tokenOptions: RequestOptions = { suppressErrorMessage: true };

    await userStore.login(
      tenantId,
      form.username.trim(),
      form.password,
      tokenOptions,
      {
        rememberMe: form.rememberMe
      }
    );
    await userStore.getInfo();
    const routes = await permissionStore.generateRoutes();
    permissionStore.registerRoutes(router);
    localStorage.setItem(REMEMBER_ME_KEY, String(form.rememberMe));
    failedAttempts.value = 0;
    cooldownSeconds.value = 0;
    errorMessage.value = "";
    const rawRedirect = route.query.redirect;
    const redirect =
      typeof rawRedirect === "string" &&
      rawRedirect.startsWith("/") &&
      !rawRedirect.startsWith("//")
        ? rawRedirect
        : null;
    const targetPath = redirect ?? "/";
    const canNavigate = targetPath === "/"
      || routes.some((item) => typeof item.path === "string" && targetPath.startsWith(item.path));
    const fallbackPath = "/";
    const staticAllowedTargets = new Set(["/"]);
    router.push(canNavigate || staticAllowedTargets.has(targetPath) ? targetPath : fallbackPath);
  } catch (error) {
    clearAuthStorage();
    failedAttempts.value += 1;
    errorMessage.value = normalizeError(error);
    if (failedAttempts.value >= COOLDOWN_THRESHOLD) {
      startCooldown();
    }
  } finally {
    loading.value = false;
  }
};

async function handleLicenseFileSelect(file: File): Promise<false> {
  licenseActivating.value = true;
  licenseActivateResult.value = null;

  let content = "";
  try {
    content = await readFileAsText(file);
  } catch (error) {
    licenseActivateResult.value = {
      success: false,
      message: error instanceof Error ? error.message : "文件读取失败，请重试"
    };
    licenseActivating.value = false;
    return false;
  }

  try {
    const resp = await activateLicense(content);
    if (resp.success) {
      licenseActivateResult.value = {
        success: true,
        message: resp.data?.message ?? resp.message ?? "授权激活成功！"
      };
      showRenewArea.value = false;
      // 激活成功后重新加载授权状态，自动填充租户信息
      await loadLicenseStatus();
    } else {
      licenseActivateResult.value = {
        success: false,
        message: resp.message || "证书激活失败"
      };
    }
  } catch (error) {
    const requestError = error as LoginApiError;
    const detailMessage =
      requestError?.payload?.message ??
      (error instanceof Error ? error.message : "");
    licenseActivateResult.value = {
      success: false,
      message: detailMessage || "证书激活失败，请重试"
    };
  } finally {
    licenseActivating.value = false;
  }
  return false;
}

function readFileAsText(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = (e) => resolve((e.target?.result as string) ?? "");
    reader.onerror = () => reject(new Error("文件读取失败，请重试"));
    reader.readAsText(file);
  });
}

async function loadLicenseStatus() {
  licenseLoading.value = true;
  try {
    const status = await getLicenseStatus();
    licenseStatusCode.value = status.status;
    licenseStatusInfo.value = status;

    // 证书激活后自动填充租户 ID（若证书含有效 GUID 且当前租户未手动输入）
    if (status.status === "Active" && status.tenantId && hasValidTenantId(status.tenantId)) {
      if (!form.tenantId || !hasValidTenantId(form.tenantId)) {
        form.tenantId = status.tenantId;
      }
    }
  } catch {
    // 静默失败，不影响页面渲染
    licenseStatusCode.value = "None";
  } finally {
    licenseLoading.value = false;
  }
}

onMounted(() => {
  loadLicenseStatus();
});
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

/* ── 授权证书区 ── */
.license-section {
  padding: 10px 0 4px;
}

.license-loading {
  display: flex;
  align-items: center;
  color: var(--color-text-secondary);
  font-size: 13px;
  padding: 4px 0;
}

.license-active-header {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
  font-size: 13px;
}

.license-inactive-header {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
  margin-bottom: 10px;
  font-size: 13px;
}

.license-badge {
  display: inline-flex;
  align-items: center;
  padding: 2px 8px;
  border-radius: 10px;
  font-size: 12px;
  font-weight: 500;
}

.license-badge--active {
  background: #f6ffed;
  border: 1px solid #b7eb8f;
  color: #389e0d;
}

.license-badge--expired {
  background: #fff7e6;
  border: 1px solid #ffd591;
  color: #d46b08;
}

.license-badge--none {
  background: #f5f5f5;
  border: 1px solid #d9d9d9;
  color: #8c8c8c;
}

.license-org {
  font-weight: 500;
  color: var(--color-text-primary);
}

.license-expire {
  color: var(--color-text-secondary);
  font-size: 12px;
}

.license-renew-link {
  margin-left: auto;
  font-size: 12px;
  color: var(--color-primary);
  cursor: pointer;
}

.license-renew-link:hover {
  opacity: 0.8;
}

.license-hint {
  color: var(--color-text-secondary);
  font-size: 12px;
}

.license-upload-area {
  margin-top: 10px;
  padding: 10px;
  border: 1px dashed var(--color-border-dashed);
  border-radius: 6px;
  background: var(--color-bg-layout);
}

.tenant-readonly-tip {
  margin-top: var(--spacing-xs);
  color: var(--color-text-secondary);
  font-size: 12px;
}

.tenant-readonly-error {
  margin-top: var(--spacing-xs);
  color: var(--color-error-text);
  font-size: 12px;
}

/* ── 登录锁定提示 ── */
.login-locked-hint {
  padding: 16px 0 8px;
  text-align: center;
  font-size: 14px;
}

/* ── 错误提示 ── */
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

.remember-row {
  display: flex;
  align-items: center;
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
