import { backendOrigin } from "@/lib/backend";
export const dynamic="force-dynamic";
export const runtime="nodejs";
// Transport only: business validation, authentication, files and SQL Server live in ASP.NET Core.
async function proxy(request:Request){
  try{
    const incoming=new URL(request.url);
    const target=new URL(incoming.pathname+incoming.search,backendOrigin());
    const headers=new Headers();
    for(const key of ["content-type","origin","cookie","x-order-code","x-file-name","accept"])
      if(request.headers.has(key))headers.set(key,request.headers.get(key)!);
    if(process.env.TRUST_IIS_PROXY==="true"&&request.headers.has("x-naghsh-client-ip"))
      headers.set("x-naghsh-client-ip",request.headers.get("x-naghsh-client-ip")!);
    const options:RequestInit&{duplex?:"half"}={method:request.method,headers,cache:"no-store",redirect:"manual",signal:request.signal};
    if(request.method!=="GET"&&request.method!=="HEAD"){options.body=request.body;options.duplex="half";}
    const response=await fetch(target,options),out=new Headers();
    for(const key of ["content-type","content-disposition","cache-control","content-security-policy","x-content-type-options","location","retry-after"])
      if(response.headers.has(key))out.set(key,response.headers.get(key)!);
    for(const cookie of response.headers.getSetCookie())out.append("set-cookie",cookie);
    return new Response(response.body,{status:response.status,headers:out});
  }catch{
    return Response.json({error:"ارتباط با سرویس برقرار نشد؛ دوباره تلاش کنید."},{status:502,headers:{"Cache-Control":"no-store"}});
  }
}
export {proxy as GET,proxy as POST,proxy as PUT,proxy as PATCH};
// Forward only the DELETE routes exposed by the backend.
export async function DELETE(request: Request) {
  const path = new URL(request.url).pathname;
  const managedRecord = /^\/api\/admin\/(?:print-catalog|services)\/[a-f0-9]{8}(?:-[a-f0-9]{4}){3}-[a-f0-9]{12}$/i.test(path);
  if (!path.startsWith("/api/realtime/") && !managedRecord) return new Response(null, { status: 405 });
  return proxy(request);
}
