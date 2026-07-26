# Inventario SQL Server CE

- Archivio: `C:\Sviluppo\Programmi\Micronote_Fish\bin\Debug\Base\MicronoteDb.sdf`
- Dimensione: 36,896,768 byte

## Tabelle e numerosità

| Tabella | Righe |
|---|---:|
| Agenti | 0 |
| Articoli | 63 |
| Aspetto | 3 |
| Attivita | 0 |
| Aziende | 0 |
| Banche | 1 |
| Carico | 5604 |
| CaricoRg | 15701 |
| CatClienti | 3 |
| Categorie | 32 |
| CausaliCont | 24 |
| CausaliDoc | 19 |
| CausaliMag | 20 |
| Clienti | 486 |
| Codiva | 36 |
| Comuni | 8089 |
| Ddt | 0 |
| DdtRg | 0 |
| Distinte | 0 |
| DistinteMp | 0 |
| Ditte | 579 |
| Fatture | 0 |
| FattureRg | 0 |
| FeCassePrev | 22 |
| FeCausalePag | 24 |
| FeCausRit | 24 |
| FeCodiciIva | 24 |
| FeCondPag | 3 |
| FeFormato | 2 |
| FeModoPag | 23 |
| FeRegimiF | 18 |
| FeTipoDoc | 18 |
| FeTipoRit | 6 |
| Fornitori | 84 |
| Gruppi | 0 |
| Lavori | 0 |
| LavoriRg | 0 |
| MovCassa | 14144 |
| MovCont | 0 |
| Movimenti | 83456 |
| MovIva | 0 |
| MovivaRg | 0 |
| NaturaGiu | 52 |
| Nazioni | 249 |
| Opzioni | 122 |
| Ordini | 0 |
| OrdiniRg | 0 |
| Pagamenti | 12 |
| Params | 38 |
| Preventivi | 0 |
| PreventiviRg | 0 |
| Progressivi | 9 |
| Provenienza | 0 |
| Province | 106 |
| Provvigioni | 0 |
| ProvvigioniRg | 0 |
| PuntiVendita | 0 |
| Ricavi | 0 |
| RicaviRg | 0 |
| SaldoIniCf | 570 |
| Scadenze | 0 |
| Specie | 1 |
| TipoMovimenti | 21 |
| TipoPagamenti | 17 |
| TipoTitoli | 8 |
| Titoli | 0 |
| UMisura | 5 |
| Utenti | 1 |
| Vendite | 19575 |
| VenditeRg | 67401 |
| VenditeTmp | 0 |
| Vettori | 0 |

## Colonne

### Agenti

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Codice | int |  | YES | 0 |
| 2 | Nome | nvarchar | 100 | YES | '' |
| 3 | Codfi | nvarchar | 20 | YES | '' |
| 4 | Piva | nvarchar | 20 | YES | '' |
| 5 | Citta | nvarchar | 100 | YES | '' |
| 6 | Cap | nvarchar | 5 | YES | '' |
| 7 | Provincia | nvarchar | 2 | YES | '' |
| 8 | Via | nvarchar | 100 | YES | '' |
| 9 | Civico | nvarchar | 15 | YES | '' |
| 10 | Contatto | nvarchar | 100 | YES | '' |
| 11 | Telefono1 | nvarchar | 20 | YES | '' |
| 12 | Telefono2 | nvarchar | 20 | YES | '' |
| 13 | Email | nvarchar | 100 | YES | '' |
| 14 | Distretto | smallint |  | YES | 0 |
| 15 | Provvigione | real |  | YES | 0 |
| 16 | Pagamento | smallint |  | YES | 0 |
| 17 | Zona | nvarchar | 100 | YES | '' |
| 18 | Banca | int |  | YES | 0 |
| 19 | Iban | nvarchar | 50 | YES | '' |
| 20 | Attivo | tinyint |  | YES | 0 |
| 21 | DataUliq | datetime |  | YES |  |
| 22 | Pec | nvarchar | 100 | YES | '' |
| 23 | Notes | nvarchar | 255 | YES | '' |
| 24 | SaldoIni | real |  | YES | 0  |

### Articoli

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Codice | nvarchar | 30 | YES | '' |
| 2 | Descrizione | nvarchar | 255 | YES | '' |
| 3 | Categoria | smallint |  | YES | 0 |
| 4 | Specie | smallint |  | YES | 0 |
| 5 | Ums | nvarchar | 20 | YES | '' |
| 6 | Umv | nvarchar | 20 | YES | '' |
| 7 | PrezzoStd | real |  | YES | 0 |
| 8 | Provenienza | smallint |  | YES | 0 |
| 9 | Tara | real |  | YES | 0 |
| 10 | GiacinC | int |  | YES | 0 |
| 11 | GiacinP | real |  | YES | 0 |
| 12 | ScortaMinC | int |  | YES | 0 |
| 13 | ScortaMinP | real |  | YES | 0 |
| 14 | CostoStd | real |  | YES | 0 |
| 15 | Prezzo2 | real |  | YES | 0 |
| 16 | PrIvato | real |  | YES | 0 |
| 17 | Prezzo3 | real |  | YES | 0 |
| 18 | AliqIva | real |  | YES | 0 |

### Aspetto

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Codice | smallint |  | YES | 0 |
| 2 | Descrizione | nvarchar | 100 | YES | '' |

### Attivita

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | TheTime | datetime |  | YES |  |
| 2 | Azienda | smallint |  | YES | 0 |
| 3 | Utente | smallint |  | YES | 0 |
| 4 | Tabella | nvarchar | 50 | YES | '' |
| 5 | Anno | smallint |  | YES | 0 |
| 6 | Settore | tinyint |  | YES | 0 |
| 7 | Codice | nvarchar | 30 | YES | '' |
| 8 | Azione | tinyint |  | YES | 0 |

### Aziende

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Codice | smallint |  | YES | 0 |
| 2 | Nome | nvarchar | 250 | YES | '' |

