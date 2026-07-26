document.addEventListener("DOMContentLoaded", () => {
  const page = document.querySelector("[data-customer-supplier-balances]");
  const form = document.querySelector("[data-balance-filters]");
  const rows = Array.from(document.querySelectorAll("[data-balance-row]"));
  let selected = null;

  const selectRow = (row, focus = false) => {
    if (!row) return;
    rows.forEach((candidate) => candidate.classList.remove("selected", "selected-row"));
    row.classList.add("selected", "selected-row");
    selected = row;
    if (focus) row.focus({ preventScroll: true });
  };

  const openStatement = () => {
    if (!selected) {
      window.MicronoteMessageBox?.show({
        title: "Saldi clienti e fornitori",
        message: "Selezionare un cliente o un fornitore."
      });
      return;
    }
    const year = Number.parseInt(page?.dataset.year || "0", 10);
    const params = new URLSearchParams({
      partyType: selected.dataset.partyType || "",
      partyCode: selected.dataset.partyCode || "",
      dateFrom: `${year}-01-01`,
      dateTo: `${year}-12-31`
    });
    window.location.href = `/EstrattoContoClientiFornitori/Index?${params}`;
  };

  form?.querySelectorAll("select, input[type='checkbox']").forEach((field) => {
    field.addEventListener("change", () => form.requestSubmit());
  });

  document.querySelectorAll("[data-balance-table]").forEach((table) => {
    const headers = Array.from(table.querySelectorAll("th[data-sort-key]"));
    headers.forEach((header) => {
      header.tabIndex = 0;
      header.setAttribute("aria-sort", "none");

      const sort = () => {
        const key = header.dataset.sortKey || "";
        const numeric = header.dataset.sortType === "number";
        const direction = header.dataset.sortDirection === "asc" ? "desc" : "asc";
        const multiplier = direction === "asc" ? 1 : -1;
        const tableRows = Array.from(table.querySelectorAll("tbody [data-balance-row]"));

        tableRows
          .sort((left, right) => {
            const leftValue = left.dataset[`sort${key[0].toUpperCase()}${key.slice(1)}`] || "";
            const rightValue = right.dataset[`sort${key[0].toUpperCase()}${key.slice(1)}`] || "";
            if (numeric) {
              return (Number(leftValue) - Number(rightValue)) * multiplier;
            }
            return leftValue.localeCompare(rightValue, "it-IT", { numeric: true, sensitivity: "base" }) * multiplier;
          })
          .forEach((row) => table.tBodies[0].appendChild(row));

        headers.forEach((candidate) => {
          const active = candidate === header;
          candidate.classList.toggle("is-sorted", active);
          candidate.dataset.sortDirection = active ? direction : "";
          candidate.setAttribute("aria-sort", active ? (direction === "asc" ? "ascending" : "descending") : "none");
        });
      };

      header.addEventListener("click", sort);
      header.addEventListener("keydown", (event) => {
        if (event.key !== "Enter" && event.key !== " ") return;
        event.preventDefault();
        sort();
      });
    });
  });

  rows.forEach((row, index) => {
    row.addEventListener("click", () => selectRow(row, true));
    row.addEventListener("dblclick", openStatement);
    row.addEventListener("keydown", (event) => {
      if (event.key === "Enter") {
        event.preventDefault();
        openStatement();
        return;
      }
      if (event.key !== "ArrowDown" && event.key !== "ArrowUp") return;
      event.preventDefault();
      const next = Math.max(0, Math.min(rows.length - 1, index + (event.key === "ArrowDown" ? 1 : -1)));
      selectRow(rows[next], true);
      rows[next].scrollIntoView({ block: "nearest" });
    });
  });

  document.querySelector("[data-balance-action='statement']")?.addEventListener("click", openStatement);
  document.querySelector("[data-balance-action='print']")?.addEventListener("click", () => {
    window.MicronoteMessageBox?.show({ title: "Stampa", message: "Stampa saldi clienti e fornitori.", detail: "Funzione in preparazione." });
  });

  if (rows.length) selectRow(rows[0]);
});
