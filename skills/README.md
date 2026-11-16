# Security Center MCP skills

This bundle contains two companion skills for the Security Center MCP server.

| Skill | Role |
|---|---|
| `sc-entity-resolver` | Resolve names to GUIDs, handle ambiguous matches, and read verified configuration fields. |
| `sc-pii-guard` | Plan scoped retrieval, process relevant personal data, and review answers against the request and field guidance. |

## Install

Server publishes include this directory beside the executable. Docker images contain it
at `/app/skills`; clients still need their own local installation.

Copy `sc-entity-resolver` and `sc-pii-guard`, with their complete contents, into your client's
supported skills directory. In Claude Code that is `.claude/skills/` in the project, or
`~/.claude/skills/` for all projects. Keep the references with each `SKILL.md`.

Configure the Security Center MCP connection in the client as well. Connecting to the
server does not automatically install skills. Both skills consist of instructions and
references; no script runtime or package installation is required.

## Use

Describe the task in plain language; see [example requests](PROMPTS.md). Entity resolver
handles names and configuration reads. PII guard applies when a task touches personal or
credential-related data and follows three stages: plan, process, and review.

Use [PII field tiers](sc-pii-guard/references/field-tiers.md) as the single policy reference.
Use aggregates when they answer the question, and person-specific detail or bounded named
lists when the request needs them. Keep retrieval and output scoped, and do not retrieve
or reproduce stored passwords, PINs, tokens, or raw credential values.

For a requested artifact, review the content available capabilities can inspect. If relevant
content cannot be inspected, mark the privacy review **Unverified** rather than claiming
it is clean. This guidance does not certify files or replace Security Center privileges,
MCP tool validation, or client approval controls.

The server's `read_only` annotation applies per tool, so a tool with both read and write
actions may require client approval even for a read action. See
[the tool surface reference](sc-entity-resolver/references/tool-surface.md).
