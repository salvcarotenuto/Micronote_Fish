document.addEventListener("DOMContentLoaded", () => {
  const form = document.getElementById("accounting-movement-form");
  const rows = Array.from(document.querySelectorAll("[data-account-row]"));
  const saveButton = document.querySelector("[data-accounting-movement-save]");
  const causeCode = document.querySelector("[data-movement-cause-code]");
  const causeDescription = document.querySelector("[data-movement-cause-description]");
  const causeSource = document.querySelector("[data-movement-cause-source]");
  const causeLookupOpen = document.querySelector("[data-movement-cause-lookup-open]");
  const movementDate = document.querySelector("[data-movement-date]");
  const movementDateDisplay = document.querySelector("[data-movement-date-display]");
  const movementYear = document.querySelector("[data-movement-year]");
  const subjectType = document.querySelector("[data-movement-subject-type]");
  const subjectLabel = document.querySelector("[data-movement-subject-label]");
  const subjectField = document.querySelector("[data-movement-subject-code]")?.closest("[data-lookup-field]");
  const subjectCodeDisplay = document.querySelector("[data-movement-subject-code-display]");
  const subjectLookupOpen = subjectField?.querySelector("[data-lookup-open]");
  const linkedDocumentInputs = {
    invoice: {
      id: document.querySelector("[data-movement-linked-document-id='invoice']"),
      sector: document.querySelector("[data-movement-linked-document-sector='invoice']")
    },
    "due-date": {
      id: document.querySelector("[data-movement-linked-document-id='due-date']"),
      sector: document.querySelector("[data-movement-linked-document-sector='due-date']")
    }
  };
  const accessoryFields = Array.from(document.querySelectorAll("[data-movement-accessory]"));
  const amountInputs = Array.from(document.querySelectorAll("[data-line-amount]"));
  const totalAmount = document.querySelector("[data-movement-total-amount]");
  const totalAmountFill = document.querySelector("[data-movement-amount-fill]");
  const debitTotal = document.querySelector("[data-side-total='debit']");
  const creditTotal = document.querySelector("[data-side-total='credit']");
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
  const normalize = (value) =>
    String(value ?? "")
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .toLowerCase();

  const escapeHtml = (value) =>
    String(value ?? "").replace(/[&<>"']/g, (char) => ({
      "&": "&amp;",
      "<": "&lt;",
      ">": "&gt;",
      "\"": "&quot;",
      "'": "&#039;"
    }[char]));

  const parseMoney = (value) => window.MicronoteMoney?.parse(value) ?? 0;
  const formatMoney = (value) => window.MicronoteMoney?.format(value) ?? "";
  const formatMoneyForEdit = (value) => window.MicronoteMoney?.formatForEdit(value) ?? "";
  const cleanMoneyInput = (input) => window.MicronoteMoney?.clean(input);
  const padDatePart = (value) => String(value).padStart(2, "0");
  const currentCentury = () => Math.floor(new Date().getFullYear() / 100) * 100;
  let datePickerPanel = null;
  let activeDateField = null;
  let activeDateMonth = null;

  const formatDisplayDate = (date) =>
    `${padDatePart(date.getDate())}/${padDatePart(date.getMonth() + 1)}/${date.getFullYear()}`;

  const formatIsoDate = (date) =>
    `${date.getFullYear()}-${padDatePart(date.getMonth() + 1)}-${padDatePart(date.getDate())}`;

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
      || !(movementDate instanceof HTMLInputElement)) {
      return true;
    }

    const date = parseDisplayDate(movementDateDisplay.value);
    if (!date) {
      if (showError) {
        showValidationError("Campo Data movimento obbligatorio.");
        movementDateDisplay.focus();
        movementDateDisplay.select?.();
      }
      return false;
    }

    movementDateDisplay.value = formatDisplayDate(date);
    movementDate.value = formatIsoDate(date);
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

  const normalizeMoneyForSubmit = (input) => {
    window.MicronoteMoney?.normalizeForSubmit(input);
  };

  const formatCode = (value, length) => {
    const number = Number(String(value ?? "").replace(/\D/g, ""));
    return number > 0 ? String(number).padStart(length, "0") : "";
  };

  const selectedZoomRow = () => zoom?.querySelector("[data-zoom-row].selected-row");
  const currentZoomRows = () => Array.from(zoomBody?.querySelectorAll("[data-zoom-row]") ?? zoomRows);
  const visibleZoomRows = () => currentZoomRows().filter((row) => !row.hidden);

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
      [masterCode, masterDescription, accountCode, accountDescription].forEach((input) => {
        if (input) {
          input.value = "";
        }
      });
      return;
    }

    if (masterCode) {
      masterCode.value = formatCode(option.dataset.masterCode, 3);
    }
    if (masterDescription) {
      masterDescription.value = option.dataset.masterDescription || "";
    }
    if (accountCode) {
      accountCode.value = formatCode(option.dataset.accountCode, 3);
    }
    if (accountDescription) {
      accountDescription.value = option.dataset.accountDescription || "";
    }
  };

  const clearLine = (row) => {
    const select = row.querySelector("[data-account-select]");
    const amount = row.querySelector("[data-line-amount]");
    if (select) {
      select.value = "";
    }
    if (amount) {
      amount.value = "";
    }
    updateRow(row);
  };

  const rowsForSide = (side) =>
    rows.filter((row) => row.closest("[data-movement-side]")?.dataset.movementSide === side);

  const calculateTotals = () => {
    const totalFor = (side) =>
      rowsForSide(side).reduce((sum, row) => sum + parseMoney(row.querySelector("[data-line-amount]")?.value), 0);
    const debit = totalFor("debit");
    const credit = totalFor("credit");

    if (debitTotal) {
      debitTotal.value = formatMoney(debit);
    }
    if (creditTotal) {
      creditTotal.value = formatMoney(credit);
    }

    return { debit, credit };
  };

  const fillTotalAmountFromLines = () => {
    amountInputs.forEach((input) => {
      input.value = formatMoney(parseMoney(input.value));
    });

    const totals = calculateTotals();
    const debit = Math.round(totals.debit * 100);
    const credit = Math.round(totals.credit * 100);

    if (debit === 0 && credit === 0) {
      if (totalAmount) {
        totalAmount.value = "";
      }
      return;
    }

    if (debit === 0) {
      window.MicronoteMessageBox?.show({
        title: "Prima nota",
        message: "La sezione Dare non contiene importi.",
        variant: "error"
      });
      return;
    }

    if (credit === 0) {
      window.MicronoteMessageBox?.show({
        title: "Prima nota",
        message: "La sezione Avere non contiene importi.",
        variant: "error"
      });
      return;
    }

    if (debit !== credit) {
      window.MicronoteMessageBox?.show({
        title: "Prima nota",
        message: "Totale Dare e totale Avere non coincidono.",
        variant: "error"
      });
      return;
    }

    if (totalAmount) {
      totalAmount.value = formatMoney(totals.debit);
    }
    totalAmountFill?.focus();
  };

  const showValidationError = (message) => {
    if (saveButton) {
      saveButton.disabled = false;
    }
    window.MicronoteProgress?.hide?.();
    window.MicronoteValidationMessageBox?.enableSubmitters?.();
    if (window.MicronoteMessageBox?.show) {
      window.MicronoteMessageBox.show({
        title: "Prima nota",
        message,
        variant: "error"
      });
    }
  };

  const lineState = (row, index = 0) => {
    const accountCode = row.querySelector("[data-account-select]")?.value || "";
    const amount = parseMoney(row.querySelector("[data-line-amount]")?.value);
    return {
      hasAccount: Number(accountCode) > 0,
      hasAmount: amount !== 0,
      amount,
      rowNumber: index + 1
    };
  };

  const validateCompleteLines = (states, sideLabel) => {
    const accountWithoutAmount = states.find((state) => state.hasAccount && !state.hasAmount);
    if (accountWithoutAmount) {
      showValidationError(`Riga ${sideLabel} ${accountWithoutAmount.rowNumber}: indicare l'importo.`);
      return false;
    }

    const amountWithoutAccount = states.find((state) => !state.hasAccount && state.hasAmount);
    if (amountWithoutAccount) {
      showValidationError(`Riga ${sideLabel} ${amountWithoutAccount.rowNumber}: indicare il conto.`);
      return false;
    }

    return true;
  };

  const validateBeforeSubmit = () => {
    if (!syncMovementDate(true)) {
      return false;
    }

    const exerciseYear = Number(movementYear?.value || 0);
    const dateYear = Number(String(movementDate?.value ?? "").slice(0, 4));
    if (exerciseYear > 0 && dateYear > 0 && dateYear !== exerciseYear) {
      showValidationError("La data movimento non appartiene all'esercizio contabile in linea.");
      movementDateDisplay?.focus();
      return false;
    }

    if (!Number(String(causeCode?.value ?? "").replace(/\D/g, ""))) {
      showValidationError("Campo Causale obbligatorio.");
      causeCode?.focus();
      return false;
    }

    const total = parseMoney(totalAmount?.value);
    if (total <= 0) {
      showValidationError("Campo Importo movimento obbligatorio.");
      totalAmount?.focus();
      return false;
    }

    const debitStates = rowsForSide("debit").map(lineState);
    if (!validateCompleteLines(debitStates, "Dare")) {
      return false;
    }

    if (!debitStates.some((state) => state.hasAccount)) {
      showValidationError("Almeno un conto Dare obbligatorio.");
      return false;
    }

    if (!debitStates.some((state) => state.hasAmount)) {
      showValidationError("Almeno un importo Dare obbligatorio.");
      return false;
    }

    const creditStates = rowsForSide("credit").map(lineState);
    if (!validateCompleteLines(creditStates, "Avere")) {
      return false;
    }

    if (!creditStates.some((state) => state.hasAccount)) {
      showValidationError("Almeno un conto Avere obbligatorio.");
      return false;
    }

    if (!creditStates.some((state) => state.hasAmount)) {
      showValidationError("Almeno un importo Avere obbligatorio.");
      return false;
    }

    const totals = calculateTotals();
    const debit = Math.round(totals.debit * 100);
    const credit = Math.round(totals.credit * 100);
    if (debit !== credit) {
      showValidationError("Totale Dare e totale Avere non coincidono.");
      return false;
    }

    if (Math.round(total * 100) !== debit) {
      showValidationError("Importo movimento diverso dal totale Dare/Avere.");
      totalAmount?.focus();
      return false;
    }

    return true;
  };

  const causeOption = () => {
    const code = Number(String(causeCode?.value ?? "").replace(/\D/g, ""));
    if (!code) {
      return null;
    }

    return Array.from(causeSource?.options ?? [])
      .find((option) => Number(option.value) === code) ?? null;
  };

  const hasSelectedCause = () => causeOption() !== null;

  const showForeignKeyError = (title, message, focusTarget) => {
    window.MicronoteMessageBox?.show({
      title,
      message,
      variant: "error",
      onConfirm: () => window.setTimeout(() => focusTarget?.focus(), 0)
    });
  };

  const requireCauseBeforeSubject = () => {
    if (hasSelectedCause()) {
      return true;
    }

    showForeignKeyError(
      "Soggetto",
      "E' necessario selezionare una causale di movimento",
      causeCode);
    return false;
  };

  const validateTypedCauseCode = () => {
    const code = String(causeCode?.value || "").replace(/\D/g, "");
    if (Number(code) <= 0 || causeOption()) {
      return true;
    }

    causeCode.value = "";
    if (causeDescription) {
      causeDescription.value = "";
    }
    setSubjectLabel("");
    updateAccessoryFields(null);
    showForeignKeyError("Causale", "Causale inesistente.", causeCode);
    return false;
  };

  let causeLookup = null;
  let selectedCauseRow = null;
  let causeLookupSort = { key: "description", direction: "asc" };
  let causeLookupLastScrollTop = 0;
  let causeLookupScrollFrame = 0;
  let causeLookupWheelFrame = 0;

  const causeRows = () =>
    Array.from(causeSource?.options ?? [])
      .filter((option) => Number(option.value) > 0)
      .map((option) => ({
        code: Number(option.value),
        description: option.dataset.description || option.textContent.replace(/^\s*\d+\s*-\s*/, "").trim()
      }));

  const causeLookupFrame = () => causeLookup?.querySelector(".accounting-movement-cause-lookup-frame");
  const causeLookupTable = () => causeLookup?.querySelector(".accounting-movement-cause-lookup-table");
  const currentCauseRows = () => Array.from(causeLookup?.querySelectorAll("[data-cause-lookup-row]") ?? []);

  const causeLookupViewport = () => {
    const frame = causeLookupFrame();
    const table = causeLookupTable();
    const frameRect = frame?.getBoundingClientRect();
    const headerHeight = table?.tHead?.getBoundingClientRect().height ?? 0;

    return {
      top: (frameRect?.top ?? 0) + headerHeight,
      bottom: frameRect?.bottom ?? 0
    };
  };

  const isCauseRowVisible = (row, viewport) => {
    const rowRect = row.getBoundingClientRect();
    return rowRect.bottom > viewport.top + 1 &&
      rowRect.top < viewport.bottom - 1;
  };

  const fullyVisibleCauseRows = (viewport) =>
    currentCauseRows().filter((row) => {
      const rowRect = row.getBoundingClientRect();
      return rowRect.top >= viewport.top + 1 &&
        rowRect.bottom <= viewport.bottom - 1;
    });

  const ensureCauseRowVisible = (row, direction = 0) => {
    const frame = causeLookupFrame();
    const table = causeLookupTable();
    if (!frame || !table || !row) {
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

  const selectCauseRow = (row, focus = false, direction = 0) => {
    if (!row) {
      return;
    }

    causeLookup?.querySelectorAll("[data-cause-lookup-row]").forEach((candidate) => {
      candidate.classList.remove("selected-row");
      candidate.removeAttribute("aria-selected");
    });
    row.classList.add("selected-row");
    row.setAttribute("aria-selected", "true");
    selectedCauseRow = row;

    if (focus) {
      row.focus({ preventScroll: true });
    }
    ensureCauseRowVisible(row, direction);
  };

  const selectCauseRowFromScroll = (delta) => {
    if (delta === 0) {
      return;
    }

    const viewport = causeLookupViewport();
    const selected = selectedCauseRow;
    if (!selected || isCauseRowVisible(selected, viewport)) {
      return;
    }

    const rows = fullyVisibleCauseRows(viewport);
    if (rows.length === 0) {
      return;
    }

    selectCauseRow(delta > 0 ? rows[0] : rows[rows.length - 1]);
  };

  const scrollCauseRowIntoView = (row) => {
    const frame = causeLookup?.querySelector(".accounting-movement-cause-lookup-frame");
    if (!frame || !row) {
      return;
    }

    const rowTop = row.offsetTop;
    const rowHeight = row.offsetHeight || 0;
    frame.scrollTop = Math.max(0, rowTop - (frame.clientHeight / 2) + (rowHeight / 2));
  };

  const closeCauseLookup = () => {
    causeLookup?.classList.remove("is-open");
    document.body.classList.remove("lookup-open");
    selectedCauseRow = null;
  };

  const chooseCauseRow = () => {
    const row = selectedCauseRow;
    if (!row || !causeCode) {
      return;
    }

    causeCode.value = formatCode(row.dataset.causeCode, 3);
    applyCauseTemplate(true);
    closeCauseLookup();
    causeCode.focus();
  };

  const sortedCauseRows = () => {
    const direction = causeLookupSort.direction === "desc" ? -1 : 1;
    return causeRows().sort((left, right) => {
      if (causeLookupSort.key === "description") {
        return left.description.localeCompare(right.description, "it", { sensitivity: "base" }) * direction;
      }

      return (left.code - right.code) * direction;
    });
  };

  const renderCauseSortHeaders = () => {
    causeLookup?.querySelectorAll("[data-cause-lookup-sort]").forEach((button) => {
      const active = button.dataset.causeLookupSort === causeLookupSort.key;
      button.classList.toggle("is-active", active);
      button.dataset.direction = active ? causeLookupSort.direction : "";
    });
  };

  const renderCauseRows = () => {
    const body = causeLookup?.querySelector("tbody");
    if (!body) {
      return;
    }

    body.innerHTML = sortedCauseRows().map((row) => `
      <tr tabindex="0" data-cause-lookup-row data-cause-code="${row.code}">
        <td class="accounting-movement-cause-lookup-code">${formatCode(row.code, 3)}</td>
        <td>${escapeHtml(row.description)}</td>
      </tr>`).join("");

    body.querySelectorAll("[data-cause-lookup-row]").forEach((row) => {
      row.addEventListener("click", () => selectCauseRow(row, true));
      row.addEventListener("dblclick", chooseCauseRow);
    });
    renderCauseSortHeaders();
  };

  const selectCauseRowByFirstCharacter = (rows, key) => {
    const character = normalize(key).trim();
    if (!character || character.length !== 1) {
      return false;
    }

    const row = rows.find((candidate) => {
      const description = normalize(candidate.cells?.[1]?.textContent || "").trim();
      return description.startsWith(character);
    });

    if (!row) {
      return false;
    }

    selectCauseRow(row, true);
    window.requestAnimationFrame(() => scrollCauseRowIntoView(row));
    return true;
  };

  const buildCauseLookup = () => {
    if (causeLookup) {
      return causeLookup;
    }

    causeLookup = document.createElement("div");
    causeLookup.className = "accounting-movement-cause-lookup";
    causeLookup.innerHTML = `
      <div class="accounting-movement-cause-lookup-dialog" role="dialog" aria-modal="true" aria-labelledby="movement-cause-lookup-title">
        <div class="accounting-movement-cause-lookup-titlebar">
          <h2 id="movement-cause-lookup-title">Causali contabili</h2>
          <button class="accounting-movement-cause-lookup-close" type="button" aria-label="Chiudi" data-cause-lookup-close></button>
        </div>
        <div class="accounting-movement-cause-lookup-frame">
          <table class="accounting-movement-cause-lookup-table">
            <thead>
              <tr>
                <th class="accounting-movement-cause-lookup-code">
                  <button type="button" data-cause-lookup-sort="code">Codice</button>
                </th>
                <th>
                  <button type="button" data-cause-lookup-sort="description">Descrizione</button>
                </th>
              </tr>
            </thead>
            <tbody></tbody>
          </table>
        </div>
        <div class="accounting-movement-cause-lookup-actions">
          <button class="accounting-movement-cause-lookup-command" type="button" data-cause-lookup-ok>OK</button>
          <button class="accounting-movement-cause-lookup-command" type="button" data-cause-lookup-cancel>Annulla</button>
        </div>
      </div>`;
    document.body.appendChild(causeLookup);

    causeLookup.addEventListener("mousedown", (event) => {
      if (event.target === causeLookup) {
        closeCauseLookup();
      }
    });
    causeLookup.querySelector("[data-cause-lookup-close]")?.addEventListener("click", closeCauseLookup);
    causeLookup.querySelector("[data-cause-lookup-cancel]")?.addEventListener("click", closeCauseLookup);
    causeLookup.querySelector("[data-cause-lookup-ok]")?.addEventListener("click", chooseCauseRow);
    causeLookup.querySelectorAll("[data-cause-lookup-sort]").forEach((button) => {
      button.addEventListener("click", () => {
        const key = button.dataset.causeLookupSort;
        causeLookupSort = {
          key,
          direction: causeLookupSort.key === key && causeLookupSort.direction === "asc" ? "desc" : "asc"
        };
        renderCauseRows();
        selectCauseRow(causeLookup.querySelector("[data-cause-lookup-row]"), true);
      });
    });
    causeLookupFrame()?.addEventListener("scroll", () => {
      if (causeLookupScrollFrame) {
        window.cancelAnimationFrame(causeLookupScrollFrame);
      }

      causeLookupScrollFrame = window.requestAnimationFrame(() => {
        causeLookupScrollFrame = 0;
        const frame = causeLookupFrame();
        if (!frame) {
          return;
        }

        const currentScrollTop = frame.scrollTop;
        const delta = currentScrollTop - causeLookupLastScrollTop;
        causeLookupLastScrollTop = currentScrollTop;
        selectCauseRowFromScroll(delta);
      });
    }, { passive: true });
    causeLookupFrame()?.addEventListener("wheel", (event) => {
      if (causeLookupWheelFrame) {
        window.cancelAnimationFrame(causeLookupWheelFrame);
      }

      const delta = event.deltaY;
      causeLookupWheelFrame = window.requestAnimationFrame(() => {
        causeLookupWheelFrame = window.requestAnimationFrame(() => {
          causeLookupWheelFrame = 0;
          selectCauseRowFromScroll(delta);
        });
      });
    }, { passive: true });
    causeLookup.addEventListener("keydown", (event) => {
      const rows = Array.from(causeLookup.querySelectorAll("[data-cause-lookup-row]"));
      const currentIndex = Math.max(rows.indexOf(selectedCauseRow), 0);

      if (event.key === "Escape") {
        event.preventDefault();
        event.stopPropagation();
        closeCauseLookup();
      } else if (event.key === "Enter") {
        event.preventDefault();
        event.stopPropagation();
        chooseCauseRow();
      } else if (event.key === "ArrowDown") {
        event.preventDefault();
        selectCauseRow(rows[Math.min(currentIndex + 1, rows.length - 1)], true, 1);
      } else if (event.key === "ArrowUp") {
        event.preventDefault();
        selectCauseRow(rows[Math.max(currentIndex - 1, 0)], true, -1);
      } else if (!event.ctrlKey && !event.altKey && !event.metaKey && event.key.length === 1) {
        if (selectCauseRowByFirstCharacter(rows, event.key)) {
          event.preventDefault();
          event.stopPropagation();
        }
      }
    });

    return causeLookup;
  };

  const openCauseLookup = () => {
    const dialog = buildCauseLookup();
    const body = dialog.querySelector("tbody");
    if (!body) {
      return;
    }

    renderCauseRows();

    dialog.classList.add("is-open");
    document.body.classList.add("lookup-open");
    causeLookupLastScrollTop = causeLookupFrame()?.scrollTop ?? 0;
    const currentCode = String(Number(String(causeCode?.value ?? "").replace(/\D/g, "")));
    const currentRow = currentCode && currentCode !== "0"
      ? body.querySelector(`[data-cause-code="${currentCode}"]`)
      : null;
    selectCauseRow(currentRow || body.querySelector("[data-cause-lookup-row]"), true);
  };

  const setSubjectLabel = (subject, preserveSubject = false) => {
    const labels = {
      C: "Cliente",
      F: "Fornitore",
      B: "Banca",
      D: "Dipendente"
    };
    const lookupTypes = {
      C: "clienti",
      F: "fornitori",
      B: "banche",
      D: "dipendenti"
    };
    const normalizedSubject = String(subject || "").trim().toUpperCase();
    const previousSubject = String(subjectType?.value || "").trim().toUpperCase();
    const label = labels[normalizedSubject] || "Soggetto";

    if (!preserveSubject && previousSubject && previousSubject !== normalizedSubject) {
      const hiddenCode = subjectField?.querySelector("[data-movement-subject-code]");
      const name = subjectField?.querySelector("[data-movement-subject-name]");
      if (hiddenCode) hiddenCode.value = "";
      if (subjectCodeDisplay) subjectCodeDisplay.value = "";
      if (name) name.value = "";
    }

    if (subjectLabel) {
      subjectLabel.textContent = label;
    }
    if (subjectType) {
      subjectType.value = normalizedSubject;
    }
    if (subjectField) {
      subjectField.dataset.lookupType = lookupTypes[normalizedSubject] || "";
      subjectField.dataset.lookupTitle = label;
    }
    if (subjectLookupOpen) {
      subjectLookupOpen.title = `Seleziona ${label.toLowerCase()}`;
      subjectLookupOpen.setAttribute("aria-label", `Seleziona ${label.toLowerCase()}`);
    }
  };

  const linkedDocumentSectorFor = (kind) => {
    if (kind === "invoice") {
      return 10;
    }

    const subject = String(subjectType?.value || "").trim().toUpperCase();
    return subject === "C" ? 30 : 10;
  };

  const selectedSubjectCode = () => {
    const hiddenCode = String(subjectField?.querySelector("[data-movement-subject-code]")?.value || "").replace(/\D/g, "");
    const displayCode = String(subjectCodeDisplay?.value || "").replace(/\D/g, "");
    return hiddenCode || displayCode;
  };

  const hasSelectedSubject = () => {
    const code = selectedSubjectCode();
    const name = String(subjectField?.querySelector("[data-movement-subject-name]")?.value || "").trim();
    return Number(code) > 0 && name.length > 0;
  };

  const linkedDocumentLabelFor = (kind, id, sector) => {
    if (!id) {
      return "";
    }

    if (kind === "invoice") {
      return `MovIva acquisto - settore ${sector}`;
    }

    return `Scadenza - settore ${sector}`;
  };

  let linkedDocumentLookup = null;
  let linkedDocumentRows = [];
  let selectedLinkedDocumentRow = null;
  let activeLinkedDocumentField = null;

  const selectedSubjectType = () => String(subjectType?.value || "").trim().toUpperCase();

  const linkedDocumentTitle = (kind) => {
    if (kind === "invoice") {
      return selectedSubjectType() === "C" ? "Fatture di vendita" : "Fatture di acquisto";
    }

    return "Scadenze collegate";
  };

  const linkedDocumentLookupYear = () =>
    linkedDocumentLookup?.querySelector("[data-linked-document-year]")?.value || movementYear?.value || "";

  const linkedDocumentLookupUrl = (kind) => {
    const params = new URLSearchParams({
      handler: "LinkedDocuments",
      kind,
      year: linkedDocumentLookupYear(),
      subjectType: selectedSubjectType(),
      subjectCode: selectedSubjectCode()
    });
    return `${window.location.pathname}?${params.toString()}`;
  };

  const selectedSubjectName = () =>
    String(subjectField?.querySelector("[data-movement-subject-name]")?.value || "").trim();

  const fillLinkedDocumentYears = () => {
    const select = linkedDocumentLookup?.querySelector("[data-linked-document-year]");
    if (!select) {
      return;
    }

    const currentYear = Number(movementYear?.value || new Date().getFullYear());
    select.innerHTML = Array.from({ length: 5 }, (_, index) => {
      const year = currentYear - index;
      return `<option value="${year}">${year}</option>`;
    }).join("");
    select.value = String(currentYear);
  };

  const loadLinkedDocumentRows = async (kind) => {
    try {
      const response = await fetch(linkedDocumentLookupUrl(kind), {
        headers: { "Accept": "application/json" }
      });
      linkedDocumentRows = response.ok ? await response.json() : [];
    } catch {
      linkedDocumentRows = [];
    }

    renderLinkedDocumentRows();
    selectLinkedDocumentRow(linkedDocumentLookup?.querySelector("[data-linked-document-row]"), true);
  };

  const closeLinkedDocumentLookup = () => {
    linkedDocumentLookup?.classList.remove("is-open");
    document.body.classList.remove("lookup-open");
    selectedLinkedDocumentRow = null;
    activeLinkedDocumentField = null;
  };

  const selectLinkedDocumentRow = (row, focus = false) => {
    if (!row) {
      return;
    }

    linkedDocumentLookup?.querySelectorAll("[data-linked-document-row]").forEach((candidate) => {
      candidate.classList.remove("selected-row");
      candidate.removeAttribute("aria-selected");
    });
    row.classList.add("selected-row");
    row.setAttribute("aria-selected", "true");
    selectedLinkedDocumentRow = row;

    if (focus) {
      row.focus({ preventScroll: true });
    }
    row.scrollIntoView({ block: "nearest", inline: "nearest" });
  };

  const chooseLinkedDocumentRow = () => {
    if (!selectedLinkedDocumentRow || !activeLinkedDocumentField) {
      return;
    }

    const kind = activeLinkedDocumentField.dataset.movementAccessory;
    const hidden = linkedDocumentInputs[kind];
    const code = activeLinkedDocumentField.querySelector("[data-movement-accessory-code]");
    const number = activeLinkedDocumentField.querySelector("[data-movement-accessory-number]");
    const date = activeLinkedDocumentField.querySelector("[data-movement-accessory-date]");

    if (hidden?.id) {
      hidden.id.value = selectedLinkedDocumentRow.dataset.documentId || "";
    }
    if (hidden?.sector) {
      hidden.sector.value = selectedLinkedDocumentRow.dataset.documentSector || "";
    }
    if (code) {
      code.value = selectedLinkedDocumentRow.dataset.documentProtocol || "";
    }
    if (number) {
      number.value = selectedLinkedDocumentRow.dataset.documentNumber || "";
    }
    if (date) {
      date.value = selectedLinkedDocumentRow.dataset.documentDate || "";
    }

    closeLinkedDocumentLookup();
  };

  const renderLinkedDocumentRows = () => {
    const body = linkedDocumentLookup?.querySelector("[data-linked-document-body]");
    const empty = linkedDocumentLookup?.querySelector("[data-linked-document-empty]");
    if (!body || !empty) {
      return;
    }

    body.innerHTML = linkedDocumentRows.map((row) => `
      <tr tabindex="0"
          data-linked-document-row
          data-document-id="${row.id}"
          data-document-sector="${row.sector}"
          data-document-protocol="${escapeHtml(row.protocol || "")}"
          data-document-number="${escapeHtml(row.number || "")}"
          data-document-date="${escapeHtml(row.date || "")}">
        <td class="accounting-linked-document-code">${escapeHtml(row.protocol || "")}</td>
        <td>${escapeHtml(row.number || "")}</td>
        <td class="accounting-linked-document-date">${escapeHtml(row.date || "")}</td>
        <td class="accounting-linked-document-amount">${escapeHtml(formatMoney(Number(row.amount || 0)))}</td>
      </tr>`).join("");

    body.querySelectorAll("[data-linked-document-row]").forEach((row) => {
      row.addEventListener("click", () => selectLinkedDocumentRow(row, true));
      row.addEventListener("dblclick", chooseLinkedDocumentRow);
    });
    empty.hidden = linkedDocumentRows.length > 0;
  };

  const buildLinkedDocumentLookup = () => {
    if (linkedDocumentLookup) {
      return linkedDocumentLookup;
    }

    linkedDocumentLookup = document.createElement("div");
    linkedDocumentLookup.className = "accounting-linked-document-lookup";
    linkedDocumentLookup.innerHTML = `
      <div class="accounting-linked-document-dialog" role="dialog" aria-modal="true" aria-labelledby="linked-document-lookup-title">
        <div class="accounting-linked-document-titlebar">
          <h2 id="linked-document-lookup-title" data-linked-document-title>Documenti collegati</h2>
          <button class="accounting-linked-document-close" type="button" aria-label="Chiudi" data-linked-document-close></button>
        </div>
        <div class="accounting-linked-document-subject">
          <span class="accounting-linked-document-subject-label">Soggetto</span>
          <span class="accounting-linked-document-subject-code" data-linked-document-subject-code></span>
          <span class="accounting-linked-document-subject-name" data-linked-document-subject-name></span>
        </div>
        <div class="accounting-linked-document-frame">
          <table class="accounting-linked-document-table">
            <thead>
              <tr>
                <th>Partita</th>
                <th>Numero</th>
                <th>Data</th>
                <th>Importo</th>
              </tr>
            </thead>
            <tbody data-linked-document-body></tbody>
          </table>
          <div class="accounting-linked-document-empty" data-linked-document-empty hidden>Nessun documento disponibile</div>
        </div>
        <div class="accounting-linked-document-actions">
          <div class="accounting-linked-document-year-filter">
            <label>Anno</label>
            <select data-linked-document-year></select>
          </div>
          <div class="accounting-linked-document-action-buttons">
            <button class="accounting-linked-document-command" type="button" data-linked-document-ok>OK</button>
            <button class="accounting-linked-document-command" type="button" data-linked-document-cancel>Annulla</button>
          </div>
        </div>
      </div>`;
    document.body.appendChild(linkedDocumentLookup);

    linkedDocumentLookup.addEventListener("mousedown", (event) => {
      if (event.target === linkedDocumentLookup) {
        closeLinkedDocumentLookup();
      }
    });
    linkedDocumentLookup.querySelector("[data-linked-document-close]")?.addEventListener("click", closeLinkedDocumentLookup);
    linkedDocumentLookup.querySelector("[data-linked-document-cancel]")?.addEventListener("click", closeLinkedDocumentLookup);
    linkedDocumentLookup.querySelector("[data-linked-document-ok]")?.addEventListener("click", chooseLinkedDocumentRow);
    linkedDocumentLookup.querySelector("[data-linked-document-year]")?.addEventListener("change", () => {
      const kind = activeLinkedDocumentField?.dataset.movementAccessory;
      if (kind) {
        loadLinkedDocumentRows(kind);
      }
    });
    linkedDocumentLookup.addEventListener("keydown", (event) => {
      const rows = Array.from(linkedDocumentLookup.querySelectorAll("[data-linked-document-row]"));
      const currentIndex = Math.max(rows.indexOf(selectedLinkedDocumentRow), 0);

      if (event.key === "Escape") {
        event.preventDefault();
        closeLinkedDocumentLookup();
      } else if (event.key === "Enter") {
        event.preventDefault();
        chooseLinkedDocumentRow();
      } else if (event.key === "ArrowDown") {
        event.preventDefault();
        selectLinkedDocumentRow(rows[Math.min(currentIndex + 1, rows.length - 1)], true);
      } else if (event.key === "ArrowUp") {
        event.preventDefault();
        selectLinkedDocumentRow(rows[Math.max(currentIndex - 1, 0)], true);
      }
    });

    return linkedDocumentLookup;
  };

  const openLinkedDocumentLookup = async (field) => {
    const kind = field.dataset.movementAccessory;
    const dialog = buildLinkedDocumentLookup();
    activeLinkedDocumentField = field;
    linkedDocumentRows = [];
    selectedLinkedDocumentRow = null;
    dialog.querySelector("[data-linked-document-title]").textContent = linkedDocumentTitle(kind);
    dialog.querySelector("[data-linked-document-subject-code]").textContent = selectedSubjectCode().padStart(5, "0");
    dialog.querySelector("[data-linked-document-subject-name]").textContent = selectedSubjectName();
    fillLinkedDocumentYears();
    renderLinkedDocumentRows();
    dialog.classList.add("is-open");
    document.body.classList.add("lookup-open");
    await loadLinkedDocumentRows(kind);
  };

  const syncAccessoryField = (field) => {
    const kind = field.dataset.movementAccessory;
    const code = field.querySelector("[data-movement-accessory-code]");
    const description = field.querySelector("[data-movement-accessory-description]");
    const year = field.querySelector("[data-movement-accessory-year]");
    const number = field.querySelector("[data-movement-accessory-number]");
    const date = field.querySelector("[data-movement-accessory-date]");
    const hidden = linkedDocumentInputs[kind];
    const id = Number(String(hidden?.id?.value || code?.value || "").split("/")[0].replace(/\D/g, ""));

    if (code) {
      code.value = id > 0
        ? `${String(id).padStart(6, "0")} / ${movementYear?.value || ""}`
        : "";
    }

    if (!hidden) {
      return;
    }

    if (id <= 0) {
      hidden.id.value = "";
      hidden.sector.value = "";
      if (description) {
        description.value = "";
      }
      if (year) {
        year.value = "";
      }
      if (number) {
        number.value = "";
      }
      if (date) {
        date.value = "";
      }
      return;
    }

    const sector = linkedDocumentSectorFor(kind);
    hidden.id.value = String(id);
    hidden.sector.value = String(sector);
    if (description) {
      description.value = linkedDocumentLabelFor(kind, id, sector);
    }
    if (year) {
      year.value = movementYear?.value || "";
    }
    if (number && !number.value) {
      number.value = "";
    }
    if (date && !date.value) {
      date.value = "";
    }
  };

  const setAccessoryFieldState = (field, enabled) => {
    const kind = field.dataset.movementAccessory;
    const code = field.querySelector("[data-movement-accessory-code]");
    const description = field.querySelector("[data-movement-accessory-description]");
    const button = field.querySelector("[data-movement-accessory-lookup]");
    const clear = field.querySelector("[data-movement-accessory-clear]");

    field.classList.toggle("is-disabled", !enabled);
    if (code) {
      code.tabIndex = -1;
      code.readOnly = true;
      if (!enabled) {
        code.value = "";
      }
    }

    field.querySelectorAll("[data-movement-accessory-year], [data-movement-accessory-number], [data-movement-accessory-date], [data-movement-accessory-description]").forEach((input) => {
      input.tabIndex = -1;
      input.readOnly = true;
      if (!enabled) {
        input.value = "";
      }
    });

    if (button) {
      button.disabled = !enabled;
      button.tabIndex = enabled ? 0 : -1;
    }
    if (clear) {
      clear.disabled = !enabled;
      clear.tabIndex = enabled ? 0 : -1;
    }

    if (!enabled) {
      if (linkedDocumentInputs[kind]?.id) {
        linkedDocumentInputs[kind].id.value = "";
      }
      if (linkedDocumentInputs[kind]?.sector) {
        linkedDocumentInputs[kind].sector.value = "";
      }
    } else {
      syncAccessoryField(field);
    }
  };

  const updateAccessoryFields = (option) => {
    const invoiceEnabled = option?.dataset.invoice === "true";
    const dueDateEnabled = option?.dataset.dueDate === "true";

    accessoryFields.forEach((field) => {
      const kind = field.dataset.movementAccessory;
      const enabled = kind === "invoice" ? invoiceEnabled : kind === "due-date" ? dueDateEnabled : false;
      setAccessoryFieldState(field, enabled);
    });

  };

  const applyCauseTemplate = (populateRows = true, preserveExisting = false) => {
    const option = causeOption();
    if (causeCode) {
      causeCode.value = formatCode(causeCode.value, 3);
    }

    if (!option) {
      if (preserveExisting) {
        setSubjectLabel(subjectType?.value || "", true);
        updateAccessoryFields(null);
        return;
      }
      if (causeDescription) {
        causeDescription.value = "";
      }
      setSubjectLabel("");
      updateAccessoryFields(null);
      return;
    }

    if (causeDescription) {
      causeDescription.value = option.dataset.description || "";
    }
    setSubjectLabel(option.dataset.subject || "", preserveExisting);
    updateAccessoryFields(option);

    if (populateRows) {
      rowsForSide("debit").forEach((row, index) => {
        const select = row.querySelector("[data-account-select]");
        if (select) {
          select.value = option.dataset[`debit-${index + 1}`] || "";
        }
        updateRow(row);
      });

      rowsForSide("credit").forEach((row, index) => {
        const select = row.querySelector("[data-account-select]");
        if (select) {
          select.value = option.dataset[`credit-${index + 1}`] || "";
        }
        updateRow(row);
      });
    }
  };

  const sortValue = (row, key) => {
    const value = row.dataset[key] || "";
    return key === "masterCode" || key === "accountCode"
      ? Number(value)
      : normalize(value);
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

  const selectZoomRowFromScroll = (delta) => {
    if (!zoomGrid || zoom?.hidden || delta === 0) {
      return;
    }

    const selected = selectedZoomRow();
    const viewport = zoomViewport();
    const selectedRect = selected?.getBoundingClientRect();
    if (!selectedRect || (selectedRect.bottom > viewport.top + 1 && selectedRect.top < viewport.bottom - 1)) {
      return;
    }

    const visible = visibleZoomRows().filter((row) => {
      const rect = row.getBoundingClientRect();
      return rect.top >= viewport.top + 1 && rect.bottom <= viewport.bottom - 1;
    });

    if (visible.length > 0) {
      selectZoomRow(delta > 0 ? visible[0] : visible[visible.length - 1], false, false);
    }
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

    select.value = row.dataset.accountCode || "";
    updateRow(activeRow);
    closeZoom();
    activeRow.querySelector("[data-line-amount]")?.focus();
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

  function closeZoom() {
    if (!zoom) {
      return;
    }

    zoom.hidden = true;
    activeRow = null;
  }

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

  rows.forEach((row) => {
    row.querySelector("[data-account-add]")?.addEventListener("click", () => openZoom(row));
    row.querySelector("[data-account-clear]")?.addEventListener("click", () => {
      clearLine(row);
      calculateTotals();
    });
    row.querySelector("[data-account-select]")?.addEventListener("change", () => updateRow(row));
    updateRow(row);
  });

  causeCode?.addEventListener("input", () => {
    causeCode.value = String(causeCode.value || "").replace(/\D/g, "").slice(0, 3);
  });
  causeCode?.addEventListener("change", () => {
    applyCauseTemplate(true);
    validateTypedCauseCode();
  });
  causeCode?.addEventListener("blur", () => {
    applyCauseTemplate(true);
    validateTypedCauseCode();
  });
  causeLookupOpen?.addEventListener("click", openCauseLookup);
  subjectLookupOpen?.addEventListener("click", (event) => {
    if (requireCauseBeforeSubject()) {
      return;
    }

    event.preventDefault();
    event.stopImmediatePropagation();
  });
  subjectCodeDisplay?.addEventListener("input", () => {
    if (!hasSelectedCause()) {
      subjectCodeDisplay.value = "";
      requireCauseBeforeSubject();
    }
  });
  subjectField?.addEventListener("micronote:lookup-selected", () => {
    setSubjectLabel(subjectType?.value || "", true);
    if (subjectCodeDisplay) {
      subjectCodeDisplay.value = formatCode(subjectCodeDisplay.value, 5);
    }
    accessoryFields.forEach(syncAccessoryField);
  });
  subjectField?.addEventListener("micronote:lookup-not-found", (event) => {
    showForeignKeyError(
      "Soggetto",
      `Codice soggetto inesistente: ${event.detail?.code || ""}.`,
      subjectCodeDisplay);
  });

  accessoryFields.forEach((field) => {
    const code = field.querySelector("[data-movement-accessory-code]");
    code?.addEventListener("input", () => {
      code.value = String(code.value || "").replace(/\D/g, "");
      syncAccessoryField(field);
    });
    code?.addEventListener("blur", () => syncAccessoryField(field));
    field.querySelector("[data-movement-accessory-clear]")?.addEventListener("click", () => {
      const kind = field.dataset.movementAccessory;
      field.querySelectorAll("[data-movement-accessory-code], [data-movement-accessory-year], [data-movement-accessory-number], [data-movement-accessory-date], [data-movement-accessory-description]").forEach((input) => {
        input.value = "";
      });
      if (linkedDocumentInputs[kind]?.id) {
        linkedDocumentInputs[kind].id.value = "";
      }
      if (linkedDocumentInputs[kind]?.sector) {
        linkedDocumentInputs[kind].sector.value = "";
      }
    });
  });

  amountInputs.forEach((input) => {
    input.addEventListener("focus", () => {
      input.value = formatMoneyForEdit(parseMoney(input.value));
      input.select();
    });
    input.addEventListener("input", () => cleanMoneyInput(input));
    input.addEventListener("blur", () => {
      input.value = formatMoney(parseMoney(input.value));
      calculateTotals();
    });
  });

  totalAmount?.addEventListener("focus", () => {
    totalAmount.value = formatMoneyForEdit(parseMoney(totalAmount.value));
    totalAmount.select();
  });
  totalAmount?.addEventListener("input", () => cleanMoneyInput(totalAmount));
  totalAmount?.addEventListener("blur", () => {
    totalAmount.value = formatMoney(parseMoney(totalAmount.value));
  });

  totalAmountFill?.addEventListener("click", fillTotalAmountFromLines);

  const enableSaveButton = () => {
    if (saveButton) {
      saveButton.disabled = false;
    }
    if (form) {
      delete form.dataset.accountingSubmitPending;
    }
    window.MicronoteProgress?.hide?.();
  };

  const submitWithProgress = () => {
    if (!form || form.dataset.accountingSubmitPending === "true") {
      return;
    }

    form.dataset.accountingSubmitPending = "true";
    if (saveButton) {
      saveButton.disabled = true;
    }

    normalizeMoneyForSubmit(totalAmount);
    amountInputs.forEach(normalizeMoneyForSubmit);
    window.MicronoteProgress?.show?.("Salvataggio in corso...");

    window.setTimeout(() => {
      HTMLFormElement.prototype.submit.call(form);
    }, 1000);
  };

  form?.addEventListener("submit", (event) => {
    event.preventDefault();
    event.stopPropagation();
    event.stopImmediatePropagation();

    if (!validateBeforeSubmit()) {
      enableSaveButton();
      return;
    }

    submitWithProgress();
  }, true);

  saveButton?.addEventListener("click", (event) => {
    event.preventDefault();
    if (!form || form.dataset.accountingSubmitPending === "true") {
      return;
    }

    if (!validateBeforeSubmit()) {
      enableSaveButton();
      return;
    }

    submitWithProgress();
  });

  window.addEventListener("pageshow", enableSaveButton);
  enableSaveButton();

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
  zoom?.addEventListener("keydown", (event) => {
    if (event.key === "Escape") {
      event.preventDefault();
      closeZoom();
    }
  });

  accessoryFields.forEach((field) => {
    field.querySelector("[data-movement-accessory-lookup]")?.addEventListener("click", () => {
      const isInvoice = field.dataset.movementAccessory === "invoice";
      if (!hasSelectedSubject()) {
        window.MicronoteMessageBox?.show({
          title: isInvoice ? "Fattura collegata" : "Scadenza collegata",
          message: "Selezionare prima il soggetto.",
          detail: "La ricerca dei documenti collegati deve essere filtrata per soggetto."
        });
        return;
      }

      openLinkedDocumentLookup(field);
    });
  });

  document.addEventListener("keydown", (event) => {
    if (document.body.classList.contains("lookup-open")) {
      return;
    }

    if (event.key === "Escape" && zoom?.hidden !== false) {
      event.preventDefault();
      window.location.href = document.querySelector(".top-actions a[href]")?.getAttribute("href") || "/PrimaNota";
    }
  });

  applyCauseTemplate(form?.dataset.isNew === "true", form?.dataset.isNew !== "true");
  calculateTotals();
  applyZoomSort();
  if (form?.dataset.isNew === "true") {
    window.setTimeout(() => causeCode?.focus(), 0);
  }
});

