using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.OpenApi.Models;

namespace CurlGenerator.Core;

public abstract class ScriptFileGenerator(ISettings settings)
{
    protected readonly ISettings settings = settings;
    protected abstract string FileExtension { get; }
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
        foreach (var kv in document.Paths)
        {
            TryLog($"Processing path: {kv.Key}");
            foreach (var operations in kv.Value.Operations)
            {
                TryLog($"Processing operation: {operations.Key}");

                var operation = operations.Value;
                var verb = operations.Key.ToString().CapitalizeFirstCharacter();
                var name = generator.GetOperationName(document, kv.Key, verb, operation);

                var filename = $"{name.CapitalizeFirstCharacter()}.{FileExtension}";

                var code = new StringBuilder();
                code.AppendLine(GenerateRequest(baseUrl, verb, kv, operation));

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

    protected static string GenerateSampleJsonFromSchema(OpenApiSchema? schema)
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

    protected static object GenerateSampleObjectFromSchema(OpenApiSchema schema)
    {
        if (schema.Example != null)
        {
            return ConvertOpenApiAnyToObject(schema.Example);
        }

        switch (schema.Type)
        {
            case "object":
                var obj = new Dictionary<string, object>();
                if (schema.Properties != null)
                {
                    foreach (var prop in schema.Properties)
                    {
                        obj[prop.Key] = GenerateSampleObjectFromSchema(prop.Value);
                    }
                }
                return obj;

            case "array":
                if (schema.Items != null)
                {
                    return new[] { GenerateSampleObjectFromSchema(schema.Items) };
                }
                return new object[0];

            case "string":
                return schema.Format switch
                {
                    "date" => DateTime.Today.ToString("yyyy-MM-dd"),
                    "date-time" => DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    "email" => "user@example.com",
                    "uri" => "https://example.com",
                    _ => "string"
                };

            case "integer":
                return 0;

            case "number":
                return 0.0;

            case "boolean":
                return false;

            default:
                return "value";
        }
    }

    protected static object ConvertOpenApiAnyToObject(Microsoft.OpenApi.Any.IOpenApiAny openApiAny)
    {
        return openApiAny switch
        {
            Microsoft.OpenApi.Any.OpenApiString str => str.Value,
            Microsoft.OpenApi.Any.OpenApiInteger integer => integer.Value,
            Microsoft.OpenApi.Any.OpenApiLong longVal => longVal.Value,
            Microsoft.OpenApi.Any.OpenApiFloat floatVal => floatVal.Value,
            Microsoft.OpenApi.Any.OpenApiDouble doubleVal => doubleVal.Value,
            Microsoft.OpenApi.Any.OpenApiBoolean boolVal => boolVal.Value,
            Microsoft.OpenApi.Any.OpenApiDateTime dateTime => dateTime.Value,
            Microsoft.OpenApi.Any.OpenApiObject obj => obj.ToDictionary(kv => kv.Key, kv => ConvertOpenApiAnyToObject(kv.Value)),
            Microsoft.OpenApi.Any.OpenApiArray array => array.Select(ConvertOpenApiAnyToObject).ToArray(),
            _ => openApiAny.ToString() ?? "value"
        };
    }


    protected abstract string GenerateRequest(
        string baseUrl,
        string verb,
        KeyValuePair<string, OpenApiPathItem> kv,
        OpenApiOperation operation);
}
