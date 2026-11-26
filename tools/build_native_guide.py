#!/usr/bin/env python3
"""Build native read-only data and an offline book from the reviewed central JSON source.
No third-party package, runtime HTML execution, shop data or duplicate authored prose.
Run --check in validation; normal mode refreshes the two generated artifacts.
"""
from pathlib import Path
import html
import json
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'docs/guide-prototype'
DATA = ROOT / 'dotnet/Area11.Desktop/Resources/Guide/guide.json'
BOOK = SOURCE / 'book.html'


def artifacts():
    authored = json.loads((ROOT / 'docs/guide/guide-source.json').read_text())
    version, date = authored['version'], authored['date']
    topics = [{key: topic[key] for key in ('id', 'title', 'parent', 'path', 'blocks', 'links')}
              for topic in authored['topics']]
    assert len(topics) == 96
    for topic in authored['topics']:
        assert topic['blocks'] and topic['sources'], topic['id']
        for reference in topic['sources']:
            assert (ROOT / reference.split('#')[0]).is_file(), reference
    source = (SOURCE / 'index.html').read_text()  # historical offline font/CSS, not authored prose
    sections = []
    ids = {t['id'] for t in topics}
    assert len(ids) == len(topics) and len([t for t in topics if not t['parent']]) == 6
    for t in topics:
        assert not t['parent'] or t['parent'] in ids
        assert all(link in ids for link in t['links'])
        ident = html.escape(t['id']); title = html.escape(t['title'])
        content = []
        for b in t['blocks']:
            text = html.escape(b['text'])
            if b['kind'] == 'table':
                content.append('<table>' + ''.join('<tr>' + ''.join('<td>' + html.escape(c) + '</td>' for c in row) + '</tr>' for row in b['rows']) + '</table>')
            else:
                tag = 'h3' if b['kind'] == 'heading' else 'p'
                content.append(f'<{tag}>' + ('• ' if b['kind'] == 'item' else '') + text + f'</{tag}>')
        assert content, t['id']
        related = list(dict.fromkeys([n['id'] for n in topics if n['parent'] == t['id']] + t['links']))
        links = ' · '.join('<a href="#' + html.escape(i) + '">' + html.escape(next(n['title'] for n in topics if n['id'] == i)) + '</a>' for i in related)
        sections.append(f'<section id="{ident}"><p class="path">{html.escape(t["path"])}</p><h2>{title}</h2>' + ''.join(content) + '<p>' + links + '</p><a href="#contents">فہرست</a></section>')
    roots = [t for t in topics if not t['parent']]
    toc = ''.join('<li><a href="#' + html.escape(t['id']) + '">' + html.escape(t['title']) + '</a></li>' for t in roots)
    # Reuse the prototype's offline CSS/font and its embedded SIL OFL notice.
    style = re.search(r'(<style>.*?</style>)', source, re.S)[1]
    license_note = re.search(r'(<!-- Embedded subset:.*?-->)', source, re.S)[1]
    book = '<!doctype html><html lang="ur" dir="rtl"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>AREA11 اردو کتاب</title>' + license_note + style + '<style>main{max-width:1100px;margin:auto;padding:28px}table{width:100%;border-collapse:collapse;font-size:16px}td{border:1px solid #ccd8d1;padding:10px;vertical-align:top}section{padding:22px 0;border-bottom:1px solid #ccd8d1}.path{color:#596d78;font-size:14px}h2,h3{break-after:avoid}section{scroll-margin-top:20px}@media print{main{padding:0;max-width:none}tr{break-inside:avoid}}@media(max-width:650px){main{padding:14px}table{font-size:14px}td{padding:6px}}</style></head><body><main><h1 id="contents">AREA11 — اردو رہنما کتاب</h1><p>Docs ' + version + ' · ' + date + '</p><p>چھ حصے اور 96 موضوعات۔ مواد اصل پروگرام کے سورس کے مطابق ہے؛ Windows/WPF، آلات اور حقیقی کاروباری عمل کی عملی تصدیق الگ ضروری ہے۔</p><p>تلاش: Ctrl+F · پرنٹ/PDF: Ctrl+P۔ اس کتاب کو اکیلا نقل کرکےoffline کھول سکتے ہیں۔</p><ol>' + toc + '</ol>' + ''.join(sections) + '</main></body></html>\n'
    return json.dumps(dict(version=version, date=date, topics=topics), ensure_ascii=False, indent=2) + '\n', book


def main():
    data, book = artifacts()
    for path, content in ((DATA, data), (BOOK, book)):
        if '--check' in sys.argv:
            assert path.exists() and path.read_text() == content, 'Regenerate ' + str(path.relative_to(ROOT))
        else:
            path.parent.mkdir(parents=True, exist_ok=True); path.write_text(content)
    print('PASS native guide/offline book exact source parity;6 roots/96 populated topics;not runtime proof')


if __name__ == '__main__': main()
