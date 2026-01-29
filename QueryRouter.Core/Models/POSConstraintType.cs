namespace QueryRouter.Core.Models;

/// <summary>
/// POS requirement constraint types
/// </summary>
public enum POSConstraintType
{
    Functional,
    Operational,
    Compliance,
    Performance,
    Security,
    Usability
}

/// <summary>
/// POS constraint subcategories
/// </summary>
public enum POSConstraintSubcategory
{
    UI,
    Software,
    Version,
    Payments,
    Promotions,
    Security,
    Finance,
    Observation
}

/// <summary>
/// POS domains
/// </summary>
public static class POSDomains
{
    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        "offline mode",
        "audit logging",
        "GST",
        "duty-free",
        "age verification",
        "payments",
        "loyalty",
        "promotions",
        "inventory",
        "accessibility",
        "peripherals"
    };
}
