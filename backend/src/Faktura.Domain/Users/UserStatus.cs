namespace Faktura.Domain.Users;

/// <summary>Account status. In v1 accounts are Active immediately (no email verification).</summary>
public enum UserStatus
{
    Active = 0,
    Invited = 1,
    Disabled = 2
}
