namespace Genetec.Mcp.SecurityCenter.Client;

public enum CustomFieldEntityType
{
    Alarm,
    Application, Area, Asset, Camera,
    Cardholder, CardholderGroup, CashRegister, Credential, Door,
    Elevator, IntrusionArea, IntrusionUnit, LprMlpiRule, LprUnit,
    Patroller, Permit, Role, Schedule, Sequence,
    TilePlugin, User, UserGroup, VideoUnit, Visitor, Zone
}

public enum CustomFieldDataType
{
    Text, Numeric, Date, DateTime, Boolean, Decimal, Image, Entity
}
