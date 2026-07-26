document.addEventListener("DOMContentLoaded", () => {
  const page = document.querySelector("[data-stock-purchase-stats]");
  const filterForm = document.querySelector("[data-purchase-stats-filters]");
  const grid = document.querySelector(".purchase-stats-grid-frame");
  const table = grid?.querySelector("table");
  const tableBody = table?.querySelector("tbody");
  const rows = Array.from(table?.querySelectorAll("[data-purchase-stats-row]") ?? []);
  const sortHeaders = Array.from(table?.querySelectorAll("th[data-sort-key]") ?? []);
  const supplierBody = document.querySelector("[data-purchase-stats-supplier-body]");
  const priceCount = document.querySelector("[data-purchase-stats-price-count]");
  const articleModal = document.querySelector("[data-purchase-stats-article-modal]");
  const articleModalFrame = document.querySelector("[data-purchase-stats-article-modal-frame]");
  const accountModal = document.querySelector("[data-purchase-stats-account-modal]");
  const accountModalFrame = document.querySelector("[data-purchase-stats-account-modal-frame]");
  const search = document.querySelector("[data-purchase-stats-search]");
  const refreshButton = document.querySelector("[data-purchase-stats-refresh]");
  let sortState = { key: "", direction: "asc" };
  let detailRequest = 0;
  let activeDateField = null;
  let activeDateMonth = null;
  let datePickerPanel = null;

  if (!page || !grid || !table || !tableBody) {
    return;
  }

  const toKebab = (value) => value.replace(/[A-Z]/g, (match) => `-${match.toLowerCase()}`);

  const visibleRows = () =>
    Array.from(tableBody.querySelectorAll("[data-purchase-stats-row]"))
      .filter((row) => !row.hidden);
  const selectedRow = () => table.querySelector("[data-purchase-stats-row].selected-row");

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
    const hidden = field.closest("[data-date-control]")?.querySelector("[data-purchase-stats-date-hidden]");
    const iso = toIsoDate(field.value);
    if (!hidden || !iso) {
      return false;
    }

    field.value = toDisplayDate(iso);
    hidden.value = iso;
    return true;
  };

  document.querySelectorAll("[data-purchase-stats-date-display]").forEach((field) => {
    field.addEventListener("input", () => normalizeDateInput(field));
    field.addEventListener("blur", () => syncDateHidden(field));
    field.addEventListener("keydown", (event) => {
      if (event.key === "Enter") {
        event.preventDefault();
        syncDateHidden(field);
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
      const field = dateButton.closest("[data-date-control]")?.querySelector("[data-purchase-stats-date-display]");
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

  search?.addEventListener("keydown", (event) => {
    if (event.key === "Enter") {
      event.preventDefault();
    }
  });

  const syncDateFields = () => {
    document.querySelectorAll("[data-purchase-stats-date-display]").forEach((field) => {
      syncDateHidden(field);
    });
  };

  refreshButton?.addEventListener("click", syncDateFields);
  filterForm?.addEventListener("submit", syncDateFields);

  const ensureVisible = (row, direction = 0) => {
    const headerHeight = table.tHead?.offsetHeight ?? 0;
    const visibleTop = grid.scrollTop + headerHeight;
    const visibleBottom = grid.scrollTop + grid.clientHeight;
    const rowTop = row.offsetTop;
    const rowBottom = rowTop + row.offsetHeight;

    if (rowTop >= visibleTop && rowBottom <= visibleBottom) {
      return;
    }

    if (direction >= 0 && rowBottom > visibleBottom) {
      grid.scrollTop = rowBottom - grid.clientHeight + 1;
      return;
    }

    if (direction <= 0 && rowTop < visibleTop) {
      grid.scrollTop = Math.max(rowTop - headerHeight - 1, 0);
    }
  };

  const dispatchRowChanged = (row) => {
    row.dispatchEvent(new CustomEvent("purchase-stats-row-changed", {
      bubbles: true,
      detail: { row }
    }));
  };

  const selectRow = (row, focus = false, direction = 0) => {
    if (!row || row.hidden) {
      return;
    }

    const previous = selectedRow();
    const changed = previous !== row;
    if (changed) {
      rows.forEach((candidate) => {
        candidate.classList.remove("selected", "selected-row");
        candidate.removeAttribute("aria-selected");
      });
      row.classList.add("selected", "selected-row");
      row.setAttribute("aria-selected", "true");
      dispatchRowChanged(row);
    }

    if (focus) {
      row.focus({ preventScroll: true });
    }
    ensureVisible(row, direction);
  };

  const navigateGrid = (event, row) => {
    const currentRows = visibleRows();
    const currentIndex = currentRows.indexOf(row);
    if (currentIndex < 0) {
      return false;
    }

    if (event.key === "ArrowDown") {
      event.preventDefault();
      selectRow(currentRows[Math.min(currentIndex + 1, currentRows.length - 1)], true, 1);
    } else if (event.key === "ArrowUp") {
      event.preventDefault();
      selectRow(currentRows[Math.max(currentIndex - 1, 0)], true, -1);
    } else if (event.key === "Home") {
      event.preventDefault();
      selectRow(currentRows[0], true, -1);
    } else if (event.key === "End") {
      event.preventDefault();
      selectRow(currentRows[currentRows.length - 1], true, 1);
    } else {
      return false;
    }

    return true;
  };

  const sortValue = (row, key, type) => {
    const value = row.getAttribute(`data-sort-${toKebab(key)}`) ?? "";
    if (type === "number") {
      const number = Number.parseFloat(value.replace(",", "."));
      return Number.isFinite(number) ? number : Number.NEGATIVE_INFINITY;
    }

    return value.trim().toLocaleLowerCase("it-IT");
  };

  const updateSortHeaders = () => {
    sortHeaders.forEach((header) => {
      const active = header.dataset.sortKey === sortState.key;
      header.classList.toggle("is-sorted", active);
      header.dataset.sortDirection = active ? sortState.direction : "";
      header.setAttribute("aria-sort", active ? (sortState.direction === "asc" ? "ascending" : "descending") : "none");
    });
  };

  const sortRows = (header) => {
    const key = header.dataset.sortKey || "";
    const type = header.dataset.sortType || "text";
    if (!key) {
      return;
    }

    sortState = {
      key,
      direction: sortState.key === key && sortState.direction === "asc" ? "desc" : "asc"
    };
    const direction = sortState.direction === "asc" ? 1 : -1;
    const selected = selectedRow();
    const ordered = rows
      .map((row, index) => ({ row, index }))
      .sort((left, right) => {
        const leftValue = sortValue(left.row, key, type);
        const rightValue = sortValue(right.row, key, type);
        let result = 0;
        if (typeof leftValue === "number" && typeof rightValue === "number") {
          result = leftValue - rightValue;
        } else {
          result = String(leftValue).localeCompare(String(rightValue), "it-IT", {
            numeric: true,
            sensitivity: "base"
          });
        }
        return result === 0 ? left.index - right.index : result * direction;
      })
      .map((entry) => entry.row);

    ordered.forEach((row) => tableBody.appendChild(row));
    updateSortHeaders();
    selectRow(selected || visibleRows()[0], true, 0);
  };

  const formatMoney = (value) => {
    const number = Number(value) || 0;
    return number === 0
      ? ""
      : number.toLocaleString("it-IT", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  };

  const formatDate = (value) => {
    if (!value) {
      return "";
    }

    const [year, month, day] = String(value).split("-");
    return `${day}-${month}-${year}`;
  };

  const cell = (text) => {
    const td = document.createElement("td");
    td.textContent = text;
    return td;
  };

  const renderSuppliers = (rowsToRender) => {
    if (!supplierBody) {
      return;
    }

    supplierBody.replaceChildren(
      ...rowsToRender.map((supplier) => {
        const row = document.createElement("tr");
        row.append(
          cell(String(Number(supplier.supplierCode) || 0).padStart(5, "0")),
          cell(supplier.supplierName || ""),
          cell(formatMoney(supplier.quantity)),
          cell(formatMoney(supplier.amount)),
          cell(formatMoney(supplier.averageCost)),
          cell(formatMoney(supplier.lastPrice)),
          cell(formatDate(supplier.lastPurchaseDate))
        );
        return row;
      })
    );
  };

  const loadSuppliers = async (row) => {
    const articleCode = row?.dataset.articleCode || "";
    if (!articleCode) {
      renderSuppliers([]);
      return;
    }

    const requestId = ++detailRequest;
    const params = new URLSearchParams(new FormData(filterForm));
    params.set("handler", "Suppliers");
    params.set("articleCode", articleCode);

    try {
      const response = await fetch(`${window.location.pathname}?${params.toString()}`, {
        headers: { "Accept": "application/json" }
      });
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }

      const payload = await response.json();
      if (requestId === detailRequest) {
        renderSuppliers(Array.isArray(payload.rows) ? payload.rows : []);
      }
    } catch {
      if (requestId === detailRequest) {
        renderSuppliers([]);
      }
    }
  };

  const requireSelection = () => {
    const row = selectedRow();
    if (row) {
      return row;
    }

    window.MicronoteMessageBox?.show({
      title: "Acquisti per articolo",
      message: "Selezionare un articolo dalla lista."
    });
    return null;
  };

  const currentReturnUrl = () => window.location.pathname + window.location.search;

  const openArticle = (row) => {
    const code = row.dataset.articleCode || "";
    if (!code) {
      return;
    }

    articleModalFrame.src = `/Articoli/Edit/${encodeURIComponent(code)}?azione=101&returnUrl=${encodeURIComponent(currentReturnUrl())}`;
    articleModal.hidden = false;
    document.body.classList.add("purchase-invoice-payment-modal-open");
    articleModalFrame.focus();
  };

  const closeArticle = () => {
    if (!articleModal) {
      return;
    }

    articleModal.hidden = true;
    document.body.classList.remove("purchase-invoice-payment-modal-open");
    articleModalFrame?.removeAttribute("src");
  };

  const openAccount = (row) => {
    const code = row.dataset.articleCode || "";
    if (!code) {
      return;
    }

    const url = `/EstrattoContoArticolo/Index?articleCode=${encodeURIComponent(code)}&modal=1&returnUrl=${encodeURIComponent(currentReturnUrl())}`;
    if (!accountModal || !accountModalFrame) {
      window.location.href = url;
      return;
    }

    accountModalFrame.src = url;
    accountModal.hidden = false;
    document.body.classList.add("purchase-invoice-payment-modal-open");
    accountModalFrame.focus();
  };

  const closeAccount = () => {
    if (!accountModal) {
      return;
    }

    accountModal.hidden = true;
    document.body.classList.remove("purchase-invoice-payment-modal-open");
    accountModalFrame?.removeAttribute("src");
    document.querySelector("[data-purchase-stats-action='account']")?.focus();
  };

  const verifyPrices = () => {
    let changed = 0;
    rows.forEach((row) => {
      const cells = Array.from(row.querySelectorAll("[data-price-cell]"));
      cells.forEach((cellElement) => {
        cellElement.classList.remove("purchase-price-low", "purchase-price-high");
      });

      const average = Number.parseFloat(row.querySelector("[data-average-price]")?.textContent.replace(/\./g, "").replace(",", ".") || "0") || 0;
      if (average === 0) {
        return;
      }

      let low = null;
      let high = null;
      cells.forEach((cellElement) => {
        const value = Number.parseFloat(cellElement.textContent.replace(/\./g, "").replace(",", ".") || "0") || 0;
        if (value > 0 && value < average && (!low || value < low.value)) {
          low = { cell: cellElement, value };
        }
        if (value > average && (!high || value > high.value)) {
          high = { cell: cellElement, value };
        }
      });

      if (low) {
        low.cell.classList.add("purchase-price-low");
        changed += 1;
      }
      if (high) {
        high.cell.classList.add("purchase-price-high");
        changed += 1;
      }
    });

    if (priceCount) {
      priceCount.value = String(changed);
    }
  };

  rows.forEach((row) => {
    row.addEventListener("click", () => selectRow(row, true, 0));
    row.addEventListener("keydown", (event) => {
      if (event.key === "Enter") {
        event.preventDefault();
        openArticle(row);
        return;
      }
      navigateGrid(event, selectedRow() || row);
    });
  });

  sortHeaders.forEach((header) => {
    header.tabIndex = 0;
    header.setAttribute("aria-sort", "none");
    header.addEventListener("click", () => sortRows(header));
    header.addEventListener("keydown", (event) => {
      if (event.key !== "Enter" && event.key !== " ") {
        return;
      }
      event.preventDefault();
      sortRows(header);
    });
  });

  page.addEventListener("purchase-stats-row-changed", (event) => {
    loadSuppliers(event.detail.row);
  });

  document.querySelectorAll("[data-purchase-stats-action]").forEach((button) => {
    button.addEventListener("click", () => {
      const action = button.dataset.purchaseStatsAction;
      if (action === "print") {
        window.MicronoteMessageBox?.show({
          title: "Stampa",
          message: "Stampa acquisti per articolo.",
          detail: "Funzione in preparazione."
        });
        return;
      }
      if (action === "verify") {
        verifyPrices();
        return;
      }

      const row = requireSelection();
      if (!row) {
        return;
      }

      if (action === "article") {
        openArticle(row);
        return;
      }
      if (action === "account") {
        openAccount(row);
      }
    });
  });

  articleModal?.addEventListener("click", (event) => {
    if (event.target === articleModal) {
      closeArticle();
    }
  });

  accountModal?.addEventListener("click", (event) => {
    if (event.target === accountModal) {
      closeAccount();
    }
  });

  window.addEventListener("message", (event) => {
    const isSameOrigin = event.origin === window.location.origin;
    const isArticleFrame = event.source === articleModalFrame?.contentWindow;
    const isAccountFrame = event.source === accountModalFrame?.contentWindow;
    if (!isSameOrigin && !isArticleFrame && !isAccountFrame) {
      return;
    }

    if (event.data?.type === "micronote:article-cancel" || event.data?.type === "micronote:article-saved") {
      closeArticle();
    }

    if (event.data?.type === "micronote:article-account-cancel") {
      closeAccount();
    }
  });

  document.addEventListener("keydown", (event) => {
    if (datePickerPanel && !datePickerPanel.hidden) {
      return;
    }
    if (event.key === "Escape" && articleModal && !articleModal.hidden) {
      event.preventDefault();
      closeArticle();
      return;
    }
    if (event.key === "Escape" && accountModal && !accountModal.hidden) {
      event.preventDefault();
      closeAccount();
      return;
    }
    if (event.key === "Escape") {
      event.preventDefault();
      window.location.href = "/";
    }
  });

  grid.addEventListener("keydown", (event) => {
    if (event.target.closest("[data-purchase-stats-row]")) {
      return;
    }

    const row = selectedRow() || visibleRows()[0];
    if (row) {
      navigateGrid(event, row);
    }
  });

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

      const selected = selectedRow();
      if (!selected) {
        return;
      }

      const gridRect = grid.getBoundingClientRect();
      const headerHeight = table.tHead?.getBoundingClientRect().height ?? 0;
      const viewportTop = gridRect.top + headerHeight;
      const viewportBottom = gridRect.bottom;
      const selectedRect = selected.getBoundingClientRect();
      const isVisible = selectedRect.bottom > viewportTop + 1 &&
        selectedRect.top < viewportBottom - 1;
      if (isVisible) {
        return;
      }

      const visible = visibleRows().filter((row) => {
        const rect = row.getBoundingClientRect();
        return rect.top >= viewportTop + 1 && rect.bottom <= viewportBottom - 1;
      });
      if (visible.length > 0) {
        selectRow(delta > 0 ? visible[0] : visible[visible.length - 1], true, delta > 0 ? 1 : -1);
      }
    });
  }, { passive: true });

  const initialRow = selectedRow() || visibleRows()[0];
  if (initialRow) {
    selectRow(initialRow, true, 0);
  }
});
