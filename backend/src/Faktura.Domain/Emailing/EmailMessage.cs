namespace Faktura.Domain.Emailing;

public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);

/// <summary>Ett e-postmeddelande redo att skickas av en <see cref="Abstractions.IEmailSender"/>.</summary>
public sealed record EmailMessage(
    string FromAddress,
    string FromDisplayName,
    string? ReplyTo,
    string To,
    string Subject,
    string Body,
    EmailAttachment? Attachment);
