using System.Text.Json.Serialization;
using HotShallot.Shared.DTOs;
using HotShallot.Shared.Enums;

namespace HotShallot.Shared.Json;

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
public partial class AppJsonContext : JsonSerializerContext
{
}
