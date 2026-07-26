(function () {
  function ensureMessageBox() {
    let overlay = document.querySelector("[data-micronote-messagebox]");
    if (overlay) {
      return overlay;
    }

    overlay = document.createElement("div");
    overlay.className = "messagebox-overlay";
    overlay.dataset.micronoteMessagebox = "true";
    overlay.setAttribute("aria-hidden", "true");
    overlay.innerHTML = `
      <div class="messagebox" role="alertdialog" aria-modal="true"
           aria-labelledby="messagebox-title" aria-describedby="messagebox-body">
        <div class="messagebox-title" id="messagebox-title"></div>
        <div class="messagebox-body" id="messagebox-body">
          <div class="messagebox-message"></div>
          <div class="messagebox-detail"></div>
        </div>
        <div class="messagebox-actions">
          <button class="button button-primary messagebox-ok" type="button">OK</button>
          <button class="button button-secondary messagebox-cancel" type="button">Annulla</button>
        </div>
      </div>
    `;
    document.body.appendChild(overlay);
    return overlay;
  }

  function show(options = {}) {
    const overlay = ensureMessageBox();
    const title = overlay.querySelector(".messagebox-title");
    const message = overlay.querySelector(".messagebox-message");
    const detail = overlay.querySelector(".messagebox-detail");
    const okButton = overlay.querySelector(".messagebox-ok");
    const cancelButton = overlay.querySelector(".messagebox-cancel");
    const previousFocus = document.activeElement;
    const mode = options.mode || "alert";
    const variant = options.variant || (mode === "confirm" ? "confirm" : "info");

    title.textContent = options.title || "Micronote Food - messaggio";
    message.textContent = options.message || "";
    detail.textContent = options.detail || "";
    detail.hidden = !options.detail;
    okButton.textContent = options.okText || "OK";
    cancelButton.textContent = options.cancelText || "Annulla";
    cancelButton.hidden = mode !== "confirm";
    cancelButton.tabIndex = mode === "confirm" ? 0 : -1;

    overlay.classList.remove(
      "messagebox-info",
      "messagebox-success",
      "messagebox-error",
      "messagebox-confirm"
    );
    overlay.classList.add(`messagebox-${variant}`);

    let closed = false;

    function restoreFocus() {
      if (previousFocus && typeof previousFocus.focus === "function") {
        previousFocus.focus();
      }
    }

    function close(confirmed) {
      if (closed) {
        return;
      }

      closed = true;
      overlay.classList.remove("active");
      overlay.setAttribute("aria-hidden", "true");
      okButton.removeEventListener("click", onOk);
      cancelButton.removeEventListener("click", onCancel);
      document.removeEventListener("keydown", onKeyDown, true);

      if (confirmed && typeof options.onConfirm === "function") {
        options.onConfirm();
      } else if (!confirmed && typeof options.onCancel === "function") {
        options.onCancel();
      } else {
        restoreFocus();
      }
    }

    function onOk() {
      close(true);
    }

    function onCancel() {
      close(false);
    }

    function onKeyDown(event) {
      if (event.key === "Escape") {
        event.preventDefault();
        event.stopPropagation();
        close(mode !== "confirm");
      } else if (event.key === "Enter") {
        event.preventDefault();
        event.stopPropagation();
        close(true);
      } else if (event.key === " " || event.key === "Spacebar") {
        if (document.activeElement === okButton) {
          event.preventDefault();
          event.stopPropagation();
          close(true);
        }
      } else if (event.key === "Tab" && mode === "confirm") {
        event.preventDefault();
        (document.activeElement === cancelButton ? okButton : cancelButton).focus();
      }
    }

    okButton.addEventListener("click", onOk);
    cancelButton.addEventListener("click", onCancel);
    document.addEventListener("keydown", onKeyDown, true);
    overlay.classList.add("active");
    overlay.setAttribute("aria-hidden", "false");
    (mode === "confirm" ? cancelButton : okButton).focus();
  }

  window.MicronoteMessageBox = { show };
})();
