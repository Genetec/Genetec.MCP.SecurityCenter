# Field guidance

Use these tiers to decide how a field fits the current request. Classify values in context: a value may be person-linked in one task and operational configuration data in another. This is retrieval and response guidance, not a tool authorization model.

## Restricted secrets

Do not retrieve or reproduce stored passwords, PINs, tokens, or raw credential values such as a stored card number or raw encoded credential data. This applies regardless of the stated purpose or the requester's role. Decide from the value and its use whether it is credential material; a field name alone does not make a value a secret. For example, `facility_code`, `company_code`, `bit_length`, or `formatName` may be technical metadata. Consider the combined value restricted if it forms or exposes a usable stored credential.

A caller-provided value may be used as input for the specifically requested permitted operation when existing tools support it, without unnecessary echo. A supplied badge or plate may be used for its requested lookup through a supported safe path. Caller input does not authorize retrieving or exposing the corresponding stored value. If the requested operation can only be completed by retrieving or echoing restricted material, report that concrete capability gap; do not invent a workaround or keep retrying alternate reads.

`credential.custom_values` is a separate case: it is an arbitrary map whose contents may include credential material. Do not retrieve the whole map. A known custom field needed for the task may be handled individually only where an existing tool exposes that field, subject to the restrictions above.

## Person-identifying data

Examples include `first_name`, `last_name`, or a person's `name`, `email`, photograph, `plate_number`, `plate_state`, and other fields that identify or contact them. Use these fields when needed for the requested person-specific answer or bounded named list. Do not add them to unrelated results merely because they are available. An entity or group name is not automatically a person's name.

## Person-linked and activity data

Cardholder, credential, visitor, or user GUIDs; group membership; status; expiry; assignments; and door, area, or zone activity paired with time may identify or profile a person when joined or listed together. Use the details needed to answer the requested question, within its population and time bounds. Use aggregates when they are sufficient, but do not suppress necessary detail for an explicitly requested person or bounded list.

GUIDs remain useful for joining records without retrieving stored credential values. GUIDs, entity names, status, and device IP addresses are not blanket PII classifications; consider their relationship to a person and the requested output.

## Unknown or extensible fields

Custom fields and other fields with unclear meanings may contain personal or credential data. Do not bulk-read unknown fields across a population to discover their contents. If a particular field is necessary and its meaning materially affects the task, ask the system or data owner once for clarification. Handle known, relevant values individually and within scope.

## Classifying an unlisted value

First determine whether the stored value is a secret or raw credential material; if so, do not retrieve or reproduce it. Otherwise, determine whether it identifies a person directly or becomes person-linked when joined with the other data needed for this task. Use relevant values within the requested scope, and omit unrelated details. If the field's meaning is unknown and necessary to proceed, clarify that meaning rather than probing a population.
