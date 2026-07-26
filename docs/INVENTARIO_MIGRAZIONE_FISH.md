# Micronote Fish - Inventario e matrice iniziale di migrazione

Data prima ricognizione: 25 luglio 2026

## Obiettivo

Micronote Fish nasce dalla base ASP.NET Core/C# di Micronote Food e utilizza MySQL
in configurazione multi-aziendale e multi-database.

La legacy VB.NET con SQL Server CE è una fonte di regole funzionali, flussi e dati,
ma non costituisce il modello strutturale del nuovo database.

## Principio vincolante per i dati

La struttura di destinazione è quella moderna di Micronote Master e dei database
aziendali derivati da Micronote Food.

- Le chiavi tecniche generate dal database usano `AUTO_INCREMENT` dove previsto.
- Dopo un inserimento, le relazioni padre-figlio usano l'ID effettivamente generato
  dal database.
- Codici documento, numeratori, codici anagrafici e altri identificativi funzionali
  restano distinti dalle chiavi tecniche.
- La migrazione conserva una mappa `ID legacy -> ID nuovo` quando serve a ricostruire
  relazioni tra record.
- Non si trasferiscono automaticamente logiche legacy basate su `MAX(...) + 1`,
  ordine fisico dei record o coincidenza tra codice funzionale e chiave primaria.
- Ogni flusso legacy viene adattato alle transazioni e alle relazioni del nuovo schema.

## Sorgenti analizzate

- Base moderna: `C:\Codex\Micronote_Fish\app`
- Legacy: `C:\Sviluppo\Programmi\Micronote_Fish`
- Schema MySQL aziendale di riferimento:
  `C:\Codex\Micronote_Fish\DbBackup\db_acbb73_minote_7_19_2026_6.sql`
- Database SQL Server CE Fish di riferimento:
  `C:\Sviluppo\Programmi\Micronote_Fish\bin\Debug\Base\MicronoteDb.sdf`

Sono presenti più copie `.sdf`; questa è il campione principale iniziale perché
contiene anche i carichi. Gli archivi di altre aziende saranno campioni aggiuntivi.

## Architettura multi-aziendale

Il modello ereditato da Food prevede:

- database master attuale: `mn_master`;
- database aziendali attuali: `mn_0001`, `mn_0002`, ...;
- prima autenticazione su azienda nel master;
- seconda autenticazione su utente nel database aziendale;
- risoluzione dinamica del database aziendale selezionato.

La nomenclatura definitiva Fish deve ancora essere fissata. Una proposta coerente è:

- master: `mnfish_master`;
- aziende: `mnfish_0001`, `mnfish_0002`, ... .

La decisione sulla nomenclatura deve precedere la creazione dei primi database Fish.

## Struttura MySQL moderna già disponibile

Lo schema aziendale Food comprende, tra le altre, queste aree:

- anagrafiche: clienti, fornitori, utenti, banche, agenti;
- articoli e classificazioni: articoli, categorie, gruppi, sottogruppi, settori,
  unità di misura;
- magazzino: carico/caricorg, movimenti, causali di magazzino, punti vendita;
- vendite: vendite/venditerg;
- acquisti e fatture: fatture/fatturerg;
- contabilità: movcont/movcontrg/movcontdc, moviva/movivarg, conti, mastri,
  causali contabili;
- pagamenti e scadenze;
- tabelle fisse e fatturazione elettronica;
- opzioni e attività applicative.

Lo schema usa già `AUTO_INCREMENT` in flussi rilevanti, tra cui carichi, fatture,
movimenti contabili, movimenti di magazzino, movimenti IVA, ordini, scadenze,
titoli e vendite.

## Matrice iniziale dei moduli

