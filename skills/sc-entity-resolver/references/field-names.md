# Verified field names

Field names below were confirmed against a live system through `get_entity` and then exercised through `get_fields` or `get_fields_batch`. Treat any name not listed here as unverified; a bad field can fail the whole request or batch item. First inspect the relevant tool schema when field names are exposed there. If discovery through `get_entity` is necessary, use it once only for a permitted non-PII-sensitive entity because it returns the complete object, including expanded relations. For cardholders, credentials, visitors, and users, follow `sc-pii-guard`; never use `get_entity` to discover their fields.

Fields marked **expanding** inline the full related entity. Request them for two or three entities at most per call.

## Known field names for selective entity reads

`Name`, `Description`, `Guid`, `EntityType`, `EntitySubType`, `SubType`, `LogicalId`, `CreatedOn`, `ModifiedOn`, `IsOnline`, `RunningState`, `IsInMaintenance`, `IsMaintenanceSupported`, `MaintenanceEndTime`, `MaintenanceReason`, `HiddenFromUI`, `HierarchicalChildren`, `HierarchicalParents`, `CustomFields`, `LinkedMaps`, `OwnerRole`, `OwnerRoleType`, `IsOwnedByPlugin`, `SupportedEvents`, `SupportedCustomEvents`

Availability and meaning can vary by entity type and Security Center version. This is a reference to field names exercised on live entities, not a guarantee that every field exists on every type. Use only fields needed for the task. These names are not list commands or search filters; use the selected list/search tool's exposed type, scope, paging, and visibility options for enumeration. For known properties, prefer `get_fields` or `get_fields_batch`; `get_entity` returns the complete object and is not the default way to explore this list.

`ModifiedOn` is useful for change detection between two baselines. `CustomFields` is where Design-phase requirements attached to a site live.

Identity fields such as `Name`, `Guid`, and `EntityType` are scalar. For unmarked relation or structured fields, do not infer scalar or expanded shape from the field name; check entity-specific guidance and response shape before broad use. Apply `sc-pii-guard` before a field read that may expose personal data.

For example, `area_list` already filters hidden areas and external-system grouping roots. Use `show_hidden_areas` and `show_group_roots` only when the currently discovered tool schema exposes those options; do not re-read fields just to repeat that list filtering.

## Area

Valid: `Name`, `Cameras`, `HierarchicalChildren`, `HierarchicalParents`

Invalid, confirmed: `Doors`, `Members`. Doors appear through `HierarchicalChildren`; resolve the children and filter by `EntityType`.

## Alarm

`Name`, `Priority`, `Recipients` (**expanding**), `AttachedEntities`, `AlarmProcedureUrl`, `IsAlarmProcedureEnabled`, `AlarmColor`, `AlarmColorString`, `AlarmRecordingTime`, `AutoAcknowledgmentDelay`, `ReactivationThreshold`, `DistributionMode`, `IsActive`, `ScheduleGuid`, `SchedulesGuids`, `Schedule` (**expanding**), `DefaultVideoMode`, `VideoMode`, `PlaybackPreTriggerTime`, `ContentCycling`, `CreateIncidentOnAcknowledgement`

`Recipients` entries are objects shaped `{Entity, EntityGuid, Priority}`. `EntityGuid` is the useful part; `Entity` is the expansion that causes the payload problem.

## Access rule

`Name`, `IsDenyingRule`, `AccessRuleType`, `IsSystemRule`, `ScheduleGuid`, `Schedule` (**expanding**), `Cardholders` (**expanding**), `Members` (**expanding**), `RelatedAccessPoints`, `StartTime`, `EndTime`

Invalid, confirmed: `IsDenying`. Use `IsDenyingRule`.

`IsSystemRule: true` marks auto-generated default rules such as `Default rule: <door> - IN`. Exclude them from audits of intended configuration, or report them separately, since they are not something the integrator authored.

## Role

`Name`, `EntityType`, `SubType`

A non-zero `SubType` GUID indicates a plugin role. Role type is not otherwise exposed on the entity, so archivers must be identified by name pattern or by whether `report_video archiver_statistics` returns data for them.

## Archiver statistics

Not an entity read. Returned by `report_video` with `report_type: "archiver_statistics"` and the archiver role GUID as `id`:

`RoleId`, `ServerId`, `ActiveCameras`, `ArchivingCameras`, `LoadPercent`, `ReceivingRate`, `WritingRate`, `ArchivingSpan`, `AvailableSpace`, `FreeSpace`, `UsedSpace`, `ComputerNetworkTrafficIn`, `ComputerNetworkTrafficOut`, `CloudArchivesUploadSpeedInBytesPerSecond`, `CloudArchivesBacklogFilesCount`, `CloudArchivesBacklogOldestFileStartTime`, `CloudArchivesBacklogSizeInBytes`

`ActiveCameras` minus `ArchivingCameras` is the count of cameras attached to the archiver that are not recording. `ArchivingSpan` is formatted `days.hh:mm:ss` and is the effective retention actually achieved, which is the number to compare against a contractual retention requirement.

## Discovering an unlisted field

Treat unlisted field names as unverified. Prefer schema or previously verified evidence. If a non-PII-sensitive field still needs discovery, use `get_entity` on at most one entity of that type and inspect the returned property names; the response may include expanded relations. For PII-sensitive types, follow `sc-pii-guard` and use only permitted explicit-field reads. Do not infer that a field exists just because a similarly named field exists on another entity type.
