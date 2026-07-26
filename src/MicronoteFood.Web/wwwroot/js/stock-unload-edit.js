document.addEventListener("DOMContentLoaded", () => {
  const form = document.querySelector("[data-unload-edit]");
  if (!form) return;

  const cause = form.querySelector("[data-unload-cause]");
  const supplier = form.querySelector("[data-unload-supplier]");
  const supplierCode = form.querySelector("[data-unload-supplier-code]");
  const supplierName = form.querySelector("[data-unload-supplier-name]");
  const supplierLookup = form.querySelector("[data-unload-supplier-lookup]");
  const article = form.querySelector("[data-unload-article]");
  const articleCode = form.querySelector("[data-unload-article-code]");
  const description = form.querySelector("[data-unload-description]");
  const unit = form.querySelector("[data-unload-unit]");
  const stock = form.querySelector("[data-unload-stock]");
  const quantity = form.querySelector("[data-decimal-field]");
  const movementDate = form.querySelector("[data-filter-date-display]");
  const movementDateValue = form.querySelector("[data-filter-date-hidden]");
  const saveButton = document.querySelector("[data-unload-save]");
  const lookup = document.querySelector("[data-unload-article-lookup]");
  const lookupFrame = lookup?.querySelector("[data-unload-article-lookup-frame]");
  const search = lookup?.querySelector("[data-unload-article-search]");
  const count = lookup?.querySelector("[data-unload-article-count]");
  const allRows = Array.from(lookup?.querySelectorAll("[data-unload-article-row]") ?? []);
  let visibleRows = allRows;
  let selectedIndex = -1;

  const updateCause = () => {
    const enabled = cause.value === "15";
    supplierCode.disabled = !enabled;
    supplierLookup.disabled = !enabled;
    if (!enabled) {
      supplier.value = "0";
      supplierCode.value = "";
      supplierName.value = "";
    }
  };

  const loadStock = async () => {
    if (!article.value) {
      stock.value = "";
      return;
    }
    const id = form.querySelector("[name='Document.Id']").value || 0;
    const response = await fetch(`?handler=Stock&articleCode=${encodeURIComponent(article.value)}&id=${id}`);
    const payload = await response.json();
    stock.value = Number(payload.stock || 0).toLocaleString("it-IT", {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    });
  };

  const applyArticle = async (row) => {
    if (!row) return;
    article.value = row.dataset.code || "";
    articleCode.value = article.value;
    description.value = row.dataset.description || "";
    unit.value = row.dataset.unit || "";
    await loadStock();
  };

  const setSelected = (index) => {
    if (!visibleRows.length) {
      selectedIndex = -1;
      return;
    }
    selectedIndex = Math.max(0, Math.min(index, visibleRows.length - 1));
    allRows.forEach(row => row.classList.remove("selected"));
    const row = visibleRows[selectedIndex];
    row.classList.add("selected");
    row.scrollIntoView({ block: "nearest" });
  };

  const filterRows = () => {
    const text = (search?.value || "").trim().toLocaleLowerCase("it");
    visibleRows = allRows.filter(row => {
      const visible = !text
        || (row.dataset.code || "").toLocaleLowerCase("it").includes(text)
        || (row.dataset.description || "").toLocaleLowerCase("it").includes(text);
      row.hidden = !visible;
      return visible;
    });
    if (count) count.value = String(visibleRows.length);
    setSelected(0);
  };

  const closeLookup = () => {
    if (!lookup) return;
    lookup.hidden = true;
    document.body.classList.remove("lookup-open");
    articleCode.focus();
  };

  const openLookup = () => {
    if (!lookup) return;
    lookup.hidden = false;
    document.body.classList.add("lookup-open");
    search.value = "";
    filterRows();
    const current = visibleRows.findIndex(row => row.dataset.code === article.value);
    if (current >= 0) setSelected(current);
    search.focus();
  };

  const confirmLookup = async () => {
    const row = visibleRows[selectedIndex];
    if (!row) return;
    await applyArticle(row);
    closeLookup();
  };

  const findTypedArticle = async () => {
    const code = articleCode.value.trim();
    const row = allRows.find(candidate =>
      (candidate.dataset.code || "").localeCompare(code, "it", { sensitivity: "base" }) === 0);
    if (row) {
      await applyArticle(row);
      return;
    }
    article.value = code;
    description.value = "";
    unit.value = "";
    stock.value = "";
  };

  const decimalComma = () => {
    if (quantity?.value.includes(".")) quantity.value = quantity.value.replace(".", ",");
  };

  const validationError = (message, field) => {
    window.MicronoteMessageBox?.show({
      title: "Scarico per perdite e resi",
      message,
      variant: "error"
    });
    field?.focus();
    return false;
  };

  const validate = () => {
    if (!articleCode.value.trim()) {
      return validationError("Il codice articolo è obbligatorio.", articleCode);
    }
    const quantityValue = window.MicronoteDecimal?.parse(quantity.value) ?? Number(quantity.value.replace(",", "."));
    if (!Number.isFinite(quantityValue) || quantityValue <= 0) {
      return validationError("La quantità è obbligatoria e deve essere maggiore di zero.", quantity);
    }
    const dateMatch = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(movementDate.value.trim());
    const exercise = Number(form.querySelector("[name='Document.Year']").value);
    if (!dateMatch || Number(dateMatch[3]) !== exercise) {
      return validationError(
        `La data movimento deve rientrare nell'esercizio contabile in linea (${exercise}).`,
        movementDate);
    }
    if (cause.value === "15" && Number(supplier.value || 0) <= 0) {
      return validationError("Il fornitore è obbligatorio per il reso a fornitore.", supplierCode);
    }
    return true;
  };

  cause.addEventListener("change", updateCause);
  saveButton?.addEventListener("click", () => {
    if (!validate()) return;

    article.value = articleCode.value.trim();
    const dateMatch = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(movementDate.value.trim());
    if (dateMatch) {
      movementDateValue.value = `${dateMatch[3]}-${dateMatch[2]}-${dateMatch[1]}`;
    }
    if (window.MicronoteDecimal?.parse) {
      const parsedQuantity = window.MicronoteDecimal.parse(quantity.value);
      if (Number.isFinite(parsedQuantity)) quantity.value = String(parsedQuantity).replace(".", ",");
    }

    saveButton.disabled = true;
    window.MicronoteProgress?.show?.("Salvataggio in corso...");
    HTMLFormElement.prototype.submit.call(form);
  });
  articleCode.addEventListener("change", findTypedArticle);
  articleCode.addEventListener("blur", findTypedArticle);
  document.querySelector("[data-unload-article-lookup-open]")?.addEventListener("click", openLookup);
  lookup?.querySelector("[data-unload-article-ok]")?.addEventListener("click", confirmLookup);
  lookup?.querySelector("[data-unload-article-cancel]")?.addEventListener("click", closeLookup);
  search?.addEventListener("input", filterRows);
  search?.addEventListener("keydown", event => {
    if (event.key === "ArrowDown") { event.preventDefault(); setSelected(selectedIndex + 1); }
    else if (event.key === "ArrowUp") { event.preventDefault(); setSelected(selectedIndex - 1); }
    else if (event.key === "Enter") { event.preventDefault(); confirmLookup(); }
    else if (event.key === "Escape") { event.preventDefault(); closeLookup(); }
  });
  lookupFrame?.addEventListener("keydown", event => {
    if (event.key === "ArrowDown") { event.preventDefault(); setSelected(selectedIndex + 1); }
    else if (event.key === "ArrowUp") { event.preventDefault(); setSelected(selectedIndex - 1); }
    else if (event.key === "Enter") { event.preventDefault(); confirmLookup(); }
    else if (event.key === "Escape") { event.preventDefault(); closeLookup(); }
  });
  allRows.forEach(row => {
    row.addEventListener("click", () => setSelected(visibleRows.indexOf(row)));
    row.addEventListener("dblclick", confirmLookup);
  });
  quantity?.addEventListener("focus", decimalComma);
  quantity?.addEventListener("input", decimalComma);
  updateCause();
});
