"use client";
import Link from "next/link";
import Image from "next/image";
import { useEffect, useState, useSyncExternalStore } from "react";
import { ArrowUpLeft, ArrowLeft, ChevronRight, ChevronLeft, Pause, Play, Calculator } from "lucide-react";
import type { Banner } from "@/lib/banners";
const motionQuery = "(prefers-reduced-motion: reduce)";
function subscribeMotion(listener: () => void) {
  const query = window.matchMedia(motionQuery);
  query.addEventListener("change", listener);
  return () => query.removeEventListener("change", listener);
}
export default function BannerShowcase({ banners }: { banners: Banner[] }) {
  const [index, setIndex] = useState(0), [manualPlaying, setPlaying] = useState<boolean | null>(null), [paused, setPaused] = useState(false);
  const reducedMotion = useSyncExternalStore(subscribeMotion, () => window.matchMedia(motionQuery).matches, () => true);
  const playing = manualPlaying ?? !reducedMotion;
  useEffect(() => {
    if (!playing || paused || banners.length < 2) return;
    const timer = setInterval(() => setIndex(value => (value + 1) % banners.length), 7000);
    return () => clearInterval(timer);
  }, [playing, paused, banners.length]);
  const banner = banners[index] || banners[0];
  if (!banner) return <section className="container empty-showcase"><h1>چاپ گیتی؛ از ایده تا کتاب</h1><Link prefetch={false} className="button" href="/calculator">محاسبه هزینه چاپ<ArrowUpLeft size={18} /></Link></section>;
  function move(delta: number) { setIndex(value => (value + banners.length + delta) % banners.length); }
  return <section className="showcase-section"><div className="container showcase-grid">
    <div className={"hero-banner " + banner.theme} role="region" aria-roledescription="اسلایدشو" aria-label="بنرهای چاپ گیتی" onMouseEnter={() => setPaused(true)} onMouseLeave={() => setPaused(false)} onFocusCapture={() => setPaused(true)} onBlurCapture={() => setPaused(false)}>
      <div className="hero-banner-copy"><span className="hero-banner-tag">{banner.eyebrow}</span><h1>{banner.title}</h1><p>{banner.subtitle}</p>{banner.label && banner.link && <Link prefetch={false} className="button button-white" href={banner.link}>{banner.label}<ArrowUpLeft size={19} /></Link>}</div>
      <Image width={1280} height={720} unoptimized loading="eager" className="hero-banner-image" src={banner.image} alt={banner.kind === "portfolio" ? banner.eyebrow : ""} />
      <div className="banner-controls">
        <div className="banner-arrows"><button type="button" onClick={() => move(-1)} aria-label="بنر قبلی"><ChevronRight size={19} /></button><button type="button" onClick={() => move(1)} aria-label="بنر بعدی"><ChevronLeft size={19} /></button></div>
        <div className="banner-dots">{banners.map((item, position) => <button type="button" key={item.id} className={position === index ? "active" : ""} aria-label={"نمایش بنر " + (position + 1)} aria-pressed={position === index} onClick={() => setIndex(position)} />)}</div>
        <button type="button" className="banner-play" onClick={() => setPlaying(!playing)} aria-label={playing ? "توقف نمایش خودکار" : "شروع نمایش خودکار"}>{playing ? <Pause size={15} /> : <Play size={15} />}</button>
      </div>
    </div>
    <div className="showcase-side">
      <Link prefetch={false} className="showcase-quote" href="/calculator"><span className="side-icon"><Calculator size={26} /></span><small>قبل از سفارش، حساب کن</small><h2>هزینه چاپ<br />کتاب من؟</h2><p>یک محاسبه دقیق‌تر،<br />با انتخاب‌های بیشتر.</p><span className="side-link">شروع محاسبه<ArrowLeft size={18} /></span></Link>

    </div>
  </div></section>;
}
