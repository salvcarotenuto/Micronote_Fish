document.addEventListener("DOMContentLoaded", () => {
  const page = document.querySelector("[data-customer-note]");
  if (!page) return;
  const filterForm = page.querySelector("[data-customer-note-filters]");
  const rows = Array.from(page.querySelectorAll("[data-customer-note-row]"));
  let detailRows = Array.from(page.querySelectorAll("[data-customer-note-detail-row]"));
  const selectedRow = () => page.querySelector("[data-customer-note-row].selected-row");
  const summaryFrame = page.querySelector(".customer-note-summary-frame");
  const summaryGrid = page.querySelector(".customer-note-summary-grid");
  const detailGrid = page.querySelector(".customer-note-detail-frame");
  const detailTable = page.querySelector(".customer-note-detail-grid");
  const summaryColumns = Array.from(page.querySelectorAll("[data-customer-note-summary-columns] col"));
  const resizeSummaryColumns = () => {
    if (!summaryGrid || !summaryColumns.length) return;
    const availableWidth = summaryGrid.getBoundingClientRect().width;
    const baseWidths = summaryColumns.map(column => Number(column.dataset.baseWidth) || 0);
    const baseTotal = baseWidths.reduce((total, width) => total + width, 0);
    if (availableWidth <= 0 || baseTotal <= 0) return;
    const scale = availableWidth / baseTotal;
    summaryColumns.forEach((column, index) => {
      column.style.width = `${baseWidths[index] * scale}px`;
    });
  };
  resizeSummaryColumns();
  if (window.ResizeObserver && summaryGrid) {
    new ResizeObserver(resizeSummaryColumns).observe(summaryGrid.parentElement || summaryGrid);
  } else {
    window.addEventListener("resize", resizeSummaryColumns);
  }

  let detailNavigationDispose = () => {};
  let detailRequest = null;
  let detailUpdateTimer = 0;
  const selectCustomer = async (row) => {
    const params = new URLSearchParams(new FormData(filterForm));
    params.set("customerCode", row.dataset.customerCode || "");
    const url = `${window.location.pathname}?${params}`;
    detailRequest?.abort();
    detailRequest = new AbortController();
    try {
      const response = await fetch(url, {
        signal: detailRequest.signal,
        headers: { Accept: "text/html" }
      });
      if (!response.ok) throw new Error();
      const documentCopy = new DOMParser().parseFromString(await response.text(), "text/html");
      const sourceBody = documentCopy.querySelector(".customer-note-detail-grid tbody");
      const targetBody = detailTable?.tBodies?.[0];
      if (!sourceBody || !targetBody) throw new Error();
      detailNavigationDispose();
      targetBody.replaceChildren(...Array.from(sourceBody.children).map(row => row.cloneNode(true)));
      detailRows = Array.from(targetBody.querySelectorAll("[data-customer-note-detail-row]"));
      detailNavigationDispose = installStandardGridNavigation(
        detailGrid, detailTable, detailRows) || (() => {});
      window.history.replaceState({}, "", url);
    } catch (error) {
      if (error.name === "AbortError") return;
      window.MicronoteMessageBox?.show({
        title: "Nota cliente",
        message: "Aggiornamento della lista articoli non riuscito.",
        variant: "error"
      });
    }
  };
  const queueCustomerSelection = row => {
    window.clearTimeout(detailUpdateTimer);
    detailUpdateTimer = window.setTimeout(() => selectCustomer(row), 70);
  };

  filterForm?.querySelectorAll("input[name], select[name]").forEach(control =>
    control.addEventListener("change", () => filterForm.requestSubmit()));

  const installStandardGridNavigation = (grid, table, gridRows, onEnter, onSelectionChanged) => {
    if (!grid || !table || gridRows.length === 0) return () => {};
    let active = true;
    const selected = () => gridRows.find(row => row.classList.contains("selected-row"));
    const ensureVisible = (row, direction = 0) => {
      const headerHeight = table.tHead?.offsetHeight ?? 0;
      const visibleTop = grid.scrollTop + headerHeight;
      const visibleBottom = grid.scrollTop + grid.clientHeight;
      const rowTop = row.offsetTop;
      const rowBottom = rowTop + row.offsetHeight;
      if (rowTop >= visibleTop && rowBottom <= visibleBottom) return;
      if (direction >= 0 && rowBottom > visibleBottom) {
        grid.scrollTop = rowBottom - grid.clientHeight + 1;
        return;
      }
      if (direction <= 0 && rowTop < visibleTop) {
        grid.scrollTop = Math.max(rowTop - headerHeight - 1, 0);
      }
    };
    const select = (row, focus = false, direction = 0) => {
      if (!row) return;
      gridRows.forEach(candidate => {
        candidate.classList.remove("selected", "selected-row");
        candidate.removeAttribute("aria-selected");
      });
      row.classList.add("selected", "selected-row");
      row.setAttribute("aria-selected", "true");
      if (focus) row.focus({ preventScroll: true });
      ensureVisible(row, direction);
    };
    const navigate = (event, row) => {
      const index = Math.max(gridRows.indexOf(row), 0);
      if (event.key === "Enter") {
        event.preventDefault();
        onEnter?.(selected() || row);
      } else if (event.key === "ArrowDown") {
        event.preventDefault();
        const next = gridRows[Math.min(index + 1, gridRows.length - 1)];
        select(next, true, 1);
        if (next !== row) onSelectionChanged?.(next);
      } else if (event.key === "ArrowUp") {
        event.preventDefault();
        const next = gridRows[Math.max(index - 1, 0)];
        select(next, true, -1);
        if (next !== row) onSelectionChanged?.(next);
      } else if (event.key === "Home") {
        event.preventDefault();
        select(gridRows[0], true, -1);
        if (gridRows[0] !== row) onSelectionChanged?.(gridRows[0]);
      } else if (event.key === "End") {
        event.preventDefault();
        select(gridRows[gridRows.length - 1], true, 1);
        if (gridRows[gridRows.length - 1] !== row) {
          onSelectionChanged?.(gridRows[gridRows.length - 1]);
        }
      }
    };
    gridRows.forEach(row => {
      row.addEventListener("click", () => {
        select(row, true);
        onSelectionChanged?.(row);
      });
      row.addEventListener("dblclick", () => {
        select(row);
        onEnter?.(row);
      });
      row.addEventListener("keydown", event => navigate(event, selected() || row));
    });
    const viewport = () => {
      const rect = grid.getBoundingClientRect();
      const headerHeight = table.tHead?.getBoundingClientRect().height ?? 0;
      return { top: rect.top + headerHeight, bottom: rect.bottom };
    };
    const isVisible = (row, area) => {
      const rect = row.getBoundingClientRect();
      return rect.bottom > area.top + 1 && rect.top < area.bottom - 1;
    };
    const fullyVisible = area => gridRows.filter(row => {
      const rect = row.getBoundingClientRect();
      return rect.top >= area.top + 1 && rect.bottom <= area.bottom - 1;
    });
    let lastScrollTop = grid.scrollTop;
    let scrollFrame = 0;
    const handleScroll = () => {
      if (!active) return;
      if (scrollFrame) window.cancelAnimationFrame(scrollFrame);
      scrollFrame = window.requestAnimationFrame(() => {
        if (!active) return;
        scrollFrame = 0;
        const currentScrollTop = grid.scrollTop;
        const delta = currentScrollTop - lastScrollTop;
        lastScrollTop = currentScrollTop;
        if (delta === 0) return;
        const area = viewport();
        const current = selected();
        if (!current || isVisible(current, area)) return;
        const visibleRows = fullyVisible(area);
        if (visibleRows.length === 0) return;
        const next = delta > 0 ? visibleRows[0] : visibleRows[visibleRows.length - 1];
        select(next);
        onSelectionChanged?.(next);
      });
    };
    grid.addEventListener("scroll", handleScroll, { passive: true });
    select(selected() || gridRows[0]);
    return () => {
      active = false;
      if (scrollFrame) window.cancelAnimationFrame(scrollFrame);
      grid.removeEventListener("scroll", handleScroll);
    };
  };

  installStandardGridNavigation(summaryFrame, summaryGrid, rows, queueCustomerSelection, queueCustomerSelection);
  detailNavigationDispose = installStandardGridNavigation(detailGrid, detailTable, detailRows);

  page.querySelector("[data-customer-note-action='statement']")?.addEventListener("click", () => {
    const row = selectedRow();
    if (!row) return;
    const params = new URLSearchParams({ partyType: "C", partyCode: row.dataset.customerCode || "", dateFrom: filterForm.dateFrom.value, dateTo: filterForm.dateTo.value });
    window.location.href = `/EstrattoContoClientiFornitori/Index?${params}`;
  });
  page.querySelector("[data-customer-note-action='modify']")?.addEventListener("click", () => {
    const row = selectedRow();
    if (!row) return;
    const returnTo = encodeURIComponent(`${window.location.pathname}${window.location.search}`);
    const customerCode = encodeURIComponent(row.dataset.customerCode || "");
    window.location.href = `/MovimentoContabileCliente/Edit?customerCode=${customerCode}&customerReadOnly=true&returnTo=${returnTo}`;
  });
  page.querySelector("[data-customer-note-action='customer-card']")?.addEventListener("click", () => {
    const row = selectedRow();
    if (!row) return;
    const overlay = page.querySelector("[data-customer-note-customer-card]");
    const frame = page.querySelector("[data-customer-note-customer-card-frame]");
    if (!overlay || !frame) return;
    frame.src = `/Clienti/Edit?code=${encodeURIComponent(row.dataset.customerCode || "")}&azione=101`;
    overlay.hidden = false;
  });
  window.addEventListener("message", event => {
    if (event.data?.type !== "micronote-customer-card-close") return;
    const overlay = page.querySelector("[data-customer-note-customer-card]");
    const frame = page.querySelector("[data-customer-note-customer-card-frame]");
    if (overlay) overlay.hidden = true;
    if (frame) frame.src = "about:blank";
    page.querySelector("[data-customer-note-action='customer-card']")?.focus();
  });
  let printedInSession = page.dataset.printedInSession === "true";
  const sessionForm = page.querySelector("[data-customer-note-session-form]");
  const token = sessionForm?.querySelector("input[name='__RequestVerificationToken']")?.value || "";
  page.querySelector("[data-customer-note-action='print']")?.addEventListener("click", async () => {
    try {
      const response = await fetch(`${window.location.pathname}?handler=Printed`, {
        method: "POST",
        headers: { RequestVerificationToken: token }
      });
      if (!response.ok) throw new Error("Impossibile registrare la stampa della nota cliente.");
      printedInSession = true;
      window.print();
    } catch (error) {
      window.MicronoteMessageBox?.show({ title: "Nota cliente", message: error.message, variant: "error" });
    }
  });
  page.querySelector("[data-customer-note-action='exit']")?.addEventListener("click", () => {
    if (!printedInSession) {
      window.location.href = "/";
      return;
    }
    window.MicronoteMessageBox?.show({
      title: "Nota cliente",
      message: "Memorizzare la data di ultima elaborazione delle note clienti?",
      detail: `Data proposta: ${new Date(`${page.dataset.processingDate}T00:00:00`).toLocaleDateString("it-IT")}`,
      mode: "confirm",
      variant: "confirm",
      okText: "Salva",
      cancelText: "Non salvare",
      onConfirm: () => page.querySelector("[data-customer-note-save-date-form]")?.requestSubmit(),
      onCancel: () => { window.location.href = "/"; }
    });
  });
  page.querySelectorAll("[data-page-message]").forEach(message => window.MicronoteMessageBox?.show({ message: message.dataset.messageText, variant: message.dataset.messageVariant || "info" }));
});
