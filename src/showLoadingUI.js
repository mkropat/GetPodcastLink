const showLoadingUI = handler => {
  let spinner = document.createElement('img');
  spinner.alt = 'loading';
  spinner.src = 'img/spinner.svg';

  return async submitEvent => {
    submitEvent.preventDefault();

    let submitButton = submitEvent.target.querySelector('[type="submit"]');
    let inputs = Array.from(submitEvent.target.querySelectorAll('button, input, textarea'));

    let buttonContents = [];
    let originalWidth = '';

    let originalDisabledValues = inputs.map(node => node.disabled);
    for (let input of inputs) {
      input.disabled = true;
    }

    if (submitButton) {
      originalWidth = submitButton.style.width;
      let buttonWidth = submitButton.getBoundingClientRect().width;
      submitButton.style.width = buttonWidth + 'px';

      submitButton.classList.add('submitting');

      buttonContents.push(...submitButton.childNodes);

      for (let node of buttonContents) {
        submitButton.removeChild(node);
      }
      submitButton.appendChild(spinner);
    }

    try {
      await handler(submitEvent);
    }
    finally {
      let inputsWithDisabledValue = inputs.map((input, i) => ({ input, disabledValue: originalDisabledValues[i] }));
      for (let { input, disabledValue } of inputsWithDisabledValue) {
        input.disabled = disabledValue;
      }

      if (submitButton) {
        submitButton.style.width = originalWidth;

        submitButton.classList.remove('submitting');

        submitButton.removeChild(spinner);
        for (let node of buttonContents) {
          submitButton.appendChild(node);
        }
      }
    }
  };
};

export default showLoadingUI;
