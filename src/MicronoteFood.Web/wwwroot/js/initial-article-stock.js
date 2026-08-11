document.addEventListener("DOMContentLoaded", () => {
  const page = document.querySelector("[data-initial-article-stock]");
  const form = page?.querySelector("[data-initial-stock-form]");
  const payload = form?.querySelector("[data-initial-stock-payload]");
  const inventoryDate = page?.querySelector("[data-initial-stock-date]");
  const grid = page?.querySelector(".initial-article-stock-grid-frame");
  const table = grid?.querySelector("table");
  const tableBody = table?.querySelector("tbody");
  const rows = Array.from(table?.querySelectorAll("[data-initial-stock-row]") ?? []);
  const sortHeaders = Array.from(table?.querySelectorAll("[data-sort-key]") ?? []);
  const exitLink = page?.querySelector("[data-initial-stock-exit]");
  let currentSortKey = "";
  let currentSortDirection = "asc";
  let modified = false;
  const packagesSelector = "[data-initial-stock-packages]";
  const quantitySelector = "[data-initial-stock-quantity]";

  if (!page || !form || !payload || !grid || !table || !tableBody) {
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
      ? `${text.slice(0, decimalIndex).replace(/[.,]/g, "")}.${text.slice(decimalIndex + 1).replace(/[.,]/g, "")}`
      : text.replace(/[.,]/g, "");
    const parsed = Number.parseFloat(normalized);
    return Number.isFinite(parsed) ? parsed : 0;
  };

  const formatQuantity = (value) => {
    const number = Number(value || 0);
    if (!Number.isFinite(number) || number === 0) {
      return "";
    }

    const fixed = Math.abs(number).toFixed(3);
    const [integerPart, decimalPart] = fixed.split(".");
    const sign = number < 0 ? "-" : "";
    const groupedInteger = integerPart.replace(/\B(?=(\d{3})+(?!\d))/g, ".");

    return `${sign}${groupedInteger},${decimalPart}`;
  };

  const formatForEdit = (value) => {
    const number = Number(value || 0);
    return number === 0 ? "" : String(Math.round(number * 1000) / 1000);
  };

  const cleanQuantity = (input) => {
    const text = String(input.value ?? "").replace(/[^\d.,]/g, "");
    const separatorMatches = Array.from(text.matchAll(/[.,]/g));
    const decimalIndex = separatorMatches.length > 1
      ? separatorMatches[separatorMatches.length - 1].index
      : (separatorMatches[0]?.index ?? -1);

    input.value = decimalIndex >= 0
      ? `${text.slice(0, decimalIndex).replace(/[.,]/g, "").slice(0, 7)}.${text.slice(decimalIndex + 1).replace(/[.,]/g, "").slice(0, 3)}`
      : text.replace(/[.,]/g, "").slice(0, 7);
  };

  const cleanPackages = (input) => {
    input.value = String(input.value ?? "").replace(/\D/g, "").slice(0, 7);
  };

  const selectedRow = () => table.querySelector("[data-initial-stock-row].selected-row");
  const visibleRows = () => rows.filter((row) => !row.hidden);

  const dataKey = (key) => `sort${key[0].toUpperCase()}${key.slice(1)}`;

  const sortValue = (row, key, type) => {
    if (type === "number") {
      return Number.parseFloat(row.dataset[dataKey(key)] ?? "0") || 0;
    }

    return normalize(row.dataset[dataKey(key)] ?? "");
  };

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

  const focusField = (row, selector = packagesSelector, direction = 0) => {
    const input = row?.querySelector(selector);
    if (!input) {
      return;
    }

    selectRow(row, direction);
    input.focus({ preventScroll: true });
    input.select();
  };

  const gridViewport = () => {
    const gridRect = grid.getBoundingClientRect();
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

    selectRow(delta > 0 ? currentRows[0] : currentRows[currentRows.length - 1]);
  };

  const updateRowQuantitySort = (input) => {
    const row = input.closest("[data-initial-stock-row]");
    if (row) {
      row.dataset.sortQuantity = String(parseDecimal(input.value));
    }
  };

  const updateRowPackagesSort = (input) => {
    const row = input.closest("[data-initial-stock-row]");
    if (row) {
      row.dataset.sortPackages = String(Number.parseInt(input.value, 10) || 0);
    }
  };

  const commitQuantity = (input) => {
    cleanQuantity(input);
    input.value = formatQuantity(parseDecimal(input.value));
    updateRowQuantitySort(input);
  };

  const applySort = (key, direction, type = "text") => {
    const selectedCode = selectedRow()?.dataset.code ?? "";
    const multiplier = direction === "desc" ? -1 : 1;

    [...rows]
      .sort((left, right) => {
        const leftValue = sortValue(left, key, type);
        const rightValue = sortValue(right, key, type);

        if (leftValue < rightValue) {
          return -1 * multiplier;
        }

        if (leftValue > rightValue) {
          return 1 * multiplier;
        }

        return 0;
      })
      .forEach((row) => tableBody.appendChild(row));

    sortHeaders.forEach((header) => {
      const active = header.dataset.sortKey === key;
      header.classList.toggle("is-sorted", active);
      header.dataset.sortDirection = active ? direction : "";
      header.setAttribute("aria-sort", active ? (direction === "asc" ? "ascending" : "descending") : "none");
    });

    selectRow(rows.find((row) => row.dataset.code === selectedCode) ?? visibleRows()[0]);
  };

  const sortByHeader = (header) => {
    const key = header.dataset.sortKey;
    if (!key) {
      return;
    }

    currentSortDirection = currentSortKey === key && currentSortDirection === "asc" ? "desc" : "asc";
    currentSortKey = key;
    applySort(key, currentSortDirection, header.dataset.sortType);
  };

  const moveSelection = (row, offset, selector) => {
    const currentRows = visibleRows();
    const index = currentRows.indexOf(row);
    if (index < 0) {
      return false;
    }

    const nextIndex = Math.max(Math.min(index + offset, currentRows.length - 1), 0);
    const direction = Math.sign(offset);
    focusField(currentRows[nextIndex], selector, direction);
    return true;
  };

  const moveFocusLikeTab = (current, backwards = false) => {
    const controls = Array.from(form.querySelectorAll(
      "input:not([type='hidden']):not([disabled]), button:not([disabled]), select:not([disabled]), textarea:not([disabled]), a[href]"
    )).filter((control) => control.tabIndex >= 0 && control.getClientRects().length > 0);
    const index = controls.indexOf(current);
    const nextIndex = index + (backwards ? -1 : 1);

    if (index < 0 || nextIndex < 0 || nextIndex >= controls.length) {
      return false;
    }

    const next = controls[nextIndex];
    next.focus({ preventScroll: true });
    next.select?.();
    next.closest("[data-initial-stock-row]") && selectRow(
      next.closest("[data-initial-stock-row]"),
      backwards ? -1 : 1
    );
    return true;
  };

  const navigateRows = (event, row, selector = packagesSelector) => {
    if (!visibleRows().includes(row)) {
      return false;
    }

    if (event.key === "ArrowDown") {
      event.preventDefault();
      moveSelection(row, 1, selector);
      return true;
    }

    if (event.key === "ArrowUp") {
      event.preventDefault();
      moveSelection(row, -1, selector);
      return true;
    }

    if (event.key === "PageDown") {
      event.preventDefault();
      const visibleCount = Math.max(Math.floor((grid.clientHeight - (table.tHead?.offsetHeight ?? 0)) / (row.offsetHeight || 27)) - 1, 1);
      moveSelection(row, visibleCount, selector);
      return true;
    }

    if (event.key === "PageUp") {
      event.preventDefault();
      const visibleCount = Math.max(Math.floor((grid.clientHeight - (table.tHead?.offsetHeight ?? 0)) / (row.offsetHeight || 27)) - 1, 1);
      moveSelection(row, -visibleCount, selector);
      return true;
    }

    if (event.key === "Home") {
      event.preventDefault();
      focusField(visibleRows()[0], selector, -1);
      return true;
    }

    if (event.key === "End") {
      event.preventDefault();
      const currentRows = visibleRows();
      focusField(currentRows[currentRows.length - 1], selector, 1);
      return true;
    }

    return false;
  };

  const buildPayload = () => {
    payload.value = JSON.stringify(rows.map((row) => ({
      code: row.dataset.code ?? "",
      packages: Number.parseInt(row.querySelector(packagesSelector)?.value, 10) || 0,
      quantity: parseDecimal(row.querySelector("[data-initial-stock-quantity]")?.value)
    })));
  };

  const confirmExit = (url) => {
    if (!modified) {
      window.location.href = url;
      return;
    }

    window.MicronoteMessageBox?.show({
      title: "Giacenza iniziale articoli",
      message: "Uscire senza registrare le modifiche?",
      mode: "confirm",
      okText: "Esci",
      onConfirm: () => {
        window.location.href = url;
      }
    });
  };

  rows.forEach((row) => {
    const packagesInput = row.querySelector(packagesSelector);
    const quantityInput = row.querySelector(quantitySelector);

    row.addEventListener("click", (event) => {
      if (event.target === packagesInput) {
        focusField(row, packagesSelector);
        return;
      }

      if (event.target === quantityInput) {
        focusField(row, quantitySelector);
        return;
      }

      focusField(row, packagesSelector);
    });

    packagesInput?.addEventListener("focus", () => {
      selectRow(row);
      packagesInput.select();
    });
    packagesInput?.addEventListener("input", () => {
      cleanPackages(packagesInput);
      updateRowPackagesSort(packagesInput);
      modified = true;
    });
    packagesInput?.addEventListener("keydown", (event) => {
      if (event.key === "Enter") {
        event.preventDefault();
        event.stopPropagation();
        moveFocusLikeTab(packagesInput, event.shiftKey);
        return;
      }

      if (event.key === "Delete") {
        event.preventDefault();
        event.stopPropagation();
        packagesInput.value = "";
        updateRowPackagesSort(packagesInput);
        modified = true;
        return;
      }

      if (navigateRows(event, row, packagesSelector)) {
        event.stopPropagation();
      }
    });

    quantityInput?.addEventListener("focus", () => {
      selectRow(row);
      quantityInput.value = formatForEdit(parseDecimal(quantityInput.value));
      quantityInput.select();
    });
    quantityInput?.addEventListener("input", () => {
      cleanQuantity(quantityInput);
      updateRowQuantitySort(quantityInput);
      modified = true;
    });
    quantityInput?.addEventListener("blur", () => {
      commitQuantity(quantityInput);
    });
    quantityInput?.addEventListener("keydown", (event) => {
      if (event.key === "Enter") {
        event.preventDefault();
        event.stopPropagation();
        commitQuantity(quantityInput);
        moveFocusLikeTab(quantityInput, event.shiftKey);
        return;
      }

      if (event.key === "Delete") {
        event.preventDefault();
        event.stopPropagation();
        quantityInput.value = "";
        updateRowQuantitySort(quantityInput);
        modified = true;
        return;
      }

      if (navigateRows(event, row, quantitySelector)) {
        event.stopPropagation();
      }
    });
  });

  sortHeaders.forEach((header) => {
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

  let lastScrollTop = grid.scrollTop;
  let scrollFrame = 0;
  let wheelFrame = 0;

  grid.addEventListener("scroll", () => {
    if (scrollFrame) {
      window.cancelAnimationFrame(scrollFrame);
    }

    scrollFrame = window.requestAnimationFrame(() => {
      scrollFrame = 0;
      const currentScrollTop = grid.scrollTop;
      const delta = currentScrollTop - lastScrollTop;
      lastScrollTop = currentScrollTop;

      selectVisibleRowFromScroll(delta);
    });
  }, { passive: true });

  grid.addEventListener("wheel", (event) => {
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

  form.addEventListener("submit", () => {
    buildPayload();
    modified = false;
  });

  inventoryDate?.addEventListener("change", () => {
    modified = true;
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

  if (rows.length > 0) {
    focusField(rows[0]);
  }
});
