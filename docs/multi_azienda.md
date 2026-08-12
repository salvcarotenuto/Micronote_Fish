# Multi azienda Micronote Food

Il modello logico segue Colf24.

## Database

- Database master: `mn_master`
- Database aziendali: `mn_0001`, `mn_0002`, ...
- L'azienda di debug corrente diventa azienda `0001`.
- Il database attuale di debug (`MicronoteDb`) deve essere duplicato/rinominato in `mn_0001` quando attiviamo il multi-azienda.

## Tabelle master

### Aziende

Registro centrale delle aziende gestite dall'applicazione.

Campi base:

- `Codice`: codice numerico azienda, 1..9999.
- `Nome`: nome azienda usato nella prima fase di login.
- `Password`: password azienda usata nella prima fase di login.
- `Attiva`: abilita/disabilita azienda.
- `Bloccata`: blocco amministrativo.
- `NomeDatabase`: database aziendale. Se vuoto viene calcolato da `Codice`, es. `mn_0001`.
- `VersioneDbAttuale`: data e ora della versione schema aziendale corrente.
- `VersioneDbRichiesta`: data e ora della versione schema richiesta dall'applicazione.

### Parametri

Parametri generali dell'applicazione, validi prima della scelta dell'azienda.
Le `Opzioni` restano invece nel database aziendale.

La chiave `VersioneSchemaDatabase` usa il campo omonimo di tipo `DATETIME` e
contiene la versione globale dello stampo. Nell'interfaccia viene visualizzata
nel formato `yyyy-MM-dd HH:mm:ss`.

Quando cambia la struttura dello stampo:

1. viene aggiornata `Parametri.VersioneSchemaDatabase`;
2. tutte le aziende ricevono la stessa `VersioneDbRichiesta`;
3. dopo l'aggiornamento riuscito di un database aziendale, la relativa
   `VersioneDbAttuale` viene portata alla versione richiesta;
4. un nuovo database aziendale nasce con versione attuale e richiesta uguali
   alla versione globale.

## Credenziali

Accesso azienda:

1. Nome azienda + password azienda, letti da `mn_master.Aziende`.
2. Username + password utente, letti dal database aziendale selezionato (`Utenti`).

Accesso amministratore software:

- Password amministratore applicazione.
- Consente gestione aziende e parametri app nel database master.
- Non coincide con utenti aziendali e non dipende da una azienda selezionata.

## Cartelle dati

Default locale:

`C:\ProgramData\MicronoteFood\Aziende\0001\...`

La radice può essere configurata con `Micronote:DataRoot`.


## Configurazione amministratore

La password dell'amministratore software e' letta da Micronote:Admin:Password oppure dalla variabile ambiente MICRONOTE_APP_ADMIN_PASSWORD, equivalente al parametro pplication_admin_password di Colf24.

