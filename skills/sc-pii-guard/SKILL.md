---
name: sc-pii-guard
description: Guide the retrieval and use of personal or credential-related data in Security Center MCP tasks. Use for questions about cardholders, credentials, visitors, users, licence plates, photographs, access activity, or other person-linked data.
---

# Personal data guidance

Use this guide when a Security Center MCP task touches data about people. It helps keep retrieval and answers within the caller's requested scope. It is client-side guidance, not an authorization or validation control; existing Security Center privileges and MCP tool behavior still apply.

Follow the stages in order:

1. [Plan](references/workflow/01-plan.md) the target, scope, output, and fields needed.
2. [Process](references/workflow/02-process.md) the request using existing MCP tools and the smallest useful result.
3. [Review](references/workflow/03-review.md) the answer for scope and unnecessary detail.

Use [field tiers](references/field-tiers.md) as the single policy reference. [Examples](references/questions.md) illustrate how the guidance applies; they do not add requirements.

Keep the plan only for the current task. Re-plan when the requested target, scope, fields, or output materially changes. A plan does not grant continuing permission or access.

## Core guidance

- Answer the question asked, including person-specific questions and bounded lists of named people when those details are needed to answer it.
- Use aggregate results when they answer the question. Do not force aggregation when a specific person, a bounded named list, or relevant activity detail is the requested answer.
- Keep retrieval and output within the stated target, time range, and purpose. Do not add unrelated profiles or enrichment.
- Use person-linked GUIDs when needed to join relevant records. GUIDs, entity names, status, and device IP addresses are not automatically personal data; consider whether a value is linked to a person in this task.
- Do not retrieve or reproduce stored passwords, PINs, tokens, or raw credential material. A caller-supplied value may be input to the specifically requested permitted operation when existing tools support it; do not echo it unnecessarily or retrieve the corresponding stored value. If the operation can only be completed by retrieving or echoing restricted material, report that concrete capability gap.
- Treat `credential.custom_values` as an arbitrary map that may contain credential material. Do not retrieve it wholesale. A known, task-relevant custom field may be handled individually where an existing tool exposes that field; if an unknown field's meaning is essential, ask once before relying on it.
- Existing MCP tools and Security Center remain responsible for access and validation. This guidance does not create additional permission checks or certify files.

## Scope boundaries

This guide applies to ordinary MCP reads and answers, and carries the same scope and secret guidance into other requested operations. It does not define a general write or file-generation process. For an artifact, review only the content available capabilities can inspect; if relevant content cannot be inspected, mark the privacy review **Unverified** rather than claiming it is clean.
