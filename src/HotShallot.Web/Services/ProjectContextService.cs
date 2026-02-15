using HotShallot.Shared.DTOs;

namespace HotShallot.Web.Services;

public class ProjectContextService
{
    public ProjectDto? SelectedProject { get; private set; }
    
    public event Action? OnProjectChanged;

    public void SelectProject(ProjectDto? project)
    {
        SelectedProject = project;
        OnProjectChanged?.Invoke();
    }
}
