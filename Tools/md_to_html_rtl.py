"""Simple Markdown (headings, paragraphs, lists, pipe tables, **bold**, `code`, > notes) to a printable RTL HTML page.
Used for the Arabic guide PDF: python Tools/md_to_html_rtl.py Docs/GUIDE_AR.md out.html, then Edge --print-to-pdf."""
import html, re, sys

def inline(t):
    t = html.escape(t)
    t = re.sub(r'\*\*(.+?)\*\*', r'<b>\1</b>', t)
    return re.sub(r'`(.+?)`', r'<code dir="ltr">\1</code>', t)

def convert(lines):
    out, i, lst = [], 0, None
    def close():
        nonlocal lst
        if lst: out.append(f'</{lst}>'); lst = None
    while i < len(lines):
        l = lines[i].rstrip()
        if not l.strip(): close(); i += 1; continue
        if m := re.match(r'^(#{1,3})\s+(.*)', l): close(); n = len(m[1]); out.append(f'<h{n}>{inline(m[2])}</h{n}>'); i += 1; continue
        if l.startswith('|'):
            close(); rows = []
            while i < len(lines) and lines[i].startswith('|'):
                if not re.fullmatch(r'\|[\s:|-]+\|', lines[i].strip()): rows.append([c.strip() for c in lines[i].strip().strip('|').split('|')])
                i += 1
            out.append('<table><thead><tr>' + ''.join(f'<th>{inline(c)}</th>' for c in rows[0]) + '</tr></thead><tbody>' +
                       ''.join('<tr>' + ''.join(f'<td>{inline(c)}</td>' for c in r) + '</tr>' for r in rows[1:]) + '</tbody></table>'); continue
        if l.startswith('> '): close(); out.append(f'<blockquote>{inline(l[2:])}</blockquote>'); i += 1; continue
        b, n = re.match(r'^\s*-\s+(.*)', l), re.match(r'^\s*\d+\.\s+(.*)', l)
        if b or n:
            kind = 'ul' if b else 'ol'
            if lst != kind: close(); out.append(f'<{kind}>'); lst = kind
            out.append(f'<li>{inline((b or n)[1])}</li>'); i += 1; continue
        close(); out.append(f'<p>{inline(l)}</p>'); i += 1
    close(); return '\n'.join(out)

CSS = """body{font-family:'Segoe UI',Tahoma,Arial,sans-serif;font-size:11.5pt;line-height:1.7;color:#1b2321;margin:0}
h1{color:#1f4e48;font-size:22pt;border-bottom:3px solid #36c9a3;padding-bottom:6px}h2{color:#1f4e48;font-size:15pt;margin-top:22px;break-after:avoid}
table{border-collapse:collapse;width:100%;margin:8px 0 14px;break-inside:avoid}th,td{border:1px solid #b9cbc6;padding:5px 8px;text-align:right;vertical-align:top}
th{background:#dce8e4}code{font-family:Consolas,monospace;font-size:9.5pt;background:#eef3f2;padding:0 3px;border-radius:3px}
blockquote{margin:0 0 14px;padding:8px 12px;border-right:4px solid #36c9a3;background:#f3f8f7}li{margin:3px 0}@page{size:A4;margin:18mm}"""

if __name__ == '__main__':
    src, dst = sys.argv[1], sys.argv[2]
    body = convert(open(src, encoding='utf-8').read().splitlines())
    open(dst, 'w', encoding='utf-8').write(f'<!doctype html><html lang="ar" dir="rtl"><head><meta charset="utf-8"><title>GripRehab guide</title><style>{CSS}</style></head><body>{body}</body></html>')