### Banche

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Codice | int |  | YES | 0 |
| 2 | Nome | nvarchar | 100 | YES | '' |
| 3 | Agenzia | nvarchar | 50 | YES | '' |
| 4 | Abi | nvarchar | 5 | YES | '' |
| 5 | Cab | nvarchar | 5 | YES | '' |
| 6 | Conto | nvarchar | 25 | YES | '' |
| 7 | Iban | nvarchar | 40 | YES | '' |
| 8 | Telefono1 | nvarchar | 20 | YES | '' |
| 9 | Telefono2 | nvarchar | 20 | YES | '' |
| 10 | Email | nvarchar | 100 | YES | '' |
| 11 | SitoWeb | nvarchar | 100 | YES | '' |
| 12 | Swift | nvarchar | 12 | YES | '' |
| 13 | Sia | nvarchar | 10 | YES | '' |
| 14 | Notes | nvarchar | 255 | YES | '' |

### Carico

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Anno | smallint |  | YES | 0 |
| 2 | Settore | tinyint |  | YES | 0 |
| 3 | Codice | int |  | YES | 0 |
| 4 | NumDoc | nvarchar | 25 | YES | '' |
| 5 | DataDoc | datetime |  | YES | 0 |
| 6 | Fornitore | int |  | YES | 0 |
| 7 | TipoMag | tinyint |  | YES | 0 |
| 8 | Merce | real |  | YES | 0 |
| 9 | Iva | real |  | YES | 0 |
| 10 | Pagato | real |  | YES | 0 |
| 11 | Totale | real |  | YES | 0 |

### CaricoRg

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Anno | smallint |  | YES | 0 |
| 2 | Settore | tinyint |  | YES | 0 |
| 3 | Codice | int |  | YES | 0 |
| 4 | Riga | smallint |  | YES | 0 |
| 5 | Fornitore | int |  | YES | 0 |
| 6 | DataDoc | datetime |  | YES | 0 |
| 7 | Articolo | nvarchar | 30 | YES | '' |
| 8 | Ums | nvarchar | 10 | YES | '' |
| 9 | Mag | tinyint |  | YES | 0 |
| 10 | Colli | real |  | YES | 0 |
| 11 | PesoLr | real |  | YES | 0 |
| 12 | Tara | real |  | YES | 0 |
| 13 | PesoNt | real |  | YES | 0 |
| 14 | Prezzo | real |  | YES | 0 |
| 15 | Sconto | real |  | YES | 0 |
| 16 | Iva | real |  | YES | 0 |
| 17 | PrNetto | real |  | YES | 0 |
| 18 | PrIvato | real |  | YES | 0 |
| 19 | Importo | real |  | YES | 0 |

### CatClienti

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Codice | smallint |  | YES | 0 |
| 2 | Descrizione | nvarchar | 100 | YES | '' |

### Categorie

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Codice | smallint |  | YES | 0 |
| 2 | Descrizione | nvarchar | 100 | YES | '' |

### CausaliCont

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Codice | smallint |  | YES | 0 |
| 2 | Descrizione | nvarchar | 100 | YES | '' |
| 3 | Tipo | nvarchar | 1 | YES | '' |
| 4 | Locked | tinyint |  | YES | 0 |
| 5 | Ditta | nvarchar | 1 | YES | ''  |

### CausaliDoc

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Codice | smallint |  | YES | 0 |
| 2 | Descrizione | nvarchar | 100 | YES | '' |

### CausaliMag

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Codice | smallint |  | YES | 0 |
| 2 | Descrizione | nvarchar | 100 | YES | '' |
| 3 | Mov | nvarchar | 1 | YES | '' |
| 4 | CliFor | nvarchar | 1 | YES | '' |

### Clienti

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Codice | int |  | YES | 0 |
| 2 | Nome | nvarchar | 250 | YES | '' |
| 3 | CodFi | nvarchar | 20 | YES | '' |
| 4 | Piva | nvarchar | 20 | YES | '' |
| 5 | Citta | nvarchar | 100 | YES | '' |
| 6 | Cap | nvarchar | 5 | YES | '' |
| 7 | Provincia | nvarchar | 2 | YES | '' |
| 8 | Via | nvarchar | 100 | YES | '' |
| 9 | Telefono1 | nvarchar | 30 | YES | '' |
| 10 | Telefono2 | nvarchar | 30 | YES | '' |
| 11 | Email | nvarchar | 50 | YES | '' |
| 12 | Pec | nvarchar | 60 | YES | '' |
| 13 | Attivo | tinyint |  | YES | 0 |
| 14 | Aliquota | real |  | YES | 0 |
| 15 | Listino | tinyint |  | YES | 0 |
| 16 | Sconto | real |  | YES | 0 |
| 17 | Pagamento | smallint |  | YES | 0 |
| 18 | Fido | float |  | YES | 0 |
| 19 | SaldoIni | float |  | YES | 0 |
| 20 | Categoria | smallint |  | YES | 0 |
| 21 | PuntoV | tinyint |  | YES | 0 |
| 22 | Contropartita | smallint |  | YES | 0  |
| 23 | CodSdi | nvarchar | 10 | YES | ''  |
| 24 | Nazione | smallint |  | YES | 0  |
| 25 | Natura | smallint |  | YES | 0  |
| 26 | Regime | nvarchar | 10 | YES | ''  |
| 27 | Agente | smallint |  | YES | 0  |
| 28 | Banca | smallint |  | YES | 0  |

### Codiva

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Codice | nvarchar | 12 | YES | '' |
| 2 | Descrizione | nvarchar | 250 | YES | '' |
| 3 | Aliquota | real |  | YES | 0 |
| 4 | Detrazione | real |  | YES | 0 |
| 5 | FeNatura | nvarchar | 10 | YES | ''  |

### Comuni

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Nome | nvarchar | 30 | YES | '' |
| 2 | Provincia | nvarchar | 2 | YES | '' |
| 3 | Cap | nvarchar | 5 | YES | '' |
| 4 | Codice | nvarchar | 4 | YES | '' |

