using System.Text;
using Microsoft.OpenApi;

namespace CurlGenerator.Core;

public class BashScriptFileGenerator(ISettings settings) : ScriptFileGenerator(settings)
{
    protected override string FileExtension => "sh";
    protected override string Joiner => "\\";
    protected override string CommentStart => "#";
    protected override string CommentContinue => "#";
    protected override string CommentEnd => "#";
    protected override string ShellName => "bash";

    protected override void AppendParameters(OpenApiOperation operation, StringBuilder code)
    {
        if (operation.Parameters is null)
        {
            return;
        }
        var parameters = operation.Parameters
            .Where(p => p.In is
                ParameterLocation.Path or
                ParameterLocation.Query or
                ParameterLocation.Header or
                ParameterLocation.Cookie)
            .ToArray();

        if (parameters.Length == 0)
        {
            code.AppendLine();
            return;
        }

        code.AppendLine();

        foreach (var parameter in parameters)
        {
            var name = parameter.Name!.ConvertKebabCaseToSnakeCase();
            code.AppendLine(
                parameter.Description is null
                    ? $"# {parameter.In.ToString().ToLowerInvariant()} parameter: {name}"
                    : $"# {parameter.Name}: {parameter.Description}");

            if (!settings.EnvironmentParameters)
            {
                code.AppendLine($"{name}=\"{GetDefaultValue(parameter)}\""); // Initialize the parameter
            }
        }

        const string curlgenerator_state = nameof(curlgenerator_state);
        foreach (var parameter in parameters.Where(p => p.Required))
        {
            var name = parameter.Name;
            if (name is null) { continue; }
            var defaultValue = GetDefaultValue(parameter);
            code.AppendLine($"if [ \"{AsVariable(name.ConvertKebabCaseToSnakeCase())}\" == \"\" ]; then");
            if (defaultValue is not null && settings.RequiredDefault)
            {
                code.AppendLine($"  >&2 echo \"Required parameter '{name}' substituted with default value '{defaultValue}'.\"");
                code.AppendLine($"  {name.ConvertKebabCaseToSnakeCase()}='{defaultValue}'");
            }
            else
            {
                code.AppendLine($"  >&2 echo \"Required parameter '{name}' is missing.\"");
                code.AppendLine($"  {curlgenerator_state}=HALT");
            }
            code.AppendLine("fi");
            code.AppendLine();
        }

        code.AppendLine($"if [ \"${curlgenerator_state}\" == \"HALT\" ]; then");
        code.AppendLine("  >&2 echo Required parameters are missing. Cancelling request.");
        code.AppendLine("  exit 1");
        code.AppendLine("fi");
        code.AppendLine();

        // Handle form data and file upload fields
        if (operation.RequestBody?.Content != null)
        {
            var contentType = operation.RequestBody.Content.Keys.FirstOrDefault() ?? "application/json";
            TryLog($"Request body content type for operation {operation.OperationId}: {contentType}");
            var schema = operation.RequestBody.Content[contentType].Schema;
            if ((contentType == "application/x-www-form-urlencoded" || contentType == "multipart/form-data") && schema is not null)
            {
                var formData = settings.EnvironmentParameters
                    ? schema.Properties.Select(p => $"{p.Key}=\"{AsScriptVariable(p.Key)}\"")
                    : schema.Properties.Select(p => $"{p.Key}=\"\"");
                foreach (var formField in formData)
                {
                    code.AppendLine(formField);
                }
            }
        }

        code.AppendLine();
    }

    protected override string AsEnvironmentVariable(string name) => AsScriptVariable(name);
}
