document.addEventListener("DOMContentLoaded", () => {
  const filterForm = document.querySelector("[data-accounting-filters]");
  const grid = document.querySelector(".accounting-movements-grid-frame");
  const table = grid?.querySelector("table");
  let rows = Array.from(table?.querySelectorAll("[data-accounting-row]") ?? []);
  const sortHeaders = Array.from(table?.querySelectorAll("th[data-sort-key]") ?? []);
  const actionButtons = Array.from(document.querySelectorAll("[data-accounting-action]"));
  const deleteForm = document.querySelector("[data-accounting-delete-form]");
  const deleteId = deleteForm?.querySelector("[data-accounting-delete-id]");
  const articleModal = document.querySelector("[data-accounting-article-modal]");
  const articleModalFrame = document.querySelector("[data-accounting-article-modal-frame]");
  const printPreview = document.querySelector("[data-accounting-print-preview]");
  const printDocument = printPreview?.querySelector("[data-accounting-preview-document]");
  const printSummary = printPreview?.querySelector("[data-accounting-print-summary]");
  const printZoomLabel = printPreview?.querySelector("[data-accounting-preview-zoom-label]");
  let printZoom = 0.8;
  let filterTimer;
  let sortState = { key: "", direction: "asc" };

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
      if (printPreview && !printPreview.hidden) {
        printPreview.hidden = true;
        return;
      }
      window.location.href = "/";
    }
  });

  const visibleRows = () => rows.filter((row) => !row.hidden);
  const selectedRow = () => table?.querySelector("[data-accounting-row].selected");

  const printableRows = () => visibleRows().filter((row) => row.dataset.printEnabled === "true");

  const updatePrintZoom = () => {
    printDocument?.style.setProperty("--report-preview-zoom", String(printZoom));
    if (printZoomLabel) printZoomLabel.textContent = `${Math.round(printZoom * 100)}%`;
  };

  const closePrintPreview = () => {
    if (printPreview) printPreview.hidden = true;
  };

  const appendPrintPage = (headers, pageRows, number, count) => {
    const sheet = document.createElement("article");
    sheet.className = "supplier-report-page accounting-movements-report-page";
    const header = document.createElement("header");
    const from = filterForm?.querySelector("[name='dateFrom']")?.value ?? "";
    const to = filterForm?.querySelector("[name='dateTo']")?.value ?? "";
    const formatDate = (value) => value ? value.split("-").reverse().join("/") : "";
    header.innerHTML = `<div><strong>MOVIMENTI CONTABILI DI PRIMA NOTA</strong><span>Periodo dal ${formatDate(from)} al ${formatDate(to)}</span></div><small>data di stampa: ${new Intl.DateTimeFormat("it-IT").format(new Date())}<br>Pagina ${number} di ${count}</small>`;
    sheet.appendChild(header);
    const reportTable = document.createElement("table");
    const headRow = reportTable.createTHead().insertRow();
    headers.forEach((value) => { const cell = document.createElement("th"); cell.textContent = value; headRow.appendChild(cell); });
    const body = reportTable.createTBody();
    pageRows.forEach((values) => {
      const row = body.insertRow();
      values.forEach((value) => { row.insertCell().textContent = value; });
    });
    sheet.appendChild(reportTable);
    printDocument.appendChild(sheet);
  };

  const openPrintPreview = () => {
    if (!printPreview || !printDocument || !table) return;
    const selectedRows = printableRows();
    if (!selectedRows.length) {
      window.MicronoteMessageBox?.show({
        title: "Stampa prima nota",
        message: "Nessun movimento stampabile.",
        detail: "Le causali dei movimenti visualizzati non hanno il flag Stampa attivo."
      });
      return;
    }
    const headers = Array.from(table.querySelectorAll("thead th")).map((cell) => cell.textContent.trim());
    const values = selectedRows.map((row) => Array.from(row.cells).map((cell) => cell.textContent.trim()));
    const rowsPerPage = 25;
    const pageCount = Math.ceil(values.length / rowsPerPage);
    printDocument.replaceChildren();
    for (let index = 0; index < pageCount; index += 1) {
      appendPrintPage(headers, values.slice(index * rowsPerPage, (index + 1) * rowsPerPage), index + 1, pageCount);
    }
    if (printSummary) printSummary.textContent = `${values.length} record stampabili · ${pageCount} pagine`;
    updatePrintZoom();
    printPreview.hidden = false;
    printPreview.querySelector("[data-accounting-preview-close]")?.focus();
  };

  const toKebab = (value) => value.replace(/[A-Z]/g, (match) => `-${match.toLowerCase()}`);

  const sortValue = (row, key, type) => {
    const value = row.getAttribute(`data-sort-${toKebab(key)}`) ?? "";
    if (type === "number") {
      const numeric = Number.parseFloat(value.replace(",", "."));
      return Number.isFinite(numeric) ? numeric : Number.NEGATIVE_INFINITY;
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
    if (!table?.tBodies[0]) {
      return;
    }

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

    rows = rows
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

    rows.forEach((row) => table.tBodies[0].appendChild(row));
    updateSortHeaders();

    if (selected) {
      selectRow(selected, true, 0);
      return;
    }

    const first = visibleRows()[0];
    if (first) {
      selectRow(first, true, 0);
    }
  };

  const closeArticleModal = () => {
    if (!articleModal) {
      return;
    }

    articleModal.hidden = true;
    document.body.classList.remove("purchase-invoice-payment-modal-open");
    articleModalFrame?.removeAttribute("src");
    document.querySelector("[data-accounting-action='article']")?.focus();
  };

  const openArticleModal = (row) => {
    const url = `/ArticoloPrimaNota?id=${encodeURIComponent(row.dataset.movementId || "")}`;
    if (!articleModal || !articleModalFrame) {
      window.location.href = url;
      return;
    }

    articleModalFrame.src = url;
    articleModal.hidden = false;
    document.body.classList.add("purchase-invoice-payment-modal-open");
    articleModalFrame.focus();
  };

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
      title: "Prima nota",
      message: "Selezionare un movimento dalla lista."
    });
    return null;
  };

  const currentReturnUrl = () => window.location.pathname + window.location.search;

  const openMovementEdit = (row) => {
    const id = row.dataset.movementId || "";
    const sector = Number.parseInt(row.dataset.movementSector || "0", 10);
    const document = row.dataset.movementDocument || "";
    const returnTo = encodeURIComponent(currentReturnUrl());

    if (sector === 20) {
      if (!document) {
        window.MicronoteMessageBox?.show({
          title: "Vendita",
          message: "Collegamento alla vendita non disponibile.",
          detail: "Il movimento non contiene il riferimento al documento origine."
        });
        return;
      }

      window.location.href = `/Vendite?saleId=${encodeURIComponent(document)}&returnTo=${returnTo}`;
      return;
    }

    if (sector === 10 || sector === 30 || sector === 40 || sector === 50 || sector === 60) {
      window.location.href = `/PrimaNota/Edit/${id}?returnTo=${returnTo}`;
      return;
    }

    window.MicronoteMessageBox?.show({
      title: "Prima nota",
      message: "Settore movimento non gestito.",
      detail: `Settore ${Number.isFinite(sector) ? sector : ""}`
    });
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

  actionButtons.forEach((button) => {
    button.addEventListener("click", () => {
      const action = button.dataset.accountingAction;
      if (action === "new") {
        window.location.href = "/PrimaNota/Edit";
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
        openMovementEdit(row);
        return;
      }

      const label = selectedLabel(row);
      if (action === "delete") {
        window.MicronoteMessageBox?.show({
          mode: "confirm",
          variant: "confirm",
          title: "Cancella movimento",
          message: `Cancellare il movimento ${label}?`,
          detail: "Saranno cancellate anche le righe contabili collegate.",
          okText: "Cancella",
          onConfirm: () => {
            if (deleteForm && deleteId) {
              deleteId.value = row.dataset.movementId || "";
              deleteForm.submit();
            }
          }
        });
        return;
      }

      if (action === "article") {
        openArticleModal(row);
        return;
      }

      const titles = {
        edit: "Modifica movimento",
        print: "Stampa"
      };

      window.MicronoteMessageBox?.show({
        title: titles[action] || "Prima nota",
        message: `${titles[action] || "Operazione"} per ${label}.`,
        detail: "Funzione in preparazione."
      });
    });
  });

  printPreview?.querySelector("[data-accounting-preview-close]")?.addEventListener("click", closePrintPreview);
  printPreview?.querySelector("[data-accounting-preview-zoom-out]")?.addEventListener("click", () => {
    printZoom = Math.max(0.45, printZoom - 0.1);
    updatePrintZoom();
  });
  printPreview?.querySelector("[data-accounting-preview-zoom-in]")?.addEventListener("click", () => {
    printZoom = Math.min(1.4, printZoom + 0.1);
    updatePrintZoom();
  });
  printPreview?.querySelector("[data-accounting-preview-print]")?.addEventListener("click", () => {
    const style = document.createElement("style");
    style.id = "accounting-movements-print-page-style";
    style.textContent = "@page { size: A4 landscape; margin: 0; }";
    document.head.appendChild(style);
    document.body.classList.add("is-printing-supplier-report");
    window.print();
  });
  window.addEventListener("afterprint", () => {
    document.body.classList.remove("is-printing-supplier-report");
    document.querySelector("#accounting-movements-print-page-style")?.remove();
  });

  window.addEventListener("message", (event) => {
    if (event.origin === window.location.origin && event.data?.type === "micronote:accounting-article:close") {
      closeArticleModal();
    }
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

