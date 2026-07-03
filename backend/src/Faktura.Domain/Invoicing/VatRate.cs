namespace Faktura.Domain.Invoicing;

/// <summary>Svenska momssatser (procent). 0 = momsfri.</summary>
public enum VatRate
{
    Zero = 0,
    Six = 6,
    Twelve = 12,
    TwentyFive = 25
}

public static class VatRateExtensions
{
    /// <summary>Momssatsen som andel (0.25m för 25 %).</summary>
    public static decimal AsFraction(this VatRate rate) => (int)rate / 100m;

    public static bool IsValid(int percent) => percent is 0 or 6 or 12 or 25;

    public static VatRate FromPercent(int percent) => percent switch
    {
        0 => VatRate.Zero,
        6 => VatRate.Six,
        12 => VatRate.Twelve,
        25 => VatRate.TwentyFive,
        _ => throw new ArgumentOutOfRangeException(nameof(percent), percent, "Ogiltig momssats.")
    };
}
