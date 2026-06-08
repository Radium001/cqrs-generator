using Scriban;
using Scriban.Runtime;

namespace CqrsGenerator.Core.Templates;

public sealed class ScribanTemplateRenderer(TemplateProvider provider)
{
    public string Render(string templateName, object model)
    {
        var templateText = provider.GetTemplate(templateName);
        var template = Template.Parse(templateText, templateName);
        if (template.HasErrors)
        {
            var errors = string.Join(Environment.NewLine, template.Messages.Select(message => message.ToString()));
            throw new InvalidOperationException($"Template '{templateName}' is invalid:{Environment.NewLine}{errors}");
        }

        var scriptObject = new ScriptObject();
        scriptObject.Import(model, renamer: member => member.Name);

        var context = new TemplateContext();
        context.PushGlobal(scriptObject);

        return template.Render(context).ReplaceLineEndings("\n");
    }
}
