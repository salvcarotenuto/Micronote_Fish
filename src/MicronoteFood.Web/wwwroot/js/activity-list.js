document.addEventListener("DOMContentLoaded", () => {
  const page = document.querySelector(".activity-list-page");
  const grid = page?.querySelector("[data-activity-grid]");
  const table = grid?.querySelector("table");
  const body = table?.querySelector("tbody");
  const rows = Array.from(table?.querySelectorAll("[data-activity-row]") ?? []);
  const headers = Array.from(table?.querySelectorAll("[data-sort-key]") ?? []);
  const empty = page?.querySelector("[data-activity-empty]");
  const printButton = page?.querySelector("[data-activity-action='print']");
  const preview = page?.querySelector("[data-activity-print-preview]");
  const previewDocument = preview?.querySelector("[data-activity-preview-document]");
  const previewSummary = preview?.querySelector("[data-activity-print-summary]");
  const previewZoomLabel = preview?.querySelector("[data-activity-preview-zoom-label]");
  let currentSortKey = "";
  let currentSortDirection = "asc";
  let previewZoom = 0.8;

  if (!page || !grid || !table || !body) {
    return;
  }

  const normalize = (value) =>
    String(value ?? "")
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .toLowerCase();

  const selectedRow = () => table.querySelector(".selected-row");

  const updatePreviewZoom = () => {
    previewDocument?.style.setProperty("--report-preview-zoom", String(previewZoom));
    if (previewZoomLabel) {
      previewZoomLabel.textContent = `${Math.round(previewZoom * 100)}%`;
    }
  };

  const closePrintPreview = () => {
    if (preview) {
      preview.hidden = true;
    }
  };

  const appendPrintPage = (columnHeaders, printRows, pageNumber, pageCount) => {
    const sheet = document.createElement("article");
    sheet.className = "supplier-report-page activity-report-page";
    const reportHeader = document.createElement("header");
    reportHeader.innerHTML = `<div><strong>LISTA ATTIVITÀ UTENTI</strong><span>Elenco secondo i filtri e l'ordinamento applicati</span></div><small>data di stampa: ${new Intl.DateTimeFormat("it-IT").format(new Date())}<br>Pagina ${pageNumber} di ${pageCount}</small>`;
    sheet.appendChild(reportHeader);

    const reportTable = document.createElement("table");
    const headRow = reportTable.createTHead().insertRow();
    columnHeaders.forEach((value) => {
      const cell = document.createElement("th");
      cell.textContent = value;
      headRow.appendChild(cell);
    });

    const reportBody = reportTable.createTBody();
    printRows.forEach((values) => {
      const row = reportBody.insertRow();
      values.forEach((value) => {
        row.insertCell().textContent = value;
      });
    });
    sheet.appendChild(reportTable);
    previewDocument.appendChild(sheet);
  };

  const openPrintPreview = () => {
    if (!preview || !previewDocument) {
      return;
    }

    const printRows = Array.from(body.rows)
      .filter((row) => !row.hidden)
      .map((row) => Array.from(row.cells).map((cell) => cell.textContent.trim()));
    if (!printRows.length) {
      window.MicronoteMessageBox?.show({
        title: "Stampa",
        message: "Nessuna attività da stampare."
      });
      return;
    }

    const columnHeaders = Array.from(table.querySelectorAll("thead th"))
      .map((cell) => cell.textContent.trim());
    const rowsPerPage = 28;
    const pageCount = Math.ceil(printRows.length / rowsPerPage);
    previewDocument.replaceChildren();
    for (let index = 0; index < pageCount; index += 1) {
      appendPrintPage(
        columnHeaders,
        printRows.slice(index * rowsPerPage, (index + 1) * rowsPerPage),
        index + 1,
        pageCount
      );
    }

    if (previewSummary) {
      previewSummary.textContent = `${printRows.length} record · ${pageCount} pagine`;
    }
    updatePreviewZoom();
    preview.hidden = false;
    preview.querySelector("[data-activity-preview-close]")?.focus();
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

  const selectRow = (row, focus = false, direction = 0) => {
    if (!row) {
      return;
    }

    rows.forEach((candidate) => {
      candidate.classList.remove("selected-row");
      candidate.removeAttribute("aria-selected");
    });
    row.classList.add("selected-row");
    row.setAttribute("aria-selected", "true");
    if (focus) {
      row.focus({ preventScroll: true });
    }
    ensureVisible(row, direction);
  };

  const sortValue = (row, key, type) => {
    const value = row.dataset[`sort${key[0].toUpperCase()}${key.slice(1)}`] ?? "";
    return type === "number" ? Number.parseFloat(value) || 0 : normalize(value);
  };

  const sortBy = (header) => {
    const key = header.dataset.sortKey;
    if (!key) {
      return;
    }

    currentSortDirection = currentSortKey === key && currentSortDirection === "asc" ? "desc" : "asc";
    currentSortKey = key;

    [...rows]
      .sort((left, right) => {
        const leftValue = sortValue(left, key, header.dataset.sortType);
        const rightValue = sortValue(right, key, header.dataset.sortType);
        let result = 0;
        if (leftValue < rightValue) result = -1;
        if (leftValue > rightValue) result = 1;
        return currentSortDirection === "desc" ? -result : result;
      })
      .forEach((row) => body.appendChild(row));

    headers.forEach((candidate) => {
      const active = candidate === header;
      candidate.classList.toggle("is-sorted", active);
      candidate.dataset.sortDirection = active ? currentSortDirection : "";
      candidate.setAttribute(
        "aria-sort",
        active ? (currentSortDirection === "asc" ? "ascending" : "descending") : "none"
      );
    });

    selectRow(rows[0]);
  };

  const navigate = (event, row) => {
    const index = Math.max(rows.indexOf(row), 0);
    if (event.key === "ArrowDown") {
      event.preventDefault();
      selectRow(rows[Math.min(index + 1, rows.length - 1)], true, 1);
    } else if (event.key === "ArrowUp") {
      event.preventDefault();
      selectRow(rows[Math.max(index - 1, 0)], true, -1);
    } else if (event.key === "Home") {
      event.preventDefault();
      selectRow(rows[0], true, -1);
    } else if (event.key === "End") {
      event.preventDefault();
      selectRow(rows[rows.length - 1], true, 1);
    }
  };

  rows.forEach((row) => {
    row.addEventListener("click", () => selectRow(row, true));
    row.addEventListener("keydown", (event) => navigate(event, selectedRow() || row));
  });

  headers.forEach((header) => {
    header.tabIndex = 0;
    header.setAttribute("role", "button");
    header.setAttribute("aria-sort", "none");
    header.addEventListener("click", () => sortBy(header));
    header.addEventListener("keydown", (event) => {
      if (event.key !== "Enter" && event.key !== " ") {
        return;
      }
      event.preventDefault();
      sortBy(header);
    });
  });

  page.querySelectorAll("[data-activity-filter]").forEach((field) => {
    field.addEventListener("change", () => field.form?.requestSubmit());
  });

  printButton?.addEventListener("click", openPrintPreview);
  preview?.querySelector("[data-activity-preview-close]")?.addEventListener("click", closePrintPreview);
  preview?.querySelector("[data-activity-preview-zoom-out]")?.addEventListener("click", () => {
    previewZoom = Math.max(0.45, previewZoom - 0.1);
    updatePreviewZoom();
  });
  preview?.querySelector("[data-activity-preview-zoom-in]")?.addEventListener("click", () => {
    previewZoom = Math.min(1.4, previewZoom + 0.1);
    updatePreviewZoom();
  });
  preview?.querySelector("[data-activity-preview-print]")?.addEventListener("click", () => {
    const style = document.createElement("style");
    style.id = "activity-print-page-style";
    style.textContent = "@page { size: A4 landscape; margin: 0; }";
    document.head.appendChild(style);
    document.body.classList.add("is-printing-supplier-report");
    window.print();
  });
  window.addEventListener("afterprint", () => {
    document.body.classList.remove("is-printing-supplier-report");
    document.querySelector("#activity-print-page-style")?.remove();
  });

  document.addEventListener("keydown", (event) => {
    if (event.key !== "Escape" || event.defaultPrevented) {
      return;
    }
    event.preventDefault();
    if (preview && !preview.hidden) {
      closePrintPreview();
      return;
    }
    window.location.href = page.dataset.menuUrl || "/";
  });

  if (empty) {
    empty.hidden = rows.length !== 0;
  }
  window.setTimeout(() => selectRow(rows[0]), 0);
});
