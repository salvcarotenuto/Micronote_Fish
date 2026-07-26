document.addEventListener("DOMContentLoaded", () => {
  const filterForm = document.querySelector("[data-sales-history-filters]");
  const grid = document.querySelector(".sales-history-grid-frame");
  const table = grid?.querySelector("table");
  const tableBody = table?.querySelector("tbody");
  const sortHeaders = Array.from(table?.querySelectorAll("[data-sort-key]") ?? []);
  const rows = Array.from(table?.querySelectorAll("[data-sales-history-row]") ?? []);
  const actionButtons = Array.from(document.querySelectorAll("[data-sales-history-action]"));
  const deleteForm = document.querySelector("[data-sales-history-delete-form]");
  const deleteId = deleteForm?.querySelector("[data-sales-history-delete-id]");
  const detailBody = document.querySelector("[data-sales-history-detail-body]");
  const articleModal = document.querySelector("[data-sales-history-article-modal]");
  const articleModalFrame = document.querySelector("[data-sales-history-article-modal-frame]");
  const printPreview = document.querySelector("[data-sales-history-print-preview]");
  const printDocument = printPreview?.querySelector("[data-sales-history-preview-document]");
  const printSummary = printPreview?.querySelector("[data-sales-history-print-summary]");
  const printZoomLabel = printPreview?.querySelector("[data-sales-history-preview-zoom-label]");
  let filterTimer;
  let currentSortKey = "";
  let currentSortDirection = "asc";
  let detailRequest = 0;
  let printZoom = 0.8;

  const submitFilters = () => {
    if (!filterForm) {
      return;
    }

    window.clearTimeout(filterTimer);
    filterTimer = window.setTimeout(() => {
      filterForm.requestSubmit();
    }, 150);
  };

  filterForm?.querySelectorAll("input, select").forEach((field) => {
    field.addEventListener("change", submitFilters);
  });

  const visibleRows = () =>
    Array.from(tableBody?.querySelectorAll("[data-sales-history-row]") ?? [])
      .filter((row) => !row.hidden);
  const selectedRow = () => table?.querySelector("[data-sales-history-row].selected-row");

  const printableColumnIndexes = () =>
    Array.from(table?.querySelectorAll("thead th") ?? [])
      .map((header, index) => ({ header, index }))
      .filter(({ header }) => !header.hasAttribute("data-print-hidden"));

  const updatePrintZoom = () => {
    printDocument?.style.setProperty("--report-preview-zoom", String(printZoom));
    if (printZoomLabel) {
      printZoomLabel.textContent = `${Math.round(printZoom * 100)}%`;
    }
  };

  const closePrintPreview = () => {
    if (printPreview) {
      printPreview.hidden = true;
    }
  };

  const appendPrintPage = (columns, printRows, pageNumber, pageCount) => {
    if (!printDocument) {
      return;
    }

    const sheet = document.createElement("article");
    sheet.className = "supplier-report-page sales-history-report-page";
    const header = document.createElement("header");
    const title = document.createElement("div");
    const strong = document.createElement("strong");
    const subtitle = document.createElement("span");
    const pageInfo = document.createElement("small");
    strong.textContent = "STORICO VENDITE";
    subtitle.textContent = "Elenco secondo i filtri e l'ordinamento applicati";
    pageInfo.innerHTML =
      `data di stampa: ${new Intl.DateTimeFormat("it-IT").format(new Date())}<br>` +
      `Pagina ${pageNumber} di ${pageCount}`;
    title.append(strong, subtitle);
    header.append(title, pageInfo);
    sheet.appendChild(header);

    const reportTable = document.createElement("table");
    const headRow = reportTable.createTHead().insertRow();
    columns.forEach(({ header: sourceHeader }) => {
      const cell = document.createElement("th");
      cell.textContent = sourceHeader.textContent.trim();
      headRow.appendChild(cell);
    });

    const body = reportTable.createTBody();
    printRows.forEach((sourceRow) => {
      const row = body.insertRow();
      columns.forEach(({ index }) => {
        row.insertCell().textContent = sourceRow.cells[index]?.textContent.trim() ?? "";
      });
    });
    sheet.appendChild(reportTable);
    printDocument.appendChild(sheet);
  };

  const openPrintPreview = () => {
    if (!printPreview || !printDocument || !table) {
      return;
    }

    const printRows = visibleRows();
    if (printRows.length === 0) {
      window.MicronoteMessageBox?.show({
        title: "Stampa",
        message: "Nessuna vendita da stampare."
      });
      return;
    }

    const columns = printableColumnIndexes();
    const rowsPerPage = 28;
    const pageCount = Math.ceil(printRows.length / rowsPerPage);
    printDocument.replaceChildren();
    for (let index = 0; index < pageCount; index += 1) {
      appendPrintPage(
        columns,
        printRows.slice(index * rowsPerPage, (index + 1) * rowsPerPage),
        index + 1,
        pageCount
      );
    }

    if (printSummary) {
      printSummary.textContent = `${printRows.length} record · ${pageCount} pagine`;
    }
    updatePrintZoom();
    printPreview.hidden = false;
    printPreview.querySelector("[data-sales-history-preview-close]")?.focus();
  };

  const normalize = (value) =>
    String(value ?? "")
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .toLowerCase();

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

  const formatMoney = (value) => {
    const number = Number(value) || 0;
    return number === 0
      ? ""
      : number.toLocaleString("it-IT", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  };

  const formatStoreCode = (value) => String(Number(value) || 0).padStart(3, "0");

  const cell = (text) => {
    const td = document.createElement("td");
    td.textContent = text;
    return td;
  };

  const renderDetails = (details) => {
    if (!detailBody) {
      return;
    }

    detailBody.replaceChildren(
      ...details.map((detail) => {
        const row = document.createElement("tr");
        row.append(
          cell(formatStoreCode(detail.storeCode)),
          cell(detail.storeName || ""),
          cell(formatMoney(detail.net)),
          cell(formatMoney(detail.nonTaxable)),
          cell(formatMoney(detail.vat)),
          cell(formatMoney(detail.total)),
          cell(formatMoney(detail.cash)),
          cell(formatMoney(detail.card)),
          cell(formatMoney(detail.tickets)),
          cell(formatMoney(detail.checks)),
          cell(formatMoney(detail.other)),
          cell(formatMoney(detail.suspended)),
          cell(formatMoney(detail.losses))
        );
        return row;
      })
    );
  };

  const loadDetails = async (row) => {
    if (!detailBody || !row) {
      return;
    }

    const requestId = ++detailRequest;
    const year = row.dataset.saleYear || "";
    const code = row.dataset.saleCode || "";
    if (!year || !code) {
      renderDetails([]);
      return;
    }

    try {
      const response = await fetch(
        `/Vendite/Storico?handler=Details&year=${encodeURIComponent(year)}&code=${encodeURIComponent(code)}`,
        { headers: { "Accept": "application/json" } }
      );

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }

      const data = await response.json();
      if (requestId === detailRequest) {
        renderDetails(Array.isArray(data.rows) ? data.rows : []);
      }
    } catch {
      if (requestId === detailRequest) {
        renderDetails([]);
      }
    }
  };

  const dispatchRowChanged = (row) => {
    row.dispatchEvent(new CustomEvent("sales-history-row-changed", {
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

  const sortValue = (row, key, type) => {
    const value = row.dataset[`sort${key[0].toUpperCase()}${key.slice(1)}`] ?? "";
    return type === "number" ? Number.parseFloat(value) || 0 : normalize(value);
  };

  const applySort = (key, direction, type = "text") => {
    if (!tableBody) {
      return;
    }

    [...rows]
      .sort((left, right) => {
        const leftValue = sortValue(left, key, type);
        const rightValue = sortValue(right, key, type);
        let result = 0;

        if (leftValue < rightValue) {
          result = -1;
        } else if (leftValue > rightValue) {
          result = 1;
        }

        return direction === "desc" ? -result : result;
      })
      .forEach((row) => tableBody.appendChild(row));
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

  const gridViewport = () => {
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

  const closeArticleModal = () => {
    if (!articleModal || !articleModalFrame) {
      return;
    }

    articleModal.hidden = true;
    articleModalFrame.removeAttribute("src");
  };

  const openArticleModal = (row) => {
    const movementId = row?.dataset.accountingMovementId || "";
    if (!movementId) {
      window.MicronoteMessageBox?.show({
        title: "Vedi articolo p.n.",
        message: "Articolo contabile non trovato."
      });
      return;
    }

    if (!articleModal || !articleModalFrame) {
      window.location.href = "/ArticoloPrimaNota?id=" + encodeURIComponent(movementId);
      return;
    }

    articleModalFrame.src = "/ArticoloPrimaNota?id=" + encodeURIComponent(movementId);
    articleModal.hidden = false;
  };

  window.addEventListener("message", (event) => {
    if (event.data?.type === "micronote:accounting-article:close") {
      closeArticleModal();
    }
  });

  const selectedLabel = (row) => {
    const cells = Array.from(row?.querySelectorAll("td") ?? []);
    const number = cells[1]?.textContent?.trim() || "";
    const date = cells[2]?.textContent?.trim() || "";
    return [number, date].filter(Boolean).join(" - ");
  };

  const requireSelection = () => {
    const row = selectedRow();
    if (row) {
      return row;
    }

    window.MicronoteMessageBox?.show({
      title: "Storico vendite",
      message: "Selezionare una vendita dalla lista."
    });
    return null;
  };

  const editSelected = () => {
    const row = requireSelection();
    if (!row) {
      return;
    }

    const returnTo = `${window.location.pathname}${window.location.search}`;
    window.location.href =
      `/Vendite/Index?saleId=${encodeURIComponent(row.dataset.saleId)}&returnTo=${encodeURIComponent(returnTo)}`;
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
    row.addEventListener("dblclick", editSelected);
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

  actionButtons.forEach((button) => {
    button.addEventListener("click", () => {
      const action = button.dataset.salesHistoryAction;
      if (action === "print") {
        openPrintPreview();
        return;
      }

      const row = requireSelection();
      if (!row) {
        return;
      }

      const label = selectedLabel(row);
      if (action === "edit") {
        editSelected();
        return;
      }

      if (action === "article") {
        openArticleModal(row);
        return;
      }

      if (action === "delete") {
        window.MicronoteMessageBox?.show({
          mode: "confirm",
          variant: "confirm",
          title: "Cancella vendita",
          message: `Cancellare la vendita ${label}?`,
          detail: "Saranno cancellati anche dettaglio, movimento IVA e articolo contabile collegato.",
          okText: "Cancella",
          onConfirm: () => {
            if (deleteForm && deleteId) {
              deleteId.value = row.dataset.saleId || "";
              deleteForm.submit();
            }
          }
        });
        return;
      }

    });
  });

  printPreview?.querySelector("[data-sales-history-preview-close]")
    ?.addEventListener("click", closePrintPreview);
  printPreview?.querySelector("[data-sales-history-preview-zoom-out]")
    ?.addEventListener("click", () => {
      printZoom = Math.max(0.45, printZoom - 0.1);
      updatePrintZoom();
    });
  printPreview?.querySelector("[data-sales-history-preview-zoom-in]")
    ?.addEventListener("click", () => {
      printZoom = Math.min(1.4, printZoom + 0.1);
      updatePrintZoom();
    });
  printPreview?.querySelector("[data-sales-history-preview-print]")
    ?.addEventListener("click", () => {
      const style = document.createElement("style");
      style.id = "sales-history-print-page-style";
      style.textContent = "@page { size: A4 landscape; margin: 0; }";
      document.head.appendChild(style);
      document.body.classList.add("is-printing-supplier-report");
      window.print();
    });

  window.addEventListener("afterprint", () => {
    document.body.classList.remove("is-printing-supplier-report");
    document.querySelector("#sales-history-print-page-style")?.remove();
  });

  if (grid && table && rows.length > 0) {
    grid.addEventListener("sales-history-row-changed", (event) => {
      loadDetails(event.detail.row);
    });

    grid.addEventListener("keydown", (event) => {
      if (event.target.closest("[data-sales-history-row]")) {
        return;
      }

      const row = selectedRow() || visibleRows()[0];
      if (row) {
        navigateGrid(event, row);
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

    window.setTimeout(() => selectRow(visibleRows()[0]), 0);
  }

  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape" && !event.defaultPrevented) {
      event.preventDefault();
      if (printPreview && !printPreview.hidden) {
        closePrintPreview();
        return;
      }
      window.location.href = "/";
    }
  });
});
