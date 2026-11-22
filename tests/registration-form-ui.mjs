import { PASSWORD_GUIDANCE } from "../frontend/lib/password-policy.ts";

// Used only inside the existing memory-only API/browser harness.
export async function passwordLabelChecks(page, check, purpose, root = page) {
  const initial = purpose === "initial";
  const password = root.getByLabel(initial ? "رمز عبور" : "رمز عبور جدید", { exact: true });
  const confirmation = root.getByLabel(initial ? "تکرار رمز عبور" : "تکرار رمز عبور جدید", { exact: true });
  check(await password.count() === 1 && await confirmation.count() === 1,
    initial ? "registration uses exact accessible initial-password and confirmation labels" : "recovery/password change retains exact accessible new-password labels");
  check(await password.getAttribute("type") === "password" && await confirmation.getAttribute("type") === "password" &&
    await password.getAttribute("autocomplete") === "new-password" && await password.getAttribute("minlength") === "6",
    "password fields retain their masked input, autofill context and six-character minimum");
  const guidance = await password.evaluate(input => {
    const hint = document.getElementById(input.getAttribute("aria-describedby"));
    return { text: hint?.textContent, below: hint?.getBoundingClientRect().top >= input.getBoundingClientRect().bottom };
  });
  check(guidance.text === PASSWORD_GUIDANCE && guidance.below && await root.getByText(PASSWORD_GUIDANCE, { exact: true }).isVisible(),
    "the unchanged Persian password requirements remain visible below the associated input");
}

export async function nameLayoutChecks(page, check, formName) {
  const original = page.viewportSize();
  try {
    await page.evaluate(() => document.fonts.ready);
    for (const width of [1440, 1024, 768, 601, 600, 390, 320]) {
      await page.setViewportSize({ width, height: 1000 });
      await page.evaluate(() => new Promise(requestAnimationFrame));
      const layout = await page.locator(".entry-name-grid").evaluate(grid => {
        const inputs = [...grid.querySelectorAll("input")];
        return {
          direction: getComputedStyle(grid).direction,
          columns: getComputedStyle(grid).gridTemplateColumns.split(" ").length,
          overflow: document.documentElement.scrollWidth > innerWidth + 1,
          fields: inputs.map(input => {
            const box = input.getBoundingClientRect(), style = getComputedStyle(input);
            return { name: input.name, x: box.x, y: box.y, width: box.width, height: box.height, radius: style.borderRadius,
              associated: [...input.labels].some(label => label.contains(input)) };
          }),
        };
      });
      const [first, last] = layout.fields;
      check(layout.direction === "rtl" && first.name === "firstName" && last.name === "lastName" && first.associated && last.associated,
        `${formName}: accessible name fields retain first/last DOM order and RTL at ${width}px`);
      check(Math.abs(first.width - last.width) <= 1 && Math.abs(first.height - last.height) <= 1 && first.radius === last.radius && !layout.overflow,
        `${formName}: equal input widths/heights, shared border style and no overflow at ${width}px`);
      check(width > 600 ? layout.columns === 2 && Math.abs(first.y - last.y) <= 1 && first.x > last.x
        : layout.columns === 1 && first.y < last.y && Math.abs(first.x - last.x) <= 1,
        `${formName}: ${width > 600 ? "first name right, last name left in two columns" : "first name above last name in one column"} at ${width}px`);
    }
    const first = page.getByLabel("نام", { exact: true }), last = page.getByLabel("نام خانوادگی", { exact: true });
    await first.focus();
    const firstFocus = await first.evaluate(input => ({ border: getComputedStyle(input).borderColor, outline: getComputedStyle(input).outlineStyle }));
    await last.focus();
    const lastFocus = await last.evaluate(input => ({ border: getComputedStyle(input).borderColor, outline: getComputedStyle(input).outlineStyle }));
    check(JSON.stringify(firstFocus) === JSON.stringify(lastFocus), `${formName}: both name inputs share their focus styling`);
  } finally { if (original) await page.setViewportSize(original); }
}
