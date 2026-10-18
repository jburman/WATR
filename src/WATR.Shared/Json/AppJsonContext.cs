using System.Text.Json.Serialization;
using WATR.Shared.DTOs;
using WATR.Shared.Enums;

namespace WATR.Shared.Json;

[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(PaginatedResponse<ProjectDto>))]
[JsonSerializable(typeof(PaginatedResponse<TaskItemSummaryDto>))]
[JsonSerializable(typeof(TaskItemDto))]
[JsonSerializable(typeof(TaskItemSummaryDto))]
[JsonSerializable(typeof(CreateTaskRequest))]
[JsonSerializable(typeof(UpdateTaskRequest))]
[JsonSerializable(typeof(PatchTaskRequest))]
[JsonSerializable(typeof(TaskNoteDto))]
[JsonSerializable(typeof(CreateTaskNoteRequest))]
[JsonSerializable(typeof(UserDto))]
[JsonSerializable(typeof(LoginOptionsRequest))]
[JsonSerializable(typeof(RegisterOptionsRequest))]
[JsonSerializable(typeof(RevokeTokenRequest))]
[JsonSerializable(typeof(DeviceLoginInitRequest))]
[JsonSerializable(typeof(DeviceLoginStatusResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(List<TaskItemSummaryDto>))]
[JsonSerializable(typeof(List<TaskNoteDto>))]
[JsonSerializable(typeof(ProjectDto))]
[JsonSerializable(typeof(CreateProjectRequest))]
[JsonSerializable(typeof(UpdateProjectRequest))]
[JsonSerializable(typeof(ObsoleteDecisionRequest))]
[JsonSerializable(typeof(TaskSearchResultDto))]
[JsonSerializable(typeof(List<TaskSearchResultDto>))]
[JsonSerializable(typeof(WatrStatusDto))]
public partial class AppJsonContext : JsonSerializerContext
{
}
