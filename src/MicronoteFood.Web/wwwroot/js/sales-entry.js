document.addEventListener("DOMContentLoaded", () => {
  const grid = document.querySelector("[data-sales-grid]");
  const body = document.querySelector("[data-sales-body]");
  const form = document.getElementById("sales-entry-form");
  const summary = document.querySelector(".sales-entry-summary");
  const vatRate = Number.parseFloat(grid?.dataset.salesVatRate || "10") || 10;
  const rows = Array.from(document.querySelectorAll("[data-sales-row]"));
  const saveButton = document.querySelector("[data-sales-save]");
  const modalCancel = document.querySelector("[data-sales-modal-cancel='true']");
  const confirmOverwrite = document.querySelector("[data-sales-confirm-overwrite]");
  const maximumVisibleRows = 10;
  const minimumVisibleRows = 4;

  const parseMoney = (value) => window.MicronoteMoney?.parse(value) ?? 0;
  const moneyText = (value) => window.MicronoteMoney?.format(value) ?? "";

  const rowValue = (row, selector) => parseMoney(row.querySelector(selector)?.value);
  const setValue = (selector, value) => {
    const field = document.querySelector(selector);
    if (field) {
      field.value = moneyText(value);
    }
  };

  const setRowValue = (row, selector, value) => {
    const field = row.querySelector(selector);
    if (field) {
      field.value = moneyText(value);
    }
  };

  const showMessage = (message, variant = "error") => {
    window.MicronoteMessageBox?.show({
      title: "Micronote Food - attenzione",
      message,
      variant,
      okText: "OK"
    });
  };

  modalCancel?.addEventListener("click", (event) => {
    event.preventDefault();
    window.parent?.postMessage({ type: "micronote:sales-cancel" }, window.location.origin);
  });

  const movementDateDisplay = document.querySelector("[data-sales-movement-date-display]");
  const movementDateHidden = document.querySelector("[data-sales-movement-date-hidden]");
  let datePickerPanel = null;
  let activeDateField = null;
  let activeDateMonth = null;

  const pad2 = (value) => String(value).padStart(2, "0");
  const currentCentury = () => Math.floor(new Date().getFullYear() / 100) * 100;
  const formatDisplayDate = (date) =>
    `${pad2(date.getDate())}/${pad2(date.getMonth() + 1)}/${date.getFullYear()}`;
  const formatIsoDate = (date) =>
    `${date.getFullYear()}-${pad2(date.getMonth() + 1)}-${pad2(date.getDate())}`;

  const parseDisplayDate = (value) => {
    const digits = String(value || "").replace(/\D/g, "");
    if (digits.length !== 6 && digits.length !== 8) {
      return null;
    }

    const day = Number.parseInt(digits.slice(0, 2), 10);
    const month = Number.parseInt(digits.slice(2, 4), 10);
    const yearText = digits.slice(4);
    const year = yearText.length === 2
      ? currentCentury() + Number.parseInt(yearText, 10)
      : Number.parseInt(yearText, 10);
    const date = new Date(year, month - 1, day);

    return date.getFullYear() === year
      && date.getMonth() === month - 1
      && date.getDate() === day
      ? date
      : null;
  };

  const syncDateEmptyState = (field) => {
    field.classList.toggle("is-empty", !field.value.trim());
  };

  const syncMovementDate = (showError = false) => {
    if (!(movementDateDisplay instanceof HTMLInputElement)
      || !(movementDateHidden instanceof HTMLInputElement)) {
      return true;
    }

    const date = parseDisplayDate(movementDateDisplay.value);
    if (!date) {
      if (showError) {
        showMessage("Data movimento obbligatoria o non valida.");
        movementDateDisplay.focus();
        movementDateDisplay.select?.();
      }
      return false;
    }

    movementDateDisplay.value = formatDisplayDate(date);
    movementDateHidden.value = formatIsoDate(date);
    syncDateEmptyState(movementDateDisplay);
    return true;
  };

  const ensureDatePickerPanel = () => {
    if (datePickerPanel) {
      return datePickerPanel;
    }

    datePickerPanel = document.createElement("div");
    datePickerPanel.className = "micronote-date-picker";
    datePickerPanel.hidden = true;
    datePickerPanel.setAttribute("role", "dialog");
    datePickerPanel.setAttribute("aria-label", "Calendario");
    document.body.appendChild(datePickerPanel);
    return datePickerPanel;
  };

  const renderDatePicker = () => {
    if (!datePickerPanel || !activeDateMonth) {
      return;
    }

    const month = activeDateMonth.getMonth();
    const year = activeDateMonth.getFullYear();
    const selectedDate = activeDateField ? parseDisplayDate(activeDateField.value) : null;
    const firstDay = new Date(year, month, 1);
    const startOffset = (firstDay.getDay() + 6) % 7;
    const daysInMonth = new Date(year, month + 1, 0).getDate();
    const monthLabel = firstDay.toLocaleDateString("it-IT", { month: "long", year: "numeric" });
    const weekdays = ["L", "M", "M", "G", "V", "S", "D"];
    const cells = [];

    for (let index = 0; index < startOffset; index += 1) {
      cells.push('<span class="micronote-date-picker-empty"></span>');
    }

    for (let day = 1; day <= daysInMonth; day += 1) {
      const isSelected = selectedDate
        && selectedDate.getFullYear() === year
        && selectedDate.getMonth() === month
        && selectedDate.getDate() === day;
      cells.push(`<button type="button" class="${isSelected ? "is-selected" : ""}" data-date-picker-day="${day}">${day}</button>`);
    }

    datePickerPanel.innerHTML = `
      <div class="micronote-date-picker-head">
        <button type="button" data-date-picker-prev>&lt;</button>
        <strong>${monthLabel}</strong>
        <button type="button" data-date-picker-next>&gt;</button>
      </div>
      <div class="micronote-date-picker-weekdays">
        ${weekdays.map((day) => `<span>${day}</span>`).join("")}
      </div>
      <div class="micronote-date-picker-days">
        ${cells.join("")}
      </div>
    `;
  };

  const closeDatePicker = () => {
    if (datePickerPanel) {
      datePickerPanel.hidden = true;
    }
    activeDateField = null;
    activeDateMonth = null;
  };

  const openDatePicker = (field) => {
    const panel = ensureDatePickerPanel();
    const date = parseDisplayDate(field.value) || new Date();
    activeDateField = field;
    activeDateMonth = new Date(date.getFullYear(), date.getMonth(), 1);
    renderDatePicker();

    const rect = field.getBoundingClientRect();
    panel.style.left = `${Math.round(rect.left + window.scrollX)}px`;
    panel.style.top = `${Math.round(rect.bottom + window.scrollY + 4)}px`;
    panel.hidden = false;
  };

  const totals = {
    taxable: 0,
    exempt: 0,
    net: 0,
    vat: 0,
    total: 0,
    cash: 0,
    card: 0,
    tickets: 0,
    checks: 0,
    other: 0,
    suspended: 0,
    losses: 0
  };

  const resetTotals = () => {
    Object.keys(totals).forEach((key) => {
      totals[key] = 0;
    });
  };

  const addTotal = (key, value) => {
    totals[key] += value;
  };

  const calculateRow = (row) => {
    const taxableGross = rowValue(row, "[data-sales-taxable]");
    const exempt = rowValue(row, "[data-sales-exempt]");
    const card = rowValue(row, "[data-sales-card]");
    const tickets = rowValue(row, "[data-sales-tickets]");
    const checks = rowValue(row, "[data-sales-checks]");
    const other = rowValue(row, "[data-sales-other]");
    const suspended = rowValue(row, "[data-sales-suspended]");
    const losses = rowValue(row, "[data-sales-losses]");
    const netTaxable = taxableGross / (1 + vatRate / 100);
    const vat = taxableGross - netTaxable;
    const net = netTaxable + exempt;
    const total = taxableGross + exempt;
    const nonCash = card + tickets + checks + other + suspended + losses;
    const cash = Math.max(total - nonCash, 0);

    setRowValue(row, "[data-sales-other]", other);
    setRowValue(row, "[data-sales-net]", net);
    setRowValue(row, "[data-sales-vat]", vat);
    setRowValue(row, "[data-sales-total]", total);
    setRowValue(row, "[data-sales-cash]", cash);

    addTotal("taxable", taxableGross);
    addTotal("exempt", exempt);
    addTotal("net", net);
    addTotal("vat", vat);
    addTotal("total", total);
    addTotal("cash", cash);
    addTotal("card", card);
    addTotal("tickets", tickets);
    addTotal("checks", checks);
    addTotal("other", other);
    addTotal("suspended", suspended);
    addTotal("losses", losses);
  };

  const calculate = () => {
    resetTotals();
    rows.forEach(calculateRow);

    setValue("[data-sales-total-taxable]", totals.taxable);
    setValue("[data-sales-total-exempt]", totals.exempt);
    setValue("[data-sales-total-net]", totals.net);
    setValue("[data-sales-total-vat]", totals.vat);
    setValue("[data-sales-total-total]", totals.total);
    setValue("[data-sales-total-cash]", totals.cash);
    setValue("[data-sales-total-card]", totals.card);
    setValue("[data-sales-total-tickets]", totals.tickets);
    setValue("[data-sales-total-checks]", totals.checks);
    setValue("[data-sales-total-other]", totals.other);
    setValue("[data-sales-total-suspended]", totals.suspended);
    setValue("[data-sales-total-losses]", totals.losses);

    const otherIncome = totals.card + totals.tickets + totals.checks;
    const missingIncome = totals.suspended + totals.losses;
    const netIncome = totals.cash + totals.other + otherIncome;
    const balance = totals.total
      - (totals.cash + totals.card + totals.tickets + totals.checks + totals.other + totals.suspended + totals.losses);

    setValue("[data-sales-summary-sales]", totals.total);
    setValue("[data-sales-summary-cash]", totals.cash + totals.other);
    setValue("[data-sales-summary-other]", otherIncome);
    setValue("[data-sales-summary-missing]", missingIncome);
    setValue("[data-sales-summary-net]", netIncome);
    setValue("[data-sales-summary-balance]", balance);
  };

  const validateBeforeSubmit = () => {
    if (!syncMovementDate(true)) {
      return false;
    }

    calculate();

    if (totals.total <= 0) {
      showMessage("Totale vendita obbligatorio.");
      return false;
    }

    const balance = totals.total
      - (totals.cash + totals.card + totals.tickets + totals.checks + totals.other + totals.suspended + totals.losses);

    if (Math.abs(balance) > 0.01) {
      showMessage("La registrazione non quadra.");
      return false;
    }

    return true;
  };

  const updateVisibleRows = () => {
    if (!grid || !body || !rows.length) {
      return;
    }

    const rowHeight = rows[0].getBoundingClientRect().height || 24;
    const bodyTop = body.getBoundingClientRect().top;
    const footerHeight = grid.querySelector("tfoot")?.getBoundingClientRect().height || 26;
    const summaryHeight = summary?.getBoundingClientRect().height || 48;
    const bottomSpace = 34;
    const availableHeight = window.innerHeight - bodyTop - footerHeight - summaryHeight - bottomSpace;
    const calculatedRows = Math.floor(availableHeight / rowHeight);
    const visibleRows = Math.max(
      minimumVisibleRows,
      Math.min(maximumVisibleRows, calculatedRows)
    );

    body.style.setProperty("--sales-visible-rows", String(visibleRows));
    body.classList.toggle("is-scrollable", rows.length > visibleRows);
  };

  const editableInputs = () =>
    Array.from(document.querySelectorAll(".sales-entry-grid input:not([readonly]):not(:disabled)"));

  const focusInput = (input) => {
    if (!input) {
      return;
    }

    input.focus();
    input.select?.();
  };

  const moveToNextInput = (input) => {
    const inputs = editableInputs();
    const index = inputs.indexOf(input);
    if (index < 0) {
      return;
    }

    focusInput(inputs[index + 1] || inputs[0]);
  };

  const moveHorizontally = (input, direction) => {
    const inputs = editableInputs();
    const index = inputs.indexOf(input);
    if (index < 0) {
      return;
    }

    focusInput(inputs[index + direction]);
  };

  const moveToPreviousGridInput = (input) => {
    const inputs = editableInputs();
    const index = inputs.indexOf(input);
    if (index < 0) {
      return;
    }

    focusInput(inputs[index - 1] || inputs[inputs.length - 1]);
  };

  const moveVertically = (input, direction) => {
    const cell = input.closest("td");
    const row = input.closest("tr");
    const rowIndex = rows.indexOf(row);
    const cellIndex = cell ? Array.from(row.children).indexOf(cell) : -1;

    if (direction < 0 && rowIndex === 0) {
      moveHorizontally(input, -1);
      return;
    }

    const targetRow = rows[rowIndex + direction];

    if (!targetRow || cellIndex < 0) {
      return;
    }

    const targetInput = targetRow.children[cellIndex]?.querySelector("input:not([readonly])");
    focusInput(targetInput);
  };

  document.querySelectorAll(".sales-entry-grid input").forEach((input) => {
    input.addEventListener("keydown", (event) => {
      if (input.readOnly) {
        return;
      }

      if (event.key === "Enter") {
        event.preventDefault();
        input.blur();
        moveToNextInput(input);
        return;
      }

      if (event.key === "ArrowUp") {
        event.preventDefault();
        input.blur();
        moveToPreviousGridInput(input);
        return;
      }

      if (event.key === "ArrowDown") {
        event.preventDefault();
        input.blur();
        moveVertically(input, 1);
      }
    });

    input.addEventListener("blur", () => {
      if (!input.readOnly) {
        input.value = moneyText(parseMoney(input.value));
      }
      calculate();
    });
  });

  document.querySelectorAll("[data-sales-exempt]").forEach((input) => {
    input.addEventListener("input", () => {
      const row = input.closest("[data-sales-row]");
      if (!row) {
        return;
      }

      setRowValue(row, "[data-sales-other]", parseMoney(input.value));
    });
  });

  movementDateDisplay?.addEventListener("input", () => {
    const digits = movementDateDisplay.value.replace(/\D/g, "").slice(0, 8);
    const day = digits.slice(0, 2);
    const month = digits.slice(2, 4);
    const year = digits.slice(4);
    movementDateDisplay.value = [day, month, year].filter(Boolean).join("/");
    syncDateEmptyState(movementDateDisplay);
  });

  movementDateDisplay?.addEventListener("focusout", () => {
    syncMovementDate(false);
  });

  document.addEventListener("click", (event) => {
    const target = event.target;
    const dateButton = target.closest?.("[data-date-picker-button]");
    if (dateButton) {
      event.preventDefault();
      const field = dateButton.closest("[data-date-control]")?.querySelector(".micronote-date-input");
      if (field instanceof HTMLInputElement) {
        if (activeDateField === field && datePickerPanel && !datePickerPanel.hidden) {
          closeDatePicker();
          return;
        }

        openDatePicker(field);
      }
      return;
    }

    if (target.closest?.(".micronote-date-picker")) {
      const previousButton = target.closest("[data-date-picker-prev]");
      const nextButton = target.closest("[data-date-picker-next]");
      const dayButton = target.closest("[data-date-picker-day]");

      if (previousButton && activeDateMonth) {
        activeDateMonth = new Date(activeDateMonth.getFullYear(), activeDateMonth.getMonth() - 1, 1);
        renderDatePicker();
        return;
      }

      if (nextButton && activeDateMonth) {
        activeDateMonth = new Date(activeDateMonth.getFullYear(), activeDateMonth.getMonth() + 1, 1);
        renderDatePicker();
        return;
      }

      if (dayButton && activeDateField && activeDateMonth) {
        const day = Number.parseInt(dayButton.getAttribute("data-date-picker-day") ?? "0", 10);
        const date = new Date(activeDateMonth.getFullYear(), activeDateMonth.getMonth(), day);
        activeDateField.value = formatDisplayDate(date);
        syncMovementDate(false);
        closeDatePicker();
        activeDateField.focus();
        return;
      }
    }

    if (!target.closest?.("[data-date-control]")) {
      closeDatePicker();
    }
  });

  document.querySelectorAll("[data-page-message]").forEach((pageMessage) => {
    showMessage(
      pageMessage.dataset.messageText || "",
      pageMessage.dataset.messageVariant || "info"
    );
  });

  const duplicateMessage = document.querySelector("[data-sales-duplicate-message]");
  if (duplicateMessage && form && confirmOverwrite) {
    window.MicronoteProgress?.hide?.();
    window.MicronoteMessageBox?.show({
      mode: "confirm",
      variant: "confirm",
      title: "Micronote Food - attenzione",
      message: duplicateMessage.dataset.salesDuplicateMessage,
      okText: "Sovrascrivi",
      cancelText: "Annulla",
      onConfirm: () => {
        confirmOverwrite.value = "true";
        form.requestSubmit(saveButton);
      }
    });
  }

  saveButton?.addEventListener("click", (event) => {
    if (!validateBeforeSubmit()) {
      event.preventDefault();
      event.stopImmediatePropagation();
    }
  });

  updateVisibleRows();
  window.addEventListener("resize", updateVisibleRows);
  calculate();
});

