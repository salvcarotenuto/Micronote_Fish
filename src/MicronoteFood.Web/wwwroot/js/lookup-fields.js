(function () {
  const apiUrl = "/api/lookup_anagrafiche";
  let overlay = null;
  let state = null;
  let searchTimer = 0;

  const columns = [
    { key: "code", label: "Codice" },
    { key: "label", label: "Nome" },
    { key: "detail", label: "Dettaglio" }
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
            <thead>
              <tr>
                ${columns.map((column) => `<th><button type="button" data-lookup-sort="${column.key}">${column.label}</button></th>`).join("")}
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
    state = {
      field,
      type: field.dataset.lookupType,
      title: field.dataset.lookupTitle || "Selezione",
      rows: [],
      selectedIndex: -1,
      sortKey: "label",
      sortDirection: "asc"
    };

    dialog.querySelector("#lookup-title").textContent = state.title;
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
    const params = new URLSearchParams({ type: state.type, q: search });
    const response = await fetch(`${apiUrl}?${params.toString()}`, {
      headers: { Accept: "application/json" }
    });
    const payload = await response.json();

    state.rows = Array.isArray(payload.rows) ? payload.rows : [];
    sortRows();
    state.selectedIndex = state.rows.length > 0 ? 0 : -1;
    renderRows();
  }

  function renderRows() {
    renderSortHeaders();
    const tbody = overlay.querySelector("tbody");
    tbody.innerHTML = state.rows.map((row, index) => `
      <tr data-index="${index}" class="${index === state.selectedIndex ? "selected" : ""}">
        <td>${escapeHtml(row.codeLabel || row.code || "")}</td>
        <td>${escapeHtml(row.label || "")}</td>
        <td>${escapeHtml(row.detail || "")}</td>
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
      if (key === "code") {
        return ((Number(left.code) || 0) - (Number(right.code) || 0)) * direction;
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
