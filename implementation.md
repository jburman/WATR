# HotShallot — Implementation Plan

This document is a step-by-step implementation guide for building HotShallot. It follows the phases described in `design.md` and breaks each phase into granular, checkable tasks. Complete them in order — each step builds on the previous one.

> **Prerequisites:** .NET 10 SDK installed, Visual Studio 2022+ or VS Code with C# Dev Kit, Git, an Azure account (for deployment later).

---

## Phase 1: Solution Setup & Data Model

The goal of this phase is to create the .NET solution structure, configure the database, and define all entity models. At the end, you should be able to run `dotnet build` successfully and generate an initial EF Core migration.

### 1.1 Create the Solution and Projects

- [ ] Create a new empty directory structure and initialize git (if not already done).
- [ ] Create the solution file:
  ```
  dotnet new sln -n HotShallot
  ```
- [ ] Create the **Web/API project** (this is the main host — it serves both the Blazor UI and the REST API):
  ```
  dotnet new blazor -n HotShallot.Web -o src/HotShallot.Web --interactivity Server
  ```
- [ ] Create the **Shared library** (contains entity models, enums, DTOs shared between Web and CLI):
  ```
  dotnet new classlib -n HotShallot.Shared -o src/HotShallot.Shared
  ```
- [ ] Create the **CLI project**:
  ```
  dotnet new console -n HotShallot.Cli -o src/HotShallot.Cli
  ```
- [ ] Add all projects to the solution:
  ```
  dotnet sln add src/HotShallot.Web
  dotnet sln add src/HotShallot.Shared
  dotnet sln add src/HotShallot.Cli
  ```
- [ ] Add project references:
  - `HotShallot.Web` → references `HotShallot.Shared`
  - `HotShallot.Cli` → references `HotShallot.Shared`
  ```
  dotnet add src/HotShallot.Web reference src/HotShallot.Shared
  dotnet add src/HotShallot.Cli reference src/HotShallot.Shared
  ```
- [ ] Verify the solution builds: `dotnet build`

### 1.2 Install NuGet Packages

- [ ] **HotShallot.Web** — install these packages:
  - `Microsoft.EntityFrameworkCore.Sqlite` (EF Core SQLite provider)
  - `Microsoft.EntityFrameworkCore.Design` (for migrations tooling)
  - `Radzen.Blazor` (UI component library)
  - `Fido2` (FIDO2/WebAuthn — this is the `fido2-net-lib` package)
  - `Microsoft.AspNetCore.Authentication.JwtBearer` (JWT auth for the API)
  - `Microsoft.Extensions.Diagnostics.HealthChecks` (health check middleware)
  ```
  cd src/HotShallot.Web
  dotnet add package Microsoft.EntityFrameworkCore.Sqlite
  dotnet add package Microsoft.EntityFrameworkCore.Design
  dotnet add package Radzen.Blazor
  dotnet add package Fido2
  dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
  ```
- [ ] **HotShallot.Cli** — install these packages:
  - `Spectre.Console` (rich terminal UI)
  - `Spectre.Console.Cli` (command-line argument parsing)
  - `System.IdentityModel.Tokens.Jwt` (for reading/validating JWT tokens locally)
  ```
  cd src/HotShallot.Cli
  dotnet add package Spectre.Console
  dotnet add package Spectre.Console.Cli
  dotnet add package System.IdentityModel.Tokens.Jwt
  ```
- [ ] Verify the solution still builds: `dotnet build`

### 1.3 Define Enums (in HotShallot.Shared)

- [ ] Create `Enums/TaskStatus.cs`:
  ```csharp
  namespace HotShallot.Shared.Enums;

  public enum TaskItemStatus
  {
      Open = 0,
      InProgress = 1,
      Completed = 2,
      Blocked = 3,
      Cancelled = 4
  }
  ```
  > **Note:** Named `TaskItemStatus` to avoid conflict with `System.Threading.Tasks.TaskStatus`.

- [ ] Create `Enums/TaskPriority.cs`:
  ```csharp
  namespace HotShallot.Shared.Enums;

  public enum TaskPriority
  {
      None = 0,
      Low = 1,
      Medium = 2,
      High = 3,
      Critical = 4
  }
  ```

- [ ] Create `Enums/ProjectRole.cs`:
  ```csharp
  namespace HotShallot.Shared.Enums;

  public enum ProjectRole
  {
      Member,
      Admin
  }
  ```

### 1.4 Define Entity Models (in HotShallot.Shared)

Create each entity class in a `Models/` folder inside `HotShallot.Shared`. Each class maps directly to a database table described in the design document.

- [ ] Create `Models/User.cs` with properties: `Id` (Guid), `Email` (string), `DisplayName` (string), `CreatedAt` (DateTime). Add navigation properties: `ICollection<UserPassKey> PassKeys`, `ICollection<ProjectMember> ProjectMemberships`.

- [ ] Create `Models/UserPassKey.cs` with properties: `Id` (Guid), `UserId` (Guid), `CredentialId` (byte[]), `PublicKey` (byte[]), `SignatureCounter` (uint), `AaGuid` (Guid). Add navigation property: `User User`.

- [ ] Create `Models/Project.cs` with properties: `Id` (Guid), `Name` (string), `Description` (string), `CreatedByUserId` (Guid), `CreatedAt` (DateTime), `UpdatedAt` (DateTime). Add navigation properties: `User CreatedBy`, `ICollection<ProjectMember> Members`, `ICollection<TaskItem> Tasks`.

- [ ] Create `Models/ProjectMember.cs` with properties: `ProjectId` (Guid), `UserId` (Guid), `Role` (ProjectRole enum). Add navigation properties: `Project Project`, `User User`.

- [ ] Create `Models/TaskItem.cs` with properties: `Id` (Guid), `ProjectId` (Guid), `Title` (string), `Body` (string), `Status` (TaskItemStatus), `Priority` (TaskPriority), `AssignedToUserId` (Guid?), `CreatedByUserId` (Guid), `CreatedAt` (DateTime), `UpdatedAt` (DateTime), `ClosedAt` (DateTime?). Add navigation properties: `Project Project`, `User? AssignedTo`, `User CreatedBy`, `ICollection<TaskNote> Notes`.
  > **Note:** Named `TaskItem` to avoid conflict with `System.Threading.Tasks.Task`.

