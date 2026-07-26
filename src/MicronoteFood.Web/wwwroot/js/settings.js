document.addEventListener("DOMContentLoaded", () => {
  const tabs = Array.from(document.querySelectorAll("[data-settings-tab]"));
  const panels = Array.from(document.querySelectorAll("[data-settings-panel]"));
  const activeTab = document.querySelector("[data-settings-active-tab]");
  const form = document.querySelector("#settings-form");
  let submittingWithProgress = false;

  const field = (name) => document.querySelector(`[name="Settings.${name}"]`);
  const value = (name) => field(name)?.value.trim() ?? "";


  document.querySelectorAll("[data-password-toggle]").forEach((button) => {
    const input = document.getElementById(button.dataset.passwordToggle);
    if (!input) {
      return;
    }

    button.addEventListener("click", () => {
      const isHidden = input.type === "password";
      input.type = isHidden ? "text" : "password";
      button.classList.toggle("active", isHidden);
      button.setAttribute("aria-label", isHidden ? "Nascondi password" : "Mostra password");
      button.title = isHidden ? "Nascondi password" : "Mostra password";
      input.focus();
    });
  });
  const showMessage = (message, target) => {
    window.MicronoteMessageBox?.show({
      title: "Micronote Food - attenzione",
      message,
      variant: "error",
      okText: "OK",
      onConfirm: () => target?.focus()
    });
  };

  const activate = (index) => {
    tabs.forEach((tab) => {
      const active = Number(tab.dataset.settingsTab) === index;
      tab.classList.toggle("active", active);
      tab.setAttribute("aria-selected", active ? "true" : "false");
    });

    panels.forEach((panel) => {
      panel.classList.toggle("active", Number(panel.dataset.settingsPanel) === index);
    });

    if (activeTab) {
      activeTab.value = String(index);
    }
  };

  tabs.forEach((tab) => {
    tab.addEventListener("click", () => activate(Number(tab.dataset.settingsTab) || 0));
  });

  const firstServerError = document.querySelector(".field-validation-error");
  const firstServerErrorPanel = firstServerError?.closest("[data-settings-panel]");
  activate(firstServerErrorPanel ? Number(firstServerErrorPanel.dataset.settingsPanel) : (Number(activeTab?.value) || 0));

  document.querySelectorAll("[data-uppercase]").forEach((field) => {
    field.addEventListener("input", () => {
      field.value = field.value.toUpperCase();
    });
  });

  document.querySelectorAll("[data-digits]").forEach((field) => {
    field.addEventListener("input", () => {
      field.value = field.value.replace(/\D/g, "");
    });
  });

  const companyVatField = field("PartitaIva");
  const xmlNameMatrixField = field("FeMatriceNomeXml");
  const isAutomaticXmlNameMatrix = (current) => !current || /^IT\d{11}_$/i.test(current.trim());
  const updateXmlNameMatrix = () => {
    if (!companyVatField || !xmlNameMatrixField) {
      return;
    }

    const vat = companyVatField.value.replace(/\D/g, "").slice(0, 11);
    if (vat.length !== 11 || !isAutomaticXmlNameMatrix(xmlNameMatrixField.value)) {
      return;
    }

    xmlNameMatrixField.value = `IT${vat}_`;
  };

  companyVatField?.addEventListener("input", updateXmlNameMatrix);
  companyVatField?.addEventListener("blur", updateXmlNameMatrix);
  updateXmlNameMatrix();

  const validateProvince = (name, label) => {
    const current = value(name);
    if (current && current.length !== 2) {
      return { message: `${label}: inserire 2 lettere.`, target: field(name) };
    }

    return null;
  };

  const validateCap = (name, label) => {
    const current = value(name);
    if (current && current.length !== 5) {
      return { message: `${label}: inserire 5 cifre.`, target: field(name) };
    }

    return null;
  };

  const validateDate = (name, label) => {
    const current = value(name);
    if (!current) {
      return null;
    }

    const ok = /^\d{4}-\d{2}-\d{2}$/.test(current);
    return ok ? null : { message: `${label}: data non valida.`, target: field(name) };
  };

  const validateTime = (name, label) => {
    const current = value(name);
    if (!current) {
      return null;
    }

    const ok = /^\d{2}:\d{2}$/.test(current);
    return ok ? null : { message: `${label}: orario non valido.`, target: field(name) };
  };

  const validateEmail = (name, label) => {
    const current = value(name);
    if (!current) {
      return null;
    }

    const ok = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(current);
    return ok ? null : { message: `${label}: indirizzo non valido.`, target: field(name) };
  };
  const normalizeFiscalValue = (current) => current.toUpperCase().replace(/[^A-Z0-9]/g, "");
  const isValidVatNumber = (current) => {
    const vat = normalizeFiscalValue(current);
    if (!/^\d{11}$/.test(vat)) {
      return false;
    }

    let sum = 0;
    for (let index = 0; index < vat.length; index += 1) {
      const digit = Number(vat[index]);
      if (index % 2 === 0) {
        sum += digit;
        continue;
      }

      const doubled = digit * 2;
      sum += doubled > 9 ? doubled - 9 : doubled;
    }

    return sum % 10 === 0;
  };

  const oddFiscalCodeValues = {
    0: 1, 1: 0, 2: 5, 3: 7, 4: 9, 5: 13, 6: 15, 7: 17, 8: 19, 9: 21,
    A: 1, B: 0, C: 5, D: 7, E: 9, F: 13, G: 15, H: 17, I: 19, J: 21,
    K: 2, L: 4, M: 18, N: 20, O: 11, P: 3, Q: 6, R: 8, S: 12, T: 14,
    U: 16, V: 10, W: 22, X: 25, Y: 24, Z: 23
  };

  const evenFiscalCodeValue = (character) => {
    if (/^\d$/.test(character)) {
      return Number(character);
    }

    const code = character.charCodeAt(0);
    return code >= 65 && code <= 90 ? code - 65 : -1;
  };

  const isValidPersonalFiscalCode = (current) => {
    const fiscalCode = normalizeFiscalValue(current);
    if (!/^[A-Z0-9]{16}$/.test(fiscalCode)) {
      return false;
    }

    let sum = 0;
    for (let index = 0; index < 15; index += 1) {
      const character = fiscalCode[index];
      const digit = index % 2 === 0 ? oddFiscalCodeValues[character] : evenFiscalCodeValue(character);
      if (digit === undefined || digit < 0) {
        return false;
      }

      sum += digit;
    }

    return fiscalCode[15] === String.fromCharCode(65 + (sum % 26));
  };

  const isValidFiscalCode = (current) => {
    const fiscalCode = normalizeFiscalValue(current);
    return fiscalCode.length === 11
      ? isValidVatNumber(fiscalCode)
      : isValidPersonalFiscalCode(fiscalCode);
  };

  const validateTaxCodeField = (showError) => {
    const taxCodeField = field("CodiceFiscale");
    if (!taxCodeField) {
      return true;
    }

    taxCodeField.value = normalizeFiscalValue(taxCodeField.value).slice(0, 16);
    if (!taxCodeField.value) {
      return true;
    }

    if (![11, 16].includes(taxCodeField.value.length) || !isValidFiscalCode(taxCodeField.value)) {
      if (showError) {
        showMessage("Codice fiscale non valido.", taxCodeField);
      }
      return false;
    }

    return true;
  };

  const validateVatNumberField = (showError) => {
    const vatNumberField = field("PartitaIva");
    if (!vatNumberField) {
      return true;
    }

    vatNumberField.value = vatNumberField.value.replace(/\D/g, "").slice(0, 11);
    if (!vatNumberField.value) {
      return true;
    }

    if (!isValidVatNumber(vatNumberField.value)) {
      if (showError) {
        showMessage("Partita IVA non valida.", vatNumberField);
      }
      return false;
    }

    return true;
  };

  field("CodiceFiscale")?.addEventListener("blur", () => validateTaxCodeField(true));
  field("PartitaIva")?.addEventListener("blur", () => validateVatNumberField(true));

  const showSmtpTestMessage = (ok, message, detail = "") => {
    window.MicronoteMessageBox?.show({
      title: ok ? "Micronote Food - connessione riuscita" : "Micronote Food - connessione non riuscita",
      message,
      detail,
      variant: ok ? "success" : "error",
      okText: "OK"
    });
  };

  const readSmtpTestResult = async (response) => {
    const contentType = response.headers.get("content-type") || "";
    if (contentType.includes("application/json")) {
      return response.json();
    }

    return {
      ok: false,
      message: `Errore durante il test della connessione SMTP (${response.status}).`
    };
  };

  document.querySelectorAll("[data-smtp-test]").forEach((button) => {
    button.addEventListener("click", async () => {
      const prefix = button.dataset.smtpTest;
      const token = form?.querySelector('input[name="__RequestVerificationToken"]')?.value ?? "";
      const minimumProgress = new Promise((resolve) => window.setTimeout(resolve, 1000));
      const payload = {
        server: value(`${prefix}ServerSmtp`),
        port: value(`${prefix}PortaSmtp`),
        security: value(`${prefix}Sicurezza`),
        authentication: field(`${prefix}Autenticazione`)?.checked ?? false,
        username: value(`${prefix}Username`),
        password: field(`${prefix}Password`)?.value ?? ""
      };

      button.disabled = true;
      const originalText = button.textContent;
      button.textContent = "Verifica...";
      window.MicronoteProgress?.show?.("Verifica connessione...");

      try {
        const request = fetch("/api/settings/test-smtp", {
          method: "POST",
          credentials: "same-origin",
          headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": token
          },
          body: JSON.stringify(payload)
        });
        const [response] = await Promise.all([request, minimumProgress]);
        const result = await readSmtpTestResult(response);
        const testOk = result.ok === true || result.Ok === true;
        const testMessage = result.message || result.Message || "";
        window.MicronoteProgress?.hide?.();
        showSmtpTestMessage(
          testOk,
          testOk ? "Connessione riuscita." : "Connessione non riuscita:",
          testMessage
        );
      } catch (error) {
        await minimumProgress;
        window.MicronoteProgress?.hide?.();
        showSmtpTestMessage(false, "Connessione non riuscita:", error.message || "Errore imprevisto.");
      } finally {
        window.MicronoteProgress?.hide?.();
        button.disabled = false;
        button.textContent = originalText || "Test connessione";
      }
    });
  });
  const validateForm = () => {
    const codiceFiscale = normalizeFiscalValue(value("CodiceFiscale"));
    const partitaIva = value("PartitaIva").replace(/\D/g, "");
    const checks = [
      !value("RagioneSociale")
        ? { message: "Campo Ragione sociale obbligatorio.", target: field("RagioneSociale") }
        : null,
      !codiceFiscale
        ? { message: "Campo Codice fiscale obbligatorio.", target: field("CodiceFiscale") }
        : null,
      !partitaIva
        ? { message: "Campo Partita IVA obbligatorio.", target: field("PartitaIva") }
        : null,
      codiceFiscale && ![11, 16].includes(codiceFiscale.length)
        ? { message: "Il Codice fiscale deve essere di 11 o 16 caratteri.", target: field("CodiceFiscale") }
        : null,
      codiceFiscale && [11, 16].includes(codiceFiscale.length) && !isValidFiscalCode(codiceFiscale)
        ? { message: "Codice fiscale non valido.", target: field("CodiceFiscale") }
        : null,
      partitaIva && partitaIva.length !== 11
        ? { message: "La Partita IVA deve essere di 11 cifre.", target: field("PartitaIva") }
        : null,
      partitaIva && partitaIva.length === 11 && !isValidVatNumber(partitaIva)
        ? { message: "Partita IVA non valida.", target: field("PartitaIva") }
        : null,
      validateProvince("SedeLegaleProvincia", "Provincia sede legale"),
      validateCap("SedeLegaleCap", "CAP sede legale"),
      validateProvince("SedeOperativaProvincia", "Provincia sede operativa"),
      validateCap("SedeOperativaCap", "CAP sede operativa"),
      validateProvince("TitolareProvincia", "Provincia titolare"),
      validateCap("TitolareCap", "CAP titolare"),
      !value("AzPec")
        ? { message: "Campo PEC obbligatorio.", target: field("AzPec") }
        : null,
      !value("AzEmail")
        ? { message: "Campo E-mail obbligatorio.", target: field("AzEmail") }
        : null,
      validateEmail("AzPec", "PEC"),
      validateEmail("AzEmail", "E-mail"),
      validateEmail("TitolareEmail", "E-mail titolare"),
      validateEmail("TitolarePec", "PEC titolare"),
      validateDate("TitolareDataNascita", "Data di nascita"),
      value("FeCodiceSdiAzienda") && value("FeCodiceSdiAzienda").length !== 7
        ? { message: "Il Codice SDI azienda deve essere di 7 caratteri.", target: field("FeCodiceSdiAzienda") }
        : null,
      validateEmail("FePecDestinazioneSdi", "PEC destinazione SDI"),
      validateEmail("MailOrdEmailMittente", "E-mail mittente ordinaria"),
      validateEmail("MailPecEmailMittente", "E-mail mittente PEC"),
      validateTime("BackupOrario", "Orario copie"),
    ].filter(Boolean);

    return checks[0] ?? null;
  };

  form?.addEventListener("submit", (event) => {
    if (submittingWithProgress) {
      return;
    }

    const error = validateForm();
    if (!error) {
      event.preventDefault();
      event.stopImmediatePropagation();

      submittingWithProgress = true;
      form.querySelectorAll("button[type='submit'], input[type='submit']").forEach((button) => {
        button.disabled = true;
      });
      document.querySelectorAll(`button[type='submit'][form="${form.id}"], input[type='submit'][form="${form.id}"]`).forEach((button) => {
        button.disabled = true;
      });

      window.MicronoteProgress?.show?.("Salvataggio in corso...");
      window.requestAnimationFrame(() => {
        window.setTimeout(() => {
          HTMLFormElement.prototype.submit.call(form);
        }, 1000);
      });
      return;
    }

    event.preventDefault();
    event.stopImmediatePropagation();
    const panel = error.target?.closest("[data-settings-panel]");
    if (panel) {
      activate(Number(panel.dataset.settingsPanel) || 0);
    }

    showMessage(error.message, error.target);
  }, true);
});



