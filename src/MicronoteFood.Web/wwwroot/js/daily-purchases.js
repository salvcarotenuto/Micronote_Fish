document.addEventListener("DOMContentLoaded", () => {
  const page = document.querySelector("[data-daily-purchases]");
  const rows = Array.from(page?.querySelectorAll("[data-daily-purchase-row]") ?? []);
  if (!page || rows.length === 0) return;

  const purchaseGrid = page.querySelector(".daily-purchases-list-panel .daily-purchases-grid-frame");
  const purchaseTable = purchaseGrid?.querySelector("table");
  const detailBody = page.querySelector(".daily-purchases-detail-grid tbody");
  const selectedPurchaseRow = () => purchaseTable?.querySelector("tbody tr.selected-row");
  const quantity = new Intl.NumberFormat("it-IT", { minimumFractionDigits: 3, maximumFractionDigits: 3 });
  const money = new Intl.NumberFormat("it-IT", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  const formatted = (value, formatter) => Number(value) === 0 ? "" : formatter.format(Number(value));
  let detailRequest = null;
  let detailSequence = 0;
  const loadDetails = async row => {
    if (!detailBody) return;
    detailRequest?.abort();
    detailRequest = new AbortController();
    const sequence = ++detailSequence;
    const query = new URLSearchParams({
      handler: "Details",
      id: row.dataset.id ?? "0",
      year: row.dataset.year ?? "0",
      code: row.dataset.code ?? "0"
    });
    try {
      const response = await fetch(`${window.location.pathname}?${query}`, {
        signal: detailRequest.signal,
        headers: { Accept: "application/json" }
      });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      const details = await response.json();
      if (sequence !== detailSequence || selectedPurchaseRow() !== row) return;
      const fragment = document.createDocumentFragment();
      details.forEach(detail => {
        const tr = document.createElement("tr");
        const values = [
          detail.articleCode,
          detail.description,
          detail.unitMeasure,
          Number(detail.packages) === 0 ? "" : detail.packages,
          formatted(detail.quantity, quantity),
          formatted(detail.price, money),
          formatted(detail.vatRate, money),
          formatted(detail.vatIncludedPrice, money),
          formatted(detail.amount, money)
        ];
        values.forEach(value => {
          const td = document.createElement("td");
          td.textContent = value ?? "";
          tr.appendChild(td);
        });
        fragment.appendChild(tr);
      });
      detailBody.replaceChildren(fragment);
    } catch (error) {
      if (error.name !== "AbortError") console.error("Aggiornamento dettaglio acquisti non riuscito", error);
    }
  };
  const ensurePurchaseVisible = (row, direction = 0) => {
    if (!purchaseGrid || !purchaseTable) return;
    const headerHeight = purchaseTable.tHead?.offsetHeight ?? 0;
    const visibleTop = purchaseGrid.scrollTop + headerHeight;
    const visibleBottom = purchaseGrid.scrollTop + purchaseGrid.clientHeight;
    const rowTop = row.offsetTop;
    const rowBottom = rowTop + row.offsetHeight;
    if (rowTop >= visibleTop && rowBottom <= visibleBottom) return;
    if (direction >= 0 && rowBottom > visibleBottom) {
      purchaseGrid.scrollTop = rowBottom - purchaseGrid.clientHeight + 1;
      return;
    }
    if (direction <= 0 && rowTop < visibleTop) {
      purchaseGrid.scrollTop = Math.max(rowTop - headerHeight - 1, 0);
    }
  };
  const select = (row, focus = true, direction = 0) => {
    rows.forEach(candidate => {
      const selected = candidate === row;
      candidate.classList.toggle("selected-row", selected);
      if (selected) candidate.setAttribute("aria-selected", "true");
      else candidate.removeAttribute("aria-selected");
    });
    if (focus) row.focus({ preventScroll: true });
    ensurePurchaseVisible(row, direction);
    loadDetails(row);
  };
  const open = row => { if (row?.dataset.url) window.location.href = row.dataset.url; };

  rows.forEach((row, index) => {
    row.addEventListener("click", () => select(row, true));
    row.addEventListener("dblclick", () => open(row));
    row.addEventListener("keydown", event => {
      let target = null;
      if (event.key === "Enter") { event.preventDefault(); open(row); return; }
      let direction = 0;
      if (event.key === "ArrowDown") { target = rows[Math.min(index + 1, rows.length - 1)]; direction = 1; }
      else if (event.key === "ArrowUp") { target = rows[Math.max(index - 1, 0)]; direction = -1; }
      else if (event.key === "Home") target = rows[0];
      else if (event.key === "End") target = rows[rows.length - 1];
      if (target) { event.preventDefault(); select(target, true, direction); }
    });
  });

  const purchaseViewport = () => {
    const gridRect = purchaseGrid.getBoundingClientRect();
    const headerHeight = purchaseTable?.tHead?.getBoundingClientRect().height ?? 0;
    return { top: gridRect.top + headerHeight, bottom: gridRect.bottom };
  };
  const purchaseRowIsVisible = (row, viewport) => {
    const rowRect = row.getBoundingClientRect();
    return rowRect.bottom > viewport.top + 1 && rowRect.top < viewport.bottom - 1;
  };
  const fullyVisiblePurchaseRows = viewport => rows.filter(row => {
    const rowRect = row.getBoundingClientRect();
    return rowRect.top >= viewport.top + 1 && rowRect.bottom <= viewport.bottom - 1;
  });
  let lastPurchaseScrollTop = purchaseGrid?.scrollTop ?? 0;
  let purchaseScrollFrame = 0;
  purchaseGrid?.addEventListener("scroll", () => {
    if (purchaseScrollFrame) window.cancelAnimationFrame(purchaseScrollFrame);
    purchaseScrollFrame = window.requestAnimationFrame(() => {
      purchaseScrollFrame = 0;
      const currentScrollTop = purchaseGrid.scrollTop;
      const delta = currentScrollTop - lastPurchaseScrollTop;
      lastPurchaseScrollTop = currentScrollTop;
      if (delta === 0) return;
      const viewport = purchaseViewport();
      const selected = selectedPurchaseRow();
      if (!selected || purchaseRowIsVisible(selected, viewport)) return;
      const visible = fullyVisiblePurchaseRows(viewport);
      if (visible.length === 0) return;
      select(delta > 0 ? visible[0] : visible[visible.length - 1], false);
    });
  }, { passive: true });
  window.setTimeout(() => {
    const selected = selectedPurchaseRow() || rows[0];
    if (selected) select(selected, true);
  }, 0);

  const balanceModal = document.querySelector("[data-daily-purchases-balance-modal]");
  const balanceOpen = page.querySelector("[data-daily-purchases-balance-open]");
  const balanceClose = balanceModal?.querySelector("[data-daily-purchases-balance-close]");
  const balanceRows = Array.from(balanceModal?.querySelectorAll(".daily-purchases-balance-grid tbody tr") ?? []);
  const balanceGridFrame = balanceModal?.querySelector(".daily-purchases-balance-grid-frame");
  const balanceTable = balanceGridFrame?.querySelector("table");
  let selectedBalanceIndex = Math.max(0, balanceRows.length - 1);
  const ensureBalanceRowVisible = (row, direction = 0) => {
    if (!balanceGridFrame || !balanceTable) return;
    const headerHeight = balanceTable.tHead?.offsetHeight ?? 0;
    const visibleTop = balanceGridFrame.scrollTop + headerHeight;
    const visibleBottom = balanceGridFrame.scrollTop + balanceGridFrame.clientHeight;
    const rowTop = row.offsetTop;
    const rowBottom = rowTop + row.offsetHeight;
    if (rowTop >= visibleTop && rowBottom <= visibleBottom) return;
    if (direction >= 0 && rowBottom > visibleBottom) {
      balanceGridFrame.scrollTop = rowBottom - balanceGridFrame.clientHeight + 1;
      return;
    }
    if (direction <= 0 && rowTop < visibleTop) {
      balanceGridFrame.scrollTop = Math.max(rowTop - headerHeight - 1, 0);
    }
  };
  const selectBalanceRow = (index, focus = false, direction = 0) => {
    if (balanceRows.length === 0) return;
    selectedBalanceIndex = Math.max(0, Math.min(index, balanceRows.length - 1));
    balanceRows.forEach((row, rowIndex) => {
      const selected = rowIndex === selectedBalanceIndex;
      row.classList.toggle("selected-row", selected);
      if (selected) row.setAttribute("aria-selected", "true");
      else row.removeAttribute("aria-selected");
    });
    const selectedRow = balanceRows[selectedBalanceIndex];
    if (focus) selectedRow.focus({ preventScroll: true });
    ensureBalanceRowVisible(selectedRow, direction);
  };
  balanceRows.forEach((row, index) => {
    row.addEventListener("click", () => selectBalanceRow(index, true));
    row.addEventListener("keydown", event => {
      let targetIndex = selectedBalanceIndex;
      let direction = 0;
      if (event.key === "ArrowDown") { targetIndex++; direction = 1; }
      else if (event.key === "ArrowUp") { targetIndex--; direction = -1; }
      else if (event.key === "Home") targetIndex = 0;
      else if (event.key === "End") targetIndex = balanceRows.length - 1;
      else return;
      event.preventDefault();
      selectBalanceRow(targetIndex, true, direction);
    });
  });
  const balanceViewport = () => {
    const gridRect = balanceGridFrame.getBoundingClientRect();
    const headerHeight = balanceTable?.tHead?.getBoundingClientRect().height ?? 0;
    return { top: gridRect.top + headerHeight, bottom: gridRect.bottom };
  };
  const balanceRowIsVisible = (row, viewport) => {
    const rowRect = row.getBoundingClientRect();
    return rowRect.bottom > viewport.top + 1 && rowRect.top < viewport.bottom - 1;
  };
  const fullyVisibleBalanceRows = viewport => balanceRows.filter(row => {
    const rowRect = row.getBoundingClientRect();
    return rowRect.top >= viewport.top + 1 && rowRect.bottom <= viewport.bottom - 1;
  });
  let lastBalanceScrollTop = balanceGridFrame?.scrollTop ?? 0;
  let balanceScrollFrame = 0;
  balanceGridFrame?.addEventListener("scroll", () => {
    if (balanceScrollFrame) window.cancelAnimationFrame(balanceScrollFrame);
    balanceScrollFrame = window.requestAnimationFrame(() => {
      balanceScrollFrame = 0;
      const currentScrollTop = balanceGridFrame.scrollTop;
      const delta = currentScrollTop - lastBalanceScrollTop;
      lastBalanceScrollTop = currentScrollTop;
      if (delta === 0) return;
      const viewport = balanceViewport();
      const selected = balanceRows[selectedBalanceIndex];
      if (!selected || balanceRowIsVisible(selected, viewport)) return;
      const visible = fullyVisibleBalanceRows(viewport);
      if (visible.length === 0) return;
      selectBalanceRow(balanceRows.indexOf(delta > 0 ? visible[0] : visible[visible.length - 1]));
    });
  }, { passive: true });
  const closeBalance = () => {
    if (!balanceModal) return;
    balanceModal.hidden = true;
    balanceOpen?.focus();
  };
  balanceOpen?.addEventListener("click", () => {
    if (!balanceModal) return;
    balanceModal.hidden = false;
    if (balanceRows.length > 0) {
      selectBalanceRow(balanceRows.length - 1, true, 1);
    } else {
      balanceModal.querySelector(".daily-purchases-balance-dialog")?.focus();
    }
  });
  balanceClose?.addEventListener("click", closeBalance);
  balanceModal?.addEventListener("click", event => {
    if (event.target === balanceModal) closeBalance();
  });
  document.addEventListener("keydown", event => {
    if (event.key === "Escape" && balanceModal && !balanceModal.hidden) closeBalance();
  });
});
