import type { ReactNode } from "react";
import { Button, Dropdown } from "@douyinfe/semi-ui";
import {
  IconPlus,
  IconCode,
  IconFolder,
  IconArticle,
  IconPuzzle,
  IconList,
  IconBox,
  IconHistogram,
  IconLink
} from "@douyinfe/semi-icons";
import type { AppMessageKey } from "../../messages";
import type { LibraryResourceType } from "../../../services/api-ai-workspace";
import { useAppI18n } from "../../i18n";

const ITEMS: { key: LibraryResourceType; labelKey: AppMessageKey; icon: ReactNode }[] = [
  { key: "plugin", labelKey: "cozeLibraryTabPlugin", icon: <IconPuzzle /> },
  { key: "workflow", labelKey: "cozeLibraryTabWorkflow", icon: <IconCode /> },
  { key: "knowledge-base", labelKey: "cozeLibraryTabKnowledge", icon: <IconFolder /> },
  { key: "card", labelKey: "cozeLibraryTabCard", icon: <IconBox /> },
  { key: "prompt", labelKey: "cozeLibraryTabPrompt", icon: <IconArticle /> },
  { key: "database", labelKey: "cozeLibraryCreateDataSourceTitle", icon: <IconList /> },
  { key: "voice", labelKey: "cozeLibraryTabVoice", icon: <IconHistogram /> },
  { key: "memory", labelKey: "cozeLibraryTabMemory", icon: <IconLink /> }
];

export interface LibraryCreateDropdownProps {
  onSelectType: (resourceType: LibraryResourceType) => void;
}

export function LibraryCreateDropdown({ onSelectType }: LibraryCreateDropdownProps) {
  const { t } = useAppI18n();

  return (
    <Dropdown
      trigger="click"
      position="bottomRight"
      render={
        <Dropdown.Menu>
          {ITEMS.map(item => (
            <Dropdown.Item key={item.key} icon={item.icon} onClick={() => onSelectType(item.key)}>
              {t(item.labelKey)}
            </Dropdown.Item>
          ))}
        </Dropdown.Menu>
      }
    >
      <Button theme="solid" type="primary" icon={<IconPlus />}>
        {t("cozeLibraryCreateResource")}
      </Button>
    </Dropdown>
  );
}
