import nextPorts from "../frontend/node_modules/next/dist/lib/helpers/get-reserved-port.js";
import { freePort } from "../scripts/sql-test-fixture.mjs";

// Windows can assign a free port that Next.js and browsers refuse, such as 6667.
export async function browserPort(allocate = freePort) {
  for (let attempt = 0; attempt < 32; attempt++) {
    const port = await allocate();
    if (!nextPorts.isPortIsReserved(port)) return port;
  }
  throw new Error("Could not allocate a loopback port allowed by Next.js.");
}
