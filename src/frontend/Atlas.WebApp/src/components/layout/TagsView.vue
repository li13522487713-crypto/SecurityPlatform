<template>
  <div class="tags-view-container">
    <div class="tags-scroll-wrapper" ref="scrollWrapper">
      <router-link
        v-for="tag in visitedViews"
        :key="tag.path"
        :to="{ path: tag.path, query: tag.query }"
        class="tags-view-item"
        :class="isActive(tag) ? 'active' : ''"
        @contextmenu.prevent="openMenu(tag, $event)"
        @click.middle="!isAffix(tag) ? closeSelectedTag(tag) : ''"
      >
        {{ tag.title }}
        <span v-if="!isAffix(tag)" class="close-icon" @click.prevent.stop="closeSelectedTag(tag)">
          ×
        </span>
      </router-link>
    </div>

    <ul
      v-show="visible"
      :style="{ left: left + 'px', top: top + 'px' }"
      class="contextmenu"
    >
      <li @click="refreshSelectedTag(selectedTag)">刷新页面</li>
      <li v-if="!isAffix(selectedTag)" @click="closeSelectedTag(selectedTag)">关闭当前</li>
      <li @click="closeOthersTags">关闭其他</li>
      <li @click="closeLeftTags">关闭左侧</li>
      <li @click="closeRightTags">关闭右侧</li>
      <li @click="closeAllTags(selectedTag)">全部关闭</li>
    </ul>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useTagsViewStore, type TagView } from "@/stores/tagsView";
import { usePermissionStore } from "@/stores/permission";
import type { RouteRecordRaw } from "vue-router";

const route = useRoute();
const router = useRouter();
const tagsViewStore = useTagsViewStore();
const permissionStore = usePermissionStore();

const visible = ref(false);
const top = ref(0);
const left = ref(0);
const selectedTag = ref<TagView>({} as TagView);
const affixTags = ref<TagView[]>([]);

const visitedViews = computed(() => tagsViewStore.visitedViews);
const routes = computed(() => permissionStore.routes);

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

function isActive(r: TagView) {
  return r.path === route.path;
}

function isAffix(tag: TagView) {
  return tag.meta && tag.meta.affix;
}

function filterAffixTags(routesData: RouteRecordRaw[], basePath = "/") {
  let tags: TagView[] = [];
  routesData.forEach((r) => {
    if (r.meta && r.meta.affix) {
      const tagPath = r.path.startsWith("/") ? r.path : basePath + "/" + r.path;
      tags.push({
        fullPath: tagPath,
        path: tagPath,
        name: r.name,
        meta: { ...r.meta }
      });
    }
    if (r.children) {
      const tempTags = filterAffixTags(r.children, r.path);
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
    // scroll logic can be implemented here if needed
  });
}

function refreshSelectedTag(view: TagView) {
  tagsViewStore.delCachedView(view);
  const { fullPath } = view;
  nextTick(() => {
    // A simple trick to remount the component is to navigate to a redirect page or replace state.
    // For simplicity without a redirect component, we just use router replace with a dummy query or key
    router.replace({
      path: view.path,
      query: {
        ...view.query,
        _refresh: Date.now()
      }
    }).catch(() => {
      // fallback
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
  if (!tagsViewStore.visitedViews.find((i: TagView) => i.path === route.path)) {
    toLastView(tagsViewStore.visitedViews);
  }
}

function closeLeftTags() {
  tagsViewStore.delLeftTags(selectedTag.value);
  if (!tagsViewStore.visitedViews.find((i: TagView) => i.path === route.path)) {
    toLastView(tagsViewStore.visitedViews);
  }
}

function closeOthersTags() {
  router.push(selectedTag.value as any);
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
  if (latestView && latestView.path) {
    router.push(latestView.path);
  } else {
    router.push("/");
  }
}

function openMenu(tag: TagView, e: MouseEvent) {
  const menuMinWidth = 105;
  // simple positioning
  left.value = e.clientX + 15;
  top.value = e.clientY;
  visible.value = true;
  selectedTag.value = tag;
}

function closeMenu() {
  visible.value = false;
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
