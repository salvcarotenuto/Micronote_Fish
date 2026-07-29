document.addEventListener("DOMContentLoaded", () => {
  const formPage = document.querySelector(".stock-load-form-page");
  const stockLoadReadonly = formPage?.dataset.stockLoadReadonly === "true";
  const stockLoadElectronicInvoiceCurrentSource = () => {
    const actionValue = Number.parseInt(formPage?.dataset.azione || "0", 10) || 0;
    const baseAction = actionValue % 10;
    return baseAction === 2 ? "" : "archive";
  };
  const stockLoadForm = formPage?.querySelector("form.entity-form");
  const stockLoadSaveButton = document.querySelector("[data-stock-load-save]");
  const stockLoadCancelLink = document.querySelector(".stock-load-form-page .top-actions a.button-secondary");
  const stockLoadModalCancel = document.querySelector("[data-stock-load-modal-cancel='true']");
  const stockLoadPayload = document.querySelector("[data-stock-load-payload]");
  let stockLoadSaveInProgress = false;
  let stockLoadOverwriteConfirmed = false;
  let stockLoadOverwriteCheckedFromFe = false;
  let stockLoadProgressStartedAt = 0;
  const stockLoadReturnTarget = new URLSearchParams(window.location.search)
    .get("returnTo")?.toLocaleLowerCase("it") ?? "";
  const stockLoadReturnToList = stockLoadReturnTarget === "list";
  const stockLoadReturnToPurchaseInvoice = stockLoadReturnTarget === "purchaseinvoice";
  const stockLoadId = document.querySelector("[data-stock-load-id]");
  const stockLoadYear = document.querySelector("[data-stock-load-year]");
  const stockLoadCode = document.querySelector("[data-stock-load-code]");
  const lineOverlay = document.querySelector("[data-stock-load-line-overlay]");
  const lineDialog = lineOverlay?.querySelector(".stock-load-line-dialog");
  const articleLookup = document.querySelector("[data-article-lookup]");
  const linesGrid = document.querySelector(".stock-load-lines-grid");
  const linesGridWrap = document.querySelector(".stock-load-lines-wrap");
  const electronicInvoiceOpen = document.querySelector("[data-electronic-invoice-open]");
  const electronicInvoiceCurrentPreview = document.querySelector("[data-electronic-invoice-current-preview]");
  const electronicInvoiceDialog = document.querySelector("[data-electronic-invoice-dialog]");
  const electronicInvoiceCloseButtons = Array.from(document.querySelectorAll("[data-electronic-invoice-close]"));
  const electronicInvoiceName = document.querySelector("[data-electronic-invoice-name]");
  const electronicInvoiceFullPath = document.querySelector("[data-electronic-invoice-full-path]");
  const electronicInvoicePath = document.querySelector("[data-electronic-invoice-path]");
  const electronicInvoicePathValue = () => electronicInvoicePath?.dataset?.electronicInvoicePathValue ?? "";
  const setElectronicInvoicePathValue = (value, mode) => {
    if (!electronicInvoicePath) {
      return;
    }

    electronicInvoicePath.dataset.electronicInvoicePathValue = value ?? "";
    if (mode) {
      electronicInvoicePath.dataset.mode = mode;
    }
  };
  const electronicInvoiceRefresh = document.querySelector("[data-electronic-invoice-refresh]");
  const electronicInvoiceFolderPicker = document.querySelector("[data-electronic-invoice-folder-picker]");
  const electronicInvoiceGrid = document.querySelector(".purchase-invoice-fe-grid-frame");
  const electronicInvoiceFiles = document.querySelector("[data-electronic-invoice-files]");
  const electronicInvoiceSearch = document.querySelector("[data-electronic-invoice-search]");
  const electronicInvoiceSearchClear = document.querySelector("[data-electronic-invoice-search-clear]");
  const electronicInvoiceCount = document.querySelector("[data-electronic-invoice-count]");
  const electronicInvoiceAccept = document.querySelector("[data-electronic-invoice-accept]");
  const electronicInvoicePreview = document.querySelector("[data-electronic-invoice-preview]");
  const electronicInvoicePreviewBox = document.querySelector("[data-electronic-invoice-preview-box]");
  const electronicInvoiceDelete = document.querySelector("[data-electronic-invoice-delete]");
  const electronicInvoiceViewer = document.querySelector("[data-electronic-invoice-viewer]");
  const electronicInvoiceViewerFrame = document.querySelector("[data-electronic-invoice-viewer-frame]");
  const electronicInvoiceViewerTitle = document.querySelector("[data-electronic-invoice-viewer-title]");
  const electronicInvoiceViewerPanel = document.querySelector("[data-electronic-invoice-viewer-panel]");
  const electronicInvoiceViewerCloseButtons = Array.from(document.querySelectorAll("[data-electronic-invoice-viewer-close]"));
  const stockLoadDocumentNumber = document.querySelector("[data-stock-load-document-number]");
  const stockLoadDocumentDate = document.querySelector("[data-stock-load-document-date]");
  const stockLoadCause = document.querySelector("[data-stock-load-cause]");
  const stockLoadSupplierCode = document.querySelector("#stockLoadSupplierCode");
  const stockLoadSupplierCodeDisplay = document.querySelector("#stockLoadSupplierCodeDisplay");
  const stockLoadSupplierName = document.querySelector("#stockLoadSupplierName");
  const stockLoadSubjectField = document.querySelector(".stock-load-supplier-field");
  const stockLoadSubjectLabel = document.querySelector("[data-stock-load-subject-label]");
  const stockLoadStore = document.querySelector("[data-stock-load-store]");
  const articleCardButton = document.querySelector("[data-stock-load-article-card]");
  const addArticleButton = document.querySelector("[data-stock-load-article-add]");
  const assignArticleButton = document.querySelector("[data-stock-load-article-assign]");
  const articleCardModal = document.querySelector("[data-stock-load-article-modal]");
  const articleCardModalFrame = document.querySelector("[data-stock-load-article-modal-frame]");
  let targetLineRow = null;
  let articleLookupRows = [];
  let articleLookupFilteredRows = [];
  let articleLookupSelectedIndex = -1;
  let articleLookupSortKey = "description";
  let articleLookupSortDirection = "asc";
  let articleLookupMode = "line";
  let selectedElectronicInvoiceFile = null;
  let selectedElectronicInvoiceBrowserFile = null;
  let electronicInvoicePreviewAbort = null;
  let electronicInvoiceLastScrollTop = 0;
  let electronicInvoiceWheelFrame = 0;
  let electronicInvoiceFolderPickerPending = false;
  let electronicInvoiceFolderAbortController = null;
  let electronicInvoiceSuppressEscapeUntil = 0;

  const stockLoadRowState = {
    ok: "ok",
    missingCode: "missing-code",
    missingArticle: "missing-article",
    assignedArticle: "assigned-article"
  };

  const formAzione = {
    inserimento: 2,
    modifica: 3,
    modale: 100,
    origineFe: 200,
    codiceBloccato: 400,
    withContesto(base, ...contexts) {
      const mask = contexts.reduce((value, context) => value | Math.floor(context / 100), 0);
      return base + (mask * 100);
    }
  };

  const dateFields = Array.from(document.querySelectorAll(".micronote-date-input"));
  let activeDateField = null;
  let activeDateMonth = null;
  let datePickerPanel = null;

  const padDatePart = (value) => String(value).padStart(2, "0");

  const syncDateEmptyState = (field) => {
    if (!field) {
      return;
    }

    field.classList.toggle("is-empty", !field.value);
  };

  const isRealDateParts = (day, month, year) => {
    if (year < 1900 || year > 2099 || month < 1 || month > 12 || day < 1) {
      return false;
    }

    const date = new Date(year, month - 1, day);
    return date.getFullYear() === year
      && date.getMonth() === month - 1
      && date.getDate() === day;
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
    syncDateEmptyState(field);
  };

  const normalizeDateOnBlur = (field) => {
    const text = field.value.trim();
    if (!text) {
      syncDateEmptyState(field);
      return;
    }

    const display = toDisplayDate(text);
    if (display) {
      field.value = display;
    }

    syncDateEmptyState(field);
  };

  dateFields.forEach((field) => {
    syncDateEmptyState(field);
    field.addEventListener("input", () => normalizeDateInput(field));
    field.addEventListener("blur", () => normalizeDateOnBlur(field));
    field.addEventListener("change", () => normalizeDateOnBlur(field));
  });

  document.addEventListener("click", (event) => {
    const target = event.target;
    if (!(target instanceof Element)) {
      return;
    }

    const dateButton = target.closest("[data-date-picker-button]");
    if (dateButton) {
      event.preventDefault();
      const field = dateButton.closest("[data-date-control]")?.querySelector(".micronote-date-input");
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
        activeDateField.dispatchEvent(new Event("change", { bubbles: true }));
        syncDateEmptyState(activeDateField);
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

  const showMessage = (message, focusTarget = null) => {
    window.MicronoteMessageBox?.show({
      title: "Micronote Food - attenzione",
      message,
      variant: "error",
      onConfirm: () => focusTarget?.focus?.()
    });
  };

  const requestVerificationToken = () =>
    document.querySelector("input[name='__RequestVerificationToken']")?.value ?? "";

  const stockLoadSubjectKind = () =>
    Number.parseInt(stockLoadCause?.value || "10", 10) === 12
      ? {
          label: "Cliente",
          lookupType: "clienti",
          lookupTitle: "Clienti",
          selectTitle: "Seleziona cliente",
          addTitle: "Aggiungi cliente"
        }
      : {
          label: "Fornitore",
          lookupType: "fornitori",
          lookupTitle: "Fornitori",
          selectTitle: "Seleziona fornitore",
          addTitle: "Aggiungi fornitore"
        };

  const applyStockLoadSubjectKind = (clearValue = false) => {
    const kind = stockLoadSubjectKind();
    if (stockLoadSubjectField) {
      stockLoadSubjectField.dataset.lookupType = kind.lookupType;
      stockLoadSubjectField.dataset.lookupTitle = kind.lookupTitle;
    }

    if (stockLoadSubjectLabel) {
      stockLoadSubjectLabel.textContent = kind.label;
    }

    const lookupButton = stockLoadSubjectField?.querySelector("[data-lookup-open]");
    lookupButton?.setAttribute("title", kind.selectTitle);
    lookupButton?.setAttribute("aria-label", kind.selectTitle);

    const addButton = stockLoadSubjectField?.querySelector(".stock-load-add-button");
    addButton?.setAttribute("title", kind.addTitle);
    addButton?.setAttribute("aria-label", kind.addTitle);

    if (clearValue) {
      setFieldValue(stockLoadSupplierCode, "");
      setFieldValue(stockLoadSupplierCodeDisplay, "");
      setFieldValue(stockLoadSupplierName, "");
    }
  };

  const formatNumber = (value, digits = 3) => {
    const parsed = Number.parseFloat(String(value ?? "0").replace(",", "."));
    if (!Number.isFinite(parsed) || parsed === 0) {
      return "";
    }

    const [integerPart, decimalPart] = parsed.toFixed(digits).split(".");
    return `${integerPart.replace(/\B(?=(\d{3})+(?!\d))/g, ".")},${decimalPart}`;
  };

  const parseDecimal = (value) => {
    const text = String(value ?? "").trim();
    const commaIndex = text.lastIndexOf(",");
    const dotIndex = text.lastIndexOf(".");
    const decimalIndex = Math.max(commaIndex, dotIndex);
    const normalized = decimalIndex >= 0
      ? `${text.slice(0, decimalIndex).replace(/[.,]/g, "")}.${text.slice(decimalIndex + 1).replace(/[.,]/g, "")}`
      : text.replace(/[.,]/g, "");
    const parsed = Number.parseFloat(normalized);
    return Number.isFinite(parsed) ? parsed : 0;
  };

  const formatMoney = (value) => window.MicronoteMoney?.format
    ? window.MicronoteMoney.format(value)
    : String(value || "");

  const parseMoney = (value) => window.MicronoteMoney?.parse
    ? window.MicronoteMoney.parse(value)
    : parseDecimal(value);

  const formatPercent = (value) => window.MicronotePercent?.format
    ? window.MicronotePercent.format(value)
    : `${value || 0},00 %`;

  const parsePercent = (value) => window.MicronotePercent?.parse
    ? window.MicronotePercent.parse(value)
    : parseDecimal(value);

  const lineFields = {
    code: lineOverlay?.querySelector("[data-line-code]"),
    description: lineOverlay?.querySelector("[data-line-description]"),
    unit: lineOverlay?.querySelector("[data-line-unit]"),
    stock: lineOverlay?.querySelector("[data-line-stock]"),
    tare: lineOverlay?.querySelector("[data-line-tare]"),
    lastPrice: lineOverlay?.querySelector("[data-line-last-price]"),
    lastVat: lineOverlay?.querySelector("[data-line-last-vat]"),
    packages: lineOverlay?.querySelector("[data-line-packages]"),
    quantity: lineOverlay?.querySelector("[data-line-quantity]"),
    price: lineOverlay?.querySelector("[data-line-price]"),
    discount: lineOverlay?.querySelector("[data-line-discount]"),
    vat: lineOverlay?.querySelector("[data-line-vat]"),
    vatPrice: lineOverlay?.querySelector("[data-line-vat-price]"),
    amount: lineOverlay?.querySelector("[data-line-amount]")
  };

  const articleLookupFields = {
    search: articleLookup?.querySelector("[data-article-search]"),
    category: articleLookup?.querySelector("[data-article-category]"),
    supplier: articleLookup?.querySelector("[data-article-supplier]"),
    count: articleLookup?.querySelector("[data-article-count]"),
    body: articleLookup?.querySelector("tbody"),
    frame: articleLookup?.querySelector(".stock-load-article-lookup-frame")
  };

  const clearLineDialog = () => {
    Object.values(lineFields).forEach((field) => {
      if (field) {
        field.value = "";
      }
    });
  };

  const calculateLineAmount = () => {
    const quantity = parseDecimal(lineFields.quantity?.value);
    const price = parseMoney(lineFields.price?.value);
    const discount = parsePercent(lineFields.discount?.value);
    const netPrice = price - (price * discount / 100);
    const amount = quantity * netPrice;
    if (lineFields.amount) {
      lineFields.amount.value = formatMoney(amount);
    }
    if (lineFields.vatPrice) {
      lineFields.vatPrice.value = formatMoney(netPrice * (1 + parsePercent(lineFields.vat?.value) / 100));
    }
  };

  const findFirstFreeRow = () => {
    const rows = Array.from(linesGrid?.querySelectorAll("tbody tr") ?? []);
    return rows.find((row) => {
      const firstCell = row.querySelector("td:first-child input");
      return !String(firstCell?.value ?? "").trim();
    }) ?? null;
  };

  const gridRows = () => Array.from(linesGrid?.querySelectorAll("tbody tr") ?? []);

  const visibleGridRows = () => gridRows()
    .filter((row) => String(row.querySelector("td:first-child input")?.value ?? "").trim());

  const selectedGridRow = () => linesGrid?.querySelector("tbody tr.selected") ?? null;

  const ensureGridRowVisible = (row, direction = 0) => {
    if (!linesGridWrap || !linesGrid || !row) {
      return;
    }

    const headerHeight = linesGrid.tHead?.offsetHeight ?? 0;
    const visibleTop = linesGridWrap.scrollTop + headerHeight;
    const visibleBottom = linesGridWrap.scrollTop + linesGridWrap.clientHeight;
    const rowTop = row.offsetTop;
    const rowBottom = rowTop + row.offsetHeight;

    if (rowTop >= visibleTop && rowBottom <= visibleBottom) {
      return;
    }

    if (direction >= 0 && rowBottom > visibleBottom) {
      linesGridWrap.scrollTop = rowBottom - linesGridWrap.clientHeight + 1;
      return;
    }

    if (direction <= 0 && rowTop < visibleTop) {
      linesGridWrap.scrollTop = Math.max(rowTop - headerHeight - 1, 0);
    }
  };

  const selectGridRow = (row, focus = false, direction = 0) => {
    const code = String(row?.querySelector("td:first-child input")?.value ?? "").trim();
    if (!row || !code) {
      return;
    }

    linesGrid?.querySelectorAll("tbody tr").forEach((gridRow) => {
      gridRow.classList.remove("selected", "selected-row");
      gridRow.removeAttribute("aria-selected");
    });

    row.classList.add("selected", "selected-row");
    row.setAttribute("aria-selected", "true");

    if (focus) {
      row.focus({ preventScroll: true });
    }

    ensureGridRowVisible(row, direction);
  };

  const selectGridRowForValidation = (row, focus = false, direction = 0) => {
    if (!row) {
      return;
    }

    linesGrid?.querySelectorAll("tbody tr").forEach((gridRow) => {
      gridRow.classList.remove("selected", "selected-row");
      gridRow.removeAttribute("aria-selected");
    });

    row.classList.add("selected", "selected-row");
    row.setAttribute("aria-selected", "true");

    if (focus) {
      row.focus({ preventScroll: true });
    }

    ensureGridRowVisible(row, direction);
  };

  const navigateGridRows = (event, currentRow) => {
    const rows = visibleGridRows();
    const currentIndex = rows.indexOf(currentRow);

    if (currentIndex < 0) {
      return;
    }

    if (event.key === "ArrowDown") {
      event.preventDefault();
      event.stopPropagation();
      selectGridRow(rows[Math.min(currentIndex + 1, rows.length - 1)], true, 1);
      return;
    }

    if (event.key === "ArrowUp") {
      event.preventDefault();
      event.stopPropagation();
      selectGridRow(rows[Math.max(currentIndex - 1, 0)], true, -1);
      return;
    }

    if (event.key === "Home") {
      event.preventDefault();
      event.stopPropagation();
      selectGridRow(rows[0], true, -1);
      return;
    }

    if (event.key === "End") {
      event.preventDefault();
      event.stopPropagation();
      selectGridRow(rows[rows.length - 1], true, 1);
    }
  };

  const wireGridRow = (row) => {
    if (!row || row.dataset.stockLoadGridWired === "true") {
      return;
    }

    row.dataset.stockLoadGridWired = "true";
    row.tabIndex = 0;
    row.addEventListener("focus", () => selectGridRow(row, false, 0));
    row.addEventListener("keydown", (event) => navigateGridRows(event, row));
  };

  const gridLineInputs = (row) => Array.from(row?.querySelectorAll("td:not([hidden]) input") ?? []);

  const rowStateInput = (row) => row?.querySelector("[data-stock-load-row-state]");

  const setGridRowState = (row, state) => {
    if (!row) {
      return;
    }

    const value = state || "";
    const articleInput = row.querySelector("td:first-child input");
    const input = rowStateInput(row);
    if (input) {
      input.value = value;
    }

    row.dataset.stockLoadRowState = value;
    if (articleInput) {
      articleInput.classList.toggle("stock-load-missing-article", value === stockLoadRowState.missingArticle);
      articleInput.classList.toggle("stock-load-assigned-article", value === stockLoadRowState.assignedArticle);
    }
  };

  const getGridRowState = (row) =>
    rowStateInput(row)?.value || row?.dataset.stockLoadRowState || "";

  const isMissingArticleRow = (row) =>
    getGridRowState(row) === stockLoadRowState.missingArticle
      || row?.dataset.articleFound === "false";

  const refreshGridRowState = (row) => {
    const values = gridLineInputs(row).map((input) => input.value);
    if (!hasLineRowValues(values)) {
      setGridRowState(row, "");
      return "";
    }

    const code = String(values[0] ?? "").trim();
    if (!code) {
      setGridRowState(row, stockLoadRowState.missingCode);
      return stockLoadRowState.missingCode;
    }

    if (isMissingArticleRow(row) || row?.dataset.articleFound === "false") {
      setGridRowState(row, stockLoadRowState.missingArticle);
      return stockLoadRowState.missingArticle;
    }

    const currentState = getGridRowState(row);
    const state = currentState === stockLoadRowState.assignedArticle
      ? stockLoadRowState.assignedArticle
      : stockLoadRowState.ok;
    setGridRowState(row, state);
    return state;
  };

  const clearGridRow = (row) => {
    row?.querySelectorAll("input").forEach((input) => {
      input.value = "";
      input.classList.remove("stock-load-missing-article");
      input.classList.remove("stock-load-assigned-article");
    });
    setGridRowState(row, "");
    row?.classList.remove("selected", "selected-row");
    row?.removeAttribute("aria-selected");
  };

  const compactGridRows = () => {
    const rows = Array.from(linesGrid?.querySelectorAll("tbody tr") ?? []);
    const values = rows
      .map((row) => ({
        cells: gridLineInputs(row).map((input) => input.value),
        state: getGridRowState(row),
        tare: row.dataset.tare || "0",
        netPrice: row.dataset.netPrice || "0",
        vatIncludedPrice: row.dataset.vatIncludedPrice || "0"
      }))
      .filter((source) => String(source.cells[0] ?? "").trim());

    rows.forEach((row, index) => {
      const inputs = gridLineInputs(row);
      const source = values[index] ?? { cells: [], state: "" };
      inputs.forEach((input, cellIndex) => {
        input.value = source.cells[cellIndex] ?? "";
      });
      row.dataset.tare = source.tare ?? "0";
      row.dataset.netPrice = source.netPrice ?? "0";
      row.dataset.vatIncludedPrice = source.vatIncludedPrice ?? "0";
      setGridRowState(row, source.state);
      refreshGridRowState(row);
      row.classList.remove("selected", "selected-row");
      row.removeAttribute("aria-selected");
    });
  };

  const updateInvoiceTotal = () => {
    const totalField = document.querySelector(".stock-load-invoice-total-field input");
    if (!totalField || !linesGrid) {
      return;
    }

    const total = Array.from(linesGrid.querySelectorAll("tbody tr"))
      .reduce((sum, row) => {
        const amountInput = row.querySelector("td:nth-child(7) input");
        return sum + parseMoney(amountInput?.value);
      }, 0);
    totalField.value = formatMoney(total);
  };

  const lineRowValues = (row) => gridLineInputs(row).map((input) => input.value);

  const hasLineRowValues = (values) => values
    .some((value) => String(value ?? "").trim());

  const stockLoadRowsForSave = () => Array.from(linesGrid?.querySelectorAll("tbody tr") ?? [])
    .map((row, index) => {
      const values = lineRowValues(row);
      return {
        row,
        rowNumber: index + 1,
        rowState: refreshGridRowState(row),
        articleCode: String(values[0] ?? "").trim(),
        description: String(values[1] ?? "").trim(),
        unitMeasure: String(values[2] ?? "").trim(),
        quantity: parseDecimal(values[3]),
        price: parseDecimal(values[4]),
        discount: parsePercent(values[5]),
        amount: parseMoney(values[6]),
        vatRate: parsePercent(values[7]),
        tare: parseDecimal(row.dataset.tare),
        netPrice: parseDecimal(row.dataset.netPrice),
        vatIncludedPrice: parseDecimal(row.dataset.vatIncludedPrice),
        hasValues: hasLineRowValues(values)
      };
    });

  const resetStockLoadOverwriteState = () => {
    stockLoadOverwriteConfirmed = false;
    stockLoadOverwriteCheckedFromFe = false;
  };

  const buildStockLoadPayload = () => ({
    id: Number.parseInt(stockLoadId?.value || "0", 10) || 0,
    year: Number.parseInt(stockLoadYear?.value || "0", 10) || 0,
    code: Number.parseInt(stockLoadCode?.value || "0", 10) || 0,
    causeCode: Number.parseInt(stockLoadCause?.value || "10", 10) || 10,
    documentNumber: String(stockLoadDocumentNumber?.value ?? "").trim(),
    documentDate: toIsoDate(stockLoadDocumentDate?.value),
    supplierCode: Number.parseInt(stockLoadSupplierCode?.value || "0", 10) || 0,
    storeCode: Number.parseInt(stockLoadStore?.value || "0", 10) || 0,
    electronicInvoiceName: String(electronicInvoiceName?.value ?? "").trim(),
    electronicInvoicePath: String(electronicInvoiceFullPath?.value ?? "").trim(),
    allowOverwrite: stockLoadOverwriteConfirmed || stockLoadOverwriteCheckedFromFe,
    rows: stockLoadRowsForSave()
      .filter((row) => row.hasValues)
      .map(({ row, hasValues, rowState, ...payloadRow }) => payloadRow)
  });

  const validateStockLoadPayload = (payload) => {
    if (!payload.documentNumber) {
      showMessage("Numero documento obbligatorio.", stockLoadDocumentNumber);
      return false;
    }

    if (!payload.documentDate) {
      showMessage("Data documento obbligatoria.", stockLoadDocumentDate);
      return false;
    }

    if (![10, 12].includes(payload.causeCode)) {
      showMessage("Tipo carico non valido.", stockLoadCause);
      return false;
    }

    if (!payload.supplierCode) {
      showMessage(`${stockLoadSubjectKind().label} obbligatorio.`, stockLoadSupplierCodeDisplay);
      return false;
    }

    const valuedRows = stockLoadRowsForSave().filter((row) => row.hasValues);
    if (valuedRows.length === 0) {
      showMessage("Inserire almeno una riga articolo.");
      return false;
    }

    const missingCodeRow = valuedRows.find((row) => row.rowState === stockLoadRowState.missingCode || !row.articleCode);
    if (missingCodeRow) {
      showMessage(`Codice articolo obbligatorio alla riga ${missingCodeRow.rowNumber}.`);
      selectGridRowForValidation(missingCodeRow.row, true, 0);
      return false;
    }

    const missingArticleRow = valuedRows.find((row) => row.rowState === stockLoadRowState.missingArticle);
    if (missingArticleRow) {
      showMessage(`Risolvere l'articolo non trovato alla riga ${missingArticleRow.rowNumber}.`);
      selectGridRowForValidation(missingArticleRow.row, true, 0);
      return false;
    }

    const missingQuantityRow = valuedRows.find((row) => row.quantity === 0);
    if (missingQuantityRow) {
      showMessage(`Quantita' obbligatoria alla riga ${missingQuantityRow.rowNumber}.`);
      selectGridRowForValidation(missingQuantityRow.row, true, 0);
      return false;
    }

    return true;
  };

  const confirmStockLoadOverwrite = (result) => {
    window.MicronoteMessageBox?.show({
      title: "Micronote Food - attenzione",
      message: `${result.message || "Documento gia' registrato."} Sovrascrivere il carico esistente?`,
      mode: "confirm",
      variant: "confirm",
      okText: "Sovrascrivi",
      cancelText: "Annulla",
      onConfirm: () => {
        stockLoadOverwriteConfirmed = true;
        if (stockLoadId && result.id) {
          stockLoadId.value = String(result.id);
        }
        if (stockLoadYear && result.year) {
          stockLoadYear.value = String(result.year);
        }
        if (stockLoadCode && result.code) {
          stockLoadCode.value = String(result.code);
        }
        saveStockLoad();
      },
      onCancel: () => {
        resetStockLoadOverwriteState();
      }
    });
  };

  const resetStockLoadForNewEntry = () => {
    resetStockLoadOverwriteState();
    if (stockLoadId) {
      stockLoadId.value = "0";
    }
    if (stockLoadCode) {
      stockLoadCode.value = "0";
    }

    setFieldValue(stockLoadDocumentNumber, "");
    setDateFieldValue(stockLoadDocumentDate, "");
    setSelectValue(stockLoadCause, 10);
    setFieldValue(stockLoadSupplierCode, "");
    setFieldValue(stockLoadSupplierCodeDisplay, "");
    setFieldValue(stockLoadSupplierName, "");
    setSelectValue(stockLoadStore, 0);
    setFieldValue(electronicInvoiceName, "");
    setFieldValue(electronicInvoiceFullPath, "");
    selectedElectronicInvoiceFile = null;
    selectedElectronicInvoiceBrowserFile = null;
    fillStockLoadRows([]);
    updateInvoiceTotal();
    stockLoadDocumentNumber?.focus();
  };

  const showStockLoadProgress = () => {
    stockLoadProgressStartedAt = Date.now();
    window.MicronoteProgress?.show?.("Salvataggio in corso...");
  };

  const hideStockLoadProgress = (afterHide) => {
    const elapsed = Date.now() - stockLoadProgressStartedAt;
    window.setTimeout(() => {
      window.MicronoteProgress?.hide?.();
      afterHide?.();
    }, Math.max(0, 1000 - elapsed));
  };

  const postStockLoadPayload = async (payload, handler = "") => {
    stockLoadPayload.value = JSON.stringify(payload);
    const formData = new FormData(stockLoadForm);
    const url = new URL(window.location.href);
    if (handler) {
      url.searchParams.set("handler", handler);
    } else {
      url.searchParams.delete("handler");
    }

    const response = await fetch(url.toString(), {
      method: "POST",
      body: formData,
      headers: {
        "Accept": "application/json",
        "RequestVerificationToken": requestVerificationToken()
      }
    });
    const responseText = await response.text();
    let result = {};
    try {
      result = responseText ? JSON.parse(responseText) : {};
    } catch {
      throw new Error("invalid-json");
    }

    return { response, result };
  };

  const checkStockLoadOverwriteSignal = async () => {
    try {
      const payload = {
        ...buildStockLoadPayload(),
        allowOverwrite: false
      };
      const { result } = await postStockLoadPayload(payload, "OverwriteCheck");
      if (result.success) {
        stockLoadOverwriteCheckedFromFe = true;
        stockLoadOverwriteConfirmed = false;
      }

      if (result.requiresOverwrite) {
        window.MicronoteMessageBox?.show({
          title: "Micronote Food - attenzione",
          message: `${result.message || "Documento gia' registrato."} Sovrascrivere il carico esistente?`,
          mode: "confirm",
          variant: "confirm",
          okText: "Sovrascrivi",
          cancelText: "Annulla",
          onConfirm: () => {
            stockLoadOverwriteCheckedFromFe = true;
            stockLoadOverwriteConfirmed = false;
            if (stockLoadId && result.id) {
              stockLoadId.value = String(result.id);
            }
            if (stockLoadYear && result.year) {
              stockLoadYear.value = String(result.year);
            }
            if (stockLoadCode && result.code) {
              stockLoadCode.value = String(result.code);
            }
          },
          onCancel: () => {
            if (stockLoadReturnToPurchaseInvoice) {
              window.parent?.postMessage({ type: "micronote:stock-load-cancel" }, window.location.origin);
              return;
            }

            resetStockLoadForNewEntry();
          }
        });
      }
    } catch {
      // The import has already populated the form; save-time validation will report any persistent issue.
    }
  };

  const saveStockLoad = async () => {
    if (!stockLoadForm || !stockLoadPayload || !stockLoadSaveButton) {
      return;
    }

    if (stockLoadSaveInProgress) {
      return;
    }

    const payload = buildStockLoadPayload();
    if (!validateStockLoadPayload(payload)) {
      return;
    }

    stockLoadSaveInProgress = true;
    stockLoadSaveButton.disabled = true;
    let saveResult = null;

    try {
      if (!payload.allowOverwrite) {
        const check = await postStockLoadPayload(payload, "OverwriteCheck");
        if (!check.response.ok || !check.result.success) {
          if (check.result.requiresOverwrite) {
            confirmStockLoadOverwrite(check.result);
            return;
          }

          resetStockLoadOverwriteState();
          showMessage(check.result.message || "Controllo duplicazione non riuscito.");
          return;
        }
      }

      showStockLoadProgress();
      const { response, result } = await postStockLoadPayload(payload);
      saveResult = result;

      if (!response.ok || !result.success) {
        hideStockLoadProgress();
        resetStockLoadOverwriteState();
        showMessage(result.message || "Salvataggio non riuscito.");
        return;
      }
    } catch (error) {
      if (error?.message === "invalid-json") {
        hideStockLoadProgress();
        showMessage("Risposta del server non valida durante il salvataggio.");
        return;
      }

      hideStockLoadProgress();
      showMessage("Non e' stato possibile salvare il carico.");
      return;
    } finally {
      stockLoadSaveInProgress = false;
      stockLoadSaveButton.disabled = false;
    }

    resetStockLoadOverwriteState();
    if (stockLoadReturnToPurchaseInvoice) {
      hideStockLoadProgress(() => {
        window.parent?.postMessage({
          type: "micronote:stock-load-saved",
          id: saveResult?.id,
          year: saveResult?.year,
          code: saveResult?.code
        }, window.location.origin);
      });
      return;
    }

    if (!stockLoadReturnToList) {
      window.setTimeout(() => {
        resetStockLoadForNewEntry();
        window.MicronoteProgress?.hide?.();
      }, Math.max(0, 1000 - (Date.now() - stockLoadProgressStartedAt)));
      return;
    }

    if (saveResult?.editUrl) {
      window.location.assign(saveResult.editUrl);
      return;
    }

    window.location.reload();
  };

  const setFieldValue = (field, value) => {
    if (!field) {
      return;
    }

    field.value = value ?? "";
    field.dispatchEvent(new Event("change", { bubbles: true }));
  };

  const setSelectValue = (field, value) => {
    if (!field) {
      return;
    }

    const text = value === null || value === undefined ? "" : String(value);
    field.value = Array.from(field.options).some((option) => option.value === text) ? text : "0";
    field.dispatchEvent(new Event("change", { bubbles: true }));
  };

  const setDateFieldValue = (field, value) => {
    if (!field) {
      return;
    }

    field.value = toDisplayDate(value) || value || "";
    syncDateEmptyState(field);
    field.dispatchEvent(new Event("change", { bubbles: true }));
  };

  const createStockLoadGridRow = () => {
    const row = document.createElement("tr");
    const cells = [
      '<input type="text" readonly tabindex="-1" value="" />',
      '<input type="text" readonly tabindex="-1" value="" />',
      '<input type="text" readonly tabindex="-1" value="" />',
      '<input type="text" readonly tabindex="-1" inputmode="decimal" data-money-field value="" />',
      '<input type="text" readonly tabindex="-1" inputmode="decimal" data-money-field value="" />',
      '<input type="text" readonly tabindex="-1" inputmode="decimal" value="" />',
      '<input type="text" readonly tabindex="-1" inputmode="decimal" data-money-field value="" />',
      '<input type="text" readonly tabindex="-1" inputmode="decimal" data-percent-field value="" />',
      '<input type="hidden" data-stock-load-row-state value="" />'
    ];

    row.innerHTML = cells.map((cell, index) => `<td${index === 8 ? " hidden" : ""}>${cell}</td>`).join("");
    wireGridRow(row);
    return row;
  };

  const fillStockLoadRows = (rows) => {
    const tbody = linesGrid?.querySelector("tbody");
    if (tbody) {
      while (tbody.querySelectorAll("tr").length < rows.length) {
        tbody.append(createStockLoadGridRow());
      }
    }

    const gridRows = Array.from(linesGrid?.querySelectorAll("tbody tr") ?? []);
    gridRows.forEach((row) => wireGridRow(row));
    gridRows.forEach((row) => clearGridRow(row));

    rows.slice(0, gridRows.length).forEach((line, index) => {
      gridRows[index].dataset.articleFound = line.articleFound === false ? "false" : "true";
      gridRows[index].dataset.electronicArticleCode = line.electronicArticleCode ?? "";
      const inputs = gridLineInputs(gridRows[index]);
      inputs[0].value = line.articleCode ?? "";
      inputs[0].classList.toggle("stock-load-missing-article", line.articleFound === false);
      setGridRowState(gridRows[index], line.articleFound === false
        ? stockLoadRowState.missingArticle
        : stockLoadRowState.ok);
      if (line.articleFound === false && line.electronicArticleCode) {
        inputs[0].title = `Codice FE non trovato: ${line.electronicArticleCode}`;
      } else {
        inputs[0].removeAttribute("title");
      }
      inputs[1].value = line.description ?? "";
      inputs[2].value = line.unitMeasure ?? "";
      inputs[3].value = line.quantity ?? "";
      inputs[4].value = line.price ?? "";
      inputs[5].value = line.discount ?? "";
      inputs[6].value = line.amount ?? "";
      inputs[7].value = line.vatRate ?? "";
      const importedPrice = parseDecimal(line.price);
      const importedDiscount = parsePercent(line.discount);
      const importedVatRate = parsePercent(line.vatRate);
      const importedNetPrice = importedPrice * (1 - importedDiscount / 100);
      gridRows[index].dataset.tare = "0";
      gridRows[index].dataset.netPrice = importedNetPrice.toFixed(3);
      gridRows[index].dataset.vatIncludedPrice =
        (importedNetPrice * (1 + importedVatRate / 100)).toFixed(3);
    });

    updateInvoiceTotal();
    const firstRow = gridRows.find((row) => String(row.querySelector("td:first-child input")?.value ?? "").trim())
      ?? gridRows.find((row) => String(row.querySelector("td:nth-child(2) input")?.value ?? "").trim());
    if (firstRow) {
      selectGridRow(firstRow, false, 0);
    }
  };

  const applyElectronicInvoiceImport = (result) => {
    setFieldValue(electronicInvoiceName, result.fileName ?? selectedElectronicInvoiceFile?.name ?? "");
    setFieldValue(electronicInvoiceFullPath, result.fullPath ?? selectedElectronicInvoiceFile?.fullPath ?? "");
    setFieldValue(stockLoadDocumentNumber, result.documentNumber ?? "");
    setDateFieldValue(stockLoadDocumentDate, result.documentDate ?? "");

    const supplier = result.supplier ?? {};
    setFieldValue(stockLoadSupplierCode, supplier.code ? String(supplier.code) : "");
    setFieldValue(stockLoadSupplierCodeDisplay, supplier.codeDisplay ?? "");
    setFieldValue(stockLoadSupplierName, supplier.name ?? "");
    setSelectValue(stockLoadStore, supplier.storeCode ?? 0);

    fillStockLoadRows(result.rows ?? []);
    closeElectronicInvoiceDialog();
    checkStockLoadOverwriteSignal();
    if (Number(result.missingArticles ?? 0) > 0) {
      showMessage(`Articoli non presenti o non associati al fornitore: ${result.missingArticles}.`);
    }
  };

  const electronicInvoiceRows = () =>
    Array.from(electronicInvoiceFiles?.querySelectorAll("tr[data-full-path]") ?? []);

  const electronicInvoiceHeaderHeight = () =>
    electronicInvoiceGrid?.querySelector("thead")?.getBoundingClientRect().height ?? 24;

  const ensureElectronicInvoiceRowVisible = (row) => {
    if (!electronicInvoiceGrid || !row) {
      return;
    }

    const headerHeight = electronicInvoiceHeaderHeight();
    const rowTop = row.offsetTop;
    const rowBottom = rowTop + row.offsetHeight;
    const visibleTop = electronicInvoiceGrid.scrollTop + headerHeight;
    const visibleBottom = electronicInvoiceGrid.scrollTop + electronicInvoiceGrid.clientHeight;

    if (rowBottom > visibleBottom) {
      electronicInvoiceGrid.scrollTop = rowBottom - electronicInvoiceGrid.clientHeight + 1;
    } else if (rowTop < visibleTop) {
      electronicInvoiceGrid.scrollTop = Math.max(rowTop - headerHeight - 1, 0);
    }
  };

  const setElectronicInvoiceSelection = (row, options = {}) => {
    Array.from(electronicInvoiceFiles?.querySelectorAll("tr") ?? []).forEach((item) => {
      item.classList.toggle("is-selected", item === row);
    });

    selectedElectronicInvoiceFile = row
      ? {
          name: row.dataset.fileName ?? "",
          fullPath: row.dataset.fullPath ?? ""
        }
      : null;
    selectedElectronicInvoiceBrowserFile = row?._electronicInvoiceFile ?? null;

    if (row && options.ensureVisible !== false) {
      ensureElectronicInvoiceRowVisible(row);
    }
  };

  const visibleElectronicInvoiceRows = () => {
    if (!electronicInvoiceGrid) {
      return [];
    }

    const gridRect = electronicInvoiceGrid.getBoundingClientRect();
    const headerHeight = electronicInvoiceHeaderHeight();
    const top = gridRect.top + headerHeight;
    const bottom = gridRect.bottom;

    return electronicInvoiceRows().filter((row) => {
      const rowRect = row.getBoundingClientRect();
      return rowRect.bottom > top && rowRect.top < bottom;
    });
  };

  const selectVisibleElectronicInvoiceRow = (direction) => {
    const rows = visibleElectronicInvoiceRows();
    if (!rows.length) {
      return;
    }

    setElectronicInvoiceSelection(direction < 0 ? rows[rows.length - 1] : rows[0], {
      ensureVisible: false
    });
  };

  const moveElectronicInvoiceSelection = (direction) => {
    const rows = electronicInvoiceRows();
    if (!rows.length) {
      return;
    }

    const current = electronicInvoiceFiles?.querySelector("tr.is-selected[data-full-path]");
    const currentIndex = current ? rows.indexOf(current) : -1;
    const nextIndex = Math.max(0, Math.min(rows.length - 1, currentIndex + direction));
    const nextRow = rows[nextIndex < 0 ? 0 : nextIndex];

    setElectronicInvoiceSelection(nextRow);
    nextRow.focus({ preventScroll: true });
  };

  const renderElectronicInvoiceFiles = (files) => {
    if (!electronicInvoiceFiles) {
      return;
    }

    electronicInvoiceFiles.innerHTML = "";
    setElectronicInvoiceSelection(null);

    if (!files.length) {
      const emptyRow = document.createElement("tr");
      const emptyCell = document.createElement("td");
      emptyCell.colSpan = 4;
      emptyCell.textContent = "Nessuna fattura elettronica trovata.";
      emptyRow.append(emptyCell);
      electronicInvoiceFiles.append(emptyRow);
      return;
    }

    files.forEach((file) => {
      const row = document.createElement("tr");
      row.tabIndex = 0;
      row.dataset.fileName = file.name;
      row.dataset.fullPath = file.fullPath;
      row._electronicInvoiceFile = file.browserFile ?? null;

      [file.name, file.type, file.lastModified, file.size].forEach((value) => {
        const cell = document.createElement("td");
        cell.textContent = value ?? "";
        row.append(cell);
      });

      row.addEventListener("click", () => setElectronicInvoiceSelection(row));
      row.addEventListener("focus", () => setElectronicInvoiceSelection(row, { ensureVisible: false }));
      row.addEventListener("mouseenter", () => showElectronicInvoiceFilePreview(row));
      row.addEventListener("mouseleave", hideElectronicInvoiceFilePreview);
      row.addEventListener("dblclick", () => acceptElectronicInvoiceFile());
      row.addEventListener("keydown", (event) => {
        if (event.key === "Enter") {
          event.preventDefault();
          acceptElectronicInvoiceFile();
        }
      });

      electronicInvoiceFiles.append(row);
    });

    setElectronicInvoiceSelection(electronicInvoiceFiles.querySelector("tr[data-full-path]"), {
      ensureVisible: false
    });
  };

  const positionElectronicInvoicePreview = (row) => {
    if (!electronicInvoiceDialog || !electronicInvoicePreviewBox) {
      return;
    }

    const dialog = electronicInvoiceDialog.querySelector(".purchase-invoice-fe-dialog");
    if (!dialog) {
      return;
    }

    const dialogRect = dialog.getBoundingClientRect();
    const rowRect = row.getBoundingClientRect();
    const left = Math.min(Math.max(rowRect.left - dialogRect.left + 215, 10), dialogRect.width - 390);
    const top = Math.min(Math.max(rowRect.top - dialogRect.top + 18, 10), dialogRect.height - 130);

    electronicInvoicePreviewBox.style.left = `${left}px`;
    electronicInvoicePreviewBox.style.top = `${top}px`;
  };

  const setElectronicInvoicePreviewContent = (preview) => {
    if (!electronicInvoicePreviewBox) {
      return;
    }

    if (!preview.success) {
      electronicInvoicePreviewBox.textContent = preview.message ?? "Anteprima non disponibile.";
      return;
    }

    const rows = [
      ["Fornitore", preview.supplier],
      ["Cliente", preview.customer],
      ["Numero", preview.number],
      ["Data", preview.date],
      ["Importo", preview.amount]
    ];

    electronicInvoicePreviewBox.innerHTML = "";
    rows.forEach(([label, value]) => {
      const line = document.createElement("div");
      const labelElement = document.createElement("span");
      const separator = document.createElement("span");
      const valueElement = document.createElement("strong");

      labelElement.textContent = label;
      separator.textContent = ":";
      valueElement.textContent = value || "";
      line.append(labelElement, separator, valueElement);
      electronicInvoicePreviewBox.append(line);
    });
  };

  const showElectronicInvoiceFilePreview = async (row) => {
    if (!electronicInvoicePreviewBox) {
      return;
    }

    positionElectronicInvoicePreview(row);
    electronicInvoicePreviewBox.hidden = false;
    electronicInvoicePreviewBox.textContent = "Lettura fattura...";

    if (row._electronicInvoiceFile) {
      electronicInvoicePreviewBox.textContent = "Anteprima disponibile solo per file letti da percorso locale.";
      return;
    }

    const fileName = row.dataset.fileName ?? "";
    if (!fileName) {
      electronicInvoicePreviewBox.textContent = "Nome file non disponibile.";
      return;
    }

    electronicInvoicePreviewAbort?.abort();
    electronicInvoicePreviewAbort = new AbortController();

    const url = new URL(window.location.href);
    url.searchParams.set("handler", "ElectronicInvoicePreview");
    url.searchParams.set("fileName", fileName);

    try {
      const response = await fetch(url, {
        headers: { Accept: "application/json" },
        signal: electronicInvoicePreviewAbort.signal
      });

      if (!response.ok) {
        throw new Error("HTTP " + response.status);
      }

      setElectronicInvoicePreviewContent(await response.json());
    } catch (error) {
      if (error.name !== "AbortError") {
        electronicInvoicePreviewBox.textContent = "Anteprima non disponibile.";
      }
    }
  };

  const hideElectronicInvoiceFilePreview = () => {
    electronicInvoicePreviewAbort?.abort();
    if (electronicInvoicePreviewBox) {
      electronicInvoicePreviewBox.hidden = true;
    }
  };

  const formatBrowserFileSize = (bytes) => {
    if (!bytes) {
      return "0 KB";
    }

    return Math.max(1, Math.round(bytes / 1024)).toLocaleString("it-IT") + " KB";
  };

  const formatBrowserFileDate = (dateValue) => {
    if (!dateValue) {
      return "";
    }

    return new Intl.DateTimeFormat("it-IT", {
      day: "2-digit",
      month: "2-digit",
      year: "numeric",
      hour: "2-digit",
      minute: "2-digit"
    }).format(new Date(dateValue));
  };

  const renderBrowserFolderFiles = (fileList) => {
    const files = Array.from(fileList ?? [])
      .filter((file) => /\.(xml|p7m)$/i.test(file.name))
      .sort((left, right) => left.name.localeCompare(right.name, "it-IT", { sensitivity: "base" }))
      .map((file) => ({
        name: file.name,
        type: file.name.split(".").pop().toUpperCase(),
        lastModified: formatBrowserFileDate(file.lastModified),
        size: formatBrowserFileSize(file.size),
        fullPath: file.webkitRelativePath || file.name,
        browserFile: file
      }));

    if (electronicInvoiceCount) {
      electronicInvoiceCount.value = `${files.length} file`;
    }

    renderElectronicInvoiceFiles(files);

    if (electronicInvoicePath) {
      const firstPath = files[0]?.fullPath ?? "";
      setElectronicInvoicePathValue(firstPath.includes("/") ? firstPath.substring(0, firstPath.indexOf("/")) : "Cartella selezionata", "browser");
    }
  };

  const openSystemFolderPicker = async () => {
    if (!electronicInvoicePath) {
      return;
    }

    const url = new URL(window.location.href);
    url.searchParams.set("handler", "ElectronicInvoiceFolder");
    electronicInvoiceFolderPickerPending = true;
    electronicInvoiceFolderAbortController = new AbortController();

    try {
      const response = await fetch(url, {
        signal: electronicInvoiceFolderAbortController.signal,
        headers: { Accept: "application/json" }
      });

      if (!response.ok) {
        throw new Error("HTTP " + response.status);
      }

      const result = await response.json();
      if (!result.selected) {
        if (result.error) {
          showMessage(result.error);
        }
        return;
      }
      setElectronicInvoicePathValue(result.path, "server");
      await loadElectronicInvoiceFiles();
    } catch (error) {
      if (error?.name !== "AbortError") {
        showMessage("Non e' stato possibile aprire la selezione cartella.");
      }
    } finally {
      electronicInvoiceFolderPickerPending = false;
      electronicInvoiceFolderAbortController = null;
      electronicInvoiceSuppressEscapeUntil = Date.now() + 500;
    }
  };

  const loadElectronicInvoiceFiles = async () => {
    if (!electronicInvoiceFiles || !electronicInvoicePath) {
      return;
    }

    const url = new URL(window.location.href);
    url.searchParams.set("handler", "ElectronicInvoiceFiles");
    url.searchParams.set("search", electronicInvoiceSearch?.value ?? "");

    try {
      const response = await fetch(url, {
        headers: { Accept: "application/json" }
      });

      if (!response.ok) {
        throw new Error("HTTP " + response.status);
      }

      const result = await response.json();
      setElectronicInvoicePathValue(result.path ?? electronicInvoicePathValue(), "server");
      electronicInvoiceCount.value = `${result.count ?? 0} file`;
      renderElectronicInvoiceFiles(result.files ?? []);

      if (result.error) {
        showMessage(result.error);
      }
    } catch {
      electronicInvoiceCount.value = "0 file";
      renderElectronicInvoiceFiles([]);
      showMessage("Non e' stato possibile leggere la cartella indicata.");
    }
  };

  async function acceptElectronicInvoiceFile() {
    if (!selectedElectronicInvoiceFile) {
      showMessage("Selezionare una fattura elettronica.");
      return;
    }

    if (selectedElectronicInvoiceBrowserFile) {
      showMessage("La lettura automatica e' disponibile solo per file letti da percorso locale.");
      return;
    }

    if (!selectedElectronicInvoiceFile.fullPath) {
      showMessage("Percorso file non disponibile.");
      return;
    }

    const url = new URL(window.location.href);
    url.searchParams.set("handler", "ElectronicInvoiceImport");
    url.searchParams.set("fileName", selectedElectronicInvoiceFile.name || selectedElectronicInvoiceFile.fullPath);

    try {
      const response = await fetch(url, {
        headers: { Accept: "application/json" }
      });

      if (!response.ok) {
        throw new Error("HTTP " + response.status);
      }

      const result = await response.json();
      if (!result.success) {
        showMessage(result.message || "Non e' stato possibile leggere la fattura elettronica.");
        return;
      }

      applyElectronicInvoiceImport(result);
    } catch {
      showMessage("Non e' stato possibile leggere la fattura elettronica.");
    }
  }

  function viewElectronicInvoiceFile() {
    if (!selectedElectronicInvoiceFile) {
      showMessage("Selezionare una fattura elettronica.");
      return;
    }

    if (selectedElectronicInvoiceBrowserFile) {
      showMessage("La visualizzazione e' disponibile solo per file letti da percorso locale.");
      return;
    }

    if (!selectedElectronicInvoiceFile.fullPath) {
      showMessage("Percorso file non disponibile.");
      return;
    }

    if (!electronicInvoiceViewer || !electronicInvoiceViewerFrame) {
      showMessage("Visualizzatore fattura non disponibile.");
      return;
    }

    const url = new URL(window.location.href);
    url.searchParams.set("handler", "ElectronicInvoiceRaw");
    url.searchParams.set("fileName", selectedElectronicInvoiceFile.name || selectedElectronicInvoiceFile.fullPath);
    if (electronicInvoiceViewerTitle) {
      electronicInvoiceViewerTitle.textContent = selectedElectronicInvoiceFile.name || "Fattura elettronica";
    }

    electronicInvoiceViewerFrame.src = url.toString();
    electronicInvoiceViewer.hidden = false;
    document.body.classList.add("purchase-invoice-xml-open");
    electronicInvoiceViewerPanel?.focus();
  }

  function viewCurrentElectronicInvoiceFile() {
    const path = (electronicInvoiceFullPath?.value ?? "").trim();
    const fileName = (electronicInvoiceName?.value ?? "").trim();
    const fileToOpen = fileName || path.split(/[\\/]/).pop() || "";
    if (!fileToOpen) {
      showMessage("Fattura elettronica non selezionata.");
      return;
    }

    if (!electronicInvoiceViewer || !electronicInvoiceViewerFrame) {
      showMessage("Visualizzatore fattura non disponibile.");
      return;
    }

    const url = new URL(window.location.href);
    url.searchParams.set("handler", "ElectronicInvoiceRaw");
    url.searchParams.set("fileName", fileToOpen);
    const source = stockLoadElectronicInvoiceCurrentSource();
    if (source) {
      url.searchParams.set("source", source);
    }

    if (electronicInvoiceViewerTitle) {
      electronicInvoiceViewerTitle.textContent = fileToOpen || "Fattura elettronica";
    }

    electronicInvoiceViewerFrame.src = url.toString();
    electronicInvoiceViewer.hidden = false;
    window.requestAnimationFrame(() => {
      electronicInvoiceViewerPanel?.focus();
    });
  }

  async function deleteElectronicInvoiceFile() {
    if (!selectedElectronicInvoiceFile) {
      showMessage("Selezionare una fattura elettronica.");
      return;
    }

    if (selectedElectronicInvoiceBrowserFile) {
      showMessage("L'eliminazione e' disponibile solo per file letti da percorso locale.");
      return;
    }

    if (!selectedElectronicInvoiceFile.fullPath) {
      showMessage("Percorso file non disponibile.");
      return;
    }

    const fileName = selectedElectronicInvoiceFile.name || "file selezionato";
    window.MicronoteMessageBox?.show({
      title: "Elimina fattura elettronica",
      message: `Confermi l'eliminazione del file ${fileName}?`,
      mode: "confirm",
      variant: "confirm",
      okText: "Elimina",
      cancelText: "Annulla",
      onConfirm: async () => {
        const url = new URL(window.location.href);
        url.searchParams.set("handler", "DeleteElectronicInvoiceFile");

        try {
          const formData = new FormData();
          formData.append("path", selectedElectronicInvoiceFile.fullPath);

          const token = requestVerificationToken();
          if (token) {
            formData.append("__RequestVerificationToken", token);
          }

          const response = await fetch(url, {
            method: "POST",
            body: formData,
            headers: { Accept: "application/json" }
          });

          if (!response.ok) {
            throw new Error("HTTP " + response.status);
          }

          const result = await response.json();
          if (!result.success) {
            showMessage(result.message || "Non e' stato possibile eliminare il file.");
            return;
          }

          hideElectronicInvoiceFilePreview();
          selectedElectronicInvoiceFile = null;
          selectedElectronicInvoiceBrowserFile = null;
          await loadElectronicInvoiceFiles();
        } catch {
          showMessage("Non e' stato possibile eliminare il file.");
        }
      }
    });
  }

  const closeElectronicInvoiceViewer = () => {
    if (!electronicInvoiceViewer) {
      return;
    }

    electronicInvoiceViewer.hidden = true;
    document.body.classList.remove("purchase-invoice-xml-open");
    if (electronicInvoiceViewerFrame) {
      electronicInvoiceViewerFrame.removeAttribute("src");
    }

    electronicInvoicePreview?.focus();
  };

  const openElectronicInvoiceDialog = () => {
    if (!electronicInvoiceDialog) {
      return;
    }

    electronicInvoiceDialog.hidden = false;
    document.body.classList.add("purchase-invoice-fe-open");
    loadElectronicInvoiceFiles();
    electronicInvoiceGrid?.focus();
  };

  const closeElectronicInvoiceDialog = () => {
    if (!electronicInvoiceDialog) {
      return;
    }

    electronicInvoiceDialog.hidden = true;
    document.body.classList.remove("purchase-invoice-fe-open");
    hideElectronicInvoiceFilePreview();
    electronicInvoiceOpen?.focus();
  };

  const closeArticleCardModal = () => {
    if (!articleCardModal) {
      return;
    }

    articleCardModal.hidden = true;
    document.body.classList.remove("purchase-invoice-payment-modal-open");
    articleCardModalFrame?.removeAttribute("src");
    articleCardButton?.focus();
  };

  const openSelectedArticleCard = () => {
    const row = selectedGridRow();
    const articleCode = String(row?.querySelector("td:first-child input")?.value ?? "").trim();
    if (!row || !articleCode) {
      showMessage("Selezionare una riga con codice articolo.");
      return;
    }

    if (isMissingArticleRow(row)) {
      showMessage("Articolo non presente in archivio.");
      return;
    }

    if (!articleCardModal || !articleCardModalFrame) {
      showMessage("Scheda articolo non disponibile.");
      return;
    }

    const returnUrl = `${window.location.pathname}${window.location.search}${window.location.hash}`;
    const azione = formAzione.withContesto(formAzione.modifica, formAzione.modale);
    articleCardModalFrame.src = `/Articoli/Edit/${encodeURIComponent(articleCode)}?azione=${azione}&returnUrl=${encodeURIComponent(returnUrl)}`;
    articleCardModal.hidden = false;
    document.body.classList.add("purchase-invoice-payment-modal-open");
    articleCardModalFrame.focus();
  };

  const openAddArticleFromSelectedLine = () => {
    const row = selectedGridRow();
    if (!row) {
      showMessage("Selezionare una riga articolo da aggiungere.");
      return;
    }

    const inputs = gridLineInputs(row);
    const articleCode = String(inputs[0]?.value ?? "").trim();
    if (!articleCode) {
      showMessage("Selezionare una riga con codice articolo.");
      return;
    }

    if (!isMissingArticleRow(row)) {
      showMessage("La funzione Aggiungi articolo e' riservata alle righe non trovate.");
      return;
    }

    if (!articleCardModal || !articleCardModalFrame) {
      showMessage("Scheda articolo non disponibile.");
      return;
    }

    const returnUrl = `${window.location.pathname}${window.location.search}${window.location.hash}`;
    const url = new URL("/Articoli/Edit", window.location.origin);
    url.searchParams.set("azione", String(formAzione.withContesto(
      formAzione.inserimento,
      formAzione.modale,
      formAzione.origineFe,
      formAzione.codiceBloccato)));
    url.searchParams.set("returnUrl", returnUrl);
    url.searchParams.set("prefillCode", articleCode);
    url.searchParams.set("description", String(inputs[1]?.value ?? "").trim());
    url.searchParams.set("unitMeasure", String(inputs[2]?.value ?? "").trim());
    url.searchParams.set("standardCost", String(inputs[4]?.value ?? "").trim());
    url.searchParams.set("vatRate", String(inputs[7]?.value ?? "").trim());
    url.searchParams.set("supplierArticleCode", row.dataset.electronicArticleCode || articleCode);

    const supplierCode = String(stockLoadSupplierCode?.value ?? "").trim();
    if (supplierCode) {
      url.searchParams.set("supplierCode", supplierCode);
    }

    articleCardModalFrame.src = `${url.pathname}${url.search}`;
    articleCardModal.hidden = false;
    document.body.classList.add("purchase-invoice-payment-modal-open");
    articleCardModalFrame.focus();
  };

  const openAssignArticleLookup = async () => {
    const row = selectedGridRow();
    if (!row) {
      showMessage("Selezionare una riga a cui assegnare il codice articolo.");
      return;
    }

    const hasAnyValue = gridLineInputs(row)
      .some((input) => String(input.value ?? "").trim());
    if (!hasAnyValue) {
      showMessage("Selezionare una riga valorizzata.");
      return;
    }

    await openArticleLookup("assign");
  };

  applyStockLoadSubjectKind(false);
  [
    stockLoadDocumentNumber,
    stockLoadDocumentDate,
    stockLoadSupplierCode,
    stockLoadSupplierCodeDisplay
  ].forEach((field) => {
    field?.addEventListener("input", resetStockLoadOverwriteState);
    field?.addEventListener("change", resetStockLoadOverwriteState);
  });
  stockLoadCause?.addEventListener("change", () => {
    resetStockLoadOverwriteState();
    applyStockLoadSubjectKind(true);
  });
  stockLoadSaveButton?.addEventListener("click", saveStockLoad);
  stockLoadModalCancel?.addEventListener("click", (event) => {
    event.preventDefault();
    window.parent?.postMessage({ type: "micronote:stock-load-cancel" }, window.location.origin);
  });
  electronicInvoiceOpen?.addEventListener("click", openElectronicInvoiceDialog);
  articleCardButton?.addEventListener("click", openSelectedArticleCard);
  addArticleButton?.addEventListener("click", openAddArticleFromSelectedLine);
  assignArticleButton?.addEventListener("click", openAssignArticleLookup);
  stockLoadCancelLink?.addEventListener("click", (event) => {
    if (!stockLoadReturnToPurchaseInvoice) {
      return;
    }

    event.preventDefault();
    window.parent?.postMessage({ type: "micronote:stock-load-cancel" }, window.location.origin);
  });
  articleCardModal?.addEventListener("click", (event) => {
    if (event.target === articleCardModal) {
      closeArticleCardModal();
    }
  });
  window.addEventListener("message", (event) => {
    if (event.origin !== window.location.origin) {
      return;
    }

    if (event.data?.type === "micronote:article-saved") {
      const row = selectedGridRow();
      const codeInput = row?.querySelector("td:first-child input");
      if (codeInput) {
        codeInput.value = event.data?.code || codeInput.value;
        codeInput.classList.remove("stock-load-missing-article");
        codeInput.classList.add("stock-load-assigned-article");
        codeInput.removeAttribute("title");
        row.dataset.articleFound = "true";
        setGridRowState(row, stockLoadRowState.assignedArticle);
      }

      closeArticleCardModal();
      return;
    }

    if (event.data?.type === "micronote:article-cancel") {
      closeArticleCardModal();
    }
  });
  electronicInvoiceGrid?.setAttribute("tabindex", "0");
  electronicInvoiceGrid?.addEventListener("click", () => electronicInvoiceGrid.focus());
  electronicInvoiceGrid?.addEventListener("keydown", (event) => {
    if (event.key === "ArrowDown") {
      event.preventDefault();
      moveElectronicInvoiceSelection(1);
      return;
    }

    if (event.key === "ArrowUp") {
      event.preventDefault();
      moveElectronicInvoiceSelection(-1);
      return;
    }

    if (event.key === "Enter") {
      event.preventDefault();
      acceptElectronicInvoiceFile();
    }
  });
  electronicInvoiceGrid?.addEventListener("scroll", () => {
    const currentScrollTop = electronicInvoiceGrid.scrollTop;
    const direction = currentScrollTop >= electronicInvoiceLastScrollTop ? 1 : -1;
    electronicInvoiceLastScrollTop = currentScrollTop;

    const selectedRow = electronicInvoiceFiles?.querySelector("tr.is-selected[data-full-path]");
    if (!selectedRow) {
      selectVisibleElectronicInvoiceRow(direction);
      return;
    }

    const visibleRows = visibleElectronicInvoiceRows();
    if (!visibleRows.includes(selectedRow)) {
      selectVisibleElectronicInvoiceRow(direction);
    }
  });
  electronicInvoiceGrid?.addEventListener("wheel", (event) => {
    const direction = event.deltaY < 0 ? -1 : 1;

    if (electronicInvoiceWheelFrame) {
      window.cancelAnimationFrame(electronicInvoiceWheelFrame);
    }

    electronicInvoiceWheelFrame = window.requestAnimationFrame(() => {
      electronicInvoiceWheelFrame = window.requestAnimationFrame(() => {
        electronicInvoiceWheelFrame = 0;
        const selectedRow = electronicInvoiceFiles?.querySelector("tr.is-selected[data-full-path]");
        const visibleRows = visibleElectronicInvoiceRows();
        if (!selectedRow || !visibleRows.includes(selectedRow)) {
          selectVisibleElectronicInvoiceRow(direction);
        }
        electronicInvoiceLastScrollTop = electronicInvoiceGrid.scrollTop;
      });
    });
  });
  electronicInvoiceRefresh?.addEventListener("click", openSystemFolderPicker);
  electronicInvoiceFolderPicker?.addEventListener("change", () => {
    renderBrowserFolderFiles(electronicInvoiceFolderPicker.files);
  });
  electronicInvoiceSearch?.addEventListener("input", loadElectronicInvoiceFiles);
  electronicInvoiceSearchClear?.addEventListener("click", () => {
    if (!electronicInvoiceSearch) {
      return;
    }

    electronicInvoiceSearch.value = "";
    loadElectronicInvoiceFiles();
    electronicInvoiceSearch.focus();
  });
  electronicInvoiceAccept?.addEventListener("click", acceptElectronicInvoiceFile);
  electronicInvoiceCurrentPreview?.addEventListener("click", viewCurrentElectronicInvoiceFile);
  electronicInvoicePreview?.addEventListener("click", viewElectronicInvoiceFile);
  electronicInvoiceDelete?.addEventListener("click", deleteElectronicInvoiceFile);
  electronicInvoiceViewerCloseButtons.forEach((button) => {
    button.addEventListener("click", closeElectronicInvoiceViewer);
  });
  electronicInvoiceViewer?.addEventListener("click", (event) => {
    if (event.target === electronicInvoiceViewer) {
      closeElectronicInvoiceViewer();
    }
  });
  electronicInvoiceCloseButtons.forEach((button) => {
    button.addEventListener("click", closeElectronicInvoiceDialog);
  });
  electronicInvoiceDialog?.addEventListener("click", (event) => {
    if (event.target === electronicInvoiceDialog) {
      closeElectronicInvoiceDialog();
    }
  });

  document.addEventListener("keydown", (event) => {
    if (event.key !== "Escape") {
      return;
    }

    if (electronicInvoiceViewer && !electronicInvoiceViewer.hidden) {
      event.preventDefault();
      closeElectronicInvoiceViewer();
      return;
    }

    if (articleCardModal && !articleCardModal.hidden) {
      event.preventDefault();
      closeArticleCardModal();
      return;
    }

    if (electronicInvoiceFolderPickerPending || Date.now() < electronicInvoiceSuppressEscapeUntil) {
      event.preventDefault();
      event.stopPropagation();
      electronicInvoiceFolderAbortController?.abort();
      return;
    }

    if (electronicInvoiceDialog && !electronicInvoiceDialog.hidden) {
      event.preventDefault();
      closeElectronicInvoiceDialog();
    }
  });

  const importXmlPath = new URLSearchParams(window.location.search).get("importXml");
  if (importXmlPath) {
    selectedElectronicInvoiceFile = {
      name: importXmlPath.split(/[\\/]/).pop() || "",
      fullPath: importXmlPath
    };
    acceptElectronicInvoiceFile();
  }

  const closeLineDialog = () => {
    if (!lineOverlay) {
      return;
    }

    lineOverlay.hidden = true;
    document.body.classList.remove("lookup-open");
    targetLineRow = null;
  };

  const openLineDialog = () => {
    targetLineRow = findFirstFreeRow();
    if (!targetLineRow) {
      showMessage("Non ci sono righe libere nella griglia.");
      return;
    }

    clearLineDialog();
    const lineTitle = lineOverlay.querySelector("[data-line-title]");
    if (lineTitle) {
      lineTitle.textContent = "Inserimento articolo";
    }
    lineOverlay.hidden = false;
    document.body.classList.add("lookup-open");
    window.setTimeout(() => lineFields.code?.focus(), 0);
  };

  const openLineDialogForEdit = () => {
    const row = selectedGridRow();
    if (!row) {
      showMessage("Selezionare una riga da modificare.");
      return;
    }

    const cells = gridLineInputs(row);
    targetLineRow = row;
    lineFields.code.value = cells[0]?.value ?? "";
    lineFields.description.value = cells[1]?.value ?? "";
    lineFields.unit.value = cells[2]?.value ?? "";
    lineFields.quantity.value = cells[3]?.value ?? "";
    lineFields.price.value = cells[4]?.value ?? "";
    lineFields.discount.value = cells[5]?.value ?? "";
    lineFields.amount.value = cells[6]?.value ?? "";
    lineFields.vat.value = cells[7]?.value ?? "";
    lineFields.stock.value = "";
    lineFields.tare.value = "";
    lineFields.lastPrice.value = "";
    lineFields.lastVat.value = "";
    lineFields.packages.value = "";
    lineFields.vatPrice.value = "";
    const lineTitle = lineOverlay.querySelector("[data-line-title]");
    if (lineTitle) {
      lineTitle.textContent = "Modifica articolo";
    }
    lineOverlay.hidden = false;
    document.body.classList.add("lookup-open");
    window.setTimeout(() => lineFields.quantity?.focus(), 0);
  };

  const deleteSelectedLine = () => {
    const row = selectedGridRow();
    if (!row) {
      showMessage("Selezionare una riga da cancellare.");
      return;
    }

    window.MicronoteMessageBox?.show({
      title: "Micronote Food - conferma",
      message: "Cancellare la riga selezionata?",
      mode: "confirm",
      variant: "confirm",
      okText: "OK",
      cancelText: "Annulla",
      onConfirm: () => {
        clearGridRow(row);
        compactGridRows();
        updateInvoiceTotal();
      }
    });
  };

  const applyArticleToDialog = (article) => {
    if (!article) {
      return;
    }

    lineFields.code.value = article.code ?? lineFields.code.value;
    lineFields.description.value = article.description ?? "";
    lineFields.unit.value = article.purchaseUnitMeasure ?? article.unitMeasure ?? "";
    lineFields.stock.value = formatNumber(article.stock ?? 0, 3);
    lineFields.tare.value = formatNumber(article.tare ?? 0, 3);
    lineFields.lastPrice.value = formatMoney(Number(article.lastPrice ?? 0));
    lineFields.lastVat.value = "";
    lineFields.price.value = formatMoney(Number(article.price ?? 0));
    lineFields.vat.value = formatPercent(Number(article.vatRate ?? 0));
    calculateLineAmount();
  };

  const escapeHtml = (value) => String(value ?? "").replace(/[&<>"']/g, (char) => ({
    "&": "&amp;",
    "<": "&lt;",
    ">": "&gt;",
    "\"": "&quot;",
    "'": "&#039;"
  }[char]));

  const buildLookupOptions = (select, rows, codeKey, labelKey) => {
    if (!select) {
      return;
    }

    const options = new Map();
    rows.forEach((row) => {
      const code = row[codeKey];
      const label = row[labelKey];
      if (code && label && !options.has(String(code))) {
        options.set(String(code), label);
      }
    });

    select.innerHTML = `<option value=""></option>${Array.from(options.entries())
      .sort((left, right) => String(left[1]).localeCompare(String(right[1]), "it", { sensitivity: "base", numeric: true }))
      .map(([code, label]) => `<option value="${escapeHtml(code)}">${escapeHtml(label)}</option>`)
      .join("")}`;
  };

  const sortArticleRows = () => {
    const direction = articleLookupSortDirection === "desc" ? -1 : 1;
    articleLookupFilteredRows.sort((left, right) => {
      if (articleLookupSortKey === "price" || articleLookupSortKey === "vatRate") {
        return ((Number(left[articleLookupSortKey]) || 0) - (Number(right[articleLookupSortKey]) || 0)) * direction;
      }

      return String(left[articleLookupSortKey] ?? "").localeCompare(String(right[articleLookupSortKey] ?? ""), "it", {
        sensitivity: "base",
        numeric: true
      }) * direction;
    });
  };

  const isArticleLookupRowVisible = (row) => {
    const frame = articleLookupFields.frame;
    if (!row || !frame) {
      return false;
    }

    const headerHeight = frame.querySelector("thead")?.offsetHeight || 0;
    const frameRect = frame.getBoundingClientRect();
    const rowRect = row.getBoundingClientRect();
    return rowRect.top >= frameRect.top + headerHeight + 1 && rowRect.bottom <= frameRect.bottom - 1;
  };

  const ensureArticleLookupSelectedVisible = (direction = 0) => {
    const row = articleLookup?.querySelector("tbody tr.selected");
    const frame = articleLookupFields.frame;
    if (!row || !frame) {
      return;
    }

    const headerHeight = frame.querySelector("thead")?.offsetHeight || 0;
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

  const setArticleLookupSelectedIndex = (index, direction = 0) => {
    if (index < 0 || index >= articleLookupFilteredRows.length) {
      return;
    }

    articleLookupSelectedIndex = index;
    articleLookup?.querySelectorAll("tbody tr").forEach((row) => {
      row.classList.toggle("selected", Number(row.dataset.index) === articleLookupSelectedIndex);
    });
    ensureArticleLookupSelectedVisible(direction);
  };

  const renderArticleLookupRows = () => {
    if (!articleLookupFields.body) {
      return;
    }

    articleLookup?.querySelectorAll("[data-article-sort]").forEach((button) => {
      const active = button.dataset.articleSort === articleLookupSortKey;
      button.classList.toggle("is-active", active);
      button.dataset.direction = active ? articleLookupSortDirection : "";
    });

    articleLookupFields.body.innerHTML = articleLookupFilteredRows.map((row, index) => `
      <tr data-index="${index}" class="${index === articleLookupSelectedIndex ? "selected" : ""}">
        <td>${escapeHtml(row.code)}</td>
        <td>${escapeHtml(row.description)}</td>
        <td>${escapeHtml(row.category)}</td>
        <td>${escapeHtml(row.group)}</td>
        <td>${escapeHtml(row.subgroup)}</td>
        <td>${formatMoney(Number(row.price ?? 0))}</td>
        <td>${formatPercent(Number(row.vatRate ?? 0))}</td>
      </tr>
    `).join("");

    if (articleLookupFields.count) {
      articleLookupFields.count.value = String(articleLookupFilteredRows.length);
    }

    ensureArticleLookupSelectedVisible();
  };

  const filterArticleLookupRows = () => {
    const search = String(articleLookupFields.search?.value ?? "").trim().toLocaleLowerCase("it");
    const category = articleLookupFields.category?.value ?? "";
    const supplier = articleLookupFields.supplier?.value ?? "";

    articleLookupFilteredRows = articleLookupRows.filter((row) => {
      const matchesSearch = !search
        || String(row.code ?? "").toLocaleLowerCase("it").includes(search)
        || String(row.description ?? "").toLocaleLowerCase("it").includes(search);
      const matchesCategory = !category || String(row.categoryCode ?? "") === category;
      const matchesSupplier = !supplier || String(row.supplierCode ?? "") === supplier;
      return matchesSearch && matchesCategory && matchesSupplier;
    });
    sortArticleRows();
    articleLookupSelectedIndex = articleLookupFilteredRows.length > 0 ? 0 : -1;
    renderArticleLookupRows();
  };

  const loadArticleLookupRows = async () => {
    const response = await fetch("/api/articoli", {
      headers: { "Accept": "application/json" }
    });
    const payload = await response.json();
    articleLookupRows = Array.isArray(payload.rows) ? payload.rows : [];
    buildLookupOptions(articleLookupFields.category, articleLookupRows, "categoryCode", "category");
    buildLookupOptions(articleLookupFields.supplier, articleLookupRows, "supplierCode", "supplier");
    filterArticleLookupRows();
  };

  const closeArticleLookup = () => {
    if (!articleLookup) {
      return;
    }

    articleLookup.hidden = true;
    lineDialog?.classList.remove("is-dimmed");
    if (articleLookupMode === "assign") {
      linesGridWrap?.focus({ preventScroll: true });
    } else {
      lineFields.code?.focus();
    }
    articleLookupMode = "line";
  };

  const openArticleLookup = async (mode = "line") => {
    if (!articleLookup) {
      return;
    }

    articleLookupMode = mode;
    articleLookup.hidden = false;
    if (mode === "line") {
      lineDialog?.classList.add("is-dimmed");
    }
    articleLookupFields.search.value = "";
    await loadArticleLookupRows();
    window.setTimeout(() => articleLookupFields.search?.focus(), 0);
  };

  const assignArticleToSelectedRow = (article) => {
    const row = selectedGridRow();
    if (!row || !article) {
      return;
    }

    const inputs = gridLineInputs(row);
    inputs[0].value = article.code ?? "";
    inputs[0].classList.remove("stock-load-missing-article");
    inputs[0].classList.add("stock-load-assigned-article");
    inputs[0].removeAttribute("title");
    inputs[1].value = article.description ?? "";
    row.dataset.articleFound = "true";
    setGridRowState(row, stockLoadRowState.assignedArticle);
  };

  const selectArticleLookupRow = () => {
    const row = articleLookupFilteredRows[articleLookupSelectedIndex];
    if (!row) {
      return;
    }

    if (articleLookupMode === "assign") {
      assignArticleToSelectedRow(row);
      closeArticleLookup();
      return;
    }

    applyArticleToDialog(row);
    closeArticleLookup();
    lineFields.quantity?.focus();
  };

  const loadArticle = async () => {
    const code = String(lineFields.code?.value ?? "").trim();
    if (!code) {
      clearLineDialog();
      return;
    }

    try {
      const response = await fetch(`/api/articoli/${encodeURIComponent(code)}`, {
        headers: { "Accept": "application/json" }
      });

      if (!response.ok) {
        clearLineDialog();
        lineFields.code.value = code;
        showMessage("Articolo non presente in archivio.", lineFields.code);
        return;
      }

      applyArticleToDialog(await response.json());
    } catch {
      showMessage("Non e' stato possibile leggere l'articolo.", lineFields.code);
    }
  };

  const confirmLineDialog = () => {
    if (!targetLineRow) {
      closeLineDialog();
      return;
    }

    if (!String(lineFields.code?.value ?? "").trim()) {
      showMessage("Campo Articolo obbligatorio", lineFields.code);
      return;
    }

    if (parseDecimal(lineFields.quantity?.value) === 0) {
      showMessage("Campo Quantita' obbligatorio", lineFields.quantity);
      return;
    }

    calculateLineAmount();
    const cells = gridLineInputs(targetLineRow);
    cells[0].value = lineFields.code.value.trim();
    cells[1].value = lineFields.description.value.trim();
    cells[2].value = lineFields.unit.value.trim();
    cells[3].value = formatNumber(parseDecimal(lineFields.quantity.value), 3);
    const price = parseDecimal(lineFields.price.value);
    const discount = parsePercent(lineFields.discount.value);
    const vatRate = parsePercent(lineFields.vat.value);
    const netPrice = price * (1 - discount / 100);
    cells[4].value = formatNumber(price, 3);
    cells[5].value = formatPercent(parsePercent(lineFields.discount.value));
    cells[6].value = formatMoney(parseMoney(lineFields.amount.value));
    cells[7].value = formatPercent(parsePercent(lineFields.vat.value));
    targetLineRow.dataset.netPrice = netPrice.toFixed(3);
    targetLineRow.dataset.vatIncludedPrice = (netPrice * (1 + vatRate / 100)).toFixed(3);
    targetLineRow.dataset.tare ||= "0";
    targetLineRow.dataset.articleFound = "true";
    setGridRowState(targetLineRow, stockLoadRowState.ok);
    updateInvoiceTotal();
    selectGridRow(targetLineRow);
    closeLineDialog();
  };

  if (formPage && lineOverlay) {
    lineOverlay.querySelectorAll("[data-money-field]").forEach((field) => {
      window.MicronoteMoney?.wire?.(field, { onBlur: calculateLineAmount, onInput: calculateLineAmount });
    });
    lineOverlay.querySelectorAll("[data-percent-field]").forEach((field) => {
      window.MicronotePercent?.wire?.(field, { onBlur: calculateLineAmount, onInput: calculateLineAmount });
    });

    lineFields.quantity?.addEventListener("input", () => {
      const text = lineFields.quantity.value.replace(/[^\d.,]/g, "");
      const separatorMatches = Array.from(text.matchAll(/[.,]/g));
      const decimalIndex = separatorMatches.length > 1
        ? separatorMatches[separatorMatches.length - 1].index
        : (separatorMatches[0]?.index ?? -1);
      lineFields.quantity.value = decimalIndex >= 0
        ? `${text.slice(0, decimalIndex).replace(/[.,]/g, "").slice(0, 7)}.${text.slice(decimalIndex + 1).replace(/[.,]/g, "").slice(0, 3)}`
        : text.replace(/[.,]/g, "").slice(0, 7);
      calculateLineAmount();
    });
    lineFields.quantity?.addEventListener("focus", () => {
      lineFields.quantity.value = parseDecimal(lineFields.quantity.value) || "";
      lineFields.quantity.select?.();
    });
    lineFields.quantity?.addEventListener("blur", () => {
      lineFields.quantity.value = formatNumber(parseDecimal(lineFields.quantity.value), 3);
      calculateLineAmount();
    });
    lineFields.price?.addEventListener("input", () => {
      const text = lineFields.price.value.replace(/[^\d.,]/g, "");
      const separatorMatches = Array.from(text.matchAll(/[.,]/g));
      const decimalIndex = separatorMatches.length > 1
        ? separatorMatches[separatorMatches.length - 1].index
        : (separatorMatches[0]?.index ?? -1);
      lineFields.price.value = decimalIndex >= 0
        ? `${text.slice(0, decimalIndex).replace(/[.,]/g, "").slice(0, 7)}.${text.slice(decimalIndex + 1).replace(/[.,]/g, "").slice(0, 3)}`
        : text.replace(/[.,]/g, "").slice(0, 7);
      calculateLineAmount();
    });
    lineFields.price?.addEventListener("focus", () => {
      lineFields.price.value = parseDecimal(lineFields.price.value) || "";
      lineFields.price.select?.();
    });
    lineFields.price?.addEventListener("blur", () => {
      lineFields.price.value = formatNumber(parseDecimal(lineFields.price.value), 3);
      calculateLineAmount();
    });

    document.querySelector("[data-stock-load-line-open]")?.addEventListener("click", openLineDialog);
    document.querySelector("[data-stock-load-line-edit]")?.addEventListener("click", openLineDialogForEdit);
    document.querySelector("[data-stock-load-line-delete]")?.addEventListener("click", deleteSelectedLine);
    linesGrid?.querySelector("tbody")?.addEventListener("click", (event) => {
      const row = event.target.closest("tr");
      if (row) {
        selectGridRow(row, true, 0);
      }
    });
    linesGridWrap?.setAttribute("tabindex", "0");
    linesGridWrap?.addEventListener("keydown", (event) => {
      navigateGridRows(event, selectedGridRow() || visibleGridRows()[0]);
    });
    Array.from(linesGrid.querySelectorAll("tbody tr")).forEach((row) => wireGridRow(row));
    if (visibleGridRows().length > 0 && !selectedGridRow()) {
      selectGridRow(visibleGridRows()[0], false, 0);
    }

    if (linesGridWrap && linesGrid) {
      let lastGridScrollTop = linesGridWrap.scrollTop;
      let gridScrollFrame = 0;

      linesGridWrap.addEventListener("scroll", () => {
        if (gridScrollFrame) {
          window.cancelAnimationFrame(gridScrollFrame);
        }

        gridScrollFrame = window.requestAnimationFrame(() => {
          gridScrollFrame = 0;
          const currentScrollTop = linesGridWrap.scrollTop;
          const delta = currentScrollTop - lastGridScrollTop;
          lastGridScrollTop = currentScrollTop;

          if (delta === 0) {
            return;
          }

          const selected = selectedGridRow();
          if (!selected) {
            return;
          }

          const gridRect = linesGridWrap.getBoundingClientRect();
          const headerHeight = linesGrid.tHead?.getBoundingClientRect().height ?? 0;
          const viewportTop = gridRect.top + headerHeight;
          const viewportBottom = gridRect.bottom;
          const selectedRect = selected.getBoundingClientRect();
          const isVisible = selectedRect.bottom > viewportTop + 1 &&
            selectedRect.top < viewportBottom - 1;

          if (isVisible) {
            return;
          }

          const visible = visibleGridRows().filter((candidate) => {
            const rect = candidate.getBoundingClientRect();
            return rect.top >= viewportTop + 1 && rect.bottom <= viewportBottom - 1;
          });

          if (visible.length > 0) {
            selectGridRow(delta > 0 ? visible[0] : visible[visible.length - 1], true, delta > 0 ? 1 : -1);
          }
        });
      }, { passive: true });
    }
    lineOverlay.querySelector("[data-line-cancel]")?.addEventListener("click", closeLineDialog);
    lineOverlay.querySelector("[data-line-confirm]")?.addEventListener("click", confirmLineDialog);
    lineFields.code?.addEventListener("change", loadArticle);
    [lineFields.quantity, lineFields.price, lineFields.discount, lineFields.vat].forEach((field) => {
      field?.addEventListener("blur", calculateLineAmount);
    });

    lineDialog?.addEventListener("keydown", (event) => {
      if (event.key === "Escape") {
        event.preventDefault();
        closeLineDialog();
      }

      if (event.key === "F5" && event.target === lineFields.code) {
        event.preventDefault();
        openArticleLookup();
      }

      if (event.key === "Tab" || event.key === "Enter") {
        const tabFields = [
          lineFields.code,
          lineOverlay.querySelector("[data-article-lookup-open]"),
          lineFields.quantity,
          lineFields.price,
          lineFields.discount,
          lineFields.vat,
          lineOverlay.querySelector("[data-line-confirm]"),
          lineOverlay.querySelector("[data-line-cancel]")
        ].filter(Boolean);
        const currentIndex = tabFields.indexOf(event.target);
        if (currentIndex >= 0) {
          if (event.key === "Enter" && event.target instanceof HTMLButtonElement) {
            return;
          }

          event.preventDefault();
          if (event.key === "Enter" && event.target === lineFields.code) {
            loadArticle().then(() => lineFields.quantity?.focus());
            return;
          }

          const nextIndex = event.shiftKey
            ? (currentIndex - 1 + tabFields.length) % tabFields.length
            : (currentIndex + 1) % tabFields.length;
          tabFields[nextIndex].focus();
        }
      }
    });

    lineOverlay.querySelector("[data-article-lookup-open]")?.addEventListener("click", openArticleLookup);

    articleLookup?.querySelector("[data-article-cancel]")?.addEventListener("click", closeArticleLookup);
    articleLookup?.querySelector("[data-article-ok]")?.addEventListener("click", selectArticleLookupRow);
    articleLookupFields.search?.addEventListener("input", filterArticleLookupRows);
    articleLookupFields.category?.addEventListener("change", filterArticleLookupRows);
    articleLookupFields.supplier?.addEventListener("change", filterArticleLookupRows);
    articleLookup?.querySelector("thead")?.addEventListener("click", (event) => {
      const button = event.target.closest("[data-article-sort]");
      if (!button) {
        return;
      }

      const key = button.dataset.articleSort;
      articleLookupSortDirection = articleLookupSortKey === key && articleLookupSortDirection === "asc" ? "desc" : "asc";
      articleLookupSortKey = key;
      filterArticleLookupRows();
    });
    articleLookupFields.body?.addEventListener("click", (event) => {
      const row = event.target.closest("tr[data-index]");
      if (!row) {
        return;
      }

      setArticleLookupSelectedIndex(Number(row.dataset.index));
      articleLookupFields.frame?.focus({ preventScroll: true });
    });
    articleLookupFields.body?.addEventListener("dblclick", selectArticleLookupRow);
    articleLookupFields.frame?.addEventListener("keydown", (event) => {
      if (event.key === "ArrowDown") {
        event.preventDefault();
        setArticleLookupSelectedIndex(Math.min(articleLookupFilteredRows.length - 1, articleLookupSelectedIndex + 1), 1);
      } else if (event.key === "ArrowUp") {
        event.preventDefault();
        setArticleLookupSelectedIndex(Math.max(0, articleLookupSelectedIndex - 1), -1);
      } else if (event.key === "Enter") {
        event.preventDefault();
        selectArticleLookupRow();
      } else if (event.key === "Escape") {
        event.preventDefault();
        closeArticleLookup();
      }
    });
    articleLookupFields.search?.addEventListener("keydown", (event) => {
      if (event.key === "ArrowDown" || event.key === "ArrowUp") {
        event.preventDefault();
        articleLookupFields.frame?.focus({ preventScroll: true });
      } else if (event.key === "Enter") {
        event.preventDefault();
        selectArticleLookupRow();
      } else if (event.key === "Escape") {
        event.preventDefault();
        closeArticleLookup();
      }
    });

    let lastArticleLookupScrollTop = 0;
    let articleLookupScrollFrame = 0;
    articleLookupFields.frame?.addEventListener("scroll", () => {
      if (articleLookupScrollFrame) {
        window.cancelAnimationFrame(articleLookupScrollFrame);
      }

      articleLookupScrollFrame = window.requestAnimationFrame(() => {
        articleLookupScrollFrame = 0;
        const frame = articleLookupFields.frame;
        const currentScrollTop = frame.scrollTop;
        const delta = currentScrollTop - lastArticleLookupScrollTop;
        lastArticleLookupScrollTop = currentScrollTop;
        const selected = frame.querySelector("tbody tr.selected");
        if (!selected || delta === 0 || isArticleLookupRowVisible(selected)) {
          return;
        }

        const visibleRows = Array.from(frame.querySelectorAll("tbody tr[data-index]"))
          .filter(isArticleLookupRowVisible);
        if (visibleRows.length === 0) {
          return;
        }

        const row = delta > 0 ? visibleRows[0] : visibleRows[visibleRows.length - 1];
        setArticleLookupSelectedIndex(Number(row.dataset.index), delta > 0 ? 1 : -1);
      });
    }, { passive: true });
  }
});
  if (stockLoadReadonly) {
    formPage?.querySelectorAll("button").forEach((button) => {
      if (button.matches("[data-stock-load-modal-cancel='true']")) {
        return;
      }

      button.disabled = true;
      button.tabIndex = -1;
    });
  }



