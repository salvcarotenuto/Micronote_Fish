(function () {
  const page = document.querySelector("[data-cash-movements-list]");
  if (!page) return;
  const filters = page.querySelector("[data-cash-filters]");
  const grid = page.querySelector(".cash-movements-list-grid-frame");
  const table = page.querySelector(".cash-movements-list-grid");
  const selectedId = filters?.querySelector("[data-cash-selected-id]");
  const rows = () => Array.from(page.querySelectorAll("[data-cash-row]"));
  const selected = () => page.querySelector("[data-cash-row].selected");
  const ensureVisible = (row, direction = 0) => {
    if (!grid || !table || !row) return;
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
    rows().forEach((item) => item.classList.toggle("selected", item === row));
    rows().forEach((item) => item.classList.toggle("selected-row", item === row));
    rows().forEach((item) => item.setAttribute("aria-selected", item === row ? "true" : "false"));
    if (selectedId) selectedId.value = row?.dataset.id || "";
    if (focus) row?.focus({ preventScroll: true });
    ensureVisible(row, direction);
  };
  const returnUrl = (row) => {
    const url = new URL(location.href);
    if (row?.dataset.id) url.searchParams.set("selectedId", row.dataset.id);
    return encodeURIComponent(`${url.pathname}${url.search}`);
  };
  const edit = (row) => {
    if (!row) return;
    if (row.dataset.subjectType !== "C") {
      window.MicronoteMessageBox?.show({ title: "Movimenti di cassa", message: "La scheda disponibile gestisce attualmente i movimenti cliente.", variant: "error" });
      return;
    }
    location.href = `/MovimentoContabileCliente/Edit/${row.dataset.id}?returnTo=${returnUrl(row)}`;
  };
  filters?.querySelectorAll("select").forEach((control) => control.addEventListener("change", () => filters.requestSubmit()));
  rows().forEach((row, index) => {
    row.addEventListener("click", () => select(row, true));
    row.addEventListener("dblclick", () => { select(row); edit(row); });
    row.addEventListener("keydown", (event) => {
      if (event.key === "ArrowDown") { event.preventDefault(); select(rows()[Math.min(index + 1, rows().length - 1)], true, 1); }
      if (event.key === "ArrowUp") { event.preventDefault(); select(rows()[Math.max(index - 1, 0)], true, -1); }
      if (event.key === "Home") { event.preventDefault(); select(rows()[0], true, -1); }
      if (event.key === "End") { event.preventDefault(); select(rows()[rows().length - 1], true, 1); }
      if (event.key === "Enter") { event.preventDefault(); edit(row); }
    });
  });
  const restoredSelection = selected();
  window.setTimeout(() => select(restoredSelection || rows()[0]), 0);

  const gridViewport = () => {
    const gridRect = grid.getBoundingClientRect();
    const headerHeight = table.tHead?.getBoundingClientRect().height ?? 0;
    return { top: gridRect.top + headerHeight, bottom: gridRect.bottom };
  };
  const isRowVisible = (row, viewport) => {
    const rowRect = row.getBoundingClientRect();
    return rowRect.bottom > viewport.top + 1 && rowRect.top < viewport.bottom - 1;
  };
  const fullyVisibleRows = (viewport) => rows().filter((row) => {
    const rowRect = row.getBoundingClientRect();
    return rowRect.top >= viewport.top + 1 && rowRect.bottom <= viewport.bottom - 1;
  });

  let previousScrollTop = grid?.scrollTop ?? 0;
  let scrollFrame = 0;
  grid?.addEventListener("scroll", () => {
    if (scrollFrame) window.cancelAnimationFrame(scrollFrame);
    scrollFrame = window.requestAnimationFrame(() => {
      scrollFrame = 0;
      const currentScrollTop = grid.scrollTop;
      const delta = currentScrollTop - previousScrollTop;
      previousScrollTop = currentScrollTop;
      if (delta === 0) return;
      const viewport = gridViewport();
      const current = selected();
      if (!current || isRowVisible(current, viewport)) return;
      const currentRows = fullyVisibleRows(viewport);
      if (currentRows.length === 0) return;
      select(delta > 0 ? currentRows[0] : currentRows[currentRows.length - 1]);
    });
  }, { passive: true });
  page.querySelectorAll("[data-cash-action]").forEach((button) => button.addEventListener("click", () => {
    const action = button.dataset.cashAction;
    if (action === "new") { location.href = `/MovimentoContabileCliente/Edit?returnTo=${returnUrl(selected())}`; return; }
    if (action === "print") { window.print(); return; }
    const row = selected();
    if (!row) { window.MicronoteMessageBox?.show({ title: "Movimenti di cassa", message: "Selezionare un movimento.", variant: "error" }); return; }
    if (action === "edit") { edit(row); return; }
    if (action === "delete" && window.confirm(`Cancellare la partita ${row.dataset.code.padStart(6, "0")} / ${row.dataset.year}?`)) {
      const form = page.querySelector("[data-cash-delete-form]");
      form.elements.id.value = row.dataset.id;
      form.submit();
    }
  }));
})();
