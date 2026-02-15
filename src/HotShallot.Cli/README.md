# HotShallot CLI (`shallot`)

The official command-line interface for **HotShallot**, the spicy task management system.

## Overview

The `shallot` CLI allows you to manage your projects and tasks directly from your terminal. It supports authentication via a secure device flow, project context management, and comprehensive task operations including creation, updates, and claiming.

## Installation

### From Source
1.  Navigate to the CLI project directory:
    ```bash
    cd src/HotShallot.Cli
    ```
2.  Build and run:
    ```bash
    dotnet run -- [command]
    ```
3.  (Optional) Publish as a standalone executable:
    ```bash
    dotnet publish -c Release -r win-x64
    ```
    The executable `shallot.exe` will be in `bin/Release/net10.0/win-x64/publish/`.

## Authentication

Before interacting with the system, you must log in.

### Login
Initiates a device code authentication flow.
```bash
shallot login
```
This command will display a user code and a URL. Open the URL in your browser, log in to the web dashboard, and enter the code to authorize the CLI.

### Logout
Clears your local session tokens.
```bash
shallot logout
```

## Configuration

To avoid typing the project name for every command, you can set a default project context.

### Set Default Project
```bash
shallot project set "MyProjectName"
```
Once set, all subsequent commands will apply to this project unless overridden.

## Task Management

### List Tasks
View tasks in the current project.
```bash
shallot list
```
**Options:**
- `--status <STATUS>`: Filter by status (Open, InProgress, Completed, Blocked, Cancelled).
- `--project <NAME>`: Explicitly specify the project.

### Add Task
Create a new task.
```bash
shallot add "Fix login bug" --priority High
```
**Options:**
- `--body <TEXT>`: Add a description.
- `--body @filename.md`: Read description from a file.
- `--priority <LEVEL>`: Set priority (Low, Medium, High, Critical).

### Show Task
View full details of a specific task, including description and notes.
```bash
shallot show <TASK_ID>
```
**Options:**
- `--json`: Output raw JSON data (useful for scripting).

### Update Task
Modify an existing task.
```bash
shallot update <TASK_ID> --title "New Title" --status InProgress
```
**Options:**
- `--title <TEXT>`
- `--body <TEXT>`
- `--status <STATUS>`
- `--priority <LEVEL>`

### Claim Task
Assign a task to yourself.
```bash
shallot claim <TASK_ID>
```

### Complete Task
Mark a task as completed.
```bash
shallot done <TASK_ID>
```

### Add Note
Append a comment or update to a task.
```bash
shallot note <TASK_ID> "Started working on the backend API."
```

## Examples

**Workflow: Starting a new feature (Bash)**
1.  **List open tasks:**
    ```bash
    shallot list --status Open
    ```
2.  **Claim a task:**
    ```bash
    shallot claim 01950a...
    ```
3.  **Add a note:**
    ```bash
    shallot note 01950a... "Investigating root cause..."
    ```
4.  **Mark as done:**
    ```bash
    shallot done 01950a...
    ```

**Workflow: Scripting with PowerShell**
Get all high-priority open tasks and export them to a CSV file.
```powershell
# Get tasks as JSON, convert from JSON, filter, and export
shallot list --status Open --json | ConvertFrom-Json | Where-Object { $_.Priority -eq 'High' } | Export-Csv -Path high_priority_tasks.csv -NoTypeInformation
```

**Bulk Update with PowerShell**
Move all "Open" tasks to "InProgress".
```powershell
$tasks = shallot list --status Open --json | ConvertFrom-Json
foreach ($task in $tasks) {
    shallot update $task.Id --status InProgress
}
```
