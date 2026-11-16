# Security Center MCP tool surface

Use `search_tools` to inspect the current tool inventory without loading every schema. `detail: "names"` is the cheapest inventory, `summary` returns names and descriptions, and `full` adds parameters and `read_only`. Before first use, inspect the full schema of the selected tool; search for only the specific tool needed for the current step. Full report schemas can include a very large `ScEventType` enum.

Tool scopes are `access_control`, `video_surveillance`, `lpr`, `events`, `administration`, and `utilities`.

## Read-only boundary

Read-only is a property of the whole tool, not an individual action. A mixed read/write tool may not be annotated read-only, and clients may request approval for its read actions. The annotation does not guarantee that approval will or will not be shown.

`entity_search` is the read-only path for name/type/logical-ID lookup and selected-field reads. `area_membership` is mixed read/write; use `entity_search` for area hierarchy and membership fields when those fields establish the requested scope. Verify tool metadata with `search_tools` when uncertain.

## Entity search contract

`entity_search` supports these actions: `search_by_type`, `search_by_name`, `search_by_logical_id`, `get_entity`, `get_fields`, and `get_fields_batch`. Its `entity_type` values are listed below. `name_search_mode` accepts `contains`, `starts_with`, `ends_with`, or `is`.

- `page_size` defaults to 50 and is limited to 1–100; `page` is one-based.
- Search and list results are raw JSON from the Web SDK. Depending on the response, rows may be an array or a `Body` table accompanied by `Columns`; an incomplete response may retain an `Rsp` envelope. Do not assume every result is a normalized `[{Guid, Name}]` object array.
- The client sends `PageSize` and `Page` to the Web SDK. The SDK may return a look-ahead row; the MCP trims rows after the requested count while preserving upstream order and status. It does not sort or deduplicate pages.
- `Partial` and `TooManyResults` envelopes are preserved and indicate incomplete coverage. A Web SDK `Fail` response becomes a tool error. Successful responses may be unwrapped, so an absent status field is not by itself proof that a multi-page inventory is complete.
- The API describes `page` as a page number and `pageSize` as the number of results per page ([Web SDK API documentation](https://documenter.getpostman.com/view/26277955/2s9YeK5VtN)). The code and documentation do not guarantee that successive pages form an exhaustive, stable inventory. Do not claim completeness without a total or other reliable end-of-results evidence.

`search_by_type` returns GUID-only results in the validated tool surface; use a selected-field lookup to resolve their names and types. `search_by_logical_id` requires a type and logical ID; the current client query explicitly requests `Name` and `EntityType`, so do not assume the result includes a GUID without checking.

## Read-only alternatives

| Need | Avoid | Use instead |
|---|---|---|
| Cameras in an area | `area_membership` get_cameras | `entity_search` `get_fields` on the area, field `Cameras` |
| Area hierarchy | `area_membership` get_members | `entity_search` `get_fields`, field `HierarchicalChildren` |
| Access rule membership | `access_rule_configuration` get_cardholders | `entity_search` `get_fields` on the rule, fields `Cardholders`, `RelatedAccessPoints` |
| Alarm recipients | `alarm_configuration` | `entity_search` `get_fields` on the alarm, field `Recipients`, small batches only |
| Archiver load and storage | `archiver_management` | `report_video` with `report_type: "archiver_statistics"` |

An `area_list` response may omit hidden areas and external-system grouping roots by default. Inspect the currently available `area_list` schema before relying on its visibility options; use `show_hidden_areas` or `show_group_roots` only if that schema exposes them. If it does not, use a supported read-only name/type search where it can establish the requested scope, or report the scope as incomplete. Do not interpret a default filtered area list as proof that no other areas exist.

## Other validated response behavior

- Role list actions may include every role regardless of role subtype. For archiver candidates, resolve role names and verify them with `report_video` archiver statistics; do not rely on the list action as a role-type filter.
- Some reports, including `report_access_control` with `report_type: "access_rule_configuration"`, return GUIDs only. Resolve the requested records with minimal fields before presenting them.
- `get_fields_batch` returns per-item success and failure. Successful `data` values are JSON strings, not nested objects; parse them separately and retain successful results if another item fails.
- Relation fields such as `Recipients`, `Schedule`, `AttachedEntities`, `Cardholders`, and `Members` can expand full related entities inline. Request them only for small batches; prefer scalar GUID fields when sufficient.

## Entity types

Accepted values for `entity_type` on `entity_search`:

`user_group`, `user`, `application`, `schedule`, `access_control_unit`, `device`, `cardholder`, `cardholder_group`, `credential`, `access_point`, `access_point_rule`, `system_config`, `partition`, `alarm`, `camera`, `role`, `output_behavior`, `door`, `area`, `elevator`, `zone`, `visitor`, `scheduled_task`, `macro`, `tile_plugin`, `badge`, `user_task`, `file`, `cash_register`, `patroller`, `permit`, `lpr_unit`, `lpr_hotlist_rule`, `lpr_overtime_rule`, `lpr_permit_rule`, `lpr_shared_permit_rule`, `lpr_mlpi_rule`, `network`, `route`, `keyboard`, `monitor`, `sequence`, `stream`, `intrusion_unit`, `intrusion_area`, `server`, `video_unit`, `agent`, `asset`, `archived_stream`, `monitor_group`, `analog_monitor`, `threat_level`, `interface_module`, `transfer_group`, `custom_entity`, `map`, `fusion_stream`, `parking_zone`, `parking_zone_rule`, `tile_layout`, `door_template`, `interface_module_device_definition`, `contact`, `workstation`, `endpoint`, `action_plan`, `folder`, `event_filter_configuration`, `map_layer`, `captain_tablet`
