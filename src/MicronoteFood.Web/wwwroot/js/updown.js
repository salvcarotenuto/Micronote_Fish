document.addEventListener("DOMContentLoaded", () => {
  const selector = "input[data-updown]:not([data-updown-ready])";

  const clamp = (value, input) => {
    const min = Number.parseInt(input.getAttribute("min") ?? input.dataset.updownMin ?? "", 10);
    const max = Number.parseInt(input.getAttribute("max") ?? input.dataset.updownMax ?? "", 10);
    let next = value;

    if (Number.isFinite(min)) {
      next = Math.max(min, next);
    }

    if (Number.isFinite(max)) {
      next = Math.min(max, next);
    }

    return next;
  };

  const digitsOnly = (value) => String(value ?? "").replace(/\D/g, "");

  const stepValue = (input) => {
    const step = Number.parseInt(input.getAttribute("step") ?? input.dataset.updownStep ?? "1", 10);
    return Number.isFinite(step) && step > 0 ? step : 1;
  };

  const updateValue = (input, delta) => {
    if (input.disabled || input.readOnly) {
      return;
    }

    const currentText = digitsOnly(input.value);
    const currentValue = currentText === "" ? 0 : Number.parseInt(currentText, 10);
    const nextValue = clamp(currentValue + delta, input);
    input.value = String(nextValue);
    input.dispatchEvent(new Event("input", { bubbles: true }));
    input.dispatchEvent(new Event("change", { bubbles: true }));
    input.focus();
    input.select();
  };

  const sanitizeInput = (input) => {
    const cleanValue = digitsOnly(input.value);
    if (input.value !== cleanValue) {
      const selectionStart = input.selectionStart ?? cleanValue.length;
      input.value = cleanValue;
      input.setSelectionRange(
        Math.min(selectionStart, cleanValue.length),
        Math.min(selectionStart, cleanValue.length)
      );
    }
  };

  const makeButton = (direction, input) => {
    const button = document.createElement("button");
    button.type = "button";
    button.className = `updown-button updown-button-${direction}`;
    button.dataset.updownDirection = direction;
    button.setAttribute("aria-label", direction === "up" ? "Aumenta" : "Diminuisci");
    button.setAttribute("tabindex", "-1");
    button.tabIndex = -1;

    let repeatDelay = 0;
    let repeatInterval = 0;
    const delta = direction === "up" ? stepValue(input) : -stepValue(input);

    const stopRepeat = () => {
      window.clearTimeout(repeatDelay);
      window.clearInterval(repeatInterval);
      repeatDelay = 0;
      repeatInterval = 0;
    };

    button.addEventListener("pointerdown", (event) => {
      event.preventDefault();
      updateValue(input, delta);
      stopRepeat();
      repeatDelay = window.setTimeout(() => {
        repeatInterval = window.setInterval(() => updateValue(input, delta), 80);
      }, 350);
    });

    ["pointerup", "pointercancel", "pointerleave", "blur"].forEach((eventName) => {
      button.addEventListener(eventName, stopRepeat);
    });

    button.addEventListener("keydown", (event) => {
      event.preventDefault();
    });

    button.addEventListener("focus", () => {
      window.requestAnimationFrame(() => {
        input.focus({ preventScroll: true });
        input.select();
      });
    });

    return button;
  };

  document.querySelectorAll(selector).forEach((input) => {
    input.dataset.updownReady = "true";
    input.inputMode = "numeric";
    input.autocomplete = input.autocomplete || "off";
    input.pattern = input.pattern || "[0-9]*";
    input.value = digitsOnly(input.value);

    const wrapper = document.createElement("div");
    wrapper.className = "updown-control";
    input.parentNode.insertBefore(wrapper, input);
    wrapper.appendChild(input);

    const buttons = document.createElement("div");
    buttons.className = "updown-buttons";
    buttons.setAttribute("aria-hidden", "true");
    buttons.appendChild(makeButton("up", input));
    buttons.appendChild(makeButton("down", input));
    wrapper.appendChild(buttons);

    input.addEventListener("focus", () => {
      window.requestAnimationFrame(() => input.select());
    });

    input.addEventListener("beforeinput", (event) => {
      if (event.data && /\D/.test(event.data)) {
        event.preventDefault();
      }
    });

    input.addEventListener("input", () => sanitizeInput(input));
    input.addEventListener("paste", () => {
      window.requestAnimationFrame(() => sanitizeInput(input));
    });

    input.addEventListener("keydown", (event) => {
      if (event.key === "ArrowUp") {
        event.preventDefault();
        updateValue(input, stepValue(input));
      } else if (event.key === "ArrowDown") {
        event.preventDefault();
        updateValue(input, -stepValue(input));
      }
    });
  });
});
