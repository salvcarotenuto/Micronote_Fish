document.addEventListener("DOMContentLoaded", () => {
  const rows = Array.from(document.querySelectorAll("[data-account-row]"));
  const form = document.getElementById("accounting-cause-form");
  const saveButton = document.querySelector("[data-accounting-cause-save]");
  const zoom = document.querySelector("[data-account-zoom]");
  const zoomGrid = zoom?.querySelector("[data-account-zoom-grid]");
  const zoomTable = zoom?.querySelector(".account-zoom-grid");
  const zoomBody = zoom?.querySelector(".account-zoom-grid tbody");
  const zoomRows = Array.from(zoom?.querySelectorAll("[data-zoom-row]") ?? []);
  const zoomSortHeaders = Array.from(zoom?.querySelectorAll("[data-account-zoom-sort]") ?? []);
  const zoomMaster = zoom?.querySelector("[data-account-zoom-master]");
  const zoomSearch = zoom?.querySelector("[data-account-zoom-search]");
  const zoomCount = zoom?.querySelector("[data-account-zoom-count]");
  const zoomEmpty = zoom?.querySelector("[data-account-zoom-empty]");
  const zoomOk = zoom?.querySelector("[data-account-zoom-ok]");
  const zoomCancel = zoom?.querySelector("[data-account-zoom-cancel]");
  const typeButtons = Array.from(zoom?.querySelectorAll("[data-account-type-filter]") ?? []);
  let activeRow = null;
  let activeType = "all";
  let zoomSort = { key: "masterCode", direction: "asc" };
  let zoomLastScrollTop = 0;
  let zoomScrollFrame = 0;
  let zoomWheelFrame = 0;

  const enableSaveButton = () => {
    if (form) {
      delete form.dataset.progressReady;
      delete form.dataset.progressPending;
    }

    if (saveButton) {
      saveButton.disabled = false;
      saveButton.removeAttribute("aria-disabled");
    }
  };

  const updateRow = (row) => {
    const select = row.querySelector("[data-account-select]");
    const selectedCode = select?.value || "";
    const option = selectedCode
      ? zoomRows.find((candidate) => candidate.dataset.accountCode === selectedCode)
      : null;
    const masterCode = row.querySelector("[data-master-code]");
    const masterDescription = row.querySelector("[data-master-description]");
    const accountCode = row.querySelector("[data-account-code]");
    const accountDescription = row.querySelector("[data-account-description]");

    if (!select || !option) {
      if (masterCode) {
        masterCode.value = "";
      }
      if (masterDescription) {
        masterDescription.value = "";
      }
      if (accountCode) {
        accountCode.value = "";
      }
      if (accountDescription) {
        accountDescription.value = "";
      }
      return;
    }

    if (masterCode) {
      masterCode.value = String(option.dataset.masterCode || "").padStart(3, "0");
    }
    if (masterDescription) {
      masterDescription.value = option.dataset.masterDescription || "";
    }
    if (accountCode) {
      accountCode.value = String(option.dataset.accountCode || "").padStart(3, "0");
    }
    if (accountDescription) {
      accountDescription.value = option.dataset.accountDescription || "";
    }
  };

  const normalize = (value) =>
    String(value ?? "")
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .toLowerCase();

  const formSubmitButtons = () => {
    if (!form) {
      return [];
    }

    return [
      ...(saveButton ? [saveButton] : []),
      ...form.querySelectorAll("button[type='submit'], input[type='submit']"),
      ...document.querySelectorAll(`button[type='submit'][form="${form.id}"], input[type='submit'][form="${form.id}"]`)
    ];
  };

  const validationSpan = (fieldName) =>
    form?.querySelector(`[data-valmsg-for="${fieldName}"]`);

  const setAccountValidation = (fieldName, message) => {
    const span = validationSpan(fieldName);
    if (!span) {
      return;
    }

    span.textContent = message;
    span.classList.remove("field-validation-valid");
    span.classList.add("field-validation-error");
  };

  const clearAccountValidation = (fieldName) => {
    const span = validationSpan(fieldName);
    if (!span) {
      return;
    }

    span.textContent = "";
    span.classList.remove("field-validation-error");
    span.classList.add("field-validation-valid");
  };

  const setFieldValidation = (fieldName, message) => {
    const span = validationSpan(fieldName);
    if (!span) {
      return;
    }

    span.textContent = message;
    span.classList.remove("field-validation-valid");
    span.classList.add("field-validation-error");
  };

  const clearFieldValidation = (fieldName) => {
    clearAccountValidation(fieldName);
  };

  const hasAccountValue = (fieldPrefix) =>
    Array.from(form?.querySelectorAll(`input[name^="Cause.${fieldPrefix}"]`) ?? [])
      .some((input) => Number(input.value || 0) > 0);

  const validateAccountSections = () => {
    let isValid = true;

    clearAccountValidation("Cause.Debit1");
    clearAccountValidation("Cause.Credit1");

    if (!hasAccountValue("Debit")) {
      setAccountValidation("Cause.Debit1", "La sezione Dare non può essere vuota.");
      isValid = false;
    }

    if (!hasAccountValue("Credit")) {
      setAccountValidation("Cause.Credit1", "La sezione Avere non può essere vuota.");
      isValid = false;
    }

    return isValid;
  };

  const accountValidationMessages = () =>
    [
      validationSpan("Cause.Description"),
      validationSpan("Cause.Debit1"),
      validationSpan("Cause.Credit1")
    ]
      .map((span) => (span?.textContent || "").replace(/\s+/g, " ").trim())
      .filter(Boolean);

  const stopInvalidSubmit = (event) => {
    event.preventDefault();
    event.stopPropagation();
    event.stopImmediatePropagation();

    delete form.dataset.progressReady;
    delete form.dataset.progressPending;
    window.MicronoteProgress?.hide?.();
    formSubmitButtons().forEach((button) => {
      button.disabled = false;
    });

    const messages = accountValidationMessages();
    if (messages.length && window.MicronoteMessageBox?.show) {
      window.MicronoteMessageBox.show({
        title: "Micronote Food - attenzione",
        message: messages[0],
        detail: messages.slice(1).join("\n"),
        variant: "error",
        okText: "OK",
        onConfirm: () => {
          formSubmitButtons().forEach((button) => {
            button.disabled = false;
          });
          rows[0]?.querySelector("[data-account-add]")?.focus();
        }
      });
      return;
    }

    window.MicronoteValidationMessageBox?.schedule?.();
  };

  const validateRequiredFields = () => {
    const description = form?.querySelector("[name='Cause.Description']");
    clearFieldValidation("Cause.Description");

    if ((description?.value || "").trim() !== "") {
      return true;
    }

    setFieldValidation("Cause.Description", "Campo Descrizione obbligatorio");
    return false;
  };

  const showProgressAndSubmit = () => {
    if (!form) {
      return;
    }

    if (saveButton) {
      saveButton.disabled = true;
    }
    window.MicronoteProgress?.show?.("Salvataggio in corso...");
    window.setTimeout(() => {
      HTMLFormElement.prototype.submit.call(form);
    }, 1000);
  };

  const selectedZoomRow = () => zoom?.querySelector("[data-zoom-row].selected-row");
  const currentZoomRows = () => Array.from(zoomBody?.querySelectorAll("[data-zoom-row]") ?? zoomRows);
  const visibleZoomRows = () => currentZoomRows().filter((row) => !row.hidden);

  const sortValue = (row, key) => {
    const value = row.dataset[key] || "";
    if (key === "masterCode" || key === "accountCode") {
      return Number(value);
    }

    return normalize(value);
  };

  const updateZoomSortHeaders = () => {
    zoomSortHeaders.forEach((header) => {
      const active = header.dataset.accountZoomSort === zoomSort.key;
      header.classList.toggle("is-sorted", active);
      header.classList.toggle("is-descending", active && zoomSort.direction === "desc");
      header.setAttribute("aria-sort", active ? (zoomSort.direction === "asc" ? "ascending" : "descending") : "none");
    });
  };

  const applyZoomSort = () => {
    if (!zoomBody) {
      return;
    }

    const direction = zoomSort.direction === "asc" ? 1 : -1;
    [...zoomRows]
      .sort((left, right) => {
        const leftValue = sortValue(left, zoomSort.key);
        const rightValue = sortValue(right, zoomSort.key);

        if (leftValue < rightValue) {
          return -1 * direction;
        }
        if (leftValue > rightValue) {
          return 1 * direction;
        }

        return Number(left.dataset.accountCode || 0) - Number(right.dataset.accountCode || 0);
      })
      .forEach((row) => zoomBody.appendChild(row));

    updateZoomSortHeaders();
  };

  const typeMatches = (type) => {
    if (activeType === "all") {
      return true;
    }

    if (activeType === "equity") {
      return type === "P" || type === "A" || type === "T";
    }

    if (activeType === "costs") {
      return type === "C";
    }

    if (activeType === "revenues") {
      return type === "R";
    }

    return true;
  };

  const selectZoomRow = (row, focus = false, scroll = true) => {
    if (!row || row.hidden) {
      return;
    }

    zoomRows.forEach((candidate) => {
      candidate.classList.remove("selected-row");
      candidate.removeAttribute("aria-selected");
    });

    row.classList.add("selected-row");
    row.setAttribute("aria-selected", "true");

    if (focus) {
      row.focus({ preventScroll: true });
    }
    if (scroll) {
      row.scrollIntoView({ block: "nearest" });
    }
  };

  const zoomViewport = () => {
    const gridRect = zoomGrid.getBoundingClientRect();
    const headerHeight = zoomTable?.tHead?.getBoundingClientRect().height ?? 0;

    return {
      top: gridRect.top + headerHeight,
      bottom: gridRect.bottom
    };
  };

  const isZoomRowVisible = (row, viewport) => {
    const rowRect = row.getBoundingClientRect();
    return rowRect.bottom > viewport.top + 1 &&
      rowRect.top < viewport.bottom - 1;
  };

  const fullyVisibleZoomRows = (viewport) => {
    return visibleZoomRows().filter((row) => {
      const rowRect = row.getBoundingClientRect();
      return rowRect.top >= viewport.top + 1 &&
        rowRect.bottom <= viewport.bottom - 1;
    });
  };

  const selectZoomRowFromScroll = (delta) => {
    if (!zoomGrid || zoom?.hidden || delta === 0) {
      return;
    }

    const viewport = zoomViewport();
    const selected = selectedZoomRow();
    if (!selected || isZoomRowVisible(selected, viewport)) {
      return;
    }

    const currentRows = fullyVisibleZoomRows(viewport);
    if (currentRows.length === 0) {
      return;
    }

    selectZoomRow(delta > 0 ? currentRows[0] : currentRows[currentRows.length - 1], false, false);
  };

  const applyZoomFilters = () => {
    const master = zoomMaster?.value ?? "";
    const search = normalize(zoomSearch?.value);
    let visibleCount = 0;

    zoomRows.forEach((row) => {
      const masterOk = master === "" || row.dataset.masterCode === master;
      const textOk = search === "" || normalize(row.dataset.filterText).includes(search);
      const typeOk = typeMatches(row.dataset.accountType || "");
      row.hidden = !(masterOk && textOk && typeOk);
      if (!row.hidden) {
        visibleCount += 1;
      }
    });

    if (zoomCount) {
      zoomCount.textContent = String(visibleCount);
    }
    if (zoomEmpty) {
      zoomEmpty.hidden = visibleCount !== 0;
    }

    applyZoomSort();

    if (!selectedZoomRow() || selectedZoomRow().hidden) {
      selectZoomRow(visibleZoomRows()[0]);
    }
  };

  const chooseZoomRow = () => {
    const row = selectedZoomRow();
    const select = activeRow?.querySelector("[data-account-select]");
    if (!row || !select) {
      return;
    }

    const returnFocus = activeRow.querySelector("[data-account-add]");
    select.value = row.dataset.accountCode || "";
    updateRow(activeRow);
    closeZoom();
    returnFocus?.focus();
  };

  const openZoom = (row) => {
    if (!zoom) {
      return;
    }

    activeRow = row;
    zoom.hidden = false;
    zoomSearch.value = "";
    zoomMaster.value = "";
    activeType = "all";
    typeButtons.forEach((button) => {
      button.classList.toggle("is-active", button.dataset.accountTypeFilter === "all");
    });
    applyZoomFilters();
    zoomLastScrollTop = zoomGrid?.scrollTop ?? 0;

    const currentCode = row.querySelector("[data-account-select]")?.value;
    const currentRow = currentCode
      ? zoomRows.find((candidate) => candidate.dataset.accountCode === currentCode)
      : null;
    selectZoomRow(currentRow || visibleZoomRows()[0]);
    zoomSearch?.focus();
  };

  const closeZoom = () => {
    if (!zoom) {
      return;
    }

    zoom.hidden = true;
    activeRow = null;
  };

  rows.forEach((row) => {
    const select = row.querySelector("[data-account-select]");
    const add = row.querySelector("[data-account-add]");
    const clear = row.querySelector("[data-account-clear]");

    select?.addEventListener("change", () => updateRow(row));

    add?.addEventListener("click", () => {
      openZoom(row);
    });

    clear?.addEventListener("click", () => {
      if (!select) {
        return;
      }

      select.value = "";
      updateRow(row);
      add?.focus();
    });

    updateRow(row);
  });

  enableSaveButton();
  window.MicronoteProgress?.hide?.();
  window.addEventListener("pageshow", enableSaveButton);

  saveButton?.addEventListener("click", () => {
    enableSaveButton();

    const requiredValid = validateRequiredFields();
    const accountSectionsValid = validateAccountSections();

    if (!requiredValid || !accountSectionsValid) {
      const syntheticEvent = {
        preventDefault() {},
        stopPropagation() {},
        stopImmediatePropagation() {}
      };
      stopInvalidSubmit(syntheticEvent);
      return;
    }

    showProgressAndSubmit();
  });

  document.addEventListener("submit", (event) => {
    if (event.target !== form) {
      return;
    }

    if (!validateAccountSections()) {
      stopInvalidSubmit(event);
    }
  }, true);

  zoomRows.forEach((row) => {
    row.addEventListener("click", () => selectZoomRow(row, true));
    row.addEventListener("dblclick", chooseZoomRow);
    row.addEventListener("keydown", (event) => {
      const currentRows = visibleZoomRows();
      const index = Math.max(currentRows.indexOf(row), 0);

      if (event.key === "Enter") {
        event.preventDefault();
        chooseZoomRow();
      } else if (event.key === "ArrowDown") {
        event.preventDefault();
        selectZoomRow(currentRows[Math.min(index + 1, currentRows.length - 1)], true);
      } else if (event.key === "ArrowUp") {
        event.preventDefault();
        selectZoomRow(currentRows[Math.max(index - 1, 0)], true);
      }
    });
  });

  zoomSortHeaders.forEach((header) => {
    header.addEventListener("click", () => {
      const key = header.dataset.accountZoomSort;
      if (!key) {
        return;
      }

      zoomSort = {
        key,
        direction: zoomSort.key === key && zoomSort.direction === "asc" ? "desc" : "asc",
      };
      applyZoomSort();
      selectZoomRow(visibleZoomRows()[0], true);
    });
    header.addEventListener("keydown", (event) => {
      if (event.key !== "Enter" && event.key !== " ") {
        return;
      }

      event.preventDefault();
      header.click();
    });
  });

  typeButtons.forEach((button) => {
    button.addEventListener("click", () => {
      activeType = button.dataset.accountTypeFilter || "all";
      typeButtons.forEach((candidate) => candidate.classList.toggle("is-active", candidate === button));
      zoomMaster.value = "";
      zoomSearch.value = "";
      applyZoomFilters();
    });
  });

  zoomMaster?.addEventListener("change", applyZoomFilters);
  zoomSearch?.addEventListener("input", applyZoomFilters);
  zoomOk?.addEventListener("click", chooseZoomRow);
  zoomCancel?.addEventListener("click", closeZoom);
  zoomGrid?.addEventListener("scroll", () => {
    if (zoomScrollFrame) {
      window.cancelAnimationFrame(zoomScrollFrame);
    }

    zoomScrollFrame = window.requestAnimationFrame(() => {
      zoomScrollFrame = 0;
      const currentScrollTop = zoomGrid.scrollTop;
      const delta = currentScrollTop - zoomLastScrollTop;
      zoomLastScrollTop = currentScrollTop;

      selectZoomRowFromScroll(delta);
    });
  }, { passive: true });
  zoomGrid?.addEventListener("wheel", (event) => {
    if (zoomWheelFrame) {
      window.cancelAnimationFrame(zoomWheelFrame);
    }

    const delta = event.deltaY;
    zoomWheelFrame = window.requestAnimationFrame(() => {
      zoomWheelFrame = window.requestAnimationFrame(() => {
        zoomWheelFrame = 0;
        selectZoomRowFromScroll(delta);
      });
    });
  }, { passive: true });
  applyZoomSort();

  zoom?.addEventListener("keydown", (event) => {
    if (event.key === "Escape") {
      event.preventDefault();
      closeZoom();
    }
  });
});
