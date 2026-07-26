(() => {
  const form = document.querySelector("#user-form");
  const saveButton = document.querySelector("[data-user-save]");

  if (!form || !saveButton) {
    return;
  }

  saveButton.addEventListener("click", () => {
    if (saveButton.disabled) {
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
})();
