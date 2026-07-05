// Delade design-tokens. Ingen hårdkodad styling i komponenter — referera dessa (constitution).
//
// Tema: "Huvudboken" — papper & bläck som en svensk kassabok/kvittorulle.
// Varmt papper som yta, bläcksvart som primärfärg, stämpelröd accent, serif-display för
// rubriker och tabular-nums för belopp. Perforeringslinjer (dashed) skiljer sektioner.
export const tokens = {
  color: {
    bg: "#f4eee1",          // papper
    surface: "#fdfaf2",     // ljusare kvittoslip
    surfaceAlt: "#4a4132",  // mörk bläckgrå (sekundära knappar)
    border: "#d6c9ab",      // blek bläcklinje
    text: "#211b12",        // bläck
    textMuted: "#82765f",   // blekt bläck
    primary: "#1c1710",     // bläcksvart (primära knappar)
    primaryText: "#f8f3e6", // papper på bläck
    danger: "#b3261e",
    success: "#1c6b3c",     // kvittogrönt ("BETALD")
    accent: "#c8102e",      // stämpelröd
  },
  radius: { sm: "2px", md: "3px", lg: "6px" },
  space: { xs: "4px", sm: "8px", md: "16px", lg: "24px", xl: "40px" },
  font: {
    family: "system-ui, -apple-system, 'Segoe UI', Roboto, sans-serif",
    display: "Georgia, 'Iowan Old Style', 'Times New Roman', serif",
    size: { sm: "13px", md: "15px", lg: "20px", xl: "30px" },
  },
  line: {
    // Kvittots perforeringslinje — används som sektionsavdelare.
    perforated: "1px dashed #b9a87f",
  },
} as const;
