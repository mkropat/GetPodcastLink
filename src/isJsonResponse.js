const isJsonResponse = (response) => {
  let contentType = response.headers.get('content-type') || '';
  let [type, ...rest] = contentType.split(/;/).map(x => x.trim());
  return type;
};

export default isJsonResponse;
