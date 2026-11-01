// Converts a simple Markdown file (headings, paragraphs, bullet/numbered lists, pipe tables, **bold**, `code`) to an
// RTL Word document. Used for the Arabic guide: node Tools/md_to_docx_rtl.js Docs/GUIDE_AR.md Docs/GUIDE_AR.docx
const fs = require('fs');
const { Document, Packer, Paragraph, TextRun, HeadingLevel, Table, TableRow, TableCell, WidthType, ShadingType, AlignmentType, LevelFormat, BorderStyle } = require('docx');
const [src, out] = process.argv.slice(2);
const lines = fs.readFileSync(src, 'utf8').split(/\r?\n/);
const FONT = 'Arial', W = 9026; // A4 text width in DXA (1" margins)

function runs(text, opts = {}) {
  const parts = []; const re = /(\*\*[^*]+\*\*|`[^`]+`)/g; let last = 0, m;
  while ((m = re.exec(text))) { if (m.index > last) parts.push([text.slice(last, m.index), {}]); const t = m[0];
    parts.push(t.startsWith('**') ? [t.slice(2, -2), { bold: true }] : [t.slice(1, -1), { font: 'Consolas' }]); last = m.index + t.length; }
  if (last < text.length) parts.push([text.slice(last), {}]);
  return parts.map(([t, o]) => new TextRun({ text: t, rightToLeft: true, font: FONT, size: 22, ...opts, ...o }));
}
const para = (text, o = {}) => new Paragraph({ bidirectional: true, alignment: AlignmentType.RIGHT, spacing: { after: 120 }, children: runs(text, o.run), ...o.p });
const cells = l => l.trim().replace(/^\||\|$/g, '').split('|').map(c => c.trim());

const body = []; let i = 0;
while (i < lines.length) {
  const l = lines[i];
  if (!l.trim()) { i++; continue; }
  const h = /^(#{1,3})\s+(.*)/.exec(l);
  if (h) { body.push(new Paragraph({ bidirectional: true, heading: [HeadingLevel.HEADING_1, HeadingLevel.HEADING_2, HeadingLevel.HEADING_3][h[1].length - 1], children: runs(h[2]) })); i++; continue; }
  if (l.startsWith('|')) {
    const rows = []; while (i < lines.length && lines[i].startsWith('|')) { if (!/^\|[\s:|-]+\|$/.test(lines[i].trim())) rows.push(cells(lines[i])); i++; }
    const n = rows[0].length, cw = Math.floor(W / n), widths = Array(n).fill(cw);
    body.push(new Table({ width: { size: cw * n, type: WidthType.DXA }, columnWidths: widths, visuallyRightToLeft: true,
      rows: rows.map((r, ri) => new TableRow({ tableHeader: ri === 0, children: r.map(c => new TableCell({ width: { size: cw, type: WidthType.DXA },
        shading: ri === 0 ? { type: ShadingType.CLEAR, color: 'auto', fill: 'DCE8E4' } : undefined, margins: { top: 60, bottom: 60, left: 100, right: 100 },
        children: [para(c, { run: ri === 0 ? { bold: true } : {} })] })) })) }));
    body.push(new Paragraph({ children: [] })); continue;
  }
  if (l.startsWith('> ')) { body.push(new Paragraph({ bidirectional: true, alignment: AlignmentType.RIGHT, spacing: { after: 160 },
    border: { right: { style: BorderStyle.SINGLE, size: 12, color: '36C9A3', space: 8 } }, children: runs(l.slice(2), { italics: true }) })); i++; continue; }
  const b = /^\s*-\s+(.*)/.exec(l), n = /^\s*\d+\.\s+(.*)/.exec(l);
  if (b || n) { body.push(new Paragraph({ bidirectional: true, alignment: AlignmentType.RIGHT, numbering: { reference: b ? 'bullets' : 'numbers', level: 0 }, spacing: { after: 80 }, children: runs((b || n)[1]) })); i++; continue; }
  body.push(para(l)); i++;
}
const doc = new Document({
  styles: { default: { document: { run: { font: FONT, size: 22 } } },
    paragraphStyles: [1, 2, 3].map(k => ({ id: 'Heading' + k, name: 'Heading ' + k, basedOn: 'Normal', next: 'Normal', quickFormat: true,
      run: { font: FONT, size: [36, 30, 26][k - 1], bold: true, color: '1F4E48' }, paragraph: { spacing: { before: 240, after: 120 }, outlineLevel: k - 1 } })) },
  numbering: { config: [
    { reference: 'bullets', levels: [{ level: 0, format: LevelFormat.BULLET, text: '•', alignment: AlignmentType.RIGHT, style: { paragraph: { indent: { right: 540, hanging: 270 } } } }] },
    { reference: 'numbers', levels: [{ level: 0, format: LevelFormat.DECIMAL, text: '%1.', alignment: AlignmentType.RIGHT, style: { paragraph: { indent: { right: 540, hanging: 360 } } } }] }] },
  sections: [{ properties: { page: { margin: { top: 1440, bottom: 1440, left: 1440, right: 1440 } } }, children: body }]
});
Packer.toBuffer(doc).then(buf => { fs.writeFileSync(out, buf); console.log('wrote', out, buf.length, 'bytes'); });