### Ddt

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Anno | smallint |  | YES | 0 |
| 2 | Settore | tinyint |  | YES | 0 |
| 3 | Codice | int |  | YES | 0 |
| 4 | Causale | smallint |  | YES | 0 |
| 5 | Sezione | nvarchar | 1 | YES | '' |
| 6 | Stato | tinyint |  | YES | 0 |
| 7 | NumDoc | nvarchar | 20 | YES | '' |
| 8 | DataDoc | datetime |  | YES | 0 |
| 9 | Regime | tinyint |  | YES | 0 |
| 10 | TipoMag | tinyint |  | YES | 0 |
| 11 | CliFor | nvarchar | 1 | YES | '' |
| 12 | Ditta | int |  | YES | 0 |
| 13 | Destino | nvarchar | 100 | YES | ''  |
| 14 | Merce | float |  | YES | 0 |
| 15 | DestCitta | nvarchar | 30 | YES | '' |
| 16 | Colli | smallint |  | YES | 0 |
| 17 | Peso | real |  | YES | 0 |
| 18 | DecCausale | nvarchar | 50 | YES | '' |
| 19 | Aspetto | smallint |  | YES | 0 |
| 20 | Trasporto | tinyint |  | YES | 0 |
| 21 | Agente | smallint |  | YES | 0 |
| 22 | Vettore | smallint |  | YES | 0 |
| 23 | Pagamento | smallint |  | YES | 0 |
| 24 | DataDec | datetime |  | YES | 0 |
| 25 | SpeseTra | real |  | YES | 0 |
| 26 | SpIncasso | real |  | YES | 0 |
| 27 | Imponibile | money |  | YES | 0 |
| 28 | Iva | money |  | YES | 0 |
| 29 | Totale | money |  | YES | 0 |
| 30 | DataPart | datetime |  | YES | 0 |
| 31 | OraPart | nvarchar | 5 | YES | '' |
| 32 | FatCode | int |  | YES | 0 |
| 33 | Notes | nvarchar | 255 | YES | '' |

### DdtRg

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Anno | smallint |  | YES | 0 |
| 2 | Settore | tinyint |  | YES | 0 |
| 3 | Codice | int |  | YES | 0 |
| 4 | Riga | smallint |  | YES | 0 |
| 5 | Articolo | nvarchar | 30 | YES | '' |
| 6 | Descrizione | nvarchar | 250 | YES | '' |
| 7 | TipoMag | tinyint |  | YES | 0 |
| 8 | Ums | nvarchar | 10 | YES | '' |
| 9 | Colli | real |  | YES | 0  |
| 10 | Prezzo | real |  | YES | 0 |
| 11 | Sconto | real |  | YES | 0 |
| 12 | Importo | money |  | YES | 0 |
| 13 | Peso | float |  | YES | 0 |
| 14 | CodIva | nvarchar | 10 | YES | '' |

### Distinte

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Prodotto | nvarchar | 30 | YES | '' |
| 2 | DataDoc | datetime |  | YES |  |
| 3 | OreLav | real |  | YES | 0 |
| 4 | CostoOra | real |  | YES | 0 |
| 5 | CostoLav | real |  | YES | 0 |
| 6 | CostoMpr | real |  | YES | 0 |
| 7 | Trasporto | real |  | YES | 0 |
| 8 | Imballo | real |  | YES | 0 |
| 9 | CostiGen | real |  | YES | 0 |
| 10 | CostoTotale | real |  | YES | 0 |
| 11 | PrezzoVen | real |  | YES | 0 |

### DistinteMp

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Prodotto | nvarchar | 30 | YES | '' |
| 2 | Riga | smallint |  | YES | 0 |
| 3 | Articolo | nvarchar | 30 | YES | '' |
| 4 | Consumo | real |  | YES | 0 |
| 5 | Costo | real |  | YES | 0 |
| 6 | Importo | real |  | YES | 0 |
| 7 | Incidenza | real |  | YES | 0 |

### Ditte

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | CliFor | nvarchar | 1 | YES | '' |
| 2 | Codice | int |  | YES | 0 |
| 3 | Nome | nvarchar | 255 | YES | '' |
| 4 | PuntoV | tinyint |  | YES | 0  |

### Fatture

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Anno | smallint |  | YES | 0 |
| 2 | Settore | tinyint |  | YES | 0 |
| 3 | Codice | int |  | YES | 0 |
| 4 | Causale | smallint |  | YES | 0 |
| 5 | Sezione | nvarchar | 1 | YES | '' |
| 6 | Stato | tinyint |  | YES | 0 |
| 7 | NumDoc | nvarchar | 20 | YES | '' |
| 8 | DataDoc | datetime |  | YES |  |
| 9 | Regime | tinyint |  | YES | 0 |
| 10 | CliFor | nvarchar | 1 | YES | '' |
| 11 | Ditta | int |  | YES | 0 |
| 12 | DestCitta | nvarchar | 30 | YES | '' |
| 13 | Destino | nvarchar | 100 | YES | ''  |
| 14 | Merce | float |  | YES | 0 |
| 15 | Colli | smallint |  | YES | 0 |
| 16 | Peso | real |  | YES | 0 |
| 17 | DecCausale | nvarchar | 50 | YES | '' |
| 18 | Aspetto | smallint |  | YES | 0 |
| 19 | Trasporto | tinyint |  | YES | 0 |
| 20 | Agente | smallint |  | YES | 0 |
| 21 | Vettore | smallint |  | YES | 0 |
| 22 | Pagamento | smallint |  | YES | 0 |
| 23 | DataDec | datetime |  | YES |  |
| 24 | SpeseTra | real |  | YES | 0 |
| 25 | SpIncasso | real |  | YES | 0 |
| 26 | Imponibile | money |  | YES | 0 |
| 27 | Iva | money |  | YES | 0 |
| 28 | Totale | money |  | YES | 0 |
| 29 | DataPart | datetime |  | YES |  |
| 30 | OraPart | nvarchar | 5 | YES | '' |
| 31 | Notes | nvarchar | 255 | YES | '' |
| 32 | FeSeriale | nvarchar | 6 | YES | '' |
| 33 | FileName | nvarchar | 25 | YES | '' |

