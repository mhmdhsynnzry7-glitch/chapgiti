process.env.NODE_ENV="production";
const {stack}=await import("./dotnet-runtime.mjs");
await stack(false);
