document.addEventListener("DOMContentLoaded", () => {
  const form = document.querySelector("#payment-code-form");
  const code = document.querySelector("#Payment_Code");
  const description = document.querySelector("#Payment_Description");
  const expenses = document.querySelector("#Payment_Expenses");
  const modalCancel = document.querySelector("[data-payment-code-modal-cancel]");
  const skipChecks = document.querySelectorAll("#Payment_SkipAugust, #Payment_SkipDecember");

  skipChecks.forEach((check) => {
    check.removeAttribute("data-val");
    check.removeAttribute("data-val-required");
  });

  if (form && window.jQuery?.validator?.unobtrusive) {
    window.jQuery(form).removeData("validator");
    window.jQuery(form).removeData("unobtrusiveValidation");
    window.jQuery.validator.unobtrusive.parse(form);
  }

  const formatCode = () => {
    if (!code || code.value.trim() === "") {
      return;
    }

    const numeric = Number.parseInt(code.value, 10);
    if (Number.isNaN(numeric)) {
      return;
    }

    code.value = String(numeric).padStart(3, "0");
  };

  const parseCurrency = (value) => window.MicronoteMoney?.parse(value) ?? 0;
  const formatCurrencyValue = (value) => window.MicronoteMoney?.format(value) ?? "";

  const formatCurrency = () => {
    if (!expenses) {
      return;
    }

    expenses.value = formatCurrencyValue(parseCurrency(expenses.value));
  };

  const normalizeCurrencyForSubmit = () => {
    if (!expenses) {
      return;
    }

    const number = parseCurrency(expenses.value);
    expenses.value = String(number);
  };

  const validationSpan = (name) =>
    form?.querySelector(`[data-valmsg-for="${name}"]`);

  const clearDescriptionValidation = () => {
    description?.classList.remove("input-validation-error");

    const span = validationSpan("Payment.Description");
    if (!span) {
      return;
    }

    span.textContent = "";
    span.classList.remove("field-validation-error");
    span.classList.add("field-validation-valid");
  };

  const setDescriptionValidation = () => {
    description?.classList.add("input-validation-error");

    const span = validationSpan("Payment.Description");
    if (!span) {
      return;
    }

    span.textContent = "Campo Descrizione obbligatorio";
    span.classList.remove("field-validation-valid");
    span.classList.add("field-validation-error");
  };

  const showValidationMessage = () => {
    window.MicronoteProgress?.hide?.();
    window.MicronoteValidationMessageBox?.enableSubmitters?.();
    window.MicronoteMessageBox?.show({
      title: "Micronote Food - attenzione",
      message: "Campo Descrizione obbligatorio",
      variant: "error",
      okText: "OK",
      onConfirm: () => {
        window.MicronoteValidationMessageBox?.enableSubmitters?.();
        description?.focus();
      }
    });
  };

  document.addEventListener("submit", (event) => {
    if (event.target !== form) {
      return;
    }

    if ((description?.value || "").trim() === "") {
      event.preventDefault();
      event.stopPropagation();
      event.stopImmediatePropagation();
      setDescriptionValidation();
      showValidationMessage();
      return;
    }

    clearDescriptionValidation();
    normalizeCurrencyForSubmit();
  }, true);

  code?.addEventListener("blur", formatCode);
  description?.addEventListener("input", clearDescriptionValidation);
  expenses?.addEventListener("blur", formatCurrency);
  modalCancel?.addEventListener("click", () => {
    window.parent.postMessage({ type: "micronote:payment-code-cancel" }, window.location.origin);
  });
});
