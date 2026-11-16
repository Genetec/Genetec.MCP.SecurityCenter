---
name: sc-entity-resolver
description: Resolve Security Center entities by exact name, name fragment, or natural-language description treated as a name-search phrase; verify supplied GUIDs or logical IDs; and list entities by type across the signed-in account's visible system or within a direct or recursive area scope. Use when a downstream task needs verified identities, a complete inventory, or selected configuration fields.
---

# Security Center entity resolver

Resolve entities visible to the signed-in Security Center session and return their verified identifiers with only the name, type, and scope context needed for the next task. This skill reads and resolves; it does not perform writes.

## Resolution outcomes

Report both the match result and how completely the requested scope was covered:

- **Confirmed:** one identity-verified match in a complete result.
- **Complete list:** all matching records in the requested type and scope were collected with verified terminal-page/end evidence and no failed branches.
- **Ambiguous:** multiple matching candidates. Show the fields needed to distinguish them and ask the user to choose. If coverage is incomplete, say more matches may exist.
- **No match:** zero candidates in a complete search of the requested type and scope.
- **Incomplete:** some candidates may be useful, but truncation, paging, inaccessible scope, or failed pages/branches prevent a uniqueness, absence, or completeness claim.
- **Failed:** the tool returned an error or could not execute the request. A failure is not a no-match result. In batch results, retain successful items and identify failed ones.

Never invent a GUID, select the first of multiple matches, or treat an incomplete result as unique or empty.

## Before reading

Use only tools marked read-only; read-only is a tool-level classification, not an action-level guarantee. Before first use of a selected tool, discover its full schema with `search_tools`; inspect only tools needed for the current step, not every schema at once. Full report schemas can be large. See `references/tool-surface.md`.

Before resolving or reading cardholders, credentials, visitors, users, licence plates, photographs, or access activity, read `sc-pii-guard` and follow its rules for whether and which records may be read. Apply the same check when a field or relation is verified or documented to return person records or personal data. Opaque GUID-only technical references alone do not trigger this check; apply the guard before resolving a referenced PII-sensitive record or using linkable identifiers in a per-person result. If that skill is unavailable, stop before the sensitive read and explain that its rules could not be checked. This skill does not relax those rules. Prefer explicit field reads. Never use `get_entity` for a PII-sensitive record.

Keep the requested entity type and any stated scope as separate constraints. Do not infer an area scope when none was requested. All results describe only entities visible to the signed-in account; do not infer that inaccessible entities do not exist.

## Targeted lookup

Use this procedure when the user names or describes one or more entities:

1. Determine the entity type from the request. If it is known, include it in the search. If the type is materially ambiguous, ask before choosing a type.
2. If the user specified an area, resolve that area before searching for the target. Constrain candidates to its verified hierarchy or membership. The name search itself has no area-scope parameter, so verify membership separately; do not treat a system-wide name hit as in-scope until verified.
3. Separate the entity type, area scope, and name or description from the user's natural-language request. Pass the entity-name or descriptive phrase as `name`, not surrounding instructions; retain words the user indicates are part of the literal name. Use `entity_search` with `action: "search_by_name"`. Set `name_search_mode` to `is` for an exact supplied name. Use `contains` for a description or partial name; use `starts_with` or `ends_with` only when that wording fits. Always provide the name and, when known, the entity type.

   ```json
   {"action":"search_by_name","name":"Loading Dock","entity_type":["camera"],"name_search_mode":"is"}
   ```

4. Use the search result's `Guid`, `Name`, and `EntityType` when it provides them. Read only missing identity fields with `get_fields`; add parent or scope context only when needed to distinguish candidates or establish membership. Reuse verified results already present in the current task; do not re-read the same fields without a reason.
5. Count matching candidates by distinct verified GUID, not result rows; repeated rows for one GUID represent one entity. Keep raw page results for coverage checks while deduplicating identities. Apply the resolution outcomes above. When the current `entity_search` contract confirms a successful unwrapped name-search response, an empty result for an exact query with no unresolved `Partial` or `TooManyResults` envelope establishes no match; a nonempty short result can confirm a unique match under the same condition. If the search indicates more results or the requested page is full, request successive pages when the discovered schema supports it, retain candidates from every page, and use terminal-page evidence to establish coverage. An earlier `TooManyResults` can be resolved by successful subsequent pages through the terminal page; any unresolved incomplete status means the search remains incomplete. Do not infer completeness from an unfamiliar raw response shape or an absent status alone. If an exact search is complete and empty, offer a broader `contains` search. A broad search that is incomplete cannot establish no match.

`entity_search` supports `contains`, `starts_with`, `ends_with`, and `is`. Its `Name` input is optional in the request schema, so always supply it for a targeted lookup; omitting it can enumerate by type instead of searching for the requested name.

## Complete type inventory

Use this only when the user explicitly asks for all entities of a type without an area or other scope. The result covers entities visible to the signed-in session. Discover the selected read-only list/search schema and filter by the requested type; if `entity_search` `search_by_type` is used, its results contain GUIDs only, so resolve names or other requested fields with minimal reads. Apply the inventory coverage and terminal-page rules below. Do not use an unscoped type inventory as a fallback for an area-scoped request.

