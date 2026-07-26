document.addEventListener("DOMContentLoaded", () => {
  const filterForm = document.querySelector("[data-purchase-invoice-filters]");
  const grid = document.querySelector(".purchase-invoices-grid-frame");
  const table = grid?.querySelector("table");
  const tableBody = table?.querySelector("tbody");
  const rows = Array.from(table?.querySelectorAll("[data-purchase-invoice-row]") ?? []);
  const sortHeaders = Array.from(table?.querySelectorAll("[data-sort-key]") ?? []);
  const actionButtons = Array.from(document.querySelectorAll("[data-purchase-invoice-action]"));
  const deleteForm = document.querySelector("[data-purchase-invoice-delete-form]");
  const deleteId = deleteForm?.querySelector("[data-purchase-invoice-delete-id]");
  const preview = document.querySelector("[data-purchase-invoices-print-preview]");
  const previewDocument = preview?.querySelector("[data-purchase-invoices-preview-document]");
  const previewSummary = preview?.querySelector("[data-purchase-invoices-print-summary]");
  const previewZoomLabel = preview?.querySelector("[data-purchase-invoices-preview-zoom-label]");
  let previewZoom = 0.8;
  let filterTimer;
  let currentSortKey = "";
  let currentSortDirection = "asc";

  const submitFilters = () => {
    if (!filterForm) {
      return;
    }

    window.clearTimeout(filterTimer);
    filterTimer = window.setTimeout(() => {
      filterForm.requestSubmit();
    }, 150);
  };

  filterForm?.querySelectorAll("input[name], select[name]").forEach((field) => {
    field.addEventListener("change", submitFilters);
  });

  filterForm?.querySelector("input[name='supplierCode']")?.addEventListener("keydown", (event) => {
    if (event.key === "Enter") {
      event.preventDefault();
      submitFilters();
    }
  });

  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape") {
      if (document.body.classList.contains("lookup-open")) {
        return;
      }

      event.preventDefault();
      if (preview && !preview.hidden) {
        closePrintPreview();
        return;
      }
      window.location.href = "/";
    }
  });

  const visibleRows = () => rows.filter((row) => !row.hidden);
  const selectedRow = () => table?.querySelector("[data-purchase-invoice-row].selected");

  const updatePreviewZoom = () => {
    previewDocument?.style.setProperty("--report-preview-zoom", String(previewZoom));
    if (previewZoomLabel) previewZoomLabel.textContent = `${Math.round(previewZoom * 100)}%`;
  };

  const closePrintPreview = () => {
    if (preview) preview.hidden = true;
  };

  const appendPrintPage = (headers, printRows, number, count) => {
    const sheet = document.createElement("article");
    sheet.className = "supplier-report-page purchase-invoices-report-page";
    const header = document.createElement("header");
    header.innerHTML = `<div><strong>LISTA FATTURE DI ACQUISTO</strong><span>Elenco secondo i filtri e l'ordinamento applicati</span></div><small>data di stampa: ${new Intl.DateTimeFormat("it-IT").format(new Date())}<br>Pagina ${number} di ${count}</small>`;
    sheet.appendChild(header);
    const reportTable = document.createElement("table");
    const headRow = reportTable.createTHead().insertRow();
    headers.forEach((value) => { const cell = document.createElement("th"); cell.textContent = value; headRow.appendChild(cell); });
    const body = reportTable.createTBody();
    printRows.forEach((values) => {
      const row = body.insertRow();
      values.forEach((value) => { row.insertCell().textContent = value; });
    });
    sheet.appendChild(reportTable);
    previewDocument.appendChild(sheet);
  };

  const openPrintPreview = () => {
    if (!preview || !previewDocument || !table) return;
    const printRows = visibleRows().map((row) => Array.from(row.cells).map((cell) => cell.textContent.trim()));
    if (!printRows.length) {
      window.MicronoteMessageBox?.show({ title: "Stampa", message: "Nessuna fattura da stampare." });
      return;
    }
    const headers = Array.from(table.querySelectorAll("thead th")).map((cell) => cell.textContent.trim());
    const rowsPerPage = 25;
    const pageCount = Math.ceil(printRows.length / rowsPerPage);
    previewDocument.replaceChildren();
    for (let index = 0; index < pageCount; index += 1) {
      appendPrintPage(headers, printRows.slice(index * rowsPerPage, (index + 1) * rowsPerPage), index + 1, pageCount);
    }
    if (previewSummary) previewSummary.textContent = `${printRows.length} record · ${pageCount} pagine`;
    updatePreviewZoom();
    preview.hidden = false;
    preview.querySelector("[data-purchase-invoices-preview-close]")?.focus();
  };
  const currentReturnUrl = () => window.location.pathname + window.location.search;
  const editUrl = (id = "") => {
    const url = new URL("/FattureAcquisto/Edit", window.location.origin);
    if (id) {
      url.searchParams.set("id", id);
    }

    url.searchParams.set("returnUrl", currentReturnUrl());
    return url.pathname + url.search;
  };

  const normalize = (value) => (value || "")
    .toString()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLocaleLowerCase("it-IT")
    .trim();

  const sortValue = (row, key, type) => {
    const datasetKey = `sort${key[0].toUpperCase()}${key.slice(1)}`;
    const value = row.dataset[datasetKey] ?? "";
    return type === "number" ? Number.parseFloat(value) || 0 : normalize(value);
  };

  const applySort = (key, direction, type = "text") => {
    if (!tableBody) {
      return;
    }

    const selected = selectedRow();
    const selectedId = selected?.dataset.invoiceId || "";
    const multiplier = direction === "desc" ? -1 : 1;
    const sortedRows = [...rows].sort((left, right) => {
      const leftValue = sortValue(left, key, type);
      const rightValue = sortValue(right, key, type);

      if (leftValue < rightValue) {
        return -1 * multiplier;
      }

      if (leftValue > rightValue) {
        return 1 * multiplier;
      }

      return 0;
    });

    sortedRows.forEach((row) => tableBody.appendChild(row));

    sortHeaders.forEach((header) => {
      const isActive = header.dataset.sortKey === key;
      header.classList.toggle("is-sorted", isActive);
      header.dataset.sortDirection = isActive ? direction : "";
      header.setAttribute("aria-sort", isActive ? (direction === "asc" ? "ascending" : "descending") : "none");
    });

    const rowToSelect = rows.find((row) => row.dataset.invoiceId === selectedId) || visibleRows()[0];
    if (rowToSelect) {
      selectRow(rowToSelect, true, 0);
      grid.scrollTop = 0;
    }
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

  const requireSelection = () => {
    const row = selectedRow();
    if (row) {
      return row;
    }

    window.MicronoteMessageBox?.show({
      title: "Fatture di acquisto",
      message: "Selezionare una fattura dalla lista."
    });
    return null;
  };

  const selectedLabel = (row) => {
    const cells = Array.from(row?.querySelectorAll("td") ?? []);
    const number = cells[0]?.textContent?.trim() || "";
    const date = cells[2]?.textContent?.trim() || "";
    return [number, date].filter(Boolean).join(" - ");
  };

  const ensureVisible = (row, direction = 0) => {
    if (!grid || !table || !row) {
      return;
    }

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

  const selectRow = (row, focus = false, direction = 0) => {
    if (!row || row.hidden) {
      return;
    }

    rows.forEach((candidate) => {
      candidate.classList.remove("selected", "selected-row");
      candidate.removeAttribute("aria-selected");
    });

    row.classList.add("selected", "selected-row");
    row.setAttribute("aria-selected", "true");

    if (focus) {
      row.focus({ preventScroll: true });
    }

    ensureVisible(row, direction);
  };

  const navigateGrid = (event, currentRow) => {
    const currentRows = visibleRows();
    const currentIndex = currentRows.indexOf(currentRow);

    if (currentIndex < 0) {
      return;
    }

    if (event.key === "ArrowDown") {
      event.preventDefault();
      selectRow(currentRows[Math.min(currentIndex + 1, currentRows.length - 1)], true, 1);
      return;
    }

    if (event.key === "ArrowUp") {
      event.preventDefault();
      selectRow(currentRows[Math.max(currentIndex - 1, 0)], true, -1);
      return;
    }

    if (event.key === "Home") {
      event.preventDefault();
      selectRow(currentRows[0], true, -1);
      return;
    }

    if (event.key === "End") {
      event.preventDefault();
      selectRow(currentRows[currentRows.length - 1], true, 1);
    }
  };

  rows.forEach((row) => {
    row.addEventListener("click", () => selectRow(row, true, 0));
    row.addEventListener("dblclick", () => {
      window.location.href = editUrl(row.dataset.invoiceId || "");
    });
    row.addEventListener("keydown", (event) => {
      if (event.key === "Enter") {
        event.preventDefault();
        selectRow(row, true, 0);
        return;
      }

      navigateGrid(event, selectedRow() || row);
    });
  });

  sortHeaders.forEach((header) => {
    header.tabIndex = 0;
    header.setAttribute("role", "button");
    header.setAttribute("aria-sort", "none");
    header.addEventListener("click", () => sortByHeader(header));
    header.addEventListener("keydown", (event) => {
      if (event.key === "Enter" || event.key === " ") {
        event.preventDefault();
        sortByHeader(header);
      }
    });
  });

  actionButtons.forEach((button) => {
    button.addEventListener("click", () => {
      const action = button.dataset.purchaseInvoiceAction;
      if (action === "new") {
        window.location.href = editUrl();
        return;
      }

      if (action === "print") {
        openPrintPreview();
        return;
      }

      const row = requireSelection();
      if (!row) {
        return;
      }

      if (action === "edit") {
        window.location.href = editUrl(row.dataset.invoiceId || "");
        return;
      }

      const label = selectedLabel(row);
      if (action === "delete") {
        window.MicronoteMessageBox?.show({
          mode: "confirm",
          variant: "confirm",
          title: "Cancella fattura",
          message: `Cancellare la fattura ${label}?`,
          detail: "Saranno cancellati anche righe IVA, scadenze e movimento contabile collegato.",
          okText: "Cancella",
          onConfirm: () => {
            if (deleteForm && deleteId) {
              deleteId.value = row.dataset.invoiceId || "";
              deleteForm.submit();
            }
          }
        });
        return;
      }

      const titles = {
        edit: "Modifica fattura",
        view: "Visualizza fattura"
      };

      window.MicronoteMessageBox?.show({
        title: titles[action] || "Fatture di acquisto",
        message: `${titles[action] || "Operazione"} per ${label}.`,
        detail: "Funzione in preparazione."
      });
    });
  });

  preview?.querySelector("[data-purchase-invoices-preview-close]")?.addEventListener("click", closePrintPreview);
  preview?.querySelector("[data-purchase-invoices-preview-zoom-out]")?.addEventListener("click", () => {
    previewZoom = Math.max(0.45, previewZoom - 0.1);
    updatePreviewZoom();
  });
  preview?.querySelector("[data-purchase-invoices-preview-zoom-in]")?.addEventListener("click", () => {
    previewZoom = Math.min(1.4, previewZoom + 0.1);
    updatePreviewZoom();
  });
  preview?.querySelector("[data-purchase-invoices-preview-print]")?.addEventListener("click", () => {
    const style = document.createElement("style");
    style.id = "purchase-invoices-print-page-style";
    style.textContent = "@page { size: A4 landscape; margin: 0; }";
    document.head.appendChild(style);
    document.body.classList.add("is-printing-supplier-report");
    window.print();
  });
  window.addEventListener("afterprint", () => {
    document.body.classList.remove("is-printing-supplier-report");
    document.querySelector("#purchase-invoices-print-page-style")?.remove();
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
  }
});

