(() => {
  const root = document.querySelector("[data-sales-document]");
  if (!root) return;
  const $ = selector => root.querySelector(selector);
  const source = JSON.parse($("[data-sales-document-data]").textContent);
  const articles = source.articles || [];
  const state = {
    id: source.document.id,
    rows: (source.document.rows || []).map(row => ({ ...row })),
    selected: -1,
    editing: -1
  };
  const parse = value => Number(String(value ?? "")
    .replace(/\s/g, "").replace(/%/g, "").replace(/\./g, "").replace(",", ".")) || 0;
  const parseControlledDecimal = value =>
    window.MicronoteDecimal?.parse?.(value) ?? parse(value);
  const format = (value, digits) => Number(value || 0).toLocaleString("it-IT", {
    minimumFractionDigits: digits,
    maximumFractionDigits: digits
  });
  const effectiveQuantity = row => row.quantity !== 0 ? row.quantity : row.packages;
  const rowNet = row => row.vatRate === 0
    ? row.amount
    : row.amount / (1 + row.vatRate / 100);
  const calculateAmount = row =>
    Math.round(effectiveQuantity(row) * row.price * (1 + row.vatRate / 100) * 100) / 100;

  const saveDocument = async () => {
    const customerCode = Number($("#salesDocumentCustomer").value) || 0;
    if (customerCode <= 0 || state.rows.length === 0) {
      window.MicronoteMessageBox?.show({
        title: "Bolla di vendita",
        message: customerCode <= 0 ? "Selezionare il cliente." : "Inserire almeno una riga articolo."
      });
      return;
    }

    const payload = JSON.stringify({
      id: state.id,
      customerCode,
      storeCode: Number($("[data-sales-document-store]").value) || 0,
      documentDate: $("[data-sales-document-date]").value,
      discount: parse($("[data-sales-discount]").value),
      paid: parse($("[data-sales-paid]").value),
      rows: state.rows
    });
    const form = $("[data-sales-document-form]");
    const formData = new FormData(form);
    formData.set("SavePayload", payload);
    const saveButton = $("[data-sales-document-save]");
    saveButton.disabled = true;
    window.MicronoteProgress?.show?.("Salvataggio in corso...");

    try {
      const response = await fetch(window.location.href, {
        method: "POST",
        body: formData,
        headers: { Accept: "text/html" }
      });
      if (!response.ok) {
        const message = (await response.text()).trim();
        window.MicronoteMessageBox?.show({
          title: "Bolla di vendita",
          message: message || "Salvataggio non riuscito."
        });
        return;
      }

      window.location.assign(response.url);
    } catch {
      window.MicronoteMessageBox?.show({
        title: "Bolla di vendita",
        message: "Non è stato possibile salvare la vendita."
      });
    } finally {
      window.MicronoteProgress?.hide?.();
      saveButton.disabled = false;
    }
  };
  $("[data-sales-document-form]").addEventListener("submit", event => {
    event.preventDefault();
  });

  const totals = () => {
    const goods = state.rows.reduce((sum, row) => sum + rowNet(row), 0);
    const gross = state.rows.reduce((sum, row) => sum + row.amount, 0);
    const vat = gross - goods;
    const discount = parse($("[data-sales-discount]").value);
    return { goods, vat, gross, net: gross - discount };
  };
  const renderTotals = () => {
    const value = totals();
    $("[data-sales-total-goods]").textContent = format(value.goods, 2);
    $("[data-sales-total-vat]").textContent = format(value.vat, 2);
    $("[data-sales-total-gross]").textContent = format(value.gross, 2);
    $("[data-sales-total-net]").textContent = format(value.net, 2);
  };
  const closeModal = $("[data-sales-close-modal]");
  const updateCloseTotals = () => {
    const gross = totals().gross;
    const discount = Math.min(gross, Math.max(0, parseControlledDecimal($("[data-sales-close-discount]").value)));
    const net = Math.round((gross - discount) * 100) / 100;
    const paid = Math.max(0, parseControlledDecimal($("[data-sales-close-paid]").value));
    $("[data-sales-close-total]").textContent = format(net, 2);
    $("[data-sales-close-balance]").textContent = format(net - paid, 2);
  };
  $("[data-sales-document-save]").addEventListener("click", () => {
    const customerCode = Number($("#salesDocumentCustomer").value) || 0;
    if (customerCode <= 0 || state.rows.length === 0) {
      window.MicronoteMessageBox?.show({
        title: "Bolla di vendita",
        message: customerCode <= 0 ? "Selezionare il cliente." : "Inserire almeno una riga articolo."
      });
      return;
    }

    const value = totals();
    $("[data-sales-close-goods]").textContent = format(value.goods, 2);
    $("[data-sales-close-vat]").textContent = format(value.vat, 2);
    $("[data-sales-close-gross]").textContent = format(value.gross, 2);
    $("[data-sales-close-discount]").value = format(parse($("[data-sales-discount]").value), 2);
    $("[data-sales-close-paid]").value = format(parse($("[data-sales-paid]").value), 2);
    updateCloseTotals();
    closeModal.hidden = false;
    window.setTimeout(() => $("[data-sales-close-discount]").focus(), 0);
  });
  ["[data-sales-close-discount]", "[data-sales-close-paid]"].forEach(selector => {
    const input = $(selector);
    input.addEventListener("focus", () => input.select());
    input.addEventListener("input", () => {
      window.MicronoteDecimal?.clean?.(input);
      updateCloseTotals();
    });
    input.addEventListener("blur", () => {
      input.value = format(Math.max(0, parseControlledDecimal(input.value)), 2);
      updateCloseTotals();
    });
  });
  $("[data-sales-close-cancel]").addEventListener("click", () => {
    closeModal.hidden = true;
  });
  const confirmClosure = () => {
    const gross = totals().gross;
    const discount = parseControlledDecimal($("[data-sales-close-discount]").value);
    if (discount < 0 || discount > gross) {
      $("[data-sales-close-discount]").focus();
      return;
    }

    $("[data-sales-discount]").value = format(discount, 2);
    $("[data-sales-paid]").value = format(
      Math.max(0, parseControlledDecimal($("[data-sales-close-paid]").value)),
      2);
    renderTotals();
    closeModal.hidden = true;
    saveDocument();
  };
  $("[data-sales-close-confirm]").addEventListener("click", confirmClosure);
  $("[data-sales-close-confirm-print]").addEventListener("click", confirmClosure);
  const selectRow = index => {
    state.selected = index;
    Array.from($("[data-sales-document-rows]").rows).forEach((row, rowIndex) =>
      row.classList.toggle("selected-row", rowIndex === index));
    $("[data-sales-line-edit]").disabled = index < 0;
    $("[data-sales-line-remove]").disabled = index < 0;
  };
  const renderRows = () => {
    const body = $("[data-sales-document-rows]");
    body.replaceChildren(...state.rows.map((item, index) => {
      const row = document.createElement("tr");
      row.tabIndex = 0;
      const vatPrice = item.price * (1 + item.vatRate / 100);
      [
        item.articleCode,
        item.description,
        item.unit,
        item.packages ? String(item.packages) : "",
        item.tare ? format(item.tare, 3) : "",
        item.quantity ? format(item.quantity, 3) : "",
        format(item.price, 3),
        item.vatRate ? `${format(item.vatRate, 2)} %` : "",
        format(vatPrice, 2),
        format(item.amount, 2)
      ].forEach(text => {
        const cell = document.createElement("td");
        const input = document.createElement("input");
        input.type = "text";
        input.readOnly = true;
        input.tabIndex = -1;
        input.value = text;
        cell.appendChild(input);
        row.appendChild(cell);
      });
      row.addEventListener("click", () => selectRow(index));
      row.addEventListener("dblclick", () => openEditor(index));
      row.addEventListener("keydown", event => {
        if (event.key === "Enter") {
          event.preventDefault();
          openEditor(index);
        } else if (event.key === "ArrowDown") {
          event.preventDefault();
          selectRow(Math.min(index + 1, state.rows.length - 1));
          body.rows[Math.min(index + 1, state.rows.length - 1)]?.focus();
        } else if (event.key === "ArrowUp") {
          event.preventDefault();
          selectRow(Math.max(index - 1, 0));
          body.rows[Math.max(index - 1, 0)]?.focus();
        }
      });
      return row;
    }));
    if (state.rows.length === 0) selectRow(-1);
    else selectRow(Math.min(Math.max(state.selected, 0), state.rows.length - 1));
    renderTotals();
  };

  const editorRow = () => ({
    articleCode: $("[data-line-code]").value.trim(),
    description: $("[data-line-description]").value.trim(),
    unit: $("[data-line-unit]").value.trim(),
    packages: Math.trunc(parse($("[data-line-packages]").value)),
    tare: parse($("[data-line-tare]").value),
    quantity: parse($("[data-line-quantity]").value),
    price: parse($("[data-line-price]").value),
    vatRate: parse($("[data-line-vat]").value),
    amount: 0
  });
  const updateEditorAmount = () => {
    const row = editorRow();
    row.amount = calculateAmount(row);
    $("[data-line-vat-price]").value = format(row.price * (1 + row.vatRate / 100), 2);
    $("[data-line-amount]").value = format(row.amount, 2);
  };
  const openEditor = (index, article = null) => {
    state.editing = index;
    const catalogueArticle = article
      ?? articles.find(item => String(item.code) === String(state.rows[index]?.articleCode))
      ?? {};
    const row = index >= 0 ? state.rows[index] : {
      articleCode: article?.code || "",
      description: article?.description || "",
      unit: article?.unit || "",
      packages: 0,
      tare: article?.tare || 0,
      quantity: 0,
      price: article?.price || 0,
      vatRate: article?.vatRate || 0,
      amount: 0
    };
    $("[data-sales-line-title]").textContent = index >= 0 ? "Modifica articolo" : "Inserimento articolo";
    $("[data-line-code]").value = row.articleCode;
    $("[data-line-code]").readOnly = index >= 0;
    $("[data-sales-line-article-lookup]").disabled = index >= 0;
    $("[data-line-description]").value = row.description;
    $("[data-line-unit]").value = row.unit;
    $("[data-line-stock]").value = format(catalogueArticle.stock || 0, 2);
    $("[data-line-packages]").value = row.packages || "";
    $("[data-line-tare]").value = row.tare ? format(row.tare, 3) : "";
    $("[data-line-quantity]").value = row.quantity ? format(row.quantity, 3) : "";
    $("[data-line-price]").value = format(row.price, 2);
    $("[data-line-vat]").value = format(row.vatRate, 2);
    $("[data-line-last-price]").value = "";
    $("[data-line-last-vat]").value = "";
    $("[data-line-vat-price]").value = format(row.price * (1 + row.vatRate / 100), 2);
    $("[data-line-amount]").value = format(row.amount || calculateAmount(row), 2);
    $("[data-sales-line-modal]").hidden = false;
    const customerCode = Number($("#salesDocumentCustomer").value) || 0;
    if (customerCode > 0) {
      const requestedArticle = row.articleCode;
      fetch(`${location.pathname}?handler=LastPrice&customerCode=${encodeURIComponent(customerCode)}&articleCode=${encodeURIComponent(requestedArticle)}`)
        .then(response => response.ok ? response.json() : null)
        .then(result => {
          if ($("[data-line-code]").value.trim() !== requestedArticle) return;
          $("[data-line-last-price]").value = result?.price == null ? "" : format(result.price, 2);
          $("[data-line-last-vat]").value = result?.vatRate == null ? "" : format(result.vatRate, 2);
        })
        .catch(() => {
          if ($("[data-line-code]").value.trim() !== requestedArticle) return;
          $("[data-line-last-price]").value = "";
          $("[data-line-last-vat]").value = "";
        });
    }
    (index >= 0 || article ? $("[data-line-quantity]") : $("[data-line-code]")).focus();
  };
  const closeEditor = () => {
    $("[data-sales-line-modal]").hidden = true;
    state.editing = -1;
  };

  $("[data-sales-line-add]").addEventListener("click", () => openEditor(-1));
  $("[data-sales-line-article-lookup]").addEventListener("click", () =>
    $("[data-sales-article-lookup-open]").click());
  root.addEventListener("micronote:lookup-selected", event => {
    if (event.target.matches(".sales-document-customer")) {
      const storeCode = Number(event.detail.row.storeCode) || 0;
      if (storeCode > 0) $("[data-sales-document-store]").value = String(storeCode);
      return;
    }
    if (!event.target.matches(".sales-document-article-lookup")) return;
    const code = String(event.detail.row.code || "");
    const source = event.detail.row;
    const catalogueArticle = articles.find(item => String(item.code) === code);
    const article = catalogueArticle
      ? {
          ...catalogueArticle,
          unit: catalogueArticle.unit || source.unitMeasure || "",
          tare: catalogueArticle.tare ?? source.tare ?? 0
        }
      : {
          code,
          description: source.description || source.label || "",
          unit: source.unitMeasure || "",
          tare: Number(source.tare) || 0,
          stock: Number(source.stock) || 0,
          price: Number(source.price) || 0,
          vatRate: Number(source.vatRate) || 0
        };
    if (article) openEditor(-1, article);
  });
  const resolveEditorArticle = () => {
    if (state.editing >= 0) return true;
    const code = $("[data-line-code]").value.trim();
    if (!code) return false;
    const article = articles.find(item => String(item.code).toLocaleLowerCase("it-IT") === code.toLocaleLowerCase("it-IT"));
    if (article) {
      openEditor(-1, article);
      return true;
    }
    $("[data-line-description]").value = "";
    window.MicronoteMessageBox?.show({
      title: "Articolo",
      message: `Articolo ${code} non trovato.`
    });
    $("[data-line-code]").focus();
    return false;
  };
  $("[data-line-code]").addEventListener("change", resolveEditorArticle);
  $("[data-line-code]").addEventListener("keydown", event => {
    if (event.key !== "Enter") return;
    event.preventDefault();
    resolveEditorArticle();
  });
  $("[data-sales-line-edit]").addEventListener("click", () =>
    state.selected >= 0 && openEditor(state.selected));
  $("[data-sales-line-remove]").addEventListener("click", () => {
    if (state.selected < 0) return;
    const index = state.selected;
    window.MicronoteMessageBox?.show({
      title: "Cancella articolo",
      message: `Cancellare la riga dell'articolo ${state.rows[index].articleCode}?`,
      mode: "confirm",
      variant: "confirm",
      okText: "Cancella",
      onConfirm: () => {
        state.rows.splice(index, 1);
        state.selected = Math.min(index, state.rows.length - 1);
        renderRows();
      }
    });
  });
  $("[data-line-confirm]").addEventListener("click", () => {
    const row = editorRow();
    row.amount = calculateAmount(row);
    if (!row.articleCode || (row.packages === 0 && row.quantity === 0) || row.price === 0) {
      window.MicronoteMessageBox?.show({
        title: "Riga articolo",
        message: "Indicare quantità o colli e un prezzo valido."
      });
      return;
    }
    if (state.editing >= 0) state.rows[state.editing] = row;
    else state.rows.push(row);
    state.selected = state.editing >= 0 ? state.editing : state.rows.length - 1;
    closeEditor();
    renderRows();
  });
  root.querySelectorAll("[data-line-cancel]").forEach(button =>
    button.addEventListener("click", closeEditor));
  root.querySelectorAll("[data-line-packages],[data-line-quantity],[data-line-price],[data-line-vat]")
    .forEach(input => input.addEventListener("input", updateEditorAmount));
  $("[data-sales-discount]").addEventListener("input", renderTotals);

  document.addEventListener("keydown", event => {
    if (event.key !== "Escape") return;
    if (!closeModal.hidden) {
      event.preventDefault();
      closeModal.hidden = true;
      return;
    }
    const modal = $("[data-sales-line-modal]");
    if (!modal.hidden) {
      event.preventDefault();
      closeEditor();
    }
  }, true);

  renderRows();
})();
