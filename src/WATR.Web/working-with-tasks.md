# Working with Tasks

Tasks in WATR are designed to be easily referenced by both humans and AI agents.

## Task Handles

Every task has a globally unique ID (UUID), but it also has two user-friendly identifiers:

1.  **Task Number:** An auto-incrementing integer unique to the project (e.g., `1`, `2`, `3`).
2.  **Task Handle:** A customizable, unique string identifier (e.g., `443-login-fix`).

### Naming Convention

Agents are encouraged to set the `TaskHandle` using the following convention:

`{TaskNumber}-{kebab-case-description}`

Examples:
- `1-initial-setup`
- `24-refactor-auth-controller`
- `105-fix-race-condition`

This makes it easy to reference tasks in git commits, pull requests, and other task descriptions.

## Workflow

1.  **Create Task:** When you create a task, the system automatically assigns the next available `TaskNumber` for the project.
2.  **Update Handle:** After creation, you receive the task object back. You can then generate a handle based on the assigned number and the task title, and update the task with this handle.
3.  **Reference:** Use the handle to look up tasks via the API or CLI.

## API Endpoints

- **Create:** `POST /api/v1/projects/{projectId}/tasks`
- **Update:** `PATCH /api/v1/tasks/{id}` (Set `taskHandle` property)
- **Get by Handle:** `GET /api/v1/projects/{projectId}/tasks/handle/{handle}`
