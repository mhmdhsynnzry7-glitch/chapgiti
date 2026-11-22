import "server-only";
import { cookies } from "next/headers";
import { cache } from "react";
import { initialSettings,initialArticles } from "./content";
import type { Banner } from "./banners";
import type { PortfolioItem } from "./portfolio";
import type { ServiceRecord } from "./services";
export class BackendError extends Error {
  constructor(public status:number,message:string){super(message);}
}
export function backendOrigin(){
  const value=process.env.DOTNET_API_URL||"http://127.0.0.1:5100";
  const url=new URL(value);
  if(!["http:","https:"].includes(url.protocol)||!["127.0.0.1","localhost","[::1]"].includes(url.hostname)||url.username||url.password||url.pathname!=="/"||url.search||url.hash)
    throw new Error("DOTNET_API_URL must be a loopback HTTP(S) origin.");
  return url.origin;
}
export async function backendApi<T=unknown>(path:string):Promise<T>{
  const jar=await cookies();
  const response=await fetch(backendOrigin()+"/api/"+path,{headers:{Cookie:jar.toString()},cache:"no-store",signal:AbortSignal.timeout(10000)});
  const data=await response.json();
  if(!response.ok)throw new BackendError(response.status,data.error||"ارتباط با سرویس برقرار نشد.");
  return data as T;
}
type Bootstrap={site:typeof initialSettings;articles:(typeof initialArticles)[number][];banners:Banner[];portfolio:PortfolioItem[];services:ServiceRecord[];orderRequirements?:Record<string,boolean>};
export const publicBootstrap=cache(()=>backendApi<Bootstrap>("public/bootstrap"));