### FattureRg

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Anno | smallint |  | YES | 0 |
| 2 | Settore | tinyint |  | YES | 0 |
| 3 | Codice | int |  | YES | 0 |
| 4 | Riga | smallint |  | YES | 0 |
| 5 | Articolo | nvarchar | 30 | YES | '' |
| 6 | Descrizione | nvarchar | 250 | YES | '' |
| 7 | TipoMag | tinyint |  | YES | 0 |
| 8 | Ums | nvarchar | 10 | YES | '' |
| 9 | Colli | real |  | YES | 0  |
| 10 | Prezzo | real |  | YES | 0 |
| 11 | Sconto | real |  | YES | 0 |
| 12 | Importo | money |  | YES | 0 |
| 13 | Peso | float |  | YES | 0 |
| 14 | CodIva | nvarchar | 10 | YES | '' |

### FeCassePrev

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Codice | nvarchar | 12 | YES | '' |
| 2 | Descrizione | nvarchar | 250 | YES | '' |

### FeCausalePag

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Codice | nvarchar | 10 | YES | '' |
| 2 | Descrizione | nvarchar | 4000 | YES | '' |

### FeCausRit

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Codice | nvarchar | 10 | YES | '' |
| 2 | Descrizione | nvarchar | 255 | YES | '' |

### FeCodiciIva

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Codice | nvarchar | 12 | YES | '' |
| 2 | Descrizione | nvarchar | 250 | YES | '' |

### FeCondPag

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Codice | nvarchar | 12 | YES | '' |
| 2 | Descrizione | nvarchar | 255 | YES | '' |

### FeFormato

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Codice | nvarchar | 12 | YES | '' |
| 2 | Descrizione | nvarchar | 250 | YES | '' |

### FeModoPag

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Codice | nvarchar | 10 | YES | '' |
| 2 | Descrizione | nvarchar | 4000 | YES | '' |

### FeRegimiF

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Codice | nvarchar | 12 | YES | '' |
| 2 | Descrizione | nvarchar | 250 | YES | '' |

### FeTipoDoc

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Codice | nvarchar | 12 | YES | '' |
| 2 | Descrizione | nvarchar | 250 | YES | '' |

### FeTipoRit

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Codice | nvarchar | 12 | YES | '' |
| 2 | Descrizione | nvarchar | 250 | YES | '' |

### Fornitori

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Codice | int |  | YES | 0 |
| 2 | Nome | nvarchar | 250 | YES | '' |
| 3 | Codfi | nvarchar | 20 | YES | '' |
| 4 | Piva | nvarchar | 20 | YES | '' |
| 5 | Citta | nvarchar | 100 | YES | '' |
| 6 | Cap | nvarchar | 5 | YES | '' |
| 7 | Provincia | nvarchar | 2 | YES | '' |
| 8 | Via | nvarchar | 100 | YES | '' |
| 9 | Telefono1 | nvarchar | 20 | YES | '' |
| 10 | Telefono2 | nvarchar | 20 | YES | '' |
| 11 | Email | nvarchar | 100 | YES | '' |
| 12 | Pec | nvarchar | 100 | YES | '' |
| 13 | Attivo | tinyint |  | YES | 0 |
| 14 | Aliquota | real |  | YES | 0 |
| 15 | Sconto | real |  | YES | 0 |
| 16 | Listino | tinyint |  | YES | 0 |
| 17 | Pagamento | smallint |  | YES | 0 |
| 18 | Categoria | smallint |  | YES | 0 |
| 19 | Fido | real |  | YES | 0 |
| 20 | SaldoIni | real |  | YES | 0 |
| 21 | Contropartita | smallint |  | YES | 0  |
| 22 | PuntoV | smallint |  | YES | 0  |
| 23 | CodSdi | nvarchar | 10 | YES | ''  |
| 24 | Nazione | smallint |  | YES | 0  |
| 25 | Natura | smallint |  | YES | 0  |
| 26 | Regime | nvarchar | 10 | YES | ''  |
| 27 | Agente | smallint |  | YES | 0  |
| 28 | Banca | smallint |  | YES | 0  |

### Gruppi

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Codice | smallint |  | YES | 0 |
| 2 | Descrizione | nvarchar | 100 | YES | '' |

### Lavori

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Anno | smallint |  | YES | 0 |
| 2 | Codice | int |  | YES | 0 |
| 3 | DataReg | datetime |  | YES |  |
| 4 | DataLav | datetime |  | YES |  |
| 5 | OraLav | nvarchar | 5 | YES | '' |
| 6 | XDataLav | datetime |  | YES |  |
| 7 | Cliente | int |  | YES | 0 |
| 8 | Incaricato | smallint |  | YES | 0 |
| 9 | Descrizione | nvarchar | 255 | YES | '' |
| 10 | Attivita | nvarchar | 255 | YES | '' |
| 11 | PrezzoLav | real |  | YES | 0 |
| 12 | PrezzoMpr | real |  | YES | 0 |
| 13 | PrezzoTot | real |  | YES | 0 |
| 14 | PrezzoRic | real |  | YES | 0 |
| 15 | PrezzoInc | real |  | YES | 0 |
| 16 | Eseguito | tinyint |  | YES | 0 |
| 17 | Esito | tinyint |  | YES | 0 |

### LavoriRg

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Anno | smallint |  | YES | 0 |
| 2 | Codice | int |  | YES | 0 |
| 3 | Riga | tinyint |  | YES | 0 |
| 4 | Articolo | nvarchar | 25 | YES | '' |
| 5 | Quantita | real |  | YES | 0 |
| 6 | Prezzo | real |  | YES | 0 |

### MovCassa

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Anno | smallint |  | YES | 0 |
| 2 | Settore | tinyint |  | YES | 0 |
| 3 | Codice | int |  | YES | 0 |
| 4 | DataMov | datetime |  | YES | 0 |
| 5 | Causale | smallint |  | YES | 0  |
| 6 | TipoMov | nvarchar | 1 | YES | '' |
| 7 | CliFor | nvarchar | 1 | YES | '' |
| 8 | Ditta | int |  | YES | 0 |
| 9 | Importo | float |  | YES | 0  |
| 10 | ModoPag | tinyint |  | YES | 0 |
| 11 | NumDoc | nvarchar | 20 | YES | '' |
| 12 | DataDoc | datetime |  | YES | 0 |
| 13 | Descrizione | nvarchar | 100 | YES | '' |

