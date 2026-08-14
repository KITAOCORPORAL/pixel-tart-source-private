# WeChat Mini Program LocalDev Preview

This native preview has exactly five pages. Set `enabled`, `publicId`, and the temporary LocalDev token through the `pixel-tart-localdev-config/v1` WeChat DevTools local-storage override. Never put a real token in a tracked file. The committed example remains disabled.

The server binds `127.0.0.1`, so this works only in the simulator on the same computer. It does not support a real phone. Production requires HTTPS, a permitted domain, server-side `code2Session`, and deployment credentials that are not part of this repository.

Run the lightweight contract check with `node tests/selection-store.spec.js`.
