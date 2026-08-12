document.addEventListener("DOMContentLoaded", () => {
  const progressOverlay = document.getElementById("operation-progress");
  const progressText = progressOverlay?.querySelector("span");
  const defaultProgressText = progressText?.textContent ?? "Operazione in corso...";
  const defaultMinimumProgressTime = 1000;

  document.querySelectorAll("input[readonly], textarea[readonly]").forEach((control) => {
    control.tabIndex = -1;
  });

  const showProgress = (message, hideText = false) => {
    if (!progressOverlay) {
      return;
    }

    if (progressText) {
      progressText.textContent = message || defaultProgressText;
    }

    progressOverlay.classList.toggle("progress-without-text", hideText);
    progressOverlay.classList.add("active");
    progressOverlay.setAttribute("aria-hidden", "false");
  };

  const hideProgress = () => {
    if (!progressOverlay) {
      return;
    }

    progressOverlay.classList.remove("active");
    progressOverlay.classList.remove("progress-without-text");
    progressOverlay.setAttribute("aria-hidden", "true");

    if (progressText) {
      progressText.textContent = defaultProgressText;
    }
  };

  const wait = (milliseconds) =>
    new Promise((resolve) => {
      window.setTimeout(resolve, milliseconds);
    });

  const waitForPaint = () =>
    new Promise((resolve) => {
      window.requestAnimationFrame(() => {
        window.requestAnimationFrame(resolve);
      });
    });

  const runWithProgress = async (work, options = {}) => {
    const minimumTime = options.minimumTime ?? defaultMinimumProgressTime;

    showProgress(options.message);
    await waitForPaint();
    const startedAt = performance.now();

    try {
      return await work();
    } finally {
      const elapsed = performance.now() - startedAt;
      const remainingTime = Math.max(0, minimumTime - elapsed);

      if (remainingTime > 0) {
        await wait(remainingTime);
      }

      if (options.hideWhenDone !== false) {
        hideProgress();
      }
    }
  };

  window.MicronoteProgress = {
    show: showProgress,
    hide: hideProgress,
    run: runWithProgress
  };
  window.addEventListener("pageshow", hideProgress);

  const showValidationErrors = () => {
    if (window.MicronoteValidationMessageBox?.enableSubmitters) {
      window.MicronoteValidationMessageBox.enableSubmitters();
    }

    if (window.MicronoteValidationMessageBox?.schedule) {
      window.MicronoteValidationMessageBox.schedule();
    }
  };

  const formSubmitButtons = (form) => {
    const buttons = Array.from(form.querySelectorAll("button[type='submit'], input[type='submit']"));

    if (form.id) {
      buttons.push(
        ...document.querySelectorAll(
          `button[type='submit'][form="${form.id}"], input[type='submit'][form="${form.id}"]`
        )
      );
    }

    return buttons;
  };

  const resetProgressSubmitState = (form, submitter) => {
    delete form.dataset.progressReady;
    delete form.dataset.progressPending;
    hideProgress();

    if (submitter) {
      submitter.disabled = false;
    }

    formSubmitButtons(form).forEach((button) => {
      if (!button.closest("fieldset")?.disabled) {
        button.disabled = false;
      }
    });
  };

  const escapeSelector = (value) => {
    if (window.CSS?.escape) {
      return window.CSS.escape(value);
    }

    return value.replace(/["\\]/g, "\\$&");
  };

  const validationTargetFor = (form, control) => {
    if (!control.name) {
      return null;
    }

    return form.querySelector(`[data-valmsg-for="${escapeSelector(control.name)}"]`);
  };

  const markControlInvalid = (form, control, message) => {
    control.classList.add("input-validation-error");

    const target = validationTargetFor(form, control);

    if (!target) {
      return;
    }

    target.textContent = message;
    target.classList.remove("field-validation-valid");
    target.classList.add("field-validation-error");
  };

  const clearControlInvalid = (form, control) => {
    control.classList.remove("input-validation-error");

    const target = validationTargetFor(form, control);

    if (!target || target.textContent.trim() === "") {
      return;
    }

    target.textContent = "";
    target.classList.remove("field-validation-error");
    target.classList.add("field-validation-valid");
  };

  const controlHasValue = (control) => {
    if (control.type === "checkbox" || control.type === "radio") {
      return control.checked;
    }

    return (control.value || "").trim() !== "";
  };

  const validateRequiredFields = (form) => {
    let isValid = true;

    form.querySelectorAll("[data-val='true'][data-val-required]").forEach((control) => {
      if (control.disabled || control.type === "hidden") {
        return;
      }

      const message = control.getAttribute("data-val-required") || "Campo obbligatorio";

      if (controlHasValue(control)) {
        clearControlInvalid(form, control);
        return;
      }

      markControlInvalid(form, control, message);
      isValid = false;
    });

    return isValid;
  };

  const isFormValid = (form) => {
    const requiredFieldsValid = validateRequiredFields(form);
    let jqueryValid = true;

    if (
      window.jQuery
      && window.jQuery.validator
      && typeof window.jQuery(form).valid === "function"
    ) {
      jqueryValid = window.jQuery(form).valid();
    }

    const htmlValid = typeof form.checkValidity === "function"
      ? form.checkValidity()
      : true;

    return requiredFieldsValid && jqueryValid && htmlValid;
  };

  document.querySelectorAll("form").forEach((form) => {
    if (form.dataset.progressDisabled === "true") {
      return;
    }

    form.addEventListener("submit", async (event) => {
      if (form.dataset.progressReady === "true") {
        delete form.dataset.progressReady;
        delete form.dataset.progressPending;
        return;
      }

      if (form.dataset.progressPending === "true") {
        event.preventDefault();
        event.stopImmediatePropagation();
        if (!isFormValid(form)) {
          resetProgressSubmitState(form, event.submitter);
          showValidationErrors();
        }
        return;
      }

      const submitter = event.submitter;

      if (!isFormValid(form)) {
        event.preventDefault();
        event.stopImmediatePropagation();
        resetProgressSubmitState(form, submitter);
        showValidationErrors();
        return;
      }

      event.preventDefault();
      event.stopImmediatePropagation();

      const configuredMinimumTime = Number.parseInt(form.dataset.progressMinimumTime, 10);
      const minimumTime = Number.isNaN(configuredMinimumTime)
        ? defaultMinimumProgressTime
        : Math.max(0, configuredMinimumTime);
      form.dataset.progressPending = "true";

      showProgress(
        form.dataset.progressMessage || "Salvataggio in corso...",
        form.dataset.progressHideText === "true"
      );
      const submitForm = () => {
        window.setTimeout(() => {
          form.dataset.progressReady = "true";
          HTMLFormElement.prototype.submit.call(form);
        }, minimumTime);
      };
      window.requestAnimationFrame(() => {
        if (form.dataset.progressWaitForPaint === "true") {
          window.requestAnimationFrame(submitForm);
          return;
        }
        submitForm();
      });
    }, { capture: true });
  });

  const menuSections = document.querySelectorAll(".main-menu .menu-section");
  const topMargin = 16;
  const bottomMargin = 16;
  const menuStorageKey = "micronote:main-menu:open-section";
  let activeMenuSection = null;

  const savedMenuSectionId = window.sessionStorage?.getItem(menuStorageKey);
  menuSections.forEach((section) => {
    section.style.gridColumn = "auto";
  });

  if (savedMenuSectionId) {
    const savedMenuSection = document.getElementById(savedMenuSectionId);
    if (savedMenuSection?.matches(".main-menu .menu-section")) {
      savedMenuSection.style.gridColumn = "auto";
      savedMenuSection.open = true;
      activeMenuSection = savedMenuSection;
    }
  }

  menuSections.forEach((currentSection) => {
    currentSection.addEventListener("toggle", () => {
      if (!currentSection.open) {
        if (activeMenuSection === currentSection) {
          activeMenuSection = null;
        }
        return;
      }

      activeMenuSection = currentSection;
      currentSection.style.gridColumn = "auto";
      window.sessionStorage?.setItem(menuStorageKey, currentSection.id);
      menuSections.forEach((otherSection) => {
        if (otherSection !== currentSection) {
          otherSection.open = false;
        }
      });

      requestAnimationFrame(() => {
        requestAnimationFrame(() => {
          const sectionBounds = currentSection.getBoundingClientRect();
          const availableHeight = window.innerHeight - topMargin - bottomMargin;
          let scrollAdjustment = 0;

          if (sectionBounds.height > availableHeight) {
            scrollAdjustment = sectionBounds.top - topMargin;
          } else if (sectionBounds.top < topMargin) {
            scrollAdjustment = sectionBounds.top - topMargin;
          } else if (sectionBounds.bottom > window.innerHeight - bottomMargin) {
            scrollAdjustment =
              sectionBounds.bottom - window.innerHeight + bottomMargin;
          }

          if (Math.abs(scrollAdjustment) > 1) {
            window.scrollBy({
              top: scrollAdjustment,
              behavior: "smooth"
            });
          }
        });
      });
    });
  });

  if (menuSections.length > 0) {
    document.addEventListener("keydown", (event) => {
      if (event.key !== "Escape" || event.defaultPrevented) {
        return;
      }

      const focusedSection = document.activeElement?.closest?.(".main-menu .menu-section[open]");
      const openSection = focusedSection
        || activeMenuSection
        || document.querySelector(".main-menu .menu-section[open]");

      if (!openSection) {
        return;
      }

      event.preventDefault();
      openSection.open = false;
      openSection.querySelector("summary")?.focus({ preventScroll: true });
      activeMenuSection = null;
      window.sessionStorage?.removeItem(menuStorageKey);
    });
  }
});
