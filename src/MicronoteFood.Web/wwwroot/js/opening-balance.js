document.addEventListener("DOMContentLoaded", () => {
  const page = document.querySelector("[data-opening-balance]");
  const form = page?.querySelector("[data-opening-balance-form]");
  const payload = form?.querySelector("[data-opening-balance-payload]");
  const yearSelect = form?.querySelector("[data-opening-balance-year]");
  const table = page?.querySelector("[data-opening-balance-table]");
  const frame = page?.querySelector(".opening-balance-grid-frame");
  const rows = Array.from(page?.querySelectorAll("[data-opening-balance-row]") ?? []);
  const clearButton = page?.querySelector("[data-opening-balance-clear]");
  const exitLink = page?.querySelector("[data-opening-balance-exit]");
  const dateField = page?.querySelector("[data-opening-date-display]");
  const dateButton = page?.querySelector("[data-opening-date-picker-button]");
  let modified = false;
  let activeCellSide = "debit";
  let datePickerPanel = null;
  let activeDateMonth = null;

  if (!page || !form || !payload || !table || !frame) {
    return;
  }

  const normalize = (value) => String(value ?? "")
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLocaleLowerCase("it-IT")
    .trim();

  const parseDecimal = (value) => {
    const text = String(value ?? "").trim();
    const commaIndex = text.lastIndexOf(",");
    const dotIndex = text.lastIndexOf(".");
    const decimalIndex = Math.max(commaIndex, dotIndex);
    const normalized = decimalIndex >= 0
      ? `${text.slice(0, decimalIndex).replace(/[^\d]/g, "")}.${text.slice(decimalIndex + 1).replace(/[^\d]/g, "")}`
      : text.replace(/[^\d]/g, "");
    const parsed = Number.parseFloat(normalized);
    return Number.isFinite(parsed) ? parsed : 0;
  };

  const formatMoney = (value) => {
    const number = Number(value || 0);
    if (!Number.isFinite(number) || number === 0) {
      return "";
    }

    const fixed = Math.abs(number).toFixed(2);
    const [integerPart, decimalPart] = fixed.split(".");
    const groupedInteger = integerPart.replace(/\B(?=(\d{3})+(?!\d))/g, ".");
    return `${groupedInteger},${decimalPart}`;
  };

  const formatTotal = (value) => formatMoney(value) || "0,00";

  const formatForEdit = (value) => {
    const number = Number(value || 0);
    return number === 0 ? "" : String(Math.round(number * 100) / 100).replace(".", ",");
  };

  const cleanAmount = (input) => {
    const text = String(input.value ?? "").replace(/[^\d.,]/g, "");
    const separatorMatches = Array.from(text.matchAll(/[.,]/g));
    const decimalIndex = separatorMatches.length > 1
      ? separatorMatches[separatorMatches.length - 1].index
      : (separatorMatches[0]?.index ?? -1);

    input.value = decimalIndex >= 0
      ? `${text.slice(0, decimalIndex).replace(/[.,]/g, "").slice(0, 12)},${text.slice(decimalIndex + 1).replace(/[.,]/g, "").slice(0, 2)}`
      : text.replace(/[.,]/g, "").slice(0, 12);
  };

  const amountInput = (row, side = activeCellSide) =>
    row?.querySelector(`[data-opening-balance-amount="${side}"]`);

  const updateTotals = () => {
    ["debit", "credit"].forEach((side) => {
      const total = rows.reduce((sum, row) => sum + parseDecimal(amountInput(row, side)?.value), 0);
      const target = page.querySelector(`[data-opening-balance-total="${side}"]`);
      if (target) {
        target.value = formatTotal(total);
      }
    });
  };

  const updateRowSort = (row) => {
    row.dataset.sortDebit = String(parseDecimal(amountInput(row, "debit")?.value));
    row.dataset.sortCredit = String(parseDecimal(amountInput(row, "credit")?.value));
    updateTotals();
  };

  const commitAmount = (input) => {
    cleanAmount(input);
    input.value = formatMoney(parseDecimal(input.value));
    updateRowSort(input.closest("[data-opening-balance-row]"));
  };

  const selectedRow = () => page.querySelector("[data-opening-balance-row].selected-row");
  const visibleRows = () => rows.filter((row) => !row.hidden);

  const ensureVisible = (row, direction = 0) => {
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

  const selectRow = (row, direction = 0) => {
    if (!row) {
      return;
    }

    rows.forEach((candidate) => {
      candidate.classList.remove("selected-row");
      candidate.removeAttribute("aria-selected");
    });
    row.classList.add("selected-row");
    row.setAttribute("aria-selected", "true");
    ensureVisible(row, direction);
  };

  const focusAmount = (row, side = activeCellSide, direction = 0) => {
    const input = amountInput(row, side);
    if (!input) {
      return;
    }

    activeCellSide = side;
    selectRow(row, direction);
    input.focus({ preventScroll: true });
    input.select();
  };

  const gridViewport = () => {
    const gridRect = frame.getBoundingClientRect();
    const headerHeight = table.tHead?.getBoundingClientRect().height ?? 0;

    return {
      top: gridRect.top + headerHeight,
      bottom: gridRect.bottom
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

  const selectVisibleRowFromScroll = (delta) => {
    if (delta === 0) {
      return;
    }

    const viewport = gridViewport();
    const selected = selectedRow();
    if (!selected || isRowVisible(selected, viewport)) {
      return;
    }

    const currentRows = fullyVisibleRows(viewport);
    if (currentRows.length === 0) {
      return;
    }

    focusAmount(delta > 0 ? currentRows[0] : currentRows[currentRows.length - 1], activeCellSide);
  };

  const moveSelection = (row, offset) => {
    const currentRows = visibleRows();
    const index = currentRows.indexOf(row);
    if (index < 0) {
      return false;
    }

    const nextIndex = Math.max(Math.min(index + offset, currentRows.length - 1), 0);
    focusAmount(currentRows[nextIndex], activeCellSide, Math.sign(offset));
    return true;
  };

  const navigateRows = (event, row) => {
    if (event.key === "ArrowDown") {
      event.preventDefault();
      return moveSelection(row, 1);
    }

    if (event.key === "ArrowUp") {
      event.preventDefault();
      return moveSelection(row, -1);
    }

    if (event.key === "ArrowLeft" || event.key === "ArrowRight") {
      event.preventDefault();
      focusAmount(row, event.key === "ArrowLeft" ? "debit" : "credit");
      return true;
    }

    if (event.key === "PageDown" || event.key === "PageUp") {
      event.preventDefault();
      const visibleCount = Math.max(Math.floor((frame.clientHeight - (table.tHead?.offsetHeight ?? 0)) / (row.offsetHeight || 27)) - 1, 1);
      return moveSelection(row, event.key === "PageDown" ? visibleCount : -visibleCount);
    }

    if (event.key === "Home") {
      event.preventDefault();
      focusAmount(visibleRows()[0], activeCellSide, -1);
      return true;
    }

    if (event.key === "End") {
      event.preventDefault();
      const currentRows = visibleRows();
      focusAmount(currentRows[currentRows.length - 1], activeCellSide, 1);
      return true;
    }

    return false;
  };

  const sortDataKey = (key) => `sort${key[0].toUpperCase()}${key.slice(1)}`;

  const sortValue = (row, key, type) => {
    if (type === "number") {
      return Number.parseFloat(row.dataset[sortDataKey(key)] ?? "0") || 0;
    }

    return normalize(row.dataset[sortDataKey(key)] ?? "");
  };

  const sortByHeader = (header) => {
    const key = header.dataset.sortKey;
    if (!key) {
      return;
    }

    const direction = header.dataset.sortDirection === "asc" ? "desc" : "asc";
    const selectedAccount = selectedRow()?.dataset.accountCode ?? "";
    const multiplier = direction === "desc" ? -1 : 1;
    const body = table.tBodies[0];

    rows
      .sort((left, right) => {
        const leftValue = sortValue(left, key, header.dataset.sortType);
        const rightValue = sortValue(right, key, header.dataset.sortType);
        if (leftValue < rightValue) {
          return -1 * multiplier;
        }
        if (leftValue > rightValue) {
          return 1 * multiplier;
        }
        return 0;
      })
      .forEach((row) => body.appendChild(row));

    table.querySelectorAll("[data-sort-key]").forEach((candidate) => {
      const active = candidate === header;
      candidate.classList.toggle("is-sorted", active);
      candidate.dataset.sortDirection = active ? direction : "";
      candidate.setAttribute("aria-sort", active ? (direction === "asc" ? "ascending" : "descending") : "none");
    });

    focusAmount(rows.find((row) => row.dataset.accountCode === selectedAccount) ?? visibleRows()[0]);
  };

  const buildPayload = () => {
    payload.value = JSON.stringify(rows.map((row) => ({
      accountCode: Number.parseInt(row.dataset.accountCode ?? "0", 10),
      debit: parseDecimal(amountInput(row, "debit")?.value),
      credit: parseDecimal(amountInput(row, "credit")?.value)
    })));
  };

  const confirmExit = (url) => {
    if (!modified) {
      window.location.href = url;
      return;
    }

    window.MicronoteMessageBox?.show({
      title: "Apertura conti patrimoniali",
      message: "Uscire senza salvare le modifiche?",
      mode: "confirm",
      okText: "Esci",
      onConfirm: () => {
        window.location.href = url;
      }
    });
  };

  const parseDisplayDate = (value) => {
    const match = String(value ?? "").match(/^(\d{1,2})[-/](\d{1,2})[-/](\d{4})$/);
    if (!match) {
      return null;
    }

    const day = Number.parseInt(match[1], 10);
    const month = Number.parseInt(match[2], 10) - 1;
    const year = Number.parseInt(match[3], 10);
    const date = new Date(year, month, day);
    return date.getFullYear() === year && date.getMonth() === month && date.getDate() === day ? date : null;
  };

  const formatDisplayDate = (date) =>
    `${String(date.getDate()).padStart(2, "0")}-${String(date.getMonth() + 1).padStart(2, "0")}-${date.getFullYear()}`;

  const closeDatePicker = () => {
    if (datePickerPanel) {
      datePickerPanel.hidden = true;
    }
  };

  const ensureDatePicker = () => {
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
    const panel = ensureDatePicker();
    const month = activeDateMonth ?? new Date();
    const monthLabel = month.toLocaleDateString("it-IT", { month: "long", year: "numeric" });
    const firstDay = new Date(month.getFullYear(), month.getMonth(), 1);
    const daysInMonth = new Date(month.getFullYear(), month.getMonth() + 1, 0).getDate();
    const startOffset = (firstDay.getDay() + 6) % 7;
    const selectedDate = parseDisplayDate(dateField?.value);
    const weekdays = ["L", "M", "M", "G", "V", "S", "D"];
    const cells = [];

    for (let index = 0; index < startOffset; index += 1) {
      cells.push('<span class="micronote-date-picker-empty"></span>');
    }

    for (let day = 1; day <= daysInMonth; day += 1) {
      const isSelected = selectedDate
        && selectedDate.getFullYear() === month.getFullYear()
        && selectedDate.getMonth() === month.getMonth()
        && selectedDate.getDate() === day;
      cells.push(`<button type="button" class="${isSelected ? "is-selected" : ""}" data-opening-date-picker-day="${day}">${day}</button>`);
    }

    panel.innerHTML = `
      <div class="micronote-date-picker-head">
        <button type="button" data-opening-date-picker-prev>&lt;</button>
        <strong>${monthLabel}</strong>
        <button type="button" data-opening-date-picker-next>&gt;</button>
      </div>
      <div class="micronote-date-picker-weekdays">
        ${weekdays.map((day) => `<span>${day}</span>`).join("")}
      </div>
      <div class="micronote-date-picker-days">
        ${cells.join("")}
      </div>
    `;

    const rect = dateButton.getBoundingClientRect();
    panel.style.left = `${Math.min(rect.left, window.innerWidth - 230)}px`;
    panel.style.top = `${rect.bottom + 4}px`;
    panel.hidden = false;
  };

  rows.forEach((row) => {
    row.addEventListener("click", (event) => {
      const input = event.target.closest("[data-opening-balance-amount]");
      focusAmount(row, input?.dataset.openingBalanceAmount ?? activeCellSide);
    });

    row.querySelectorAll("[data-opening-balance-amount]").forEach((input) => {
      input.addEventListener("focus", () => {
        activeCellSide = input.dataset.openingBalanceAmount;
        selectRow(row);
        input.value = formatForEdit(parseDecimal(input.value));
        input.select();
      });
      input.addEventListener("input", () => {
        cleanAmount(input);
        updateRowSort(row);
        modified = true;
      });
      input.addEventListener("blur", () => commitAmount(input));
      input.addEventListener("keydown", (event) => {
        if (event.key === "Enter") {
          event.preventDefault();
          event.stopPropagation();
          commitAmount(input);
          const currentRows = visibleRows();
          const next = currentRows[Math.min(currentRows.indexOf(row) + 1, currentRows.length - 1)];
          focusAmount(next, activeCellSide, 1);
          return;
        }

        if (event.key === "Delete") {
          event.preventDefault();
          event.stopPropagation();
          input.value = "";
          updateRowSort(row);
          modified = true;
          return;
        }

        if (navigateRows(event, row)) {
          event.stopPropagation();
        }
      });
    });
  });

  table.querySelectorAll("[data-sort-key]").forEach((header) => {
    header.tabIndex = 0;
    header.setAttribute("role", "button");
    header.setAttribute("aria-sort", "none");
    header.addEventListener("click", () => sortByHeader(header));
    header.addEventListener("keydown", (event) => {
      if (event.key !== "Enter" && event.key !== " ") {
        return;
      }

      event.preventDefault();
      sortByHeader(header);
    });
  });

  let lastScrollTop = frame.scrollTop;
  let scrollFrame = 0;

  frame.addEventListener("scroll", () => {
    if (scrollFrame) {
      window.cancelAnimationFrame(scrollFrame);
    }

    scrollFrame = window.requestAnimationFrame(() => {
      scrollFrame = 0;
      const currentScrollTop = frame.scrollTop;
      const delta = currentScrollTop - lastScrollTop;
      lastScrollTop = currentScrollTop;

      selectVisibleRowFromScroll(delta);
    });
  }, { passive: true });

  let wheelFrame = 0;

  frame.addEventListener("wheel", (event) => {
    if (wheelFrame) {
      window.cancelAnimationFrame(wheelFrame);
    }

    const delta = event.deltaY;
    wheelFrame = window.requestAnimationFrame(() => {
      wheelFrame = window.requestAnimationFrame(() => {
        wheelFrame = 0;
        selectVisibleRowFromScroll(delta);
      });
    });
  }, { passive: true });

  clearButton?.addEventListener("click", () => {
    rows.forEach((row) => {
      row.querySelectorAll("[data-opening-balance-amount]").forEach((input) => {
        input.value = "";
      });
      updateRowSort(row);
    });
    modified = true;
    focusAmount(rows[0], "debit");
  });

  yearSelect?.addEventListener("change", () => {
    if (modified) {
      window.MicronoteMessageBox?.show({
        title: "Apertura conti patrimoniali",
        message: "Cambiare esercizio senza salvare le modifiche?",
        mode: "confirm",
        okText: "Cambia",
        onConfirm: () => {
          window.location.href = `${window.location.pathname}?year=${encodeURIComponent(yearSelect.value)}`;
        }
      });
      return;
    }

    window.location.href = `${window.location.pathname}?year=${encodeURIComponent(yearSelect.value)}`;
  });

  dateField?.addEventListener("input", () => {
    const text = String(dateField.value ?? "").replace(/[^\d]/g, "").slice(0, 8);
    const parts = [text.slice(0, 2), text.slice(2, 4), text.slice(4, 8)].filter(Boolean);
    dateField.value = parts.join("-");
    modified = true;
  });

  dateButton?.addEventListener("click", (event) => {
    event.preventDefault();
    if (datePickerPanel && !datePickerPanel.hidden) {
      closeDatePicker();
      return;
    }

    const parsed = parseDisplayDate(dateField?.value);
    activeDateMonth = parsed
      ? new Date(parsed.getFullYear(), parsed.getMonth(), 1)
      : new Date(Number.parseInt(yearSelect?.value ?? String(new Date().getFullYear()), 10), 0, 1);
    renderDatePicker();
  });

  document.addEventListener("mousedown", (event) => {
    const target = event.target;
    if (target instanceof Element && target.closest(".micronote-date-picker")) {
      event.preventDefault();
    }
  });

  document.addEventListener("click", (event) => {
    const target = event.target;
    if (!(target instanceof Element)) {
      return;
    }

    if (target.closest(".micronote-date-picker")) {
      event.preventDefault();
      const previousButton = target.closest("[data-opening-date-picker-prev]");
      const nextButton = target.closest("[data-opening-date-picker-next]");
      const dayButton = target.closest("[data-opening-date-picker-day]");

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

      if (dayButton && dateField && activeDateMonth) {
        const day = Number.parseInt(dayButton.getAttribute("data-opening-date-picker-day") ?? "0", 10);
        dateField.value = formatDisplayDate(new Date(activeDateMonth.getFullYear(), activeDateMonth.getMonth(), day));
        modified = true;
        closeDatePicker();
      }

      return;
    }

    if (!target.closest("[data-opening-date-control]")) {
      closeDatePicker();
    }
  });

  form.addEventListener("submit", () => {
    rows.forEach((row) => {
      row.querySelectorAll("[data-opening-balance-amount]").forEach((input) => commitAmount(input));
    });
    buildPayload();
    modified = false;
  });

  exitLink?.addEventListener("click", (event) => {
    event.preventDefault();
    confirmExit(exitLink.href || "/");
  });

  document.addEventListener("keydown", (event) => {
    if (event.key !== "Escape" || document.body.classList.contains("lookup-open")) {
      return;
    }

    event.preventDefault();
    confirmExit("/");
  });

  updateTotals();
  if (rows.length > 0) {
    focusAmount(rows[0], "debit");
  }
});
