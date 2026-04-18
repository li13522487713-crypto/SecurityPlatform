using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;

namespace Atlas.Connectors.WeCom.Internal;

/// <summary>
/// 企业微信回调 XML ↔ JSON 通用转换。
/// 规则：
/// - 同名兄弟节点合并为 JSON 数组（匹配企微审批回调常见的 StatuChangeEvent + SpNo 重复结构）；
/// - 叶子节点直接取 <c>InnerText</c>，不丢 CDATA 包装；
/// - 属性挂 <c>@name</c> 前缀（当前回调负载极少用属性，仅兜底）。
/// 替换掉原先的 <c>{ rawXml: "..." }</c> 占位，让下游 <c>ExternalApprovalFanoutHandler</c> 直接按 JSON 字段消费。
/// </summary>
internal static class WeComXmlJsonConverter
{
    public static string ToJson(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return "{}";
        }

        var doc = new XmlDocument();
        doc.PreserveWhitespace = false;
        doc.LoadXml(xml);

        var root = doc.DocumentElement;
        if (root is null)
        {
            return "{}";
        }

        var node = ConvertElement(root);
        return node.ToJsonString(new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
    }

    private static JsonNode ConvertElement(XmlElement element)
    {
        var groups = new Dictionary<string, List<JsonNode?>>(StringComparer.Ordinal);

        foreach (XmlAttribute attr in element.Attributes)
        {
            var attrKey = "@" + attr.Name;
            AddToGroup(groups, attrKey, JsonValue.Create(attr.Value));
        }

        foreach (XmlNode child in element.ChildNodes)
        {
            switch (child)
            {
                case XmlElement el:
                    AddToGroup(groups, el.Name, ConvertChild(el));
                    break;

                case XmlCDataSection cdata when !string.IsNullOrWhiteSpace(cdata.Value):
                    AddToGroup(groups, "#text", JsonValue.Create(cdata.Value));
                    break;

                case XmlText text when !string.IsNullOrWhiteSpace(text.Value):
                    AddToGroup(groups, "#text", JsonValue.Create(text.Value));
                    break;

                default:
                    break;
            }
        }

        if (groups.Count == 1 && groups.ContainsKey("#text"))
        {
            return groups["#text"][0] ?? JsonValue.Create(string.Empty)!;
        }

        var obj = new JsonObject();
        foreach (var (key, values) in groups)
        {
            if (values.Count == 1)
            {
                obj[key] = values[0];
            }
            else
            {
                var arr = new JsonArray();
                foreach (var v in values)
                {
                    arr.Add(v);
                }
                obj[key] = arr;
            }
        }
        return obj;
    }

    private static JsonNode ConvertChild(XmlElement child)
    {
        var hasChildElement = false;
        foreach (XmlNode n in child.ChildNodes)
        {
            if (n is XmlElement)
            {
                hasChildElement = true;
                break;
            }
        }

        if (!hasChildElement && child.Attributes.Count == 0)
        {
            return JsonValue.Create(child.InnerText) ?? JsonValue.Create(string.Empty)!;
        }

        return ConvertElement(child);
    }

    private static void AddToGroup(Dictionary<string, List<JsonNode?>> groups, string key, JsonNode? node)
    {
        if (!groups.TryGetValue(key, out var list))
        {
            list = new List<JsonNode?>(capacity: 1);
            groups[key] = list;
        }
        list.Add(node);
    }
}
