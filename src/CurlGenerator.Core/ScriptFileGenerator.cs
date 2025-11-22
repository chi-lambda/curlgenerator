using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.OpenApi;

namespace CurlGenerator.Core;

public abstract class ScriptFileGenerator(ISettings settings)
{
    protected readonly ISettings settings = settings;
    protected abstract string FileExtension { get; }
    protected abstract string Joiner { get; }
    protected abstract string CommentStart { get; }
    protected abstract string CommentContinue { get; }
    protected abstract string CommentEnd { get; }
    protected abstract string ShellName{ get; }

    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        ReferenceHandler = ReferenceHandler.Preserve,
        WriteIndented = true
    };

    public async Task<GeneratorResult> Generate()
    {
        TryLog("Starting generation...");
        TryLog($"Settings: {SerializeObject(settings)}");

        var document = await OpenApiDocumentFactory.CreateAsync(settings.OpenApiPath);
        TryLog($"Document: {SerializeObject(document)}");

        var generator = new OperationNameGenerator();

        var baseUrl = settings.BaseUrl + document.Servers?.FirstOrDefault()?.Url;
        if (!Uri.IsWellFormedUriString(baseUrl, UriKind.Absolute) &&
            settings.OpenApiPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            baseUrl = new Uri(settings.OpenApiPath)
                          .GetLeftPart(UriPartial.Authority) +
                      baseUrl;
        }

        TryLog($"Base URL: {baseUrl}");

        return GenerateCode(document, generator, baseUrl);
    }

    private GeneratorResult GenerateCode(
        OpenApiDocument document,
        OperationNameGenerator generator,
        string baseUrl)
    {
        var files = new List<ScriptFile>();
        foreach (var pathItem in document.Paths)
        {
            TryLog($"Processing path: {pathItem.Key}");
            foreach (var operations in pathItem.Value.Operations ?? [])
            {
                TryLog($"Processing operation: {operations.Key}");

                var operation = operations.Value;
                var verb = operations.Key.ToString().CapitalizeFirstCharacter();
                var name = generator.GetOperationName(document, pathItem.Key, verb, operation);

                var filename = $"{name.CapitalizeFirstCharacter()}.{FileExtension}";

                var code = new StringBuilder();
                code.AppendLine(GenerateRequest(baseUrl, verb, pathItem, operation));

                TryLog($"Generated code for {filename}:\n{code}");

                files.Add(new ScriptFile(filename, code.ToString()));
            }
        }

        return new GeneratorResult(files);
    }

    protected void TryLog(string message)
    {
        if (settings.LogFile is null) { return; }
        try
        {
            using var writer = new StreamWriter(settings.LogFile, true);
            writer.WriteLine($"{DateTime.Now}: {message}");
        }
        catch
        {
            // Ignore
        }
    }

    protected static string SerializeObject(object obj)
    {
        try
        {
            return JsonSerializer.Serialize(obj, jsonOptions);
        }
        catch
        {
            return obj?.ToString() ?? "null";
        }
    }

    protected static string GenerateSampleJsonFromSchema(IOpenApiSchema? schema)
    {
        if (schema == null)
            return "{}";

        try
        {
            var sampleObject = GenerateSampleObjectFromSchema(schema);
            return JsonSerializer.Serialize(sampleObject, jsonOptions);
        }
        catch
        {
            return "{}";
        }
    }

    protected static object? GenerateSampleObjectFromSchema(IOpenApiSchema schema)
    {
        if (schema.Example != null)
        {
            return ConvertOpenApiAnyToObject(schema.Example);
        }

        switch (schema.Type)
        {
            case JsonSchemaType.Object:
                var obj = new Dictionary<string, object?>();
                if (schema.Properties != null)
                {
                    foreach (var prop in schema.Properties)
                    {
                        obj[prop.Key] = GenerateSampleObjectFromSchema(prop.Value);
                    }
                }
                return obj;

            case JsonSchemaType.Array:
                if (schema.Items != null)
                {
                    return new[] { GenerateSampleObjectFromSchema(schema.Items) };
                }
                return new object[0];

            case JsonSchemaType.String:
                return schema.Format switch
                {
                    "date" => DateTime.Today.ToString("yyyy-MM-dd"),
                    "date-time" => DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    "email" => "user@example.com",
                    "uri" => "https://example.com",
                    _ => "string"
                };

            case JsonSchemaType.Integer:
                return 0;

            case JsonSchemaType.Number:
                return 0.0;

            case JsonSchemaType.Boolean:
                return false;

            default:
                return "value";
        }
    }

    protected static object? ConvertOpenApiAnyToObject(JsonNode? openApiAny)
    {
        if(openApiAny is null)
        {
            return null;
        }
        return openApiAny.GetValueKind() switch
        {
            JsonValueKind.String => openApiAny.GetValue<string>(),
            JsonValueKind.Number => openApiAny.GetValue<double>(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Object => openApiAny.AsObject().ToDictionary(kv => kv.Key, kv => ConvertOpenApiAnyToObject(kv.Value)),
            JsonValueKind.Array => openApiAny.AsArray().Select(ConvertOpenApiAnyToObject).ToArray(),
            _ => openApiAny.ToString() ?? "value"
        };
    }

    protected void AppendSummary(
        string verb,
        KeyValuePair<string, IOpenApiPathItem> pathItem,
        OpenApiOperation operation,
        StringBuilder code)
    {
        code.AppendLine(CommentStart);
        code.AppendLine($"{CommentContinue} Request: {verb.ToUpperInvariant()} {pathItem.Key}");

        if (!string.IsNullOrWhiteSpace(operation.Summary))
        {
            code.AppendLine($"{CommentContinue} Summary: {operation.Summary}");
        }

        if (!string.IsNullOrWhiteSpace(operation.Description))
        {
            code.AppendLine($"{CommentContinue} Description: {operation.Description}");
        }

        code.AppendLine(CommentEnd);
    }

    protected string CreateQueryString(OpenApiOperation operation)
    {
        if (operation.Parameters is not null && operation.Parameters.Any())
        {
            var parameters = operation.Parameters
                .Where(p => p.In == ParameterLocation.Query)
                .Select(p => $"{p.Name}={AsVariable(p.Name!)}");
            return "?" + string.Join('&', parameters);
        }
        return string.Empty;
    }


    protected string GenerateRequest(
        string baseUrl,
        string verb,
        KeyValuePair<string, IOpenApiPathItem> pathItem,
        OpenApiOperation operation)
    {
        TryLog($"Generating {ShellName} request for operation: {operation.OperationId}");

        var code = new StringBuilder();
        AppendSummary(verb, pathItem, operation, code);
        AppendParameters(operation, code);

        var route = pathItem.Key.Replace("{", "$").Replace("}", null);
        var queryString = CreateQueryString(operation);
        code.AppendLine($"curl -X {verb.ToUpperInvariant()} \"{baseUrl}{route}{queryString}\" {Joiner}");

        if (settings.SkipCertificateCheck)
        {
            code.AppendLine($"  -k {Joiner}");
        }

        code.AppendLine($"  -H 'Accept: {settings.ContentType}' {Joiner}");

        // Determine content type based on request body
        var contentType = operation.RequestBody?.Content?.Keys.FirstOrDefault()
                          ?? "application/json";

        TryLog($"Content type for operation {operation.OperationId}: {contentType}");
        code.AppendLine($"  -H 'Content-Type: {contentType}' {Joiner}");

        if (!string.IsNullOrWhiteSpace(settings.AuthorizationHeader))
        {
            code.AppendLine($"  -H 'Authorization: {settings.AuthorizationHeader}' {Joiner}");
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
                        var schema = operation.RequestBody.Content[contentType].Schema;
                        if (schema is not null)
                        {
                            var formData = schema.Properties
                                .Select(p => $"-F \"{p.Key}=${{{p.Key}}}\"")
                                .ToList();

                            for (int i = 0; i < formData.Count; i++)
                            {
                                // Only add trailing backslash if not the last item
                                if (i < formData.Count - 1)
                                {
                                    code.AppendLine(formData[i] + $" {Joiner}");
                                }
                                else
                                {
                                    code.AppendLine(formData[i]);
                                }
                            }
                        }
                        break;
                    case "application/octet-stream":
                        code.AppendLine($"  --data-binary '@filename'");
                        break;
                    case "application/json":
                        var requestBodySchema = operation.RequestBody.Content[contentType].Schema;
                        var requestBodyJson = GenerateSampleJsonFromSchema(requestBodySchema);

                        code.AppendLine($"  -d '{requestBodyJson}'");
                        break;
                    default:
                        // Remove the trailing backslash and newline if there is no request body
                        var currentCode = code.ToString();
                        if (currentCode.EndsWith($" {Joiner}\n") || currentCode.EndsWith($" {Joiner}\r\n"))
                        {
                            code.Length -= currentCode.EndsWith("\r\n") ? 4 : 3; // Remove " \\\n" or " \\\r\n"
                            code.AppendLine(); // Add back just the newline
                        }
                        break;
                }
            }
        }

        TryLog($"Generated {ShellName} request: {code}");

        return code.ToString();
    }

    protected abstract void AppendParameters(OpenApiOperation operation, StringBuilder code);
    protected abstract string AsVariable(string name);
}
