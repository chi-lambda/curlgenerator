using System.Text;
using Microsoft.OpenApi;

namespace CurlGenerator.Core;

public class PwshScriptFileGenerator(ISettings settings) : ScriptFileGenerator(settings)
{
    protected override string FileExtension => "ps1";
    protected override string Joiner => "`";
    protected override string CommentStart => "<#";
    protected override string CommentContinue => " ";
    protected override string CommentEnd => "#>";
    protected override string ShellName => "PowerShell";

    private readonly string CommaCrLf = "," + Environment.NewLine;

    protected override void AppendParameters(OpenApiOperation operation, StringBuilder code)
    {
        if (operation.Parameters is null)
        {
            return;
        }
        var parameters = operation.Parameters
            .Where(p => p.In is
                ParameterLocation.Path or
                ParameterLocation.Query)
            .ToArray();

        if (parameters.Length == 0)
        {
            code.AppendLine();
            return;
        }

        if (!settings.EnvironmentParameters)
        {
            code.AppendLine("param(");

            var parameterStrings = parameters.Select(parameter =>
            {
                var name = parameter.Name!.ConvertKebabCaseToSnakeCase();
                var mandatory = parameter.Required && (!settings.RequiredDefault || parameter.Schema?.Default is null) ? "True" : "False";
                return parameter.Description is null
                        ? $"""
                             [Parameter(Mandatory=${mandatory})]
                             [String] ${name},
                          """
                        : $"""
                             <# {parameter.Description} #>
                             [Parameter(Mandatory=${mandatory})]
                             [String] ${name}
                          """;
            });
            code.AppendLine(string.Join(CommaCrLf, parameterStrings));
            code.AppendLine(")");
        }
        code.AppendLine();
    }

    protected override string AsEnvironmentVariable(string name) => $"${{env:{name.ConvertKebabCaseToSnakeCase()}}}";

}