## Supplied GUID

If the GUID was already verified in this task with the required type and scope, reuse it. Otherwise, call `entity_search` with `action: "get_fields"` and the GUID, requesting the fields needed to verify the requested type and scope. The supplied GUID remains unverified until the read succeeds. Request `Name` only when needed for a useful result or disambiguation. Report an entity as missing only when the tool provides explicit not-found evidence; permission, network, or other read errors are failures, not proof that the entity does not exist. If the GUID identifies an entity of a different type or outside the requested scope, report a verified mismatch with the actual identity; it is not a no-match or tool failure. Do not fetch the complete object just to verify these fields.

## Logical ID

Use `entity_search` with `action: "search_by_logical_id"`, exactly one `entity_type`, and the logical ID. The implementation requires a type and uses the first type if several are supplied. Its query asks the Web SDK for `Name` and `EntityType`; a live camera lookup returned those fields without a GUID. If a result does include a GUID, verify the returned identity and scope. Otherwise, resolve the returned name with an exact name search of the same type. Compare `LogicalId` only when that field is verified for this entity type; otherwise report incomplete resolution. If no unique GUID can be confirmed, report ambiguity, incomplete resolution, or no match according to the available evidence; do not infer one.

## Complete scoped inventory

Use this procedure only when the user asks for a list within a specified scope. Resolve the scope first. For an area, use read-only `entity_search` field reads such as `HierarchicalChildren` and the verified membership fields, then filter member entities by `EntityType`. Inspect the returned shape: child references may contain only GUIDs, in which case resolve only the identity fields needed to filter and report them; do not assume full entities are expanded. In recursive traversal, read each Area GUID at most once and reuse verified child results while retaining every relevant parent path. If a child points to an Area on the current ancestor path, stop that branch and report recursive coverage as incomplete; an Area already seen on another parent branch is a shared reference, so reuse its result and retain both paths. Direct scope means entities associated with the resolved area only; recursive scope includes its descendant areas. Include descendants when the user asks for the full area or site scope. If wording does not make direct versus recursive scope clear and it changes the result, ask which scope they mean. `Cameras` is a valid Area field; `Doors` and `Members` are not. `area_membership` is mixed read/write and is not a read-only substitute. See the verified fields in `references/field-names.md`.

Use candidates returned by the resolved scope's hierarchy or membership fields where possible, filtering by the requested type. Use another read-only list/search surface only when it can establish scope without reading unrelated populations. Do not assume every entity type is represented by an Area hierarchy. If the current read-only fields cannot establish membership for a type, report that the scoped inventory cannot be confirmed with this path instead of substituting a global list.

List and search requests default to `page_size: 50`, allow `1–100`, and use one-based `page` values. For a requested larger inventory, use `page_size: 100` only if the selected tool's discovered description permits that query; then request successive pages. Track page and branch coverage separately from the final entity count: retain each raw page and its GUID/path evidence while checking coverage. The MCP trims Web SDK look-ahead rows to the requested count, but it does not sort or deduplicate pages. Deduplicate final records by GUID and preserve all relevant parent paths for each record. Use a returned total or explicit end/completion evidence when available. If pages remain `Partial` or `TooManyResults`, a page/branch fails, any required branch remains unvisited, or no evidence establishes the end of the requested scope, label the inventory incomplete; do not claim an exhaustive list just because several page numbers were queried.

The tool may return raw JSON rather than a normalized array. Inspect response columns and rows when present. `Partial` and `TooManyResults` envelopes are preserved and mean coverage is incomplete until resolved; upstream `Fail` becomes a tool error. Successful responses may be unwrapped. For inventory, accept a successful empty/short terminal page only when the currently discovered tool contract supports that as end-of-results evidence; an absent status field by itself is not enough. A `TooManyResults` status on an earlier page can be resolved by successful subsequent pages through the terminal page; unresolved incomplete statuses, failed pages, or unvisited scope branches keep coverage incomplete. Do not assume page order is stable or exhaustive beyond this evidence.

## Reading fields

Use `get_fields` for one entity and `get_fields_batch` for several, requesting only verified fields needed by the downstream task. The batch response contains per-item success/failure; `data` is a JSON string and must be parsed separately. Preserve successful items when others fail. A single invalid field can fail that item; see `references/field-names.md`.

Relation fields can expand complete related entities inline. Prefer scalar identifiers such as `ScheduleGuid` when they answer the question. Request expanding fields such as `Recipients`, `Schedule`, `AttachedEntities`, `Cardholders`, or `Members` only for small batches; never combine them with a large entity batch.

For a field not in the reference, first check an appropriate tool schema or existing verified result. For other entity types, use `get_entity` once only when full-record discovery is necessary and permitted; it returns the complete object. For PII-sensitive types, follow `sc-pii-guard` and do not use `get_entity`.

## References

- `references/tool-surface.md`: tool discovery, read-only boundaries, output behavior, and validated entity types.
- `references/field-names.md`: live-verified fields and relation expansion notes.
