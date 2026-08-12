document.addEventListener("DOMContentLoaded", () => {
  const page = document.querySelector("[data-group-purchase-stats]");
  if (!page) return;

  const form = page.querySelector("[data-group-purchase-filters]");
  const rows = Array.from(page.querySelectorAll("[data-group-purchase-row]"));
  const articleBody = page.querySelector("[data-group-purchase-articles]");
  const supplierBody = page.querySelector("[data-group-purchase-suppliers]");
  const grouping = page.querySelector("[data-group-purchase-grouping]");
  let requestId = 0;

  const money = (value) => {
    const number = Number(value ?? 0);
    return number === 0 ? "" : number.toLocaleString("it-IT", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  };

  const select = async (row) => {
    rows.forEach((item) => item.classList.toggle("selected-row", item === row));
    if (!row || !articleBody || !supplierBody || !form) return;
    const currentRequest = ++requestId;
    articleBody.innerHTML = "";
    supplierBody.innerHTML = "";
    if (Number(row.dataset.code) <= 0) return;

    const parameters = new URLSearchParams(new FormData(form));
    parameters.set("handler", "Details");
    parameters.set("code", row.dataset.code ?? "0");
    try {
      const response = await fetch(`${window.location.pathname}?${parameters.toString()}`, {
        headers: { Accept: "application/json" }
      });
      if (!response.ok) throw new Error("Impossibile caricare il dettaglio.");
      const data = await response.json();
      if (currentRequest !== requestId) return;
      articleBody.innerHTML = (data.articles ?? []).map((item) => `
        <tr><td>${item.code ?? ""}</td><td>${item.description ?? ""}</td><td class="number">${money(item.amount)}</td></tr>
      `).join("");
      supplierBody.innerHTML = (data.suppliers ?? []).map((item) => `
        <tr><td>${String(item.code ?? 0).padStart(5, "0")}</td><td>${item.name ?? ""}</td><td class="number">${money(item.amount)}</td></tr>
      `).join("");
    } catch (error) {
      if (currentRequest !== requestId) return;
      articleBody.innerHTML = `<tr><td colspan="3">${error.message}</td></tr>`;
    }
  };

  rows.forEach((row, index) => {
    row.addEventListener("click", () => select(row));
    row.addEventListener("focus", () => select(row));
    row.addEventListener("keydown", (event) => {
      if (event.key !== "ArrowDown" && event.key !== "ArrowUp") return;
      event.preventDefault();
      const nextIndex = Math.max(0, Math.min(rows.length - 1, index + (event.key === "ArrowDown" ? 1 : -1)));
      rows[nextIndex]?.focus();
    });
  });

  if (rows.length) {
    select(rows[0]);
  }
});
