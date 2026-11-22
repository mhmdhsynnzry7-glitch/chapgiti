import { backendApi,BackendError } from "./backend";
export function safeAdminNext(value?: string | string[]) {
  if (typeof value !== "string" || value.length > 5000 || /[\\\r\n]/.test(value)) return "/admin";
  try {
    const url = new URL(value, "http://local.invalid");
    if (url.origin !== "http://local.invalid" || !/^\/admin(?:\/[a-z-]+(?:\/[a-f0-9-]{36})?)?$/.test(url.pathname) ||
      ["/admin/login", "/admin/session-renew"].includes(url.pathname)) return "/admin";
    return url.pathname + url.search;
  } catch { return "/admin"; }
}
export async function currentAdmin(){
  try{return await backendApi<{userId:string;email:string;displayName:string}>("auth/me");}
  catch(error){if(error instanceof BackendError&&[401,403].includes(error.status))return null;throw error;}
}
