document.addEventListener("DOMContentLoaded", () => {
  const page = document.querySelector("[data-warehouse-statistics]");
  if (!page) return;

  const form = page.querySelector("#warehouse-statistics-filters");
  const mainFrame = page.querySelector("[data-warehouse-statistics-main-grid]");
  const customersFrame = page.querySelector("[data-warehouse-statistics-customers-grid]");
  const customersBody = page.querySelector("[data-warehouse-statistics-customers]");
  if (!form || !mainFrame || !customersFrame || !customersBody) return;

  const money = (value) => Number(value ?? 0).toLocaleString("it-IT", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  const quantity = (value) => Number(value ?? 0).toLocaleString("it-IT", { minimumFractionDigits: 3, maximumFractionDigits: 3 });
  const escapeHtml = (value) => String(value ?? "").replace(/[&<>"']/g, (character) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#039;" })[character]);
  let requestId = 0;

  const rowsFor = (kind) => kind === "main"
    ? Array.from(page.querySelectorAll("[data-warehouse-statistics-row]"))
    : Array.from(customersBody.querySelectorAll("[data-warehouse-statistics-customer-row]"));
  const frameFor = (kind) => kind === "main" ? mainFrame : customersFrame;
  const selectedRow = (kind) => rowsFor(kind).find((row) => row.classList.contains("selected-row"));
  const mainTable = mainFrame.querySelector(".warehouse-statistics-grid");
  const mainBody = mainTable?.tBodies[0];
  let sortColumn = -1;
  let sortDirection = "asc";

  const ensureVisible = (frame, row, direction = 0) => {
    const table = row.closest("table");
    const headerHeight = table?.tHead?.offsetHeight ?? 0;
    const visibleTop = frame.scrollTop + headerHeight;
    const visibleBottom = frame.scrollTop + frame.clientHeight;
    const rowTop = row.offsetTop;
    const rowBottom = rowTop + row.offsetHeight;
    if (rowTop >= visibleTop && rowBottom <= visibleBottom) return;
    if (direction >= 0 && rowBottom > visibleBottom) {
      frame.scrollTop = rowBottom - frame.clientHeight + 1;
    } else if (direction <= 0 && rowTop < visibleTop) {
      frame.scrollTop = Math.max(rowTop - headerHeight - 1, 0);
    }
  };

  const selectRow = (kind, row, focus = false, direction = 0) => {
    if (!row) return;
    rowsFor(kind).forEach((item) => { item.classList.remove("selected-row"); item.removeAttribute("aria-selected"); });
    row.classList.add("selected-row");
    row.setAttribute("aria-selected", "true");
    if (focus) row.focus({ preventScroll: true });
    ensureVisible(frameFor(kind), row, direction);
  };

  const selectCustomer = (row, focus = false, direction = 0) => selectRow("customers", row, focus, direction);

  const loadCustomers = async (row) => {
    customersBody.replaceChildren();
    if (!row) return;
    const currentRequest = ++requestId;
    const parameters = new URLSearchParams(new FormData(form));
    parameters.set("handler", "Customers");
    parameters.set("code", row.dataset.code ?? "");
    try {
      const response = await fetch(`${window.location.pathname}?${parameters.toString()}`, { headers: { Accept: "application/json" } });
      if (!response.ok) throw new Error("Impossibile caricare il dettaglio clienti.");
      const data = await response.json();
      if (currentRequest !== requestId) return;
      customersBody.innerHTML = data.map((item) => `<tr tabindex="0" data-warehouse-statistics-customer-row><td>${escapeHtml(String(item.code ?? "").padStart(5, "0"))}</td><td>${escapeHtml(item.name)}</td><td class="number">${quantity(item.quantity)}</td><td class="number">${money(item.amount)}</td></tr>`).join("");
      selectCustomer(rowsFor("customers")[0]);
    } catch (error) {
      if (currentRequest === requestId) customersBody.innerHTML = `<tr><td colspan="4">${escapeHtml(error.message)}</td></tr>`;
    }
  };

  const selectMain = (row, focus = false, direction = 0) => {
    selectRow("main", row, focus, direction);
    loadCustomers(row);
  };

  const navigate = (kind, event, callback) => {
    const rows = rowsFor(kind);
    if (!rows.length) return;
    const current = selectedRow(kind) || rows[0];
    const index = Math.max(rows.indexOf(current), 0);
    let target = null;
    let direction = 0;
    if (event.key === "ArrowDown") { target = rows[Math.min(index + 1, rows.length - 1)]; direction = 1; }
    else if (event.key === "ArrowUp") { target = rows[Math.max(index - 1, 0)]; direction = -1; }
    else if (event.key === "Home") { target = rows[0]; direction = -1; }
    else if (event.key === "End") { target = rows[rows.length - 1]; direction = 1; }
    else return;
    event.preventDefault();
    callback(target, true, direction);
  };

  const bindFrame = (kind, frame, rowSelector, callback) => {
    frame.addEventListener("click", (event) => {
      const row = event.target.closest(rowSelector);
      if (row) callback(row, true);
    });
    frame.addEventListener("focusin", (event) => {
      const row = event.target.closest(rowSelector);
      if (row && row !== selectedRow(kind)) callback(row);
    });
    frame.addEventListener("keydown", (event) => navigate(kind, event, callback));
    let lastScrollTop = frame.scrollTop;
    let scrollFrame = 0;
    frame.addEventListener("scroll", () => {
      if (scrollFrame) window.cancelAnimationFrame(scrollFrame);
      scrollFrame = window.requestAnimationFrame(() => {
        scrollFrame = 0;
        const delta = frame.scrollTop - lastScrollTop;
        lastScrollTop = frame.scrollTop;
        if (!delta) return;
        const selected = selectedRow(kind);
        if (!selected) return;
        const table = selected.closest("table");
        const headerHeight = table?.tHead?.offsetHeight ?? 0;
        const top = frame.scrollTop + headerHeight;
        const bottom = frame.scrollTop + frame.clientHeight;
        if (selected.offsetTop >= top && selected.offsetTop + selected.offsetHeight <= bottom) return;
        const visible = rowsFor(kind).filter((row) => row.offsetTop >= top && row.offsetTop + row.offsetHeight <= bottom);
        if (visible.length) callback(delta > 0 ? visible[0] : visible[visible.length - 1]);
      });
    }, { passive: true });
  };

  bindFrame("main", mainFrame, "[data-warehouse-statistics-row]", selectMain);
  bindFrame("customers", customersFrame, "[data-warehouse-statistics-customer-row]", selectCustomer);

  const sortMain = (header) => {
    if (!mainBody || !mainTable) return;
    const column = Array.from(header.parentElement?.children ?? []).indexOf(header);
    if (column < 0) return;
    sortDirection = sortColumn === column && sortDirection === "asc" ? "desc" : "asc";
    sortColumn = column;
    const multiplier = sortDirection === "asc" ? 1 : -1;
    const type = header.dataset.sortType ?? "text";
    Array.from(mainBody.querySelectorAll("[data-warehouse-statistics-row]"))
      .sort((left, right) => {
        const leftValue = left.cells[column]?.dataset.sortValue ?? "";
        const rightValue = right.cells[column]?.dataset.sortValue ?? "";
        if (type === "number") return (Number(leftValue) - Number(rightValue)) * multiplier;
        return leftValue.localeCompare(rightValue, "it", { sensitivity: "base", numeric: true }) * multiplier;
      })
      .forEach((row) => mainBody.appendChild(row));
    Array.from(mainTable.tHead?.querySelectorAll("th[data-sort-type]") ?? []).forEach((item) => {
      item.setAttribute("aria-sort", item === header ? (sortDirection === "asc" ? "ascending" : "descending") : "none");
    });
    const current = selectedRow("main");
    if (current) ensureVisible(mainFrame, current);
  };

  mainTable?.querySelectorAll("th[data-sort-type]").forEach((header) => {
    header.tabIndex = 0;
    header.setAttribute("role", "button");
    header.setAttribute("aria-sort", "none");
    header.addEventListener("click", () => sortMain(header));
    header.addEventListener("keydown", (event) => {
      if (event.key !== "Enter" && event.key !== " ") return;
      event.preventDefault();
      sortMain(header);
    });
  });

  if (rowsFor("main").length) selectMain(rowsFor("main")[0]);
});
