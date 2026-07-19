namespace Faktura.Domain.Invoicing;

/// <summary>
/// OCR-referensnummer enligt bankgirots standard ("hård kontrollnivå 2"):
/// basnummer + längdsiffra (totala längden mod 10) + Luhn-kontrollsiffra (mod 10).
/// </summary>
public static class OcrReference
{
    public static string Generate(long invoiceNumber)
    {
        var payload = $"{invoiceNumber}{(invoiceNumber.ToString().Length + 2) % 10}";
        return payload + LuhnCheckDigit(payload);
    }

    /// <summary>Verifierar både Luhn-kontrollsiffran och längdsiffran (näst sista positionen).</summary>
    public static bool IsValid(string ocr)
    {
        if (ocr.Length < 3 || !ocr.All(char.IsAsciiDigit)) return false;
        if ((ocr[^2] - '0') != ocr.Length % 10) return false;
        return LuhnCheckDigit(ocr[..^1]) == ocr[^1] - '0';
    }

    private static int LuhnCheckDigit(string digits)
    {
        var sum = 0;
        var doubling = true; // vikterna 2,1,2,1… räknat från höger
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var d = (digits[i] - '0') * (doubling ? 2 : 1);
            sum += d > 9 ? d - 9 : d;
            doubling = !doubling;
        }
        return (10 - sum % 10) % 10;
    }
}
