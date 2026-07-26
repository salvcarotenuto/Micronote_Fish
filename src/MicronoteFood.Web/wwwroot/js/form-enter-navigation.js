document.addEventListener("DOMContentLoaded", () => {
  const forms = document.querySelectorAll("form[data-enter-navigation]");

  const navigableSelector = [
    "input:not([type='hidden']):not([type='submit']):not([type='button'])",
    "select",
    "textarea"
  ].join(",");

  const isNavigable = (element) =>
    element
    && !element.disabled
    && !element.readOnly
    && element.tabIndex >= 0
    && element.offsetParent !== null
    && element.matches(navigableSelector);

  const documentOrder = (left, right) => {
    if (left === right) {
      return 0;
    }

    return left.compareDocumentPosition(right) & Node.DOCUMENT_POSITION_FOLLOWING ? -1 : 1;
  };

  const tabOrder = (left, right) => {
    const leftTab = left.tabIndex;
    const rightTab = right.tabIndex;
    const leftPositive = leftTab > 0;
    const rightPositive = rightTab > 0;

    if (leftPositive && rightPositive && leftTab !== rightTab) {
      return leftTab - rightTab;
    }

    if (leftPositive !== rightPositive) {
      return leftPositive ? -1 : 1;
    }

    return documentOrder(left, right);
  };

  const getNavigableControls = (form) =>
    Array.from(form.querySelectorAll(navigableSelector))
      .filter(isNavigable)
      .sort(tabOrder);

  const focusNextControl = (form, currentControl) => {
    const controls = getNavigableControls(form);
    const currentIndex = controls.indexOf(currentControl);

    if (currentIndex === -1 || controls.length === 0) {
      return;
    }

    const nextControl = controls[currentIndex + 1] ?? controls[0];
    nextControl.focus();

    if (typeof nextControl.select === "function"
        && nextControl.tagName.toLowerCase() !== "select"
        && nextControl.type !== "checkbox") {
      nextControl.select();
    }
  };

  forms.forEach((form) => {
    form.addEventListener("keydown", (event) => {
      if (event.key !== "Enter" || event.shiftKey || event.ctrlKey || event.altKey) {
        return;
      }

      const target = event.target;
      if (!isNavigable(target) || target.tagName.toLowerCase() === "textarea") {
        return;
      }

      event.preventDefault();
      target.dispatchEvent(new Event("change", { bubbles: true }));

      window.requestAnimationFrame(() => {
        focusNextControl(form, target);
      });
    });
  });
});
