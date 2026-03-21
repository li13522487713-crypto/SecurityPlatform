<template>
  <div class="sidebar-logo-container" :class="{ collapse: collapse }">
    <transition name="sidebarLogoFade">
      <router-link v-if="collapse" key="collapse" class="sidebar-logo-link" to="/">
        <img v-if="logo" :src="logo" class="sidebar-logo" />
        <h1 v-else class="sidebar-title">{{ title }}</h1>
      </router-link>
      <router-link v-else key="expand" class="sidebar-logo-link" to="/">
        <img v-if="logo" :src="logo" class="sidebar-logo" />
        <h1 class="sidebar-title">{{ title }}</h1>
      </router-link>
    </transition>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";

defineProps({
  collapse: {
    type: Boolean,
    required: true
  }
});

const { t } = useI18n();
const title = computed(() => t("layoutChrome.brandTitle"));
const logo = ref("");
</script>

<style scoped>
.sidebarLogoFade-enter-active {
  transition: opacity 1.5s;
}

.sidebarLogoFade-enter-from,
.sidebarLogoFade-leave-to {
  opacity: 0;
}

.sidebar-logo-container {
  position: relative;
  width: 100%;
  height: 50px;
  line-height: 50px;
  background: transparent;
  text-align: center;
  overflow: hidden;
  border-bottom: 1px solid var(--color-border);
}

.sidebar-logo-container .sidebar-logo-link {
  height: 100%;
  width: 100%;
}

.sidebar-logo-container .sidebar-logo {
  width: 32px;
  height: 32px;
  vertical-align: middle;
  margin-right: 12px;
  border-radius: 50%;
}

.sidebar-logo-container .sidebar-title {
  display: inline-block;
  margin: 0;
  color: var(--color-text-primary);
  font-weight: 600;
  line-height: 50px;
  font-size: 16px;
  font-family: Avenir, Helvetica Neue, Arial, Helvetica, sans-serif;
  vertical-align: middle;
}

.sidebar-logo-container.collapse .sidebar-logo {
  margin-right: 0px;
}
</style>
