import { ref } from "vue";
import { message } from "ant-design-vue";
import { translate } from "@/i18n";
import { requestApi, requestApiBlob } from "@/services/api-core";
import type { ApiResponse } from "@/types/api";

export function useExcelExport() {
  const t = translate;
  const exporting = ref(false);
  const importing = ref(false);

  async function downloadBlob(url: string, filename: string) {
    const blob = await requestApiBlob(url);
    const objectUrl = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = objectUrl;
    anchor.download = filename;
    anchor.click();
    URL.revokeObjectURL(objectUrl);
  }

  async function exportUsers(keyword?: string) {
    exporting.value = true;
    try {
      const query = keyword ? `?keyword=${encodeURIComponent(keyword)}` : "";
      await downloadBlob(
        `/users/export${query}`,
        `users_${new Date().toISOString().slice(0, 10)}.xlsx`
      );
      message.success(t("excel.exportUsersSuccess"));
    } catch (error) {
      message.error(error instanceof Error ? error.message : t("excel.exportUsersFailed"));
    } finally {
      exporting.value = false;
    }
  }

  async function downloadImportTemplate() {
    try {
      await downloadBlob("/users/import-template", "user_import_template.xlsx");
    } catch (error) {
      message.error(error instanceof Error ? error.message : t("excel.downloadTemplateFailed"));
    }
  }

  async function importUsers(file: File): Promise<ImportResult | null> {
    importing.value = true;
    try {
      const formData = new FormData();
      formData.append("file", file);
      const response = await requestApi<ApiResponse<ImportResult>>("/users/import", {
        method: "POST",
        body: formData
      });
      return response.data ?? null;
    } catch (error) {
      message.error(error instanceof Error ? error.message : t("excel.importUsersFailed"));
      return null;
    } finally {
      importing.value = false;
    }
  }

  return { exporting, importing, exportUsers, downloadImportTemplate, importUsers };
}

export interface ImportError {
  row: number;
  field: string;
  message: string;
}

export interface ImportResult {
  totalRows: number;
  successCount: number;
  failureCount: number;
  errors: ImportError[];
}
