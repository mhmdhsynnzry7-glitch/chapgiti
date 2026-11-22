"use client";
import { Combobox } from "@base-ui/react";
import { Check, ChevronDown, Search } from "lucide-react";
import { useId, useState } from "react";
type Option = { value: string; label: string; description?: string };
export default function CatalogSelect({
  label,
  value,
  options,
  onChange,
  disabled = false,
  placeholder,
}: {
  label: string;
  value: string;
  options: Option[];
  onChange: (value: string) => void;
  disabled?: boolean;
  placeholder?: string;
}) {
  const id = useId(),
    [query, setQuery] = useState("");
  const selected = options.find((i) => i.value === value);
  const normalize = (s: string) =>
    s
      .replaceAll("ي", "ی")
      .replaceAll("ك", "ک")
      .replaceAll("‌", " ")
      .toLocaleLowerCase("fa");
  const visible = options.filter((i) =>
    normalize(i.label + " " + (i.description ?? "")).includes(normalize(query)),
  );
  return (
    <div className="field catalog-field">
      <label id={id}>{label}</label>
      <Combobox.Root
        items={visible}
        value={selected ?? null}
        onValueChange={(item) => {
          if (item) onChange(item.value);
        }}
        itemToStringLabel={(item) => item.label}
        itemToStringValue={(item) => item.value}
        onOpenChange={() => setQuery("")}
      >
        <Combobox.Trigger
          className="catalog-trigger"
          aria-labelledby={id}
          disabled={disabled}
        >
          <span>{selected?.label ?? placeholder ?? "انتخاب فعلی در دسترس نیست"}</span>
          <ChevronDown size={17} />
        </Combobox.Trigger>
        <Combobox.Portal>
          <Combobox.Positioner
            className="catalog-positioner" style={{maxWidth:"calc(100vw - 24px)",zIndex:80}}
            sideOffset={6}
            align="start"
            collisionPadding={12}
          >
            <Combobox.Popup className="catalog-popup" dir="rtl">
              <div className="catalog-search">
                <Search size={17} />
                <Combobox.Input
                  aria-label={"جست‌وجو در " + label}
                  placeholder="جست‌وجو…"
                  value={query}
                  onChange={(e) => setQuery(e.target.value)}
                />
              </div>
              <Combobox.Empty className="catalog-empty">
                گزینه‌ای پیدا نشد.
              </Combobox.Empty>
              <Combobox.List className="catalog-list" style={{maxHeight:"min(16rem, 45dvh, calc(var(--available-height, 100vh) - 4rem))",overflowY:"auto",overscrollBehavior:"contain"}}>
                {(item: Option) => (
                  <Combobox.Item
                    className="catalog-option"
                    key={item.value}
                    value={item}
                  >
                    <span>
                      {item.label}
                      {item.description && <small>{item.description}</small>}
                    </span>
                    <Combobox.ItemIndicator>
                      <Check size={17} />
                    </Combobox.ItemIndicator>
                  </Combobox.Item>
                )}
              </Combobox.List>
            </Combobox.Popup>
          </Combobox.Positioner>
        </Combobox.Portal>
      </Combobox.Root>
      {!selected && (value || !placeholder) && (
        <small className="catalog-selection-error" role="alert">
          این گزینه غیرفعال یا تغییر کرده؛ گزینه دیگری انتخاب کنید.
        </small>
      )}
    </div>
  );
}
