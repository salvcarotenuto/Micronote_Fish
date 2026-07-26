(function () {
  function textOf(element) {
    return (element.textContent || "").replace(/\s+/g, " ").trim();
  }

  function unique(values) {
    const seen = new Set();
    return values.filter((value) => {
      if (!value || seen.has(value)) {
        return false;
      }

      seen.add(value);
      return true;
    });
  }

  function collectMessages() {
    const summaryMessages = Array.from(
      document.querySelectorAll(".validation-summary-errors li")
    ).map(textOf);

    const fieldMessages = Array.from(
      document.querySelectorAll(".field-validation-error")
    ).map(textOf);

    return unique(summaryMessages.concat(fieldMessages));
  }

  function firstInvalidControl() {
    const error = document.querySelector(".field-validation-error");
    const field = error ? error.closest(".field") : null;

    if (field) {
      const control = field.querySelector("input, select, textarea");

      if (control && !control.disabled && typeof control.focus === "function") {
        return control;
      }
    }

    return document.querySelector(
      "input.input-validation-error, select.input-validation-error, textarea.input-validation-error"
    );
  }

  function submitButtonsFor(form) {
    if (!form) {
      return [];
    }

    const buttons = Array.from(
      form.querySelectorAll("button[type='submit'], input[type='submit']")
    );

    if (form.id) {
      buttons.push(
        ...document.querySelectorAll(
          `button[type='submit'][form="${form.id}"], input[type='submit'][form="${form.id}"]`
        )
      );
    }

    return buttons;
  }

  function enableValidationFormSubmitters() {
    document.querySelectorAll("form").forEach((form) => {
      delete form.dataset.progressReady;
      delete form.dataset.progressPending;

      submitButtonsFor(form).forEach((button) => {
        if (!button.closest("fieldset")?.disabled) {
          button.disabled = false;
        }
      });
    });

    if (window.MicronoteProgress?.hide) {
      window.MicronoteProgress.hide();
    }
  }

  function showValidationBox() {
    if (!window.MicronoteMessageBox || typeof window.MicronoteMessageBox.show !== "function") {
      return;
    }

    const messages = collectMessages();

    if (!messages.length) {
      return;
    }

    enableValidationFormSubmitters();

    window.MicronoteMessageBox.show({
      title: "Micronote Fish - attenzione",
      message: messages[0],
      detail: "",
      variant: "error",
      okText: "OK",
      onConfirm: function () {
        enableValidationFormSubmitters();

        const control = firstInvalidControl();

        if (control) {
          control.focus();
        }
      },
    });
  }

  function scheduleValidationBox() {
    window.setTimeout(showValidationBox, 0);
    window.setTimeout(showValidationBox, 80);
    window.setTimeout(enableValidationFormSubmitters, 150);
    window.setTimeout(enableValidationFormSubmitters, 350);
  }

  window.MicronoteValidationMessageBox = {
    show: showValidationBox,
    schedule: scheduleValidationBox,
    enableSubmitters: enableValidationFormSubmitters
  };

  document.addEventListener("DOMContentLoaded", function () {
    showValidationBox();

    document.querySelectorAll("form").forEach((form) => {
      form.addEventListener("submit", scheduleValidationBox, true);
    });
  });
})();
