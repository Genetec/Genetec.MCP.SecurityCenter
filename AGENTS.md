# Genetec Security Center MCP server

## Project context

This MCP server connects AI clients to Genetec Security Center through the Security Center Web SDK. Tools translate typed MCP requests into Web SDK calls using the signed-in user's credentials, privileges, and partition access. Keep tools thin and reuse the existing client, session, authentication, serialization, and helper layers.

Stdio supports local client-launched sessions. HTTP supports local use and shared deployments with OAuth sign-in. Both transports use the same tool implementations and request standards.

Use the [Web SDK API documentation](https://documenter.getpostman.com/view/26277955/2s9YeK5VtN#auth-info-b3248a87-cfff-412d-b82c-0e0a383a73e2) to confirm contracts when implementing a requested operation or resolving uncertainty. Follow repository implementations for established coding patterns. Finding other API capabilities or differences does not authorize expanding the task. Postman resources are read-only references; editing collections, changing workspace configuration, or publishing resources requires an explicit user request.

## Working principles

- Prefer the simplest change that solves the requested problem. Keep code readable and explicit; add abstractions only when existing duplication or requirements justify them.
- Inspect an equivalent implementation and its tests before editing. Search the client and shared helpers before writing new functionality.
- Maintain existing patterns across tools, requests, responses, configuration, and tests. An inconsistent file is a bug to investigate, not a new pattern to copy.
- Fix defects at their source. Missing descriptions, invalid inputs, or failed assertions must not be hidden by weakening discovery rules, validation, or tests.
- Keep changes within the task. Avoid unrelated refactors, speculative features, fallback paths, migration tooling, and new dependencies.
- Stay within the scope agreed with the user. Discovered adjacent issues, known test failures, new validation, description changes, or refactors do not authorize expanding that scope. Obtain explicit user approval before making out-of-scope edits.
- Preserve existing user edits. Create documentation, reports, or other artifacts only when requested.

## Tool and request standards

- Use one tool class per file, named `<Entity><Operation>Tool.cs`, with accurate MCP name, description, and `ReadOnly`, `Destructive`, `Idempotent`, and `OpenWorld` metadata.
- Put all business inputs in one public sealed `*Request` record passed as `request`, even for a single field. No flat business parameters. Injected services and cancellation tokens remain separate. Tools with no business inputs need no request record.
- Put `[Description("...")]` on the `request` parameter. `search_tools` exposes only described parameters; a missing description is a defect. Describe every request property, including management `Action` fields.
- Use precise contract types throughout: `Guid` for entity IDs, numeric and Boolean types for their values, enums for closed sets, `List<T>` for multiple values, and sealed records for known nested structures. Do not parse typed inputs from strings inside `Execute`.
- Preserve open dictionaries or arbitrary text only when the upstream contract actually defines extensible fields or a text payload. Known structured bodies must expose their fields through typed models.
- Use DataAnnotations for required fields and value constraints. Put `Description` first when combining attributes, for example `[property: Description("Entity GUID"), Required] Guid Id`. Use property-targeted attributes when combining description and validation.
- Use `Required` with `MinLength(1)` for required nonempty strings and collections. Use `Range`, `MaxLength`, and the existing `Iso8601` attribute where appropriate. Conditional requirements and mutually exclusive inputs are checked in `Execute`; do not duplicate ordinary field validation there.
- Validate inputs primarily through precise contract types and existing DataAnnotations. Keep conditional and mutually exclusive requirements in `Execute` only. Do not add ad hoc validators or parsing, move validation into generic REST-client flows, or introduce new validation flows without explicit user approval.
- Use PascalCase for C# types and properties and camelCase for locals. MCP names and parameter references in descriptions and errors use snake_case. The shared serializer performs the naming conversion.
- Management requests use a described, required enum `Action`. Follow existing switch-based implementations and retain the default invalid-action guard.

Use [DoorGetTool](src/Genetec.Mcp.SecurityCenter/Tools/AccessControl/DoorGetTool.cs) for getters and [UserCreateTool](src/Genetec.Mcp.SecurityCenter/Tools/Administration/UserCreateTool.cs) / [UserUpdateTool](src/Genetec.Mcp.SecurityCenter/Tools/Administration/UserUpdateTool.cs) for single/batch CRUD. See [CardholderGroupManagementTool](src/Genetec.Mcp.SecurityCenter/Tools/AccessControl/CardholderGroupManagementTool.cs) for enum action dispatch and per-item collection results.

## Reuse and implementation patterns

- Obtain the existing REST client from `SecurityCenterSessionManager`. Add Web SDK operations to the appropriate client layer rather than implementing HTTP handling in a tool.
- Pass typed models to client methods. Reuse `NumericStringConverter.ToolOptions` for tool serialization and binding. Do not manually serialize request bodies inside tools or replace known nested models with JSON strings or dictionaries.
- Always deserialize known structured responses into typed models and map those models when filtering or transforming data. Do not use `JsonNode`, `JsonObject`, `JsonArray`, or manual JSON property traversal for this work. Handle upstream representation differences in focused JSON converters, and preserve the response contract and paging status when mapping models.
- Reuse `EntityNameSanitizer.Sanitize` when sending entity names to the client.
- Create, update, and delete requests use one `Items` collection for both single and batch operations. Select the operation by item count. Avoid separate single/batch input shapes and comma-separated inputs.
- Reuse `CreateToolHelper.BuildBatchResponse`, `DeleteToolHelper.Execute`, and existing client batch methods. Preserve per-item success and failure results; one failed item must not erase successful batch results.
- Reuse existing `RequireEntityType`, typed getter, and batch-helper patterns for entity-type guards.
- Follow the existing operation's return contract: pass through upstream raw JSON for unchanged reads, and return mapped models directly for reads that filter or transform structured data. Do not serialize mapped models back to JSON inside tools. Use established result models for mutations and batches. Management tools can return different result types through `Task<object>`.
- Throw `ArgumentException` for invalid business input, using snake_case parameter names. Let the shared error filter translate exceptions; do not return custom JSON error objects or conceal failures.
- Reuse setup, parsing, assertions, and cleanup helpers in tests too. Extend a suitable helper when shared behavior is missing instead of copying near-identical implementations.

## Testing standards

- Every tool needs actual unit and integration coverage in the corresponding `Tools/<Domain>/` directories. An empty test file or tool-name assertion alone is insufficient.
- For each change, identify every tool whose behavior or inputs are affected, including tools affected through shared client or helper changes. Add or update unit and integration tests for each affected tool and every changed scenario; tests for one tool do not cover another. Before finishing, check the diff against this list and report test results and any verification gaps for every affected tool.
- Unit tests cover required fields, production request deserialization, invalid values, conditional validation, and request schemas. Use `NumericStringConverter.ToolOptions` when testing binding and `TestHelpers.FakeManager()` for direct validation tests.
- Preserve the assembly-wide checks for a single sealed request, required descriptions, and discoverable request fields. Changes to a tool's inputs must be reflected in its discovery tests.
- Integration tests invoke tool `Execute` methods directly against live Security Center using `ScIntegrationFixture`, covering every exposed action, collection operation, and single/batch path. Follow the corresponding tool integration tests; do not add MCP client or local transport tests for tool behavior.
- Verify entity identity and returned values. Verify mutations by reading the affected entity or report back. Non-null output, HTTP success, or an arbitrary exception is insufficient evidence.
- Cover schema and discovery changes in unit tests using the existing request binding and discovery helpers. Tool integration tests verify live Web SDK behavior through direct tool invocation and read-back.
- Track every created entity with `sc.TrackForCleanup`. Reuse the fixture and test helpers. Live tests modify Security Center; use the configured test instance and preserve serial execution.
- Keep tool happy-path tests against live Security Center in the integration project. Do not extend `FakeManager()` to intercept HTTP and duplicate those tests in the unit project.
- Use `[Theory]` for cases sharing setup and assertions and `[Fact]` for distinct behavior. Do not skip tests to hide missing configuration or an unavailable dependency; report what could and could not be verified.

## Configuration and security invariants

- `ConnectionOptions.Hosts` is an allowlist, not a selected host. Runtime sign-in validates the submitted host and builds `ScConnectionContext` from that host and browser credentials. Sessions and token restoration retain that context. Only the live-test fixture explicitly selects `Hosts[0]`.
- Preserve configuration precedence: CLI overrides environment, which overrides the active JSON. `APPSETTINGS_PATH` selects that JSON; otherwise it is beside the binaries. Exactly one of `Transport.Stdio.Enabled` and `Transport.Http.Enabled` must be enabled.
- HTTP startup uses built-in ASP.NET Core Data Protection to persist `Authentication.ProtectedSigningKey`. Plaintext `Authentication.SigningKey` configuration is rejected. Preserve HS256 and existing signing-secret and `sc_ctx` semantics.
- Retain the active JSON and complete Data Protection key ring across restarts. Missing or corrupt established state must fail before HTTP begins listening; never silently reset it. `Auto` uses Windows DPAPI on Windows and filesystem persistence on Linux, where key-ring files rely on owner-only permissions and protected storage.
- Docker runs as UID/GID `1654`, with persistent JSON and keys under `/app/config`, separate logs, and a read-only certificate mount. Preserve image-seeded configuration and Linux directory/file permissions (`0700`/`0600`). Reuse built-in protection and persistence rather than introducing custom key managers or startup infrastructure.
- Preserve HTTPS settings, certificate validation controls, host restrictions, scope and privilege checks, rate limiting, and file-log rotation. Keep credentials, tokens, and signing material out of committed files and diagnostic output.

## Repository scope and verification

Paths below are relative to `src/Genetec.Mcp.SecurityCenter/`, except the test projects.

| Path | Purpose |
|---|---|
| `Tools/` | MCP tools and request records |
| `Client/` | Web SDK operations and session handling |
| `Helpers/`, `JsonConverters/` | Shared behavior to check before adding implementations |
| `Authentication/`, `Configuration/` | Sign-in, tokens, and configuration |
| `tests/Genetec.Mcp.SecurityCenter.UnitTests/`, `tests/Genetec.Mcp.SecurityCenter.IntegrationTests/` | Unit and integration suites |

Use this map to orient yourself, then inspect the relevant code. Historical plans, generated reports, and temporary artifacts may be outdated; verify their claims against the implementation. `Scott/` and `5.13.0/`, when present, are reference/dependency directories and require an explicit request to modify.

For code changes, run the full unit suite and integration tests relevant to the changed behavior. Distinguish local transport tests from live Security Center evidence. Keep instructions and examples aligned with the current implementation.

```sh
dotnet build src/Genetec.Mcp.SecurityCenter
dotnet test tests/Genetec.Mcp.SecurityCenter.UnitTests
dotnet test tests/Genetec.Mcp.SecurityCenter.IntegrationTests
```

The project targets .NET 10. Live integration settings come from `appsettings.test.json` or environment overrides under `SecurityCenter__WebSdkSettings__` (`Hosts__0`, `User`, `Password`). If running processes lock build outputs, use an isolated `--artifacts-path` instead of stopping them without authorization.
