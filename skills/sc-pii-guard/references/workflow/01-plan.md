# 1. Plan

Before retrieving Security Center data, identify the minimum plan needed for this task:

- **Intent and target:** What entity, person, group, event, or population did the caller ask about?
- **Bounds:** What time range, location, or population limits the request?
- **Inputs:** Which identifiers or facts did the caller supply, and which facts must come from Security Center?
- **Answer form:** Would a count, summary, specific person answer, or bounded named list answer the request?
- **Fields:** Which fields are needed to produce that answer? Use [field tiers](../field-tiers.md) to account for person-linked values, unknown fields, and restricted secrets.

If a caller names a person or supplies a GUID whose target or type is not yet resolved, plan only the minimum identity or type information needed to resolve it before any broader read. If more than one target remains possible and the choice affects the answer, ask which one they mean.

Infer ordinary intent from a clear request. Ask a concise question only when an unresolved choice would materially change the target, scope, fields, or answer. Do not ask for a separate purpose statement when the requested target and scope are already clear.

Keep this plan within the current task. It is not standing permission. Re-plan if the target, scope, fields, or output materially changes.
