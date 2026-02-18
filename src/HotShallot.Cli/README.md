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

## Initialization

Before using the CLI in a new folder, you must initialize it with the server URL. This creates a local configuration file (`.shallot/settings.json`) that links the directory to a specific project context.

```bash
shallot init "https://hotshallot.azurewebsites.net"
```

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
- `--body <TEXT>`: Update description. Use `@filename` to read from a file.
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
Append a comment or update to a task. Notes support **Markdown**.
```bash
shallot note <TASK_ID> "Started working on the backend API."
```
To load a note from a file (useful for long logs):
```bash
shallot note <TASK_ID> @log.txt
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

## For Agents / Scripting

The CLI has been enhanced to support automation and agentic workflows.

### Parsing Task IDs
The `add` command outputs the full task ID (GUID) upon success, making it easy to capture and use in subsequent commands.
```text
✅ Created task 019c641e-afca-786a-91f9-5a7040386c64
```

### JSON Output
Use the `--json` flag with `list` and `show` commands to get structured JSON output. This is ideal for parsing with tools like `jq` or PowerShell's `ConvertFrom-Json`.

**Example: Get all task IDs in PowerShell**
```powershell
shallot list --json | ConvertFrom-Json | Select-Object -ExpandProperty Id
```

### Full IDs in List
The default `list` view now shows the full Task ID and the Handle (e.g. `PROJ-123`) to ensure unambiguous identification.

### File Input for Descriptions
For complex or multi-line descriptions (especially Markdown), it is recommended to write the content to a temporary file and reference it using the `@` prefix. This avoids shell escaping issues.

1. Write content to a temp file (e.g., `desc.md`).
2. Run command: `shallot update <ID> --body @desc.md`
3. Delete the temp file.

This is supported by both `add` and `update` commands.

## Best Practices for Agents

When managing tasks autonomously, follow these guidelines to ensure clarity and traceability for human users.

### User-Friendly Handles
By default, tasks may have numeric or auto-generated handles. You should update these to meaningful, human-readable identifiers that reflect the task's context (e.g., `FE-LOGIN-01`, `API-AUTH-BUG`).
```bash
shallot update <TASK_ID> --handle "POS-HARDWARE"
```

### Rich Descriptions (Markdown)
The task **Body** supports full Markdown. Use it to store comprehensive requirements, acceptance criteria, and implementation details.
- **For Epics:** Maintain a list of references to sub-tasks in the body. Prefer using the human-readable **Handle** for these references, but UUIDs are also acceptable.
    ```markdown
    ## Sub-tasks
    - [ ] [POS-UI-01] Design Cash Register Interface
    - [ ] [POS-API-02] Implement Transaction Endpoint
    ```
- **Updating the Body:**
    ```bash
    shallot update <TASK_ID> --body @description.md
    ```

### Completion & Verification Logs
Do not just mark a task as `Completed`. Before closing a task, use the `note` command to add a **Verification Log**.
- Explain *how* the task was completed.
- detailed the steps taken to verify the fix or feature.
- This field supports Markdown, so use code blocks for command output or test results.

```bash
shallot note <TASK_ID> "✅ **Verified**\n\nRan integration tests:\n\`\`\`powershell\n./test-script.ps1\n\`\`\`\nAll passed."
```

### Direct Execution vs Scripts
Prefer executing `shallot.exe` directly via your shell command tool rather than creating temporary `.ps1` or `.sh` files. This keeps the file system clean.

- **Simple Commands:**
    ```powershell
    .\output\shallot.exe add "Cleanup Temp Files" --priority Low
    ```
- **Complex Strings:** For multi-line bodies or special characters, use a concise PowerShell command string:
    ```powershell
    powershell -Command ".\output\shallot.exe update <ID> --body 'Line 1`nLine 2'"
    ```
- **Escaping:** Be mindful of shell escaping for quotes (`"`) and backticks (`` ` ``).
