using System.ComponentModel;

namespace ApiLens.Cli.Commands;

public enum OutputFormat
{
    [Description("Table format for human reading")]
    Table,

    [Description("JSON format for machine processing")]
    Json,

    [Description("Markdown format for documentation")]
    Markdown
}