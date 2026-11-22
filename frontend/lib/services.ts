export type ServiceRecord = {
  id: string; slug: string; title: string; subtitle: string; description: string;
  fullDescription: string; image: string; icon: string; color: string;
  features: string[]; sections: { heading: string; body: string }[];
  seoTitle: string; seoDescription: string; displayOrder: number; active: boolean;
  updatedAt?: string; version: number; aliases: string[];
};
export function servicePayload(service: ServiceRecord) {
  return Object.fromEntries(Object.entries(service).filter(([key]) => key !== "id" && key !== "aliases" && key !== "updatedAt"));
}
export function consultationUrl() {
  return "/contact";
}
