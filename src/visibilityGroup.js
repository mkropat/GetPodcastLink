const visibilityGroup = elementIds => visibleId => {
  for (let id of elementIds) {
    let elem = document.getElementById(id);
    elem.style.display = id === visibleId ? 'block' : 'none';
  }
};

export default visibilityGroup;