### MovCont

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Anno | smallint |  | YES | 0 |
| 2 | Settore | tinyint |  | YES | 0 |
| 3 | Codice | int |  | YES | 0 |
| 4 | Causale | smallint |  | YES | 0 |
| 5 | Protocollo | int |  | YES | 0 |
| 6 | DataReg | datetime |  | YES |  |
| 7 | DataMov | datetime |  | YES |  |
| 8 | NumDoc | nvarchar | 20 | YES | '' |
| 9 | Descrizione | nvarchar | 100 | YES | '' |
| 10 | CliFor | nvarchar | 1 | YES | '' |
| 11 | Ditta | int |  | YES | 0 |
| 12 | Importo | money |  | YES | 0 |
| 13 | Segno | nvarchar | 1 | YES | '' |
| 14 | TipoMov | tinyint |  | YES | 0 |
| 15 | Fattura | nvarchar | 22 | YES | ''  |
| 16 | Scadenza | nvarchar | 22 | YES | ''  |
| 17 | TipoTitolo | tinyint |  | YES | 0  |
| 18 | NumTitolo | nvarchar | 25 | YES | ''  |
| 19 | ScadTitolo | datetime |  | YES |  |
| 20 | Banca | smallint |  | YES | 0  |

### Movimenti

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Anno | smallint |  | YES | 0 |
| 2 | Settore | tinyint |  | YES | 0 |
| 3 | Codice | int |  | YES | 0 |
| 4 | Riga | smallint |  | YES | 0 |
| 5 | Causale | smallint |  | YES | 0 |
| 6 | NumDoc | nvarchar | 20 | YES | '' |
| 7 | DataMov | datetime |  | YES | 0 |
| 8 | TipoMag | tinyint |  | YES | 0 |
| 9 | TipoMov | nvarchar | 1 | YES | '' |
| 10 | Articolo | nvarchar | 25 | YES | '' |
| 11 | Mag | tinyint |  | YES | 0 |
| 12 | Fornitore | int |  | YES | 0 |
| 13 | CliFor | nvarchar | 1 | YES | '' |
| 14 | Ditta | int |  | YES | 0 |
| 15 | Colli | real |  | YES | 0 |
| 16 | Peso | real |  | YES | 0 |
| 17 | Prezzo | money |  | YES | 0 |
| 18 | Importo | money |  | YES | 0 |
| 19 | PuntoV | tinyint |  | YES | 0  |

### MovIva

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Anno | smallint |  | YES | 0 |
| 2 | Settore | tinyint |  | YES | 0 |
| 3 | Codice | int |  | YES | 0 |
| 4 | Causale | smallint |  | YES | 0 |
| 5 | Sezione | nvarchar | 1 | YES | '' |
| 6 | DataReg | datetime |  | YES | 0 |
| 7 | Numero | nvarchar | 20 | YES | '' |
| 8 | DataDoc | datetime |  | YES | 0 |
| 9 | Protocollo | int |  | YES | 0 |
| 10 | Regime | tinyint |  | YES | 0 |
| 11 | Segno | tinyint |  | YES | 0 |
| 12 | CliFor | nvarchar | 1 | YES | '' |
| 13 | Ditta | int |  | YES | 0 |
| 14 | CtPartita | smallint |  | YES | 0 |
| 15 | Pagata | tinyint |  | YES | 0 |
| 16 | Pagamento | nvarchar | 20 | YES | '' |
| 17 | Banca | smallint |  | YES | 0 |
| 18 | Imponibile | float |  | YES | 0 |
| 19 | Iva | float |  | YES | 0 |
| 20 | Totale | float |  | YES | 0 |
| 21 | FeName | nvarchar | 50 | YES | '' |

### MovivaRg

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Anno | smallint |  | YES | 0 |
| 2 | Settore | tinyint |  | YES | 0 |
| 3 | Codice | int |  | YES | 0 |
| 4 | CodIva | nvarchar | 12 | YES | '' |
| 5 | AlqIva | real |  | YES | 0 |
| 6 | Imponibile | real |  | YES | 0 |
| 7 | Iva | real |  | YES | 0 |

### NaturaGiu

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Codice | smallint |  | YES | 0 |
| 2 | Descrizione | nvarchar | 250 | YES | '' |

### Nazioni

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Codice | smallint |  | YES | 0 |
| 2 | Nome | nvarchar | 100 | YES | '' |
| 3 | Sigla2 | nvarchar | 5 | YES | '' |
| 4 | Sigla3 | nvarchar | 5 | YES | '' |
| 5 | Iso | nvarchar | 5 | YES | '' |
| 6 | Codfi | nvarchar | 5 | YES | '' |
| 7 | Zona | nvarchar | 15 | YES | '' |
| 8 | Regime | nvarchar | 10 | YES | '' |

### Opzioni

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Nome | nvarchar | 255 | YES | '' |
| 2 | Utente | smallint |  | YES | 0 |
| 3 | Valore | nvarchar | 255 | YES | '' |
| 4 | Tipo | tinyint |  | YES | 0 |

### Ordini

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Anno | smallint |  | YES | 0 |
| 2 | Settore | tinyint |  | YES | 0 |
| 3 | Codice | int |  | YES | 0 |
| 4 | Causale | smallint |  | YES | 0 |
| 5 | TipoDoc | tinyint |  | YES | 0 |
| 6 | NumDoc | nvarchar | 25 | YES | '' |
| 7 | DataDoc | datetime |  | YES |  |
| 8 | CliFor | nvarchar | 1 | YES | '' |
| 9 | Ditta | int |  | YES | 0 |
| 10 | ULocale | smallint |  | YES | 0 |
| 11 | NumFattura | nvarchar | 25 | YES | '' |
| 12 | DataFattura | datetime |  | YES |  |
| 13 | Totale | float |  | YES | 0 |
| 14 | Ordine | nvarchar | 20 | YES | '' |
| 15 | Notes | nvarchar | 255 | YES | '' |