- [ ] Create `Models/TaskNote.cs` with properties: `Id` (Guid), `TaskId` (Guid), `AuthorUserId` (Guid), `Content` (string), `CreatedAt` (DateTime), `UpdatedAt` (DateTime?). Add navigation properties: `TaskItem Task`, `User Author`.

### 1.5 Create the DbContext (in HotShallot.Web)

- [ ] Create `Data/HotShallotDbContext.cs` in the Web project.
- [ ] Define `DbSet<>` properties for all six entities: `Users`, `UserPassKeys`, `Projects`, `ProjectMembers`, `TaskItems`, `TaskNotes`.
- [ ] Override `OnModelCreating` to configure:
  - **User:** `Email` has a unique index, `CreatedAt` defaults to `DateTime.UtcNow`.
  - **UserPassKey:** FK to `User` with cascade delete.
  - **Project:** `Name` has a unique index. FK to `User` (`CreatedBy`) with restrict delete.
  - **ProjectMember:** Composite PK on `(ProjectId, UserId)`. `Role` stored as string (use `.HasConversion<string>()`).
  - **TaskItem:** FK to `Project` (cascade delete), FK to `AssignedTo` (set null on delete), FK to `CreatedBy` (restrict delete). `Status` and `Priority` stored as integers.
  - **TaskNote:** FK to `TaskItem` (cascade delete), FK to `User` (`Author`) with restrict delete.
- [ ] Configure SQLite connection string in `appsettings.json`:
  ```json
  {
    "ConnectionStrings": {
      "DefaultConnection": "Data Source=App_Data/hotshallot.db"
    }
  }
  ```
- [ ] Register `HotShallotDbContext` in `Program.cs`:
  ```csharp
  builder.Services.AddDbContext<HotShallotDbContext>(options =>
      options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
  ```
- [ ] Create the `App_Data` directory in the Web project (add a `.gitkeep` file so git tracks the empty folder).

### 1.6 Configure SQLite Pragmas

- [ ] After building the `WebApplication` (after `builder.Build()`), add startup code to set SQLite pragmas:
  ```csharp
  using (var scope = app.Services.CreateScope())
  {
      var db = scope.ServiceProvider.GetRequiredService<HotShallotDbContext>();
      db.Database.ExecuteSqlRaw("PRAGMA journal_mode = WAL;");
      db.Database.ExecuteSqlRaw("PRAGMA synchronous = NORMAL;");
  }
  ```

### 1.7 Create Initial EF Core Migration

- [ ] Install the EF Core CLI tool (if not already installed):
  ```
  dotnet tool install --global dotnet-ef
  ```
- [ ] Generate the initial migration:
  ```
  cd src/HotShallot.Web
  dotnet ef migrations add InitialCreate
  ```
- [ ] Verify the migration files were created in `Migrations/` folder.
- [ ] Add auto-migration on startup (in `Program.cs`, just after the pragma configuration):
  ```csharp
  db.Database.Migrate();
  ```
- [ ] Run the Web project and verify the SQLite database file is created in `App_Data/`:
  ```
  dotnet run --project src/HotShallot.Web
  ```
- [ ] Verify the database has all expected tables (use a SQLite browser tool or the `sqlite3` CLI).

### 1.8 Build Verification

- [ ] Ensure `dotnet build` succeeds with no warnings for the entire solution.
- [ ] Ensure `dotnet run --project src/HotShallot.Web` starts without errors and the database is created.

---

## Phase 2: Authentication

This phase implements two authentication flows: Passkey login for the web UI and a device-code flow for the CLI. This is the most complex phase — take your time and test thoroughly.

### 2.1 Configure Authentication Middleware

- [ ] In `Program.cs`, add authentication services **before** `builder.Build()`:
  ```csharp
  builder.Services.AddAuthentication(options =>
  {
      options.DefaultScheme = "Cookies";
      options.DefaultChallengeScheme = "Cookies";
  })
  .AddCookie("Cookies", options =>
  {
      options.LoginPath = "/login";
      options.Cookie.HttpOnly = true;
      options.Cookie.SameSite = SameSiteMode.Strict;
  })
  .AddJwtBearer("Bearer", options =>
  {
      options.TokenValidationParameters = new TokenValidationParameters
      {
          ValidateIssuer = true,
          ValidateAudience = true,
          ValidIssuer = "HotShallot",
          ValidAudience = "HotShallot",
          IssuerSigningKey = new SymmetricSecurityKey(
              Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SigningKey"]!))
      };
  });
  ```
- [ ] Add the JWT signing key to `appsettings.Development.json` (for local dev only — never commit production keys):
  ```json
  {
    "Jwt": {
      "SigningKey": "DEVELOPMENT-ONLY-KEY-CHANGE-IN-PRODUCTION-minimum-32-chars!"
    }
  }
  ```
- [ ] Add `app.UseAuthentication()` and `app.UseAuthorization()` in the middleware pipeline (after routing, before endpoint mapping).
- [ ] Add an authorization policy that requires authenticated users for all Blazor pages (but not the login page or health endpoint).

### 2.2 Implement FIDO2 Passkey Registration

- [ ] Register `Fido2` services in `Program.cs`:
  ```csharp
  builder.Services.AddFido2(options =>
  {
      options.ServerDomain = builder.Configuration["Fido2:ServerDomain"];
      options.ServerName = "HotShallot";
      options.Origins = new HashSet<string> { builder.Configuration["Fido2:Origin"]! };
  });
  ```
- [ ] Add FIDO2 config to `appsettings.Development.json`:
  ```json
  {
    "Fido2": {
      "ServerDomain": "localhost",
      "Origin": "https://localhost:5001"
    }
  }
  ```
- [ ] Create `Services/PassKeyService.cs` with methods:
  - `MakeCredentialOptionsAsync(User user)` — generates a FIDO2 registration challenge.
  - `MakeCredentialAsync(User user, AuthenticatorAttestationRawResponse response, CredentialCreateOptions originalOptions)` — verifies the browser response and stores the new `UserPassKey` record in the database.
