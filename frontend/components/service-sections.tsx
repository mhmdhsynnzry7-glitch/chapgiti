"use client";
import { useState } from "react";
import EditorDialog from "./editor-dialog";
import ManagementTable from "./management-table";
type Section = { heading: string; body: string };
export default function SectionEditor({
  sections,
  change,
}: {
  sections: Section[];
  change: (rows: Section[]) => void;
}) {
  const [index, setIndex] = useState<number | null>(null),
    [draft, setDraft] = useState<Section>({ heading: "", body: "" });
  function edit(i: number) {
    setIndex(i);
    setDraft(sections[i] || { heading: "", body: "" });
  }
  function shift(i: number, d: number) {
    const copy = [...sections];
    [copy[i], copy[i + d]] = [copy[i + d], copy[i]];
    change(copy);
  }
  return (
    <section>
      <h3>بخش‌های توضیح</h3>
      <button
        type="button"
        disabled={sections.length >= 8}
        onClick={() => edit(sections.length)}
      >
        افزودن بخش
      </button>
      <ManagementTable
        remote
        title="بخش‌ها"
        columns={["ترتیب", "عنوان بخش", "خلاصه"]}
        rows={sections.map((s, i) => ({
          id: String(i),
          values: [i + 1, s.heading, s.body.slice(0, 100)],
          actions: (
            <>
              <button type="button" onClick={() => edit(i)}>
                ویرایش
              </button>
              <button type="button" disabled={!i} onClick={() => shift(i, -1)}>
                بالا
              </button>
              <button
                type="button"
                disabled={i === sections.length - 1}
                onClick={() => shift(i, 1)}
              >
                پایین
              </button>
              <button
                type="button"
                onClick={() => {
                  if (confirm("این بخش حذف شود؟"))
                    change(sections.filter((_, n) => n !== i));
                }}
              >
                حذف
              </button>
            </>
          ),
        }))}
      />
      {index !== null && (
        <EditorDialog title="ویرایش بخش توضیح" close={() => setIndex(null)}>
          <form
            onSubmit={(e) => {
              e.preventDefault();
              e.stopPropagation();
              const copy = [...sections];
              copy[index] = draft;
              change(copy);
              setIndex(null);
            }}
          >
            <label className="field">
              عنوان بخش
              <input
                required
                minLength={2}
                maxLength={100}
                value={draft.heading}
                onChange={(e) =>
                  setDraft({ ...draft, heading: e.target.value })
                }
              />
            </label>
            <label className="field">
              متن بخش
              <textarea
                required
                minLength={10}
                maxLength={5000}
                value={draft.body}
                onChange={(e) => setDraft({ ...draft, body: e.target.value })}
              />
            </label>
            <button>ذخیره بخش</button>
            <button type="button" onClick={() => setIndex(null)}>
              انصراف
            </button>
          </form>
        </EditorDialog>
      )}
    </section>
  );
}
