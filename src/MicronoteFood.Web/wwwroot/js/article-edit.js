(function () {
  const form = document.querySelector("#article-form");
  const saveButton = document.querySelector("[data-article-save]");
  const modalCancel = document.querySelector("[data-article-modal-cancel='true']");

  if (!form) {
    return;
  }

  modalCancel?.addEventListener("click", (event) => {
    event.preventDefault();
    window.parent.postMessage({ type: "micronote:article-cancel" }, window.location.origin);
  });

  if (modalCancel) {
    document.addEventListener("keydown", (event) => {
      if (event.key !== "Escape") {
        return;
      }

      event.preventDefault();
      event.stopImmediatePropagation();
      window.parent.postMessage({ type: "micronote:article-cancel" }, window.location.origin);
    }, true);
  }

  form.querySelectorAll("[data-integer-field]").forEach((input) => {
    const clean = () => {
      input.value = input.value.replace(/\D/g, "");
    };
    const format = () => {
      clean();
      if (input.value !== "") {
        input.value = String(Number.parseInt(input.value, 10));
      }
    };

    input.addEventListener("beforeinput", (event) => {
      if (!event.data || /^\d+$/.test(event.data)) {
        return;
      }

      event.preventDefault();
    });

    input.addEventListener("input", clean);
    input.addEventListener("blur", format);
    input.addEventListener("paste", () => {
      window.setTimeout(clean, 0);
    });
  });

  form.querySelectorAll("[data-percent-field]").forEach((input) => {
    const percent = window.MicronotePercent;
    if (percent?.parse && percent?.format) {
      input.value = percent.format(percent.parse(input.value));
    }
  });

  const normalizePercentFields = () => {
    form.querySelectorAll("[data-percent-field]").forEach((input) => {
      window.MicronotePercent?.normalizeForSubmit?.(input);
    });
  };

  const normalizeMoneyFields = () => {
    form.querySelectorAll("[data-money-field]").forEach((input) => {
      window.MicronoteMoney?.normalizeForSubmit?.(input);
    });
  };

  const normalizeDecimalFields = () => {
    form.querySelectorAll("[data-decimal-field]").forEach((input) => {
      const decimal = window.MicronoteDecimal;
      if (decimal?.normalizeForSubmit) {
        decimal.normalizeForSubmit(input);
        return;
      }

      input.value = String(input.value ?? "")
        .replace(/\./g, "")
        .replace(",", ".");
    });
  };

  const normalizeNumericFields = () => {
    normalizeMoneyFields();
    normalizeDecimalFields();
    normalizePercentFields();
  };

  const localizeDecimalFieldsForServer = () => {
    form.querySelectorAll("[data-decimal-field]").forEach((input) => {
      const decimal = window.MicronoteDecimal;
      const value = decimal?.parse
        ? decimal.parse(input.value)
        : Number.parseFloat(String(input.value ?? "").replace(",", "."));
      input.value = Number.isFinite(value)
        ? String(value).replace(".", ",")
        : "";
    });
  };

  const azione = Number.parseInt(form.querySelector("#Azione")?.value ?? "2", 10);
  const azioneBase = Math.abs(azione) % 100;
  const azioneContesto = Math.floor(Math.abs(azione) / 100);
  const isNew = azioneBase === 2;
  const isCodeReadonly = (azioneContesto & 4) === 4;

  if (!isNew || isCodeReadonly) {
    window.setTimeout(() => {
      const description = form.querySelector("[data-article-description]");
      description?.focus();
      description?.select?.();
    }, 0);
  }

  form.querySelector("[data-article-supplier-clear]")?.addEventListener("click", () => {
    const code = form.querySelector("#Article_SupplierCode");
    const codeDisplay = form.querySelector("#articleSupplierCodeDisplay");
    const nameDisplay = form.querySelector("#articleSupplierNameDisplay");

    if (code) {
      code.value = "";
      code.dispatchEvent(new Event("change", { bubbles: true }));
    }

    if (codeDisplay) {
      codeDisplay.value = "";
      codeDisplay.focus();
    }

    if (nameDisplay) {
      nameDisplay.value = "";
    }
  });

  saveButton?.addEventListener("click", (event) => {
    event.preventDefault();

    if (saveButton.disabled) {
      return;
    }

    normalizeNumericFields();

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
      normalizeNumericFields();
      localizeDecimalFieldsForServer();
      HTMLFormElement.prototype.submit.call(form);
    }, 1000);
  });
})();
