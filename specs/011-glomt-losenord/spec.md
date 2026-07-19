# Feature Specification: Glömt lösenord

**Feature Branch**: `feature/011-glomt-losenord` · **Created**: 2026-07-06 · **Status**: Draft

## Översikt

Självbetjänad lösenordsåterställning via e-post, byggd enumereringssäkert från start:
"Glömt lösenord?" på inloggningen → ange e-post → **alltid samma generiska svar** →
finns kontot mejlas en **engångslänk** (1 h giltighet) → ny lösenordssida → vid lyckat byte
**återkallas alla användarens refresh-tokens** (stulna sessioner dör).

## User Stories
### US1 — Begär återställning (P1)
**Given** en användare anger sin e-post, **Then** svaras alltid generiskt ("Om kontot finns har
ett mejl skickats") oavsett om adressen finns; finns kontot mejlas en återställningslänk.
Upprepade begäranden per adress bromsas tyst (svaret förblir generiskt, inget mejl skickas).

### US2 — Sätt nytt lösenord (P1)
**Given** en giltig, oanvänd länk inom giltighetstiden, **When** användaren sätter ett nytt
lösenord (policy från 001), **Then** byts lösenordet, länken förbrukas (engångs) och alla
användarens refresh-tokens återkallas. Ogiltig/förbrukad/utgången länk ⇒ generiskt fel.

## Requirements
- **FR-001**: `POST /api/auth/forgot-password` svarar alltid 202 med samma kropp; mejl skickas
  endast om kontot finns och adressen inte är bromsad ("forgot:"-nyckel i throttlen).
- **FR-002**: Återställningstoken lagras endast hashad, gäller 1 h, är engångs.
- **FR-003**: `POST /api/auth/reset-password` validerar lösenordspolicyn, byter lösenord,
  förbrukar token och återkallar användarens samtliga refresh-tokens.
- **FR-004**: Ogiltig token ger generiskt fel utan detaljer. Mejlfel sväljs och loggas.

## Success Criteria
- **SC-001**: Okänd adress ⇒ 202 utan mejl; känd adress ⇒ 202 + mejl med reset-länk (test).
- **SC-002**: Länken fungerar exakt en gång; nytt lösenord loggar in, gammalt nekas (test).
- **SC-003**: Befintlig refresh-token är död efter byte (test).
- **SC-004**: Bromsad adress ⇒ 202 utan ytterligare mejl (test).

## Out of Scope
Lösenordsbyte inloggad (profilsida), 2FA, magic links för inloggning.