| Area | Copertura Food | Evidenza legacy Fish | Classificazione iniziale |
|---|---|---|---|
| Login, utenti, cambio azienda/esercizio | Presente | Presente | Riutilizzo con adattamento nome/configurazione |
| Gestione aziende e database | Presente | Selezione ditte/database legacy | Riutilizzo diretto del modello moderno |
| Clienti e fornitori | Presente | Presente | Riutilizzo con verifica campi Fish |
| Articoli | Presente | Presente | Adattamento Fish necessario |
| Categorie, gruppi, sottogruppi, unità di misura | Presente | Presente in parte | Riutilizzo prevalente |
| Provenienza | Non presente come modulo autonomo | `FrmProvenienza` e uso nelle vendite | Nuovo modulo Fish |
| Specie | Non presente | `FrmSpecie` | Nuovo modulo Fish |
| Carichi/acquisti | Presente | Presente | Riutilizzo con adattamento colli/peso e relazioni |
| Scarichi perdite/resi | Presente | `FrmScaricoLista/Scheda` | Adattamento mirato |
| Movimenti e giacenze di magazzino | Presente | Logiche colli, peso e valore | Adattamento strutturale del flusso dati |
| Vendite | Presente | Vendita al banco e vendita plurima | Adattamento Fish rilevante |
| Vendite giornaliere e storico | Presente | `FrmAVGiorno`, `FrmAVStorico` | Riutilizzo con verifica semantica |
| Agenti e provvigioni | Schema/parziale | Moduli completi legacy | Da valutare |
| DDT, vettori e aspetto beni | Schema/parziale | Moduli legacy | Da valutare |
| Ordini e preventivi | Schema/parziale | Moduli legacy | Da valutare |
| Fatture acquisto | Presente | Presente | Riutilizzo con adattamenti limitati |
| Fatture vendita | Non completato in Food | Presente | Da implementare/adattare |
| Fatturazione elettronica | Parziale/presente | Presente | Riutilizzo e completamento |
| Prima nota e contabilità | Presente | Presente | Riutilizzo prevalente |
| Scadenze, pagamenti e titoli | Parziale | Presente | Completamento da legacy sullo schema moderno |
| Statistiche di magazzino | Presente in parte | Presente | Adattamento Fish |
| Backup/restore e manutenzione | Voci/parziale | Presente | Da riprogettare per MySQL multi-database |
| Importazione SQL Server CE | Strumenti tecnici presenti | Modulo legacy presente | Nuovo processo di migrazione controllata |

## Specificità Fish già emerse

La legacy mostra almeno queste esigenze settoriali:

- classificazione per provenienza;
- classificazione per specie;
- movimentazione e giacenza espresse anche in colli e peso;
- valorizzazione separata delle quantità fisiche e del valore;
- vendita al banco;
- vendita plurima;
- ricerca/selezione articoli per provenienza;
- causali di scarico articolate.

Queste indicazioni sono preliminari e devono essere verificate sullo schema dello
`.sdf` e sui flussi completi dei form legacy.

## Prima estrazione dal campione legacy

Il campione principale misura 36.896.768 byte e contiene 72 tabelle.

Numerosità particolarmente utili:

- `Carico`: 5.604 testate;
- `CaricoRg`: 15.701 righe;
- `Movimenti`: 83.456 righe;
- `Vendite`: 19.575 testate;
- `VenditeRg`: 67.401 righe;
- `MovCassa`: 14.144 righe;
- `Clienti`: 486;
- `Fornitori`: 84;
- `Articoli`: 63.

Il flusso legacy dei carichi collega testata e righe mediante la chiave logica
`Anno + Settore + Codice`; le righe aggiungono `Riga`. La struttura MySQL moderna
introduce invece un ID tecnico `AUTO_INCREMENT` sulla testata. La migrazione dovrà
quindi:

1. inserire o identificare la testata moderna;
2. acquisire il nuovo ID generato;
3. collegare le righe mediante tale ID;
4. conservare in una tabella di mapping la chiave legacy
   `Anno + Settore + Codice` quando necessaria per verifiche e riconciliazione.

Le righe Fish confermano i dati settoriali `Colli`, `PesoLr`, `Tara`, `PesoNt`,
`Prezzo`, `PrNetto`, `PrIvato` e `Importo`. Questi dati dovranno essere preservati
o rappresentati esplicitamente nel nuovo schema, senza comprimerli in una sola
quantità generica.

## Regole per la migrazione dei flussi

Per ogni modulo verrà prodotta una scheda con:

1. obiettivo funzionale;
2. form e classi legacy coinvolte;
3. pagina, repository, modello e JavaScript Food riutilizzabili;
4. tabelle legacy lette o scritte;
5. tabelle MySQL di destinazione;
6. mapping di campi e conversioni;
7. strategia per ID generati e relazioni;
8. confini transazionali;
9. regole di validazione;
10. test di accettazione e regressione.

## Prossime attività

1. Confrontare le tabelle Fish con lo schema aziendale MySQL moderno.
2. Definire il mapping dettagliato di `Carico`, `CaricoRg` e `Movimenti`.
3. Definire la nomenclatura definitiva dei database Fish.
4. Scegliere il primo modulo pilota.

Il modulo pilota consigliato è **Provenienze**, perché è specifico Fish, piccolo,
usato dall'anagrafica articoli e dai flussi di vendita, e permette di validare
l'estensione dello schema moderno senza coinvolgere subito transazioni complesse.
