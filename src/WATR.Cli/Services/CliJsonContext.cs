using System.Text.Json.Serialization;
using WATR.Cli.Services;

namespace WATR.Cli.Json;

[JsonSerializable(typeof(ConfigData))]
[JsonSerializable(typeof(Tokens))]
[JsonSerializable(typeof(LocalProjectSettings))]
public partial class CliJsonContext : JsonSerializerContext
{
}
