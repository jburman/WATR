---
name: watr-manager
description: Comprehensive guide for using WATR via MCP Tools or the CLI (watr) to manage projects, tasks, epics, and semantic search.
version: 2.0.0
---

# WATR Task Management

WATR provides two interfaces for AI coding agents:
1. **Model Context Protocol (MCP) Server (`watr mcp`)**: Direct, structured tool calling without shell execution overhead or escaping hazards (Recommended).
2. **CLI (`watr`)**: Interactive or scripted command-line operations with JSON support.

---

## 1. Configuring WATR as an MCP Server

When configuring WATR for Antigravity or any MCP-compatible coding agent, register `watr mcp` in your Antigravity MCP configuration (`~/.gemini/antigravity/mcp_config.json` or project-level `.gemini/settings.json`):

```json
{
  "mcpServers": {
    "watr": {
      "command": "watr",
      "args": ["mcp"]
    }
  }
}
```

> **Note:** Ensure `watr` is initialized and logged in via `watr init <SERVER_URL>` and `watr login` before starting the MCP server.

---

## 2. MCP Tool Suite Reference

When the WATR MCP server is active, prefer calling these tools directly:

| Tool Name | Type | Description | Key Parameters |
| :--- | :--- | :--- | :--- |
| `watr_status` | Read | Get server URL, authenticated user, and active project context | None |
| `watr_list_projects` | Read | List all accessible projects | `page`, `pageSize` |
| `watr_create_project` | Write | Create a new project | `name`, `description` |
| `watr_set_project` | Write | Set active project in local `.watr/settings.json` or global config | `name`, `global` (bool) |
| `watr_list_tasks` | Read | List tasks with filtering | `project`, `status`, `priority`, `assignedTo`, `page`, `pageSize` |
| `watr_search_tasks` | Read | **Semantic vector search** across task titles, bodies, and notes | `query`, `project`, `status`, `top`, `minScore` |
| `watr_get_task` | Read | Get full task details including markdown body and notes | `identifier` (UUID or handle), `project` |
| `watr_create_task` | Write | Create a new task or epic | `title`, `project`, `body`, `handle`, `taskType`, `priority` |
| `watr_update_task` | Write | Update task properties | `id`, `title`, `body`, `status`, `priority`, `handle`, `taskType` |
| `watr_claim_task` | Write | Assign task to authenticated user | `id` |
| `watr_release_task` | Write | Release assigned task back to pool | `id` |
| `watr_complete_task` | Write | Mark task status as Done | `id` |
| `watr_add_note` | Write | Add markdown note to task activity log | `id`, `content` |

---

## 3. Semantic Vector Search

WATR features local, CPU-based semantic vector embeddings (`all-MiniLM-L6-v2`) generated asynchronously in the background for all task titles, descriptions, and notes.

### Using MCP:
```json
// Tool Call: watr_search_tasks
{
  "query": "fix memory leak in background worker",
  "minScore": 0.25,
  "top": 5
}
```

### Using CLI:
```bash
watr search "fix memory leak in background worker" --min-score 0.25 --top 5
# With JSON output
watr search "database connection timeouts" --json
```

Use semantic search when investigating related tasks, existing bugs, or prior work where exact keywords may vary.

---

## 4. CLI Workflow & Commands

If using the CLI directly:

### Initial Setup
```bash
watr init https://watr.example.com
watr login
watr project create "MyProject" --description "Project description"
watr project set "MyProject"
```

### Managing Tasks
```bash
# List tasks
watr list --status Open --json

# Semantic search
watr search "database connection timeout"

# Create standard task
watr add "Implement OAuth flow" --handle "oauth-ui" --type Task --priority High

# Create epic
watr add "Security & Authentication" --handle "sec-epic" --type Epic --priority Critical

# Inspect task
watr get <ID-or-Handle> --json

# Claim task
watr claim <ID>

# Update body (Definition of Done)
watr update <ID> --body @plan.md

# Update status
watr update <ID> --status InProgress

# Add progress note
watr note <ID> "Implemented PKCE code verifier and challenge."

# Complete task
watr note <ID> "✅ Verified with unit and integration tests."
watr done <ID>
```

---

## 5. Body vs. Notes: The Golden Rule

* **BODY:** The **Definition of Done**. Requirements, acceptance criteria, implementation plans, and checklists. Update via `watr_update_task` (MCP) or `watr update <ID> --body ...` (CLI).
* **NOTES:** The **Activity Log**. Progress updates, roadblocks, decision records, and verification results. Add via `watr_add_note` (MCP) or `watr note <ID> ...` (CLI).

❌ **BAD:** Putting implementation plans or architecture requirements in a Note.  
✅ **GOOD:** Putting implementation plans in the Task Body, and progress / completion verification in Notes.

---

## 6. Agent Best Practices

1. **Prefer MCP Tools:** Use MCP tools (`watr_*`) whenever available. They provide native JSON arguments, typed responses, and eliminate escaping risks with multi-line Markdown.
2. **Handle Context:** Use friendly handles (`login-auth`, `db-pooling`) for key tasks. The server automatically prefixes handles with the task number (e.g. `12-login-auth`).
3. **Semantic Querying:** Before creating duplicate work or searching for existing bugs, run `watr_search_tasks` / `watr search`.
4. **Error Handling:**
   - Auth errors: Run `watr login` (CLI).
   - Project unconfigured: Run `watr_set_project` (MCP) or `watr project set` (CLI).
   - Task claimed by another user: Check assignee before modifying.
