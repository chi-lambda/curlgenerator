using System.Text;
using Microsoft.OpenApi.Models;

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
            var name = parameter.Name.ConvertKebabCaseToSnakeCase();
            code.AppendLine(
                parameter.Description is null
                    ? $"# {parameter.In.ToString().ToLowerInvariant()} parameter: {name}"
                    : $"# {parameter.Description}");

            code.AppendLine($"{name}=\"\""); // Initialize the parameter
        }

        // Handle form data and file upload fields
        if (operation.RequestBody?.Content != null)
        {
            var contentType = operation.RequestBody.Content.Keys.FirstOrDefault() ?? "application/json";
            TryLog($"Request body content type for operation {operation.OperationId}: {contentType}");
            if (contentType == "application/x-www-form-urlencoded" || contentType == "multipart/form-data")
            {
                var formData = operation.RequestBody.Content[contentType].Schema.Properties
                    .Select(p => $"{p.Key}=\"\"");
                foreach (var formField in formData)
                {
                    code.AppendLine(formField);
                }
            }
        }

        code.AppendLine();
    }

    protected override string AsVariable(string name) => $"${{{name.ConvertKebabCaseToSnakeCase()}}}";
}
