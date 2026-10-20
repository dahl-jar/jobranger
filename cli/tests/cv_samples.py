from __future__ import annotations

import io
import zipfile
import zlib
from typing import TYPE_CHECKING

from factories import FIXTURES
from pypdf import PdfReader, PdfWriter

if TYPE_CHECKING:
    from collections.abc import Sequence

CV_FIXTURES = FIXTURES / "cv"
CV_PDF = CV_FIXTURES / "cv_user.pdf"
LONG_LINE = "Backend developer at Employer One AS building data pipelines. " * 5
PDF_TEXT_LINE = b"BT /F1 12 Tf 50 700 Td (Backend developer at Employer One AS) Tj ET\n"
PDF_FONT = b"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
DOCUMENT_MEMBER = "word/document.xml"
WORD_NAMESPACES = (
    'xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main" '
    'xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006" '
    'xmlns:v="urn:schemas-microsoft-com:vml" '
    'xmlns:wps="http://schemas.microsoft.com/office/word/2010/wordprocessingShape"'
)


def flate_stream(content: bytes) -> bytes:
    data = zlib.compress(content)
    return b"<< /Length %d /Filter /FlateDecode >>\nstream\n%b\nendstream" % (
        len(data),
        data,
    )


def pdf(page_streams: Sequence[bytes]) -> bytes:
    page_count = len(page_streams)
    first_page = 3 + page_count + 1
    kids = b" ".join(b"%d 0 R" % (first_page + index) for index in range(page_count))
    objects = [
        b"<< /Type /Catalog /Pages 2 0 R >>",
        b"<< /Type /Pages /Kids [%b] /Count %d >>" % (kids, page_count),
        PDF_FONT,
        *page_streams,
    ]
    objects += [
        b"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] "
        b"/Resources << /Font << /F1 3 0 R >> >> /Contents %d 0 R >>" % (4 + index)
        for index in range(page_count)
    ]
    out = io.BytesIO()
    out.write(b"%PDF-1.7\n")
    offsets = []
    for number, body in enumerate(objects, 1):
        offsets.append(out.tell())
        out.write(b"%d 0 obj\n%b\nendobj\n" % (number, body))
    xref = out.tell()
    out.write(b"xref\n0 %d\n0000000000 65535 f \n" % (len(objects) + 1))
    for offset in offsets:
        out.write(b"%010d 00000 n \n" % offset)
    out.write(
        b"trailer\n<< /Size %d /Root 1 0 R >>\nstartxref\n%d\n%%%%EOF\n"
        % (len(objects) + 1, xref)
    )
    return out.getvalue()


def pdf_with_text(text: str) -> bytes:
    content = f"BT /F1 12 Tf 72 720 Td ({text}) Tj ET".encode()
    return pdf([flate_stream(content)])


def repeated_cv_pdf(pages: int) -> bytes:
    source = PdfReader(CV_PDF)
    writer = PdfWriter()
    for index in range(pages):
        writer.add_page(source.pages[index % len(source.pages)])
    out = io.BytesIO()
    writer.write(out)
    return out.getvalue()


def paragraph(text: str) -> str:
    return f"<w:p><w:r><w:t>{text}</w:t></w:r></w:p>"


def word_part(root: str, body: str) -> bytes:
    return (
        f'<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        f"<w:{root} {WORD_NAMESPACES}>{body}</w:{root}>"
    ).encode()


def document(body: str) -> bytes:
    return word_part("document", f"<w:body>{body}</w:body>")


def docx(main_part: bytes | None, parts: Sequence[tuple[str, bytes]] = ()) -> bytes:
    out = io.BytesIO()
    with zipfile.ZipFile(out, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        if main_part is not None:
            archive.writestr(DOCUMENT_MEMBER, main_part)
        for name, content in parts:
            archive.writestr(name, content)
    return out.getvalue()
