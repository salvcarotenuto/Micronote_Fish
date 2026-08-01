document.addEventListener("DOMContentLoaded", () => {
  document.querySelectorAll("[data-article-row-dialog]").forEach((dialog) => {
    const navigableControls = () => Array.from(dialog.querySelectorAll("input, button"))
      .filter((control) => !control.disabled)
      .filter((control) => !control.readOnly)
      .filter((control) => control.type !== "hidden")
      .filter((control) => control.tabIndex !== -1);

    dialog.addEventListener("keydown", (event) => {
      if (event.defaultPrevented || (event.key !== "Enter" && event.key !== "Tab")) {
        return;
      }

      if (!(event.target instanceof HTMLInputElement || event.target instanceof HTMLButtonElement)) {
        return;
      }

      // Il codice articolo richiede la risoluzione specifica del singolo modulo.
      if (event.key === "Enter" && event.target.matches("[data-line-code]")) {
        return;
      }

      // Invio sui pulsanti deve eseguire normalmente l'azione del pulsante.
      if (event.key === "Enter" && event.target instanceof HTMLButtonElement) {
        return;
      }

      const controls = navigableControls();
      const currentIndex = controls.indexOf(event.target);
      if (currentIndex < 0 || controls.length === 0) {
        return;
      }

      event.preventDefault();
      const direction = event.shiftKey ? -1 : 1;
      const nextIndex = (currentIndex + direction + controls.length) % controls.length;
      controls[nextIndex].focus();
      controls[nextIndex].select?.();
    });
  });
});