- [ ] Create a registration API controller or Blazor page endpoint:
  - `POST /api/auth/passkey/register-options` — takes email, returns FIDO2 challenge JSON.
  - `POST /api/auth/passkey/register` — takes the browser attestation response, creates the `User` + `UserPassKey` records, returns success.
- [ ] **Test:** Use a browser with WebAuthn support (Chrome/Edge). Call the register-options endpoint, feed the result to `navigator.credentials.create()`, and send the result back to the register endpoint. Verify the user and passkey appear in the database.

### 2.3 Implement FIDO2 Passkey Login

- [ ] Add methods to `PassKeyService`:
  - `MakeAssertionOptionsAsync(string email)` — looks up the user's credential IDs and generates an assertion challenge.
  - `MakeAssertionAsync(AuthenticatorAssertionRawResponse response, AssertionOptions originalOptions)` — verifies the signature, updates the signature counter, returns the `User`.
- [ ] Create login API endpoints:
  - `POST /api/auth/passkey/login-options` — takes email, returns assertion challenge.
  - `POST /api/auth/passkey/login` — takes the browser assertion response, verifies it, signs the user in with a cookie (`HttpContext.SignInAsync`), returns success.
- [ ] Create the **Login Blazor page** (`/login`):
  - Simple centered card layout.
  - Email input field.
  - "Login with Passkey" button.
  - On click: call login-options endpoint via JS interop → call `navigator.credentials.get()` → send result to login endpoint.
  - On first visit (no account): show a "Register" flow instead.
  - **Tip:** You'll need a small JavaScript file for the WebAuthn browser API calls. Create `wwwroot/js/webauthn.js` with helper functions for `navigator.credentials.create()` and `navigator.credentials.get()`. Call them from Blazor via `IJSRuntime`.
- [ ] Add a "Logout" link in the navigation that calls `HttpContext.SignOutAsync` and redirects to `/login`.
- [ ] **Test:** Register a new user, log out, log back in with the passkey. Verify the session cookie is issued and protected pages are accessible.

### 2.4 Implement Device Code Flow (for CLI)

This allows the CLI to obtain a JWT by having the user approve a code in the web browser.

- [ ] Create `Services/DeviceCodeService.cs`:
  - Use `IMemoryCache` (or a `ConcurrentDictionary<string, DeviceCodeRequest>`) to store pending requests.
  - `DeviceCodeRequest` model: `Code` (string), `CreatedAt` (DateTime), `Status` (Pending/Approved/Denied), `UserId` (Guid? — set when approved), `AccessToken` (string? — set when approved), `RefreshToken` (string? — set when approved), `FailedAttempts` (int).
  - Method: `InitiateAsync(string code)` — stores a new pending request, expires after 5 minutes.
  - Method: `CheckStatusAsync(string code)` — returns the current status. If approved, returns the tokens.
  - Method: `ApproveAsync(string code, Guid userId)` — validates the code exists and is pending, generates JWT tokens, marks as approved. Returns false if code not found or expired. Increments `FailedAttempts` on wrong codes; rejects after 5 attempts.
- [ ] Create `Services/JwtTokenService.cs`:
  - Method: `GenerateAccessToken(User user)` — creates a JWT with claims (`sub`, `email`, `project_roles`), 1-hour expiry, signed with the configured key.
  - Method: `GenerateRefreshToken()` — creates a cryptographically random refresh token string. Store it in the database or in-memory for the MVP (in-memory is acceptable for now).
  - Method: `RefreshAccessToken(string refreshToken)` — validates the refresh token and issues a new access token.
- [ ] Create API endpoints for the device code flow:
  - `POST /api/auth/device-login-init` — body: `{ "code": "1234-5678" }`. Calls `DeviceCodeService.InitiateAsync`. Returns 200 OK. **This endpoint is unauthenticated** (the CLI is not logged in yet).
  - `GET /api/auth/device-login-status?code=...` — calls `DeviceCodeService.CheckStatusAsync`. Returns `{ "status": "pending" }` or `{ "status": "approved", "accessToken": "...", "refreshToken": "..." }`. **This endpoint is unauthenticated.**
  - `POST /api/auth/revoke` — body: `{ "refreshToken": "..." }`. Invalidates the token. **This endpoint requires JWT authentication.**
- [ ] Create the **Device Login Blazor page** (`/device`):
  - Requires the user to be logged in (redirect to `/login` if not).
  - Shows a simple form: "Enter the 8-digit code shown in your terminal."
  - On submit: calls `DeviceCodeService.ApproveAsync` with the entered code and the current user's ID.
  - Shows success ("Device authorized!") or error ("Invalid or expired code").
- [ ] **Test the full flow manually:**
  1. Use a tool like `curl` or Postman to POST to `/api/auth/device-login-init` with a code.
  2. Log into the web app, go to `/device`, enter the code.
  3. Use `curl` to poll `/api/auth/device-login-status` and verify you get back tokens.
  4. Use the access token in an `Authorization: Bearer <token>` header on a protected endpoint.

### 2.5 Protect API Endpoints

- [ ] Create a base API controller (`Controllers/ApiControllerBase.cs`) that applies `[Authorize(AuthenticationSchemes = "Bearer")]` and `[Route("api/v1/[controller]")]`.
- [ ] Verify that unauthenticated requests to API endpoints return `401 Unauthorized`.
- [ ] Verify that requests with a valid JWT can access protected endpoints.

### 2.6 CORS Configuration

- [ ] Add CORS policy in `Program.cs`:
  ```csharp
  builder.Services.AddCors(options =>
  {
      options.AddPolicy("ApiCors", policy =>
      {
          policy.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
      });
  });
  ```
- [ ] Apply the CORS policy to API routes only (not Blazor routes):
  ```csharp
  app.UseCors(); // default restrictive
  // On API controllers, use [EnableCors("ApiCors")]
  ```

### 2.7 Build Verification

- [ ] Full flow test: Register user → Login → Device code → JWT obtained → Access protected endpoint.
- [ ] Verify `dotnet build` succeeds with no errors.

