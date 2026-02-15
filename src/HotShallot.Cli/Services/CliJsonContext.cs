using System.Text.Json.Serialization;
using HotShallot.Cli.Services;

namespace HotShallot.Cli.Json;

[JsonSerializable(typeof(ConfigData))]
[JsonSerializable(typeof(Tokens))]
public partial class CliJsonContext : JsonSerializerContext
{
}
