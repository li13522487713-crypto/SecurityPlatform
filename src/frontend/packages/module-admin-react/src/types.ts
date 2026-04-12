import type {
  ChangePasswordRequest,
  DepartmentCreateRequest,
  DepartmentListItem,
  DepartmentUpdateRequest,
  PagedRequest,
  PagedResult,
  PositionCreateRequest,
  PositionDetail,
  PositionListItem,
  PositionUpdateRequest,
  RoleCreateRequest,
  RoleDetail,
  RoleListItem,
  RoleQueryRequest,
  RoleUpdateRequest,
  UserCreateRequest,
  UserDetail,
  UserListItem,
  UserProfileDetail,
  UserProfileUpdateRequest,
  UserUpdateRequest
} from "@atlas/shared-react-core/types";

export type AdminLocale = "zh-CN" | "en-US";

export interface ApprovalTaskItem {
  id: string;
  instanceId: string;
  flowName: string;
  title: string;
  currentNodeName: string;
  status: number;
  createdAt: string;
}

export interface ApprovalInstanceItem {
  id: string;
  flowName: string;
  title: string;
  status: number;
  createdAt: string;
  completedAt?: string;
}

export interface ApprovalCopyItem {
  id: string;
  instanceId: string;
  flowName: string;
  title: string;
  isRead: boolean;
  createdAt: string;
}

export interface ReportItem {
  id: string;
  name: string;
  description?: string | null;
  createdAt: string;
}

export interface DashboardItem {
  id: string;
  name: string;
  description?: string | null;
  isDefault?: boolean;
  createdAt: string;
}

export interface VisualizationInstanceSummary {
  id: string;
  flowName: string;
  status: string;
  currentNode?: string | null;
  startedAt: string;
  durationMinutes?: number | null;
}

export interface DatabaseConnectionStatus {
  connected: boolean;
  message: string;
  latencyMs: number | null;
}

export interface DatabaseInfo {
  dbType: string;
  connectionString: string;
  fileSizeBytes: number | null;
  journalMode: string | null;
  pageCount: number | null;
  pageSize: number | null;
}

export interface BackupFileInfo {
  fileName: string;
  sizeBytes: number;
  createdAt: string;
  sha256: string | null;
}

export interface BackupResult {
  success: boolean;
  fileName: string | null;
  message: string | null;
  sizeBytes: number | null;
}

export interface SaveReportRequest {
  name: string;
  description?: string;
  category?: string;
  configJson: string;
  dataSourceJson?: string;
}

export interface SaveDashboardRequest {
  name: string;
  description?: string;
  category?: string;
  layoutJson: string;
  isLargeScreen: boolean;
  canvasWidth?: number;
  canvasHeight?: number;
  themeJson?: string;
}

export interface AdminModuleApi {
  listUsers: (request: PagedRequest) => Promise<PagedResult<UserListItem>>;
  getUserDetail: (id: string) => Promise<UserDetail>;
  createUser: (request: UserCreateRequest) => Promise<void>;
  updateUser: (id: string, request: UserUpdateRequest) => Promise<void>;
  deleteUser: (id: string) => Promise<void>;
  listRoles: (request: RoleQueryRequest) => Promise<PagedResult<RoleListItem>>;
  getRoleDetail: (id: string) => Promise<RoleDetail>;
  createRole: (request: RoleCreateRequest) => Promise<void>;
  updateRole: (id: string, request: RoleUpdateRequest) => Promise<void>;
  deleteRole: (id: string) => Promise<void>;
  listDepartments: (request: PagedRequest) => Promise<PagedResult<DepartmentListItem>>;
  listDepartmentsAll: () => Promise<DepartmentListItem[]>;
  createDepartment: (request: DepartmentCreateRequest) => Promise<void>;
  updateDepartment: (id: string, request: DepartmentUpdateRequest) => Promise<void>;
  deleteDepartment: (id: string) => Promise<void>;
  listPositions: (request: PagedRequest) => Promise<PagedResult<PositionListItem>>;
  listPositionsAll: () => Promise<PositionListItem[]>;
  getPositionDetail: (id: string) => Promise<PositionDetail>;
  createPosition: (request: PositionCreateRequest) => Promise<void>;
  updatePosition: (id: string, request: PositionUpdateRequest) => Promise<void>;
  deletePosition: (id: string) => Promise<void>;
  listPendingApprovals: (request: PagedRequest) => Promise<PagedResult<ApprovalTaskItem>>;
  listDoneApprovals: (request: PagedRequest) => Promise<PagedResult<ApprovalTaskItem>>;
  listMyRequests: (request: PagedRequest) => Promise<PagedResult<ApprovalInstanceItem>>;
  listCopyApprovals: (request: PagedRequest) => Promise<PagedResult<ApprovalCopyItem>>;
  listReports: (request: PagedRequest) => Promise<PagedResult<ReportItem>>;
  createReport: (request: SaveReportRequest) => Promise<void>;
  updateReport: (id: string, request: SaveReportRequest) => Promise<void>;
  deleteReport: (id: string) => Promise<void>;
  listDashboards: (request: PagedRequest) => Promise<PagedResult<DashboardItem>>;
  createDashboard: (request: SaveDashboardRequest) => Promise<void>;
  updateDashboard: (id: string, request: SaveDashboardRequest) => Promise<void>;
  deleteDashboard: (id: string) => Promise<void>;
  listVisualization: (request: PagedRequest) => Promise<PagedResult<VisualizationInstanceSummary>>;
  getProfile: () => Promise<UserProfileDetail>;
  updateProfile: (request: UserProfileUpdateRequest) => Promise<void>;
  changePassword: (request: ChangePasswordRequest) => Promise<void>;
  testConnection: () => Promise<DatabaseConnectionStatus>;
  getDatabaseInfo: () => Promise<DatabaseInfo>;
  listBackups: () => Promise<BackupFileInfo[]>;
  backupNow: () => Promise<BackupResult>;
}

export interface AdminPageCommonProps {
  api: AdminModuleApi;
  locale: AdminLocale;
}
