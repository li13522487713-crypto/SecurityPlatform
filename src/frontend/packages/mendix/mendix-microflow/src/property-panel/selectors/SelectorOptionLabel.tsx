import { Space, Tag, Typography } from "@douyinfe/semi-ui";

const { Text } = Typography;

export function SelectorOptionLabel({
  title,
  subtitle,
  tags = [],
}: {
  title: string;
  subtitle?: string;
  tags?: string[];
}) {
  return (
    <Space align="center" spacing={6}>
      <span>
        <Text size="small">{title}</Text>
        {subtitle ? <Text size="small" type="tertiary"> {subtitle}</Text> : null}
      </span>
      {tags.map(tag => <Tag key={tag} size="small">{tag}</Tag>)}
    </Space>
  );
}
