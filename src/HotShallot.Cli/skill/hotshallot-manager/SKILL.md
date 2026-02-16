---
name: hotshallot-manager
description: Comprehensive guide for using the HotShallot CLI (shallot) to manage projects, tasks, and epics.
version: 1.0.0
---

# HotShallot Task Management

This skill provides instructions for using the `shallot` CLI tool to manage your work. It assumes `shallot` is installed and available in your system PATH.

## 1. Initial Setup

Before starting work, ensure you are logged in and have a project context set.

### Login
If you receive authentication errors (exit code 2), run:
```bash
shallot login
```
Follow the device code flow instructions provided in the output.

### Project Setup
1.  **Create a Project:** If starting a new project.
    ```bash
    shallot project create "MyProject" --description "Description of the project"
    ```
2.  **Set Context:** Set the default project to avoid repeating the `--project` flag.
    ```bash
    shallot project set "MyProject"
    ```

## 2. Managing Tasks

### Listing Tasks
To see what needs to be done:
```bash
shallot list --status Open
```
*   **Tip:** Use `--json` to parse output programmatically.
    ```bash
    shallot list --status Open --json
    ```

### Creating Tasks & Epics
Create tasks with clear titles, priorities, and a short handle.

**Standard Task:**
```bash
shallot add "Implement Login Page" --handle "login-ui" --priority High
```
*Note: The server will automatically prefix the handle with the task number (e.g., `42-login-ui`).*

**Epic (Large Feature):**
1.  Create the high-level Epic.
    ```bash
    shallot add "Authentication System" --handle "auth-epic" --type Epic --priority Critical
    ```
2.  **Capture the ID** returned in the output.

### Adding Details (Markdown)
For complex requirements, use the task body. You can reference files directly to avoid shell escaping issues.

1.  Write details to a temporary file (e.g., `temp_desc.md`).
2.  Update the task using the `@` prefix.
    ```bash
    shallot update <ID> --body @temp_desc.md
    ```
3.  Delete the temporary file.

**Best Practice for Epics:**
In the body of an Epic, maintain a checklist of sub-tasks referencing their Handles or IDs.

### Body vs. Notes: The Golden Rule
*   **BODY:** Use `shallot update <ID> --body ...` for the **Definition of Done**. This includes requirements, acceptance criteria, implementation plans, and checklists. This is the "What" and "How".
*   **NOTES:** Use `shallot note <ID> ...` for the **Activity Log**. This includes daily progress, blockers, decision records, and final verification. This is the "Status".

**❌ BAD:** Putting the implementation plan in a Note.
**✅ GOOD:** Putting the implementation plan in the Body via `shallot update`.

## 3. Workflow Lifecycle

### Claiming Work
When you start working on a task:
```bash
shallot claim <ID>
```
This assigns the task to you.

### Refining the Plan
Before writing code, update the task **Body** with your detailed technical plan.
```bash
# Write plan to file
echo "## Plan`n- [ ] Create Interface`n- [ ] Implement Class" > plan.md
# Update Body
shallot update <ID> --body @plan.md
```

### Updating Status
Move tasks through their lifecycle:
```bash
shallot update <ID> --status InProgress
```

### Adding Notes
Keep a log of your progress, decisions, and technical details. **Notes support Markdown.**
```bash
shallot note <ID> "Started implementation. Using JWT for token management."
```
For longer notes, use a file:
```bash
shallot note <ID> @progress.md
```

### Completion
When the work is done and verified:
1.  **Add a Verification Note:**
    ```bash
    shallot note <ID> "✅ Verified. Unit tests passed. Manual test of login flow successful."
    ```
2.  **Mark as Done:**
    ```bash
    shallot done <ID>
    ```

## 4. Agent Best Practices

*   **JSON Output:** Always prefer `--json` when you need to read data. It contains full fidelity fields.
*   **Handles:** Assign user-friendly handles (`shallot update <ID> --handle "NAME"`) to important tasks so you can reference them easily later without memorizing UUIDs.
*   **Error Handling:**
    *   Exit Code `0`: Success.
    *   Exit Code `2`: Auth expired -> Run `shallot login`.
    *   Exit Code `3`: Not found -> Check ID.
    *   Exit Code `4`: Conflict -> Task might be claimed by someone else.
