import type { NextConfig } from "next";
const nextConfig: NextConfig = {
  // Verification builds compile locally without creating a deployment-ready standalone copy.
  output: process.env.CHAP_GITI_VERIFY_BUILD === "true" ? undefined : "standalone",
  outputFileTracingRoot: process.cwd(),
  poweredByHeader: false,
  async headers() {
    return [{
      source: "/:path*",
      headers: [
        { key: "X-Content-Type-Options", value: "nosniff" },
        { key: "X-Frame-Options", value: "SAMEORIGIN" },
        { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
      ],
    }];
  },
};
export default nextConfig;
