document.addEventListener("DOMContentLoaded", () => {
  const page = document.querySelector("[data-list-page]");
  const grid = page?.querySelector("[data-customer-grid]");
  const table = grid?.querySelector("table");
  const tableBody = table?.querySelector("tbody");
  const sortHeaders = Array.from(table?.querySelectorAll("[data-sort-key]") ?? []);
  const rows = Array.from(table?.querySelectorAll("[data-customer-row]") ?? []);
  const search = page?.querySelector("[data-customer-search]");
  const searchClear = page?.querySelector("[data-customer-search-clear]");
  const account = page?.querySelector("[data-customer-account]");
  const count = page?.querySelector("[data-customer-count]");
  const empty = page?.querySelector("[data-customer-empty]");
  const deleteForm = page?.querySelector("[data-customer-delete-form]");
  const deleteCode = deleteForm?.querySelector("[data-customer-delete-code]");
  const preview = page?.querySelector("[data-customer-print-preview]");
  const previewDocument = preview?.querySelector("[data-customer-preview-document]");
  const previewSummary = preview?.querySelector("[data-customer-print-summary]");
  const previewZoomLabel = preview?.querySelector("[data-customer-preview-zoom-label]");

  page?.querySelectorAll("[data-page-message]").forEach((pageMessage) => {
    window.MicronoteMessageBox?.show({
      message: pageMessage.dataset.messageText,
      variant: pageMessage.dataset.messageVariant || "info"
    });
  });

  if (!page || !grid || !table || !search || !account) {
    return;
  }

  const normalize = (value) =>
    String(value ?? "")
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .toLowerCase();

  const visibleRows = () =>
    Array.from(tableBody?.querySelectorAll("[data-customer-row]") ?? [])
      .filter((row) => !row.hidden);
  const selectedRow = () => table.querySelector(".selected-row");
  let currentSortKey = "";
  let currentSortDirection = "asc";

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
    if (!row || row.hidden) {
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

    if (type === "number") {
      return Number.parseFloat(value) || 0;
    }

    return normalize(value);
  };

  const applySort = (key, direction, type = "text") => {
    if (!tableBody) {
      return;
    }

    const sortedRows = [...rows].sort((left, right) => {
      const leftValue = sortValue(left, key, type);
      const rightValue = sortValue(right, key, type);
      let result = 0;

      if (leftValue < rightValue) {
        result = -1;
      } else if (leftValue > rightValue) {
        result = 1;
      }

      return direction === "desc" ? -result : result;
    });

    sortedRows.forEach((row) => tableBody.appendChild(row));
  };

  const updateSortHeaders = () => {
    sortHeaders.forEach((header) => {
      const isActive = header.dataset.sortKey === currentSortKey;
      header.classList.toggle("is-sorted", isActive);
      header.dataset.sortDirection = isActive ? currentSortDirection : "";
      header.setAttribute(
        "aria-sort",
        isActive
          ? (currentSortDirection === "asc" ? "ascending" : "descending")
          : "none"
      );
    });
  };

  const sortByHeader = (header) => {
    const key = header.dataset.sortKey;

    if (!key) {
      return;
    }

    currentSortDirection =
      currentSortKey === key && currentSortDirection === "asc" ? "desc" : "asc";
    currentSortKey = key;

    applySort(key, currentSortDirection, header.dataset.sortType);
    updateSortHeaders();
    selectRow(visibleRows()[0]);
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

  const applyFilters = () => {
    const needle = normalize(search.value);
    const accountCode = account.value;
    let visibleCount = 0;

    if (searchClear) {
      searchClear.hidden = search.value === "";
    }

    rows.forEach((row) => {
      const textMatches =
        needle === "" || normalize(row.dataset.filterName).includes(needle);
      const accountMatches =
        accountCode === "" || row.dataset.accountCode === accountCode;
      row.hidden = !(textMatches && accountMatches);
      if (!row.hidden) {
        visibleCount += 1;
      }
    });

    if (count) {
      count.textContent = String(visibleCount);
    }
    if (empty) {
      empty.hidden = visibleCount !== 0;
    }

    if (!selectedRow() || selectedRow().hidden) {
      selectRow(visibleRows()[0]);
    }
  };

  const requireSelection = () => {
    const row = selectedRow();
    if (!row) {
      window.MicronoteMessageBox?.show({
        message: "Selezionare un cliente.",
        variant: "info"
      });
      return null;
    }
    return row;
  };

  const editSelected = () => {
    const row = requireSelection();
    if (row?.dataset.editUrl) {
      window.location.href = row.dataset.editUrl;
    }
  };

  const printableRows = () => visibleRows().map((row) => ({
    code: Number.parseInt(row.dataset.printCode ?? "", 10),
    inactive: row.classList.contains("is-inactive"),
    cells: Array.from(row.cells).map((cell) => cell.textContent.trim())
  }));

  let previewZoom = 1;

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

  const buildPrintPreview = () => {
    if (!preview || !previewDocument) {
      return;
    }

    const sourceRows = printableRows();
    if (sourceRows.length === 0) {
      window.MicronoteMessageBox?.show({
        message: "Non ci sono clienti da stampare con i filtri correnti.",
        variant: "info"
      });
      return;
    }

    const headers = Array.from(table.tHead?.rows[0]?.cells ?? [])
      .map((cell) => cell.textContent.trim());
    const rowsPerPage = 25;
    const pageCount = Math.ceil(sourceRows.length / rowsPerPage);
    const generatedAt = new Intl.DateTimeFormat("it-IT", {
      dateStyle: "short",
      timeStyle: "short"
    }).format(new Date());

    previewDocument.replaceChildren();
    for (let pageIndex = 0; pageIndex < pageCount; pageIndex += 1) {
      const pageElement = document.createElement("article");
      pageElement.className = "supplier-report-page";

      const heading = document.createElement("header");
      heading.innerHTML = `
        <div><strong>Lista clienti</strong><span>Elaborazione del ${generatedAt} · ${sourceRows.length} record</span></div>
        <small>Pagina ${pageIndex + 1} di ${pageCount}</small>`;
      pageElement.appendChild(heading);

      const reportTable = document.createElement("table");
      const reportHead = reportTable.createTHead();
      const headerRow = reportHead.insertRow();
      headers.forEach((header) => {
        const cell = document.createElement("th");
        cell.textContent = header;
        headerRow.appendChild(cell);
      });

      const reportBody = reportTable.createTBody();
      sourceRows
        .slice(pageIndex * rowsPerPage, (pageIndex + 1) * rowsPerPage)
        .forEach((sourceRow) => {
          const reportRow = reportBody.insertRow();
          reportRow.classList.toggle("is-inactive", sourceRow.inactive);
          sourceRow.cells.forEach((value) => {
            const cell = reportRow.insertCell();
            cell.textContent = value;
          });
        });

      pageElement.appendChild(reportTable);
      previewDocument.appendChild(pageElement);
    }

    if (previewSummary) {
      previewSummary.textContent = `${sourceRows.length} record · ${pageCount} ${pageCount === 1 ? "pagina" : "pagine"}`;
    }
    previewZoom = 1;
    updatePreviewZoom();
    preview.hidden = false;
    preview.querySelector("[data-customer-preview-close]")?.focus();
  };

  const downloadPdf = async () => {
    const sourceRows = printableRows();
    const token = page.querySelector('input[name="__RequestVerificationToken"]')?.value;
    const button = preview?.querySelector("[data-customer-preview-pdf]");
    if (!token || sourceRows.length === 0) {
      return;
    }

    button?.setAttribute("disabled", "disabled");
    try {
      const response = await fetch("?handler=Pdf", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "RequestVerificationToken": token
        },
        body: JSON.stringify({ codes: sourceRows.map((row) => row.code) })
      });
      if (!response.ok) {
        throw new Error("Creazione PDF non riuscita.");
      }

      const blob = await response.blob();
      const disposition = response.headers.get("Content-Disposition") ?? "";
      const encodedName = disposition.match(/filename\*=UTF-8''([^;]+)/i)?.[1];
      const plainName = disposition.match(/filename="?([^";]+)"?/i)?.[1];
      const fileName = encodedName ? decodeURIComponent(encodedName) : (plainName || "ListaClienti.pdf");
      const url = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = fileName;
      link.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      window.MicronoteMessageBox?.show({
        message: error.message || "Creazione PDF non riuscita.",
        variant: "error"
      });
    } finally {
      button?.removeAttribute("disabled");
    }
  };

  const navigateGrid = (event, row) => {
    const currentRows = visibleRows();
    if (currentRows.length === 0) {
      return false;
    }

    const currentIndex = Math.max(currentRows.indexOf(row), 0);

    if (event.key === "Enter") {
      event.preventDefault();
      editSelected();
    } else if (event.key === "ArrowDown") {
      event.preventDefault();
      selectRow(
        currentRows[Math.min(currentIndex + 1, currentRows.length - 1)],
        true,
        1
      );
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

  rows.forEach((row) => {
    row.addEventListener("click", () => selectRow(row, true));
    row.addEventListener("dblclick", () => {
      selectRow(row);
      editSelected();
    });
    row.addEventListener("keydown", (event) => {
      navigateGrid(event, selectedRow() || row);
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

  grid.addEventListener("keydown", (event) => {
    if (event.target.closest("[data-customer-row]")) {
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
    });
  }, { passive: true });

  page.querySelectorAll("[data-customer-action]").forEach((button) => {
    button.addEventListener("click", () => {
      const action = button.dataset.customerAction;
      if (action === "edit") {
        editSelected();
        return;
      }

      if (action === "print") {
        buildPrintPreview();
        return;
      }

      const row = requireSelection();
      if (action === "delete" && row && deleteForm && deleteCode) {
        window.MicronoteMessageBox?.show({
          mode: "confirm",
          variant: "confirm",
          message: `Eliminare il cliente ${row.dataset.recordLabel}?`,
          detail: "L'operazione non potrà essere annullata.",
          okText: "Elimina",
          onConfirm: () => {
            deleteCode.value = row.dataset.deleteCode ?? "";
            deleteForm.submit();
          }
        });
      }
    });
  });

  search.addEventListener("input", applyFilters);
  searchClear?.addEventListener("click", () => {
    search.value = "";
    search.focus();
    applyFilters();
  });
  account.addEventListener("change", applyFilters);

  preview?.querySelector("[data-customer-preview-close]")?.addEventListener("click", closePrintPreview);
  preview?.querySelector("[data-customer-preview-zoom-out]")?.addEventListener("click", () => {
    previewZoom = Math.max(0.5, previewZoom - 0.1);
    updatePreviewZoom();
  });
  preview?.querySelector("[data-customer-preview-zoom-in]")?.addEventListener("click", () => {
    previewZoom = Math.min(1.8, previewZoom + 0.1);
    updatePreviewZoom();
  });
  preview?.querySelector("[data-customer-preview-print]")?.addEventListener("click", () => {
    document.body.classList.add("is-printing-supplier-report");
    window.print();
  });
  preview?.querySelector("[data-customer-preview-pdf]")?.addEventListener("click", downloadPdf);
  window.addEventListener("afterprint", () => {
    document.body.classList.remove("is-printing-supplier-report");
  });

  document.addEventListener("keydown", (event) => {
    const target = event.target;
    const isEditable = target instanceof HTMLInputElement ||
      target instanceof HTMLTextAreaElement ||
      target instanceof HTMLSelectElement ||
      target instanceof HTMLButtonElement ||
      target?.isContentEditable;

    if (event.defaultPrevented || isEditable || event.altKey || event.ctrlKey || event.metaKey) {
      return;
    }

    if (event.key.length === 1) {
      event.preventDefault();
      search.value += event.key;
      search.focus();
      applyFilters();
      return;
    }

    if (event.key === "Backspace" && search.value !== "") {
      event.preventDefault();
      search.value = search.value.slice(0, -1);
      search.focus();
      applyFilters();
    }
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

  applyFilters();
  window.setTimeout(() => selectRow(visibleRows()[0]), 0);
});