### OrdiniRg

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Anno | smallint |  | YES | 0 |
| 2 | Settore | tinyint |  | YES | 0 |
| 3 | Codice | int |  | YES | 0 |
| 4 | IdRiga | nvarchar | 20 | YES | '' |
| 5 | Riga | smallint |  | YES | 0 |
| 6 | Articolo | nvarchar | 30 | YES | '' |
| 7 | Lotto | nvarchar | 30 | YES | '' |
| 8 | Scadenza | datetime |  | YES |  |
| 9 | Um | nvarchar | 10 | YES | '' |
| 10 | Quantita | real |  | YES | 0 |
| 11 | Costo | real |  | YES | 0 |
| 12 | Sconto | real |  | YES | 0 |
| 13 | CstNetto | real |  | YES | 0 |
| 14 | Importo | real |  | YES | 0 |

### Pagamenti

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Codice | smallint |  | YES | 0 |
| 2 | Descrizione | nvarchar | 100 | YES | '' |
| 3 | Sigla | nvarchar | 1 | YES | '' |
| 4 | TipoPagamento | tinyint |  | YES | 0 |
| 5 | TipoTitolo | tinyint |  | YES | 0 |
| 6 | NumScadenze | tinyint |  | YES | 0 |
| 7 | PrimoInterv | smallint |  | YES | 0 |
| 8 | Intervallo | smallint |  | YES | 0 |
| 9 | TimeOffset | smallint |  | YES | 0 |
| 10 | SkipAgo | tinyint |  | YES | 0 |
| 11 | SkipDic | tinyint |  | YES | 0 |
| 12 | Spese | real |  | YES | 0 |
| 13 | Decorrenza | tinyint |  | YES | 0 |
| 14 | Condizioni | nvarchar | 10 | YES | '' |
| 15 | Modalita | nvarchar | 10 | YES | '' |

### Params

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Nome | nvarchar | 255 | YES | '' |
| 2 | Valore | nvarchar | 255 | YES | '' |

### Preventivi

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Anno | smallint |  | YES | 0 |
| 2 | Settore | tinyint |  | YES | 0 |
| 3 | Codice | int |  | YES | 0 |
| 4 | Sezione | nvarchar | 1 | YES | '' |
| 5 | Causale | smallint |  | YES | 0 |
| 6 | Stato | tinyint |  | YES | 0 |
| 7 | Chiusa | tinyint |  | YES | 0 |
| 8 | Chiusura | nvarchar | 15 | YES | '' |
| 9 | NumDoc | nvarchar | 20 | YES | '' |
| 10 | DataDoc | datetime |  | YES |  |
| 11 | Regime | tinyint |  | YES | 0 |
| 12 | CliFor | nvarchar | 1 | YES | '' |
| 13 | Ditta | int |  | YES | 0 |
| 14 | Destino | smallint |  | YES | 0 |
| 15 | ULocale | smallint |  | YES | 0 |
| 16 | Colli | smallint |  | YES | 0 |
| 17 | Peso | real |  | YES | 0 |
| 18 | Pezzi | smallint |  | YES | 0 |
| 19 | DecCausale | nvarchar | 50 | YES | '' |
| 20 | Aspetto | smallint |  | YES | 0 |
| 21 | Trasporto | tinyint |  | YES | 0 |
| 22 | Agente | smallint |  | YES | 0 |
| 23 | Vettore | smallint |  | YES | 0 |
| 24 | Pagamento | smallint |  | YES | 0 |
| 25 | DataDec | datetime |  | YES |  |
| 26 | SpeseTra | money |  | YES | 0 |
| 27 | SpIncasso | money |  | YES | 0 |
| 28 | Provvigioni | real |  | YES | 0 |
| 29 | MerceLrd | money |  | YES | 0 |
| 30 | LavorLrd | money |  | YES | 0 |
| 31 | AlqSconto | real |  | YES | 0 |
| 32 | ValSconto | money |  | YES | 0 |
| 33 | CorrsNtt | money |  | YES | 0 |
| 34 | AlqCassa | real |  | YES | 0 |
| 35 | ValCassa | real |  | YES | 0 |
| 36 | AlqIrpef | real |  | YES | 0 |
| 37 | ValIrpef | money |  | YES | 0 |
| 38 | Imponibile | money |  | YES | 0 |
| 39 | Iva | money |  | YES | 0 |
| 40 | Totale | money |  | YES | 0 |
| 41 | NettoPag | money |  | YES | 0 |
| 42 | DataPart | datetime |  | YES |  |
| 43 | OraPart | nvarchar | 5 | YES | '' |
| 44 | FatAnno | smallint |  | YES | 0 |
| 45 | FatCode | int |  | YES | 0 |
| 46 | OrdAnno | smallint |  | YES | 0 |
| 47 | OrdCode | int |  | YES | 0 |
| 48 | FeNumero | int |  | YES | 0 |
| 49 | Notes | nvarchar | 255 | YES | '' |

### PreventiviRg

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Anno | smallint |  | YES | 0 |
| 2 | Settore | tinyint |  | YES | 0 |
| 3 | Codice | int |  | YES | 0 |
| 4 | Riga | smallint |  | YES | 0 |
| 5 | IdRiga | nvarchar | 20 | YES | '' |
| 6 | Articolo | nvarchar | 30 | YES | '' |
| 7 | Descrizione | nvarchar | 250 | YES | '' |
| 8 | Ums | nvarchar | 10 | YES | '' |
| 9 | Fornitore | int |  | YES | 0 |
| 10 | TipoMag | tinyint |  | YES | 0 |
| 11 | Colli | smallint |  | YES | 0 |
| 12 | PesoL | real |  | YES | 0 |
| 13 | Tara | real |  | YES | 0 |
| 14 | PesoN | real |  | YES | 0 |
| 15 | Prezzo | money |  | YES | 0 |
| 16 | Sconto | real |  | YES | 0 |
| 17 | Importo | money |  | YES | 0 |
| 18 | CodIva | nvarchar | 10 | YES | '' |

### Progressivi

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Nome | nvarchar | 50 | YES | '' |
| 2 | Anno | smallint |  | YES | 0 |
| 3 | Sezione | nvarchar | 2 | YES | '' |
| 4 | Numero | int |  | YES | 0 |
| 5 | Data | datetime |  | YES |  |

### Provenienza

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Codice | smallint |  | YES | 0 |
| 2 | Descrizione | nvarchar | 100 | YES | '' |

