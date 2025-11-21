using System.Text;
using Microsoft.OpenApi.Models;

namespace CurlGenerator.Core;

public class PwshScriptFileGenerator(ISettings settings) : ScriptFileGenerator(settings)
{
    protected override string FileExtension => "ps1";
    protected override string Joiner => "`";
    protected override string CommentStart => "<#";
    protected override string CommentContinue => " ";
    protected override string CommentEnd => "#>";
    protected override string ShellName => "PowerShell";

    protected override void AppendParameters(OpenApiOperation operation, StringBuilder code)
    {
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

        code.AppendLine("param(");

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
        }
        code.Remove(code.Length - 5, 3);

        code.AppendLine(")");
        code.AppendLine();
    }

    protected override string AsVariable(string name) => $"${name.ConvertKebabCaseToSnakeCase()}";
}
