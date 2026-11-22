"use client";
import Link from "next/link";
import NavLink from "./nav-link";
import { useRef, useState } from "react";
import { Menu, X, ArrowUpLeft, UserRound } from "lucide-react";
import { Brand } from "./ui";
import { NotificationBell,NavigationBadge } from "./notification-center";
export default function Header({ brand, customerName, hasCustomerAccount }: { brand: string; customerName?: string; hasCustomerAccount?: boolean }) {
  const [open, setOpen] = useState(false);
  const toggle = useRef<HTMLButtonElement>(null);
  return (
    <>
      <div className="topbar">
        <div className="container">
          <span>هر ایده، شایسته چاپ شدن است.</span>
          {hasCustomerAccount && <Link prefetch={false} href="/account/orders">سفارش‌های من <NavigationBadge kind="orders" label="سفارش دیده‌نشده"/><span>↖</span></Link>}
        </div>
      </div>
      <header className="header" onKeyDown={event => { if (event.key === "Escape" && open) { setOpen(false); toggle.current?.focus(); } }}>
        <div className="container header-inner">
          <Brand name={brand} />
          <nav id="giti-main-nav" className={open ? "nav open" : "nav"} aria-label="منوی اصلی" onClick={() => setOpen(false)}>
            <NavLink href="/">خانه</NavLink>
            <NavLink href="/services">خدمات ما</NavLink>
            <NavLink href={hasCustomerAccount ? "/calculator" : "/customer?next=%2Fcalculator"} match="/calculator">محاسبه هزینه</NavLink>
            <NavLink href="/blog">مجله</NavLink>
            {hasCustomerAccount && <NavLink href="/account/orders" match={["/account/orders","/track"]}>سفارش‌های من <NavigationBadge kind="orders" label="سفارش دیده‌نشده"/></NavLink>}
            {hasCustomerAccount && <NavLink href="/tickets">پشتیبانی <NavigationBadge kind="support" label="پیام پشتیبانی خوانده‌نشده"/></NavLink>}
            <NavLink href="/contact">تماس با ما</NavLink>
          </nav>
          <div className="header-actions">
            {hasCustomerAccount && <NotificationBell />}
            <NavLink exact={!!customerName} href={customerName ? "/account" : "/customer"} match={customerName ? ["/account","/customer/profile","/account/notifications"] : "/customer"} className="customer-header-link" label={customerName ? "حساب کاربری" : "ورود"}>
              <UserRound size={21} /><span>{customerName || "ورود"}</span>
            </NavLink>
            <NavLink href="/order" className="button button-sm">
              ثبت سفارش <ArrowUpLeft size={17} />
            </NavLink>
            <button
              ref={toggle}
              type="button"
              className="mobile-toggle icon-button"
              onClick={() => setOpen(!open)}
              aria-label={open ? "بستن منو" : "باز کردن منو"}
              aria-expanded={open}
              aria-controls="giti-main-nav"
            >
              {open ? <X /> : <Menu />}
            </button>
          </div>
        </div>
      </header>
    </>
  );
}
