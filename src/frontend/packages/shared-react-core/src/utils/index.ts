export {
  setAuthStorageNamespace,
  getAccessToken,
  setAccessToken,
  getRefreshToken,
  setRefreshToken,
  getTenantId,
  setTenantId,
  getProjectId,
  setProjectId,
  clearProjectId,
  getProjectScopeEnabled,
  setProjectScopeEnabled,
  getAuthProfile,
  setAuthProfile,
  hasAuthSessionSignal,
  clearAuthStorage,
  isAdminRole,
  hasPermission
} from "./auth";

export { getClientContextHeaders } from "./client-context";

export {
  isExternal,
  validURL,
  validLowerCase,
  validUpperCase,
  validAlphabets,
  validEmail,
  isString,
  isArray
} from "./validate";

export {
  formatDateTime,
  debounce,
  loadSelectOptions,
  handleTree,
  addDateRange,
  selectDictLabel,
  selectDictLabels
} from "./common";
export type {
  FormMode,
  SelectOption,
  LoadSelectOptionsConfig,
  TreeNode,
  DateRangeParams,
  DictItem
} from "./common";
