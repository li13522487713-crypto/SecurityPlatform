<template>
  <a-breadcrumb class="app-breadcrumb">
    <a-breadcrumb-item v-for="item in items" :key="item.path || item.title">
      <span v-if="item.redirect === 'noRedirect' || item.noLink" class="no-redirect">{{ item.title }}</span>
      <router-link v-else-if="item.path" :to="item.path">{{ item.title }}</router-link>
      <span v-else>{{ item.title }}</span>
    </a-breadcrumb-item>
  </a-breadcrumb>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useRoute } from "vue-router";

const route = useRoute();

const items = computed(() => {
  let matched = route.matched.filter(
    (item) => item.meta && item.meta.title && item.meta.breadcrumb !== false
  );
  
  const first = matched[0];
  if (!isDashboard(first)) {
    matched = [{ path: "/", meta: { title: "首页" } } as any].concat(matched);
  }

  return matched.map((record, index) => ({
    title: String(record.meta?.title),
    path: record.path,
    redirect: record.redirect,
    noLink: index === matched.length - 1
  }));
});

function isDashboard(routeRecord: any) {
  const name = routeRecord && routeRecord.name;
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
