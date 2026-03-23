<template>
  <div class="tags-view-container" data-testid="e2e-tags-view">
    <div ref="scrollWrapper" class="tags-scroll-wrapper" data-testid="e2e-tags-scroll">
      <router-link
        v-for="tag in visitedViews"
        :key="tag.path ?? tag.fullPath ?? tag.name"
        :to="{ path: tag.path ?? '/', query: tag.query }"
        class="tags-view-item"
        :class="isActive(tag) ? 'active' : ''"
        :data-testid="`e2e-tag-${(tag.path ?? '/').replace(/[^a-zA-Z0-9]+/g, '-').replace(/^-+|-+$/g, '').toLowerCase() || 'root'}`"
        @contextmenu.prevent="openMenu(tag, $event)"
        @click.middle="!isAffix(tag) ? closeSelectedTag(tag) : ''"
      >
        {{ resolveTagTitle(tag) }}
        <span
          v-if="!isAffix(tag)"
          class="close-icon"
          :data-testid="`e2e-tag-close-${(tag.path ?? '/').replace(/[^a-zA-Z0-9]+/g, '-').replace(/^-+|-+$/g, '').toLowerCase() || 'root'}`"
          @click.prevent.stop="closeSelectedTag(tag)"
        >
          ×
        </span>
      </router-link>
    </div>

    <ul
      v-show="visible"
      :style="{ left: `${left}px`, top: `${top}px` }"
      class="contextmenu"
      data-testid="e2e-tags-contextmenu"
    >
      <li @click="refreshSelectedTag(selectedTag)">{{ labels.refreshPage }}</li>
      <li v-if="!isAffix(selectedTag)" @click="closeSelectedTag(selectedTag)">{{ labels.closeCurrent }}</li>
      <li @click="closeOthersTags">{{ labels.closeOthers }}</li>
      <li @click="closeLeftTags">{{ labels.closeLeft }}</li>
      <li @click="closeRightTags">{{ labels.closeRight }}</li>
      <li @click="closeAllTags(selectedTag)">{{ labels.closeAll }}</li>
    </ul>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from "vue";
import type { RouteRecordRaw } from "vue-router";
import { useRoute, useRouter } from "vue-router";
import { i18n } from "@/i18n";
import { resolveRouteTitle } from "@/utils/i18n-navigation";
import { usePermissionStore } from "@/stores/permission";
import { useTagsViewStore, type TagView } from "@/stores/tagsView";

const route = useRoute();
const router = useRouter();
const tagsViewStore = useTagsViewStore();
const permissionStore = usePermissionStore();
const composer = i18n.global as unknown as { t: (messageKey: string) => string };

const visible = ref(false);
const top = ref(0);
const left = ref(0);
const selectedTag = ref<TagView>({} as TagView);
const affixTags = ref<TagView[]>([]);

const visitedViews = computed(() => tagsViewStore.visitedViews);
const routes = computed(() => permissionStore.routes);
const labels = computed(() => ({
  refreshPage: composer.t("tags.refreshPage"),
  closeCurrent: composer.t("tags.closeCurrent"),
  closeOthers: composer.t("tags.closeOthers"),
  closeLeft: composer.t("tags.closeLeft"),
  closeRight: composer.t("tags.closeRight"),
  closeAll: composer.t("tags.closeAll")
}));

watch(route, () => {
  addTags();
  moveToCurrentTag();
});

watch(visible, (value) => {
  if (value) {
    document.body.addEventListener("click", closeMenu);
  } else {
    document.body.removeEventListener("click", closeMenu);
  }
});

onMounted(() => {
  initTags();
  addTags();
});

function isActive(tag: TagView) {
  return tag.path === route.path;
}

function isAffix(tag: TagView) {
  return Boolean(tag.meta?.affix);
}

function filterAffixTags(routesData: RouteRecordRaw[], basePath = "/") {
  let tags: TagView[] = [];
  routesData.forEach((item) => {
    if (item.meta?.affix) {
      const tagPath = item.path.startsWith("/") ? item.path : `${basePath}/${item.path}`;
      tags.push({
        fullPath: tagPath,
        path: tagPath,
        name: item.name,
        meta: { ...item.meta }
      });
    }
    if (item.children) {
      const tempTags = filterAffixTags(item.children, item.path);
      if (tempTags.length >= 1) {
        tags = [...tags, ...tempTags];
      }
    }
  });
  return tags;
}

function initTags() {
  const affix = filterAffixTags(routes.value);
  affixTags.value = affix;
  for (const tag of affix) {
    if (tag.name) {
      tagsViewStore.addVisitedView(tag);
    }
  }
}

