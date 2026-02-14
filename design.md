# HotShallot Design Document

## 1. Overview
**HotShallot** is a task tracking system designed for AI coding agents and their human supervisors. It provides visibility into agent activities and facilitates coordination between multiple agents. The system consists of a centralized cloud-based web application (hosting both a Web UI and a REST API) and a cross-platform CLI tool.

### Core Goals
1.  **Visibility:** Provide a dashboard for humans to monitor agent progress.
2.  **Coordination:** Allow agents to claim tasks, leave notes, and update status to prevent work duplication.
3.  **Accessibility:** accessible via Web (humans) and CLI/API (agents & humans).

## 2. Architecture

### 2.1 High-Level Diagram
```mermaid
graph TD
    User[Human User] -->|HTTPS/Browser| WebApp
    Agent[AI Agent / CLI] -->|HTTPS/REST| API
    
    subgraph Azure_App_Service [Azure App Service (Linux/Windows)]
        WebApp[Blazor Web Frontend]
        API[ASP.NET Core Web API]
        DB[(SQLite Database)]
    end
    
    WebApp <--> DB
    API <--> DB
```

### 2.2 Technology Stack
*   **Framework:** .NET 10 (ASP.NET Core)
*   **Hosting:** Azure App Service (Single Instance)
*   **Database:** SQLite (WAL Mode enabled for concurrency)
*   **ORM:** Entity Framework Core 10
*   **Frontend:** Blazor Server (Interactive Server) with **Radzen.Blazor** components.
*   **CLI:** .NET 10 Console App, AOT Compiled, using **Spectre.Console**.
*   **Authentication:** 
    *   **Web:** FIDO2/WebAuthn (Passkeys) via `fido2-net-lib`.
    *   **API:** JWT Bearer Tokens.

## 3. Data Model

### 3.1 Schema
The database will use SQLite. IDs for Tasks will be **UUID v7** (Time-sortable).

#### **Users**
| Column | Type | Notes |
| :--- | :--- | :--- |
| `Id` | `Guid` | PK |
| `Email` | `TEXT` | Unique, Normalized |
| `DisplayName` | `TEXT` | |
| `CreatedAt` | `DATETIME` | UTC |

#### **UserPassKeys**
| Column | Type | Notes |
| :--- | :--- | :--- |
| `Id` | `Guid` | PK |
| `UserId` | `Guid` | FK -> Users.Id |
| `CredentialId` | `BLOB` | FIDO2 Credential ID |
| `PublicKey` | `BLOB` | FIDO2 Public Key |
| `SignatureCounter` | `INTEGER` | Security counter |
| `AaGuid` | `Guid` | Authenticator Attestation GUID |

#### **Projects**
| Column | Type | Notes |
| :--- | :--- | :--- |
| `Id` | `Guid` | PK |
| `Name` | `TEXT` | Unique |
| `Description` | `TEXT` | |

#### **ProjectMembers**
| Column | Type | Notes |
| :--- | :--- | :--- |
| `ProjectId` | `Guid` | FK, Composite PK |
| `UserId` | `Guid` | FK, Composite PK |
| `Role` | `TEXT` | e.g., "Admin", "Member" |

#### **Tasks**
| Column | Type | Notes |
| :--- | :--- | :--- |
| `Id` | `Guid` | PK, UUID v7 |
| `ProjectId` | `Guid` | FK -> Projects.Id |
| `Title` | `TEXT` | |
| `Body` | `TEXT` | Markdown content |
| `Status` | `INTEGER` | 0=Open, 1=InProgress, 2=Completed, 3=Blocked |
| `AssignedToUserId` | `Guid` | FK -> Users.Id (Nullable) |
| `CreatedAt` | `DATETIME` | UTC |
| `UpdatedAt` | `DATETIME` | UTC |

#### **TaskNotes**
| Column | Type | Notes |
| :--- | :--- | :--- |
| `Id` | `Guid` | PK |
| `TaskId` | `Guid` | FK -> Tasks.Id |
| `AuthorUserId` | `Guid` | FK -> Users.Id |
| `Content` | `TEXT` | Markdown content |
| `CreatedAt` | `DATETIME` | UTC |

### 3.2 Database Configuration
*   **WAL Mode:** Must be enabled (`PRAGMA journal_mode = WAL;`) to allow concurrent reads/writes between the Web UI thread and API threads.
*   **Synchronous:** `PRAGMA synchronous = NORMAL;` for better performance while maintaining safety.

## 4. Authentication & Security

### 4.1 Web Authentication (Passkeys)
*   **Library:** `fido2-net-lib`
*   **Flow:**
    1.  **Registration:** User enters email -> Server generates challenge -> Browser prompts for Passkey creation -> Server verifies & stores public key.
    2.  **Login:** User enters email -> Server generates challenge -> Browser prompts for Passkey signature -> Server verifies signature -> Issues **ASP.NET Core Session Cookie**.

### 4.2 CLI Authentication (Device Code Flow)
Since the CLI cannot handle FIDO2 challenges directly (and to support headless agents), we use a "Device Code" style flow.

1.  **Initiation (CLI):** 
    *   CLI requests a login session.
    *   Generates a random 8-digit code (e.g., `1234-5678`).
    *   Sends a request to API: `POST /api/auth/device-login-init` with the code (hashed or raw, depending on security model - raw is fine if displayed to user).
