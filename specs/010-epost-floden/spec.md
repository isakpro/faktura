# Feature Specification: Kontoflöden via e-post (registreringsskydd + mejlade inbjudningar)

**Feature Branch**: `feature/010-epost-floden` · **Created**: 2026-07-06 · **Status**: Draft

## Översikt

Två uppföljningar som använder e-postmotorn:
1. **Registreringsskydd (A3):** upprepade registreringsförsök mot samma e-postadress bromsas
   (429), och adressens ägare får ett **varningsmejl** ("någon försökte registrera med din
   adress"). Auto-login för nya konton behålls (beslut 2026-07-06: broms + varningsmejl i
   stället för identiska svar — massenumerering blir opraktisk utan att signup-UX offras).
2. **Mejlade inbjudningar (A4):** när en användare bjuds in mejlas accept-länken direkt till
   den inbjudna (Owner ser fortfarande länken som fallback). Mejlfel stoppar aldrig inbjudan.

## Requirements
- **FR-001**: Registreringsförsök mot en upptagen adress räknas per adress; över tröskeln
  svaras 429 + `Retry-After`. Nya (lediga) adresser påverkas inte.
- **FR-002**: Vid försök mot upptagen adress skickas ett varningsmejl till adressens ägare
  (fel sväljs och loggas — svaret påverkas inte).
- **FR-003**: Inbjudningar mejlas till den inbjudna med accept-länk (`{App__BaseUrl}/accept/{token}`);
  svarsformen (inkl. token) är oförändrad och mejlfel stoppar inte inbjudan.
- **FR-004**: Ingen ändring av lyckad registrering (auto-login kvar) eller accept-flödet.

## Success Criteria
- **SC-001**: N:e försöket mot upptagen adress ⇒ 429; varningsmejl gick till adressen (test).
- **SC-002**: Inbjudan ⇒ mejl till den inbjudna vars innehåll bär accept-länken med token (test).
- **SC-003**: Mejlfel vid inbjudan ⇒ inbjudan skapas ändå (test).

## Out of Scope
Per-IP-broms (kräver distribuerad store — dokumenterad framtida förbättring), e-postverifiering
av nya konton, glömt lösenord (B-listan).
