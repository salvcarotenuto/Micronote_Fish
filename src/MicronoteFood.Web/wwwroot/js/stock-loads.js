document.addEventListener("DOMContentLoaded", () => {
  const page = document.querySelector("[data-stock-loads]");
  const filterForm = document.querySelector("[data-stock-load-filters]");
  const grid = document.querySelector(".stock-loads-grid-frame");
  const table = grid?.querySelector("table");
  const tableBody = table?.querySelector("tbody");
  const detailBody = document.querySelector("[data-stock-load-detail-body]");
  const articleCodeFilter = document.getElementById("stockLoadFilterArticleCode");
  const articleDescriptionFilter = document.getElementById("stockLoadFilterArticleDescription");
  const articleLookup = document.querySelector("[data-stock-load-filter-article-lookup]");
  const rows = Array.from(table?.querySelectorAll("[data-stock-load-row]") ?? []);
  const sortHeaders = Array.from(table?.querySelectorAll("[data-sort-key]") ?? []);
  const actionButtons = Array.from(document.querySelectorAll("[data-stock-load-action]"));
  const electronicInvoiceViewer = document.querySelector("[data-stock-load-electronic-invoice-viewer]");
  const electronicInvoiceViewerFrame = document.querySelector("[data-stock-load-electronic-invoice-viewer-frame]");
  const electronicInvoiceViewerTitle = document.querySelector("[data-stock-load-electronic-invoice-viewer-title]");
  const electronicInvoiceViewerPanel = document.querySelector("[data-stock-load-electronic-invoice-viewer-panel]");
  const electronicInvoiceViewerCloseButtons = Array.from(document.querySelectorAll("[data-stock-load-electronic-invoice-viewer-close]"));
  const preview = document.querySelector("[data-stock-loads-print-preview]");
  const previewDocument = preview?.querySelector("[data-stock-loads-preview-document]");
  const previewSummary = preview?.querySelector("[data-stock-loads-print-summary]");
  const previewZoomLabel = preview?.querySelector("[data-stock-loads-preview-zoom-label]");
  let previewZoom = 0.8;
  let filterTimer;
  let currentSortKey = "";
  let currentSortDirection = "asc";
  let detailController = null;
  let currentDetailKey = table?.querySelector("[data-stock-load-row].selected")?.dataset.stockLoadKey || "";
  let articleLookupRows = [];
  let articleLookupFilteredRows = [];
  let articleLookupSelectedIndex = -1;
  let articleLookupSortKey = "description";
  let articleLookupSortDirection = "asc";

  const articleLookupFields = {
    search: articleLookup?.querySelector("[data-article-search]"),
    category: articleLookup?.querySelector("[data-article-category]"),
    supplier: articleLookup?.querySelector("[data-article-supplier]"),
    count: articleLookup?.querySelector("[data-article-count]"),
    body: articleLookup?.querySelector("tbody"),
    frame: articleLookup?.querySelector(".stock-load-article-lookup-frame")
  };

  if (!page) {
    return;
  }

  const updatePreviewZoom = () => {
    previewDocument?.style.setProperty("--report-preview-zoom", String(previewZoom));
    if (previewZoomLabel) previewZoomLabel.textContent = `${Math.round(previewZoom * 100)}%`;
  };

  const closePrintPreview = () => {
    if (preview) preview.hidden = true;
  };

  const appendPrintPage = (headers, printRows, number, count) => {
    const sheet = document.createElement("article");
    sheet.className = "supplier-report-page stock-loads-report-page";
    const header = document.createElement("header");
    header.innerHTML = `<div><strong>LISTA DOCUMENTI DI CARICO</strong><span>Elenco secondo i filtri e l'ordinamento applicati</span></div><small>data di stampa: ${new Intl.DateTimeFormat("it-IT").format(new Date())}<br>Pagina ${number} di ${count}</small>`;
    sheet.appendChild(header);
    const reportTable = document.createElement("table");
    const headRow = reportTable.createTHead().insertRow();
    headers.forEach((value) => {
      const cell = document.createElement("th");
      cell.textContent = value;
      headRow.appendChild(cell);
    });
    const body = reportTable.createTBody();
    printRows.forEach((values) => {
      const row = body.insertRow();
      values.forEach((value) => { row.insertCell().textContent = value; });
    });
    sheet.appendChild(reportTable);
    previewDocument.appendChild(sheet);
  };

  const openPrintPreview = () => {
    if (!preview || !previewDocument || !table || !tableBody) return;
    const visiblePrintRows = Array.from(tableBody.querySelectorAll("[data-stock-load-row]"))
      .filter((row) => !row.hidden)
      .map((row) => Array.from(row.cells).map((cell) => cell.textContent.trim()));
    if (!visiblePrintRows.length) {
      window.MicronoteMessageBox?.show({ title: "Stampa", message: "Nessun documento di carico da stampare." });
      return;
    }
    const headers = Array.from(table.querySelectorAll("thead th")).map((cell) => cell.textContent.trim());
    const rowsPerPage = 25;
    const pageCount = Math.ceil(visiblePrintRows.length / rowsPerPage);
    previewDocument.replaceChildren();
    for (let index = 0; index < pageCount; index += 1) {
      appendPrintPage(headers, visiblePrintRows.slice(index * rowsPerPage, (index + 1) * rowsPerPage), index + 1, pageCount);
    }
    if (previewSummary) previewSummary.textContent = `${visiblePrintRows.length} record · ${pageCount} pagine`;
    updatePreviewZoom();
    preview.hidden = false;
    preview.querySelector("[data-stock-loads-preview-close]")?.focus();
  };

  const submitFilters = () => {
    if (!filterForm) {
      return;
    }

    window.clearTimeout(filterTimer);
    filterTimer = window.setTimeout(() => filterForm.requestSubmit(), 150);
  };

  const escapeHtml = (value) => String(value ?? "").replace(/[&<>"']/g, (char) => ({
    "&": "&amp;",
    "<": "&lt;",
    ">": "&gt;",
    "\"": "&quot;",
    "'": "&#039;"
  }[char]));

  const formatLookupMoney = (value) => {
    const number = Number(value ?? 0);
    if (!Number.isFinite(number) || number === 0) {
      return "";
    }

    return number.toLocaleString("it-IT", {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    });
  };

  const formatLookupPercent = (value) => {
    const formatted = formatLookupMoney(value);
    return formatted ? `${formatted} %` : "";
  };

  const buildLookupOptions = (select, sourceRows, codeKey, labelKey) => {
    if (!select) {
      return;
    }

    const options = new Map();
    sourceRows.forEach((row) => {
      const code = row[codeKey];
      const label = row[labelKey];
      if (code && label && !options.has(String(code))) {
        options.set(String(code), label);
      }
    });

    select.innerHTML = `<option value=""></option>${Array.from(options.entries())
      .sort((left, right) => String(left[1]).localeCompare(String(right[1]), "it", { sensitivity: "base", numeric: true }))
      .map(([code, label]) => `<option value="${escapeHtml(code)}">${escapeHtml(label)}</option>`)
      .join("")}`;
  };

  const sortArticleRows = () => {
    const direction = articleLookupSortDirection === "desc" ? -1 : 1;
    articleLookupFilteredRows.sort((left, right) => {
      if (articleLookupSortKey === "price" || articleLookupSortKey === "vatRate") {
        return ((Number(left[articleLookupSortKey]) || 0) - (Number(right[articleLookupSortKey]) || 0)) * direction;
      }

      return String(left[articleLookupSortKey] ?? "").localeCompare(String(right[articleLookupSortKey] ?? ""), "it", {
        sensitivity: "base",
        numeric: true
      }) * direction;
    });
  };

  const isArticleLookupRowVisible = (row) => {
    const frame = articleLookupFields.frame;
    if (!row || !frame) {
      return false;
    }

    const headerHeight = frame.querySelector("thead")?.offsetHeight || 0;
    const frameRect = frame.getBoundingClientRect();
    const rowRect = row.getBoundingClientRect();
    return rowRect.top >= frameRect.top + headerHeight + 1 && rowRect.bottom <= frameRect.bottom - 1;
  };

  const ensureArticleLookupSelectedVisible = (direction = 0) => {
    const row = articleLookup?.querySelector("tbody tr.selected");
    const frame = articleLookupFields.frame;
    if (!row || !frame) {
      return;
    }

    const headerHeight = frame.querySelector("thead")?.offsetHeight || 0;
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

  const setArticleLookupSelectedIndex = (index, direction = 0) => {
    if (index < 0 || index >= articleLookupFilteredRows.length) {
      return;
    }

    articleLookupSelectedIndex = index;
    articleLookup?.querySelectorAll("tbody tr").forEach((row) => {
      row.classList.toggle("selected", Number(row.dataset.index) === articleLookupSelectedIndex);
    });
    ensureArticleLookupSelectedVisible(direction);
  };

  const renderArticleLookupRows = () => {
    if (!articleLookupFields.body) {
      return;
    }

    articleLookup?.querySelectorAll("[data-article-sort]").forEach((button) => {
      const active = button.dataset.articleSort === articleLookupSortKey;
      button.classList.toggle("is-active", active);
      button.dataset.direction = active ? articleLookupSortDirection : "";
    });

    articleLookupFields.body.innerHTML = articleLookupFilteredRows.map((row, index) => `
      <tr data-index="${index}" class="${index === articleLookupSelectedIndex ? "selected" : ""}">
        <td>${escapeHtml(row.code)}</td>
        <td>${escapeHtml(row.description)}</td>
        <td>${escapeHtml(row.category)}</td>
        <td>${escapeHtml(row.group)}</td>
        <td>${escapeHtml(row.subgroup)}</td>
        <td>${formatLookupMoney(row.price)}</td>
        <td>${formatLookupPercent(row.vatRate)}</td>
      </tr>
    `).join("");

    if (articleLookupFields.count) {
      articleLookupFields.count.value = String(articleLookupFilteredRows.length);
    }

    ensureArticleLookupSelectedVisible();
  };

  const filterArticleLookupRows = () => {
    const search = String(articleLookupFields.search?.value ?? "").trim().toLocaleLowerCase("it");
    const category = articleLookupFields.category?.value ?? "";
    const supplier = articleLookupFields.supplier?.value ?? "";

    articleLookupFilteredRows = articleLookupRows.filter((row) => {
      const matchesSearch = !search
        || String(row.code ?? "").toLocaleLowerCase("it").includes(search)
        || String(row.description ?? "").toLocaleLowerCase("it").includes(search);
      const matchesCategory = !category || String(row.categoryCode ?? "") === category;
      const matchesSupplier = !supplier || String(row.supplierCode ?? "") === supplier;
      return matchesSearch && matchesCategory && matchesSupplier;
    });
    sortArticleRows();
    articleLookupSelectedIndex = articleLookupFilteredRows.length > 0 ? 0 : -1;
    renderArticleLookupRows();
  };

  const loadArticleLookupRows = async () => {
    const response = await fetch("/api/articoli", {
      headers: { "Accept": "application/json" }
    });
    const payload = await response.json();
    articleLookupRows = Array.isArray(payload.rows) ? payload.rows : [];
    buildLookupOptions(articleLookupFields.category, articleLookupRows, "categoryCode", "category");
    buildLookupOptions(articleLookupFields.supplier, articleLookupRows, "supplierCode", "supplier");
    filterArticleLookupRows();
  };

  const closeArticleLookup = () => {
    if (!articleLookup) {
      return;
    }

    articleLookup.hidden = true;
    document.body.classList.remove("lookup-open");
    articleCodeFilter?.focus();
  };

  const openArticleLookup = async () => {
    if (!articleLookup) {
      return;
    }

    articleLookup.hidden = false;
    document.body.classList.add("lookup-open");
    articleLookupFields.search.value = "";
    await loadArticleLookupRows();
    window.setTimeout(() => articleLookupFields.search?.focus(), 0);
  };

  const selectArticleLookupRow = () => {
    const row = articleLookupFilteredRows[articleLookupSelectedIndex];
    if (!row || !articleCodeFilter || !articleDescriptionFilter) {
      return;
    }

    articleCodeFilter.value = row.code ?? "";
    articleDescriptionFilter.value = row.description ?? "";
    closeArticleLookup();
    submitFilters();
  };

  filterForm?.querySelectorAll("input[name], select[name]").forEach((field) => {
    field.addEventListener("change", submitFilters);
  });

  filterForm?.querySelector("input[name='articleCode']")?.addEventListener("keydown", (event) => {
    if (event.key === "Enter") {
      event.preventDefault();
      submitFilters();
    }
  });

  document.querySelector("[data-stock-load-article-lookup-open]")?.addEventListener("click", openArticleLookup);
  articleLookup?.querySelector("[data-article-cancel]")?.addEventListener("click", closeArticleLookup);
  articleLookup?.querySelector("[data-article-ok]")?.addEventListener("click", selectArticleLookupRow);
  articleLookupFields.search?.addEventListener("input", filterArticleLookupRows);
  articleLookupFields.category?.addEventListener("change", filterArticleLookupRows);
  articleLookupFields.supplier?.addEventListener("change", filterArticleLookupRows);
  articleLookup?.querySelector("thead")?.addEventListener("click", (event) => {
    const button = event.target.closest("[data-article-sort]");
    if (!button) {
      return;
    }

    const key = button.dataset.articleSort;
    articleLookupSortDirection = articleLookupSortKey === key && articleLookupSortDirection === "asc" ? "desc" : "asc";
    articleLookupSortKey = key;
    filterArticleLookupRows();
  });
  articleLookupFields.body?.addEventListener("click", (event) => {
    const row = event.target.closest("tr[data-index]");
    if (!row) {
      return;
    }

    setArticleLookupSelectedIndex(Number(row.dataset.index));
    articleLookupFields.frame?.focus({ preventScroll: true });
  });
  articleLookupFields.body?.addEventListener("dblclick", selectArticleLookupRow);
  articleLookupFields.frame?.addEventListener("keydown", (event) => {
    if (event.key === "ArrowDown") {
      event.preventDefault();
      setArticleLookupSelectedIndex(Math.min(articleLookupFilteredRows.length - 1, articleLookupSelectedIndex + 1), 1);
    } else if (event.key === "ArrowUp") {
      event.preventDefault();
      setArticleLookupSelectedIndex(Math.max(0, articleLookupSelectedIndex - 1), -1);
    } else if (event.key === "Enter") {
      event.preventDefault();
      selectArticleLookupRow();
    } else if (event.key === "Escape") {
      event.preventDefault();
      closeArticleLookup();
    }
  });
  articleLookupFields.search?.addEventListener("keydown", (event) => {
    if (event.key === "ArrowDown" || event.key === "ArrowUp") {
      event.preventDefault();
      articleLookupFields.frame?.focus({ preventScroll: true });
    } else if (event.key === "Enter") {
      event.preventDefault();
      selectArticleLookupRow();
    } else if (event.key === "Escape") {
      event.preventDefault();
      closeArticleLookup();
    }
  });

  let lastArticleLookupScrollTop = 0;
  let articleLookupScrollFrame = 0;
  articleLookupFields.frame?.addEventListener("scroll", () => {
    if (articleLookupScrollFrame) {
      window.cancelAnimationFrame(articleLookupScrollFrame);
    }

    articleLookupScrollFrame = window.requestAnimationFrame(() => {
      articleLookupScrollFrame = 0;
      const frame = articleLookupFields.frame;
      const currentScrollTop = frame.scrollTop;
      const delta = currentScrollTop - lastArticleLookupScrollTop;
      lastArticleLookupScrollTop = currentScrollTop;
      const selected = frame.querySelector("tbody tr.selected");
      if (!selected || delta === 0 || isArticleLookupRowVisible(selected)) {
        return;
      }

      const visibleArticleRows = Array.from(frame.querySelectorAll("tbody tr[data-index]"))
        .filter(isArticleLookupRowVisible);
      if (visibleArticleRows.length === 0) {
        return;
      }

      const row = delta > 0 ? visibleArticleRows[0] : visibleArticleRows[visibleArticleRows.length - 1];
      setArticleLookupSelectedIndex(Number(row.dataset.index), delta > 0 ? 1 : -1);
    });
  }, { passive: true });

  document.addEventListener("keydown", (event) => {
    if (event.key !== "Escape" || document.body.classList.contains("lookup-open")) {
      return;
    }

    if (electronicInvoiceViewer && !electronicInvoiceViewer.hidden) {
      event.preventDefault();
      closeElectronicInvoiceViewer();
      return;
    }

    if (preview && !preview.hidden) {
      event.preventDefault();
      closePrintPreview();
      return;
    }

    event.preventDefault();
    window.location.href = "/";
  });

  const visibleRows = () => rows.filter((row) => !row.hidden);
  const selectedRow = () => table?.querySelector("[data-stock-load-row].selected");

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

    const nextDetailKey = row.dataset.stockLoadKey || "";
    if (nextDetailKey && nextDetailKey !== currentDetailKey) {
      currentDetailKey = nextDetailKey;
      loadDetailsForRow(row);
    }
  };

  const applySort = (key, direction, type = "text") => {
    if (!tableBody) {
      return;
    }

    const selected = selectedRow();
    const selectedKey = selected?.dataset.stockLoadKey || "";
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

    const rowToSelect = rows.find((row) => row.dataset.stockLoadKey === selectedKey) || visibleRows()[0];
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

  const requireSelection = () => {
    const row = selectedRow();
    if (row) {
      return row;
    }

    window.MicronoteMessageBox?.show({
      title: "Carico di magazzino",
      message: "Selezionare un documento dalla lista."
    });
    return null;
  };

  const selectedLabel = (row) => {
    const cells = Array.from(row?.querySelectorAll("td") ?? []);
    const number = cells[2]?.textContent?.trim() || "";
    const date = cells[3]?.textContent?.trim() || "";
    return [number, date].filter(Boolean).join(" - ");
  };

  const editSelectedDocument = (row) => {
    const id = Number.parseInt(row?.dataset.stockLoadId || "0", 10);
    if (!id || id <= 0) {
      window.MicronoteMessageBox?.show({
        title: "Modifica documento",
        message: "Il documento selezionato non ha un ID valido."
      });
      return;
    }

    const returnUrl = encodeURIComponent(`${window.location.pathname}${window.location.search}`);
    window.location.href = `/CaricoAcquisti/Edit?id=${id}&returnTo=list&returnUrl=${returnUrl}`;
  };

  const closeElectronicInvoiceViewer = () => {
    if (!electronicInvoiceViewer) {
      return;
    }

    electronicInvoiceViewer.hidden = true;
    document.body.classList.remove("purchase-invoice-xml-open");
    electronicInvoiceViewerFrame?.removeAttribute("src");
    document.querySelector("[data-stock-load-action='invoice']")?.focus();
  };

  const viewElectronicInvoice = (row) => {
    const fileName = (row?.dataset.electronicInvoiceName ?? "").trim();
    if (!fileName) {
      window.MicronoteMessageBox?.show({
        title: "Fattura elettronica",
        message: "Documento senza fattura elettronica collegata.",
        detail: "La riga selezionata non contiene il nome del file FE."
      });
      return;
    }

    if (!electronicInvoiceViewer || !electronicInvoiceViewerFrame) {
      window.MicronoteMessageBox?.show({
        title: "Fattura elettronica",
        message: "Visualizzatore fattura non disponibile."
      });
      return;
    }

    const url = new URL("/CaricoAcquisti/Edit", window.location.origin);
    url.searchParams.set("handler", "ElectronicInvoiceRaw");
    url.searchParams.set("fileName", fileName);
    url.searchParams.set("source", "archive");

    if (electronicInvoiceViewerTitle) {
      electronicInvoiceViewerTitle.textContent = fileName || "Fattura elettronica";
    }

    electronicInvoiceViewerFrame.src = url.toString();
    electronicInvoiceViewer.hidden = false;
    document.body.classList.add("purchase-invoice-xml-open");
    window.requestAnimationFrame(() => {
      electronicInvoiceViewerPanel?.focus();
    });
  };

  const formatNumber = (value, digits = 2) => {
    const number = Number.parseFloat(value);
    if (!Number.isFinite(number) || number === 0) {
      return "";
    }

    return number.toLocaleString("it-IT", {
      minimumFractionDigits: digits,
      maximumFractionDigits: digits
    });
  };

  const formatQuantity = (value) => formatNumber(value, 3);

  const formatPercent = (value) => {
    const formatted = formatNumber(value, 2);
    return formatted ? `${formatted} %` : "";
  };

  const renderDetails = (details) => {
    if (!detailBody) {
      return;
    }

    detailBody.replaceChildren();
    details.forEach((detail) => {
      const row = document.createElement("tr");
      [
        detail.articleCode || "",
        detail.description || "",
        detail.unitMeasure || "",
        formatQuantity(detail.quantity),
        formatNumber(detail.price),
        formatPercent(detail.discount),
        formatNumber(detail.amount),
        formatPercent(detail.vatRate)
      ].forEach((value) => {
        const cell = document.createElement("td");
        cell.textContent = value;
        row.appendChild(cell);
      });
      detailBody.appendChild(row);
    });
  };

  const updateUrlForRow = (row) => {
    if (!row?.dataset.url) {
      return;
    }

    window.history.replaceState(null, "", row.dataset.url);
  };

  const loadDetailsForRow = async (row) => {
    if (!row) {
      return;
    }

    const id = Number.parseInt(row.dataset.stockLoadId || "0", 10);
    if (!id || id <= 0) {
      if (row.dataset.url) {
        window.location.href = row.dataset.url;
      }
      return;
    }

    detailController?.abort();
    detailController = new AbortController();

    try {
      const response = await fetch(`${window.location.pathname}?handler=Details&id=${id}`, {
        headers: { "Accept": "application/json" },
        signal: detailController.signal
      });

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }

      renderDetails(await response.json());
      updateUrlForRow(row);
    } catch (error) {
      if (error.name === "AbortError") {
        return;
      }

      window.MicronoteMessageBox?.show({
        title: "Carico di magazzino",
        message: "Aggiornamento dettaglio non riuscito."
      });
    }
  };

  rows.forEach((row) => {
    row.addEventListener("click", () => {
      selectRow(row, true, 0);
    });
    row.addEventListener("dblclick", () => {
      editSelectedDocument(row);
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
      const action = button.dataset.stockLoadAction;
      if (action === "new") {
        const returnUrl = encodeURIComponent(`${window.location.pathname}${window.location.search}`);
        window.location.href = `/CaricoAcquisti/Edit?returnTo=list&returnUrl=${returnUrl}`;
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
        editSelectedDocument(row);
        return;
      }

      if (action === "invoice") {
        viewElectronicInvoice(row);
        return;
      }

      const titles = {
        delete: "Cancella documento"
      };

      window.MicronoteMessageBox?.show({
        title: titles[action] || "Carico di magazzino",
        message: `${titles[action] || "Operazione"} per ${selectedLabel(row)}.`,
        detail: "Funzione in preparazione."
      });
    });
  });

  preview?.querySelector("[data-stock-loads-preview-close]")?.addEventListener("click", closePrintPreview);
  preview?.querySelector("[data-stock-loads-preview-zoom-out]")?.addEventListener("click", () => {
    previewZoom = Math.max(0.45, previewZoom - 0.1);
    updatePreviewZoom();
  });
  preview?.querySelector("[data-stock-loads-preview-zoom-in]")?.addEventListener("click", () => {
    previewZoom = Math.min(1.4, previewZoom + 0.1);
    updatePreviewZoom();
  });
  preview?.querySelector("[data-stock-loads-preview-print]")?.addEventListener("click", () => {
    const style = document.createElement("style");
    style.id = "stock-loads-print-page-style";
    style.textContent = "@page { size: A4 landscape; margin: 0; }";
    document.head.appendChild(style);
    document.body.classList.add("is-printing-supplier-report");
    window.print();
  });
  window.addEventListener("afterprint", () => {
    document.body.classList.remove("is-printing-supplier-report");
    document.querySelector("#stock-loads-print-page-style")?.remove();
  });

  electronicInvoiceViewerCloseButtons.forEach((button) => {
    button.addEventListener("click", closeElectronicInvoiceViewer);
  });

  electronicInvoiceViewer?.addEventListener("click", (event) => {
    if (event.target === electronicInvoiceViewer) {
      closeElectronicInvoiceViewer();
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

        const visible = visibleRows().filter((candidate) => {
          const rect = candidate.getBoundingClientRect();
          return rect.top >= viewportTop + 1 && rect.bottom <= viewportBottom - 1;
        });

        if (visible.length > 0) {
          selectRow(delta > 0 ? visible[0] : visible[visible.length - 1], true, delta > 0 ? 1 : -1);
        }
      });
    }, { passive: true });

    const initialRow = selectedRow() || visibleRows()[0];
    if (initialRow) {
      initialRow.classList.add("selected-from-server");
      selectRow(initialRow, true, 0);
    }
  }
});

