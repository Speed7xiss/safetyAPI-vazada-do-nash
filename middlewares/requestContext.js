const crypto = require('crypto');

function requestContext(req, res, next) {
  req.requestId = crypto.randomUUID();
  res.setHeader('x-request-id', req.requestId);

  const start = process.hrtime.bigint();
  res.on('finish', () => {
    const end = process.hrtime.bigint();
    req.latencyMs = Number(end - start) / 1e6;
  });

  next();
}

module.exports = { requestContext };
