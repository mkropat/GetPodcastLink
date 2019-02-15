import copyToClipboard from './copyToClipboard';
import isJsonResponse from './isJsonResponse';
import showLoadingUI from './showLoadingUI';
import visibilityGroup from './visibilityGroup';

import marx from 'marx-css/css/marx.min.css';
import style from './site.css';

for (let button of document.querySelectorAll('button.copy')) {
  button.addEventListener('click', copyToClipboard);
}

const setResultsVisibility = visibilityGroup(['error-result', 'no-results', 'results']);

document.querySelector('form').addEventListener('submit', showLoadingUI(async e => {
  setResultsVisibility();

  let query = e.target.elements['query'].value;
  let resp;
  let url = 'https://getpodcastlink.azurewebsites.net/api/GetPodcastLink?query=' + encodeURIComponent(query);
  try {
    resp = await fetch(url, { mode: 'cors' });
  }
  catch (ex) {
    displayError(ex.message);
    return;
  }

  if (!resp.ok) {
    displayError(await getErrorDetails(resp));
    return;
  }

  displayResults(await resp.json());
}));

const displayResults = ({ feedUrls=[] }) => {
  setResultsVisibility(feedUrls.length ? 'results' : 'no-results');

  if (feedUrls.length) {
    let resultBox = document.getElementById('results').querySelector('input');
    resultBox.value = feedUrls[0];
  }
}

const displayError = (details) => {
  setResultsVisibility('error-result');

  let container = document.getElementById('error-result').querySelector('pre');
  container.innerText = details;
};

const getErrorDetails = async (resp) => {
  if (!isJsonResponse(resp))
    return '';
  
  try {
    let errorDetails = await resp.json();
    return JSON.stringify(errorDetails, null, 4);
  }
  catch (ex) {
    console.error('Error when parsing error details', ex);
    return '';
  }
}