---

## Phase 3: REST API

This phase builds out the complete task management API. The API is used by both the Blazor frontend and the CLI.

### 3.1 Define DTOs (in HotShallot.Shared)

DTOs (Data Transfer Objects) are the JSON shapes sent to and from the API. They decouple the database entities from the API contract.

- [ ] Create `DTOs/PaginatedResponse.cs` — generic wrapper:
  ```csharp
  public class PaginatedResponse<T>
  {
      public List<T> Items { get; set; } = [];
      public int Page { get; set; }
      public int PageSize { get; set; }
      public int TotalCount { get; set; }
  }
  ```
- [ ] Create `DTOs/ErrorResponse.cs`:
  ```csharp
  public class ErrorResponse
  {
      public string Error { get; set; } = "";
      public string Message { get; set; } = "";
      public object? Details { get; set; }
  }
  ```
- [ ] Create DTOs for each resource:
  - `ProjectDto` — Id, Name, Description, CreatedByUserId, CreatedAt, UpdatedAt.
  - `CreateProjectRequest` — Name, Description.
  - `TaskItemDto` — all task fields + `AssignedToDisplayName`, `CreatedByDisplayName`, list of `TaskNoteDto`.
  - `TaskItemSummaryDto` — lighter version for list views: Id, Title, Status, Priority, AssignedToDisplayName, UpdatedAt.
  - `CreateTaskRequest` — Title, Body, Priority (optional, defaults to None).
  - `UpdateTaskRequest` — Title, Body, Status, Priority (all required for PUT).
  - `PatchTaskRequest` — Title?, Body?, Status?, Priority? (all optional for PATCH).
  - `TaskNoteDto` — Id, AuthorDisplayName, Content, CreatedAt, UpdatedAt.
  - `CreateTaskNoteRequest` — Content.
  - `UserDto` — Id, Email, DisplayName.

### 3.2 Implement Projects Controller

- [ ] Create `Controllers/ProjectsController.cs` extending `ApiControllerBase`.
- [ ] `GET /api/v1/projects` — returns `PaginatedResponse<ProjectDto>`. Only returns projects where the current user is a member. Accepts `?page=1&pageSize=20`.
- [ ] `POST /api/v1/projects` — creates a new project. Automatically adds the current user as an `Admin` member. Returns `201 Created` with the `ProjectDto`.
- [ ] **Test:** Create a project via Postman/curl with a valid JWT, then list projects and verify it appears.

### 3.3 Implement Tasks Controller