function addTags() {
  const { name } = route;
  if (name && name !== "login" && name !== "register" && name !== "not-found") {
    tagsViewStore.addView(route as unknown as TagView);
  }
  return false;
}

function moveToCurrentTag() {
  nextTick(() => {
    // Reserved for future scroll syncing.
  });
}

function refreshSelectedTag(view: TagView) {
  tagsViewStore.delCachedView(view);
  nextTick(() => {
    router.replace({
      path: view.path,
      query: {
        ...view.query,
        _refresh: Date.now()
      }
    }).catch(() => {
      window.location.reload();
    });
  });
}

function closeSelectedTag(view: TagView) {
  tagsViewStore.delView(view);
  if (isActive(view)) {
    toLastView(tagsViewStore.visitedViews, view);
  }
}

function closeRightTags() {
  tagsViewStore.delRightTags(selectedTag.value);
  if (!tagsViewStore.visitedViews.find((item: TagView) => item.path === route.path)) {
    toLastView(tagsViewStore.visitedViews);
  }
}

function closeLeftTags() {
  tagsViewStore.delLeftTags(selectedTag.value);
  if (!tagsViewStore.visitedViews.find((item: TagView) => item.path === route.path)) {
    toLastView(tagsViewStore.visitedViews);
  }
}

function closeOthersTags() {
  router.push(selectedTag.value as never);
  tagsViewStore.delOthersViews(selectedTag.value);
  moveToCurrentTag();
}

function closeAllTags(view: TagView) {
  tagsViewStore.delAllViews();
  if (affixTags.value.some((tag) => tag.path === route.path)) {
    return;
  }
  toLastView(tagsViewStore.visitedViews, view);
}

function toLastView(visitedViewsList: TagView[], view?: TagView) {
  const latestView = visitedViewsList.slice(-1)[0];
  if (latestView?.path) {
    router.push(latestView.path);
  } else {
    router.push("/");
  }
}

function openMenu(tag: TagView, event: MouseEvent) {
  left.value = event.clientX + 15;
  top.value = event.clientY;
  visible.value = true;
  selectedTag.value = tag;
}

function closeMenu() {
  visible.value = false;
}

function resolveTagTitle(tag: TagView) {
  return resolveRouteTitle(tag.meta, tag.path ?? "", tag.title ?? "no-name");
}
</script>

<style scoped>
.tags-view-container {
  height: 34px;
  width: 100%;
  background: var(--color-bg-container);
  border-bottom: 1px solid var(--color-border);
  position: relative;
}

.tags-scroll-wrapper {
  white-space: nowrap;
  overflow-x: auto;
  overflow-y: hidden;
  height: 100%;
}

.tags-scroll-wrapper::-webkit-scrollbar {
  display: none;
}

.tags-view-item {
  display: inline-block;
  position: relative;
  cursor: pointer;
  height: 26px;
  line-height: 26px;
  border: 1px solid var(--color-border);
  color: var(--color-text-secondary);
  background: var(--color-bg-container);
  padding: 0 8px;
  font-size: 12px;
  margin-left: 5px;
  margin-top: 4px;
  text-decoration: none;
  border-radius: var(--border-radius-sm);
}

.tags-view-item:first-of-type {
  margin-left: 15px;
}

.tags-view-item:last-of-type {
  margin-right: 15px;
}

.tags-view-item.active {
  background-color: var(--color-primary);
  color: var(--color-text-white);
  border-color: var(--color-primary);
}

.tags-view-item.active::before {
  content: "";
  background: var(--color-text-white);
  display: inline-block;
  width: 8px;
  height: 8px;
  border-radius: 50%;
  position: relative;
  margin-right: 4px;
}

.close-icon {
  margin-left: 4px;
  border-radius: 50%;
  width: 16px;
  height: 16px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s;
}

.close-icon:hover {
  background-color: var(--color-text-quaternary);
  color: var(--color-text-white);
}

.contextmenu {
  margin: 0;
  background: var(--color-bg-elevated);
  z-index: 3000;
  position: fixed;
  list-style-type: none;
  padding: 4px 0;
  border-radius: var(--border-radius-md);
  font-size: 12px;
  color: var(--color-text-primary);
  box-shadow: var(--shadow-sm);
}

.contextmenu li {
  margin: 0;
  padding: 7px 16px;
  cursor: pointer;
}

.contextmenu li:hover {
  background: var(--color-bg-hover);
}
</style>
