<template>
  <div class="app-breadcrumb" data-testid="e2e-breadcrumb">
    <a-breadcrumb>
      <a-breadcrumb-item v-for="item in items" :key="item.path || item.title">
        <span v-if="item.redirect === 'noRedirect' || item.noLink" class="no-redirect">{{ item.title }}</span>
        <router-link v-else-if="item.path" :to="item.path">{{ item.title }}</router-link>
        <span v-else>{{ item.title }}</span>
      </a-breadcrumb-item>
    </a-breadcrumb>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useRoute } from "vue-router";
import { resolveBreadcrumbTitle } from "@/utils/i18n-navigation";

const route = useRoute();

const items = computed(() => {
  let matched = route.matched.filter(
    (item) => item.meta && item.meta.title && item.meta.breadcrumb !== false
  );

  const first = matched[0];
  if (!isDashboard(first)) {
    matched = ([{ path: "/", meta: { titleKey: "route.home" } }] as unknown as typeof matched).concat(matched);
  }

  return matched.map((record, index) => ({
    title: resolveBreadcrumbTitle(record),
    path: record.path,
    redirect: record.redirect,
    noLink: index === matched.length - 1
  }));
});

function isDashboard(routeRecord: unknown) {
  const name = (routeRecord as { name?: string | symbol } | undefined)?.name;
  if (!name) {
    return false;
  }
  return name.toString().trim().toLowerCase() === "home";
}
</script>

<style scoped>
.app-breadcrumb {
  display: inline-block;
  font-size: 14px;
  line-height: 50px;
}

.no-redirect {
  color: #97a8be;
  cursor: text;
}
</style>
