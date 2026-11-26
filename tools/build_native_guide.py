#!/usr/bin/env python3
"""Build native read-only data and an offline book from the approved HTML prototype.
No third-party package, runtime HTML execution, shop data or duplicate authored prose.
Run --check in validation; normal mode refreshes the two generated artifacts.
"""
from pathlib import Path
from html.parser import HTMLParser
import html
import json
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'docs/guide-prototype'
DATA = ROOT / 'dotnet/Area11.Desktop/Resources/Guide/guide.json'
BOOK = SOURCE / 'book.html'


class Article(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.main = False
        self.blocks = []
        self.links = []
        self.kind = None
        self.words = []
        self.rows = None
        self.row = None
        self.cell = None

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if tag == 'main': self.main = True
        if not self.main: return
        if tag == 'a':
            href = attrs.get('href', '')
            match = re.fullmatch(r'index\.html\?topic=([a-z0-9.-]+)', href)
            if match: self.links.append(match[1])
        if tag == 'table': self.rows = []
        elif tag == 'tr': self.row = []
        elif tag in ('td', 'th'): self.cell = []
        elif self.rows is None and (tag in ('h2', 'h3', 'p', 'li') or (tag == 'div' and attrs.get('class') in ('note', 'warning'))):
            self.kind = 'heading' if tag.startswith('h') else 'item' if tag == 'li' else 'paragraph'
            self.words = []

    def handle_data(self, data):
        if not self.main: return
        if self.cell is not None: self.cell.append(data)
        elif self.kind: self.words.append(data)

    def handle_endtag(self, tag):
        if tag == 'main': self.main = False
        if tag in ('td', 'th') and self.cell is not None:
            self.row.append(' '.join(''.join(self.cell).split())); self.cell = None
        elif tag == 'tr' and self.row is not None:
            self.rows.append(self.row); self.row = None
        elif tag == 'table' and self.rows is not None:
            self.blocks.append(dict(kind='table', text='', rows=self.rows)); self.rows = None
        elif tag in ('h2', 'h3', 'p', 'li', 'div') and self.kind:
            text = ' '.join(''.join(self.words).split())
            if text: self.blocks.append(dict(kind=self.kind, text=text, rows=[]))
            self.kind = None


def artifacts():
    source = (SOURCE / 'index.html').read_text()
    groups = json.loads(re.search(r'const GROUPS=(.*?);\nconst MAP=', source, re.S)[1])
    files = {'sales.no-bill': 'no-bill.html', 'system.settings.printing': 'printing.html', 'basics.login': 'login.html'}
    version, date = re.search(r'رف نمونہ (R[0-9]+) · ([0-9-]+)', source).groups()
    topics = []
    sections = []
    for group in groups:
        def walk(node, parent='', trail=()):
            title, ident = node['title'], node['id']
            path = (*trail, title)
            parser = Article()
            if ident in files:
                text = (SOURCE / files[ident]).read_text()
                assert f'رف نمونہ {version} · {date}' in text
                parser.feed(text)
            topics.append(dict(id=ident, title=title, parent=parent, path=' ← '.join(path),
                               blocks=parser.blocks, links=list(dict.fromkeys(parser.links))))
            for child in node['children']: walk(child, ident, path)
        walk(group)
    ids = {t['id'] for t in topics}
    assert len(ids) == len(topics) and len(groups) == 6
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
        if not content: content.append('<p>یہاں «' + title + '» کی معلومات آئے گی۔</p>')
        related = list(dict.fromkeys([n['id'] for n in topics if n['parent'] == t['id']] + t['links']))
        links = ' · '.join('<a href="#' + html.escape(i) + '">' + html.escape(next(n['title'] for n in topics if n['id'] == i)) + '</a>' for i in related)
        sections.append(f'<section id="{ident}"><p class="path">{html.escape(t["path"])}</p><h2>{title}</h2>' + ''.join(content) + '<p>' + links + '</p><a href="#contents">فہرست</a></section>')
    roots = [t for t in topics if not t['parent']]
    toc = ''.join('<li><a href="#' + html.escape(t['id']) + '">' + html.escape(t['title']) + '</a></li>' for t in roots)
    # Reuse the prototype's offline CSS/font and its embedded SIL OFL notice.
    style = re.search(r'(<style>.*?</style>)', source, re.S)[1]
    license_note = re.search(r'(<!-- Embedded subset:.*?-->)', source, re.S)[1]
    book = '<!doctype html><html lang="ur" dir="rtl"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>AREA11 اردو کتاب</title>' + license_note + style + '<style>main{max-width:1100px;margin:auto;padding:28px}table{width:100%;border-collapse:collapse;font-size:16px}td{border:1px solid #ccd8d1;padding:10px;vertical-align:top}section{padding:22px 0;border-bottom:1px solid #ccd8d1}.path{color:#596d78;font-size:14px}h2,h3{break-after:avoid}section{scroll-margin-top:20px}@media print{main{padding:0;max-width:none}tr{break-inside:avoid}}@media(max-width:650px){main{padding:14px}table{font-size:14px}td{padding:6px}}</style></head><body><main><h1 id="contents">AREA11 — اردو کتاب کا محدود نمونہ</h1><p>Docs ' + version + ' · ' + date + '</p><p>چھ حصے؛صرف No Bill،Printing اورLogin کے تین مضامین مکمل نمونے ہیں۔ باقی عنوانات placeholders ہیں۔ یہ مکمل کتاب یا WPF تصدیق نہیں۔</p><p>تلاش: Ctrl+F · پرنٹ/PDF: Ctrl+P۔ اس کتاب کو اکیلا نقل کرکےoffline کھول سکتے ہیں۔</p><ol>' + toc + '</ol>' + ''.join(sections) + '</main></body></html>\n'
    return json.dumps(dict(version=version, date=date, topics=topics), ensure_ascii=False, indent=2) + '\n', book


def main():
    data, book = artifacts()
    for path, content in ((DATA, data), (BOOK, book)):
        if '--check' in sys.argv:
            assert path.exists() and path.read_text() == content, 'Regenerate ' + str(path.relative_to(ROOT))
        else:
            path.parent.mkdir(parents=True, exist_ok=True); path.write_text(content)
    print('PASS native guide/offline book exact source parity;6 roots/3 sample articles;not runtime proof')


if __name__ == '__main__': main()
