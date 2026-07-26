document.addEventListener("DOMContentLoaded", () => {
  const page = document.querySelector("[data-trial-balance]");
  if (!page) return;

  const rows = Array.from(page.querySelectorAll("[data-trial-row]"));
  const preview = page.querySelector("[data-trial-print-preview]");
  const previewDocument = preview?.querySelector("[data-trial-preview-document]");
  const previewSummary = preview?.querySelector("[data-trial-print-summary]");
  const previewZoomLabel = preview?.querySelector("[data-trial-preview-zoom-label]");
  let previewZoom = 1;

  const selectedRow = () => page.querySelector("[data-trial-row].selected-row");
  const selectRow = (row, focus = false) => {
    if (!row) return;
    rows.forEach((candidate) => candidate.classList.remove("selected-row"));
    row.classList.add("selected-row");
    if (focus) row.focus({ preventScroll: true });
  };

  const openAccount = () => {
    const row = selectedRow();
    if (!row) {
      window.MicronoteMessageBox?.show({ message: "Selezionare un conto.", variant: "info" });
      return;
    }
    const year = Number.parseInt(page.dataset.year ?? "", 10);
    const returnUrl = `/BilancioVerifica?run=true&year=${year}`;
    window.location.href = `/SchedaContabile/Index?accountCode=${encodeURIComponent(row.dataset.accountCode)}&dateFrom=${year}-01-01&dateTo=${year}-12-31&returnUrl=${encodeURIComponent(returnUrl)}`;
  };

  rows.forEach((row) => {
    row.addEventListener("click", () => selectRow(row, true));
    row.addEventListener("dblclick", () => { selectRow(row); openAccount(); });
  });

  const sectionData = () => Array.from(page.querySelectorAll(".trial-balance-panel")).map((panel) => ({
    title: panel.querySelector("h2")?.textContent.trim() ?? "",
    headers: Array.from(panel.querySelectorAll("thead th")).map((cell) => cell.textContent.trim()),
    rows: Array.from(panel.querySelectorAll("tbody tr")).map((row) =>
      Array.from(row.cells).map((cell) => cell.textContent.trim())),
    footers: Array.from(panel.querySelectorAll("tfoot tr")).map((row) =>
      Array.from(row.cells).map((cell) => cell.textContent.trim()))
  }));

  const updatePreviewZoom = () => {
    previewDocument?.style.setProperty("--report-preview-zoom", String(previewZoom));
    if (previewZoomLabel) previewZoomLabel.textContent = `${Math.round(previewZoom * 100)}%`;
  };
  const closePreview = () => { if (preview) preview.hidden = true; };

  const appendReportPage = (section, pageRows, pageNumber, pageCount, includeFooter) => {
    const sheet = document.createElement("article");
    sheet.className = "supplier-report-page trial-balance-report-page";
    sheet.classList.add(section.headers.length === 4 ? "trial-equity-report-page" : "trial-income-report-page");
    const header = document.createElement("header");
    header.innerHTML = `<div><strong>BILANCIO DI VERIFICA - Esercizio contabile: ${page.dataset.year}</strong><span>${section.title}</span></div><small>data di stampa: ${new Intl.DateTimeFormat("it-IT").format(new Date())}<br>Pagina ${pageNumber} di ${pageCount}</small>`;
    sheet.appendChild(header);
    const table = document.createElement("table");
    const headRow = table.createTHead().insertRow();
    section.headers.forEach((value) => {
      const cell = document.createElement("th");
      cell.textContent = value;
      headRow.appendChild(cell);
    });
    const body = table.createTBody();
    pageRows.forEach((values) => {
      const row = body.insertRow();
      values.forEach((value) => { row.insertCell().textContent = value; });
    });
    if (includeFooter && section.footers.length) {
      const footer = table.createTFoot();
      section.footers.forEach((values) => {
        const row = footer.insertRow();
        values.forEach((value) => { row.insertCell().textContent = value; });
      });
    }
    sheet.appendChild(table);
    previewDocument.appendChild(sheet);
  };

  const buildPreview = () => {
    if (!preview || !previewDocument) return;
    const sections = sectionData();
    if (!sections.some((section) => section.rows.length > 0)) {
      window.MicronoteMessageBox?.show({ message: "Elaborare il bilancio prima della stampa.", variant: "info" });
      return;
    }
    previewDocument.replaceChildren();
    const prepared = sections.map((section) => ({ section, pageCount: Math.max(1, Math.ceil(section.rows.length / 35)) }));
    const totalPages = prepared.reduce((total, item) => total + item.pageCount, 0);
    let documentPage = 0;
    prepared.forEach(({ section, pageCount }) => {
      for (let index = 0; index < pageCount; index += 1) {
        documentPage += 1;
        appendReportPage(section, section.rows.slice(index * 35, (index + 1) * 35), documentPage, totalPages, index === pageCount - 1);
      }
    });
    if (previewSummary) previewSummary.textContent = `${rows.length} record · ${totalPages} pagine`;
    previewZoom = 0.85;
    updatePreviewZoom();
    preview.hidden = false;
    preview.querySelector("[data-trial-preview-close]")?.focus();
  };

  const downloadPdf = async () => {
    const token = page.querySelector('input[name="__RequestVerificationToken"]')?.value;
    const button = preview?.querySelector("[data-trial-preview-pdf]");
    if (!token || !button) return;
    button.disabled = true;
    try {
      const response = await fetch("?handler=Pdf", {
        method: "POST",
        headers: { "Content-Type": "application/json", "RequestVerificationToken": token },
        body: JSON.stringify({ year: Number.parseInt(page.dataset.year, 10) })
      });
      if (!response.ok) throw new Error("Creazione PDF non riuscita.");
      const url = URL.createObjectURL(await response.blob());
      const link = document.createElement("a");
      link.href = url;
      link.download = `Bilancio_${page.dataset.year}.pdf`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      window.MicronoteMessageBox?.show({ message: error.message, variant: "error" });
    } finally {
      button.disabled = false;
    }
  };

  const downloadExcel = () => {
    const quote = (value) => `"${String(value).replaceAll('"', '""')}"`;
    const lines = [["BILANCIO DI VERIFICA", `Esercizio ${page.dataset.year}`]];
    sectionData().forEach((section) => {
      lines.push([], [section.title], section.headers, ...section.rows, ...section.footers);
    });
    const csv = `\uFEFF${lines.map((line) => line.map(quote).join(";")).join("\r\n")}`;
    const url = URL.createObjectURL(new Blob([csv], { type: "text/csv;charset=utf-8" }));
    const link = document.createElement("a");
    link.href = url;
    link.download = `Bilancio_${page.dataset.year}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  };

  page.querySelector('[data-trial-action="account"]')?.addEventListener("click", openAccount);
  page.querySelector('[data-trial-action="print"]')?.addEventListener("click", buildPreview);
  preview?.querySelector("[data-trial-preview-close]")?.addEventListener("click", closePreview);
  preview?.querySelector("[data-trial-preview-zoom-out]")?.addEventListener("click", () => { previewZoom = Math.max(0.5, previewZoom - 0.1); updatePreviewZoom(); });
  preview?.querySelector("[data-trial-preview-zoom-in]")?.addEventListener("click", () => { previewZoom = Math.min(1.5, previewZoom + 0.1); updatePreviewZoom(); });
  preview?.querySelector("[data-trial-preview-print]")?.addEventListener("click", () => {
    const pageStyle = document.createElement("style");
    pageStyle.id = "trial-balance-print-page-style";
    pageStyle.textContent = "@page { size: A4 portrait; margin: 0; }";
    document.head.appendChild(pageStyle);
    document.body.classList.add("is-printing-supplier-report");
    window.print();
  });
  preview?.querySelector("[data-trial-preview-pdf]")?.addEventListener("click", downloadPdf);
  preview?.querySelector("[data-trial-preview-excel]")?.addEventListener("click", downloadExcel);
  window.addEventListener("afterprint", () => {
    document.body.classList.remove("is-printing-supplier-report");
    document.querySelector("#trial-balance-print-page-style")?.remove();
  });

  document.addEventListener("keydown", (event) => {
    if (event.key !== "Escape" || event.defaultPrevented) return;
    event.preventDefault();
    if (preview && !preview.hidden) { closePreview(); return; }
    window.location.href = page.dataset.menuUrl || "/";
  });
});
