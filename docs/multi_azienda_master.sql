CREATE DATABASE IF NOT EXISTS mn_master
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_0900_ai_ci;

USE mn_master;

CREATE TABLE IF NOT EXISTS Aziende (
    Codice INT NOT NULL PRIMARY KEY,
    Nome VARCHAR(120) NOT NULL,
    Password VARCHAR(255) NOT NULL,
    Attiva TINYINT(1) NOT NULL DEFAULT 1,
    Bloccata TINYINT(1) NOT NULL DEFAULT 0,
    NomeDatabase VARCHAR(120) NULL,
    VersioneDbAttuale VARCHAR(30) NULL,
    VersioneDbRichiesta VARCHAR(30) NULL,
    UNIQUE KEY UX_Aziende_Nome (Nome)
);

CREATE TABLE IF NOT EXISTS Parametri (
    Chiave VARCHAR(100) NOT NULL PRIMARY KEY,
    Valore TEXT NULL
);

INSERT INTO Aziende
(
    Codice,
    Nome,
    Password,
    Attiva,
    Bloccata,
    NomeDatabase
)
VALUES
(
    1,
    '0001',
    '0001',
    1,
    0,
    'mn_0001'
)
ON DUPLICATE KEY UPDATE
    NomeDatabase = VALUES(NomeDatabase),
    Attiva = VALUES(Attiva),
    Bloccata = VALUES(Bloccata);
