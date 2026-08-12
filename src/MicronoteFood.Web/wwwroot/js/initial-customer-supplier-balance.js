document.addEventListener("DOMContentLoaded", () => {
  const page = document.querySelector("[data-initial-cf-balance]");
  const form = page?.querySelector("[data-initial-cf-form]");
  const payload = form?.querySelector("[data-initial-cf-payload]");
  const yearSelect = form?.querySelector("[data-initial-cf-year]");
  const tables = Array.from(page?.querySelectorAll("[data-initial-cf-table]") ?? []);
  const rows = Array.from(page?.querySelectorAll("[data-initial-cf-row]") ?? []);
  const exitLink = page?.querySelector("[data-initial-cf-exit]");
  const clearButton = page?.querySelector("[data-initial-cf-clear]");
  let modified = false;

  if (!page || !form || !payload || tables.length === 0) {
    return;
  }

  const normalize = (value) => String(value ?? "")
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLocaleLowerCase("it-IT")
    .trim();

  const parseDecimal = (value) => {
    const text = String(value ?? "").trim();
    const sign = text.startsWith("-") ? -1 : 1;
    const unsigned = text.replace(/^[+-]/, "");
    const commaIndex = unsigned.lastIndexOf(",");
    const dotIndex = unsigned.lastIndexOf(".");
    const decimalIndex = Math.max(commaIndex, dotIndex);
    const normalized = decimalIndex >= 0
      ? `${unsigned.slice(0, decimalIndex).replace(/[.,]/g, "")}.${unsigned.slice(decimalIndex + 1).replace(/[.,]/g, "")}`
      : unsigned.replace(/[.,]/g, "");
    const parsed = Number.parseFloat(normalized);
    return Number.isFinite(parsed) ? parsed * sign : 0;
  };

  const formatMoney = (value) => {
    const number = Number(value || 0);
    if (!Number.isFinite(number) || number === 0) {
      return "";
    }

    const fixed = Math.abs(number).toFixed(2);
    const [integerPart, decimalPart] = fixed.split(".");
    const sign = number < 0 ? "-" : "";
    const groupedInteger = integerPart.replace(/\B(?=(\d{3})+(?!\d))/g, ".");
    return `${sign}${groupedInteger},${decimalPart}`;
  };

  const formatTotal = (value) => {
    const formatted = formatMoney(value);
    return formatted || "0,00";
  };

  const cleanBalance = (input) => {
    const original = String(input.value ?? "").trim();
    const sign = original.startsWith("-") ? "-" : "";
    const text = original.replace(/[^\d.,]/g, "");
    const separatorMatches = Array.from(text.matchAll(/[.,]/g));
    const decimalIndex = separatorMatches.length > 1
      ? separatorMatches[separatorMatches.length - 1].index
      : (separatorMatches[0]?.index ?? -1);

    input.value = decimalIndex >= 0
      ? `${sign}${text.slice(0, decimalIndex).replace(/[.,]/g, "").slice(0, 10)},${text.slice(decimalIndex + 1).replace(/[.,]/g, "").slice(0, 2)}`
      : `${sign}${text.replace(/[.,]/g, "").slice(0, 10)}`;
  };

  const dataKey = (key) => `sort${key[0].toUpperCase()}${key.slice(1)}`;

  const selectedRow = () => page.querySelector("[data-initial-cf-row].selected-row");
  const tableRows = (table) => Array.from(table.querySelectorAll("[data-initial-cf-row]"));
  const visibleRows = (table) => tableRows(table).filter((row) => !row.hidden);

  const updateTotals = () => {
    ["C", "F"].forEach((type) => {
      const total = rows
        .filter((row) => row.dataset.cfType === type)
        .reduce((sum, row) => sum + parseDecimal(row.querySelector("[data-initial-cf-balance-input]")?.value), 0);
      const target = page.querySelector(`[data-initial-cf-total="${type}"]`);
      if (target) {
        target.value = formatTotal(total);
      }
    });
  };

  const updateRowBalanceSort = (input) => {
    const row = input.closest("[data-initial-cf-row]");
    if (row) {
      row.dataset.sortBalance = String(parseDecimal(input.value));
    }
    updateTotals();
  };

  const commitBalance = (input) => {
    cleanBalance(input);
    input.value = formatMoney(parseDecimal(input.value));
    updateRowBalanceSort(input);
  };

  const gridFrame = (row) => row.closest(".initial-cf-grid-frame");
  const tableForRow = (row) => row.closest("[data-initial-cf-table]");

  const gridViewport = (table) => {
    const frame = table.closest(".initial-cf-grid-frame");
    const gridRect = frame.getBoundingClientRect();
    const headerHeight = table.tHead?.getBoundingClientRect().height ?? 0;
    return {
      top: gridRect.top + headerHeight,
      bottom: gridRect.bottom
    };
  };

  const ensureVisible = (row, direction = 0) => {
    const frame = gridFrame(row);
    const table = tableForRow(row);
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

  const focusBalance = (row, direction = 0) => {
    const input = row?.querySelector("[data-initial-cf-balance-input]");
    if (!input) {
      return;
    }

    selectRow(row, direction);
    input.focus({ preventScroll: true });
    input.select();
  };

  const isRowVisible = (row, viewport) => {
    const rowRect = row.getBoundingClientRect();
    return rowRect.bottom > viewport.top + 1 && rowRect.top < viewport.bottom - 1;
  };

  const fullyVisibleRows = (table, viewport) =>
    visibleRows(table).filter((row) => {
      const rowRect = row.getBoundingClientRect();
      return rowRect.top >= viewport.top + 1 && rowRect.bottom <= viewport.bottom - 1;
    });

  const selectVisibleRowFromScroll = (table, delta) => {
    if (delta === 0) {
      return;
    }

    const selected = selectedRow();
    const viewport = gridViewport(table);
    if (!selected || tableForRow(selected) !== table || isRowVisible(selected, viewport)) {
      return;
    }

    const currentRows = fullyVisibleRows(table, viewport);
    if (currentRows.length > 0) {
      focusBalance(delta > 0 ? currentRows[0] : currentRows[currentRows.length - 1]);
    }
  };

  const moveSelection = (row, offset) => {
    const currentRows = visibleRows(tableForRow(row));
    const index = currentRows.indexOf(row);
    if (index < 0) {
      return false;
    }

    const nextIndex = Math.max(Math.min(index + offset, currentRows.length - 1), 0);
    focusBalance(currentRows[nextIndex], Math.sign(offset));
    return true;
  };

  const navigateRows = (event, row) => {
    const table = tableForRow(row);
    if (!visibleRows(table).includes(row)) {
      return false;
    }

    if (event.key === "ArrowDown") {
      event.preventDefault();
      return moveSelection(row, 1);
    }

    if (event.key === "ArrowUp") {
      event.preventDefault();
      return moveSelection(row, -1);
    }

    if (event.key === "PageDown" || event.key === "PageUp") {
      event.preventDefault();
      const frame = gridFrame(row);
      const visibleCount = Math.max(Math.floor((frame.clientHeight - (table.tHead?.offsetHeight ?? 0)) / (row.offsetHeight || 27)) - 1, 1);
      return moveSelection(row, event.key === "PageDown" ? visibleCount : -visibleCount);
    }

    if (event.key === "Home") {
      event.preventDefault();
      focusBalance(visibleRows(table)[0], -1);
      return true;
    }

    if (event.key === "End") {
      event.preventDefault();
      const currentRows = visibleRows(table);
      focusBalance(currentRows[currentRows.length - 1], 1);
      return true;
    }

    return false;
  };

  const sortValue = (row, key, type) => {
    if (type === "number") {
      return Number.parseFloat(row.dataset[dataKey(key)] ?? "0") || 0;
    }

    return normalize(row.dataset[dataKey(key)] ?? "");
  };

  const sortByHeader = (table, header) => {
    const key = header.dataset.sortKey;
    if (!key) {
      return;
    }

    const direction = header.dataset.sortDirection === "asc" ? "desc" : "asc";
    const selectedCode = selectedRow()?.dataset.code ?? "";
    const selectedType = selectedRow()?.dataset.cfType ?? "";
    const multiplier = direction === "desc" ? -1 : 1;
    const body = table.tBodies[0];

    tableRows(table)
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

    selectRow(rows.find((row) => row.dataset.code === selectedCode && row.dataset.cfType === selectedType) ?? visibleRows(table)[0]);
  };

  const buildPayload = () => {
    payload.value = JSON.stringify(rows.map((row) => ({
      type: row.dataset.cfType ?? "",
      code: Number.parseInt(row.dataset.code ?? "0", 10),
      balance: parseDecimal(row.querySelector("[data-initial-cf-balance-input]")?.value)
    })));
  };

  const confirmExit = (url) => {
    if (!modified) {
      window.location.href = url;
      return;
    }

    window.MicronoteMessageBox?.show({
      title: "Saldo iniziale clienti e fornitori",
      message: "Uscire senza salvare le modifiche?",
      mode: "confirm",
      okText: "Esci",
      onConfirm: () => {
        window.location.href = url;
      }
    });
  };

  rows.forEach((row) => {
    const input = row.querySelector("[data-initial-cf-balance-input]");

    row.addEventListener("click", () => focusBalance(row));
    row.addEventListener("dblclick", () => focusBalance(row));
    row.addEventListener("keydown", (event) => {
      if (event.key === "Enter") {
        event.preventDefault();
        focusBalance(row);
        return;
      }

      navigateRows(event, row);
    });

    input?.addEventListener("focus", () => {
      selectRow(row);
      input.select();
    });
    input?.addEventListener("input", () => {
      cleanBalance(input);
      updateRowBalanceSort(input);
      modified = true;
    });
    input?.addEventListener("blur", () => commitBalance(input));
    input?.addEventListener("keydown", (event) => {
      if (event.key === "Enter") {
        event.preventDefault();
        event.stopPropagation();
        commitBalance(input);
        const currentRows = visibleRows(tableForRow(row));
        const next = currentRows[Math.min(currentRows.indexOf(row) + 1, currentRows.length - 1)];
        focusBalance(next, 1);
        return;
      }

      if (event.key === "Delete") {
        event.preventDefault();
        event.stopPropagation();
        input.value = "";
        updateRowBalanceSort(input);
        modified = true;
        return;
      }

      if (navigateRows(event, row)) {
        event.stopPropagation();
      }
    });
  });

  tables.forEach((table) => {
    const headers = Array.from(table.querySelectorAll("[data-sort-key]"));
    headers.forEach((header) => {
      header.tabIndex = 0;
      header.setAttribute("role", "button");
      header.setAttribute("aria-sort", "none");
      header.addEventListener("click", () => sortByHeader(table, header));
      header.addEventListener("keydown", (event) => {
        if (event.key !== "Enter" && event.key !== " ") {
          return;
        }

        event.preventDefault();
        sortByHeader(table, header);
      });
    });

    const frame = table.closest(".initial-cf-grid-frame");
    let lastScrollTop = frame.scrollTop;
    let scrollFrame = 0;
    let wheelFrame = 0;

    frame.addEventListener("scroll", () => {
      if (scrollFrame) {
        window.cancelAnimationFrame(scrollFrame);
      }

      scrollFrame = window.requestAnimationFrame(() => {
        scrollFrame = 0;
        const currentScrollTop = frame.scrollTop;
        const delta = currentScrollTop - lastScrollTop;
        lastScrollTop = currentScrollTop;
        selectVisibleRowFromScroll(table, delta);
      });
    }, { passive: true });

    frame.addEventListener("wheel", (event) => {
      if (wheelFrame) {
        window.cancelAnimationFrame(wheelFrame);
      }

      const delta = event.deltaY;
      wheelFrame = window.requestAnimationFrame(() => {
        wheelFrame = window.requestAnimationFrame(() => {
          wheelFrame = 0;
          selectVisibleRowFromScroll(table, delta);
        });
      });
    }, { passive: true });
  });

  clearButton?.addEventListener("click", () => {
    rows.forEach((row) => {
      const input = row.querySelector("[data-initial-cf-balance-input]");
      if (input) {
        input.value = "";
        updateRowBalanceSort(input);
      }
    });
    modified = true;
    focusBalance(rows[0]);
  });

  yearSelect?.addEventListener("change", () => {
    if (modified) {
      window.MicronoteMessageBox?.show({
        title: "Saldo iniziale clienti e fornitori",
        message: "Cambiare anno senza salvare le modifiche?",
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

  form.addEventListener("submit", () => {
    rows.forEach((row) => {
      const input = row.querySelector("[data-initial-cf-balance-input]");
      if (input) {
        commitBalance(input);
      }
    });
    buildPayload();
    modified = false;
  });

  form.addEventListener("formdata", (event) => {
    rows.forEach((row) => {
      const input = row.querySelector("[data-initial-cf-balance-input]");
      if (input) {
        commitBalance(input);
      }
    });
    buildPayload();
    event.formData.set("Payload", payload.value);
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

  rows.forEach((row) => {
    const input = row.querySelector("[data-initial-cf-balance-input]");
    if (input) {
      const initialValue = Number.parseFloat(input.dataset.initialValue ?? "0");
      input.value = formatMoney(Number.isFinite(initialValue) ? initialValue : 0);
      updateRowBalanceSort(input);
    }
  });
  updateTotals();
  if (rows.length > 0) {
    focusBalance(rows[0]);
    const firstInput = rows[0].querySelector("[data-initial-cf-balance-input]");
    if (firstInput) {
      commitBalance(firstInput);
      firstInput.select();
    }
  }
});
