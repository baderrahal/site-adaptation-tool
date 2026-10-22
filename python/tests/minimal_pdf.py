def build_pdf(text_items, lines, rects, width=200, height=200):
    """Smallest valid one page PDF. text_items are (x, y, text), y from the bottom."""
    content = []
    for x, y, text in text_items:
        content.append("BT /F1 12 Tf " + str(x) + " " + str(y) + " Td (" + text + ") Tj ET")
    for x1, y1, x2, y2 in lines:
        content.append(str(x1) + " " + str(y1) + " m " + str(x2) + " " + str(y2) + " l S")
    for x, y, w, h in rects:
        content.append(str(x) + " " + str(y) + " " + str(w) + " " + str(h) + " re S")
    stream = "\n".join(content)
    objects = [
        "<< /Type /Catalog /Pages 2 0 R >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 " + str(width) + " " + str(height) + "] "
        "/Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
        "<< /Length " + str(len(stream)) + " >>\nstream\n" + stream + "\nendstream",
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
    ]
    body = "%PDF-1.4\n"
    offsets = []
    for number, obj in enumerate(objects, start=1):
        offsets.append(len(body))
        body += str(number) + " 0 obj\n" + obj + "\nendobj\n"
    xref_at = len(body)
    body += "xref\n0 " + str(len(objects) + 1) + "\n0000000000 65535 f \n"
    for offset in offsets:
        body += str(offset).zfill(10) + " 00000 n \n"
    body += "trailer\n<< /Size " + str(len(objects) + 1) + " /Root 1 0 R >>\nstartxref\n" + str(xref_at) + "\n%%EOF\n"
    return body.encode("latin-1")
