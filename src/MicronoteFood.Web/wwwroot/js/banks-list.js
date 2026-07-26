document.addEventListener("DOMContentLoaded", () => {
  const page = document.querySelector("[data-bank-list]");
  if (!page) return;
  const rows = Array.from(page.querySelectorAll("[data-bank-row]"));
  const search = page.querySelector("[data-bank-search]");
  const clear = page.querySelector("[data-bank-clear]");
  const count = page.querySelector("[data-bank-count]");
  const empty = page.querySelector("[data-bank-empty]");
  const form = page.querySelector("[data-bank-delete-form]");
  const code = form?.querySelector("[data-bank-delete-code]");
  const selected = () => page.querySelector("[data-bank-row].selected-row");
  const choose = (row, focus = false) => {
    rows.forEach(candidate => candidate.classList.remove("selected-row"));
    if (!row) return;
    row.classList.add("selected-row");
    if (focus) row.focus();
  };
  const requireSelection = () => {
    const row = selected();
    if (!row) window.MicronoteMessageBox?.show({ message: "Selezionare una banca.", variant: "info" });
    return row;
  };
  const edit = () => { const row = requireSelection(); if (row) window.location.href = row.dataset.editUrl; };
  const filter = () => {
    const needle = (search?.value || "").trim().toLocaleLowerCase("it");
    let visible = 0;
    rows.forEach(row => { row.hidden = needle !== "" && !row.dataset.filter.toLocaleLowerCase("it").includes(needle); if (!row.hidden) visible += 1; });
    if (count) count.textContent = String(visible);
    if (clear) clear.hidden = !search.value;
    if (empty) empty.hidden = visible !== 0;
    if (!selected() || selected().hidden) choose(rows.find(row => !row.hidden));
  };
  rows.forEach(row => {
    row.addEventListener("click", () => choose(row, true));
    row.addEventListener("dblclick", edit);
    row.addEventListener("keydown", event => {
      const visible = rows.filter(candidate => !candidate.hidden);
      const position = visible.indexOf(row);
      if (event.key === "Enter") { event.preventDefault(); edit(); return; }
      if (event.key !== "ArrowDown" && event.key !== "ArrowUp") return;
      event.preventDefault();
      choose(visible[Math.max(0, Math.min(visible.length - 1, position + (event.key === "ArrowDown" ? 1 : -1)))], true);
    });
  });
  page.querySelectorAll("[data-bank-action]").forEach(button => button.addEventListener("click", () => {
    const action = button.dataset.bankAction;
    const row = requireSelection();
    if (!row) return;
    if (action === "edit") edit();
    else if (action === "balance") window.location.href = row.dataset.balanceUrl;
    else if (action === "delete") window.MicronoteMessageBox?.show({
      mode: "confirm", variant: "confirm", message: "Eliminare la banca " + row.dataset.label + "?",
      detail: "L'operazione non potrà essere annullata.", okText: "Elimina",
      onConfirm: () => { code.value = row.dataset.code; form.submit(); }
    });
  }));
  page.querySelectorAll("[data-page-message]").forEach(message => window.MicronoteMessageBox?.show({ message: message.dataset.messageText, variant: "error" }));
  search?.addEventListener("input", filter);
  clear?.addEventListener("click", () => { search.value = ""; search.focus(); filter(); });
  document.addEventListener("keydown", event => { if (event.key === "Escape" && !event.defaultPrevented) window.location.href = page.dataset.menuUrl || "/"; });
  filter(); choose(rows.find(row => !row.hidden));
});
