namespace Faktura.Infrastructure.Configuration;

/// <summary>Appövergripande inställningar, bundna från "App"-sektionen.</summary>
public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>Frontendens bas-URL — används i mejlade länkar (t.ex. inbjudningar).</summary>
    public string BaseUrl { get; set; } = "http://localhost:5173";
}
