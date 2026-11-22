"use client";
export default function ErrorPage({ reset }: { reset: () => void }) {
  return (
    <main id="main" className="container error-page">
      <h1>بارگذاری صفحه کامل نشد.</h1>
      <p>لطفاً دوباره تلاش کنید.</p>
      <button className="button" onClick={reset}>
        تلاش دوباره
      </button>
    </main>
  );
}
