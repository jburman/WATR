using HotShallot.Shared.DTOs;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace HotShallot.Web.Services;

public class ProjectContextService
{
    private readonly ProtectedLocalStorage _localStorage;

    public ProjectContextService(ProtectedLocalStorage localStorage)
    {
        _localStorage = localStorage;
    }

    public ProjectDto? SelectedProject { get; private set; }
    
    public event Action? OnProjectChanged;

    public async Task SelectProjectAsync(ProjectDto? project)
    {
        SelectedProject = project;
        OnProjectChanged?.Invoke();

        if (project != null)
        {
            try
            {
                await _localStorage.SetAsync("selectedProjectId", project.Id);
            }
            catch
            {
                // Ignore storage errors (e.g. prerendering)
            }
        }
        else
        {
            try
            {
                await _localStorage.DeleteAsync("selectedProjectId");
            }
            catch
            {
                // Ignore
            }
        }
    }

    // Keep synchronous for backward compatibility if needed, but prefer Async
    public void SelectProject(ProjectDto? project)
    {
        SelectProjectAsync(project); // Fire and forget storage update
    }

    public async Task<Guid?> GetStoredProjectIdAsync()
    {
        try
        {
            var result = await _localStorage.GetAsync<Guid>("selectedProjectId");
            return result.Success ? result.Value : null;
        }
        catch
        {
            return null;
        }
    }
}
