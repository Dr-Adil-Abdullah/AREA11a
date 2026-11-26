#!/usr/bin/env python3
"""Independent static/data checks only; never claims Windows/WPF execution."""
import base64
from html.parser import HTMLParser
import json
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'docs/guide-prototype'
VIEWS = ROOT / 'dotnet/Area11.Desktop/Views'
checks = 0


def check(condition, message):
    global checks
    assert condition, message
    checks += 1


class MainText(HTMLParser):
    def __init__(self):
        super().__init__()
        self.inside = False
        self.fragments = []

    def handle_starttag(self, tag, attrs):
        if tag == 'main': self.inside = True

    def handle_endtag(self, tag):
        if tag == 'main': self.inside = False

    def handle_data(self, text):
        if self.inside and text.strip(): self.fragments.append(' '.join(text.split()))


def main():
    subprocess.run(['python3', str(ROOT / 'tools/build_native_guide.py'), '--check'], check=True)
    bundle = json.loads((ROOT / 'dotnet/Area11.Desktop/Resources/Guide/guide.json').read_text())
    topics = {topic['id']: topic for topic in bundle['topics']}
    check(len(topics) == len(bundle['topics']), 'unique topic IDs')
    check([t['id'] for t in topics.values() if not t['parent']] ==
          ['sales', 'stock', 'customers', 'money', 'system', 'basics'], 'six approved sections/order')
    samples = {'sales.no-bill': 'no-bill.html', 'system.settings.printing': 'printing.html', 'basics.login': 'login.html'}
    check({t['id'] for t in topics.values() if t['blocks']} == set(samples), 'only three complete sample articles')
    for ident, topic in topics.items():
        check(not topic['parent'] or topic['parent'] in topics, 'parent ' + ident)
        check(all(link in topics for link in topic['links']), 'related links ' + ident)
        seen = {ident}
        parent = topic['parent']
        while parent:
            check(parent not in seen, 'cycle ' + ident)
            seen.add(parent)
            parent = topics[parent]['parent']
    book = (SOURCE / 'book.html').read_text()
    book_parser = MainText(); book_parser.feed(book)
    book_text = ' '.join(book_parser.fragments)
    for ident, filename in samples.items():
        source = (SOURCE / filename).read_text()
        check(f"رف نمونہ {bundle['version']} · {bundle['date']}" in source, 'shared source metadata ' + filename)
        parser = MainText(); parser.feed(source)
        topic = topics[ident]
        native_text = ' '.join(block['text'] + ' ' + ' '.join(cell for row in block['rows'] for cell in row) for block in topic['blocks'])
        native_text = ' '.join(native_text.split())
        for fragment in parser.fragments:
            check(fragment in native_text, 'native dropped source text: ' + fragment)
            check(fragment in book_text, 'book dropped source text: ' + fragment)
    # Embedded font is exactly the previously published licensed font, not rewritten metadata.
    for filename in ['index.html', *samples.values(), 'book.html']:
        source = (SOURCE / filename).read_text()
        font = base64.b64decode(re.search(r'data:font/woff2;base64,([A-Za-z0-9+/=]+)', source)[1], validate=True)
        check(font[:4] == b'wOF2', 'valid font signature ' + filename)
        check(len(font) > 70000, 'font is nonempty ' + filename)
        published = subprocess.check_output(['git', 'show', '2d16680:docs/guide-prototype/index.html'], cwd=ROOT, text=True)
        check(font == base64.b64decode(re.search(r'data:font/woff2;base64,([A-Za-z0-9+/=]+)', published)[1]), 'font bytes retained ' + filename)
        check('SIL OPEN FONT LICENSE' in source, 'offline license ' + filename)
    check('<script' not in book and 'src="http' not in book and 'href="http' not in book, 'standalone offline book')
    check('محدود نمونہ' in book and 'placeholders' in book, 'sample book honestly labelled')
    for ident in topics:
        check(f'id="{ident}"' in book, 'offline topic anchor ' + ident)
    for path in VIEWS.glob('*.xaml'):
        ET.parse(path)
        for ident in re.findall(r'GuideContext.Topic="([^"]+)"', path.read_text()):
            if not ident.startswith('{'): check(ident in topics, 'mapped XAML topic ' + str(path))
    context = (VIEWS / 'GuideContext.cs').read_text()
    for ident in re.findall(r'=> "([a-z][a-z.-]+)"', context):
        check(ident in topics, 'mapped header route ' + ident)
    for phrase in ['TextBoxBase or PasswordBox or DataGrid or Selector', 'item.ContextMenu is not null',
                   'window.Show();', 'owner.IsActive', 'element.IsVisible && element.IsEnabled', 'Help ابھی تیار نہیں']:
        check(phrase in context, 'context safety source boundary ' + phrase)
    guide_code = '\n'.join((VIEWS / f).read_text() for f in ['GuideContext.cs', 'GuideView.xaml.cs', 'GuideWindow.xaml.cs'])
    for forbidden in ['ShowDialog(', 'SaveCommand', 'CancelCommand', 'RefreshCommand', 'Db.', 'Services.', 'WebView2']:
        # Comments may mention the forbidden operation to explain why it is absent.
        uncommented = re.sub(r'//[^\n]*', '', guide_code)
        check(forbidden not in uncommented, 'read-only source ' + forbidden)
    shell = (VIEWS / 'ShellWindow.xaml.cs').read_text()
    body = shell.split('internal void ShowGuidelines()')[1].split('///')[0]
    check('CurrentViewModel =' not in body and 'OpenPage' not in body, 'original view remains attached')
    check('guide.FocusSearch();' in body, 'intentional keyboard focus')
    shell_xaml = (VIEWS / 'ShellWindow.xaml').read_text()
    check('KeyboardNavigation.TabNavigation="Cycle"' in shell_xaml and 'ElementName=GuideHost' in shell_xaml,
          'overlay keyboard containment source')
    project = (ROOT / 'dotnet/Area11.Desktop/Area11.Desktop.csproj').read_text()
    check('<Link>Guide\\guide.json</Link>' in project and '<CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>' in project,
          'package output/publish copy source')
    launcher = '\n'.join(line for line in (ROOT / 'URDU-GUIDE.cmd').read_text().splitlines() if not line.lower().startswith('rem '))
    check('start "" "%PAGE%"' in launcher and 'git ' not in launcher and 'dotnet ' not in launcher,
          'read-only offline launcher source')
    print(f'PASS {checks} native-guide static/data assertions; NOT .NET/WPF/focus/Windows-launch proof')


if __name__ == '__main__': main()
