namespace Atlas.Core.Plugins;

public interface ITemplateSpi
{
    string TemplateKey { get; }
    string Render(string templateContent, Dictionary<string, object> context);
}