### Province

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Codice | nvarchar | 2 | YES | '' |
| 2 | Nome | nvarchar | 50 | YES | '' |

### Provvigioni

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Anno | smallint |  | YES | 0 |
| 2 | Settore | tinyint |  | YES | 0 |
| 3 | Codice | int |  | YES | 0 |
| 4 | DataDoc | datetime |  | YES |  |
| 5 | Agente | smallint |  | YES | 0 |
| 6 | DataLq1 | datetime |  | YES |  |
| 7 | DataLq2 | datetime |  | YES |  |
| 8 | Vendite | real |  | YES | 0 |
| 9 | Importo | real |  | YES | 0 |
| 10 | AlqIva | real |  | YES | 0 |
| 11 | ValIva | real |  | YES | 0 |
| 12 | Totale | real |  | YES | 0 |

### ProvvigioniRg

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Anno | smallint |  | YES | 0 |
| 2 | Settore | tinyint |  | YES | 0 |
| 3 | Codice | int |  | YES | 0 |
| 4 | Riga | smallint |  | YES | 0 |
| 5 | VendAnno | smallint |  | YES | 0 |
| 6 | VendCode | int |  | YES | 0 |
| 7 | VendData | datetime |  | YES |  |
| 8 | Cliente | int |  | YES | 0 |
| 9 | Venduto | real |  | YES | 0 |
| 10 | Aliquota | real |  | YES | 0 |
| 11 | Importo | real |  | YES | 0 |

### PuntiVendita

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Codice | smallint |  | YES | 0 |
| 2 | Nome | nvarchar | 100 | YES | '' |

### Ricavi

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Anno | smallint |  | YES | 0 |
| 2 | Codice | int |  | YES | 0 |
| 3 | Settimana | tinyint |  | YES | 0 |
| 4 | Fornitore | int |  | YES | 0 |
| 5 | Data1 | datetime |  | YES |  |
| 6 | Data2 | datetime |  | YES |  |

### RicaviRg

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Anno | smallint |  | YES | 0 |
| 2 | Codice | int |  | YES | 0 |
| 3 | Fornitore | int |  | YES | 0 |
| 4 | Articolo | nvarchar | 25 | YES | '' |
| 5 | DataDoc | datetime |  | YES |  |
| 6 | Settimana | tinyint |  | YES | 0 |
| 7 | Colli | real |  | YES | 0  |
| 8 | Peso | real |  | YES | 0 |
| 9 | Merce | money |  | YES | 0 |
| 10 | Prezzo | real |  | YES | 0 |
| 11 | Aliquota | real |  | YES | 0 |
| 12 | Provvigione | money |  | YES | 0 |
| 13 | Ricavo | money |  | YES | 0 |

### SaldoIniCf

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Anno | smallint |  | YES | 0 |
| 2 | CliFor | nvarchar | 1 | YES | '' |
| 3 | Ditta | int |  | YES | 0 |
| 4 | Importo | float |  | YES | 0 |

### Scadenze

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Anno | smallint |  | YES | 0 |
| 2 | Settore | tinyint |  | YES | 0 |
| 3 | Codice | int |  | YES | 0 |
| 4 | Numero | tinyint |  | YES | 0 |
| 5 | NumTitolo | nvarchar | 25 | YES | '' |
| 6 | DataScadenza | datetime |  | YES |  |
| 7 | Importo | float |  | YES | 0 |
| 8 | Pagata | tinyint |  | YES | 0 |
| 9 | Sigla | nvarchar | 1 | YES | '' |
| 10 | CodPagamento | smallint |  | YES | 0 |
| 11 | TipoPagamento | tinyint |  | YES | 0 |
| 12 | TipoTitolo | tinyint |  | YES | 0 |
| 13 | Banca | int |  | YES | 0 |
| 14 | CliFor | nvarchar | 1 | YES | '' |
| 15 | Ditta | int |  | YES | 0 |
| 16 | NumeroFatt | nvarchar | 15 | YES | '' |
| 17 | DataFatt | datetime |  | YES |  |
| 18 | TotaleFatt | float |  | YES | 0 |

### Specie

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Codice | smallint |  | YES | 0 |
| 2 | Descrizione | nvarchar | 100 | YES | '' |

### TipoMovimenti

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Codice | smallint |  | YES | 0 |
| 2 | Descrizione | nvarchar | 100 | YES | '' |
| 3 | Ditta | nvarchar | 1 | YES | '' |

### TipoPagamenti

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Codice | smallint |  | YES | 0 |
| 2 | Descrizione | nvarchar | 100 | YES | '' |
| 3 | CodiceFe | nvarchar | 10 | YES | '' |
| 4 | Sigla | nvarchar | 1 | YES | '' |
| 5 | Banca | tinyint |  | YES | 0 |
| 6 | Titolo | tinyint |  | YES | 0 |

### TipoTitoli

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Codice | smallint |  | YES | 0 |
| 2 | Descrizione | nvarchar | 100 | YES | '' |

### Titoli

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Anno | smallint |  | YES | 0 |
| 2 | Settore | tinyint |  | YES | 0 |
| 3 | Codice | int |  | YES | 0 |
| 4 | DataReg | datetime |  | YES |  |
| 5 | Tipo | tinyint |  | YES | 0 |
| 6 | Numero | nvarchar | 30 | YES | '' |
| 7 | Cliente | int |  | YES | 0 |
| 8 | Scadenza | datetime |  | YES |  |
| 9 | Importo | money |  | YES | 0 |
| 10 | Traente | nvarchar | 100 | YES | '' |
| 11 | Banca | int |  | YES | 0 |
| 12 | MovAnno | smallint |  | YES | 0 |
| 13 | MovSett | tinyint |  | YES | 0 |
| 14 | MovCode | int |  | YES | 0 |
| 15 | Girato | tinyint |  | YES | 0 |
| 16 | Giratario | nvarchar | 100 | YES | '' |
| 17 | DataGirata | datetime |  | YES |  |
| 18 | Stato | tinyint |  | YES | 0 |
| 19 | Spese | money |  | YES | 0 |
| 20 | Notes | nvarchar | 254 | YES | '' |

