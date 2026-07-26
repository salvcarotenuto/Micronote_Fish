document.addEventListener("DOMContentLoaded", () => {
  const page = document.querySelector("[data-accounting-statement]");
  if (!page) {
    return;
  }

  const form = document.querySelector("[data-accounting-statement-filters]");
  const statementMode = page.dataset.statementMode || "account";
  const isPartyStatement = statementMode === "party";
  const messageTitle = page.dataset.messageTitle || "Scheda contabile";
  const codeInput = document.querySelector("[data-statement-account-code]");
  const descriptionInput = document.querySelector("[data-statement-account-description]");
  const partyTypeInput = document.querySelector("[data-statement-party-type]");
  const partyTypeDisplay = document.querySelector("[data-statement-party-type-display]");
  const dateFromInput = document.querySelector("[data-statement-date-from]");
  const dateToInput = document.querySelector("[data-statement-date-to]");
  const choice = document.querySelector("[data-statement-choice]");
  const lookup = document.querySelector("[data-account-lookup]");
  const accountsScript = document.querySelector("[data-accounting-statement-accounts]");
  const partiesScript = document.querySelector("[data-accounting-statement-parties]");
  const grid = document.querySelector(".accounting-statement-grid-frame");
  const table = document.querySelector(".accounting-statement-grid");
  const rows = Array.from(document.querySelectorAll("[data-accounting-statement-row]"));
  const articleModal = document.querySelector("[data-accounting-statement-article-modal]");
  const articleModalFrame = document.querySelector("[data-accounting-statement-article-modal-frame]");

  if (page.dataset.readOnlyNavigation === "true") {
    page.querySelectorAll("button").forEach((button) => {
      button.disabled = true;
    });
  }
  const accounts = JSON.parse(accountsScript?.textContent || "[]");
  const parties = JSON.parse(partiesScript?.textContent || "[]");

  const choiceFields = {
    code: choice?.querySelector("[data-choice-account-code]"),
    description: choice?.querySelector("[data-choice-account-description]"),
    address: choice?.querySelector("[data-choice-party-address]"),
    city: choice?.querySelector("[data-choice-party-city]"),
    postalCode: choice?.querySelector("[data-choice-party-postal-code]"),
    province: choice?.querySelector("[data-choice-party-province]"),
    taxCode: choice?.querySelector("[data-choice-party-tax-code]"),
    vatNumber: choice?.querySelector("[data-choice-party-vat-number]"),
    dateFrom: choice?.querySelector("[data-choice-date-from]"),
    dateFromDisplay: choice?.querySelector("[data-choice-date-from-display]"),
    dateTo: choice?.querySelector("[data-choice-date-to]"),
    dateToDisplay: choice?.querySelector("[data-choice-date-to-display]")
  };
  const lookupFields = {
    body: lookup?.querySelector("[data-account-lookup-body]"),
    master: lookup?.querySelector("[data-account-master]"),
    search: lookup?.querySelector("[data-account-search]"),
    count: lookup?.querySelector("[data-account-count]")
  };
  const choiceLookupField = choice?.querySelector("[data-lookup-field]");

  let lookupRows = [...accounts];
  let lookupSelectedIndex = -1;
  let lookupType = "";
  let activeDateField = null;
  let activeDateMonth = null;
  let datePickerPanel = null;
  let activePartyType = partyTypeInput?.value || "F";
  let currentChoice = {
    partyType: partyTypeInput?.value || "F",
    code: codeInput?.value || "",
    description: descriptionInput?.value || "",
    dateFrom: dateFromInput?.value || "",
    dateTo: dateToInput?.value || ""
  };

  const normalize = (value) =>
    String(value ?? "")
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .toLowerCase();

  const escapeHtml = (value) => String(value ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");

  const accountByCode = (code) => {
    const number = Number.parseInt(String(code ?? "").trim(), 10);
    if (!Number.isFinite(number)) {
      return null;
    }
    if (isPartyStatement) {
      const party = parties.find((row) =>
        row.type === activePartyType &&
        Number(row.code) === number);
      return party
        ? {
            accountCode: party.code,
            accountDescription: party.name,
            party
          }
        : null;
    }
    return accounts.find((account) => Number(account.accountCode) === number) ?? null;
  };

  const formatCode = (value) =>
    String(Number.parseInt(value, 10) || 0).padStart(isPartyStatement ? 5 : 3, "0");

  const padDatePart = (value) => String(value).padStart(2, "0");

  const isRealDateParts = (day, month, year) => {
    const date = new Date(year, month - 1, day);
    return date.getFullYear() === year &&
      date.getMonth() === month - 1 &&
      date.getDate() === day;
  };

  const toIsoDate = (value) => {
    const text = String(value ?? "").trim();
    const isoMatch = /^(\d{4})-(\d{2})-(\d{2})$/.exec(text);
    if (isoMatch) {
      const year = Number.parseInt(isoMatch[1], 10);
      const month = Number.parseInt(isoMatch[2], 10);
      const day = Number.parseInt(isoMatch[3], 10);
      return isRealDateParts(day, month, year) ? text : "";
    }

    const match = /^(\d{1,2})[/-](\d{1,2})[/-](\d{2}|\d{4})$/.exec(text);
    if (!match) {
      return "";
    }

    const day = Number.parseInt(match[1], 10);
    const month = Number.parseInt(match[2], 10);
    const currentCentury = Math.floor(new Date().getFullYear() / 100) * 100;
    const year = match[3].length === 2
      ? currentCentury + Number.parseInt(match[3], 10)
      : Number.parseInt(match[3], 10);

    return isRealDateParts(day, month, year)
      ? `${year}-${padDatePart(month)}-${padDatePart(day)}`
      : "";
  };

  const toDisplayDate = (value) => {
    const iso = toIsoDate(value);
    if (!iso) {
      return "";
    }

    const [year, month, day] = iso.split("-");
    return `${day}/${month}/${year}`;
  };

  const dateFromField = (field) => {
    const iso = toIsoDate(field?.value);
    if (!iso) {
      return null;
    }

    const [year, month, day] = iso.split("-").map((part) => Number.parseInt(part, 10));
    return new Date(year, month - 1, day);
  };

  const formatDisplayDate = (date) =>
    `${padDatePart(date.getDate())}/${padDatePart(date.getMonth() + 1)}/${date.getFullYear()}`;

  const clampDay = (day, month, year) =>
    Math.min(day, new Date(year, month, 0).getDate());

  const datePartRanges = {
    day: [0, 2],
    month: [3, 5],
    year: [6, 10]
  };

  const selectDatePart = (field, part) => {
    const range = datePartRanges[part] ?? datePartRanges.day;
    window.setTimeout(() => field.setSelectionRange(range[0], range[1]), 0);
  };

  const datePartFromCaret = (field) => {
    const position = field.selectionStart ?? 0;
    if (position >= 6) {
      return "year";
    }
    if (position >= 3) {
      return "month";
    }
    return "day";
  };

  const shiftDatePart = (field, part, delta) => {
    const iso = toIsoDate(field.value);
    if (!iso) {
      return false;
    }

    let [year, month, day] = iso.split("-").map((value) => Number.parseInt(value, 10));
    if (part === "day") {
      day = Math.max(1, Math.min(day + delta, new Date(year, month, 0).getDate()));
    } else if (part === "month") {
      month += delta;
      if (month < 1) {
        month = 12;
        year -= 1;
      } else if (month > 12) {
        month = 1;
        year += 1;
      }
      day = clampDay(day, month, year);
    } else if (part === "year") {
      year += delta;
      day = clampDay(day, month, year);
    }

    field.value = `${padDatePart(day)}/${padDatePart(month)}/${year}`;
    syncDateHidden(field);
    selectDatePart(field, part);
    return true;
  };

  const buildDatePickerPanel = () => {
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

  const closeDatePicker = () => {
    if (!datePickerPanel) {
      return;
    }

    datePickerPanel.hidden = true;
    activeDateField = null;
  };

  const renderDatePicker = () => {
    const panel = buildDatePickerPanel();
    if (!activeDateField || !activeDateMonth) {
      panel.hidden = true;
      return;
    }

    const selectedDate = dateFromField(activeDateField);
    const year = activeDateMonth.getFullYear();
    const month = activeDateMonth.getMonth();
    const monthLabel = activeDateMonth.toLocaleDateString("it-IT", { month: "long", year: "numeric" });
    const firstDay = new Date(year, month, 1);
    const startOffset = (firstDay.getDay() + 6) % 7;
    const daysInMonth = new Date(year, month + 1, 0).getDate();
    const weekdays = ["Lu", "Ma", "Me", "Gi", "Ve", "Sa", "Do"];
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

    panel.innerHTML = `
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

  const positionDatePicker = () => {
    if (!activeDateField || !datePickerPanel) {
      return;
    }

    const rect = activeDateField.getBoundingClientRect();
    datePickerPanel.style.left = `${Math.max(8, rect.right - datePickerPanel.offsetWidth + window.scrollX)}px`;
    datePickerPanel.style.top = `${rect.bottom + 4 + window.scrollY}px`;
  };

  const openDatePicker = (field) => {
    activeDateField = field;
    const baseDate = dateFromField(field) ?? new Date();
    activeDateMonth = new Date(baseDate.getFullYear(), baseDate.getMonth(), 1);
    const panel = buildDatePickerPanel();
    renderDatePicker();
    panel.hidden = false;
    positionDatePicker();
  };

  const normalizeDateInput = (field) => {
    const digits = field.value.replace(/\D/g, "").slice(0, 8);
    let text = digits;
    if (digits.length > 4) {
      text = `${digits.slice(0, 2)}/${digits.slice(2, 4)}/${digits.slice(4)}`;
    } else if (digits.length > 2) {
      text = `${digits.slice(0, 2)}/${digits.slice(2)}`;
    }
    field.value = text.slice(0, 10);
  };

  const syncDateHidden = (field) => {
    const hidden = field.closest("[data-date-control]")?.querySelector("[data-statement-date-hidden]");
    const iso = toIsoDate(field.value);
    if (!hidden || !iso) {
      return false;
    }

    field.value = toDisplayDate(iso);
    hidden.value = iso;
    return true;
  };

  const showMessage = (message, afterClose) => {
    if (window.MicronoteMessageBox?.show) {
      window.MicronoteMessageBox.show({
        title: messageTitle,
        message,
        onConfirm: afterClose
      });
    } else {
      window.alert(message);
      afterClose?.();
    }
  };

  const fillChoice = (account) => {
    if (!account) {
      if (choiceFields.description) {
        choiceFields.description.value = "";
      }
      [choiceFields.address, choiceFields.city, choiceFields.postalCode, choiceFields.province,
       choiceFields.taxCode, choiceFields.vatNumber].forEach((field) => {
        if (field) {
          field.value = "";
        }
      });
      return;
    }
    choiceFields.code.value = formatCode(account.accountCode);
    choiceFields.description.value = account.accountDescription ?? "";
    if (isPartyStatement) {
      const party = account.party ?? {};
      if (choiceFields.address) {
        choiceFields.address.value = party.address ?? "";
      }
      if (choiceFields.city) {
        choiceFields.city.value = party.city ?? "";
      }
      if (choiceFields.postalCode) {
        choiceFields.postalCode.value = party.postalCode ?? "";
      }
      if (choiceFields.province) {
        choiceFields.province.value = party.province ?? "";
      }
      if (choiceFields.taxCode) {
        choiceFields.taxCode.value = party.taxCode ?? "";
      }
      if (choiceFields.vatNumber) {
        choiceFields.vatNumber.value = party.vatNumber ?? "";
      }
    }
  };

  const validateChoiceAccount = (showError = false) => {
    const account = accountByCode(choiceFields.code?.value);
    if (!account) {
      if (choiceFields.description) {
        choiceFields.description.value = "";
      }
      if (showError) {
        showMessage(isPartyStatement ? "Ditta non trovata." : "Conto non trovato.", () => {
          choiceFields.code?.focus();
          choiceFields.code?.select?.();
        });
      }
      return null;
    }

    fillChoice(account);
    return account;
  };

  const openChoice = () => {
    if (!choice) {
      return;
    }

    choice.hidden = false;
    document.body.classList.add("lookup-open");
    currentChoice = {
      partyType: partyTypeInput?.value || currentChoice.partyType || "F",
      code: codeInput?.value || currentChoice.code,
      description: descriptionInput?.value || currentChoice.description,
      dateFrom: dateFromInput?.value || currentChoice.dateFrom,
      dateTo: dateToInput?.value || currentChoice.dateTo
    };
    activePartyType = currentChoice.partyType || "F";
    refreshPartyTypeButtons();
    choiceFields.code.value = currentChoice.code;
    choiceFields.description.value = currentChoice.description;
    if (isPartyStatement) {
      fillChoice(accountByCode(choiceFields.code.value));
    }
    choiceFields.dateFrom.value = currentChoice.dateFrom;
    if (choiceFields.dateFromDisplay) {
      choiceFields.dateFromDisplay.value = toDisplayDate(choiceFields.dateFrom.value);
    }
    choiceFields.dateTo.value = currentChoice.dateTo;
    if (choiceFields.dateToDisplay) {
      choiceFields.dateToDisplay.value = toDisplayDate(choiceFields.dateTo.value);
    }
    window.setTimeout(() => {
      choiceFields.code?.focus();
      choiceFields.code?.select?.();
    }, 0);
  };

  const closeChoice = (cancel = false) => {
    choice.hidden = true;
    document.body.classList.remove("lookup-open");
    if (cancel && page.dataset.isLoaded !== "true") {
      window.location.href = "/";
      return;
    }
    document.querySelector("[data-statement-change]")?.focus();
  };

  const confirmChoice = () => {
    const account = validateChoiceAccount(true);
    if (!account) {
      return;
    }
    if (choiceFields.dateFromDisplay && !syncDateHidden(choiceFields.dateFromDisplay)) {
      showMessage("Indicare una data iniziale valida.", () => choiceFields.dateFromDisplay?.focus());
      return;
    }
    if (choiceFields.dateToDisplay && !syncDateHidden(choiceFields.dateToDisplay)) {
      showMessage("Indicare una data finale valida.", () => choiceFields.dateToDisplay?.focus());
      return;
    }
    if (!choiceFields.dateFrom.value || !choiceFields.dateTo.value) {
      showMessage("Indicare le date di inizio e fine periodo.");
      return;
    }

    codeInput.value = formatCode(account.accountCode);
    descriptionInput.value = account.accountDescription ?? "";
    if (partyTypeInput) {
      partyTypeInput.value = activePartyType;
    }
    if (partyTypeDisplay) {
      partyTypeDisplay.value = activePartyType;
    }
    dateFromInput.value = choiceFields.dateFrom.value;
    dateToInput.value = choiceFields.dateTo.value;
    currentChoice = {
      partyType: activePartyType,
      code: codeInput.value,
      description: descriptionInput.value,
      dateFrom: dateFromInput.value,
      dateTo: dateToInput.value
    };
    closeChoice();
    form?.requestSubmit();
  };

  const renderLookup = () => {
    const master = lookupFields.master?.value || "";
    const search = normalize(lookupFields.search?.value || "");
    lookupRows = accounts.filter((account) => {
      const byType = !lookupType || account.typeCode === lookupType;
      const byMaster = !master || String(account.masterCode) === master;
      const bySearch = !search ||
        normalize(account.accountDescription).includes(search) ||
        normalize(account.masterDescription).includes(search) ||
        String(account.accountCode).includes(search);
      return byType && byMaster && bySearch;
    });

    lookupRows.sort((left, right) =>
      Number(left.masterCode) - Number(right.masterCode) ||
      Number(left.accountCode) - Number(right.accountCode));

    if (lookupFields.count) {
      lookupFields.count.textContent = String(lookupRows.length);
    }
    if (!lookupFields.body) {
      return;
    }
    lookupFields.body.innerHTML = lookupRows.map((account, index) => `
      <tr tabindex="0" data-account-index="${index}">
        <td>${escapeHtml(formatCode(account.masterCode))}</td>
        <td>${escapeHtml(account.masterDescription)}</td>
        <td>${escapeHtml(formatCode(account.accountCode))}</td>
        <td>${escapeHtml(account.accountDescription)}</td>
        <td>${escapeHtml(account.typeCode)}</td>
      </tr>`).join("");
    setLookupSelected(Math.min(Math.max(lookupSelectedIndex, 0), lookupRows.length - 1));
  };

  const ensureLookupSelectedVisible = (direction = 0) => {
    const row = lookup?.querySelector("[data-account-index].selected-row");
    const frame = row?.closest(".accounting-statement-account-grid-frame");
    const table = row?.closest("table");
    if (!row || !frame || !table) {
      return;
    }

    const headerHeight = table.tHead?.offsetHeight ?? 0;
    const visibleTop = frame.scrollTop + headerHeight;
    const visibleBottom = frame.scrollTop + frame.clientHeight;
    const rowTop = row.offsetTop;
    const rowBottom = rowTop + row.offsetHeight;

    if (rowTop >= visibleTop && rowBottom <= visibleBottom) {
      return;
    }

    if (direction >= 0 && rowBottom > visibleBottom) {
      frame.scrollTop = rowBottom - frame.clientHeight + 1;
      return;
    }

    if (direction <= 0 && rowTop < visibleTop) {
      frame.scrollTop = Math.max(rowTop - headerHeight - 1, 0);
    }
  };

  const lookupViewport = () => {
    const frame = lookup?.querySelector(".accounting-statement-account-grid-frame");
    const table = lookup?.querySelector(".accounting-statement-account-grid");
    const frameRect = frame?.getBoundingClientRect();
    const headerHeight = table?.tHead?.getBoundingClientRect().height ?? 0;
    if (!frameRect) {
      return null;
    }

    return {
      top: frameRect.top + headerHeight,
      bottom: frameRect.bottom
    };
  };

  const isLookupRowVisible = (row, viewport) => {
    const rowRect = row.getBoundingClientRect();
    return rowRect.bottom > viewport.top + 1 &&
      rowRect.top < viewport.bottom - 1;
  };

  const fullyVisibleLookupRows = (viewport) =>
    Array.from(lookupFields.body?.querySelectorAll("[data-account-index]") ?? [])
      .filter((row) => {
        const rowRect = row.getBoundingClientRect();
        return rowRect.top >= viewport.top + 1 &&
          rowRect.bottom <= viewport.bottom - 1;
      });

  const selectLookupVisibleRowFromScroll = (delta) => {
    if (delta === 0) {
      return;
    }

    const viewport = lookupViewport();
    const selected = lookup?.querySelector("[data-account-index].selected-row");
    if (!viewport || !selected || isLookupRowVisible(selected, viewport)) {
      return;
    }

    const rowsInView = fullyVisibleLookupRows(viewport);
    if (!rowsInView.length) {
      return;
    }

    const row = delta > 0 ? rowsInView[0] : rowsInView[rowsInView.length - 1];
    setLookupSelected(Number(row.dataset.accountIndex), delta > 0 ? 1 : -1);
  };

  const setLookupSelected = (index, direction = 0) => {
    if (!lookupRows.length) {
      lookupSelectedIndex = -1;
      return;
    }
    lookupSelectedIndex = Math.max(0, Math.min(index, lookupRows.length - 1));
    lookup?.querySelectorAll("[data-account-index]").forEach((row) => {
      const selected = Number(row.dataset.accountIndex) === lookupSelectedIndex;
      row.classList.toggle("selected-row", selected);
      row.setAttribute("aria-selected", String(selected));
    });
    ensureLookupSelectedVisible(direction);
  };

  const openLookup = () => {
    lookup.hidden = false;
    document.body.classList.add("lookup-open");
    lookupSelectedIndex = 0;
    refreshLookupTypeButtons();
    renderLookup();
    lookupLastScrollTop = lookup?.querySelector(".accounting-statement-account-grid-frame")?.scrollTop ?? 0;
    window.setTimeout(() => lookupFields.search?.focus(), 0);
  };

  const closeLookup = () => {
    if (!lookup) {
      return;
    }
    lookup.hidden = true;
    choiceFields.code?.focus();
  };

  const confirmLookup = () => {
    const account = lookupRows[lookupSelectedIndex];
    if (!account) {
      return;
    }
    fillChoice(account);
    closeLookup();
  };

  const refreshLookupTypeButtons = () => {
    lookup?.querySelectorAll("[data-account-type]").forEach((button) => {
      const isSelected = (button.dataset.accountType || "") === lookupType;
      button.classList.toggle("is-selected", isSelected);
      button.setAttribute("aria-pressed", String(isSelected));
    });
  };

  const refreshPartyTypeButtons = () => {
    choice?.querySelectorAll("[data-choice-party-type]").forEach((button) => {
      const isSelected = button.dataset.choicePartyType === activePartyType;
      button.classList.toggle("is-selected", isSelected);
      button.setAttribute("aria-pressed", String(isSelected));
    });
    syncPartyLookupType();
  };

  function syncPartyLookupType() {
    if (!choiceLookupField) {
      return;
    }

    const isSupplier = activePartyType === "F";
    choiceLookupField.dataset.lookupType = isSupplier ? "fornitori" : "clienti";
    choiceLookupField.dataset.lookupTitle = isSupplier ? "Fornitori" : "Clienti";

    const lookupButton = choiceLookupField.querySelector("[data-lookup-open]");
    if (lookupButton) {
      const title = isSupplier ? "Seleziona fornitore" : "Seleziona cliente";
      lookupButton.title = title;
      lookupButton.setAttribute("aria-label", title);
    }
  }

  const choiceCycleFields = () => [
    choiceFields.code,
    choiceFields.dateFromDisplay,
    choiceFields.dateToDisplay,
    choice?.querySelector("[data-choice-confirm]"),
    choice?.querySelector("[data-choice-cancel]")
  ].filter((field) => field instanceof HTMLElement);

  const moveChoiceFocus = (currentField, direction = 1) => {
    const fields = choiceCycleFields();
    if (!fields.length) {
      return;
    }

    const index = Math.max(fields.indexOf(currentField), 0);
    const nextIndex = (index + direction + fields.length) % fields.length;
    const nextField = fields[nextIndex];
    nextField.focus();
    if (nextField instanceof HTMLInputElement) {
      nextField.select?.();
    }
    if (nextField.matches?.("[data-statement-date-display]") && toIsoDate(nextField.value)) {
      selectDatePart(nextField, "day");
    }
  };

  const focusChoiceDateFrom = () => {
    choiceFields.dateFromDisplay?.focus();
    if (choiceFields.dateFromDisplay instanceof HTMLInputElement) {
      choiceFields.dateFromDisplay.select?.();
      if (toIsoDate(choiceFields.dateFromDisplay.value)) {
        selectDatePart(choiceFields.dateFromDisplay, "day");
      }
    }
  };

  const visibleRows = () => rows.filter((row) => !row.hidden);
  const selectedRow = () => table?.querySelector(".selected-row");

  const gridViewport = () => {
    const gridRect = grid.getBoundingClientRect();
    const headerHeight = table.tHead?.getBoundingClientRect().height ?? 0;
    const footerHeight = table.tFoot?.getBoundingClientRect().height ?? 0;

    return {
      top: gridRect.top + headerHeight,
      bottom: gridRect.bottom - footerHeight
    };
  };

  const isRowVisible = (row, viewport) => {
    const rowRect = row.getBoundingClientRect();
    return rowRect.bottom > viewport.top + 1 &&
      rowRect.top < viewport.bottom - 1;
  };

  const fullyVisibleRows = (viewport) =>
    visibleRows().filter((row) => {
      const rowRect = row.getBoundingClientRect();
      return rowRect.top >= viewport.top + 1 &&
        rowRect.bottom <= viewport.bottom - 1;
    });

  const ensureVisible = (row, direction = 0) => {
    if (!grid || !table || !row) {
      return;
    }
    const headerHeight = table.tHead?.offsetHeight ?? 0;
    const footerHeight = table.tFoot?.offsetHeight ?? 0;
    const visibleTop = grid.scrollTop + headerHeight;
    const visibleBottom = grid.scrollTop + grid.clientHeight - footerHeight;
    const rowTop = row.offsetTop;
    const rowBottom = rowTop + row.offsetHeight;

    if (rowTop >= visibleTop && rowBottom <= visibleBottom) {
      return;
    }
    if (direction >= 0 && rowBottom > visibleBottom) {
      grid.scrollTop = rowBottom - grid.clientHeight + footerHeight + 1;
    } else if (direction <= 0 && rowTop < visibleTop) {
      grid.scrollTop = Math.max(rowTop - headerHeight - 1, 0);
    }
  };

  const selectRow = (row, focus = false, direction = 0) => {
    if (!row) {
      return;
    }
    rows.forEach((candidate) => candidate.classList.remove("selected-row", "selected"));
    rows.forEach((candidate) => candidate.removeAttribute("aria-selected"));
    row.classList.add("selected-row", "selected");
    row.setAttribute("aria-selected", "true");
    if (focus) {
      row.focus({ preventScroll: true });
    }
    ensureVisible(row, direction);
  };

  const navigateGrid = (event) => {
    const currentRows = visibleRows();
    if (!currentRows.length) {
      return;
    }
    const current = selectedRow() ?? currentRows[0];
    const index = Math.max(currentRows.indexOf(current), 0);
    if (event.key === "ArrowDown") {
      event.preventDefault();
      selectRow(currentRows[Math.min(index + 1, currentRows.length - 1)], true, 1);
    } else if (event.key === "ArrowUp") {
      event.preventDefault();
      selectRow(currentRows[Math.max(index - 1, 0)], true, -1);
    } else if (event.key === "Home") {
      event.preventDefault();
      selectRow(currentRows[0], true, -1);
    } else if (event.key === "End") {
      event.preventDefault();
      selectRow(currentRows[currentRows.length - 1], true, 1);
    }
  };

  document.querySelector("[data-statement-change]")?.addEventListener("click", openChoice);
  choice?.querySelector("[data-choice-confirm]")?.addEventListener("click", confirmChoice);
  choice?.querySelector("[data-choice-cancel]")?.addEventListener("click", () => closeChoice(true));
  choice?.querySelector("[data-choice-account-lookup]")?.addEventListener("click", openLookup);
  choiceLookupField?.addEventListener("micronote:lookup-selected", (event) => {
    activePartyType = event.detail?.type === "fornitori" ? "F" : "C";
    refreshPartyTypeButtons();
    validateChoiceAccount(false);
    focusChoiceDateFrom();
  });
  choiceLookupField?.addEventListener("micronote:lookup-not-found", () => {
    fillChoice(null);
    choiceFields.code?.focus();
    choiceFields.code?.select?.();
  });
  choice?.querySelectorAll("[data-choice-party-type]").forEach((button) => {
    button.setAttribute("aria-pressed", String(button.dataset.choicePartyType === activePartyType));
    button.addEventListener("click", () => {
      activePartyType = button.dataset.choicePartyType || "F";
      refreshPartyTypeButtons();
      validateChoiceAccount(false);
      choiceFields.code?.focus();
      choiceFields.code?.select?.();
    });
  });
  choiceFields.code?.addEventListener("blur", () => validateChoiceAccount(false));
  choiceFields.code?.addEventListener("input", () => {
    const maxLength = isPartyStatement ? 5 : 3;
    choiceFields.code.value = choiceFields.code.value.replace(/\D/g, "").slice(0, maxLength);
  });
  choiceFields.code?.addEventListener("keydown", (event) => {
    if (event.key === "Enter" || event.key === "Tab") {
      event.preventDefault();
      if (validateChoiceAccount(true)) {
        if (event.shiftKey) {
          moveChoiceFocus(choiceFields.code, -1);
        } else {
          focusChoiceDateFrom();
        }
      }
    }
  });
  document.querySelectorAll("[data-statement-date-display]").forEach((field) => {
    field.addEventListener("input", () => normalizeDateInput(field));
    field.addEventListener("blur", () => syncDateHidden(field));
    field.addEventListener("focus", () => {
      if (toIsoDate(field.value)) {
        selectDatePart(field, "day");
      }
    });
    field.addEventListener("click", () => {
      if (toIsoDate(field.value)) {
        selectDatePart(field, datePartFromCaret(field));
      }
    });
    field.addEventListener("keydown", (event) => {
      if (event.key === "Enter") {
        syncDateHidden(field);
        if (choice?.contains(field)) {
          event.preventDefault();
          moveChoiceFocus(field, 1);
        }
      } else if (event.key === "Tab") {
        syncDateHidden(field);
        if (choice?.contains(field)) {
          event.preventDefault();
          moveChoiceFocus(field, event.shiftKey ? -1 : 1);
        }
      } else if (event.key === "ArrowUp" || event.key === "ArrowDown") {
        const part = datePartFromCaret(field);
        if (shiftDatePart(field, part, event.key === "ArrowUp" ? 1 : -1)) {
          event.preventDefault();
        }
      } else if (event.key === "ArrowLeft" || event.key === "ArrowRight") {
        const part = datePartFromCaret(field);
        const nextPart = event.key === "ArrowLeft"
          ? part === "year" ? "month" : "day"
          : part === "day" ? "month" : "year";
        event.preventDefault();
        selectDatePart(field, nextPart);
      }
    });
  });
  choice?.querySelectorAll("[data-choice-confirm], [data-choice-cancel]").forEach((button) => {
    button.addEventListener("keydown", (event) => {
      if (event.key === "Tab") {
        event.preventDefault();
        moveChoiceFocus(button, event.shiftKey ? -1 : 1);
      }
    });
  });

  document.addEventListener("click", (event) => {
    const target = event.target;
    if (!(target instanceof Element)) {
      return;
    }

    const dateButton = target.closest("[data-date-picker-button]");
    if (dateButton) {
      event.preventDefault();
      const field = dateButton.closest("[data-date-control]")?.querySelector("[data-statement-date-display]");
      if (field instanceof HTMLInputElement) {
        if (activeDateField === field && datePickerPanel && !datePickerPanel.hidden) {
          closeDatePicker();
          return;
        }

        openDatePicker(field);
        field.focus();
      }
      return;
    }

    if (target.closest(".micronote-date-picker")) {
      const previousButton = target.closest("[data-date-picker-prev]");
      const nextButton = target.closest("[data-date-picker-next]");
      const dayButton = target.closest("[data-date-picker-day]");

      if (previousButton && activeDateMonth) {
        activeDateMonth = new Date(activeDateMonth.getFullYear(), activeDateMonth.getMonth() - 1, 1);
        renderDatePicker();
        positionDatePicker();
        return;
      }

      if (nextButton && activeDateMonth) {
        activeDateMonth = new Date(activeDateMonth.getFullYear(), activeDateMonth.getMonth() + 1, 1);
        renderDatePicker();
        positionDatePicker();
        return;
      }

      if (dayButton && activeDateField && activeDateMonth) {
        const day = Number.parseInt(dayButton.getAttribute("data-date-picker-day") ?? "0", 10);
        activeDateField.value = formatDisplayDate(new Date(activeDateMonth.getFullYear(), activeDateMonth.getMonth(), day));
        syncDateHidden(activeDateField);
        activeDateField.dispatchEvent(new Event("change", { bubbles: true }));
        closeDatePicker();
      }

      return;
    }

    if (!target.closest("[data-date-control]")) {
      closeDatePicker();
    }
  });

  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape" && datePickerPanel && !datePickerPanel.hidden) {
      event.preventDefault();
      closeDatePicker();
      activeDateField?.focus();
    }
  });

  window.addEventListener("resize", positionDatePicker);
  window.addEventListener("scroll", positionDatePicker, true);

  lookup?.querySelector("[data-account-ok]")?.addEventListener("click", confirmLookup);
  lookup?.querySelector("[data-account-cancel]")?.addEventListener("click", closeLookup);
  lookupFields.master?.addEventListener("change", renderLookup);
  lookupFields.search?.addEventListener("input", renderLookup);
  lookup?.querySelectorAll("[data-account-type]").forEach((button) => {
    button.setAttribute("aria-pressed", "false");
    button.addEventListener("click", () => {
      lookupType = button.dataset.accountType || "";
      lookupSelectedIndex = 0;
      refreshLookupTypeButtons();
      renderLookup();
    });
  });
  lookupFields.body?.addEventListener("click", (event) => {
    const row = event.target.closest("[data-account-index]");
    if (!row) {
      return;
    }
    const nextIndex = Number(row.dataset.accountIndex);
    setLookupSelected(nextIndex, nextIndex >= lookupSelectedIndex ? 1 : -1);
  });
  lookupFields.body?.addEventListener("dblclick", confirmLookup);
  const lookupGridFrame = lookup?.querySelector(".accounting-statement-account-grid-frame");
  let lookupLastScrollTop = lookupGridFrame?.scrollTop ?? 0;
  let lookupScrollFrame = 0;
  lookupGridFrame?.addEventListener("scroll", () => {
    if (lookupScrollFrame) {
      window.cancelAnimationFrame(lookupScrollFrame);
    }

    lookupScrollFrame = window.requestAnimationFrame(() => {
      lookupScrollFrame = 0;
      const currentScrollTop = lookupGridFrame.scrollTop;
      const delta = currentScrollTop - lookupLastScrollTop;
      lookupLastScrollTop = currentScrollTop;
      selectLookupVisibleRowFromScroll(delta);
    });
  }, { passive: true });
  lookup?.addEventListener("keydown", (event) => {
    if (event.key === "ArrowDown") {
      event.preventDefault();
      setLookupSelected(lookupSelectedIndex + 1, 1);
    } else if (event.key === "ArrowUp") {
      event.preventDefault();
      setLookupSelected(lookupSelectedIndex - 1, -1);
    } else if (event.key === "Home") {
      event.preventDefault();
      setLookupSelected(0, -1);
    } else if (event.key === "End") {
      event.preventDefault();
      setLookupSelected(lookupRows.length - 1, 1);
    } else if (event.key === "Enter") {
      event.preventDefault();
      confirmLookup();
    } else if (event.key === "Escape") {
      event.preventDefault();
      closeLookup();
    }
  });

  rows.forEach((row) => {
    row.addEventListener("click", () => selectRow(row, true));
    row.addEventListener("keydown", navigateGrid);
  });

  grid?.addEventListener("keydown", (event) => {
    if (event.target.closest("[data-accounting-statement-row]")) {
      return;
    }

    const row = selectedRow() || visibleRows()[0];
    if (row) {
      navigateGrid(event);
    }
  });

  if (grid && table && rows.length > 0) {
    let lastScrollTop = grid.scrollTop;
    let scrollFrame = 0;

    grid.addEventListener("scroll", () => {
      if (scrollFrame) {
        window.cancelAnimationFrame(scrollFrame);
      }

      scrollFrame = window.requestAnimationFrame(() => {
        scrollFrame = 0;
        const currentScrollTop = grid.scrollTop;
        const delta = currentScrollTop - lastScrollTop;
        lastScrollTop = currentScrollTop;

        if (delta === 0) {
          return;
        }

        const viewport = gridViewport();
        const selected = selectedRow();
        if (!selected || isRowVisible(selected, viewport)) {
          return;
        }

        const currentRows = fullyVisibleRows(viewport);
        if (!currentRows.length) {
          return;
        }

        selectRow(delta > 0 ? currentRows[0] : currentRows[currentRows.length - 1]);
      });
    }, { passive: true });
  }

  const closeArticleModal = () => {
    if (!articleModal) {
      return;
    }

    articleModal.hidden = true;
    document.body.classList.remove("purchase-invoice-payment-modal-open");
    articleModalFrame?.removeAttribute("src");
    document.querySelector("[data-accounting-statement-action='article']")?.focus();
  };

  const openArticleModal = (row) => {
    const url = `/ArticoloPrimaNota?id=${encodeURIComponent(row.dataset.id || "")}`;
    if (!articleModal || !articleModalFrame) {
      window.location.href = url;
      return;
    }

    articleModalFrame.src = url;
    articleModal.hidden = false;
    document.body.classList.add("purchase-invoice-payment-modal-open");
    articleModalFrame.focus();
  };

  window.addEventListener("message", (event) => {
    if (event.origin === window.location.origin && event.data?.type === "micronote:accounting-article:close") {
      closeArticleModal();
    }
  });

  if (rows.length) {
    selectRow(rows[0]);
  }

  document.querySelectorAll("[data-accounting-statement-action]").forEach((button) => {
    button.addEventListener("click", () => {
      const action = button.dataset.accountingStatementAction;
      const row = selectedRow();
      if (!row || !row.dataset.id) {
        showMessage("Selezionare un movimento.");
        return;
      }
      if (action === "article") {
        openArticleModal(row);
        return;
      }

      showMessage(action === "print"
        ? "Stampa scheda contabile. Funzione in preparazione."
        : "Visualizzazione movimento. Funzione in preparazione.");
    });
  });

  if (page.dataset.isLoaded !== "true") {
    openChoice();
  }

  document.addEventListener("keydown", (event) => {
    if (event.key !== "Escape" || event.defaultPrevented) {
      return;
    }

    event.preventDefault();
    window.location.href = page.dataset.returnUrl || "/";
  });
});
