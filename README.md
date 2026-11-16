# Disclaimer

By downloading or using the MCP Server, you agree to this disclaimer on your own behalf and on behalf of any organization for which you use it, and you represent that you are authorized to bind that organization.

**TO THE MAXIMUM EXTENT PERMITTED BY LAW: (A) THE MCP SERVER IS PROVIDED “AS IS”, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, EXPRESS, IMPLIED OR STATUTORY, INCLUDING ANY WARRANTIES OR CONDITIONS OF MERCHANTABILITY, MERCHANTABLE QUALITY, FITNESS FOR A PARTICULAR PURPOSE, TITLE OR NON-INFRINGEMENT; AND (B) GENETEC DISCLAIMS ALL LIABILITY ARISING FROM OR RELATING TO THE MCP SERVER OR ITS USE, INCLUDING LIABILITY FOR ANY ACTIONS OR RESULTS OF AN AI SYSTEM OR OTHER THIRD-PARTY TECHNOLOGY USED THROUGH THE MCP SERVER, WHETHER OR NOT INTENDED, AUTHORIZED OR EXPECTED**.

The MCP Server may allow third-party AI systems to access information and take actions in your Genetec environment. You are responsible for any AI system or third-party service you connect, the permissions you grant it and its actions, and for assessing its security, privacy and data-handling implications. Genetec’s processing of personal data is described in its Global Privacy Policy (https://www.genetec.com/legal/privacy).

Genetec has no obligation to support, maintain or update the MCP Server or to meet any service level or availability commitment. Any assistance Genetec provides is voluntary, may stop at any time, and creates no obligation, ongoing or otherwise.

This disclaimer does not affect Genetec’s obligations under the agreement governing any other Genetec product or service, but those obligations do not extend to the MCP Server merely because it connects to or is used with that product or service.

# Genetec Security Center MCP Server

Connect an MCP-compatible AI client to Genetec Security Center for access control, video, license plate recognition (LPR), events, and administration. The MCP server calls the **Security Center Web SDK REST API** using the Security Center credentials you enter on the sign-in page.



## Table of contents

1. [Before you begin](#before-you-begin)
2. [stdio quick start](#stdio-quick-start)
3. [HTTP mode](#http-mode)
   - [Local development with a self-signed certificate](#local-development-with-a-self-signed-certificate)
   - [Published server](#published-server)
   - [Add an HTTP server to your client](#add-an-http-server-to-your-client)
   - [Sign in](#sign-in)
4. [Deployment hardening](#deployment-hardening)
5. [Docker deployment (HTTP)](#docker-http-deployment)
6. [Scopes](#scopes)
7. [Configuration](#configuration)
   - [CLI flags](#cli-flags)
   - [Signing key](#signing-key)
   - [Logs and rate limits](#logs-and-rate-limits)
8. [Troubleshooting](#troubleshooting)
9. [Build from source](#build-from-source)
10. [Skills](#skills)
11. [Running tests](#running-tests)
12. [Product status](#product-status)

## Before you begin

Choose one transport for each server process:

- Use **stdio for local use**, where the MCP client launches the server on the same machine.
- Use **HTTP for local use through `localhost` or deployment across your organization**, where authorized users connect to a running server.

Select the transport with `--transport stdio` or `--transport http`, or enable exactly one transport in `appsettings.json`. For shared deployments, follow [Deployment hardening](#deployment-hardening).

Before connecting:

- Configure a Web-based SDK role in Security Center and make its endpoint reachable from this server. The role is not installed by default; first add it from the **Roles** view of the **System** task in Security Center Config Tool. See [Web-based SDK configuration tabs](https://techdocs.genetec.com/r/en-US/Security-Center-Administrator-Guide-5.14/Web-based-SDK-configuration-tabs) in the Genetec TechDoc Hub, or search for **Web-based SDK role** in the documentation for your Security Center version.
- Confirm the listening port for the Web-based SDK role and whether it uses HTTPS. The MCP server defaults to port `4590`; HTTPS does not automatically change that port.
- The **GSC-MCPServer-User** part number is required and is not included in Security Center licenses by default. Contact [Genetec Customer Service](mailto:CustomerService@genetec.com) to have it added to your license. After the license change, reapply the new Security Center license in Server Admin. See [Reapplying a Security Center license](https://techdocs.genetec.com/r/en-US/Security-Center-Administrator-Guide-5.14/Reapplying-a-Security-Center-license).
- For a quick start, import [privilegeTemplates/mcp-operator-privileges.xml](privilegeTemplates/mcp-operator-privileges.xml) in Config Tool and apply **MCP Operator** to your non-administrator user or group. It provides Operator privileges plus SDK logon and explicitly denies Config Tool and Security Desk logon; configure partition access separately. Applying a privilege template adds to existing privileges; it does not reset or replace them. Verify the user's effective privileges, including privileges assigned directly or inherited from groups, and adjust them as needed. See [Assigning privileges to users](https://techdocs.genetec.com/r/en-US/Security-Center-Administrator-Guide-5.14/Assigning-privileges-to-users).

## stdio quick start

stdio lets your MCP client launch the server locally. Sign-in requires a browser on that machine.

1. Extract the published server folder. Keep `appsettings.json` and `skills/` beside the executable.
2. Edit `SecurityCenter.WebSdkSettings` in `appsettings.json` as shown below, preserving the other sections. See [Configuration](#configuration) for details.

   ```json
   "SecurityCenter": {
     "WebSdkSettings": {
       "Hosts": ["your-sc-server.example.com"],
       "Port": 4590,
       "UseHttps": true,
       "SkipCertificateValidation": false,
       "InvokeTimeoutSec": 30
     }
   }
   ```

   This is a section of the existing JSON file. With these settings, enable **Use HTTPS** on the Web-based SDK role's **Properties** page and allow connections from the MCP server to the role's configured port. The example uses port `4590`.

3. Register the server with your client using one of the following commands. Replace the executable path with the location where you extracted the server files.

   **[Copilot CLI](https://docs.github.com/en/copilot/how-tos/copilot-cli/customize-copilot/add-mcp-servers)**

   ```sh
   copilot mcp add genetec-sc -- "C:/path/to/Genetec.Mcp.SecurityCenter.exe" --transport stdio
   ```

   **[Codex](https://developers.openai.com/codex/mcp)**

   ```sh
   codex mcp add genetec-sc -- "C:/path/to/Genetec.Mcp.SecurityCenter.exe" --transport stdio
   ```

   **[Claude Code](https://code.claude.com/docs/en/mcp)**

   ```sh
   claude mcp add --scope user --transport stdio genetec-sc -- "C:/path/to/Genetec.Mcp.SecurityCenter.exe" --transport stdio
   ```

   On Linux, use the executable path without `.exe`; run `chmod +x` on it if needed.

4. Restart the client and ask the agent to connect to Security Center. It calls `session_management` with `action: "connect"`, opening the sign-in page.
5. Enter your Security Center host, username, and password, then [select your scopes](#scopes) and sign in. Scopes are groups of tools, such as Access Control or Video Surveillance, that the agent can use in this session.
6. Return to your client to use the server.

> **Note:** `connect` reuses an active session. To change credentials or the connected host, use `logout` and then `connect`. The sign-in page closes its local listener after success or five minutes.

## HTTP mode

HTTP runs a server that OAuth-capable MCP clients connect to at `https://your-server:5000/mcp`. Users sign in with their Security Center credentials and select scopes in the browser.

Configure the Web-based SDK role connection as described in the [stdio quick start](#stdio-quick-start), then start the MCP server with `--transport http`. The MCP server's HTTPS certificate is separate from the certificate used by the Web-based SDK role.

### Local development with a self-signed certificate

With the .NET 10 SDK installed, create and trust a localhost development certificate, then run from the repository root:

```powershell
dotnet dev-certs https --trust
dotnet run --project src/Genetec.Mcp.SecurityCenter/Genetec.Mcp.SecurityCenter.csproj -- --transport http
```

On Linux, add the certificate to the trust store used by your distribution and MCP client. A localhost development certificate is suitable only for local development.

### Published server

Use a certificate whose hostname matches the URL your clients use. Update the `Kestrel` section in `appsettings.json` to use your certificate and listening address, preserving any other settings:

```json
"Kestrel": {
  "Endpoints": {
    "Https": {
      "Url": "https://0.0.0.0:5000",
      "Certificate": { "Path": "C:/certs/server.pfx" }
    }
  }
}
```

If the PFX has a password, supply it through the process environment:

```powershell
$env:Kestrel__Endpoints__Https__Certificate__Password = "your-pfx-password"
./Genetec.Mcp.SecurityCenter.exe --transport http
```

The service account needs write access to `appsettings.json` and its signing-key storage. See [Signing key](#signing-key).

### Add an HTTP server to your client

Replace the URL with your server address. For local development or Docker Compose, use `https://localhost:5000/mcp`.

**Copilot CLI**

```sh
copilot mcp add --transport http genetec-sc https://your-server:5000/mcp
```

**Codex**

```sh
codex mcp add genetec-sc --url https://your-server:5000/mcp
```

**Claude Code**

```sh
claude mcp add --scope user --transport http genetec-sc https://your-server:5000/mcp
```

### Sign in

1. Start authentication in your client for the `/mcp` endpoint. The client opens the server's sign-in page in your browser.
2. In the browser, enter your Security Center host, username, and password, then [select your scopes](#scopes) and sign in. Scopes determine which groups of tools the agent can use in this session.
3. Return to your client to use the server.

## Deployment hardening

The MCP server does not need to run on the same machine as Security Center. It can run on a separate server or container with network access to the Web-based SDK role endpoint. Deploy the MCP server in a protected, secure environment: it handles Security Center credentials and can perform operations using each signed-in user's privileges.

- **Restrict network access.** Host the server on a managed internal network or behind an organization-controlled VPN. Allow inbound access only from authorized client networks, and restrict outbound access to the required Web-based SDK role hosts and ports. Do not expose the MCP server directly to the public internet. The HTTP example binds to all interfaces (`0.0.0.0`); restrict access with firewall rules or bind to the intended private interface. Docker Compose binds to loopback by default.
- **Use HTTPS on both connections.** Configure a trusted certificate matching the MCP server hostname, and set `SecurityCenter.WebSdkSettings.UseHttps` to `true` and `SkipCertificateValidation` to `false` for the Web SDK connection. These are the current defaults; verify the effective settings before deployment. Install the issuing CA in the server's trust store when using an internal CA. Use development certificates only for local development.
- **Limit allowed Security Center hosts.** Set `SecurityCenter.WebSdkSettings.Hosts` to only the approved Web-based SDK role hostnames or IP addresses. Combine this allowlist with network restrictions. See [Configuration](#configuration) for array override behavior.
- **Apply least privilege.** Run the server under a dedicated, non-administrator service account. Give Security Center users only the privileges and partition access their work requires, and select only the needed MCP scopes at sign-in. Scopes limit available tools but do not replace Security Center permissions.
- **Protect secrets and persistent state.** Restrict access to certificate private keys, certificate passwords, the active `appsettings.json`, and signing-key storage. Supply certificate passwords through your organization's secret-management or service configuration mechanism. Give the service account only the required file permissions, including write access to the active JSON, signing-key storage, and logs. Protect backups of the JSON and complete key ring as described under [Signing key](#signing-key).
- **Protect and monitor logs.** Restrict access to log files and collected console output; Web SDK error dumps can contain credentials and personal data. Keep Web SDK logging at `Information` during normal operation and enable `Verbose` only for controlled troubleshooting. Monitor authentication failures, service health, and disk space. File rotation manages retention but does not prevent logging failures when storage is full or unavailable.
- **Maintain the host and service.** Keep the operating system, server binaries, container images, and certificates current. Keep rate limiting enabled, tune it for expected usage, and use a service manager or container restart policy to recover from failures.

<a id="docker-http-deployment"></a>

## Docker deployment (HTTP)

This procedure is optional and applies to deployments using Docker Compose. The included Compose configuration runs the server in HTTP mode. Native stdio and HTTP deployments do not require Docker.

Install Docker with Docker Compose support, then run these steps from the repository root:

1. Place your HTTPS certificate at `certs/server.pfx`. For local development, export the self-signed certificate from the [HTTP guide](#local-development-with-a-self-signed-certificate):

   ```powershell
   New-Item -ItemType Directory -Force certs
   dotnet dev-certs https --export-path ./certs/server.pfx --password "your-local-password"
   ```

2. Copy `.env.example` to `.env`. Set the allowed Web-based SDK role host, port, and protocol, and set `Kestrel__Endpoints__Https__Certificate__Password` to match the PFX password. This is the environment-variable form of `Kestrel:Endpoints:Https:Certificate:Password` in `appsettings.json`, using the same name for Docker and native runs.
3. Start the server:

   ```sh
   docker compose up --build -d
   ```

4. [Add the HTTP server to your client](#add-an-http-server-to-your-client) using **`https://localhost:5000/mcp`**, then [sign in](#sign-in). Docker Compose binds port 5000 to loopback; adjust the port binding if you need remote access and follow [Deployment hardening](#deployment-hardening). Remote clients also need a trusted certificate matching the server hostname.

> **Note:** Docker Compose uses **`/app/config/appsettings.json`**. Docker seeds a new `configuration` volume with the packaged JSON; existing volumes retain their settings. Environment variables supplied by `docker-compose.yml` override that JSON. Docker Compose reads `.env`, but shell variables take precedence over its values. Transport and certificate paths are set by Docker Compose.

Keep the `configuration` volume: it contains the JSON and signing-key ring. Logs use a separate `logs` volume. The container runs as UID/GID `1654`; mounted certificates must be readable by that identity. Recreate the container after changing `.env`:

```sh
docker compose up -d --force-recreate
```

## Scopes

Scopes group tools by function and limit what the agent can call in your session. Select them when signing in through either stdio or HTTP. They do not grant Security Center privileges: operations still use your account's permissions and partition access.

| Scope | Includes |
|---|---|
| `access_control` | Doors, cardholders, credentials, access rules, visitors |
| `video_surveillance` | Cameras, PTZ, recording, streams, archivers |
| `lpr` | Hotlists, parking, permits, LPR operations |
| `events` | Event operations and streaming |
| `administration` | Users, groups, roles, alarms, schedules |
| `utilities` | Entity search, reports, maps, system configuration |

Use `--scopes` or `Transport.Scopes` to preselect scopes in the sign-in form for either transport. You can change the selection before signing in. Tool discovery is filtered by the selected scopes once a session is established.

## Configuration

- Native runs read `appsettings.json` beside the binaries, or the file selected by `APPSETTINGS_PATH`.
- CLI flags override process environment variables, which override JSON.
- Native executables do not load `.env` files; export the variables or supply them through your MCP client's `env` configuration.

Use `__` between sections in environment variables. For example, `SecurityCenter__WebSdkSettings__Hosts__0=sc.example.com` overrides the first allowed host. Arrays merge by index, so override every existing index when replacing an allowlist.

The following settings are under `SecurityCenter.WebSdkSettings`:

| Setting | Purpose |
|---|---|
| `Hosts` | Allowed hostnames or IP addresses, without ports, URLs, or wildcards. Matching is exact and case-insensitive; an empty list blocks sign-in. |
| `Port` / `UseHttps` | Web SDK port and protocol; defaults are `4590` and `true`. |
| `SkipCertificateValidation` | Controls whether Web SDK certificate validation is bypassed. Defaults to `false`; keep it `false` for deployment. |
| `InvokeTimeoutSec` | Web SDK request timeout; default `30` seconds. |

Users choose the connected host in the sign-in form; `Hosts` only validates that choice. Runtime credentials come from the form and do not use configured `User` or `Password` values.

The Web SDK application ID is fixed by the server and cannot be overridden through configuration.

### CLI flags

| Flag | Usage |
|---|---|
| `--transport stdio` or `--transport http` | Select one transport. The shipped JSON defaults to stdio; Docker Compose selects HTTP. |
| `--scopes access_control,video_surveillance` | Preselect HTTP or stdio scopes; comma- or space-separated. Omit to use configured defaults, or all scopes when none apply. |

### Signing key

On first HTTP startup, the server generates a signing key and stores it encrypted as `Authentication.ProtectedSigningKey` in the active JSON. Restarts reuse it.

`Authentication.SigningKeyProtection.Provider` defaults to `Auto`: Windows DPAPI under the service account on Windows, or filesystem protection on Linux. `FileSystem` leaves key-ring secrets unencrypted in their files; protect the directory with mode `0700` and files with `0600`.

The key ring defaults to `keys/` beside the active JSON on Windows, `~/.config/genetec/mcp/keys` under the service account on native Linux, and `/app/config/keys` in Docker. Set `Authentication.SigningKeyProtection.KeyRingPath` to an absolute path to override it.

Back up and retain the JSON **and the complete key ring**, including expired keys. Keep the same Windows identity when using DPAPI. Missing or corrupt established key state stops HTTP startup. Replacing the signing key requires clients to sign in again.

### Logs and rate limits

- The shipped configuration logs to the console and files in `logs/` beside the server executable. Log files rotate daily or when a file reaches 100 MiB (104,857,600 bytes).
- The file logger retains the latest 30 log files and deletes the oldest files when that limit is exceeded.
- When a Serilog File sink omits `Args.path`, the server defaults to `logs/mcp-server-.log` beside the executable. Explicitly configured paths are used unchanged; relative paths are resolved from the process's working directory. Remove an older relative path setting to use the default, or configure an absolute path for another location.
- The MCP server's Web SDK logging defaults to `Information`, which includes successful request metadata. Failed requests log full error response bodies and exceptions at `Warning`.
- Set `Serilog__MinimumLevel__Override__WebSdk=Verbose` to include successful response bodies and collected events. Error bodies and verbose dumps can contain credentials and personal data; restrict access and enable verbose logging only for controlled troubleshooting.

Tool calls and resource reads share per-user limits: 5 concurrent requests and 200 calls per 5-minute window, with up to 30 seconds waiting for a concurrency slot. Configure them under `RateLimiting`. Authorization and token endpoints also limit attempts per IP.

## Troubleshooting

### Cannot connect to Security Center

Check the effective configuration described under [Configuration](#configuration), including environment variables and CLI overrides:

- **Username and password:** Verify the Security Center credentials entered on the sign-in page and that the user account has the **Log on using the SDK** privilege.
- **Hosts:** Verify that the host entered at sign-in points to the Web-based SDK role server and is included in `SecurityCenter.WebSdkSettings.Hosts`. Allowlist entries must be hostnames or IP addresses without a protocol or port.
- **Web-based SDK role:** In Security Center Config Tool, confirm that the Web-based SDK role is running and check its configured listening port and HTTPS setting. Set `SecurityCenter.WebSdkSettings.Port` to that port and `UseHttps` to match the role's protocol. The default port is `4590`; enabling HTTPS does not automatically change it.
- **Network and certificates:** Confirm that the MCP server can reach the Web-based SDK role host and port. When using HTTPS, verify that the role's certificate matches the hostname and is trusted by the machine running the MCP server.

## Build from source

Build from source if you want to compile the MCP server yourself instead of using a published executable. This requires the **.NET 10 SDK**.

Clone the repository, then run the server from its root:

```sh
git clone https://github.com/Genetec/Genetec.MCP.SecurityCenter.git
cd Genetec.MCP.SecurityCenter
dotnet run --project src/Genetec.Mcp.SecurityCenter/Genetec.Mcp.SecurityCenter.csproj -- --transport stdio
```

To create a self-contained Windows executable, publish the server with its configuration, privilege templates, and skills:

```powershell
dotnet publish src/Genetec.Mcp.SecurityCenter/Genetec.Mcp.SecurityCenter.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -o publish/win-x64
```

Use `linux-x64` and a matching output folder for Linux. Distribute the complete publish folder, including `appsettings.json`, `privilegeTemplates/`, and `skills/`.

## Skills

Publishes and Docker images include two companion skills: **entity resolver** and **PII guard**. Install them in your client's skills directory using [skills/README.md](skills/README.md); connecting to MCP does not install them. See [example requests](skills/PROMPTS.md). The agent uses `search_tools` to discover tools and retrieve parameter schemas before calling them.

## Running tests

This section is for contributors working from source. Run the test suites from the repository root:

```sh
dotnet test tests/Genetec.Mcp.SecurityCenter.UnitTests
dotnet test tests/Genetec.Mcp.SecurityCenter.IntegrationTests
```

For integration tests, configure `SecurityCenter.WebSdkSettings` in `appsettings.test.json` with `Hosts`, `User`, and `Password`, or set `SecurityCenter__WebSdkSettings__Hosts__0`, `SecurityCenter__WebSdkSettings__User`, and `SecurityCenter__WebSdkSettings__Password` as environment variables. Each `__` separates configuration levels; `Hosts__0` selects the first array entry. The fixture selects the first allowed host. Live tests can modify Security Center; use a dedicated test instance, preserve serial execution, and track every created entity with `sc.TrackForCleanup`.

Tool tests belong in the corresponding tool's test file under `Tools/` in each test project, using the same domain directories as the implementation.

| Test suite | What it verifies |
|---|---|
| Unit tests | Required fields, production JSON binding, invalid values, conditional validation, request schemas, and discovery descriptions. Assembly-wide checks enforce the shared request and discovery standards across registered tools. These checks do not verify live Web SDK behavior. |
| Tool integration tests | The actual tool's `Execute` method against live Security Center through `ScIntegrationFixture`. Cover each exposed action and the applicable single and batch paths, then verify entity identity and returned values. Verify mutations by reading the affected entity or report back. |

Do not use MCP client tests or a local MCP HTTP server to validate tool behavior. Checking `tools/list` or `search_tools` over HTTP verifies discovery and transport; it does not execute the selected tool's Web SDK operation. Keep schema and discovery assertions in the unit suite. An external discovery test does not replace integration coverage in the actual tool's test file.

A successful call, non-null result, or session-state response does not prove that a Web SDK operation or browser sign-in completed. Assertions must verify the behavior being tested, and test reports must state what was actually checked.

Every tool changed directly or affected by shared client or helper changes needs unit and integration coverage for its changed scenarios. Coverage for one tool does not replace coverage for another. Run the full unit suite and the relevant integration tests for every affected tool. Report failures and unverified scenarios explicitly; passing discovery checks must not be reported as successful live tool operations. See [AGENTS.md](AGENTS.md) for the complete testing standards.

## Product status

This is **not an official Genetec product**. Its use does not change or extend any Genetec service-level agreement (SLA).

Licensed under the [Apache License 2.0](LICENSE).
