namespace Faktura.Domain.Common;

/// <summary>
/// Ett exakt penningbelopp (SEK) baserat på <see cref="decimal"/>. Avrundning sker till öre
/// (2 decimaler) med away-from-zero enligt svensk praxis.
/// </summary>
public readonly record struct Money(decimal Amount)
{
    public static readonly Money Zero = new(0m);

    /// <summary>Avrundar ett råbelopp till öre (2 decimaler, away-from-zero).</summary>
    public static Money Round(decimal raw) => new(Math.Round(raw, 2, MidpointRounding.AwayFromZero));

    public static Money operator +(Money a, Money b) => new(a.Amount + b.Amount);
    public static Money operator -(Money a, Money b) => new(a.Amount - b.Amount);

    public Money Negate() => new(-Amount);

    public override string ToString() => Amount.ToString("0.00");
}
