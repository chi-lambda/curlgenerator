using System.Text;
using Microsoft.OpenApi.Models;

namespace CurlGenerator.Core;
public class BashScriptFileGenerator : ScriptFileGenerator
{
    protected override string FileExtension => "sh";

    protected override string GenerateRequest(
        GeneratorSettings settings,
        string baseUrl,
        string verb,
        KeyValuePair<string, OpenApiPathItem> kv,
        OpenApiOperation operation)
    {
        TryLog($"Generating bash request for operation: {operation.OperationId}");

        var code = new StringBuilder();
        AppendBashSummary(verb, kv, operation, code);
        AppendBashParameters(verb, kv, operation, code);

        var route = kv.Key.Replace("{", "$").Replace("}", null);

        // Add query parameters directly to the URL if there are any
        var queryParams = operation.Parameters
            .Where(p => p.In == ParameterLocation.Query)
            .Select(p => $"{p.Name}=${{{p.Name.ConvertKebabCaseToSnakeCase()}}}")
            .ToList();

        var queryString = queryParams.Any() ? $"?{string.Join("&", queryParams)}" : string.Empty;
        code.AppendLine($"curl -X {verb.ToUpperInvariant()} \"{baseUrl}{route}{queryString}\" \\");

        if (settings.SkipCertificateCheck)
        {
            code.AppendLine("  -k \\");
        }


        code.AppendLine($"  -H \"Accept: application/json\" \\");

        // Determine content type based on request body
        var contentType = operation.RequestBody?.Content?.Keys.FirstOrDefault()
                          ?? "application/json";

        TryLog($"Content type for operation {operation.OperationId}: {contentType}");
        code.AppendLine($"  -H \"Content-Type: {contentType}\" \\");

        if (!string.IsNullOrWhiteSpace(settings.AuthorizationHeader))
        {
            code.AppendLine($"  -H \"Authorization: {settings.AuthorizationHeader}\" \\");
        }

        if (operation.RequestBody?.Content != null)
        {
            if (settings.ReadBodyFromStdin)
            {
                switch (contentType)
                {
                    case "application/octet-stream":
                        code.AppendLine($"  --data-binary @-");
                        break;
                    default:
                        var requestBodySchema = operation.RequestBody.Content[contentType].Schema;
                        var requestBodyJson = GenerateSampleJsonFromSchema(requestBodySchema);
                        code.AppendLine($"  -d@-");
                        break;
                }
            }
            else
            {
                switch (contentType)
                {
                    case "application/x-www-form-urlencoded":
                    case "multipart/form-data":
                        {
                            var formData = operation.RequestBody.Content[contentType].Schema.Properties
                                .Select(p => $"-F \"{p.Key}=${{{p.Key}}}\"")
                                .ToList();

                            for (int i = 0; i < formData.Count; i++)
                            {
                                // Only add trailing backslash if not the last item
                                if (i < formData.Count - 1)
                                {
                                    code.AppendLine(formData[i] + " \\");
                                }
                                else
                                {
                                    code.AppendLine(formData[i]);
                                }
                            }
                            break;
                        }

                    case "application/octet-stream":
                        code.AppendLine($"  --data-binary '@filename'");
                        break;
                    default:
                        // Remove the trailing backslash and newline if there is no request body
                        var currentCode = code.ToString();
                        if (currentCode.EndsWith(" \\\n") || currentCode.EndsWith(" \\\r\n"))
                        {
                            code.Length -= (currentCode.EndsWith("\r\n") ? 4 : 3); // Remove " \\\n" or " \\\r\n"
                            code.AppendLine(); // Add back just the newline
                        }
                        break;
                }
            }
        }
        else
        {
            // Remove the trailing backslash if there is no request body
            code.Length -= 2; // Remove the last backslash and newline
        }

        TryLog($"Generated bash request: {code}");

        return code.ToString();
    }

    private static void AppendBashParameters(
        string verb,
        KeyValuePair<string, OpenApiPathItem> kv,
        OpenApiOperation operation,
        StringBuilder code)
    {
        var parameters = operation.Parameters
            .Where(p =>
                p.In == ParameterLocation.Path ||
                p.In == ParameterLocation.Query ||
                p.In == ParameterLocation.Header ||
                p.In == ParameterLocation.Cookie)
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

    private static void AppendBashSummary(
        string verb,
        KeyValuePair<string, OpenApiPathItem> kv,
        OpenApiOperation operation,
        StringBuilder code)
    {
        code.AppendLine("#");
        code.AppendLine($"# Request: {verb.ToUpperInvariant()} {kv.Key}");

        if (!string.IsNullOrWhiteSpace(operation.Summary))
        {
            code.AppendLine($"# Summary: {operation.Summary}");
        }

        if (!string.IsNullOrWhiteSpace(operation.Description))
        {
            code.AppendLine($"# Description: {operation.Description}");
        }

        code.AppendLine("#");
    }
}
