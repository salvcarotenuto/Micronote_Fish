document.addEventListener("DOMContentLoaded", () => {
  const page = document.querySelector("[data-store-summary]");
  const grids = Array.from(document.querySelectorAll(".store-summary-grid-frame"));
  const actionButtons = Array.from(document.querySelectorAll("[data-store-summary-action]"));
  const preview = page?.querySelector("[data-store-summary-print-preview]");
  const previewDocument = preview?.querySelector("[data-store-summary-preview-document]");
  const previewSummary = preview?.querySelector("[data-store-summary-print-summary]");
  const previewZoomLabel = preview?.querySelector("[data-store-summary-preview-zoom-label]");
  let previewZoom = 0.75;

  if (!page || grids.length === 0) {
    return;
  }

  const rowsInGrid = (grid) =>
    Array.from(grid.querySelectorAll("[data-store-summary-row]"));

  const selectedRow = () =>
    page.querySelector("[data-store-summary-row].selected-row");

  const visibleRows = (grid) =>
    rowsInGrid(grid).filter((row) => !row.hidden);

  const ensureVisible = (grid, table, row, direction = 0) => {
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

  const selectRow = (grid, row, focus = false, direction = 0) => {
    const table = grid?.querySelector("table");
    if (!grid || !table || !row || row.hidden) {
      return;
    }

    const previous = selectedRow();
    if (previous !== row) {
      page.querySelectorAll("[data-store-summary-row]").forEach((candidate) => {
        candidate.classList.remove("selected", "selected-row");
        candidate.removeAttribute("aria-selected");
      });
      row.classList.add("selected", "selected-row");
      row.setAttribute("aria-selected", "true");
    }

    if (focus) {
      row.focus({ preventScroll: true });
    }
    ensureVisible(grid, table, row, direction);
  };

  const navigateGrid = (event, grid, row) => {
    const rows = visibleRows(grid);
    if (rows.length === 0) {
      return false;
    }

    const currentIndex = Math.max(rows.indexOf(row), 0);
    if (event.key === "ArrowDown") {
      event.preventDefault();
      selectRow(grid, rows[Math.min(currentIndex + 1, rows.length - 1)], true, 1);
    } else if (event.key === "ArrowUp") {
      event.preventDefault();
      selectRow(grid, rows[Math.max(currentIndex - 1, 0)], true, -1);
    } else if (event.key === "Home") {
      event.preventDefault();
      selectRow(grid, rows[0], true, -1);
    } else if (event.key === "End") {
      event.preventDefault();
      selectRow(grid, rows[rows.length - 1], true, 1);
    } else {
      return false;
    }

    return true;
  };

  const gridViewport = (grid, table) => {
    const gridRect = grid.getBoundingClientRect();
    const headerHeight = table?.tHead?.getBoundingClientRect().height ?? 0;
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

  const fullyVisibleRows = (grid, viewport) =>
    visibleRows(grid).filter((row) => {
      const rowRect = row.getBoundingClientRect();
      return rowRect.top >= viewport.top + 1 &&
        rowRect.bottom <= viewport.bottom - 1;
    });

  const selectVisibleRowFromScroll = (grid, table, delta) => {
    if (delta === 0) {
      return;
    }

    const selected = selectedRow();
    if (!selected || selected.closest(".store-summary-grid-frame") !== grid) {
      return;
    }

    const viewport = gridViewport(grid, table);
    if (isRowVisible(selected, viewport)) {
      return;
    }

    const rows = fullyVisibleRows(grid, viewport);
    if (rows.length > 0) {
      selectRow(grid, delta > 0 ? rows[0] : rows[rows.length - 1]);
    }
  };

  grids.forEach((grid) => {
    const table = grid.querySelector("table");
    if (!table) {
      return;
    }

    rowsInGrid(grid).forEach((row) => {
      row.addEventListener("click", () => selectRow(grid, row, true));
      row.addEventListener("keydown", (event) => {
        navigateGrid(event, grid, selectedRow() || row);
      });
    });

    grid.addEventListener("keydown", (event) => {
      if (event.target.closest("[data-store-summary-row]")) {
        return;
      }

      const row = selectedRow() || visibleRows(grid)[0];
      if (row) {
        navigateGrid(event, grid, row);
      }
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
        selectVisibleRowFromScroll(grid, table, delta);
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
          selectVisibleRowFromScroll(grid, table, delta);
        });
      });
    }, { passive: true });
  });

  const firstCostRow = page.querySelector("[data-store-summary-grid='costs'][data-account-code]:not([data-account-code=''])");
  if (firstCostRow) {
    selectRow(firstCostRow.closest(".store-summary-grid-frame"), firstCostRow);
  }

  const requireCostAccount = () => {
    const row = selectedRow();
    if (row?.dataset.storeSummaryGrid === "costs" && row.dataset.accountCode) {
      return row;
    }

    window.MicronoteMessageBox?.show({
      title: "Riepilogo movimenti per punto vendita",
      message: "Selezionare un conto di costo dalla griglia Costi."
    });
    return null;
  };

  const sectionData = () => Array.from(page.querySelectorAll(".store-summary-panel")).map((panel) => {
    const table = panel.querySelector("table");
    return {
      title: panel.querySelector("h2")?.textContent.trim() ?? "",
      headers: Array.from(table?.querySelectorAll("thead th") ?? []).map((cell) => cell.textContent.trim()).filter((_, index) => index < 4 || index % 4 !== 0),
      rows: Array.from(table?.querySelectorAll("tbody tr:not(.store-summary-separator)") ?? []).map((row) =>
        Array.from(row.cells).map((cell) => cell.textContent.trim()).filter((_, index) => index < 4 || index % 4 !== 0))
    };
  });

  const updatePreviewZoom = () => {
    previewDocument?.style.setProperty("--report-preview-zoom", String(previewZoom));
    if (previewZoomLabel) previewZoomLabel.textContent = `${Math.round(previewZoom * 100)}%`;
  };

  const closePreview = () => { if (preview) preview.hidden = true; };

  const appendReportPage = (section, rows, pageNumber, pageCount) => {
    const sheet = document.createElement("article");
    sheet.className = "supplier-report-page store-summary-report-page";
    const header = document.createElement("header");
    header.innerHTML = `<div><strong>RIEPILOGO MOVIMENTI CONTABILI PER PUNTO VENDITA</strong><span>Esercizio contabile: ${page.dataset.year} · ${section.title}</span></div><small>data di stampa: ${new Intl.DateTimeFormat("it-IT").format(new Date())}<br>Pagina ${pageNumber} di ${pageCount}</small>`;
    sheet.appendChild(header);
    const table = document.createElement("table");
    const head = table.createTHead().insertRow();
    section.headers.forEach((value) => { const cell = document.createElement("th"); cell.textContent = value; head.appendChild(cell); });
    const body = table.createTBody();
    rows.forEach((values) => {
      const row = body.insertRow();
      values.forEach((value) => { row.insertCell().textContent = value; });
    });
    sheet.appendChild(table);
    previewDocument.appendChild(sheet);
  };

  const buildPreview = () => {
    if (!preview || !previewDocument) return;
    const sections = sectionData();
    if (!sections.some((section) => section.rows.length)) {
      window.MicronoteMessageBox?.show({ message: "Elaborare il riepilogo prima della stampa.", variant: "info" });
      return;
    }
    previewDocument.replaceChildren();
    const prepared = sections.map((section) => ({ section, pages: Math.max(1, Math.ceil(section.rows.length / 31)) }));
    const totalPages = prepared.reduce((sum, item) => sum + item.pages, 0);
    let number = 0;
    prepared.forEach(({ section, pages }) => {
      for (let index = 0; index < pages; index += 1) {
        appendReportPage(section, section.rows.slice(index * 31, (index + 1) * 31), ++number, totalPages);
      }
    });
    if (previewSummary) previewSummary.textContent = `${page.querySelectorAll("[data-store-summary-row]").length} record · ${totalPages} pagine`;
    updatePreviewZoom();
    preview.hidden = false;
    preview.querySelector("[data-store-summary-preview-close]")?.focus();
  };

  const downloadPdf = async () => {
    const token = page.querySelector('input[name="__RequestVerificationToken"]')?.value;
    const button = preview?.querySelector("[data-store-summary-preview-pdf]");
    if (!token || !button) return;
    button.disabled = true;
    try {
      const response = await fetch("?handler=Pdf", {
        method: "POST",
        headers: { "Content-Type": "application/json", "RequestVerificationToken": token },
        body: JSON.stringify({ year: Number.parseInt(page.dataset.year, 10) })
      });
      if (!response.ok) throw new Error("Creazione PDF non riuscita.");
      const url = URL.createObjectURL(await response.blob());
      const link = document.createElement("a");
      link.href = url; link.download = `RiepilogoPV_${page.dataset.year}.pdf`; link.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      window.MicronoteMessageBox?.show({ message: error.message, variant: "error" });
    } finally { button.disabled = false; }
  };

  const downloadExcel = () => {
    const quote = (value) => `"${String(value).replaceAll('"', '""')}"`;
    const lines = [["RIEPILOGO MOVIMENTI CONTABILI PER PUNTO VENDITA", `Esercizio ${page.dataset.year}`]];
    sectionData().forEach((section) => lines.push([], [section.title], section.headers, ...section.rows));
    const csv = `\uFEFF${lines.map((line) => line.map(quote).join(";")).join("\r\n")}`;
    const url = URL.createObjectURL(new Blob([csv], { type: "text/csv;charset=utf-8" }));
    const link = document.createElement("a"); link.href = url; link.download = `RiepilogoPV_${page.dataset.year}.csv`; link.click();
    URL.revokeObjectURL(url);
  };

  actionButtons.forEach((button) => {
    button.addEventListener("click", () => {
      const action = button.dataset.storeSummaryAction;
      if (action === "print") {
        buildPreview();
        return;
      }

      const row = requireCostAccount();
      if (!row) {
        return;
      }

      window.MicronoteMessageBox?.show({
        title: "Vedi e/conto",
        message: `${row.dataset.accountCode} - ${row.dataset.accountDescription}`,
        detail: "La scheda contabile generale non e' ancora disponibile nel web."
      });
    });
  });

  preview?.querySelector("[data-store-summary-preview-close]")?.addEventListener("click", closePreview);
  preview?.querySelector("[data-store-summary-preview-zoom-out]")?.addEventListener("click", () => { previewZoom = Math.max(0.45, previewZoom - 0.1); updatePreviewZoom(); });
  preview?.querySelector("[data-store-summary-preview-zoom-in]")?.addEventListener("click", () => { previewZoom = Math.min(1.4, previewZoom + 0.1); updatePreviewZoom(); });
  preview?.querySelector("[data-store-summary-preview-print]")?.addEventListener("click", () => {
    const style = document.createElement("style");
    style.id = "store-summary-print-page-style";
    style.textContent = "@page { size: A4 landscape; margin: 0; }";
    document.head.appendChild(style);
    document.body.classList.add("is-printing-supplier-report");
    window.print();
  });
  preview?.querySelector("[data-store-summary-preview-pdf]")?.addEventListener("click", downloadPdf);
  preview?.querySelector("[data-store-summary-preview-excel]")?.addEventListener("click", downloadExcel);
  window.addEventListener("afterprint", () => {
    document.body.classList.remove("is-printing-supplier-report");
    document.querySelector("#store-summary-print-page-style")?.remove();
  });

  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape" && !event.defaultPrevented) {
      event.preventDefault();
      if (preview && !preview.hidden) { closePreview(); return; }
      window.location.href = "/";
    }
  });
});
