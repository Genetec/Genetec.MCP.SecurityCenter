# Examples

These examples illustrate the guidance in [field-tiers.md](field-tiers.md). They are not additional policy rules; adapt the scope and detail to the actual request.

## Configuration and operations

**Are credentials set up correctly?** Counts by status or assignment can answer this. Include credential GUIDs or other specific exceptions only if needed to resolve the requested issue. Do not retrieve stored credential values.

**Did a cardholder import work?** Report counts and relevant conflict categories. If the caller asks to identify specific rejected rows, return the bounded names or identifiers needed to locate them without including unrelated source fields.

**Is the group structure right?** Group names and membership counts may be sufficient. If the caller asks who is in a particular group, provide that bounded roster when relevant.

**Who can open this door?** Describe the rules, groups, schedules, and direct assignments that apply. If named people are needed to answer the requested access question, include only the people covered by that question.

**What can this person reach?** This is a person-specific question. Answer with the relevant rules, doors, areas, or schedules for that person; do not expand into unrelated profile details.

## Activity and investigation

**What happened at this door overnight?** A count or time pattern may be enough. Include specific events or person-linked details when the question requests them or they are needed to explain the result.

**Who had repeated denials?** If the caller asks for the people involved, provide the bounded named result and relevant counts. If they ask only for the pattern, aggregate results may answer it.

**What did this person do during this incident?** Use the stated person and time window. Return the activity detail needed to answer the incident question without adding a broader movement history.

**Here is a plate from the car park; whose is it?** Use the supplied plate only through a supported path that returns the needed match without restricted stored values. Return the minimum relevant identity detail without echoing the plate unnecessarily; if no safe result path is available, report that capability gap.

## Choosing useful detail

Use aggregates when they answer the question. Use names, contact details, activity, or bounded person-level rows when they are needed for the explicitly requested answer. In either case, keep the result scoped and omit restricted secrets and unrelated fields.
