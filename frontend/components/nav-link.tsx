"use client";
import Link from "next/link";
import { usePathname } from "next/navigation";
import type { ReactNode } from "react";

export default function NavLink({href, match = href, children, className, exact=false, label}: {href:string; match?:string | readonly string[]; children:ReactNode; className?:string; exact?:boolean; label?:string}) {
  const path = usePathname();
  const routes = typeof match === "string" ? [match] : match;
  const active = routes.some(route => path === route || (!exact && route !== "/" && path.startsWith(route + "/")));
  return <Link href={href} prefetch={false} aria-label={label} aria-current={active ? "page" : undefined} className={className}>{children}</Link>;
}
