"use client";
import { useState } from "react";
export default function CopyOrderCode({code}:{code:string}) {
  const [message,setMessage]=useState("");
  return <><bdi>{code}</bdi><button type="button" className="text-link" aria-label="کپی کد پیگیری" onClick={async()=>{
    try {await navigator.clipboard.writeText(code);setMessage("کپی شد");}
    catch {setMessage("کپی خودکار ممکن نشد؛ کد را انتخاب و کپی کنید.");}
  }}>کپی</button>{message&&<span role="status">{message}</span>}</>;
}
