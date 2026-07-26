document.addEventListener("DOMContentLoaded", () => {
  const code = document.querySelector("#Account_Code");
  const master = document.querySelector("#Account_MasterCode");
  const type = document.querySelector("[data-chart-account-type]");
  const modalCancel = document.querySelector("[data-chart-account-modal-cancel]");
  const form = document.querySelector("#chart-account-form");
  const saveButton = document.querySelector("[data-chart-account-save]");
  const masterTypes = window.MicronoteChartAccountMasterTypes || {};

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

  code?.addEventListener("blur", formatCode);
  master?.addEventListener("change", () => {
    if (type) {
      type.value = masterTypes[master.value] || "";
    }
  });
  modalCancel?.addEventListener("click", () => {
    window.parent.postMessage({ type: "micronote:chart-account-cancel" }, window.location.origin);
  });
  saveButton?.addEventListener("click", () => {
    if (!form || saveButton.disabled) {
      return;
    }

    const jqueryValid =
      window.jQuery && typeof window.jQuery(form).valid === "function"
        ? window.jQuery(form).valid()
        : true;
    const htmlValid =
      typeof form.checkValidity === "function"
        ? form.checkValidity()
        : true;

    if (!jqueryValid || !htmlValid) {
      window.MicronoteValidationMessageBox?.schedule?.();
      return;
    }

    saveButton.disabled = true;
    window.MicronoteProgress?.show?.("Salvataggio in corso...");
    window.requestAnimationFrame(() => {
      window.setTimeout(() => {
        HTMLFormElement.prototype.submit.call(form);
      }, 700);
    });
  });
});
