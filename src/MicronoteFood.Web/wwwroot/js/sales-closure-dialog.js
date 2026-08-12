document.addEventListener("DOMContentLoaded", () => {
  document.querySelectorAll("[data-sales-closure-dialog]").forEach((dialog) => {
    const controls = () => Array.from(dialog.querySelectorAll("input, button, [href]"))
      .filter((control) => !control.disabled)
      .filter((control) => control.tabIndex !== -1)
      .filter((control) => control.offsetParent !== null);

    dialog.addEventListener("keydown", (event) => {
      if (event.key === "Escape") {
        event.preventDefault();
        event.stopPropagation();
        dialog.querySelector("[data-sales-close-cancel]")?.click();
        return;
      }

      if (event.defaultPrevented || (event.key !== "Enter" && event.key !== "Tab")) {
        return;
      }

      if (event.key === "Enter" && event.target instanceof HTMLButtonElement) {
        return;
      }

      const navigable = controls();
      if (navigable.length === 0) {
        return;
      }

      const currentIndex = navigable.indexOf(event.target);
      event.preventDefault();
      const direction = event.shiftKey ? -1 : 1;
      const nextIndex = currentIndex < 0
        ? (direction > 0 ? 0 : navigable.length - 1)
        : (currentIndex + direction + navigable.length) % navigable.length;
      navigable[nextIndex].focus();
      navigable[nextIndex].select?.();
    });
  });
});
