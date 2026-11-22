import Image from "next/image";
import { Fragment, type ReactNode } from "react";
import { publicLink } from "@/lib/magazine";
function inline(text: string): ReactNode {
  const tokens = text.split(/(!?\[[^\]]*\]\([^\s)]+\)|\*\*[^*]+\*\*|`[^`]+`)/g);
  return tokens.map((token, index) => {
    if (token.startsWith("**") && token.endsWith("**"))
      return <strong key={index}>{token.slice(2, -2)}</strong>;
    if (token.startsWith("`") && token.endsWith("`"))
      return (
        <code key={index} dir="auto">
          {token.slice(1, -1)}
        </code>
      );
    const match = token.match(/^(!?)\[([^\]]*)\]\(([^\s)]+)\)$/);
    if (match) {
      const [, image, label, url] = match;
      if (image && /^\/api\/banner-assets\/[a-fA-F0-9-]{36}$/.test(url))
        return (
          <Image
            unoptimized
            className="article-inline-image"
            key={index}
            src={url}
            alt={label}
            width={900}
            height={560}
          />
        );
      if (!image && publicLink(url))
        return (
          <a
            key={index}
            href={url}
            rel={url.startsWith("https:") ? "noopener noreferrer" : undefined}
          >
            {label}
          </a>
        );
      return <Fragment key={index}>{label}</Fragment>;
    }
    return <Fragment key={index}>{token}</Fragment>;
  });
}
// Defined Markdown v1: h2/h3, paragraphs, lists, quotes, inline formatting and scrollable tables.
// React escapes all text; raw HTML is never rendered.
export default function ArticleContent({ content }: { content: string }) {
  const lines = content.replaceAll("\r\n", "\n").split("\n"),
    nodes: ReactNode[] = [];
  let i = 0;
  while (i < lines.length) {
    const line = lines[i].trim();
    if (!line) {
      i++;
      continue;
    }
    const heading = line.match(/^(#{1,3})\s+(.+)$/);
    if (heading) {
      nodes.push(
        heading[1].length === 3 ? (
          <h3 key={i}>{inline(heading[2])}</h3>
        ) : (
          <h2 key={i}>{inline(heading[2])}</h2>
        ),
      );
      i++;
      continue;
    }
    if (/^[-*]\s+/.test(line) || /^\d+\.\s+/.test(line)) {
      const ordered = /^\d+\./.test(line),
        items: ReactNode[] = [],
        start = i;
      while (
        i < lines.length &&
        (ordered ? /^\d+\.\s+/ : /^[-*]\s+/).test(lines[i].trim())
      ) {
        items.push(
          <li key={i}>
            {inline(
              lines[i].trim().replace(ordered ? /^\d+\.\s+/ : /^[-*]\s+/, ""),
            )}
          </li>,
        );
        i++;
      }
      nodes.push(
        ordered ? <ol key={start}>{items}</ol> : <ul key={start}>{items}</ul>,
      );
      continue;
    }
    if (line.startsWith("> ")) {
      nodes.push(<blockquote key={i}>{inline(line.slice(2))}</blockquote>);
      i++;
      continue;
    }
    if (line.startsWith("|") && lines[i + 1]?.match(/^\s*\|?[\s:|-]+\|\s*$/)) {
      const start = i,
        cells = (s: string) =>
          s
            .trim()
            .replace(/^\||\|$/g, "")
            .split("|")
            .map((c) => c.trim()),
        head = cells(line),
        rows: string[][] = [];
      i += 2;
      while (i < lines.length && lines[i].trim().startsWith("|")) {
        rows.push(cells(lines[i]));
        i++;
      }
      nodes.push(
        <div
          key={start}
          className="article-table-wrap"
          role="region"
          aria-label="جدول مقاله"
          tabIndex={0}
        >
          <table>
            <thead>
              <tr>
                {head.map((c, n) => (
                  <th scope="col" key={n}>
                    {inline(c)}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {rows.map((row, n) => (
                <tr key={n}>
                  {row.map((c, k) => (
                    <td key={k}>{inline(c)}</td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>,
      );
      continue;
    }
    const start = i,
      paragraph = [line];
    i++;
    while (
      i < lines.length &&
      lines[i].trim() &&
      !/^(#{1,3}\s|[-*]\s|\d+\.\s|>\s|\|)/.test(lines[i].trim())
    ) {
      paragraph.push(lines[i].trim());
      i++;
    }
    nodes.push(<p key={start}>{inline(paragraph.join("\n"))}</p>);
  }
  return <div className="article-content">{nodes}</div>;
}
