document.addEventListener("DOMContentLoaded", () => {
  const page = document.querySelector("[data-daily-movements]");
  const form = page?.querySelector("[data-daily-movements-form]");
  if (!page || !form) return;
  const type = page.querySelector("[data-daily-movements-type]");
  const articleField = page.querySelector("[data-daily-movements-article-field]");
  const comboFields = Array.from(page.querySelectorAll("[data-daily-movements-combo]"));
  const showSelection = () => {
    const articleActive = type?.value === "articolo";
    const articleCode = articleField?.querySelector("[data-daily-movements-article-code]");
    if (articleCode) articleCode.readOnly = !articleActive;
    if (!articleActive) {
      if (articleCode) articleCode.value = "";
      const storedArticleCode = page.querySelector("#dailyMovementArticleCode");
      const articleDescription = page.querySelector("[data-daily-movements-article-description]");
      if (storedArticleCode) storedArticleCode.value = "";
      if (articleDescription) articleDescription.value = "";
    }
    articleField?.querySelectorAll("button").forEach(control => control.disabled = !articleActive);
    articleField?.classList.toggle("is-disabled", !articleActive);
    comboFields.forEach(field => {
      const active = field.dataset.dailyMovementsCombo === type?.value;
      field.hidden = !active;
      field.querySelectorAll("select").forEach(control => control.disabled = !active || articleActive);
    });
  };
  type?.addEventListener("change", showSelection);
  const articleSelect = articleField?.querySelector("[data-daily-movements-article-code]");
  const articleDescription = page.querySelector("[data-daily-movements-article-description]");
  page.querySelector("[data-daily-movements-article-clear]")?.addEventListener("click", () => {
    if (articleSelect) articleSelect.value = "";
    const articleCode = page.querySelector("#dailyMovementArticleCode");
    if (articleCode) articleCode.value = "";
    if (articleDescription) articleDescription.value = "";
    articleSelect?.focus();
  });
  const dateFrom = form.querySelector("input[name='dateFrom']");
  const dateTo = form.querySelector("input[name='dateTo']");
  const dateToDisplay = dateTo?.closest("[data-date-control]")?.querySelector("[data-filter-date-display]");
  dateFrom?.addEventListener("change", () => {
    if (!dateTo || !dateToDisplay || !dateFrom.value) return;
    dateTo.value = dateFrom.value;
    const [year, month, day] = dateFrom.value.split("-");
    dateToDisplay.value = `${day}/${month}/${year}`;
  });
  showSelection();
  const validateSelection = () => {
    const selected = type?.value === "articolo"
      ? articleSelect
      : comboFields.find(field => !field.hidden)?.querySelector("select:not(:disabled)");
    if (selected?.value?.trim()) return true;
    const focusFilter = () => selected?.focus();
    if (window.MicronoteMessageBox?.show) {
      window.MicronoteMessageBox.show({
        title: "Movimenti del giorno",
        message: "Selezionare un articolo o un raggruppamento.",
        variant: "error",
        okText: "OK",
        onConfirm: focusFilter
      });
    } else {
      window.alert("Selezionare un articolo o un raggruppamento.");
      focusFilter();
    }
    return false;
  };
  page.querySelector("[data-daily-movements-refresh]")?.addEventListener("click", () => {
    if (validateSelection()) form.requestSubmit();
  });
  form.addEventListener("submit", event => {
    if (validateSelection()) return;
    event.preventDefault();
  });

  const frame = page.querySelector(".daily-movements-grid-frame");
  const table = frame?.querySelector("table");
  const rows = Array.from(table?.querySelectorAll("tbody tr:not(.daily-movements-section):not(.daily-movements-spacer)") ?? []);
  if (!frame || !table || rows.length === 0) return;
  let selectedIndex = 0;
  const ensureVisible = (row, direction = 0) => {
    const header = table.tHead?.offsetHeight ?? 0;
    const top = frame.scrollTop + header;
    const bottom = frame.scrollTop + frame.clientHeight;
    const rowTop = row.offsetTop;
    const rowBottom = rowTop + row.offsetHeight;
    if (rowTop >= top && rowBottom <= bottom) return;
    if (direction >= 0 && rowBottom > bottom) frame.scrollTop = rowBottom - frame.clientHeight + 1;
    else frame.scrollTop = Math.max(rowTop - header - 1, 0);
  };
  const select = (index, focus = false, direction = 0) => {
    selectedIndex = Math.max(0, Math.min(index, rows.length - 1));
    rows.forEach((row, current) => row.classList.toggle("selected-row", current === selectedIndex));
    if (focus) rows[selectedIndex].focus({ preventScroll: true });
    ensureVisible(rows[selectedIndex], direction);
  };
  rows.forEach((row, index) => {
    row.addEventListener("click", () => select(index, true));
    row.addEventListener("keydown", event => {
      let target = selectedIndex; let direction = 0;
      if (event.key === "ArrowDown") { target++; direction = 1; }
      else if (event.key === "ArrowUp") { target--; direction = -1; }
      else if (event.key === "Home") target = 0;
      else if (event.key === "End") target = rows.length - 1;
      else return;
      event.preventDefault(); select(target, true, direction);
    });
  });
  select(0, true);
});
