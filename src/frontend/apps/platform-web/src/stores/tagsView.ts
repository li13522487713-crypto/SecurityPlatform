import { defineStore } from "pinia";
import type { RouteLocationNormalizedLoaded } from "vue-router";

export interface TagView extends Partial<RouteLocationNormalizedLoaded> {
  title?: string;
}

interface TagsViewState {
  visitedViews: TagView[];
  cachedViews: string[];
}

const MAX_CACHE_VIEWS = 8;

export const useTagsViewStore = defineStore("tagsView", {
  state: (): TagsViewState => ({
    visitedViews: [],
    cachedViews: []
  }),
  actions: {
    addView(view: TagView) {
      this.addVisitedView(view);
      this.addCachedView(view);
    },
    addVisitedView(view: TagView) {
      if (this.visitedViews.some((v) => v.path === view.path)) return;
      this.visitedViews.push(
        Object.assign({}, view, { title: view.meta?.title || "no-name" })
      );
    },
    addCachedView(view: TagView) {
      const name = view.name as string | undefined;
      if (!name || view.meta?.noCache) return;
      const index = this.cachedViews.indexOf(name);
      if (index > -1) this.cachedViews.splice(index, 1);
      this.cachedViews.push(name);
      while (this.cachedViews.length > MAX_CACHE_VIEWS) {
        this.cachedViews.shift();
      }
    },
    delView(view: TagView) {
      this.delVisitedView(view);
      this.delCachedView(view);
    },
    delVisitedView(view: TagView) {
      const index = this.visitedViews.findIndex((v) => v.path === view.path);
      if (index > -1) this.visitedViews.splice(index, 1);
    },
    delCachedView(view: TagView) {
      const index = this.cachedViews.indexOf(view.name as string);
      if (index > -1) this.cachedViews.splice(index, 1);
    },
    delOthersViews(view: TagView) {
      this.delOthersVisitedViews(view);
      this.delOthersCachedViews(view);
    },
    delOthersVisitedViews(view: TagView) {
      this.visitedViews = this.visitedViews.filter(
        (v) => v.meta?.affix || v.path === view.path
      );
    },
    delOthersCachedViews(view: TagView) {
      const index = this.cachedViews.indexOf(view.name as string);
      if (index > -1) {
        this.cachedViews = this.cachedViews.slice(index, index + 1);
      } else {
        this.cachedViews = [];
      }
    },
    delAllViews() {
      this.delAllVisitedViews();
      this.delAllCachedViews();
    },
    delAllVisitedViews() {
      const affixTags = this.visitedViews.filter((tag) => tag.meta?.affix);
      this.visitedViews = affixTags;
    },
    delAllCachedViews() {
      this.cachedViews = [];
    },
    delRightTags(view: TagView) {
      const index = this.visitedViews.findIndex((v) => v.path === view.path);
      if (index === -1) return;
      this.visitedViews = this.visitedViews.filter((item, idx) => {
        if (idx <= index || item.meta?.affix) return true;
        const cacheIndex = this.cachedViews.indexOf(item.name as string);
        if (cacheIndex > -1) this.cachedViews.splice(cacheIndex, 1);
        return false;
      });
    },
    delLeftTags(view: TagView) {
      const index = this.visitedViews.findIndex((v) => v.path === view.path);
      if (index === -1) return;
      this.visitedViews = this.visitedViews.filter((item, idx) => {
        if (idx >= index || item.meta?.affix) return true;
        const cacheIndex = this.cachedViews.indexOf(item.name as string);
        if (cacheIndex > -1) this.cachedViews.splice(cacheIndex, 1);
        return false;
      });
    },
    updateVisitedView(view: TagView) {
      const index = this.visitedViews.findIndex((v) => v.path === view.path);
      if (index > -1) {
        this.visitedViews = Object.assign([...this.visitedViews], {
          [index]: Object.assign({}, view, { title: view.meta?.title || "no-name" })
        });
      }
    }
  }
});
