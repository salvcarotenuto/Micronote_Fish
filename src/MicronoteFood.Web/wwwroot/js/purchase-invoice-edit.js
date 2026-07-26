document.addEventListener("DOMContentLoaded", () => {
  const saveButton = document.querySelector("[data-purchase-invoice-save]");
  const page = document.querySelector(".purchase-invoice-form-page");
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
  const electronicInvoiceCurrentSource = () => {
    const actionValue = Number.parseInt(page?.dataset.azione || document.querySelector("input[name='Azione']")?.value || "0", 10) || 0;
    const baseAction = actionValue % 10;
    return baseAction === 2 ? "" : "archive";
  };
  const dueDateFields = Array.from(document.querySelectorAll(".purchase-invoice-due-date"));
  const invoiceType = document.querySelector("[data-purchase-invoice-type]");
  const invoiceNumber = document.querySelector("[data-purchase-invoice-number]");
  const invoiceDate = document.querySelector("[data-purchase-invoice-date]");
  const invoiceSupplierCode = document.querySelector("#purchaseInvoiceSupplierCode");
  const invoiceSupplierCodeDisplay = document.querySelector("#purchaseInvoiceSupplierCodeDisplay");
  const invoiceSupplierName = document.querySelector("#purchaseInvoiceSupplierName");
  const invoiceContraAccount = document.querySelector("[data-purchase-invoice-contra]");
  const invoiceStore = document.querySelector("[data-purchase-invoice-store]");
  const invoicePayment = document.querySelector("[data-purchase-invoice-payment]");
  const invoiceBank = document.querySelector("[data-purchase-invoice-bank]");
  const addPaymentButton = document.querySelector("[data-purchase-invoice-add-payment]");
  const addBankButton = document.querySelector("[data-purchase-invoice-add-bank]");
  const calculateDueDatesButton = document.querySelector("[data-purchase-invoice-calculate-due-dates]");
  const clearDueDatesButton = document.querySelector("[data-purchase-invoice-clear-due-dates]");
  const paymentCodeModal = document.querySelector("[data-payment-code-modal]");
  const paymentCodeModalFrame = document.querySelector("[data-payment-code-modal-frame]");
  const chartAccountModal = document.querySelector("[data-chart-account-modal]");
  const chartAccountModalFrame = document.querySelector("[data-chart-account-modal-frame]");
  const stockLoadModal = document.querySelector("[data-stock-load-modal]");
  const stockLoadModalFrame = document.querySelector("[data-stock-load-modal-frame]");
  const invoiceNotes = document.querySelector("[data-purchase-invoice-notes]");
  const invoiceTotal = document.querySelector("[data-purchase-invoice-total]");
  const invoiceVatTable = document.querySelector("[data-purchase-invoice-vat-table]");
  const invoiceVatTaxableTotal = document.querySelector("[data-purchase-invoice-vat-taxable-total]");
  const invoiceVatTaxTotal = document.querySelector("[data-purchase-invoice-vat-tax-total]");
  const invoiceInserted = document.querySelector("[data-purchase-invoice-inserted]");
  const invoiceDifference = document.querySelector("[data-purchase-invoice-difference]");
  const invoiceDueTable = document.querySelector("[data-purchase-invoice-due-table]");
  const form = document.querySelector(".purchase-invoice-form-page form[data-enter-navigation]");
  let selectedElectronicInvoiceFile = null;
  let selectedElectronicInvoiceBrowserFile = null;
  let electronicInvoicePreviewAbort = null;
  let electronicInvoiceLastScrollTop = 0;
  let electronicInvoiceWheelFrame = 0;
  let electronicInvoiceFolderPickerPending = false;
  let electronicInvoiceFolderAbortController = null;
  let electronicInvoiceSuppressEscapeUntil = 0;
  let duplicateInvoiceConfirmedKey = "";
  let duplicateInvoicePromptedKey = "";
  let duplicateInvoicePromptOpen = false;
  let originalInvoiceIdentityKey = "";
  let applyingInitialInvoiceData = false;
  let pendingInvoiceRedirectUrl = "";

  const showMessage = (message, title = "Fattura elettronica", variant = "") => {
    window.MicronoteMessageBox?.show({
      title,
      message,
      variant
    });
  };

  const purchaseInvoiceIdentityKey = () => {
    const documentNumber = (invoiceNumber?.value ?? "").trim();
    const documentDate = toIsoDate(invoiceDate?.value);
    const supplierCode = (invoiceSupplierCode?.value ?? "").trim();
    return documentNumber && documentDate && supplierCode
      ? `${documentNumber.toUpperCase()}|${documentDate}|${supplierCode}`
      : "";
  };

  const clearDuplicateInvoiceConfirmation = () => {
    const key = purchaseInvoiceIdentityKey();
    if (key !== duplicateInvoiceConfirmedKey) {
      duplicateInvoiceConfirmedKey = "";
    }

    if (key !== duplicateInvoicePromptedKey) {
      duplicateInvoicePromptedKey = "";
    }
  };

  const checkDuplicateInvoice = async ({ force = false } = {}) => {
    if (applyingInitialInvoiceData) {
      return true;
    }

    clearDuplicateInvoiceConfirmation();
    const key = purchaseInvoiceIdentityKey();
    if (!key
        || key === originalInvoiceIdentityKey
        || duplicateInvoiceConfirmedKey === key
        || duplicateInvoicePromptOpen) {
      return true;
    }

    if (!force && duplicateInvoicePromptedKey === key) {
      return false;
    }

    const url = new URL(window.location.href);
    url.searchParams.set("handler", "DuplicateInvoice");
    url.searchParams.set("documentNumber", invoiceNumber.value.trim());
    url.searchParams.set("documentDate", toIsoDate(invoiceDate.value));
    url.searchParams.set("supplierCode", invoiceSupplierCode.value.trim());
    if (page?.dataset.invoiceId) {
      url.searchParams.set("currentId", page.dataset.invoiceId);
    }

    try {
      const response = await fetch(url, {
        headers: {
          "Accept": "application/json"
        }
      });
      if (!response.ok) {
        return true;
      }

      const result = await response.json();
      if (!result.exists) {
        return true;
      }

      duplicateInvoicePromptedKey = key;
      duplicateInvoicePromptOpen = true;
      window.MicronoteMessageBox?.show({
        title: "Fattura gia' presente",
        message: `La fattura e' gia' presente in archivio\nPartita: ${String(result.code ?? 0).padStart(6, "0")} / ${result.year ?? ""}\nVuoi registrare in sovrascrittura?`,
        mode: "confirm",
        variant: "confirm",
        confirmText: "Sovrascrivi",
        cancelText: "Annulla",
        onConfirm: () => {
          duplicateInvoiceConfirmedKey = key;
          duplicateInvoicePromptOpen = false;
        },
        onCancel: () => {
          duplicateInvoicePromptOpen = false;
          invoiceNumber?.focus();
          invoiceNumber?.select?.();
        }
      });
      window.setTimeout(() => {
        duplicateInvoicePromptOpen = false;
      }, 0);
      return false;
    } catch {
      return true;
    }
  };

  const navigableSelector = [
    "input:not([type='hidden']):not([type='submit']):not([type='button'])",
    "select",
    "textarea"
  ].join(",");

  const isNavigableControl = (element) =>
    element
    && !element.disabled
    && !element.readOnly
    && element.tabIndex >= 0
    && element.offsetParent !== null
    && element.matches(navigableSelector);

  const focusNextControl = (currentControl) => {
    const controls = Array.from(form?.querySelectorAll(navigableSelector) ?? [])
      .filter(isNavigableControl);
    const currentIndex = controls.indexOf(currentControl);
    if (currentIndex < 0 || controls.length === 0) {
      return;
    }

    const nextControl = controls[currentIndex + 1] ?? controls[0];
    nextControl.focus();
    if (typeof nextControl.select === "function"
        && nextControl.tagName.toLowerCase() !== "select"
        && nextControl.type !== "checkbox") {
      nextControl.select();
    }
  };

  const showMissingSupplierMessage = (result) => {
    const supplier = result?.supplier ?? {};
    const name = (supplier.name || "").trim() || "(nome non disponibile)";
    const vat = (supplier.vat || "").trim() || "(partita IVA non disponibile)";
    const fiscalCode = (supplier.fiscalCode || "").trim();
    const supplierParams = {
      address: supplier.address,
      city: supplier.city,
      postalCode: supplier.postalCode,
      province: supplier.province,
      phone: supplier.phone,
      email: supplier.email,
      certifiedEmail: supplier.certifiedEmail
    };

    window.MicronoteMessageBox?.show({
      title: "Fornitore non trovato",
      message: `Il fornitore\n${name}\npartita iva: ${vat}\nnon e' presente in archivio\nvuoi inserirlo ora ?`,
      mode: "confirm",
      variant: "confirm",
      confirmText: "Inserisci",
      cancelText: "Annulla",
      onConfirm: () => {
        const supplierUrl = new URL("/Fornitori/Edit", window.location.origin);
        const returnUrl = new URL(window.location.href);
        if (selectedElectronicInvoiceFile?.fullPath) {
          returnUrl.searchParams.set("importXml", selectedElectronicInvoiceFile.fullPath);
        }
        supplierUrl.searchParams.set("returnUrl", returnUrl.pathname + returnUrl.search);
        supplierUrl.searchParams.set("name", name === "(nome non disponibile)" ? "" : name);
        supplierUrl.searchParams.set("vat", vat === "(partita IVA non disponibile)" ? "" : vat);
        if (fiscalCode) {
          supplierUrl.searchParams.set("fiscalCode", fiscalCode);
        }
        Object.entries(supplierParams).forEach(([key, value]) => {
          const normalized = (value || "").trim();
          if (normalized) {
            supplierUrl.searchParams.set(key, normalized);
          }
        });

        window.location.href = supplierUrl.toString();
      }
    });
  };

  const requestVerificationToken = () =>
    document.querySelector("input[name='__RequestVerificationToken']")?.value ?? "";

  const setFieldValue = (field, value, eventName = "input") => {
    if (!field) {
      return;
    }

    field.value = value ?? "";
    field.dispatchEvent(new Event(eventName, { bubbles: true }));
  };

  const setFieldValueQuiet = (field, value) => {
    if (field) {
      field.value = value ?? "";
    }
  };

  const padDatePart = (value) => String(value).padStart(2, "0");

  const toDisplayDate = (value) => {
    const text = String(value ?? "").trim();
    const iso = toIsoDate(text);
    if (!iso) {
      return "";
    }

    const [year, month, day] = iso.split("-");
    return `${day}/${month}/${year}`;
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

  const setDateFieldValue = (field, value, eventName = "change") => {
    setFieldValue(field, toDisplayDate(value), eventName);
    syncDateEmptyState(field);
  };

  const setPercentFieldValue = (field, value) => {
    if (!field) {
      return;
    }

    const text = String(value ?? "").trim();
    const parsed = window.MicronotePercent?.parse(text) ?? 0;
    field.value = text && parsed !== 0
      ? (window.MicronotePercent?.format(parsed) ?? text)
      : "";
  };

  const setMoneyFieldValue = (field, value, { notify = true } = {}) => {
    if (!field) {
      return;
    }

    if (window.MicronoteMoney?.format && window.MicronoteMoney?.parse) {
      const amount = typeof value === "number" ? value : window.MicronoteMoney.parse(value);
      field.value = window.MicronoteMoney.format(amount);
    } else {
      field.value = String(value ?? "");
    }

    if (notify) {
      field.dispatchEvent(new Event("input", { bubbles: true }));
    }
  };

  const syncDateEmptyState = (field) => {
    if (!field) {
      return;
    }

    const isEmpty = !field.value;
    field.classList.toggle("is-empty", isEmpty);
  };

  let activeDateField = null;
  let activeDateMonth = null;
  let datePickerPanel = null;

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
    if (!field) {
      return;
    }

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

  const normalizeDateOnBlur = (field, { showError = true } = {}) => {
    if (!field) {
      return true;
    }

    const text = field.value.trim();
    if (!text) {
      syncDateEmptyState(field);
      return true;
    }

    const display = toDisplayDate(text);
    if (!display) {
      if (showError) {
        showSaveError("Data non valida.", field);
      }
      return false;
    }

    field.value = display;
    syncDateEmptyState(field);
    return true;
  };

  const parseMoneyField = (field) => window.MicronoteMoney?.parse(field?.value) ?? 0;

  const parsePercentField = (field) => window.MicronotePercent?.parse(field?.value) ?? 0;

  const roundCurrency = (value) => Math.round(value * 100) / 100;

  const getVatRows = () => Array.from(invoiceVatTable?.querySelectorAll("tbody tr") ?? []);

  const vatCellSnapshots = new WeakMap();

  const getVatRowTotal = (row) =>
    parseMoneyField(row?.querySelector("[data-vat-taxable]"))
    + parseMoneyField(row?.querySelector("[data-vat-tax]"));

  const hasVatRowData = (row) => {
    const rateText = String(row?.querySelector("[data-vat-rate]")?.value ?? "").trim();
    const taxableText = String(row?.querySelector("[data-vat-taxable]")?.value ?? "").trim();
    const taxText = String(row?.querySelector("[data-vat-tax]")?.value ?? "").trim();
    return Boolean(rateText || taxableText || taxText);
  };

  const hasVatRowsDataFrom = (rows, startIndex) =>
    rows.slice(startIndex).some((row) => hasVatRowData(row));

  const clearZeroVatRateDisplay = (field) => {
    if (field?.matches?.("[data-vat-rate]") && parsePercentField(field) === 0) {
      field.value = "";
    }
  };

  const updateVatTotals = () => {
    const rows = getVatRows();
    const taxableTotal = rows.reduce((total, row) =>
      total + parseMoneyField(row.querySelector("[data-vat-taxable]")), 0);
    const taxTotal = rows.reduce((total, row) =>
      total + parseMoneyField(row.querySelector("[data-vat-tax]")), 0);
    const inserted = roundCurrency(taxableTotal + taxTotal);
    const invoiceAmount = parseMoneyField(invoiceTotal);

    setMoneyFieldValue(invoiceVatTaxableTotal, roundCurrency(taxableTotal));
    setMoneyFieldValue(invoiceVatTaxTotal, roundCurrency(taxTotal));
    setMoneyFieldValue(invoiceInserted, inserted);
    setMoneyFieldValue(invoiceDifference, roundCurrency(invoiceAmount - inserted));
  };

  const setStoredVatTotals = (taxableTotal, vatTotal, invoiceAmount) => {
    const parseAmount = (value) => typeof value === "number"
      ? value
      : (window.MicronoteMoney?.parse(value) ?? 0);
    const taxable = roundCurrency(parseAmount(taxableTotal));
    const vat = roundCurrency(parseAmount(vatTotal));
    const total = roundCurrency(parseAmount(invoiceAmount));
    const inserted = roundCurrency(taxable + vat);
    setMoneyFieldValue(invoiceVatTaxableTotal, taxable, { notify: false });
    setMoneyFieldValue(invoiceVatTaxTotal, vat, { notify: false });
    setMoneyFieldValue(invoiceInserted, inserted, { notify: false });
    setMoneyFieldValue(invoiceDifference, roundCurrency(total - inserted), { notify: false });
  };

  const initialAmountValue = (value) => typeof value === "number"
    ? value
    : (window.MicronoteMoney?.parse(value) ?? 0);

  const vatRowsCheckTotals = (rows) => {
    const values = (rows ?? []).reduce((totals, row) => {
      totals.taxable += initialAmountValue(row.taxable);
      totals.tax += initialAmountValue(row.tax);
      return totals;
    }, { taxable: 0, tax: 0 });

    values.taxable = roundCurrency(values.taxable);
    values.tax = roundCurrency(values.tax);
    values.total = roundCurrency(values.taxable + values.tax);
    return values;
  };

  const hasAmountMismatch = (left, right) =>
    Math.abs(roundCurrency(initialAmountValue(left) - initialAmountValue(right))) >= 0.01;

  const checkStoredVatTotals = (data) => {
    if (!data?.id) {
      return;
    }

    const detailTotals = vatRowsCheckTotals(data.vatRows);
    const headerTaxable = roundCurrency(initialAmountValue(data.taxableTotal));
    const headerVat = roundCurrency(initialAmountValue(data.vatTotal));
    const headerTotal = roundCurrency(initialAmountValue(data.total));

    if (!hasAmountMismatch(headerTaxable, detailTotals.taxable)
        && !hasAmountMismatch(headerVat, detailTotals.tax)
        && !hasAmountMismatch(headerTotal, detailTotals.total)) {
      return;
    }

    const formatAmount = (value) =>
      window.MicronoteMoney?.format
        ? window.MicronoteMoney.format(value)
        : String(value);

    showMessage(
      `Il dettaglio IVA non quadra con i totali registrati.\n`
        + `Moviva: imponibile ${formatAmount(headerTaxable)}, iva ${formatAmount(headerVat)}, totale ${formatAmount(headerTotal)}\n`
        + `MovivaRg: imponibile ${formatAmount(detailTotals.taxable)}, iva ${formatAmount(detailTotals.tax)}, totale ${formatAmount(detailTotals.total)}`,
      "Fattura di acquisto",
      "error"
    );
  };

  const calculateVatRowFromGross = (row, grossAmount) => {
    const rateField = row?.querySelector("[data-vat-rate]");
    const rateText = String(rateField?.value ?? "").trim();
    const rate = parsePercentField(rateField);
    if (!row || grossAmount <= 0 || !rateText || rate < 0) {
      return false;
    }

    const taxable = roundCurrency(grossAmount / (1 + (rate / 100)));
    const tax = roundCurrency(grossAmount - taxable);
    setMoneyFieldValue(row.querySelector("[data-vat-taxable]"), taxable);
    setMoneyFieldValue(row.querySelector("[data-vat-tax]"), tax);
    return true;
  };

  const calculateVatRowFromTaxable = (row) => {
    const rateField = row?.querySelector("[data-vat-rate]");
    const taxableField = row?.querySelector("[data-vat-taxable]");
    const rateText = String(rateField?.value ?? "").trim();
    const taxableText = String(taxableField?.value ?? "").trim();
    const rate = parsePercentField(rateField);
    const taxable = parseMoneyField(taxableField);
    if (!row || !rateText || !taxableText || rate < 0) {
      return false;
    }

    setMoneyFieldValue(row.querySelector("[data-vat-tax]"), roundCurrency(taxable * rate / 100));
    return true;
  };

  const calculateVatRowsFrom = (startIndex, { stopBeforeFilled = true } = {}) => {
    const rows = getVatRows();
    const invoiceAmount = parseMoneyField(invoiceTotal);
    if (invoiceAmount <= 0) {
      return;
    }

    for (let index = startIndex; index < rows.length; index += 1) {
      if (stopBeforeFilled && hasVatRowData(rows[index])) {
        break;
      }

      const previousTotal = rows
        .slice(0, index)
        .reduce((total, row) => total + getVatRowTotal(row), 0);
      const residual = roundCurrency(invoiceAmount - previousTotal);
      if (Math.abs(residual) < 0.01) {
        break;
      }

      if (!calculateVatRowFromGross(rows[index], residual)) {
        break;
      }
    }

    updateVatTotals();
  };

  const isVatBalanced = () => Math.abs(parseMoneyField(invoiceDifference)) < 0.01;

  const setSelectValue = (select, value) => {
    if (!select) {
      return;
    }

    const normalized = value == null ? "" : String(value);
    select.value = Array.from(select.options).some((option) => option.value === normalized)
      ? normalized
      : "";
    select.dispatchEvent(new Event("change", { bubbles: true }));
  };

  const trySetInvoiceType = (documentType) => {
    if (!invoiceType || !documentType) {
      return;
    }

    const normalized = String(documentType).trim().toUpperCase();
    const internalCauseByFeType = {
      TD01: "10",
      TD05: "11",
      TD04: "12"
    };
    const internalCause = internalCauseByFeType[normalized] ?? "";
    if (internalCause && Array.from(invoiceType.options).some((option) => option.value === internalCause)) {
      invoiceType.value = internalCause;
      invoiceType.dispatchEvent(new Event("change", { bubbles: true }));
    }
  };

  const clearVatRows = () => {
    invoiceVatTable?.querySelectorAll("tbody tr").forEach((row) => {
      setFieldValueQuiet(row.querySelector("[data-vat-rate]"), "");
      setFieldValueQuiet(row.querySelector("[data-vat-taxable]"), "");
      setFieldValueQuiet(row.querySelector("[data-vat-tax]"), "");
    });
  };

  const fillVatRows = (rows) => {
    clearVatRows();
    const tableRows = Array.from(invoiceVatTable?.querySelectorAll("tbody tr") ?? []);
    (rows ?? []).forEach((row, index) => {
      const target = tableRows[index];
      if (!target) {
        return;
      }

      setPercentFieldValue(target.querySelector("[data-vat-rate]"), row.rate ?? "");
      setMoneyFieldValue(target.querySelector("[data-vat-taxable]"), row.taxable ?? "", { notify: false });
      setMoneyFieldValue(target.querySelector("[data-vat-tax]"), row.tax ?? "", { notify: false });
    });
  };

  const clearDueRows = () => {
    invoiceDueTable?.querySelectorAll("tbody tr").forEach((row) => {
      setFieldValue(row.querySelector("[data-due-number]"), "");
      setFieldValue(row.querySelector("[data-due-amount]"), "");
      const dueDate = row.querySelector("[data-due-date]");
      setDateFieldValue(dueDate, "");
      const paid = row.querySelector("input[type='checkbox']");
      if (paid) {
        paid.checked = false;
        paid.dispatchEvent(new Event("change", { bubbles: true }));
      }
    });
  };

  const clearZeroAmountDueDates = () => {
    invoiceDueTable?.querySelectorAll("tbody tr").forEach((row) => {
      if (parseMoneyField(row.querySelector("[data-due-amount]")) !== 0) {
        return;
      }

      setDateFieldValue(row.querySelector("[data-due-date]"), "");
    });
  };

  const fillDueRows = (rows) => {
    clearDueRows();
    const tableRows = Array.from(invoiceDueTable?.querySelectorAll("tbody tr") ?? []);
    (rows ?? []).forEach((row, index) => {
      const target = tableRows[index];
      if (!target) {
        return;
      }

      setFieldValue(target.querySelector("[data-due-number]"), row.number ?? "");
      setFieldValue(target.querySelector("[data-due-amount]"), row.amount ?? "");
      const dueDate = target.querySelector("[data-due-date]");
      setDateFieldValue(dueDate, row.date ?? "");
      const paid = target.querySelector("input[type='checkbox']");
      if (paid) {
        paid.checked = Boolean(row.paid);
        paid.dispatchEvent(new Event("change", { bubbles: true }));
      }
    });
    clearZeroAmountDueDates();
  };

  const calculateDueDates = async () => {
    const paymentCode = Number.parseInt(invoicePayment?.value || "0", 10) || 0;
    const total = roundCurrency(parseMoneyField(invoiceTotal));
    const documentDate = toIsoDate(invoiceDate?.value);

    if (paymentCode <= 0) {
      showSaveError("Campo Pagamento obbligatorio.", invoicePayment);
      return;
    }

    if (!documentDate) {
      showSaveError("Campo Data documento obbligatorio.", invoiceDate);
      return;
    }

    if (total <= 0) {
      showSaveError("Campo Totale fattura obbligatorio.", invoiceTotal);
      return;
    }

    const url = new URL(window.location.href);
    url.searchParams.set("handler", "CalculateDueDates");
    url.searchParams.set("paymentCode", String(paymentCode));
    url.searchParams.set("total", String(total));
    url.searchParams.set("documentDate", documentDate);

    try {
      const response = await fetch(url, {
        headers: { Accept: "application/json" }
      });
      const result = await response.json();

      if (!response.ok || !result?.success) {
        showSaveError(result?.message || "Calcolo scadenze non riuscito.", invoicePayment);
        return;
      }

      fillDueRows(result.dueRows ?? []);
    } catch {
      showSaveError("Calcolo scadenze non riuscito.", invoicePayment);
    }
  };

  const applyElectronicInvoiceImport = (result) => {
    setFieldValue(electronicInvoiceName, result.fileName ?? selectedElectronicInvoiceFile?.name ?? "");
    setFieldValue(electronicInvoiceFullPath, result.fullPath ?? selectedElectronicInvoiceFile?.fullPath ?? "");
    setFieldValue(invoiceNumber, result.documentNumber ?? "");
    setDateFieldValue(invoiceDate, result.documentDate ?? "");
    setFieldValue(invoiceTotal, result.total ?? "");
    setFieldValue(invoiceInserted, result.inserted ?? result.total ?? "");
    setFieldValue(invoiceDifference, result.difference ?? "");
    setFieldValue(invoiceVatTaxableTotal, result.vatTotals?.taxable ?? "");
    setFieldValue(invoiceVatTaxTotal, result.vatTotals?.tax ?? "");
    trySetInvoiceType(result.documentType);

    const supplier = result.supplier ?? {};
    setFieldValue(invoiceSupplierCode, supplier.code ? String(supplier.code) : "");
    setFieldValue(invoiceSupplierCodeDisplay, supplier.codeDisplay ?? "");
    setFieldValue(invoiceSupplierName, supplier.name ?? "");
    setSelectValue(invoiceContraAccount, supplier.accountCode);
    setSelectValue(invoiceStore, supplier.storeCode ?? 0);
    setSelectValue(invoicePayment, result.paymentCode);

    fillVatRows(result.vatRows ?? []);
    fillDueRows(result.dueRows ?? []);
    void checkDuplicateInvoice();

    if (electronicInvoiceFullPath && selectedElectronicInvoiceBrowserFile) {
      electronicInvoiceFullPath.dataset.browserFileName = selectedElectronicInvoiceBrowserFile.name;
    }

    closeElectronicInvoiceDialog();
  };

  const readInitialInvoiceData = () => {
    const script = document.querySelector("[data-purchase-invoice-initial]");
    if (!script?.textContent?.trim()) {
      return null;
    }

    try {
      return JSON.parse(script.textContent);
    } catch {
      return null;
    }
  };

  const applyInitialInvoiceData = (data) => {
    if (!data?.id) {
      return;
    }

    applyingInitialInvoiceData = true;
    try {
      setSelectValue(invoiceType, data.causeCode);
      setFieldValue(invoiceNumber, data.documentNumber ?? "");
      setDateFieldValue(invoiceDate, data.documentDate ?? "");
      setFieldValue(invoiceSupplierCode, data.supplier?.code ? String(data.supplier.code) : "");
      setFieldValue(invoiceSupplierCodeDisplay, data.supplier?.codeDisplay ?? "");
      setFieldValue(invoiceSupplierName, data.supplier?.name ?? "");
      setSelectValue(invoiceContraAccount, data.contraAccountCode);
      setSelectValue(invoiceStore, data.storeCode ?? 0);
      setSelectValue(invoicePayment, data.paymentCode);
      setSelectValue(invoiceBank, data.bankCode);
      setMoneyFieldValue(invoiceTotal, data.total ?? 0, { notify: false });
      setFieldValue(invoiceNotes, data.notes ?? "");
      setFieldValue(electronicInvoiceName, data.electronicInvoiceFileName ?? "");
      fillVatRows(data.vatRows ?? []);
      fillDueRows(data.dueRows ?? []);
      setStoredVatTotals(data.taxableTotal, data.vatTotal, data.total);
      checkStoredVatTotals(data);
      originalInvoiceIdentityKey = purchaseInvoiceIdentityKey();
      duplicateInvoiceConfirmedKey = originalInvoiceIdentityKey;
      duplicateInvoicePromptedKey = "";
    } finally {
      applyingInitialInvoiceData = false;
    }

    invoiceNumber?.focus();
    invoiceNumber?.select?.();
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
      const valueElement = document.createElement("strong");

      labelElement.textContent = label;
      valueElement.textContent = value || "";
      line.append(labelElement, document.createTextNode(" : "), valueElement);
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
        headers: {
          Accept: "application/json"
        },
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

  const renderDirectoryHandleFiles = async (directoryHandle) => {
    const files = [];

    for await (const entry of directoryHandle.values()) {
      if (entry.kind !== "file" || !/\.(xml|p7m)$/i.test(entry.name)) {
        continue;
      }

      const file = await entry.getFile();
      files.push({
        name: file.name,
        type: file.name.split(".").pop().toUpperCase(),
        lastModified: formatBrowserFileDate(file.lastModified),
        size: formatBrowserFileSize(file.size),
        fullPath: `${directoryHandle.name}\\${file.name}`,
        browserFile: file
      });
    }

    files.sort((left, right) => left.name.localeCompare(right.name, "it-IT", { sensitivity: "base" }));

    if (electronicInvoiceCount) {
      electronicInvoiceCount.value = `${files.length} file`;
    }

    if (electronicInvoicePath) {
      setElectronicInvoicePathValue(directoryHandle.name, "browser");
    }

    renderElectronicInvoiceFiles(files);
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
        headers: {
          Accept: "application/json"
        }
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
      if (error?.name === "AbortError") {
        return;
      }

      showMessage("Non e' stato possibile aprire la selezione cartella.");
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
        headers: {
          Accept: "application/json"
        }
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
        headers: {
          Accept: "application/json"
        }
      });

      if (!response.ok) {
        throw new Error("HTTP " + response.status);
      }

      const result = await response.json();
      if (!result.success) {
        if (result.reason === "supplierMissing") {
          showMissingSupplierMessage(result);
          return;
        }

        showMessage(result.message || "Non e' stato possibile leggere la fattura elettronica.", "Fattura elettronica", "error");
        return;
      }

      applyElectronicInvoiceImport(result);
    } catch {
      showMessage("Non e' stato possibile leggere la fattura elettronica.", "Fattura elettronica", "error");
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
    const source = electronicInvoiceCurrentSource();
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
      confirmText: "Elimina",
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
            headers: {
              Accept: "application/json"
            }
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

  const openPaymentCodeModal = () => {
    if (!paymentCodeModal || !paymentCodeModalFrame) {
      return;
    }

    paymentCodeModalFrame.src = "/CodiciPagamento/Edit?azione=102";
    paymentCodeModal.hidden = false;
    document.body.classList.add("purchase-invoice-payment-modal-open");
    paymentCodeModalFrame.focus();
  };

  const closePaymentCodeModal = () => {
    if (!paymentCodeModal) {
      return;
    }

    paymentCodeModal.hidden = true;
    document.body.classList.remove("purchase-invoice-payment-modal-open");
    paymentCodeModalFrame?.removeAttribute("src");
    addPaymentButton?.focus();
  };

  const reloadPaymentOptions = async () => {
    if (!invoicePayment) {
      return;
    }

    const currentValue = invoicePayment.value;
    const url = new URL(window.location.href);
    url.searchParams.set("handler", "PaymentOptions");

    try {
      const response = await fetch(url, {
        headers: {
          "Accept": "application/json"
        }
      });
      if (!response.ok) {
        throw new Error("HTTP " + response.status);
      }

      const result = await response.json();
      invoicePayment.innerHTML = '<option value=""></option>';
      (result.payments ?? []).forEach((payment) => {
        const option = document.createElement("option");
        option.value = String(payment.code ?? "");
        option.textContent = payment.description ?? "";
        invoicePayment.append(option);
      });

      if (currentValue && Array.from(invoicePayment.options).some((option) => option.value === currentValue)) {
        invoicePayment.value = currentValue;
      }
    } catch {
      showMessage("Non e' stato possibile aggiornare l'elenco pagamenti.", "Fattura di acquisto", "error");
    }
  };

  const openChartAccountModal = () => {
    if (!chartAccountModal || !chartAccountModalFrame) {
      return;
    }

    chartAccountModalFrame.src = "/PianoConti/Edit?azione=102&companyType=B&type=P";
    chartAccountModal.hidden = false;
    document.body.classList.add("purchase-invoice-payment-modal-open");
    chartAccountModalFrame.focus();
  };

  const closeChartAccountModal = () => {
    if (!chartAccountModal) {
      return;
    }

    chartAccountModal.hidden = true;
    document.body.classList.remove("purchase-invoice-payment-modal-open");
    chartAccountModalFrame?.removeAttribute("src");
    addBankButton?.focus();
  };

  const closeStockLoadModal = () => {
    if (!stockLoadModal) {
      return;
    }

    stockLoadModal.hidden = true;
    document.body.classList.remove("purchase-invoice-payment-modal-open");
    stockLoadModalFrame?.removeAttribute("src");
    saveButton?.focus();
  };

  const archiveElectronicInvoiceFile = async () => {
    const xmlPath = (electronicInvoiceFullPath?.value ?? "").trim();
    if (!xmlPath) {
      return true;
    }

    const url = new URL(window.location.href);
    url.searchParams.set("handler", "ArchiveElectronicInvoice");

    const formData = new FormData();
    formData.append("path", xmlPath);

    const response = await fetch(url, {
      method: "POST",
      body: formData,
      headers: {
        "Accept": "application/json",
        "RequestVerificationToken": requestVerificationToken()
      }
    });
    const result = await response.json();
    if (!response.ok || !result?.success) {
      showMessage(result?.message || "Archiviazione file XML non riuscita.", "Fattura di acquisto", "error");
      saveButton?.focus();
      return false;
    }

    if (result.path && electronicInvoiceFullPath) {
      electronicInvoiceFullPath.value = result.path;
    }

    if (result.path && electronicInvoiceName) {
      electronicInvoiceName.value = String(result.path).split(/[\\/]/).pop() || electronicInvoiceName.value;
    }

    return true;
  };

  const continueAfterStockLoadBridge = async (archiveXml = false) => {
    if (archiveXml && !(await archiveElectronicInvoiceFile())) {
      return;
    }

    const redirectUrl = pendingInvoiceRedirectUrl || "/FattureAcquisto";
    pendingInvoiceRedirectUrl = "";
    window.location.href = redirectUrl;
  };

  const requiresStockLoadBridge = async () => {
    const contraAccountCode = Number.parseInt(invoiceContraAccount?.value || "0", 10) || 0;
    if (contraAccountCode <= 0) {
      return false;
    }

    const url = new URL(window.location.href);
    url.searchParams.set("handler", "StockLoadBridge");
    url.searchParams.set("contraAccountCode", String(contraAccountCode));

    const response = await fetch(url, {
      headers: { Accept: "application/json" }
    });
    if (!response.ok) {
      throw new Error("stock load bridge check failed");
    }

    const result = await response.json();
    return result?.requiresStockLoad === true;
  };

  const openStockLoadBridge = () => {
    const xmlPath = (electronicInvoiceFullPath?.value ?? "").trim();
    if (!xmlPath) {
      return false;
    }

    if (!stockLoadModal || !stockLoadModalFrame) {
      showMessage("Scheda Carico per acquisti non disponibile.", "Fattura di acquisto", "error");
      return false;
    }

    const url = new URL("/CaricoAcquisti/Edit", window.location.origin);
    url.searchParams.set("returnTo", "purchaseInvoice");
    url.searchParams.set("returnUrl", `${window.location.pathname}${window.location.search}`);
    url.searchParams.set("importXml", xmlPath);

    stockLoadModalFrame.src = `${url.pathname}${url.search}`;
    stockLoadModal.hidden = false;
    document.body.classList.add("purchase-invoice-payment-modal-open");
    stockLoadModalFrame.focus();
    return true;
  };

  const warnAndContinueAfterSave = (message) => {
    if (!window.MicronoteMessageBox?.show) {
      void continueAfterStockLoadBridge(false);
      return;
    }

    window.MicronoteMessageBox.show({
      title: "Fattura di acquisto",
      message,
      variant: "error",
      okText: "OK",
      onConfirm: () => {
        void continueAfterStockLoadBridge(false);
      }
    });
  };

  const reloadBankOptions = async () => {
    if (!invoiceBank) {
      return;
    }

    const currentValue = invoiceBank.value;
    const url = new URL(window.location.href);
    url.searchParams.set("handler", "BankOptions");

    try {
      const response = await fetch(url, {
        headers: {
          "Accept": "application/json"
        }
      });
      if (!response.ok) {
        throw new Error("HTTP " + response.status);
      }

      const result = await response.json();
      invoiceBank.innerHTML = '<option value=""></option>';
      (result.banks ?? []).forEach((bank) => {
        const option = document.createElement("option");
        option.value = String(bank.code ?? "");
        option.textContent = bank.description ?? "";
        invoiceBank.append(option);
      });

      if (currentValue && Array.from(invoiceBank.options).some((option) => option.value === currentValue)) {
        invoiceBank.value = currentValue;
      }
    } catch {
      showMessage("Non e' stato possibile aggiornare l'elenco banche.", "Fattura di acquisto", "error");
    }
  };

  const firstFilledVatRow = () =>
    getVatRows().find((row) =>
      parseMoneyField(row.querySelector("[data-vat-taxable]")) !== 0
      || parseMoneyField(row.querySelector("[data-vat-tax]")) !== 0);

  const vatRowsPayload = () =>
    getVatRows()
      .map((row) => ({
        rate: parsePercentField(row.querySelector("[data-vat-rate]")),
        taxable: roundCurrency(parseMoneyField(row.querySelector("[data-vat-taxable]"))),
        tax: roundCurrency(parseMoneyField(row.querySelector("[data-vat-tax]")))
      }))
      .filter((row) => row.taxable !== 0 || row.tax !== 0);

  const dueRowsPayload = () =>
    Array.from(invoiceDueTable?.querySelectorAll("tbody tr") ?? [])
      .map((row, index) => ({
        number: Number.parseInt(row.querySelector("[data-due-number]")?.value || `${index + 1}`, 10) || (index + 1),
        amount: roundCurrency(parseMoneyField(row.querySelector("[data-due-amount]"))),
        date: toIsoDate(row.querySelector("[data-due-date]")?.value) || null,
        paid: row.querySelector("input[type='checkbox']")?.checked ?? false
      }))
      .filter((row) => row.amount !== 0 || row.date);

  const buildSavePayload = (confirmOverwrite = false, confirmDueDateMismatch = false) => ({
    id: Number.parseInt(page?.dataset.invoiceId || "0", 10) || null,
    causeCode: Number.parseInt(invoiceType?.value || "0", 10) || 0,
    documentNumber: (invoiceNumber?.value ?? "").trim(),
    documentDate: toIsoDate(invoiceDate?.value) || null,
    supplierCode: Number.parseInt(invoiceSupplierCode?.value || "0", 10) || 0,
    contraAccountCode: Number.parseInt(invoiceContraAccount?.value || "0", 10) || 0,
    storeCode: Number.parseInt(invoiceStore?.value || "0", 10) || 0,
    paymentCode: Number.parseInt(invoicePayment?.value || "0", 10) || 0,
    bankCode: Number.parseInt(invoiceBank?.value || "0", 10) || 0,
    total: roundCurrency(parseMoneyField(invoiceTotal)),
    notes: (invoiceNotes?.value ?? "").trim(),
    electronicInvoiceFileName: (electronicInvoiceName?.value ?? "").trim(),
    confirmOverwrite,
    confirmDueDateMismatch,
    vatRows: vatRowsPayload(),
    dueRows: dueRowsPayload()
  });

  const showSaveError = (message, field) => {
    showMessage(message, "Fattura di acquisto", "error");
    field?.focus?.();
    field?.select?.();
  };

  const validateBeforeSave = () => {
    updateVatTotals();

    if (!invoiceType?.value) {
      showSaveError("Campo Tipo documento obbligatorio.", invoiceType);
      return false;
    }

    if (!(invoiceNumber?.value ?? "").trim()) {
      showSaveError("Campo Numero documento obbligatorio.", invoiceNumber);
      return false;
    }

    const invoiceDateText = (invoiceDate?.value ?? "").trim();
    if (!invoiceDateText) {
      showSaveError("Campo Data documento obbligatorio.", invoiceDate);
      return false;
    }

    if (!toIsoDate(invoiceDateText)) {
      showSaveError("Data documento non valida.", invoiceDate);
      return false;
    }

    if (!Number.parseInt(invoiceSupplierCode?.value || "0", 10)) {
      showSaveError("Campo Fornitore obbligatorio.", invoiceSupplierCodeDisplay);
      return false;
    }

    if (!invoiceContraAccount?.value) {
      showSaveError("Campo Contropartita obbligatorio.", invoiceContraAccount);
      return false;
    }

    if (parseMoneyField(invoiceTotal) <= 0) {
      showSaveError("Campo Totale fattura obbligatorio.", invoiceTotal);
      return false;
    }

    if (!firstFilledVatRow()) {
      showSaveError("Inserire almeno una riga di dettaglio imponibile/iva.", invoiceVatTable?.querySelector("[data-vat-rate]"));
      return false;
    }

    if (!isVatBalanced()) {
      showSaveError("Quadratura importi errata", invoiceDifference);
      return false;
    }

    const dueRows = dueRowsPayload();
    const firstDueWithoutDate = dueRows.find((row) => row.amount !== 0 && !row.date);
    if (firstDueWithoutDate) {
      const dueRow = Array.from(invoiceDueTable?.querySelectorAll("tbody tr") ?? [])
        .find((row) => parseMoneyField(row.querySelector("[data-due-amount]")) !== 0
          && !toIsoDate(row.querySelector("[data-due-date]")?.value));
      const dueField = dueRow?.querySelector("[data-due-date]");
      const dueText = (dueField?.value ?? "").trim();
      showSaveError(dueText ? "Scadenza non valida." : "Campo Scadenza obbligatorio.", dueField);
      return false;
    }

    if (dueRows.length > 0 && !invoicePayment?.value) {
      showSaveError("Campo Pagamento obbligatorio.", invoicePayment);
      return false;
    }

    return true;
  };

  const hasDueDateMismatch = () => {
    const dueTotal = roundCurrency(dueRowsPayload().reduce((total, row) => total + row.amount, 0));
    return dueTotal !== 0
      && roundCurrency(dueTotal - roundCurrency(parseMoneyField(invoiceTotal))) !== 0;
  };

  const confirmDueDateMismatchIfNeeded = (confirmOverwrite) => {
    if (!hasDueDateMismatch()) {
      return false;
    }

    window.MicronoteMessageBox?.show({
      title: "Fattura di acquisto",
      message: "Il totale delle scadenze non coincide con il totale fattura.\nVuoi registrare ugualmente?",
      mode: "confirm",
      variant: "confirm",
      confirmText: "Registra",
      cancelText: "Annulla",
      onConfirm: () => {
        void saveInvoice(confirmOverwrite, true);
      }
    });
    return true;
  };

  const postInvoiceSave = async (confirmOverwrite = false, confirmDueDateMismatch = false) => {
    const url = new URL(window.location.href);
    url.searchParams.set("handler", "Save");
    const token = requestVerificationToken();
    const response = await fetch(url, {
      method: "POST",
      headers: {
        "Accept": "application/json",
        "Content-Type": "application/json",
        ...(token ? { "RequestVerificationToken": token } : {})
      },
      body: JSON.stringify(buildSavePayload(confirmOverwrite, confirmDueDateMismatch))
    });

    if (!response.ok) {
      throw new Error("save failed");
    }

    return response.json();
  };

  const saveInvoice = async (confirmOverwrite = false, confirmDueDateMismatch = false) => {
    const result = window.MicronoteProgress?.run
      ? await window.MicronoteProgress.run(
        () => postInvoiceSave(confirmOverwrite, confirmDueDateMismatch),
        { message: "Salvataggio in corso..." })
      : await postInvoiceSave(confirmOverwrite, confirmDueDateMismatch);

    if (result?.requiresDueDateMismatchConfirmation) {
      window.MicronoteMessageBox?.show({
        title: "Fattura di acquisto",
        message: result.message || "Il totale delle scadenze non coincide con il totale fattura.\nVuoi registrare ugualmente?",
        mode: "confirm",
        variant: "confirm",
        confirmText: "Registra",
        cancelText: "Annulla",
        onConfirm: () => {
          void saveInvoice(confirmOverwrite, true);
        }
      });
      return;
    }

    if (result?.requiresOverwriteConfirmation) {
      window.MicronoteMessageBox?.show({
        title: "Fattura gia' presente",
        message: `La fattura e' gia' presente in archivio\nPartita: ${String(result.code ?? 0).padStart(6, "0")} / ${result.year ?? ""}\nVuoi registrare in sovrascrittura?`,
        mode: "confirm",
        variant: "confirm",
        confirmText: "Sovrascrivi",
        cancelText: "Annulla",
        onConfirm: () => {
          void saveInvoice(true, confirmDueDateMismatch);
        }
      });
      return;
    }

    if (!result?.success) {
      showMessage(result?.message || "Registrazione fattura non riuscita.", "Fattura di acquisto", "error");
      return;
    }

    if (page && result.id) {
      page.dataset.invoiceId = String(result.id);
    }
    originalInvoiceIdentityKey = purchaseInvoiceIdentityKey();
    pendingInvoiceRedirectUrl = result.redirectUrl || "/FattureAcquisto";
    try {
      if (await requiresStockLoadBridge()) {
        if (openStockLoadBridge()) {
          return;
        }

        warnAndContinueAfterSave("File XML non disponibile per il carico di magazzino.");
        return;
      }
    } catch {
      warnAndContinueAfterSave("Controllo carico magazzino non riuscito.");
      return;
    }

    await continueAfterStockLoadBridge(true);
  };

  saveButton?.addEventListener("click", async () => {
    if (!validateBeforeSave()) {
      return;
    }

    const confirmOverwrite = duplicateInvoiceConfirmedKey === purchaseInvoiceIdentityKey();
    if (confirmDueDateMismatchIfNeeded(confirmOverwrite)) {
      return;
    }

    try {
      await saveInvoice(confirmOverwrite);
    } catch {
      window.MicronoteProgress?.hide?.();
      showMessage("Registrazione fattura non riuscita.", "Fattura di acquisto", "error");
    }
  });

  [invoiceNumber, invoiceDate, invoiceSupplierCode, invoiceSupplierCodeDisplay].forEach((field) => {
    field?.addEventListener("change", () => {
      void checkDuplicateInvoice();
    });
    field?.addEventListener("blur", () => {
      void checkDuplicateInvoice();
    });
    field?.addEventListener("input", clearDuplicateInvoiceConfirmation);
  });

  form?.addEventListener("keydown", (event) => {
    if (event.key !== "Enter" || event.shiftKey || event.ctrlKey || event.altKey) {
      return;
    }

    const target = event.target;
    if (!isNavigableControl(target) || target.tagName.toLowerCase() === "textarea") {
      return;
    }

    event.preventDefault();
    event.stopImmediatePropagation();
    target.dispatchEvent(new Event("change", { bubbles: true }));
    window.requestAnimationFrame(() => focusNextControl(target));
  }, true);

  invoiceVatTable?.addEventListener("keydown", (event) => {
    if (event.key !== "ArrowUp") {
      return;
    }

    const target = event.target;
    if (!(target instanceof HTMLInputElement)) {
      return;
    }

    const row = target.closest("tr");
    let previous = null;
    if (target.matches("[data-vat-rate]")) {
      const previousRow = row?.previousElementSibling;
      previous = previousRow?.querySelector("[data-vat-rate]") ?? invoiceTotal;
    } else if (target.matches("[data-vat-taxable]")) {
      previous = row?.querySelector("[data-vat-rate]");
    } else if (target.matches("[data-vat-tax]")) {
      previous = row?.querySelector("[data-vat-taxable]");
    }

    if (!previous) {
      return;
    }

    event.preventDefault();
    previous.focus();
    previous.select?.();
  });

  const confirmVatCell = (target, { onlyIfChanged = false } = {}) => {
    if (!(target instanceof HTMLInputElement)) {
      return;
    }

    const row = target.closest("tr");
    const rows = getVatRows();
    const rowIndex = rows.indexOf(row);
    if (rowIndex < 0) {
      return;
    }

    const currentValue = String(target.value ?? "");
    const previousValue = vatCellSnapshots.get(target);
    if (onlyIfChanged && previousValue === currentValue) {
      updateVatTotals();
      return;
    }

    if (target.matches("[data-vat-rate]")) {
      const previousTotal = rows
        .slice(0, rowIndex)
        .reduce((total, vatRow) => total + getVatRowTotal(vatRow), 0);
      calculateVatRowFromGross(row, roundCurrency(parseMoneyField(invoiceTotal) - previousTotal));
      updateVatTotals();
      if (!hasVatRowsDataFrom(rows, rowIndex + 1)) {
        calculateVatRowsFrom(rowIndex + 1);
      }
      vatCellSnapshots.set(target, String(target.value ?? ""));
      return;
    }

    if (target.matches("[data-vat-taxable]")) {
      calculateVatRowFromTaxable(row);
      updateVatTotals();
      if (!hasVatRowsDataFrom(rows, rowIndex + 1)) {
        calculateVatRowsFrom(rowIndex + 1);
      }
      vatCellSnapshots.set(target, String(target.value ?? ""));
      return;
    }

    if (target.matches("[data-vat-tax]")) {
      updateVatTotals();
      if (!hasVatRowsDataFrom(rows, rowIndex + 1)) {
        calculateVatRowsFrom(rowIndex + 1);
      }
      vatCellSnapshots.set(target, String(target.value ?? ""));
    }
  };

  invoiceVatTable?.addEventListener("focus", (event) => {
    if (event.target instanceof HTMLInputElement) {
      vatCellSnapshots.set(event.target, String(event.target.value ?? ""));
    }
  }, true);

  invoiceVatTable?.addEventListener("blur", (event) => {
    confirmVatCell(event.target, { onlyIfChanged: true });
    if (event.target instanceof HTMLInputElement) {
      vatCellSnapshots.delete(event.target);
    }
  }, true);

  invoiceVatTable?.addEventListener("change", (event) => {
    confirmVatCell(event.target, { onlyIfChanged: true });
  });

  invoiceVatTable?.querySelectorAll("[data-vat-rate]").forEach((field) => {
    field.addEventListener("blur", () => clearZeroVatRateDisplay(field));
    field.addEventListener("change", () => clearZeroVatRateDisplay(field));
  });

  electronicInvoiceOpen?.addEventListener("click", openElectronicInvoiceDialog);
  document.querySelector(".purchase-invoice-supplier-field")?.addEventListener("micronote:lookup-selected", (event) => {
    setSelectValue(invoiceContraAccount, event.detail?.row?.accountCode);
    setSelectValue(invoiceStore, event.detail?.row?.storeCode ?? 0);
    clearDuplicateInvoiceConfirmation();
    void checkDuplicateInvoice();
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
  electronicInvoiceRefresh?.addEventListener("click", () => {
    openSystemFolderPicker();
  });
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
  addPaymentButton?.addEventListener("click", openPaymentCodeModal);
  addBankButton?.addEventListener("click", openChartAccountModal);
  calculateDueDatesButton?.addEventListener("click", calculateDueDates);
  clearDueDatesButton?.addEventListener("click", clearDueRows);
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

  [invoiceDate, ...dueDateFields].forEach((field) => {
    syncDateEmptyState(field);
    field?.addEventListener("input", () => normalizeDateInput(field));
    field?.addEventListener("blur", () => normalizeDateOnBlur(field));
    field?.addEventListener("change", () => normalizeDateOnBlur(field));
  });
  clearZeroAmountDueDates();
  applyInitialInvoiceData(readInitialInvoiceData());

  document.addEventListener("focusout", (event) => {
    const target = event.target;
    if (target instanceof HTMLInputElement && target.classList.contains("micronote-date-input")) {
      normalizeDateOnBlur(target, { showError: false });
    }
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
        const date = new Date(activeDateMonth.getFullYear(), activeDateMonth.getMonth(), day);
        setFieldValue(activeDateField, formatDisplayDate(date), "change");
        syncDateEmptyState(activeDateField);
        closeDatePicker();
      }

      return;
    }

    if (!target.closest("[data-date-control]")) {
      closeDatePicker();
    }
  });

  window.addEventListener("resize", positionDatePicker);
  window.addEventListener("scroll", positionDatePicker, true);

  electronicInvoiceDialog?.addEventListener("click", (event) => {
    if (event.target === electronicInvoiceDialog) {
      closeElectronicInvoiceDialog();
    }
  });

  paymentCodeModal?.addEventListener("click", (event) => {
    if (event.target === paymentCodeModal) {
      closePaymentCodeModal();
    }
  });

  chartAccountModal?.addEventListener("click", (event) => {
    if (event.target === chartAccountModal) {
      closeChartAccountModal();
    }
  });

  window.addEventListener("message", (event) => {
    if (event.origin !== window.location.origin) {
      return;
    }

    if (event.data?.type === "micronote:payment-code-saved") {
      closePaymentCodeModal();
      void reloadPaymentOptions();
      return;
    }

    if (event.data?.type === "micronote:payment-code-cancel") {
      closePaymentCodeModal();
      return;
    }

    if (event.data?.type === "micronote:chart-account-saved") {
      closeChartAccountModal();
      void reloadBankOptions();
      return;
    }

    if (event.data?.type === "micronote:chart-account-cancel") {
      closeChartAccountModal();
      return;
    }

    if (event.data?.type === "micronote:stock-load-saved") {
      closeStockLoadModal();
      void continueAfterStockLoadBridge(true);
      return;
    }

    if (event.data?.type === "micronote:stock-load-cancel") {
      closeStockLoadModal();
      if (!window.MicronoteMessageBox?.show) {
        void continueAfterStockLoadBridge(false);
        return;
      }

      window.MicronoteMessageBox.show({
        title: "Fattura di acquisto",
        message: "Carico di magazzino non completato.\nIl file XML non verra' archiviato.",
        variant: "error",
        okText: "OK",
        onConfirm: () => {
          void continueAfterStockLoadBridge(false);
        }
      });
    }
  });

  document.addEventListener("keydown", (event) => {
    if (event.key !== "Escape") {
      return;
    }

    if (datePickerPanel && !datePickerPanel.hidden) {
      event.preventDefault();
      closeDatePicker();
      return;
    }

    if (electronicInvoiceViewer && !electronicInvoiceViewer.hidden) {
      event.preventDefault();
      closeElectronicInvoiceViewer();
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
      return;
    }

    if (paymentCodeModal && !paymentCodeModal.hidden) {
      event.preventDefault();
      closePaymentCodeModal();
      return;
    }

    if (chartAccountModal && !chartAccountModal.hidden) {
      event.preventDefault();
      closeChartAccountModal();
      return;
    }

    if (document.body.classList.contains("lookup-open")) {
      return;
    }

    event.preventDefault();
    window.location.href = page?.dataset.returnUrl || "/FattureAcquisto";
  });

  const importXmlPath = new URLSearchParams(window.location.search).get("importXml");
  if (importXmlPath) {
    selectedElectronicInvoiceFile = {
      name: importXmlPath.split(/[\\/]/).pop() || "",
      fullPath: importXmlPath
    };
    acceptElectronicInvoiceFile();
  }
});



