import { redirect } from "next/navigation";
import { cookies } from "next/headers";
import { backendApi,BackendError } from "./backend";
export type CustomerProfile={id:string;phone:string;firstName:string;lastName:string;phoneVerified:boolean;hasAccount:boolean;isAdmin?:boolean};
export function customerIsReady(customer:CustomerProfile|null):customer is CustomerProfile{return !!(customer?.hasAccount&&customer.firstName&&customer.lastName);}
export function safeCustomerNext(value?:string|string[]){
  if(typeof value!=="string"||value.length>5000||!value.startsWith("/")||value.startsWith("//")||/[\\\r\n]/.test(value))return "/account";
  try{
    const url=new URL(value,"http://local.invalid");
    const allowed=["/calculator","/order","/track","/spine","/tickets","/account","/account/orders","/contact","/customer/profile"].includes(url.pathname)||/^\/account\/orders\/[a-f0-9-]{36}$/.test(url.pathname)||/^\/tickets\/[a-f0-9-]{36}$/.test(url.pathname);
    if(url.origin!=="http://local.invalid"||!allowed)return "/account";
    return url.pathname+url.search+url.hash;
  }catch{return "/account";}
}
export async function currentCustomer():Promise<CustomerProfile|null>{
  try{return await backendApi<CustomerProfile>("customer/me");}
  catch(error){if(error instanceof BackendError&&error.status===401)return null;throw error;}
}
export async function requireCustomerProfile(next="/account"){
  const customer=await currentCustomer(),destination=encodeURIComponent(safeCustomerNext(next));
  if(!customer){
    if((await cookies()).has("giti_customer_refresh"))redirect("/session/renew?next="+destination);
    redirect("/customer?next="+destination);
  }
  if(!customerIsReady(customer))redirect("/customer/profile?next="+destination);
  return customer;
}
