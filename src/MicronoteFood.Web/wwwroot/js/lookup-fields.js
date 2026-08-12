(function () {
  const apiUrl = "/api/lookup_anagrafiche";
  let overlay = null;
  let state = null;
  let searchTimer = 0;

  const defaultColumns = [
    { key: "code", label: "Codice" },
    { key: "label", label: "Nome" },
    { key: "detail", label: "Dettaglio" }
  ];
  const customerColumns = [
    { key: "code", label: "Codice", width: 90 },
    { key: "label", label: "Cliente", width: 420 },
    { key: "city", label: "Città", width: 220 },
    { key: "category", label: "Categoria", width: 200 }
  ];
  const articleColumns = [
    { key: "code", label: "Codice", width: 120 },
    { key: "description", label: "Descrizione", width: 300 },
    { key: "unitMeasure", label: "U.m.", width: 70, align: "center" },
    { key: "standardCost", label: "Costo std", numeric: true, width: 90, priceMode: "carico" },
    { key: "standardPrice", label: "Prezzo std", numeric: true, width: 90, priceMode: "vendita" },
    { key: "category", label: "Categoria", width: 150 },
    { key: "group", label: "Gruppo", width: 150 },
    { key: "species", label: "Specie", width: 150 },
    { key: "origin", label: "Provenienza", width: 150 }
  ];

  function buildOverlay() {
    if (overlay) {
      return overlay;
    }

    overlay = document.createElement("div");
    overlay.className = "lookup-overlay";
    overlay.innerHTML = `
      <div class="lookup-dialog" role="dialog" aria-modal="true" aria-labelledby="lookup-title">
        <div class="lookup-titlebar">
          <h2 id="lookup-title"></h2>
          <button class="lookup-close" type="button" aria-label="Chiudi"></button>
        </div>
        <div class="lookup-toolbar">
          <label for="lookup-search">Cerca</label>
          <input id="lookup-search" class="lookup-search" type="text" autocomplete="off">
        </div>
        <div class="lookup-table-wrap" tabindex="0">
          <table class="lookup-table">
            <colgroup></colgroup>
            <thead>
              <tr>
                ${defaultColumns.map((column) => `<th><button type="button" data-lookup-sort="${column.key}">${column.label}</button></th>`).join("")}
              </tr>
            </thead>
            <tbody></tbody>
          </table>
        </div>
        <div class="lookup-actions">
          <button class="button button-primary primary lookup-select" type="button">OK</button>
          <button class="button button-secondary secondary lookup-cancel" type="button">Annulla</button>
        </div>
      </div>
    `;
    document.body.appendChild(overlay);

    overlay.querySelector(".lookup-close")?.addEventListener("click", closeLookup);
    overlay.querySelector(".lookup-cancel")?.addEventListener("click", closeLookup);
    overlay.querySelector(".lookup-select")?.addEventListener("click", selectCurrent);
    overlay.addEventListener("mousedown", (event) => {
      if (event.target === overlay) {
        closeLookup();
      }
    });

    const search = overlay.querySelector(".lookup-search");
    search?.addEventListener("input", () => {
      window.clearTimeout(searchTimer);
      searchTimer = window.setTimeout(fetchRows, 160);
    });
    search?.addEventListener("keydown", handleLookupKeydown);

    overlay.querySelector("tbody")?.addEventListener("click", (event) => {
      const row = event.target.closest("tr[data-index]");
      if (!row) {
        return;
      }

      setSelectedIndex(Number(row.dataset.index));
      overlay.querySelector(".lookup-table-wrap")?.focus({ preventScroll: true });
    });
    overlay.querySelector("tbody")?.addEventListener("dblclick", selectCurrent);
    overlay.querySelector(".lookup-table-wrap")?.addEventListener("keydown", handleLookupKeydown);
    setupScrollSelection(overlay.querySelector(".lookup-table-wrap"));
    overlay.querySelector("thead")?.addEventListener("click", (event) => {
      const button = event.target.closest("[data-lookup-sort]");
      if (!button || !state) {
        return;
      }

      const key = button.dataset.lookupSort;
      state.sortDirection = state.sortKey === key && state.sortDirection === "asc" ? "desc" : "asc";
      state.sortKey = key;
      sortRows();
      state.selectedIndex = state.rows.length > 0 ? 0 : -1;
      renderRows();
    });

    return overlay;
  }

  function openLookup(field) {
    const dialog = buildOverlay();
    const articlePriceMode = field.dataset.lookupArticlePrice === "vendita"
      ? "vendita"
      : "carico";
    state = {
      field,
      type: field.dataset.lookupType,
      title: field.dataset.lookupTitle || "Selezione",
      columns: field.dataset.lookupType === "articoli"
        ? articleColumns.filter(column => !column.priceMode || column.priceMode === articlePriceMode)
        : field.dataset.lookupType === "clienti"
          ? customerColumns
          : defaultColumns,
      rows: [],
      selectedIndex: -1,
      sortKey: "label",
      sortDirection: "asc"
    };

    dialog.querySelector("#lookup-title").textContent = state.title;
    overlay.classList.toggle("is-article-lookup", state.type === "articoli");
    const dialogPanel = dialog.querySelector(".lookup-dialog");
    dialogPanel?.classList.toggle("is-article-lookup", state.type === "articoli");
    dialogPanel?.classList.toggle("is-customer-lookup", state.type === "clienti");
    dialog.querySelector("colgroup").innerHTML = state.columns
      .map(column => `<col${column.width ? ` style="width:${column.width}px"` : ""}>`)
      .join("");
    const assignedTableWidth = state.columns.reduce((total, column) => total + (column.width || 0), 0);
    dialog.querySelector(".lookup-table").style.width = assignedTableWidth
      ? `${assignedTableWidth}px`
      : "100%";
    dialog.querySelector(".lookup-table").style.minWidth = assignedTableWidth
      ? `${assignedTableWidth}px`
      : "650px";
    dialog.querySelector("thead tr").innerHTML = state.columns
      .map(column => {
        const alignment = column.numeric ? "right" : column.align || "";
        const style = alignment ? ` style="text-align:${alignment}"` : "";
        const buttonStyle = alignment
          ? ` style="justify-content:${alignment === "right" ? "flex-end" : "center"};text-align:${alignment}"`
          : "";
        return `<th class="${column.numeric ? "numeric-column" : ""}"${style}><button type="button" data-lookup-sort="${column.key}"${buttonStyle}>${column.label}</button></th>`;
      })
      .join("");
    dialog.querySelector(".lookup-search").value = "";
    dialog.querySelector("tbody").innerHTML = "";
    dialog.classList.add("is-open");
    document.body.classList.add("lookup-open");
    fetchRows();
    window.setTimeout(() => dialog.querySelector(".lookup-search")?.focus(), 0);
  }

  function closeLookup() {
    overlay?.classList.remove("is-open");
    document.body.classList.remove("lookup-open");
    state = null;
  }

  async function fetchRows() {
    if (!state?.type) {
      return;
    }

    const search = overlay.querySelector(".lookup-search")?.value.trim() || "";
    const isArticleLookup = state.type === "articoli";
    const params = isArticleLookup
      ? new URLSearchParams({ q: search })
      : new URLSearchParams({ type: state.type, q: search });
    const response = await fetch(`${isArticleLookup ? "/api/articoli" : apiUrl}?${params.toString()}`, {
      headers: { Accept: "application/json" }
    });
    const payload = await response.json();

    state.rows = Array.isArray(payload.rows)
      ? payload.rows.map(row => isArticleLookup
        ? {
            ...row,
            unitMeasure: state.field.dataset.lookupArticleUnit === "vendita"
              ? row.salesUnitMeasure || ""
              : row.purchaseUnitMeasure || "",
            codeLabel: row.code,
            label: row.description || "",
            detail: [row.unitMeasure, row.category].filter(Boolean).join(" · ")
          }
        : row)
      : [];
    sortRows();
    state.selectedIndex = state.rows.length > 0 ? 0 : -1;
    renderRows();
  }

  function renderRows() {
    renderSortHeaders();
    const tbody = overlay.querySelector("tbody");
    tbody.innerHTML = state.rows.map((row, index) => `
      <tr data-index="${index}" class="${index === state.selectedIndex ? "selected" : ""}">
        ${state.columns.map(column => {
          const alignment = column.numeric ? "right" : column.align || "";
          return `<td class="${column.numeric ? "numeric-column" : ""}"${alignment ? ` style="text-align:${alignment}"` : ""}>${escapeHtml(
          column.numeric
            ? Number(row[column.key] || 0).toLocaleString("it-IT", { minimumFractionDigits: 2, maximumFractionDigits: 2 })
            : row[column.key] || ""
          )}</td>`;
        }).join("")}
      </tr>
    `).join("");

    ensureSelectedVisible();
  }

  function renderSortHeaders() {
    overlay.querySelectorAll("[data-lookup-sort]").forEach((button) => {
      const isActive = button.dataset.lookupSort === state.sortKey;
      button.classList.toggle("is-active", isActive);
      button.dataset.direction = isActive ? state.sortDirection : "";
    });
  }

  function sortRows() {
    if (!state) {
      return;
    }

    const direction = state.sortDirection === "desc" ? -1 : 1;
    const key = state.sortKey;
    state.rows.sort((left, right) => {
      const column = state.columns.find(item => item.key === key);
      if (key === "code" || column?.numeric) {
        return ((Number(left[key]) || 0) - (Number(right[key]) || 0)) * direction;
      }

      return String(left[key] || "").localeCompare(String(right[key] || ""), "it", {
        sensitivity: "base",
        numeric: true
      }) * direction;
    });
  }

  function setSelectedIndex(index, direction = 0) {
    if (!state || index < 0 || index >= state.rows.length) {
      return;
    }

    state.selectedIndex = index;
    overlay.querySelectorAll("tbody tr").forEach((row) => {
      row.classList.toggle("selected", Number(row.dataset.index) === state.selectedIndex);
    });
    ensureSelectedVisible(direction);
  }

  function moveSelection(delta) {
    if (!state || state.rows.length === 0) {
      return;
    }

    setSelectedIndex(Math.max(0, Math.min(state.rows.length - 1, state.selectedIndex + delta)), delta);
  }

  function handleLookupKeydown(event) {
    if (event.key === "ArrowDown") {
      event.preventDefault();
      moveSelection(1);
      return;
    }

    if (event.key === "ArrowUp") {
      event.preventDefault();
      moveSelection(-1);
      return;
    }

    if (event.key === "Home") {
      event.preventDefault();
      setSelectedIndex(0, -1);
      return;
    }

    if (event.key === "End") {
      event.preventDefault();
      setSelectedIndex(state.rows.length - 1, 1);
      return;
    }

    if (event.key === "Enter") {
      event.preventDefault();
      selectCurrent();
      return;
    }

    if (event.key === "Escape") {
      event.preventDefault();
      event.stopPropagation();
      closeLookup();
      return;
    }

    const search = overlay?.querySelector(".lookup-search");
    if (!search || event.target === search || event.ctrlKey || event.altKey || event.metaKey) {
      return;
    }

    if (event.key === "Backspace") {
      event.preventDefault();
      search.value = search.value.slice(0, -1);
      search.dispatchEvent(new Event("input", { bubbles: true }));
      return;
    }

    if (event.key.length === 1) {
      event.preventDefault();
      search.value += event.key;
      search.dispatchEvent(new Event("input", { bubbles: true }));
    }
  }

  function ensureSelectedVisible(direction = 0) {
    const row = overlay.querySelector("tbody tr.selected");
    const tableWrap = row?.closest(".lookup-table-wrap");
    const table = row?.closest("table");
    if (!row || !tableWrap || !table) {
      return;
    }

    const headerHeight = table.querySelector("thead")?.offsetHeight || 0;
    const visibleTop = tableWrap.scrollTop + headerHeight;
    const visibleBottom = tableWrap.scrollTop + tableWrap.clientHeight;
    const rowTop = row.offsetTop;
    const rowBottom = rowTop + row.offsetHeight;

    if (rowTop >= visibleTop && rowBottom <= visibleBottom) {
      return;
    }

    if (direction >= 0 && rowBottom > visibleBottom) {
      tableWrap.scrollTop = rowBottom - tableWrap.clientHeight + 1;
      return;
    }

    if (direction <= 0 && rowTop < visibleTop) {
      tableWrap.scrollTop = Math.max(rowTop - headerHeight - 1, 0);
    }
  }

  function setupScrollSelection(tableWrap) {
    if (!tableWrap) {
      return;
    }

    let lastScrollTop = tableWrap.scrollTop;
    let frame = 0;

    tableWrap.addEventListener("scroll", () => {
      if (frame) {
        window.cancelAnimationFrame(frame);
      }

      frame = window.requestAnimationFrame(() => {
        frame = 0;
        const currentScrollTop = tableWrap.scrollTop;
        const delta = currentScrollTop - lastScrollTop;
        lastScrollTop = currentScrollTop;

        if (!state || delta === 0) {
          return;
        }

        const selected = tableWrap.querySelector("tbody tr.selected");
        if (!selected || isRowVisible(selected, tableWrap)) {
          return;
        }

        const rows = fullyVisibleRows(tableWrap);
        if (rows.length === 0) {
          return;
        }

        setSelectedIndex(Number((delta > 0 ? rows[0] : rows[rows.length - 1]).dataset.index), delta > 0 ? 1 : -1);
      });
    }, { passive: true });
  }

  function isRowVisible(row, tableWrap) {
    const table = tableWrap.querySelector("table");
    const wrapRect = tableWrap.getBoundingClientRect();
    const headerHeight = table?.querySelector("thead")?.offsetHeight || 0;
    const rowRect = row.getBoundingClientRect();
    return rowRect.top >= wrapRect.top + headerHeight + 1 && rowRect.bottom <= wrapRect.bottom - 1;
  }

  function fullyVisibleRows(tableWrap) {
    return Array.from(tableWrap.querySelectorAll("tbody tr[data-index]")).filter((row) => isRowVisible(row, tableWrap));
  }

  function selectCurrent() {
    if (!state || state.selectedIndex < 0) {
      return;
    }

    const row = state.rows[state.selectedIndex];
    if (!row) {
      return;
    }

    applyRow(state.field, row, state.type);
    closeLookup();
  }

  function applyRow(field, row, type) {
    setInputValue(field.dataset.codeInput, row.code || "");
    setInputValue(field.dataset.codeDisplay, row.codeLabel || row.code || "");
    setInputValue(field.dataset.labelInput, row.label || "");

    const codeInput = document.getElementById(field.dataset.codeInput);
    codeInput?.dispatchEvent(new Event("change", { bubbles: true }));
    field.dispatchEvent(new CustomEvent("micronote:lookup-selected", {
      bubbles: true,
      detail: { row, type }
    }));
  }

  function clearRow(field) {
    setInputValue(field.dataset.codeInput, "");
    setInputValue(field.dataset.codeDisplay, "");
    setInputValue(field.dataset.labelInput, "");
    document.getElementById(field.dataset.codeInput)?.dispatchEvent(new Event("change", { bubbles: true }));
  }

  async function lookupByCode(field) {
    const codeInput = document.getElementById(field.dataset.codeDisplay);
    if (!codeInput) {
      return;
    }

    const code = String(codeInput.value || "").replace(/\D/g, "");
    if (code === "") {
      clearRow(field);
      return;
    }

    const params = new URLSearchParams({ type: field.dataset.lookupType, code });
    const response = await fetch(`${apiUrl}?${params.toString()}`, {
      headers: { Accept: "application/json" }
    });
    const payload = await response.json();
    if (payload.row) {
      applyRow(field, payload.row, field.dataset.lookupType);
      return;
    }

    field.dispatchEvent(new CustomEvent("micronote:lookup-not-found", {
      bubbles: true,
      detail: { code, type: field.dataset.lookupType }
    }));
    clearRow(field);
  }

  function setInputValue(id, value) {
    const input = document.getElementById(id);
    if (input) {
      input.value = value;
    }
  }

  function escapeHtml(value) {
    return String(value).replace(/[&<>"']/g, (char) => ({
      "&": "&amp;",
      "<": "&lt;",
      ">": "&gt;",
      "\"": "&quot;",
      "'": "&#039;"
    }[char]));
  }

  document.addEventListener("click", (event) => {
    const button = event.target.closest("[data-lookup-open]");
    if (!button) {
      return;
    }

    const field = button.closest("[data-lookup-field]");
    if (field) {
      openLookup(field);
    }
  });

  document.addEventListener("keydown", (event) => {
    if (!state || event.target === overlay?.querySelector(".lookup-search")
        || event.target?.closest?.(".lookup-table-wrap")) {
      return;
    }
    handleLookupKeydown(event);
  });

  document.querySelectorAll("[data-lookup-field]").forEach((field) => {
    const codeInput = document.getElementById(field.dataset.codeDisplay);
    if (!codeInput) {
      return;
    }

    codeInput.addEventListener("focus", () => codeInput.select());
    codeInput.addEventListener("input", () => {
      codeInput.value = codeInput.value.replace(/\D/g, "");
    });
    codeInput.addEventListener("change", () => lookupByCode(field));
    codeInput.addEventListener("keydown", (event) => {
      if (event.key === "Enter") {
        event.preventDefault();
        codeInput.blur();
      }
    });
  });
}());
