document.addEventListener("DOMContentLoaded", () => {
  const page = document.querySelector("[data-customer-note]");
  if (!page) return;
  const filterForm = page.querySelector("[data-customer-note-filters]");
  const rows = Array.from(page.querySelectorAll("[data-customer-note-row]"));
  const selectedRow = () => page.querySelector("[data-customer-note-row].selected");
  const summaryGrid = page.querySelector(".customer-note-summary-grid");
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

  const selectCustomer = (row) => {
    const params = new URLSearchParams(new FormData(filterForm));
    params.set("customerCode", row.dataset.customerCode || "");
    window.location.href = `${window.location.pathname}?${params}`;
  };

  filterForm?.querySelectorAll("input[name], select[name]").forEach(control =>
    control.addEventListener("change", () => filterForm.requestSubmit()));
  rows.forEach((row, index) => {
    row.addEventListener("click", () => selectCustomer(row));
    row.addEventListener("keydown", event => {
      if (event.key === "Enter") { event.preventDefault(); selectCustomer(row); return; }
      if (event.key !== "ArrowDown" && event.key !== "ArrowUp") return;
      event.preventDefault();
      const next = Math.max(0, Math.min(rows.length - 1, index + (event.key === "ArrowDown" ? 1 : -1)));
      rows[next].focus();
    });
  });

  page.querySelector("[data-customer-note-action='statement']")?.addEventListener("click", () => {
    const row = selectedRow();
    if (!row) return;
    const params = new URLSearchParams({ partyType: "C", partyCode: row.dataset.customerCode || "", dateFrom: filterForm.dateFrom.value, dateTo: filterForm.dateTo.value });
    window.location.href = `/EstrattoContoClientiFornitori/Index?${params}`;
  });
  page.querySelector("[data-customer-note-action='modify']")?.addEventListener("click", () => {
    const overlay = page.querySelector("[data-customer-note-discount]");
    if (overlay) {
      overlay.hidden = false;
      overlay.querySelector("[data-note-previous]")?.focus();
      overlay.querySelector("[data-note-previous]")?.select();
    }
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
  page.querySelector("[data-customer-note-discount-cancel]")?.addEventListener("click", () => {
    page.querySelector("[data-customer-note-discount]").hidden = true;
  });
  const discountOverlay = page.querySelector("[data-customer-note-discount]");
  const parseMoney = value => window.MicronoteMoney?.parse(value) ?? 0;
  const formatMoney = value => window.MicronoteMoney?.format(value) ?? "";
  const recalculateDiscount = () => {
    if (!discountOverlay) return;
    const merchandise = parseMoney(discountOverlay.querySelector("[data-note-merchandise]")?.value);
    const previous = parseMoney(discountOverlay.querySelector("[data-note-previous]")?.value);
    const paid = parseMoney(discountOverlay.querySelector("[data-note-paid]")?.value);
    const discount = parseMoney(discountOverlay.querySelector("[data-note-discount]")?.value);
    const updated = discountOverlay.querySelector("[data-note-updated]");
    if (updated) updated.value = formatMoney(previous + merchandise - paid - discount);
  };
  discountOverlay?.querySelectorAll("[data-note-previous], [data-note-paid], [data-note-discount]").forEach(input => {
    input.addEventListener("input", recalculateDiscount);
    input.addEventListener("change", recalculateDiscount);
  });
  document.addEventListener("keydown", event => {
    if (event.key !== "Escape" || !discountOverlay || discountOverlay.hidden) return;
    event.preventDefault();
    discountOverlay.hidden = true;
    page.querySelector("[data-customer-note-action='modify']")?.focus();
  }, true);
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
