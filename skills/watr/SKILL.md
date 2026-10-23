---
name: watr
description: >-
  Use this skill whenever interacting with WATR (Where's All the Records)—a task
  management and decision support system for AI coding agents—via the WATR MCP
  server (watr_* tools) or the watr CLI. Activate when the user mentions WATR,
  asks to check, claim, create, update, or complete tasks/epics/bugs, asks "why"
  a system or architectural decision was made, wants to record or offload
  architectural decisions, design rationale, or investigation findings out of
  model context, or searches historical project records by meaning.
---

# WATR Task Management & Decision Support Skill

**WATR** (Where's All the Records) is both a **task management** and **decision support system** with built-in semantic vector search.

It serves two complementary roles for AI coding agents and their human supervisors:
1. **Task Coordination:** Track epics, tasks, and bugs, claim work in progress, and prevent duplicate effort across agents.
2. **Decision Support & Context Offloading:** Act as a persistent external knowledge store for architectural decisions, trade-offs, root-cause analyses, and the rationale behind *why* things are the way they are. Offloading this knowledge into WATR keeps the model's context window lean while allowing future sessions to look up historical context and "why" answers on demand via semantic search.

Coding agents can interact with WATR through two interfaces:
1. **MCP Server (`watr_*` tools) — Recommended:** Native, strongly-typed JSON tool calls via `watr mcp` with zero shell-escaping issues.
2. **CLI (`watr`) — Fallback:** Command-line execution supporting `--json` output and `@file.md` inputs.

---

## 1. Setup & Workspace Configuration

### MCP Server Registration (`mcp_config.json`)
If the `watr` MCP tools are not yet registered in the agent environment, configure `watr mcp` in `~/.gemini/antigravity/mcp_config.json` (or workspace `.agents/mcp_config.json`):

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

### Prerequisite Auth & Project Context
- Both `watr` and `watr mcp` share credentials stored in `~/.watr/tokens.dat` and server config in `~/.watr/config.json`.
- Project context is automatically resolved from `.watr/settings.json` in the workspace root.
- If `watr_status` or `watr status` reports an unauthenticated user or missing server URL, run:
  ```bash
  watr init <SERVER_URL>
  watr login
  ```
- If no active project is linked to the workspace, link one using `watr_set_project` (MCP) or `watr project set <NAME>` (CLI). Once linked in `.watr/settings.json`, omit the `project` parameter on subsequent calls.

---

## 2. Standard Agent Workflow

Follow this lifecycle when working on tasks or investigating architectural questions in a WATR-enabled repository:

1. **Check Context & Search Prior Decisions First:**
   - Call `watr_status` to confirm the active project.
   - Before creating new tasks, changing architecture, or answering questions about *why* something works a certain way, run `watr_search_decisions` (and `watr_list_decisions`) to retrieve prior architectural decisions and design rationale, plus `watr_search_tasks` and `watr_list_tasks` for related tasks and bugs.
   - Check `status == "Obsolete"` and `supersededByHandle` (or `[Obsolete]` / `[Superseded by ...]` in `matchedPreview`) to ensure you are following the active decision rather than an obsolete or superseded one.
2. **Claim Before Coding:**
   - Call `watr_get_task` (`idOrHandle`) to read the full Markdown `body` (requirements, context, acceptance criteria), `tags`, and existing `notes` (prior decisions and activity).
   - Call `watr_claim_task` (`idOrHandle`) to assign the task to yourself and transition its status to `InProgress` so other agents do not duplicate the work.
3. **Maintain the Plan & Context in the Task Body:**
   - Use `watr_update_task` to keep the task `body` updated with the **Definition of Done**, architectural context, implementation plan, or sub-task checklist.
4. **Offload Decisions, Findings & Progress:**
   - **Standalone Architectural Decisions (ADRs):** Call `watr_record_decision` (`title`, `handle`, `body`, `tags`, `supersedesHandle`) to record architectural or design decisions (`TaskType = "Decision"`, `Status = "Completed"`) in a single call. If this decision replaces one or more older decisions, pass comma-separated handles/IDs in `supersedesHandle` so the older records are automatically marked `Obsolete` with `SupersededByHandle` pointing to the new decision. Use `watr_update_decision` to update an existing decision and `watr_obsolete_decision` to mark a decision obsolete.
   - **Task-Specific Findings:** Use `watr_add_note` to offload key implementation trade-offs ("why Option A was chosen over Option B"), root-cause findings, and milestones on an active task so they do not need to remain solely in the model's conversation context.
   - If blocked on external input, set `status: "Blocked"` via `watr_update_task` and explain why in a note.
5. **Verify & Complete:**
   - Run builds and tests to verify your changes.
   - Complete the task using `watr_complete_task` with a `verificationNote` summarizing the final implementation, key decisions made, and how it was verified.

---

## 3. Golden Rule: Task Body vs. Task Notes

Keep the distinction between **Body** and **Notes** clean for human supervisors and future agent lookups:

| Field | Purpose | What Belongs Here | How to Update |
| :--- | :--- | :--- | :--- |
| **`body`** | **Definition of Done, Plan & Design Context** | Problem statement, architectural context/rationale, requirements, acceptance criteria, implementation checklist, and (for Epics) links to child task handles. | `watr_update_task`, `watr_update_decision`, or `watr_record_decision` / `watr update <ID> --body @file.md` |
| **`notes`** | **Chronological Activity & Decision Log** | Progress updates, architectural decisions & trade-offs discovered during implementation, root-cause findings, roadblocks, and final verification / test logs. | `watr_add_note` or `watr_complete_task` / `watr note <ID> ...` |

- **Built-in Types & Sub-Types:** Every project always has `Task` and `Decision` types built in, and projects can configure additional custom `Task` sub-types (e.g., `Epic`, `Bug`, `Story`) via `watr_create_project` or `watr_update_project`.
- **Task Handles:** When creating a task or decision record (`watr_create_task`, `watr_record_decision`, or `watr add`), always provide a concise, lowercase kebab-case `handle` slug (e.g., `login-fix`, `adr-sqlite-vectors`). The server automatically prefixes it with the project's incremental task number (e.g., `42-login-fix`).
- **Tags:** Attach comma-separated domain/component `tags` (e.g., `"auth,sqlite,security"`) so future searches and lists can filter by subsystem.
- **Epics:** Create Epics with `taskType: "Epic"` and list child task handles in the Epic's `body`:
  ```markdown
  ## Sub-tasks
  - [ ] `42-auth-models` — Add refresh token entity
  - [ ] `43-auth-endpoint` — Implement `/api/auth/revoke`
  ```
- **Standalone Decision / Knowledge Records:** Use `watr_record_decision` (or `watr add --type Decision`) to offload architectural decisions, "why" rationale, and system invariants. Records with `taskType: "Decision"` automatically default to `Completed` status so they do not clutter the open work queue while remaining prioritized in semantic searches (`watr_search_decisions`). Use `watr_get_decision`, `watr_list_decisions`, `watr_update_decision`, and `watr_obsolete_decision` to manage decisions directly. When superseded or retired, decisions transition to `Obsolete` status (`Status = "Obsolete"`).

---

## 4. MCP Tools Reference (`watr mcp`)

Always prefer calling MCP tools when the `watr` MCP server is connected. All task and decision tools accept **either a GUID or a handle** (e.g., `"42-login-fix"`, `"12-adr-sqlite-vectors"`) in `idOrHandle`.

| Tool Name | Parameters (`*` = required) | Description |
| :--- | :--- | :--- |
| `watr_status` | *(none)* | Returns `serverUrl`, `userDisplayName`, `userEmail`, and `activeProject`. |
| `watr_list_projects` | `page?` (1), `pageSize?` (50) | Lists all projects accessible to the authenticated user. |
| `watr_create_project` | `name*`, `description?`, `taskTypes?` | Creates a new project (`Task` and `Decision` are always included, plus any comma-separated custom `taskTypes` such as `"Epic,Bug"`) and binds it to `.watr/settings.json`. |
| `watr_update_project` | `name?`, `description?`, `taskTypes?`, `project?` | Updates a project's name, description, or custom task sub-types (`Task` and `Decision` are always preserved). |
| `watr_set_project` | `project*` | Links the current workspace directory (`.watr/settings.json`) to `project`. |
| `watr_list_tasks` | `project?`, `status?`, `priority?`, `taskType?`, `tag?`, `assignedTo?`, `page?`, `pageSize?` | Lists tasks (defaults `taskType` to `!Decision`). Filter `status` (`Open`, `InProgress`, `Completed`, `Blocked`, `Cancelled`, `Obsolete`), `priority` (`None`, `Low`, `Medium`, `High`, `Critical`), `taskType` (`Task`, `Epic`, `Bug`), `tag`, or `assignedTo` (`"me"`). |
| `watr_search_tasks` | `query*`, `project?`, `status?`, `taskType?`, `tag?`, `top?` (10), `minScore?` (0.25) | **Semantic Vector Search (Tasks):** Searches non-decision task titles, bodies, tags, and notes by natural-language meaning, returning ranked results with `similarityScore` and context-aware `matchedPreview`. |
| `watr_list_decisions` | `project?`, `status?`, `tag?`, `page?`, `pageSize?` | Lists architectural decision records (`TaskType = "Decision"`). Filter by `status` (`Completed`, `Obsolete`) or `tag`. |
| `watr_search_decisions` | `query*`, `project?`, `status?`, `tag?`, `top?` (10), `minScore?` (0.25) | **Semantic Vector Search (Decisions):** Searches architectural decisions (`TaskType = "Decision"`), rationale, tags, and notes by natural-language meaning. |
| `watr_get_decision` | `idOrHandle*`, `project?` | Gets full decision details including Markdown `body`, `tags`, `supersededByHandle`, `supersedesHandles`, and chronological `notes`. |
| `watr_get_task` | `idOrHandle*`, `project?` | Gets full task details including Markdown `body`, `tags`, `supersededByHandle`, `supersedesHandles`, and chronological `notes`. |
| `watr_create_task` | `title*`, `handle*`, `body?`, `priority?`, `taskType?`, `status?`, `tags?`, `supersededByHandle?`, `supersedesHandles?`, `project?` | Creates a new task (`Task`, `Bug`, `Epic`). Supports raw multi-line Markdown in `body`. |
| `watr_record_decision` | `title*`, `handle*`, `body*`, `tags?`, `supersedesHandle?`, `project?` | **Single-Call Decision Offloading:** Records an architectural or design decision (`TaskType = "Decision"`, `Status = "Completed"`) and optionally accepts one or more comma-separated handles/IDs in `supersedesHandle` to automatically mark those previous decisions `Obsolete` with `SupersededByHandle` pointing to the new decision. |
| `watr_update_decision` | `idOrHandle*`, `title?`, `body?`, `status?`, `handle?`, `tags?`, `supersededByHandle?`, `supersedesHandles?`, `project?` | Partially updates any specified fields on an existing architectural decision in a single round-trip. |
| `watr_obsolete_decision` | `idOrHandle*`, `supersededByHandle?`, `project?` | Marks an existing decision as `Obsolete` (`Status = "Obsolete"`) and optionally sets `supersededByHandle` to the newer decision that replaced it. |
| `watr_update_task` | `idOrHandle*`, `title?`, `body?`, `status?`, `priority?`, `handle?`, `taskType?`, `tags?`, `supersededByHandle?`, `supersedesHandles?`, `project?` | Partially updates any specified fields on an existing task in a single round-trip. |
| `watr_claim_task` | `idOrHandle*`, `project?` | Assigns the task to the current user and sets status to `InProgress`. |
| `watr_release_task` | `idOrHandle*`, `project?` | Unassigns the task and reverts status to `Open`. |
| `watr_complete_task` | `idOrHandle*`, `verificationNote?`, `project?` | Appends `verificationNote` (if provided) and marks the task `Completed`. |
| `watr_add_note` | `idOrHandle*`, `content*`, `project?` | Appends a Markdown note to a task's or decision's activity log. |

### Valid Enum Values
- **`status`:** `Open`, `InProgress`, `Completed`, `Blocked`, `Cancelled`, `Obsolete`
- **`priority`:** `None`, `Low`, `Medium`, `High`, `Critical`
- **Built-in `taskType` values:** `Task`, `Decision` (plus project-configured sub-types such as `Epic`, `Bug`)

---

## 5. CLI Fallback Reference (`watr`)

If the MCP server is unavailable in your environment, invoke the `watr` CLI directly. Use `--json` on read/update commands for machine-parseable output, and use `@file.md` when passing multi-line Markdown to `--body` or `watr note` to avoid shell-escaping issues.

> **Important CLI Note:** While MCP tools accept both GUIDs and handles via `idOrHandle`, the CLI mutation/detail commands (`show`, `update`, `claim`, `done`, `note`) require the task's **GUID (`<ID>`)**. Obtain the GUID from `watr list --json`, `watr search <QUERY> --json`, or the output of `watr add`.

```bash
# Check authentication and active workspace project
watr status

# Project management (Task and Decision are always included; optionally specify custom Task sub-types)
watr project list
watr project create "<NAME>" --description "<TEXT>" --task-types "Epic,Bug"
watr project set "<NAME>"

# List & semantic search (filter by --type Decision or --tag <tag>, use --json for structured parsing)
watr list --status Open --json
watr list --type Decision --tag auth --json
watr search "why did we choose SQLite over Postgres" --type Decision --top 5 --json
watr search "token expiration race condition" --top 5 --min-score 0.25 --json

# Inspect full task details (body + tags + supersedes + notes) by GUID
watr show <TASK_GUID> --json

# Create a task or decision (--handle is required; Decision defaults to Completed status)
watr add "Fix login race condition" --handle "login-race" --type Bug --priority High --tags "auth,concurrency" --body @plan.md
watr add "Use SQLite BLOBs with SIMD for vector search" --handle "adr-sqlite-vectors" --type Decision --tags "search,sqlite" --supersedes "12-old-search-adr,15-fts-adr" --body @adr.md

# Claim a task
watr claim <TASK_GUID>

# Update task fields (title, body, status, priority, handle, type, tags, supersedes, superseded-by)
watr update <TASK_GUID> --status InProgress --tags "auth,jwt" --body @updated-plan.md --json
watr update <DECISION_GUID> --status Obsolete --superseded-by "45-adr-sqlite-vectors" --json

# Add a progress or verification note (inline or via @file.md)
watr note <TASK_GUID> "Verified fix with dotnet test (all 23 tests passing)."

# Mark task as Completed
watr done <TASK_GUID>
```

### CLI Exit Codes
- `0` — Success
- `1` — General error (invalid arguments or server error)
- `2` — Authentication failure (run `watr login`)
- `3` — Not found (project or task GUID does not exist)
- `4` — Conflict (e.g., failed to claim task)


