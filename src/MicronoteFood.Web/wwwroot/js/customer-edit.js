(function () {
  const form = document.querySelector("#customer-form");
  const saveButton = document.querySelector("[data-customer-save]");

  if (!form || !saveButton) {
    return;
  }

  const field = (name) => form.querySelector(`[name="Customer.${name}"]`);
  const taxCodeField = field("TaxCode");
  const vatNumberField = field("VatNumber");
  const moneyFields = Array.from(form.querySelectorAll("[data-money-field]"));

  const showMessage = (message, target) => {
    window.MicronoteMessageBox?.show({
      title: "Micronote Fish - attenzione",
      message,
      variant: "error",
      okText: "OK",
      onConfirm: () => window.setTimeout(() => target?.focus(), 0)
    });
  };

  const normalizeFiscalValue = (value) => (value ?? "").toUpperCase().replace(/[^A-Z0-9]/g, "");
  const normalizeVatNumber = (value) => (value ?? "").replace(/\D/g, "").slice(0, 11);

  const isValidVatNumber = (value) => {
    const vat = normalizeVatNumber(value);
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

  const isValidPersonalFiscalCode = (value) => {
    const fiscalCode = normalizeFiscalValue(value);
    if (!/^[A-Z0-9]{16}$/.test(fiscalCode)) {
      return false;
    }

    let sum = 0;
    for (let index = 0; index < 15; index += 1) {
      const character = fiscalCode[index];
      const current = index % 2 === 0 ? oddFiscalCodeValues[character] : evenFiscalCodeValue(character);
      if (current === undefined || current < 0) {
        return false;
      }

      sum += current;
    }

    return fiscalCode[15] === String.fromCharCode(65 + (sum % 26));
  };

  const isValidFiscalCode = (value) => {
    const fiscalCode = normalizeFiscalValue(value);
    return fiscalCode.length === 11
      ? isValidVatNumber(fiscalCode)
      : isValidPersonalFiscalCode(fiscalCode);
  };

  const validateTaxCode = (showError) => {
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

  const validateVatNumber = (showError) => {
    if (!vatNumberField) {
      return true;
    }

    vatNumberField.value = normalizeVatNumber(vatNumberField.value);
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

  taxCodeField?.addEventListener("input", () => {
    const start = taxCodeField.selectionStart ?? taxCodeField.value.length;
    taxCodeField.value = normalizeFiscalValue(taxCodeField.value).slice(0, 16);
    taxCodeField.setSelectionRange(Math.min(start, taxCodeField.value.length), Math.min(start, taxCodeField.value.length));
  });

  vatNumberField?.addEventListener("input", () => {
    vatNumberField.value = normalizeVatNumber(vatNumberField.value);
  });

  taxCodeField?.addEventListener("blur", () => validateTaxCode(true));
  vatNumberField?.addEventListener("blur", () => validateVatNumber(true));

  saveButton.addEventListener("click", () => {
    if (saveButton.disabled) {
      return;
    }

    if (!validateTaxCode(true) || !validateVatNumber(true)) {
      return;
    }

    moneyFields.forEach((input) => {
      window.MicronoteMoney?.normalizeForSubmit?.(input);
    });

    const jqueryValid = window.jQuery && typeof window.jQuery(form).valid === "function"
      ? window.jQuery(form).valid()
      : true;
    const htmlValid = typeof form.checkValidity === "function"
      ? form.checkValidity()
      : true;

    if (!jqueryValid || !htmlValid) {
      window.MicronoteValidationMessageBox?.schedule?.();
      return;
    }

    saveButton.disabled = true;
    window.MicronoteProgress?.show?.("Salvataggio in corso...");
    window.setTimeout(() => {
      HTMLFormElement.prototype.submit.call(form);
    }, 1000);
  });
})();
