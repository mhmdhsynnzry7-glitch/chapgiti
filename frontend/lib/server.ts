import { currentAdmin } from "./auth";
import { BackendError, publicBootstrap, backendApi } from "./backend";
export class HttpError extends Error {
  constructor(
    public status: number,
    message: string,
  ) {
    super(message);
  }
}
export async function admin() {
  const user = await currentAdmin();
  if (!user) throw new HttpError(401, "برای ادامه وارد حساب مدیریت شوید.");
  return user;
}
export const siteSettings = async () => (await publicBootstrap()).site;
export const orderRequirements = async () =>
  (await publicBootstrap()).orderRequirements || {};
export const publicArticles = async () =>
  (await backendApi<import("./magazine").MagazinePage>("public/articles"))
    .items;
export const publicServices = async () => (await publicBootstrap()).services;
export const origin = () => process.env.SITE_URL || "http://127.0.0.1:3000";
export const indexed = () => process.env.INDEXING_ENABLED === "true";
export { BackendError };
