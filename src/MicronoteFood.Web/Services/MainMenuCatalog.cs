using MicronoteFood.Web.Models;

namespace MicronoteFood.Web.Services;

public static class MainMenuCatalog
{
    public static IReadOnlyList<QuickLinkDefinition> QuickLinks { get; } =
    [
        new("Fornitori", "FN", "/Fornitori/Index"),
        new("Articoli", "AR", "/Articoli/Index"),
        new("Carichi", "CA", "/CaricoAcquisti/Index"),
        new("Vendite", "VN", "/Vendite/Index"),
        new("Prima nota", "PN", "/PrimaNota/Index")
    ];

    public static IReadOnlyList<MenuSectionDefinition> Sections { get; } =
    [
        new(
            "tabelle",
            "Tabelle",
            "TB",
            "accent-tables",
            [
                Group(
                    "Soggetti",
                    new MenuItemDefinition("Fornitori", "/Fornitori/Index"),
                    new MenuItemDefinition("Clienti", "/Clienti/Index"),
                    new MenuItemDefinition("Categorie clienti/fornitori", "/CategorieCF/Index"),
                    new MenuItemDefinition("Utenti", "/Utenti/Index"),
                    new MenuItemDefinition("Banche", "/Banche/Index")),
                Group(
                    "Magazzino",
                    new MenuItemDefinition("Categorie", "/Categorie/Index"),
                    new MenuItemDefinition("Gruppi", "/Gruppi/Index"),
                    new MenuItemDefinition("Specie", "/Specie/Index"),
                    new MenuItemDefinition("Provenienza", "/Provenienza/Index"),
                    new MenuItemDefinition("Settori di attività", "/Settori/Index"),
                    new MenuItemDefinition("Unità di misura", "/UnitaMisura/Index"),
                    new MenuItemDefinition("Punti vendita", "/PuntiVendita/Index")),
                Group(
                    "Contabili",
                    new MenuItemDefinition("Causali contabili", "/CausaliContabili/Index"),
                    new MenuItemDefinition("Mastri di conto", "/Mastri/Index"),
                    new MenuItemDefinition("Aliquote IVA", "/AliquoteIva/Index"),
                    new MenuItemDefinition("Piano dei conti", "/PianoConti/Index"),
                    new MenuItemDefinition("Codici di pagamento", "/CodiciPagamento/Index")),
                Group(
                    "Territorio",
                    new MenuItemDefinition("Comuni", "/Comuni/Index"),
                    new MenuItemDefinition("Nazioni", "/Nazioni/Index")),
                Group("Manutenzione", "Allineamento tabelle")
            ]),
        new(
            "magazzino",
            "Magazzino",
            "MG",
            "accent-stock",
            [
                Group(
                    "Articoli e movimenti",
                    new MenuItemDefinition("Anagrafica articoli", "/Articoli/Index"),
                    new MenuItemDefinition("Carico per acquisti", "/CaricoAcquisti/Edit"),
                    new MenuItemDefinition("Scarico per perdite e resi", "/ScaricoPerditeResi/Index"),
                    new MenuItemDefinition("Lista documenti di carico", "/CaricoAcquisti/Index")),
                Group(
                    "Controlli",
                    new MenuItemDefinition("Giacenza iniziale articoli", "/GiacenzaInizialeArticoli/Index"),
                    new MenuItemDefinition("Estratto conto articolo", "/EstrattoContoArticolo/Index"),
                    new MenuItemDefinition("Lista movimenti di magazzino", "/MovimentiMagazzino/Index")),
                Group(
                    "Statistiche",
                    new MenuItemDefinition("Acquisti per articolo", "/AcquistiPerArticolo/Index"),
                    new MenuItemDefinition("Acquisti per gruppo", "/AcquistiPerGruppo/Index")),
                Group("Ordini a fornitori", "Emissione ordine", "Evasione ordine")
            ]),
        new(
            "vendite",
            "Vendite",
            "VN",
            "accent-sales",
            [
                Group(
                    "Vendite operative",
                    new MenuItemDefinition("Vendita al banco", "/VenditaBanco/Index"),
                    new MenuItemDefinition("Vendita plurima"),
                    new MenuItemDefinition("Bolla di vendita", "/BollaVendita/Index"),
                    new MenuItemDefinition("Dettaglio vendite del giorno", "/VenditeGiorno/Index"),
                    new MenuItemDefinition("Storico vendite", "/Vendite/Storico")),
                Group(
                    "Documenti di vendita",
                    new MenuItemDefinition("DDT"),
                    new MenuItemDefinition("Fattura di vendita"),
                    new MenuItemDefinition("Preventivi"),
                    new MenuItemDefinition("Ordini clienti")),
                Group(
                    "Agenti",
                    new MenuItemDefinition("Anagrafica agenti"),
                    new MenuItemDefinition("Provvigioni"),
                    new MenuItemDefinition("Vendite per agente"))
            ]),
        new(
            "contabilita",
            "Contabilità",
            "CN",
            "accent-accounting",
            [
                Group(
                    "Prima nota IVA",
                    new MenuItemDefinition("Fattura di acquisto", "/FattureAcquisto/Edit", "menu"),
                    new MenuItemDefinition("Fattura di vendita"),
                    new MenuItemDefinition("Lista fatture di acquisto", "/FattureAcquisto/Index"),
                    new MenuItemDefinition("Lista fatture di vendita"),
                    new MenuItemDefinition("Caricamento FE acquisti", "/CaricamentoFeAcquisti/Index"),
                    new MenuItemDefinition("Liquidazione periodica IVA")),
                Group(
                    "Prima nota contabile",
                    new MenuItemDefinition("Movimento di prima nota", "/PrimaNota/Edit", "menu"),
                    new MenuItemDefinition("Lista movimenti di prima nota", "/PrimaNota/Index"),
                    new MenuItemDefinition("Lista movimenti di banca", "/MovimentiBanca/Index"),
                    new MenuItemDefinition("Pagamento scadenza passiva"),
                    new MenuItemDefinition("Saldo iniziale clienti e fornitori", "/SaldoInizialeClientiFornitori/Index"),
                    new MenuItemDefinition("Apertura conti patrimoniali", "/AperturaContiPatrimoniali/Index")),
                Group(
                    "Situazione economica",
                    new MenuItemDefinition("Scheda contabile", "/SchedaContabile/Index"),
                    new MenuItemDefinition("Estratto conto clienti e fornitori", "/EstrattoContoClientiFornitori/Index"),
                    new MenuItemDefinition("Estratto conto banca"),
                    new MenuItemDefinition("Rendiconto di cassa", "/RendicontoCassa/Index"),
                    new MenuItemDefinition("Riepilogo movimenti contabili", "/RiepilogoMovimentiContabili/Index"),
                    new MenuItemDefinition("Riepilogo movimenti per punto vendita", "/RiepilogoMovimentiPuntiVendita/Index"),
                    new MenuItemDefinition("Bilancio di verifica", "/BilancioVerifica/Index"),
                    new MenuItemDefinition("Saldi clienti e fornitori", "/SaldiClientiFornitori/Index"))
            ]),
        new(
            "dipendenti",
            "Dipendenti",
            "DP",
            "accent-employees",
            [
                Group("Gestione dipendenti", "Anagrafica dipendente", "Qualifiche", "Movimento contabile", "Lista movimenti contabili", "Scheda contabile")
            ]),
        new(
            "strumenti",
            "Strumenti",
            "ST",
            "accent-tools",
            [
                Group(
                    "Applicazione",
                    new MenuItemDefinition("Opzioni di base", "/Impostazioni/Index"),
                    new MenuItemDefinition("Cambia azienda", "/Login"),
                    new MenuItemDefinition("Cambia utente", "/Login?handler=User"),
                    new MenuItemDefinition("Cambia esercizio", "/CambiaEsercizio/Index")),
                Group("Archivio", "Copie di sicurezza", "Ripristino copie di sicurezza", "Elimina movimenti per anno", "Aggiornamento database", "Azzeramento tabelle"),
                Group("Utilità", new MenuItemDefinition("Assistenza remota"), new MenuItemDefinition("Lista attività", "/AttivitaUtenti/Index"), new MenuItemDefinition("Conversione .p7m in .xml"), new MenuItemDefinition("Importazione dati"), new MenuItemDefinition("Note di aggiornamento"))
            ])
    ];

    private static MenuGroupDefinition Group(string title, params string[] labels) =>
        new(title, labels.Select(label => new MenuItemDefinition(label)).ToArray());

    private static MenuGroupDefinition Group(
        string title,
        params MenuItemDefinition[] items) =>
        new(title, items);
}