2.  **User Interaction:**
    *   CLI displays: "Please go to `https://hotshallot.azurewebsites.net/device` and enter code: `1234-5678`".
    *   CLI opens the browser automatically if possible.
3.  **Polling (CLI):**
    *   CLI polls `GET /api/auth/device-login-status?code=...` every 5 seconds (timeout 5 mins).
4.  **Authorization (Web):**
    *   User logs into Web App (via Passkey).
    *   Navigates to `/device`.
    *   Enters the 8-digit code.
    *   Web App finds the pending memory-cache request and associates it with the current logged-in user.
    *   Web App marks the request as "Approved" and generates a long-lived **JWT Refresh Token** and short-lived **Access Token**.
5.  **Completion (CLI):**
    *   Next poll from CLI receives the Tokens.
    *   CLI securely stores tokens (Windows: DPAPI/CredManager).

### 4.3 JWT Policy
*   **Issuer/Audience:** `HotShallot`
*   **Claims:** `sub` (UserId), `email`, `project_roles`.
*   **Expiration:** Access Token (1 hour), Refresh Token (30 days).

## 5. API Design (REST)

All API endpoints are prefixed with `/api/v1`.

### Endpoints
*   `GET /projects` - List visible projects.
*   `GET /projects/{id}/tasks` - List tasks (supports filtering by status).
*   `POST /projects/{id}/tasks` - Create a new task.
*   `GET /tasks/{id}` - Get task details.
*   `PUT /tasks/{id}` - Update task (title, body, status).
*   `POST /tasks/{id}/claim` - Assign task to current user (Agent).
*   `POST /tasks/{id}/release` - Unassign task.
*   `POST /tasks/{id}/notes` - Add a comment/note.
*   `GET /user/me` - Current user info.

## 6. Web Frontend (Blazor)

### 6.1 Layout
*   **MainLayout:** Standard top navigation bar (Logo, Project Selector, User Profile/Logout).
*   **Theme:** Dark mode by default (coding agent aesthetic).

### 6.2 Pages
*   **Login:** Simple centered card with "Login with Passkey" button.
*   **Dashboard:**
    *   **Component:** `RadzenDataGrid<Task>`
    *   **Columns:** ID (truncated), Title, Status (Badge), Assigned To (Avatar/Name), Updated.
    *   **Features:**
        *   Filtering (Status: Open, InProgress, Done).
        *   Sorting (by Date, Priority).
        *   Row Selection opens a **Task Detail Dialog**.
*   **Task Detail (Dialog/Page):**
    *   Title (Editable).
    *   Status Dropdown.
    *   Body: Markdown Viewer (rendered) + Editor tab.
    *   Notes Section: List of notes with timestamps + "Add Note" text area.
*   **Device Login:** Simple form to enter the 8-digit CLI code.

## 7. CLI Tool (`shallot`)

### 7.1 Implementation
*   **Library:** `Spectre.Console` for rich terminal UI (tables, spinners, colored output).
*   **Output:** Human-readable by default. Flag `--json` for machine-parsing by other agents.
*   **AOT:** Published as Native AOT for fast startup and single-file distribution.

### 7.2 Commands
*   `shallot login`: Initiates device code flow.
*   `shallot logout`: Clears local credentials.
*   `shallot list --project <name> --status <filter>`: key value table of tasks.
*   `shallot show <id>`: Renders task details (renders Markdown to ANSI if possible).
*   `shallot add "Title" --body "file.md" --project <name>`: Creates a task.
*   `shallot claim <id>`: Assigns task to self.
*   `shallot done <id>`: Marks task as completed.
*   `shallot note <id> "Message"`: Appends a note.

## 8. Infrastructure & Deployment

### 8.1 Azure App Service
*   **Plan:** B1 or higher (for SQLite performance) or a Linux Container plan.
*   **Configuration:**
    *   `WEBSITE_RUN_FROM_PACKAGE = 1`
    *   Data Directory: `/home/data` (Linux) or `C:\home\data` (Windows) mapped to a persistent volume if using containers, or just `App_Data` for standard code deploy.

### 8.2 Build System
*   **CI/CD:** GitHub Actions or Azure DevOps.
*   **Steps:**
    1.  Restore & Build Solution.
    2.  Run Unit Tests.
    3.  Publish Web App -> Artifact.
    4.  Publish CLI (win-x64, linux-x64, osx-x64) -> Artifacts.

## 9. Brand Identity

### Logo Description
**"The Hot Shallot"**
A cartoon vector-style shallot (small onion) with a determined, slightly manic facial expression. The top of the shallot (the green shoots) is engulfed in stylized orange and red flames. The shallot is wearing a headset and typing on a keyboard that is melting slightly.

*Caption:* "Cooking up code."

## 10. Implementation Roadmap

1.  **Phase 1: Core Skeleton**
    *   Setup Solution (Web, API, Shared, CLI).
    *   Configure EF Core + SQLite.
    *   Implement Basic User/Task Models.
2.  **Phase 2: Authentication**
    *   Implement FIDO2 Web Login.
    *   Implement Device Code Token Exchange.
3.  **Phase 3: API & CLI**
    *   Build Task CRUD API.
    *   Build `shallot` CLI with Spectre.Console.
4.  **Phase 4: Web UI**
    *   Implement Radzen DataGrid.
    *   Task Detail Views.
