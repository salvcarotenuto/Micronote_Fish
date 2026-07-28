-- MicroFish_0001 - adeguamento CaricoRg ai dati SQL Server CE.
-- Eseguire dopo un backup e prima di abilitare il salvataggio dei carichi.

USE `MicroFish_0001`;

-- Precondizione: questa query deve restituire zero righe.
SELECT Anno, Codice, COUNT(*) AS Occorrenze
FROM carico
GROUP BY Anno, Codice
HAVING COUNT(*) > 1;

ALTER TABLE caricorg
    MODIFY COLUMN Quantita DECIMAL(10,3) DEFAULT 0.000,
    MODIFY COLUMN Prezzo DECIMAL(10,3) DEFAULT 0.000,
    ADD COLUMN Tara DECIMAL(10,3) DEFAULT 0.000 AFTER Importo,
    ADD COLUMN PrNetto DECIMAL(10,3) DEFAULT 0.000 AFTER Tara,
    ADD COLUMN PrIvato DECIMAL(10,3) DEFAULT 0.000 AFTER PrNetto;

-- Ricostruzione della chiave tecnica padre/figlio.
-- Anno + Codice è la chiave aziendale stabile durante la migrazione.
UPDATE caricorg rg
JOIN carico c
  ON c.Anno = rg.Anno
 AND c.Codice = rg.Codice
SET rg.ID = c.ID
WHERE rg.ID <> c.ID;

-- Vincoli che rendono non ambigua la ricostruzione e il collegamento.
ALTER TABLE carico
    ADD UNIQUE KEY UX_carico_Anno_Codice (Anno, Codice);

ALTER TABLE caricorg
    ADD UNIQUE KEY UX_caricorg_Anno_Codice_Riga (Anno, Codice, Riga),
    ADD KEY IX_caricorg_ID (ID);

-- Verifiche finali: tutte devono restituire zero righe.
SELECT rg.Anno, rg.Codice, rg.Riga, rg.ID
FROM caricorg rg
LEFT JOIN carico c
  ON c.Anno = rg.Anno
 AND c.Codice = rg.Codice
WHERE c.ID IS NULL;

SELECT rg.Anno, rg.Codice, rg.Riga,
       rg.ID AS IdRiga, c.ID AS IdTestata
FROM caricorg rg
JOIN carico c
  ON c.Anno = rg.Anno
 AND c.Codice = rg.Codice
WHERE rg.ID <> c.ID;

SELECT Anno, Codice, Riga, COUNT(*) AS Occorrenze
FROM caricorg
GROUP BY Anno, Codice, Riga
HAVING COUNT(*) > 1;
