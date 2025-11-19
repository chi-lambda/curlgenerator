using System.Text;
using Microsoft.OpenApi.Models;

namespace CurlGenerator.Core;
public class PwshScriptFileGenerator(ISettings settings) : ScriptFileGenerator(settings)
{
    protected override string FileExtension => "ps1";

    protected override string GenerateRequest(
        string baseUrl,
        string verb,
        KeyValuePair<string, OpenApiPathItem> kv,
        OpenApiOperation operation)
    {
        var code = new StringBuilder();
        AppendSummary(verb, kv, operation, code);
        var parameterNameMap = AppendParameters(verb, kv, operation, code);

        var url = kv.Key.Replace("{", "$").Replace("}", null);

        if (parameterNameMap.Count > 0)
        {
            url += "?";
        }

        foreach (var parameterName in parameterNameMap)
        {
            var value = parameterName.Value.ConvertKebabCaseToSnakeCase();
            url += $"{parameterName.Key}=${value}&";
        }

        if (parameterNameMap.Count > 0)
        {
            url = url.Remove(url.Length - 1);
        }

        code.AppendLine($"curl -X {verb.ToUpperInvariant()} {baseUrl}{url} `");
        if (settings.SkipCertificateCheck)
        {
            code.AppendLine("  -k `");
        }

        code.AppendLine($"  -H 'Accept: {settings.ContentType}' `");
        code.AppendLine($"  -H 'Content-Type: {settings.ContentType}' `");

        if (!string.IsNullOrWhiteSpace(settings.AuthorizationHeader))
        {
            code.AppendLine($"  -H 'Authorization: {settings.AuthorizationHeader}' `");
        }

        var contentType = operation.RequestBody?.Content?.Keys
            ?.FirstOrDefault(c => c.Contains(settings.ContentType));

        if (operation.RequestBody?.Content is null || contentType is null)
            return code.ToString();

        var requestBody = operation.RequestBody;
        var requestBodySchema = requestBody.Content[contentType].Schema;
        var requestBodyJson = GenerateSampleJsonFromSchema(requestBodySchema);

        code.AppendLine($"  -d '{requestBodyJson}'");
        return code.ToString();
    }

    private static Dictionary<string, string> AppendParameters(
        string verb,
        KeyValuePair<string, OpenApiPathItem> kv,
        OpenApiOperation operation,
        StringBuilder code)
    {
        var parameters = operation
            .Parameters
            .Where(c => c.In is ParameterLocation.Path or ParameterLocation.Query)
            .ToArray();

        if (parameters.Length == 0)
        {
            code.AppendLine();
            return [];
        }

        code.AppendLine("param(");

        var parameterNameMap = new Dictionary<string, string>();
        foreach (var parameter in parameters)
        {
            var name = parameter.Name.ConvertKebabCaseToSnakeCase();
            code.AppendLine(
                parameter.Description is null
                    ? $"""
                          [Parameter(Mandatory=$True)]
                          [String] ${name},
                       """
                    : $"""
                          <# {parameter.Description} #>
                          [Parameter(Mandatory=$True)]
                          [String] ${name},
                       """);
            code.AppendLine();
            parameterNameMap[parameter.Name] = name;
        }
        code.Remove(code.Length - 5, 3);

        code.AppendLine(")");
        code.AppendLine();

        return parameterNameMap;
    }

    private static void AppendSummary(
        string verb,
        KeyValuePair<string, OpenApiPathItem> kv,
        OpenApiOperation operation,
        StringBuilder code)
    {
        code.AppendLine("<#");
        code.AppendLine($"  Request: {verb.ToUpperInvariant()} {kv.Key}");

        if (!string.IsNullOrWhiteSpace(operation.Summary))
        {
            code.AppendLine($"  Summary: {operation.Summary}");
        }

        if (!string.IsNullOrWhiteSpace(operation.Description))
        {
            code.AppendLine($"  Description: {operation.Description}");
        }

        code.AppendLine("#>");
    }

}