### UMisura

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Codice | nvarchar | 4 | YES | '' |
| 2 | Descrizione | nvarchar | 25 | YES | '' |

### Utenti

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Codice | smallint |  | YES | 0 |
| 2 | Nome | nvarchar | 255 | YES | '' |
| 3 | Cognome | nvarchar | 255 | YES | '' |
| 4 | Username | nvarchar | 255 | YES | '' |
| 5 | Passwd | nvarchar | 255 | YES | '' |
| 6 | Tipo | tinyint |  | YES | 0 |
| 7 | Attivo | tinyint |  | YES | 0 |
| 8 | Bloccato | smallint |  | YES | 0 |
| 9 | Azienda | smallint |  | YES | 0 |
| 10 | ULocale | smallint |  | YES | 0 |
| 11 | SetMenu | tinyint |  | YES | 0 |
| 12 | Telefono | nvarchar | 200 | YES | '' |
| 13 | Cellulare | nvarchar | 200 | YES | '' |
| 14 | Email | nvarchar | 255 | YES | '' |
| 15 | Pec | nvarchar | 255 | YES | '' |
| 16 | Qualifica | tinyint |  | YES | 0 |
| 17 | Sesso | nvarchar | 1 | YES | '' |
| 18 | Citta | nvarchar | 50 | YES | '' |
| 19 | Indirizzo | nvarchar | 50 | YES | '' |
| 20 | CodFi | nvarchar | 200 | YES | '' |
| 21 | Agente | smallint |  | YES | 0 |
| 22 | Cartella | nvarchar | 100 | YES | '' |
| 23 | LogIn | datetime |  | YES |  |
| 24 | LogOut | datetime |  | YES |  |
| 25 | PcName | nvarchar | 50 | YES | ''  |

### Vendite

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Anno | smallint |  | YES | 0 |
| 2 | Settore | tinyint |  | YES | 0 |
| 3 | Codice | int |  | YES | 0 |
| 4 | Stato | tinyint |  | YES | 0 |
| 5 | NumDoc | int |  | YES | 0 |
| 6 | DataDoc | datetime |  | YES | 0 |
| 7 | Cliente | int |  | YES | 0 |
| 8 | Merce | real |  | YES | 0 |
| 9 | Agente | smallint |  | YES | 0 |
| 10 | Liquidato | real |  | YES | 0 |
| 11 | Provvigione | real |  | YES | 0 |
| 12 | Iva | real |  | YES | 0 |
| 13 | Totale | real |  | YES | 0 |
| 14 | Pagato | real |  | YES | 0 |
| 15 | Abbuono | real |  | YES | 0 |
| 16 | PuntoV | tinyint |  | YES | 0  |

### VenditeRg

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Anno | smallint |  | YES | 0 |
| 2 | Settore | tinyint |  | YES | 0 |
| 3 | Codice | int |  | YES | 0 |
| 4 | Riga | smallint |  | YES | 0 |
| 5 | Cliente | int |  | YES | 0 |
| 6 | DataDoc | datetime |  | YES | 0 |
| 7 | Articolo | nvarchar | 30 | YES | '' |
| 8 | Fornitore | int |  | YES | 0 |
| 9 | Mag | tinyint |  | YES | 0 |
| 10 | Ums | nvarchar | 10 | YES | '' |
| 11 | Colli | real |  | YES | 0 |
| 12 | PesoLr | real |  | YES | 0 |
| 13 | Tara | real |  | YES | 0 |
| 14 | PesoNt | real |  | YES | 0 |
| 15 | Prezzo | real |  | YES | 0 |
| 16 | Sconto | real |  | YES | 0 |
| 17 | Iva | real |  | YES | 0 |
| 18 | PrNetto | real |  | YES | 0 |
| 19 | PrIvato | real |  | YES | 0 |
| 20 | Importo | real |  | YES | 0 |

### VenditeTmp

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Ord | smallint |  | YES | 0 |
| 2 | Cliente | int |  | YES | 0 |
| 3 | Riga | smallint |  | YES | 0 |
| 4 | Articolo | nvarchar | 30 | YES | '' |
| 5 | Descrizione | nvarchar | 100 | YES | '' |
| 6 | Fornitore | int |  | YES | 0 |
| 7 | Ums | nvarchar | 10 | YES | '' |
| 8 | Mag | tinyint |  | YES | 0 |
| 9 | Colli | smallint |  | YES | 0 |
| 10 | PesoLr | real |  | YES | 0 |
| 11 | Tara | real |  | YES | 0 |
| 12 | PesoNt | real |  | YES | 0 |
| 13 | Prezzo | real |  | YES | 0 |
| 14 | AlqIva | real |  | YES | 0 |
| 15 | PrIvato | real |  | YES | 0 |
| 16 | Merce | real |  | YES | 0 |
| 17 | ValIva | real |  | YES | 0 |
| 18 | Importo | real |  | YES | 0 |

### Vettori

| # | Colonna | Tipo | Lunghezza | Null | Default |
|---:|---|---|---:|---|---|
| 1 | Codice | smallint |  | YES | 0 |
| 2 | Nome | nvarchar | 255 | YES | '' |
| 3 | Citta | nvarchar | 100 | YES | '' |
| 4 | Cap | nvarchar | 5 | YES | '' |
| 5 | Provincia | nvarchar | 2 | YES | '' |
| 6 | Via | nvarchar | 100 | YES | '' |
| 7 | Civico | nvarchar | 20 | YES | '' |
| 8 | CodFi | nvarchar | 20 | YES | '' |
| 9 | PIva | nvarchar | 12 | YES | '' |
| 10 | Telefono1 | nvarchar | 20 | YES | '' |
| 11 | Telefono2 | nvarchar | 20 | YES | '' |
| 12 | SitoWeb | nvarchar | 100 | YES | '' |
| 13 | Email | nvarchar | 100 | YES | '' |
| 14 | Pec | nvarchar | 100 | YES | '' |
| 15 | Pagamento | smallint |  | YES | 0 |
| 16 | Banca | int |  | YES | 0 |
| 17 | Iban | nvarchar | 50 | YES | '' |