- [ ] Create `Controllers/TasksController.cs`.
- [ ] `GET /api/v1/projects/{projectId}/tasks` — returns `PaginatedResponse<TaskItemSummaryDto>`. Verify the current user is a member of the project (return `403 Forbidden` if not). Support query filters:
  - `?status=Open` (filter by status)
  - `?priority=High` (filter by priority)
  - `?assignedTo=me` (filter to current user's tasks)
  - `?page=1&pageSize=20` (pagination)
- [ ] `POST /api/v1/projects/{projectId}/tasks` — creates a new task. Sets `CreatedByUserId` to the current user. Generates a UUID v7 for the `Id`. Returns `201 Created`.
  > **UUID v7 generation tip:** Use `Guid.CreateVersion7()` available in .NET 9+.
- [ ] `GET /api/v1/tasks/{id}` — returns full `TaskItemDto` including inline notes. Return `404` if not found, `403` if user is not a project member.
- [ ] `PUT /api/v1/tasks/{id}` — full update. Requires all fields. Updates `UpdatedAt`. If status changes to `Completed` or `Cancelled`, set `ClosedAt`. If status changes away from those, clear `ClosedAt`.
- [ ] `PATCH /api/v1/tasks/{id}` — partial update. Only updates fields present in the request body. Same `ClosedAt` logic as PUT.
- [ ] `POST /api/v1/tasks/{id}/claim` — sets `AssignedToUserId` to the current user. Returns `409 Conflict` if already assigned to someone else.
- [ ] `POST /api/v1/tasks/{id}/release` — clears `AssignedToUserId`. Only the assigned user (or a project admin) can release.
- [ ] **Test each endpoint** with valid and invalid inputs. Verify error responses match the standard `ErrorResponse` format.

### 3.4 Implement Task Notes Endpoints

- [ ] `GET /api/v1/tasks/{taskId}/notes` — returns `PaginatedResponse<TaskNoteDto>`, ordered by `CreatedAt` ascending.
- [ ] `POST /api/v1/tasks/{taskId}/notes` — creates a new note. Sets `AuthorUserId` to the current user. Returns `201 Created`.
- [ ] **Test:** Add a note to a task, retrieve the task and verify the note appears.

### 3.5 Implement User Endpoint

- [ ] `GET /api/v1/user/me` — returns `UserDto` for the currently authenticated user.
- [ ] **Test:** Call with a valid JWT and verify the response.

### 3.6 Global Error Handling

- [ ] Create an exception handling middleware or use `app.UseExceptionHandler` to catch unhandled exceptions and return a consistent `ErrorResponse` JSON with a `500` status code.
- [ ] Create a `NotFoundException` and `ForbiddenException` custom exception classes. Catch them in the middleware and return `404` / `403` responses in `ErrorResponse` format.
- [ ] **Test:** Trigger a 404 (invalid task ID) and verify the response shape matches:
  ```json
  { "error": "not_found", "message": "Task with ID '...' was not found.", "details": null }
  ```

### 3.7 Health Check Endpoint

- [ ] Register health checks in `Program.cs`:
  ```csharp
  builder.Services.AddHealthChecks()
      .AddDbContextCheck<HotShallotDbContext>();
  ```
- [ ] Map the health endpoint (before auth middleware so it's unauthenticated):
  ```csharp
  app.MapHealthChecks("/health");
  ```
- [ ] **Test:** `GET /health` returns `200 OK` with status `Healthy`.

### 3.8 Build Verification

- [ ] All API endpoints return correct responses for valid inputs.
- [ ] All API endpoints return correct error responses for invalid inputs (400, 401, 403, 404, 409).
- [ ] `dotnet build` succeeds with no errors.

---

## Phase 4: Real-Time Updates (SignalR)

This phase adds live push updates so the web dashboard refreshes automatically when tasks change via the API.

### 4.1 Create the SignalR Hub

- [ ] Create `Hubs/TaskHub.cs`:
  ```csharp
  using Microsoft.AspNetCore.SignalR;

  namespace HotShallot.Web.Hubs;

  public class TaskHub : Hub
  {
      // Clients join a group per project for scoped notifications
      public async Task JoinProject(string projectId)
      {
          await Groups.AddToGroupAsync(Context.ConnectionId, projectId);
      }

      public async Task LeaveProject(string projectId)
      {
          await Groups.RemoveFromGroupAsync(Context.ConnectionId, projectId);
      }
  }
  ```
- [ ] Register SignalR in `Program.cs`:
  ```csharp
  builder.Services.AddSignalR();
  ```
- [ ] Map the hub endpoint:
  ```csharp
  app.MapHub<TaskHub>("/hubs/tasks");
  ```

### 4.2 Broadcast Events from API Controllers

- [ ] Inject `IHubContext<TaskHub>` into `TasksController`.
- [ ] After each mutation (create, update, patch, claim, release, add note), broadcast an event to the project's group:
  ```csharp
  await _hubContext.Clients.Group(task.ProjectId.ToString())
      .SendAsync("TaskUpdated", new { taskId = task.Id, action = "updated" });
  ```
- [ ] Define event types: `TaskCreated`, `TaskUpdated`, `TaskClaimed`, `TaskReleased`, `NoteAdded`.

### 4.3 Build Verification

- [ ] Start the web app. Open the browser console and connect to the SignalR hub at `/hubs/tasks`. Join a project group.
- [ ] Use curl/Postman to update a task via the API.
- [ ] Verify the SignalR event arrives in the browser console.

---

## Phase 5: Web UI (Blazor)

This phase builds the Blazor Server frontend with Radzen components. The dashboard will auto-refresh via SignalR when tasks change.

### 5.1 Configure Radzen

- [ ] Add Radzen services in `Program.cs`:
  ```csharp
  builder.Services.AddRadzenComponents();
  ```
- [ ] Add Radzen CSS and JS to `App.razor` (or `_Host.cshtml`):
  ```html
  <link rel="stylesheet" href="_content/Radzen.Blazor/css/material-dark.css">
  <script src="_content/Radzen.Blazor/Radzen.Blazor.js"></script>
  ```
- [ ] Add `@using Radzen` and `@using Radzen.Blazor` to `_Imports.razor`.
- [ ] Add `<RadzenComponents />` in `MainLayout.razor` (required for dialogs, notifications, etc.).

### 5.2 Create the Main Layout

- [ ] Edit `MainLayout.razor`:
  - Top navigation bar with: Logo/app name ("HotShallot 🌶️‍🔥"), a project selector dropdown, user display name, and a logout button.
  - Use `RadzenHeader`, `RadzenBody`, `RadzenLayout` components for structure.
- [ ] Apply dark theme by default (the `material-dark.css` from Step 5.1 handles this).
- [ ] Implement the project selector dropdown:
  - On page load, fetch the user's projects from the database (via a service, not direct API call — Blazor Server has direct access).
  - Store the selected project in a scoped service (e.g., `ProjectContextService`) so all pages know which project is active.
  - When the user switches projects, refresh the dashboard data.

### 5.3 Create a Data Access Service Layer

Since Blazor Server runs on the server, it can access the `HotShallotDbContext` directly — no need to call the REST API from Blazor.

- [ ] Create `Services/TaskService.cs` with methods:
  - `GetTasksAsync(Guid projectId, TaskItemStatus? statusFilter, bool? myTasksOnly, Guid currentUserId, int page, int pageSize)` — queries tasks with filtering, sorting, pagination. Returns `PaginatedResponse<TaskItemSummaryDto>`.
  - `GetTaskAsync(Guid taskId)` — returns full `TaskItemDto` with notes.
  - `CreateTaskAsync(Guid projectId, CreateTaskRequest request, Guid userId)` — creates and returns the new task.
  - `UpdateTaskAsync(Guid taskId, UpdateTaskRequest request)` — full update.
  - `ClaimTaskAsync(Guid taskId, Guid userId)` — claim.
  - `ReleaseTaskAsync(Guid taskId)` — release.
  - `AddNoteAsync(Guid taskId, string content, Guid userId)` — add note.
- [ ] Create `Services/ProjectService.cs` with methods:
  - `GetUserProjectsAsync(Guid userId)` — returns list of projects the user is a member of.
  - `CreateProjectAsync(CreateProjectRequest request, Guid userId)`.
- [ ] Register these services as **scoped** in `Program.cs`.
  > **Important:** EF Core's `DbContext` is scoped, so services that use it must also be scoped.

### 5.4 Build the Dashboard Page

- [ ] Create `Pages/Dashboard.razor`:
  - Inject `TaskService`, `ProjectContextService`, `NavigationManager`.
  - Use `RadzenDataGrid<TaskItemSummaryDto>` with columns:
    - **ID** — truncated to first 8 chars of the GUID.
    - **Title** — full text.
    - **Status** — `RadzenBadge` with color coding (Open=blue, InProgress=yellow, Completed=green, Blocked=red, Cancelled=gray).
    - **Priority** — `RadzenBadge` with color coding (None=default, Low=blue, Medium=yellow, High=orange, Critical=red).
    - **Assigned To** — display name or "Unassigned."
    - **Updated** — relative time (e.g., "5 min ago").
  - Enable server-side sorting and pagination (use `LoadData` event on the DataGrid).
  - Add filter controls above the grid:
    - Status dropdown filter (multi-select or single select).
    - "My Tasks" toggle button.
  - On row click, open the Task Detail dialog.

### 5.5 Build the Task Detail Dialog

- [ ] Create `Components/TaskDetailDialog.razor`:
  - Inject `TaskService` and `NotificationService`.
  - **Title:** Editable text field. Save on blur or Enter.
  - **Status:** `RadzenDropDown` with all `TaskItemStatus` values.
  - **Priority:** `RadzenDropDown` with all `TaskPriority` values.
  - **Body:** Two tabs:
    - "View" tab: render the Markdown as HTML (use a Markdown rendering library like `Markdig`).
    - "Edit" tab: `RadzenTextArea` with the raw markdown.
  - **Notes section:**
    - List of existing notes, each showing author name, relative timestamp, and rendered markdown content.
    - "Add Note" text area with a submit button at the bottom.
  - **Save button** at the top/bottom that commits all changes.
  - Use `RadzenDialog` to show this as a modal.

### 5.6 Wire Up SignalR for Live Updates

- [ ] In `Dashboard.razor`, connect to the SignalR hub on page initialization:
  ```csharp
  @inject NavigationManager Navigation
  @implements IAsyncDisposable

  private HubConnection? _hubConnection;

  protected override async Task OnInitializedAsync()
  {
      _hubConnection = new HubConnectionBuilder()
          .WithUrl(Navigation.ToAbsoluteUri("/hubs/tasks"))
          .Build();

      _hubConnection.On<object>("TaskUpdated", async (data) =>
      {
          await InvokeAsync(RefreshGrid);
      });

      await _hubConnection.StartAsync();
      await _hubConnection.InvokeAsync("JoinProject", CurrentProjectId.ToString());
  }
  ```
- [ ] When a `TaskUpdated` event arrives, re-query the grid data and call `StateHasChanged()`.
- [ ] Add `RadzenNotification` toast messages when events arrive (e.g., "Task 'Fix login bug' was claimed by Agent-1").
- [ ] Implement `IAsyncDisposable` to clean up the hub connection.

### 5.7 Build the Login Page

- [ ] Create `Pages/Login.razor` (this should already be partially done from Phase 2).
- [ ] Ensure it's excluded from the authorization requirement (`@attribute [AllowAnonymous]`).
- [ ] Style it as a centered card with the HotShallot logo, email input, and "Login with Passkey" / "Register" buttons.

### 5.8 Build the Device Login Page

- [ ] Create `Pages/DeviceLogin.razor` at route `/device`:
  - Requires authentication.
  - Simple form with a single text input for the 8-digit code.
  - On submit, calls `DeviceCodeService.ApproveAsync`.
  - Shows success or error feedback using `RadzenAlert` or `RadzenNotification`.

### 5.9 Add Keyboard Shortcuts

- [ ] Add a small JavaScript file (`wwwroot/js/shortcuts.js`) that listens for keydown events:
  - `n` → trigger "New Task" action (call a C# method via JS interop).
  - `Escape` → close the active dialog.
- [ ] Register the JS file in `App.razor`.
- [ ] Wire up the JS callbacks to Blazor component methods via `[JSInvokable]` attributes.

### 5.10 Build Verification

- [ ] Navigate to the dashboard — grid displays tasks with correct columns and formatting.
- [ ] Create a task via the API (curl) — the grid updates in real-time without page refresh.
- [ ] Click a task row — the detail dialog opens with all fields.
- [ ] Edit a task in the dialog — changes persist to the database.
- [ ] Add a note — it appears in the notes list.
- [ ] Use the status filter and "My Tasks" toggle — grid filters correctly.
- [ ] Press `n` — new task dialog opens.
- [ ] Log out and back in — everything works.

---

## Phase 6: CLI Tool (`shallot`)

This phase builds the cross-platform CLI tool that agents and humans use to interact with HotShallot from the terminal.

### 6.1 Set Up CLI Project Structure

- [ ] Configure `HotShallot.Cli.csproj` for Native AOT:
  ```xml
  <PropertyGroup>
      <PublishAot>true</PublishAot>
      <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>
  ```
- [ ] Set up `Spectre.Console.Cli` command infrastructure in `Program.cs`:
  ```csharp
  var app = new CommandApp();
  app.Configure(config =>
  {
      config.SetApplicationName("shallot");
      // Commands will be registered here
  });
  return app.Run(args);
  ```

### 6.2 Implement Configuration & Token Storage

- [ ] Create `Services/ConfigService.cs`:
  - Config file location: `~/.hotshallot/config.json` (cross-platform).
  - Stores: `ServerUrl` (string), `DefaultProject` (string?).
  - Methods: `Load()`, `Save()`, `GetServerUrl()`, `SetDefaultProject(string name)`, `GetDefaultProject()`.
- [ ] Create `Services/TokenStore.cs`:
  - **Windows:** Use `System.Security.Cryptography.ProtectedData` (DPAPI) to encrypt tokens at rest. Store encrypted blob in `~/.hotshallot/tokens.dat`.
  - Methods: `SaveTokens(string accessToken, string refreshToken)`, `LoadTokens()` → `(string? accessToken, string? refreshToken)`, `ClearTokens()`.
  - Check if the access token is expired by reading the `exp` claim. If expired, return null for the access token but keep the refresh token.

### 6.3 Implement the HTTP Client Wrapper

- [ ] Create `Services/ApiClient.cs`:
  - Constructor takes `ConfigService` and `TokenStore`.
  - Maintains an `HttpClient` pointed at the configured server URL.
  - Automatically sets the `Authorization: Bearer <token>` header.
  - **Auto-refresh logic:** Before each request, check if the access token is expired. If so, call `POST /api/auth/token/refresh` with the refresh token to get a new access token. Save the new token. If the refresh token is also invalid, return exit code 2 (auth failure).
  - Helper methods: `GetAsync<T>(string path)`, `PostAsync<T>(string path, object body)`, `PutAsync<T>(string path, object body)`, `PatchAsync<T>(string path, object body)`.
  - On error responses, parse the `ErrorResponse` JSON and throw a typed exception.

### 6.4 Implement the `login` Command

- [ ] Create `Commands/LoginCommand.cs`:
  - Generate a random 8-digit code (format: `XXXX-XXXX`).
  - POST to `/api/auth/device-login-init` with the code.
  - Display the code in a styled Spectre.Console panel:
    ```
    ┌──────────────────────────────────────────┐
    │  Open your browser and enter this code:  │
    │                                          │
    │              1234-5678                    │
    │                                          │
    │  https://hotshallot.azurewebsites.net/   │
    └──────────────────────────────────────────┘
    ```
  - Attempt to open the browser to the device login URL using `Process.Start`.
  - Show a Spectre.Console spinner while polling `/api/auth/device-login-status` every 5 seconds.
  - On approval, save the tokens via `TokenStore` and display "✅ Successfully logged in as {email}".
  - On timeout (5 minutes), display an error and exit with code 2.

### 6.5 Implement the `logout` Command

- [ ] Create `Commands/LogoutCommand.cs`:
  - Call `POST /api/auth/revoke` to invalidate the refresh token (best effort — ignore errors).
  - Call `TokenStore.ClearTokens()`.
  - Display "Logged out."

### 6.6 Implement the `status` Command

- [ ] Create `Commands/StatusCommand.cs`:
  - Call `GET /api/v1/user/me` to get user info.
  - Read the default project from `ConfigService`.
  - If a default project is set, call the API to get task counts grouped by status.
  - Display in a Spectre.Console table:
    ```
    User:     agent-1 (agent1@example.com)
    Project:  MyProject
    Open:     5    InProgress: 3    Blocked: 1
    ```
  - If `--json` flag is set, output as JSON instead.

### 6.7 Implement the `project` Commands

- [ ] Create `Commands/ProjectSetCommand.cs`:
  - Takes a project name argument.
  - Validates the project exists by calling `GET /api/v1/projects` and matching by name.
  - Saves to config via `ConfigService.SetDefaultProject()`.
  - Display "Default project set to: {name}".
- [ ] Create `Commands/ProjectGetCommand.cs`:
  - Reads from `ConfigService.GetDefaultProject()`.
  - Display the name or "No default project set."

### 6.8 Implement the `list` Command

- [ ] Create `Commands/ListCommand.cs`:
  - Options: `--project <name>` (optional if default is set), `--status <filter>`, `--json`, `--quiet`.
  - Call `GET /api/v1/projects/{id}/tasks` with appropriate query parameters.
  - **Default output:** Spectre.Console `Table` with columns: ID (truncated), Title, Status, Priority, Assigned To, Updated.
  - **`--json` output:** Raw JSON array.
  - **`--quiet` output:** Just IDs, one per line.

### 6.9 Implement the `show` Command

- [ ] Create `Commands/ShowCommand.cs`:
  - Takes a task ID argument.
  - Call `GET /api/v1/tasks/{id}`.
  - **Default output:** Spectre.Console `Panel` with task title, status, priority, assigned user, body (attempt to render markdown to ANSI using Spectre.Console markup), and a list of notes.
  - **`--json` output:** Full JSON response.

### 6.10 Implement the `add` Command

- [ ] Create `Commands/AddCommand.cs`:
  - Arguments: `title` (required).
  - Options: `--body <value>`, `--project <name>`, `--priority <level>`, `--json`.
  - Body resolution:
    - If `--body` starts with `@`, read the file (e.g., `@file.md` → read `file.md`).
    - If `--body` is `-`, read from stdin.
    - Otherwise, treat as literal text.
  - Call `POST /api/v1/projects/{id}/tasks`.
  - Display "Created task {id}" or full JSON.

### 6.11 Implement the `update` Command

- [ ] Create `Commands/UpdateCommand.cs`:
  - Arguments: `id` (required).
  - Options: `--status <status>`, `--title <text>`, `--body <value>`, `--priority <level>`, `--json`.
  - Call `PATCH /api/v1/tasks/{id}` with only the provided fields.
  - Display "Updated task {id}" or full JSON.

### 6.12 Implement the `claim` and `done` Commands

- [ ] Create `Commands/ClaimCommand.cs`:
  - Call `POST /api/v1/tasks/{id}/claim`.
  - Display "Claimed task {id}".
  - On 409 conflict, display "Task is already claimed by {user}" and exit with code 4.
- [ ] Create `Commands/DoneCommand.cs`:
  - Call `PATCH /api/v1/tasks/{id}` with `{ "status": "Completed" }`.
  - Display "Completed task {id}".

### 6.13 Implement the `note` Command

- [ ] Create `Commands/NoteCommand.cs`:
  - Arguments: `id` (required), `message` (required).
  - Call `POST /api/v1/tasks/{id}/notes`.
  - Display "Note added to task {id}".

### 6.14 Register All Commands

- [ ] In `Program.cs`, register all commands in the Spectre.Console.Cli app:
  ```csharp
  config.AddCommand<LoginCommand>("login");
  config.AddCommand<LogoutCommand>("logout");
  config.AddCommand<StatusCommand>("status");
  config.AddBranch("project", project =>
  {
      project.AddCommand<ProjectSetCommand>("set");
      project.AddCommand<ProjectGetCommand>("get");
  });
  config.AddCommand<ListCommand>("list");
  config.AddCommand<ShowCommand>("show");
  config.AddCommand<AddCommand>("add");
  config.AddCommand<UpdateCommand>("update");
  config.AddCommand<ClaimCommand>("claim");
  config.AddCommand<DoneCommand>("done");
  config.AddCommand<NoteCommand>("note");
  ```

### 6.15 Exit Code Handling

- [ ] In each command's `Execute` method, return the appropriate exit code:
  - `0` — success.
  - `1` — general error.
  - `2` — authentication failure.
  - `3` — not found.
  - `4` — conflict.
- [ ] In `ApiClient`, map HTTP status codes to exit codes and throw typed exceptions that commands can catch.

### 6.16 Build Verification

- [ ] Build the CLI: `dotnet build src/HotShallot.Cli`
- [ ] Run `shallot login` → complete the device flow → verify tokens are stored.
- [ ] Run `shallot project set <name>` → verify config is saved.
- [ ] Run `shallot status` → verify output.
- [ ] Run `shallot add "Test task" --body "Hello world"` → verify task is created.
- [ ] Run `shallot list` → verify the task appears.
- [ ] Run `shallot claim <id>` → verify assignment.
- [ ] Run `shallot note <id> "Working on it"` → verify note is added.
- [ ] Run `shallot done <id>` → verify completion.
- [ ] Run `shallot show <id>` → verify full details.
- [ ] Run `shallot list --json` → verify JSON output.
- [ ] Run `shallot logout` → verify tokens are cleared.
- [ ] Publish AOT: `dotnet publish src/HotShallot.Cli -c Release -r win-x64` → verify single-file output.

---

## Phase 7: Infrastructure & Deployment

This phase sets up CI/CD, logging, and deploys the application to Azure.

### 7.1 Configure Application Insights

- [ ] Install the NuGet package in the Web project:
  ```
  dotnet add package Microsoft.ApplicationInsights.AspNetCore
  ```
- [ ] Register in `Program.cs`:
  ```csharp
  builder.Services.AddApplicationInsightsTelemetry();
  ```
- [ ] Add the instrumentation key to `appsettings.json` (placeholder for now):
  ```json
  {
    "ApplicationInsights": {
      "ConnectionString": ""
    }
  }
  ```
- [ ] Verify structured log messages appear in the console during local development.

### 7.2 Create Azure Resources

- [ ] Create an Azure Resource Group (e.g., `rg-hotshallot`).
- [ ] Create an Azure App Service Plan (B1 tier, Linux).
- [ ] Create an Azure Web App targeting .NET 10.
- [ ] Create an Application Insights instance and link it to the Web App.
- [ ] Configure App Settings in the Azure portal:
  - `Jwt__SigningKey` = a strong random 64+ character string.
  - `Fido2__ServerDomain` = your app's domain (e.g., `hotshallot.azurewebsites.net`).
  - `Fido2__Origin` = `https://hotshallot.azurewebsites.net`.
  - `ApplicationInsights__ConnectionString` = from the AI resource.
  - `ConnectionStrings__DefaultConnection` = `Data Source=/home/data/hotshallot.db`.

### 7.3 Create GitHub Actions CI/CD Pipeline

- [ ] Create `.github/workflows/deploy.yml`:
  ```yaml
  name: Build and Deploy

  on:
    push:
      branches: [main]

  jobs:
    build:
      runs-on: ubuntu-latest
      steps:
        - uses: actions/checkout@v4
        - uses: actions/setup-dotnet@v4
          with:
            dotnet-version: '10.0.x'
        - run: dotnet restore
        - run: dotnet build --no-restore --configuration Release
        - run: dotnet test --no-build --configuration Release
        - run: dotnet publish src/HotShallot.Web -c Release -o ./publish/web
        - uses: azure/webapps-deploy@v3
          with:
            app-name: 'hotshallot'
            publish-profile: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE }}
            package: ./publish/web

    publish-cli:
      runs-on: ubuntu-latest
      needs: build
      strategy:
        matrix:
          rid: [win-x64, linux-x64, osx-x64]
      steps:
        - uses: actions/checkout@v4
        - uses: actions/setup-dotnet@v4
          with:
            dotnet-version: '10.0.x'
        - run: dotnet publish src/HotShallot.Cli -c Release -r ${{ matrix.rid }}
        - uses: actions/upload-artifact@v4
          with:
            name: shallot-${{ matrix.rid }}
            path: src/HotShallot.Cli/bin/Release/net10.0/${{ matrix.rid }}/publish/
  ```
- [ ] Add the Azure Web App publish profile as a GitHub secret (`AZURE_WEBAPP_PUBLISH_PROFILE`).
- [ ] Push to `main` and verify the pipeline runs successfully.

### 7.4 SQLite Backup Strategy

- [ ] For MVP, set up Azure App Service daily automatic backup (Backup blade in the Azure portal — requires Standard tier or higher) OR:
- [ ] Document a manual backup procedure: SSH into the App Service, copy the `.db` file to a safe location periodically.

### 7.5 Verify Production Deployment

- [ ] Navigate to `https://hotshallot.azurewebsites.net/health` → should return `Healthy`.
- [ ] Register a new user via the web UI.
- [ ] Log in with a passkey.
- [ ] Create a project and tasks.
- [ ] Use the CLI (`shallot login`) to authenticate and interact with tasks.
- [ ] Verify real-time updates work (change a task via CLI, see it update in the browser).

---

## Phase 8: Polish & Final Testing

### 8.1 End-to-End Workflow Test

- [ ] **Full lifecycle test:**
  1. Register a user on the web UI.
  2. Create a project on the web UI.
  3. Use the CLI to log in (`shallot login`).
  4. Set the default project (`shallot project set MyProject`).
  5. Add a task (`shallot add "Implement feature X" --body @spec.md --priority High`).
  6. Verify the task appears on the web dashboard in real-time.
  7. Claim the task (`shallot claim <id>`).
  8. Add a note (`shallot note <id> "Started working on this"`).
  9. Mark as done (`shallot done <id>`).
  10. Verify the dashboard shows the task as completed with the note.

### 8.2 Error Handling Verification

- [ ] CLI with no token → exits with code 2 and helpful message.
- [ ] CLI with expired token → auto-refreshes silently and succeeds.
- [ ] CLI with invalid task ID → exits with code 3 and "not found" message.
- [ ] Claiming an already-claimed task → exits with code 4 and "conflict" message.
- [ ] API request with no auth header → returns 401.
- [ ] API request for another user's project → returns 403.

### 8.3 UI/UX Review

- [ ] Dashboard loads quickly and looks correct in dark mode.
- [ ] All badge colors are visually distinct and meaningful.
- [ ] Task detail dialog opens and closes smoothly.
- [ ] Toast notifications appear for real-time events.
- [ ] Keyboard shortcuts work (`n`, `Enter`, `Esc`).
- [ ] The device login page is simple and clear.
- [ ] CLI output is clean and readable (no garbled characters, tables align correctly).
- [ ] `--json` output is valid JSON for all commands.

### 8.4 Documentation

- [ ] Update `README.md` with:
  - Project description.
  - How to set up the development environment.
  - How to run the web app locally.
  - How to build and use the CLI.
  - How to deploy to Azure.
- [ ] Verify all code has appropriate comments where logic is non-obvious.
