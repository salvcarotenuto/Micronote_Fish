document.addEventListener("DOMContentLoaded", () => {
  const root = document.querySelector("[data-sales-history]");
  if (!root) return;

  const filterForm = root.querySelector("[data-sales-history-filters]");
  const grid = root.querySelector(".sales-history-grid-frame");
  const table = grid?.querySelector("table");
  const body = table?.tBodies[0];
  const rows = Array.from(body?.querySelectorAll("[data-sales-history-row]") ?? []);
  const detailBody = root.querySelector("[data-sales-history-detail-body]");
  const editButton = root.querySelector("[data-sales-history-edit]");
  const printButton = root.querySelector("[data-sales-history-print]");
  const preview = root.querySelector("[data-sales-history-print-preview]");
  const previewDocument = root.querySelector("[data-sales-history-preview-document]");
  let selected = null;
  let detailRequest = 0;
  let sortKey = "";
  let sortDirection = "asc";
  let filterTimer = 0;

  const normalize = value => String(value ?? "")
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase();
  const money = value => {
    const number = Number(value) || 0;
    return number === 0 ? "" : number.toLocaleString("it-IT", {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    });
  };
  const quantity = value => {
    const number = Number(value) || 0;
    return number === 0 ? "" : number.toLocaleString("it-IT", {
      minimumFractionDigits: 3,
      maximumFractionDigits: 3
    });
  };
  const cell = text => {
    const element = document.createElement("td");
    element.textContent = text;
    return element;
  };

  const submitFilters = () => {
    window.clearTimeout(filterTimer);
    filterTimer = window.setTimeout(() => filterForm?.requestSubmit(), 180);
  };
  filterForm?.querySelectorAll("select").forEach(field =>
    field.addEventListener("change", submitFilters));
  filterForm?.querySelector("input[name='customer']")?.addEventListener("change", submitFilters);

  const ensureVisible = (row, direction = 0) => {
    if (!grid || !row) return;
    const headerHeight = table?.tHead?.offsetHeight ?? 0;
    const top = grid.scrollTop + headerHeight;
    const bottom = grid.scrollTop + grid.clientHeight;
    const rowTop = row.offsetTop;
    const rowBottom = rowTop + row.offsetHeight;
    if (rowTop >= top && rowBottom <= bottom) return;
    grid.scrollTop = direction >= 0 && rowBottom > bottom
      ? rowBottom - grid.clientHeight + 1
      : Math.max(rowTop - headerHeight - 1, 0);
  };

  const renderDetails = details => {
    detailBody?.replaceChildren(...details.map(detail => {
      const row = document.createElement("tr");
      row.append(
        cell(detail.articleCode || ""),
        cell(detail.description || ""),
        cell(detail.unit || ""),
        cell(detail.packages ? String(detail.packages) : ""),
        cell(quantity(detail.tare)),
        cell(quantity(detail.quantity)),
        cell(quantity(detail.price)),
        cell(`${money(detail.vatRate)}${Number(detail.vatRate) ? " %" : ""}`),
        cell(money(detail.vatPrice)),
        cell(money(detail.amount))
      );
      return row;
    }));
  };

  const loadDetails = async row => {
    const request = ++detailRequest;
    renderDetails([]);
    try {
      const response = await fetch(
        `/Vendite/Storico?handler=Details&saleId=${encodeURIComponent(row.dataset.saleId)}`,
        { headers: { Accept: "application/json" } });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      const result = await response.json();
      if (request === detailRequest)
        renderDetails(Array.isArray(result.rows) ? result.rows : []);
    } catch {
      if (request === detailRequest) renderDetails([]);
    }
  };

  const selectRow = (row, focus = false, direction = 0) => {
    if (!row) return;
    rows.forEach(candidate => {
      candidate.classList.toggle("selected-row", candidate === row);
      candidate.setAttribute("aria-selected", candidate === row ? "true" : "false");
    });
    const changed = selected !== row;
    selected = row;
    editButton.disabled = false;
    if (changed) loadDetails(row);
    if (focus) row.focus({ preventScroll: true });
    ensureVisible(row, direction);
  };

  const navigate = (event, row) => {
    const index = Math.max(rows.indexOf(row), 0);
    let target = null;
    if (event.key === "ArrowDown") target = rows[Math.min(index + 1, rows.length - 1)];
    else if (event.key === "ArrowUp") target = rows[Math.max(index - 1, 0)];
    else if (event.key === "Home") target = rows[0];
    else if (event.key === "End") target = rows[rows.length - 1];
    if (!target) return;
    event.preventDefault();
    selectRow(target, true, target === row ? 0 : (rows.indexOf(target) > index ? 1 : -1));
  };

  rows.forEach(row => {
    row.addEventListener("click", () => selectRow(row, true));
    row.addEventListener("keydown", event => navigate(event, row));
  });

  editButton?.addEventListener("click", () => {
    if (!selected) return;
    window.location.href =
      `/BollaVendita/Index?id=${encodeURIComponent(selected.dataset.saleId)}`;
  });

  const sortValue = (row, key, type) => {
    const name = `sort${key[0].toUpperCase()}${key.slice(1)}`;
    const value = row.dataset[name] ?? "";
    return type === "number" ? Number(value) || 0 : normalize(value);
  };
  root.querySelectorAll("[data-sort-key]").forEach(header => {
    header.tabIndex = 0;
    header.setAttribute("role", "button");
    const applySort = () => {
      const key = header.dataset.sortKey;
      sortDirection = sortKey === key && sortDirection === "asc" ? "desc" : "asc";
      sortKey = key;
      rows.sort((left, right) => {
        const a = sortValue(left, key, header.dataset.sortType);
        const b = sortValue(right, key, header.dataset.sortType);
        const result = a < b ? -1 : a > b ? 1 : 0;
        return sortDirection === "desc" ? -result : result;
      }).forEach(row => body.appendChild(row));
      root.querySelectorAll("[data-sort-key]").forEach(candidate => {
        const active = candidate === header;
        candidate.classList.toggle("is-sorted", active);
        candidate.setAttribute("aria-sort", active
          ? (sortDirection === "asc" ? "ascending" : "descending")
          : "none");
      });
      selectRow(rows[0]);
    };
    header.addEventListener("click", applySort);
    header.addEventListener("keydown", event => {
      if (event.key === "Enter" || event.key === " ") {
        event.preventDefault();
        applySort();
      }
    });
  });

  const openPrintPreview = () => {
    if (!preview || !previewDocument || !table || rows.length === 0) return;
    const sheet = document.createElement("article");
    sheet.className = "supplier-report-page sales-history-report-page";
    const header = document.createElement("header");
    header.innerHTML = `<div><strong>STORICO VENDITE</strong><span>Elenco secondo i filtri e l'ordinamento applicati</span></div>
      <small>data di stampa: ${new Intl.DateTimeFormat("it-IT").format(new Date())}</small>`;
    const reportTable = table.cloneNode(true);
    reportTable.querySelectorAll("tbody tr").forEach(row => row.removeAttribute("tabindex"));
    sheet.append(header, reportTable);
    previewDocument.replaceChildren(sheet);
    root.querySelector("[data-sales-history-print-summary]").textContent = `${rows.length} record`;
    preview.hidden = false;
    root.querySelector("[data-sales-history-preview-close]")?.focus();
  };
  printButton?.addEventListener("click", openPrintPreview);
  root.querySelector("[data-sales-history-preview-close]")?.addEventListener("click", () => {
    preview.hidden = true;
  });
  root.querySelector("[data-sales-history-preview-print]")?.addEventListener("click", () => {
    document.body.classList.add("is-printing-supplier-report");
    window.print();
  });
  window.addEventListener("afterprint", () =>
    document.body.classList.remove("is-printing-supplier-report"));

  document.addEventListener("keydown", event => {
    if (event.key !== "Escape" || event.defaultPrevented) return;
    event.preventDefault();
    if (preview && !preview.hidden) {
      preview.hidden = true;
      return;
    }
    window.location.href = "/";
  });

  if (rows.length > 0)
    window.setTimeout(() => selectRow(rows[0]), 0);
});
