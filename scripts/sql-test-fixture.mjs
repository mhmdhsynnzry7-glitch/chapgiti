import { createServer } from "node:net";

export async function freePort() {
  const probe = createServer();
  await new Promise(resolve => probe.listen(0, "127.0.0.1", resolve));
  const port = probe.address().port;
  await new Promise(resolve => probe.close(resolve));
  return port;
}

export async function sqlFixture() {
  throw new Error("Broad SQL fixtures are disabled. Use memory tests or explicitly authorized identity-guarded tests/sql005 acceptance; never fall back to the working ChapGiti database.");
}
