namespace Faktura.Infrastructure.Email;

/// <summary>SMTP-konfiguration, bunden från "Smtp"-sektionen.</summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 587;
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    public bool UseStartTls { get; set; } = true;

    /// <summary>Systemets avsändaradress (From). Visningsnamnet sätts per utskick (organisationens namn).</summary>
    public string FromAddress { get; set; } = "no-reply@faktura.local";
}
