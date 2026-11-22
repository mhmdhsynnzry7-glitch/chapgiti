import { origin, indexed } from "./server";
export function seo(title: string, description: string, path: string) {
  return {
    title,
    description,
    alternates: { canonical: origin() + path },
    robots: { index: indexed(), follow: indexed() },
    openGraph: {
      title,
      description,
      url: origin() + path,
      locale: "fa_IR",
      type: "website",
      images: [origin() + "/og.png"],
    },
  };
}
