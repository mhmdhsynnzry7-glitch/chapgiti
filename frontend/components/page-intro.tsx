import Link from "next/link";
export default function PageIntro({
  title,
  description,
  eyebrow,
}: {
  title: string;
  description: string;
  eyebrow?: string;
}) {
  return (
    <div className="page-intro">
      <div className="breadcrumb">
        <Link prefetch={false} href="/">خانه</Link>
        <span>/</span>
        <span>{title}</span>
      </div>
      {eyebrow && <span className="eyebrow">{eyebrow}</span>}
      <h1>{title}</h1>
      <p>{description}</p>
    </div>
  );
}
