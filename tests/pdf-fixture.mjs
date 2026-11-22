// Complete synthetic structure for private-upload tests; contains no customer data.
export function pdfFixture(text="isolated test document") {
  const prefix="%PDF-1.7\n1 0 obj\n<< /Type /Catalog >>\nendobj\n% "+text+"\n";
  return Buffer.from(prefix+"xref\n0 2\n0000000000 65535 f \n0000000009 00000 n \ntrailer\n<< /Size 2 /Root 1 0 R >>\nstartxref\n"+Buffer.byteLength(prefix)+"\n%%EOF\n","ascii");
}

// Exercise the public multipart contract when a browser scenario needs an existing order.
export async function submitPrintRequest(page, base, payload) {
  const response = await page.request.post(base + "/api/orders", {
    headers: { Origin: base },
    multipart: {
      payload: JSON.stringify({ ...payload, termsAccepted: true }),
      file: { name: "sample.pdf", mimeType: "application/pdf", buffer: pdfFixture() },
    },
  });
  return { status: response.status(), body: await response.json() };
}
