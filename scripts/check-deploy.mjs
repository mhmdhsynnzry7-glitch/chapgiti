import path from "node:path";
const {environment,cli}=await import("./dotnet-runtime.mjs");
environment(false);
const configured = process.env.SITE_URL;
if (!configured) throw new Error("Set SITE_URL in .env.local");
const url = new URL(configured);
const allowedProtocol = url.protocol === "https:" || (url.protocol === "http:" && process.env.ALLOW_HTTP === "true");
if (!allowedProtocol || url.username || url.password || url.pathname !== "/" || url.search || url.hash || configured.endsWith("/"))
  throw new Error("SITE_URL must be an HTTPS origin, or HTTP with ALLOW_HTTP=true, without a trailing slash.");
if (!process.env.DATA_DIR || !path.isAbsolute(process.env.DATA_DIR)) throw new Error("Production DATA_DIR must be absolute.");
const expected = process.argv[2];
if (expected && path.resolve(expected).toLowerCase() !== path.resolve(process.env.DATA_DIR).toLowerCase())
  throw new Error("DataDirectory must match DATA_DIR in .env.local.");
if (!process.env.SQLSERVER_CONNECTION_STRING && !process.env.ConnectionStrings__SqlServer)
  throw new Error("Set SQLSERVER_CONNECTION_STRING for production.");
await cli(["--check"],environment(false));
if (process.env.SMS_ENABLED === "true" && (!process.env.MELIPAYAMAK_USERNAME || !process.env.MELIPAYAMAK_PASSWORD || !(process.env.MELIPAYAMAK_FROM || process.env.MELIPAYAMAK_BODY_ID)))
  throw new Error("SMS is enabled but configuration is incomplete.");
console.log("Production SQL Server, ASP.NET Core and admin account verified.");
