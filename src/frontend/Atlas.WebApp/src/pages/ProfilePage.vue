<template>
  <div class="profile-page">
    <a-card title="个人中心" :bordered="false">
      <a-row :gutter="[16, 16]">
        <a-col :xs="24" :lg="12">
          <a-descriptions bordered size="small" :column="1">
            <a-descriptions-item label="用户名">
              {{ profile?.username || "-" }}
            </a-descriptions-item>
            <a-descriptions-item label="显示名称">
              {{ profile?.displayName || "-" }}
            </a-descriptions-item>
            <a-descriptions-item label="租户ID">
              {{ profile?.tenantId || "-" }}
            </a-descriptions-item>
            <a-descriptions-item label="角色">
              <a-space wrap>
                <a-tag v-for="role in profile?.roles ?? []" :key="role">{{ role }}</a-tag>
                <span v-if="(profile?.roles?.length ?? 0) === 0">-</span>
              </a-space>
            </a-descriptions-item>
            <a-descriptions-item label="登录端">
              {{ clientContextLabel }}
            </a-descriptions-item>
          </a-descriptions>
        </a-col>

        <a-col :xs="24" :lg="12">
          <a-form :model="formModel" :rules="rules" layout="vertical">
            <a-form-item label="显示名称" name="displayName">
              <a-input v-model:value="formModel.displayName" placeholder="请输入显示名称" />
            </a-form-item>
            <a-form-item label="邮箱" name="email">
              <a-input v-model:value="formModel.email" placeholder="请输入邮箱" />
            </a-form-item>
            <a-form-item label="手机号" name="phoneNumber">
              <a-input v-model:value="formModel.phoneNumber" placeholder="请输入手机号" />
            </a-form-item>
            <a-space>
              <a-button type="primary" :loading="saving" @click="submit">保存</a-button>
              <a-button :disabled="saving" @click="resetForm">重置</a-button>
            </a-space>
          </a-form>
        </a-col>
      </a-row>
    </a-card>

    <a-card title="修改密码" :bordered="false" class="password-card">
      <a-form ref="passwordFormRef" :model="passwordModel" :rules="passwordRules" layout="vertical">
        <a-row :gutter="[16, 0]">
          <a-col :xs="24" :md="12">
            <a-form-item label="当前密码" name="currentPassword">
              <a-input-password v-model:value="passwordModel.currentPassword" placeholder="请输入当前密码" />
            </a-form-item>
          </a-col>
          <a-col :xs="24" :md="12">
            <a-form-item label="新密码" name="newPassword">
              <a-input-password v-model:value="passwordModel.newPassword" placeholder="请输入新密码" />
            </a-form-item>
          </a-col>
          <a-col :xs="24" :md="12">
            <a-form-item label="确认新密码" name="confirmPassword">
              <a-input-password v-model:value="passwordModel.confirmPassword" placeholder="请再次输入新密码" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-space>
          <a-button type="primary" :loading="changing" @click="submitPassword">更新密码</a-button>
          <a-button :disabled="changing" @click="resetPasswordForm">重置</a-button>
        </a-space>
      </a-form>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import { useRouter } from "vue-router";
import { message } from "ant-design-vue";
import { changePassword, getCurrentUser, getProfileDetail, logout as apiLogout, updateProfile } from "@/services/api";
import type { AuthProfile, ChangePasswordRequest, UserProfileDetail, UserProfileUpdateRequest } from "@/types/api";
import { clearAuthStorage, getAuthProfile, setAuthProfile } from "@/utils/auth";

const profile = ref<AuthProfile | null>(null);
const userDetail = ref<UserProfileDetail | null>(null);
const saving = ref(false);
const changing = ref(false);
const passwordFormRef = ref();
const router = useRouter();

const formModel = reactive<UserProfileUpdateRequest>({
  displayName: "",
  email: "",
  phoneNumber: ""
});

const rules = {
  displayName: [{ required: true, message: "请输入显示名称" }]
};

const passwordModel = reactive<ChangePasswordRequest>({
  currentPassword: "",
  newPassword: "",
  confirmPassword: ""
});

const passwordRules = {
  currentPassword: [{ required: true, message: "请输入当前密码" }],
  newPassword: [{ required: true, message: "请输入新密码" }],
  confirmPassword: [
    { required: true, message: "请确认新密码" },
    {
      validator: (_: unknown, value: string) =>
        value === passwordModel.newPassword ? Promise.resolve() : Promise.reject("两次输入的新密码不一致")
    }
  ]
};

const clientContextLabel = computed(() => {
  const ctx = profile.value?.clientContext;
  if (!ctx) return "-";
  return `${ctx.clientPlatform}/${ctx.clientChannel}/${ctx.clientAgent}`;
});

const loadProfile = async () => {
  const cached = getAuthProfile();
  if (cached) {
    profile.value = cached;
  }

  try {
    const result = await getCurrentUser();
    profile.value = result;
    setAuthProfile(result);
  } catch (error) {
    message.error((error as Error).message || "获取用户信息失败");
  }
};

const loadDetail = async () => {
  if (!profile.value?.id) {
    return;
  }

  try {
    const detail = await getProfileDetail();
    userDetail.value = detail;
    formModel.displayName = detail.displayName;
    formModel.email = detail.email ?? "";
    formModel.phoneNumber = detail.phoneNumber ?? "";
  } catch (error) {
    message.error((error as Error).message || "加载个人信息失败");
  }
};

const resetForm = () => {
  if (!userDetail.value) {
    return;
  }
  formModel.displayName = userDetail.value.displayName;
  formModel.email = userDetail.value.email ?? "";
  formModel.phoneNumber = userDetail.value.phoneNumber ?? "";
};

const submit = async () => {
  saving.value = true;
  try {
    await updateProfile({
      displayName: formModel.displayName,
      email: formModel.email || undefined,
      phoneNumber: formModel.phoneNumber || undefined
    });
    message.success("保存成功");
    await loadProfile();
    await loadDetail();
  } catch (error) {
    message.error((error as Error).message || "保存失败");
  } finally {
    saving.value = false;
  }
};

const resetPasswordForm = () => {
  passwordModel.currentPassword = "";
  passwordModel.newPassword = "";
  passwordModel.confirmPassword = "";
  passwordFormRef.value?.clearValidate?.();
};

const submitPassword = async () => {
  try {
    await passwordFormRef.value?.validate();
  } catch {
    return;
  }

  changing.value = true;
  try {
    await changePassword({
      currentPassword: passwordModel.currentPassword,
      newPassword: passwordModel.newPassword,
      confirmPassword: passwordModel.confirmPassword
    });
    message.success("密码更新成功，即将退出登录");
    resetPasswordForm();
    await logoutAndRedirect();
  } catch (error) {
    message.error((error as Error).message || "修改密码失败");
  } finally {
    changing.value = false;
  }
};

const logoutAndRedirect = async () => {
  try {
    await apiLogout();
  } catch {
    // ignore
  }

  clearAuthStorage();
  router.push({ name: "login" });
};

onMounted(async () => {
  await loadProfile();
  await loadDetail();
});
</script>

<style scoped>
.profile-page {
  padding: 16px;
}

.password-card {
  margin-top: 16px;
}
</style>
