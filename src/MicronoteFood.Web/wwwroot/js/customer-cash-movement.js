(function () {
  const form = document.querySelector("[data-customer-cash-movement-form]");
  if (!form) return;

  const saveButton = document.querySelector("#cashMovementSave");
  const enableSaveButton = () => {
    if (saveButton) saveButton.disabled = false;
  };
  enableSaveButton();
  window.addEventListener("pageshow", enableSaveButton);

  const lookup = form.querySelector("[data-lookup-field]");
  const code = form.querySelector("#cashMovementCustomerCode");
  const display = form.querySelector("#cashMovementCustomerDisplay");
  const name = form.querySelector("#cashMovementCustomerName");
  const customerClear = form.querySelector("#cashMovementCustomerClear");
  const storeCode = form.querySelector("#cashMovementCustomerStoreCode");
  const store = form.querySelector("#cashMovementCustomerStore");
  const amount = form.querySelector("[name='Movement.Amount']");
  const documentType = form.querySelector("#cashMovementDocumentType");
  const documentId = form.querySelector("#cashMovementDocumentId");
  const documentYearValue = form.querySelector("#cashMovementDocumentYearValue");
  const documentCode = form.querySelector("#cashMovementDocumentCode");
  const documentNumber = form.querySelector("#cashMovementDocumentNumber");
  const documentDate = form.querySelector("#cashMovementDocumentDate");
  const documentDisplay = form.querySelector("#cashMovementDocumentDisplay");
  const documentDescription = form.querySelector("#cashMovementDocumentDescription");
  const documentLookup = form.querySelector("#cashMovementDocumentLookup");
  const documentCopy = form.querySelector("#cashMovementDocumentCopy");
  const documentClear = form.querySelector("#cashMovementDocumentClear");
  const documentDialog = document.querySelector("#cashMovementDocumentDialog");
  const documentYear = document.querySelector("#cashMovementDocumentYear");
  const documentRows = document.querySelector("[data-document-rows]");
  const documentTitle = document.querySelector("#cashMovementDocumentDialogTitle");
  const customerReadOnly = lookup?.dataset.customerReadonly === "true";
  let selectedDocument = null;

  const setCustomerStore = (value) => {
    const normalized = String(Number(value || 0));
    if (storeCode) storeCode.value = normalized;
    if (store) store.value = normalized;
  };

  store?.addEventListener("change", () => {
    if (storeCode) storeCode.value = store.value || "0";
  });

  display?.addEventListener("input", () => {
    const digits = display.value.replace(/\D/g, "").slice(0, 5);
    display.value = digits;
    if (code) code.value = digits ? String(Number(digits)) : "0";
    if (name) name.value = "";
    setCustomerStore(0);
  });

  display?.addEventListener("blur", async () => {
    const customerCode = Number(display.value || 0);
    if (!customerCode) return;
    try {
      const response = await fetch(`/api/lookup_anagrafiche?type=clienti&code=${customerCode}`,
        { headers: { Accept: "application/json" } });
      const payload = await response.json();
      if (!payload.row) throw new Error();
      if (code) code.value = payload.row.code;
      display.value = payload.row.codeLabel || String(payload.row.code).padStart(5, "0");
      if (name) name.value = payload.row.label || "";
      setCustomerStore(payload.row.pointV);
    } catch {
      if (customerReadOnly) {
        if (code) code.value = String(customerCode);
        display.value = String(customerCode).padStart(5, "0");
        return;
      }
      if (code) code.value = "0";
      if (name) name.value = "";
      setCustomerStore(0);
      window.MicronoteMessageBox?.show({
        title: "Movimento contabile cliente",
        message: "Cliente inesistente.",
        variant: "error"
      });
      window.setTimeout(() => display.focus(), 0);
    }
  });

  if (customerReadOnly && Number(display?.value || 0) > 0) {
    window.setTimeout(() => display.dispatchEvent(new Event("blur")), 0);
  }

  lookup?.addEventListener("micronote:lookup-selected", (event) => {
    setCustomerStore(event.detail?.row?.pointV);
    window.setTimeout(() => form.querySelector("select[name='Movement.CauseCode']")?.focus(), 0);
  });

  const documentLabel = (row) => `${String(row.code || 0).padStart(6, "0")} / ${row.year}`;

  const applyDocument = (row) => {
    if (!row) return;
    if (documentType) documentType.value = "B";
    if (documentId) documentId.value = String(row.id);
    if (documentYearValue) documentYearValue.value = String(row.year);
    if (documentCode) documentCode.value = String(row.code);
    if (documentNumber) documentNumber.value = String(row.number || "");
    if (documentDate) documentDate.value = row.date || "";
    if (documentDisplay) documentDisplay.value = documentLabel(row);
    if (documentDescription) documentDescription.value = `Bolla n.ro ${row.number || ""} del ${row.date || ""}`;
    if (documentCopy) documentCopy.disabled = false;
    if (amount) {
      amount.value = window.MicronoteMoney?.format(Number(row.total || 0))
        ?? Number(row.total || 0).toLocaleString("it-IT", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
      amount.dispatchEvent(new Event("input", { bubbles: true }));
      amount.dispatchEvent(new Event("change", { bubbles: true }));
    }
  };

  const clearDocument = () => {
    selectedDocument = null;
    if (documentType) documentType.value = "";
    if (documentId) documentId.value = "";
    if (documentYearValue) documentYearValue.value = "";
    if (documentCode) documentCode.value = "";
    if (documentNumber) documentNumber.value = "";
    if (documentDate) documentDate.value = "";
    if (documentDisplay) documentDisplay.value = "";
    if (documentDescription) documentDescription.value = "";
    if (documentCopy) documentCopy.disabled = true;
    if (amount) {
      amount.value = "";
      amount.dispatchEvent(new Event("input", { bubbles: true }));
      amount.dispatchEvent(new Event("change", { bubbles: true }));
    }
    documentLookup?.focus();
  };

  customerClear?.addEventListener("click", () => {
    if (code) code.value = "0";
    if (display) display.value = "";
    if (name) name.value = "";
    setCustomerStore(0);
    clearDocument();
    display?.focus();
  });

  const chooseDocument = (row) => {
    selectedDocument = row;
    documentRows?.querySelectorAll("tr").forEach((item) => item.classList.toggle("selected", item.dataset.id === String(row.id)));
  };

  const loadDocuments = async () => {
    if (!documentRows || !documentType || !documentYear) return;
    documentRows.innerHTML = '<tr><td colspan="4">Caricamento...</td></tr>';
    const params = new URLSearchParams({
      handler: "Documents",
      type: "B",
      customerCode: code?.value || "0",
      year: documentYear.value
    });
    try {
      const response = await fetch(`${window.location.pathname}?${params}`, { headers: { Accept: "application/json" } });
      const rows = response.ok ? await response.json() : [];
      documentRows.innerHTML = "";
      selectedDocument = null;
      if (!rows.length) {
        documentRows.innerHTML = '<tr><td colspan="4">Nessun documento disponibile.</td></tr>';
        return;
      }
      rows.forEach((row) => {
        const tr = document.createElement("tr");
        tr.dataset.id = row.id;
        tr.innerHTML = `<td>${String(row.code || 0).padStart(6, "0")}</td><td>${row.number || ""}</td><td>${row.date || ""}</td><td>${Number(row.total || 0).toLocaleString("it-IT", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</td>`;
        tr.addEventListener("click", () => chooseDocument(row));
        tr.addEventListener("dblclick", () => {
          chooseDocument(row);
          applyDocument(row);
          documentDialog?.close();
        });
        documentRows.appendChild(tr);
      });
    } catch {
      documentRows.innerHTML = '<tr><td colspan="4">Errore durante il caricamento.</td></tr>';
    }
  };

  documentLookup?.addEventListener("click", async () => {
    if (Number(code?.value || 0) <= 0) {
      window.MicronoteMessageBox?.show({ title: "Movimento contabile cliente", message: "Selezionare prima il cliente.", variant: "error", onConfirm: () => display?.focus() });
      return;
    }
    if (documentTitle) documentTitle.textContent = "Bolle di vendita del cliente";
    documentDialog?.showModal();
    await loadDocuments();
  });
  documentClear?.addEventListener("click", clearDocument);
  documentCopy?.addEventListener("click", async () => {
    const text = documentDescription?.value.trim() || "";
    if (!text) return;
    try {
      await navigator.clipboard.writeText(text);
    } catch {
      const copyField = document.createElement("textarea");
      copyField.value = text;
      copyField.style.position = "fixed";
      copyField.style.opacity = "0";
      document.body.appendChild(copyField);
      copyField.select();
      document.execCommand("copy");
      copyField.remove();
    }
    documentCopy.focus();
  });
  documentYear?.addEventListener("change", loadDocuments);
  document.querySelectorAll("[data-document-close]").forEach((button) => button.addEventListener("click", () => documentDialog?.close()));
  document.querySelector("[data-document-confirm]")?.addEventListener("click", () => {
    if (!selectedDocument) return;
    applyDocument(selectedDocument);
    documentDialog?.close();
  });
  const validateBeforeSave = () => {
    let message = "";
    let field = null;
    if (Number(code?.value || 0) <= 0) {
      message = "Campo Cliente obbligatorio.";
      field = display;
    } else if ((window.MicronoteMoney?.parse(amount?.value) ?? 0) <= 0) {
      message = "Campo Importo obbligatorio.";
      field = amount;
    }
    if (!message) return true;
    window.MicronoteMessageBox?.show({
      title: "Movimento contabile cliente",
      message,
      variant: "error",
      onConfirm: () => field?.focus()
    });
    return false;
  };

  saveButton?.addEventListener("click", (event) => {
    if (!validateBeforeSave()) event.preventDefault();
  });

  form.addEventListener("submit", (event) => {
    if (!validateBeforeSave()) event.preventDefault();
  });

  document.addEventListener("keydown", (event) => {
    if (event.key !== "Escape" || !document.body.classList.contains("lookup-open")) return;
    event.preventDefault();
    event.stopImmediatePropagation();
    document.querySelector(".lookup-overlay.is-open .lookup-cancel")?.click();
    window.setTimeout(() => display?.focus(), 0);
  }, true);

  document.addEventListener("keydown", (event) => {
    if (event.key !== "Escape" || document.body.classList.contains("lookup-open")) return;
    event.preventDefault();
    window.location.href = form.dataset.returnUrl || "/";
  });
})();
