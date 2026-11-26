#!/usr/bin/env python3
"""Native guide UI source/data regression checks. NOT WPF compilation or mouse/focus proof."""
import json
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
VIEWS = ROOT / 'dotnet/Area11.Desktop/Views'
NS = {'w': 'http://schemas.microsoft.com/winfx/2006/xaml/presentation'}
NAME = '{http://schemas.microsoft.com/winfx/2006/xaml}Name'
checks = 0


def check(value, label):
    global checks
    assert value, label
    checks += 1


def source_contract(code, dashboard):
    assert 'TopicTree.Items.Add(CreateBranch(root))' in code, 'roots must populate the native tree'
    assert 'item.Items.Add(CreateBranch(child))' in code, 'child branches must recurse'
    assert 'ancestor = _library!.Find(ancestor.Parent)' in code, 'context/search must reveal ancestor chain'
    assert 'branch.IsExpanded = true' in code, 'selected category must reveal its next level'
    assert 'selected.IsSelected = true' in code, 'cards/search must synchronize selection'
    assert 'finally { _changing = wasChanging; }' in code, 'tree selection recursion guard must restore'
    assert 'if (!_changing && e.NewValue is TreeViewItem { Tag: GuideTopic topic })' in code, 'tree navigation must honor guard'
    assert 'children.All(child => child.Id != link.Id)' in code, 'related links must not duplicate children'
    assert 'link.Id != topic.Parent' in code, 'parent belongs in breadcrumbs, not child directory'
    assert 'e.ClickCount != 2' in dashboard and 'e.ChangedButton != MouseButton.Left' in dashboard, 'card must gate double left click'
    assert 'e.Handled = true;' in dashboard and 'OnGuidelines(sender, e);' in dashboard, 'card must use same intentional open path'


def main():
    view = ET.parse(VIEWS / 'GuideView.xaml').getroot()
    named = {node.get(NAME): node for node in view.iter() if node.get(NAME)}
    code = (VIEWS / 'GuideView.xaml.cs').read_text()
    dashboard_code = (VIEWS / 'DashboardView.xaml.cs').read_text()
    source_contract(code, dashboard_code)
    check(True, 'native tree/card source contract')
    for name in ['TopicTree', 'SearchResults', 'BreadcrumbPanel', 'RootGrid', 'ChildrenSection', 'ChildPanel', 'RelatedPanel', 'Reader']:
        check(name in named, 'named region ' + name)
    check(named['TopicTree'].tag.endswith('TreeView'), 'real native hierarchical tree')
    check(named['TopicTree'].get('SelectedItemChanged') == 'OnTreeSelection', 'tree selection event')
    check(named['SearchResults'].get('SelectionChanged') == 'OnSearchSelection', 'separate search selection')
    check(named['SearchResults'].get('DisplayMemberPath') is None, 'do not mix DisplayMemberPath with item template')
    check(named['ChildrenSection'].tag.endswith('Expander') and named['ChildrenSection'].get('IsExpanded') == 'True', 'collapsible expanded directory')
    check(named['ChildrenSection'].get('Grid.Row') == '1' and named['Reader'].get('Grid.Row') == '2', 'directory above article')
    check(named['RelatedSection'].get('Grid.Row') == '3', 'related topics separate below article')
    check(named['Reader'].get('IsToolBarVisible') == 'False', 'quiet reader chrome')
    check('GuideCardButton' in (VIEWS / 'GuideView.xaml').read_text(), 'local rounded-card style')
    check('Background = Brushes.White' not in code and 'Background = GuideBrush("SurfaceBrush")' in code, 'reader uses theme keys rather than fixed white palette')
    check('"BrandSoftBrush"' in code and '"LineBrush"' in code, 'themed tables')
    dashboard = ET.parse(VIEWS / 'DashboardView.xaml').getroot()
    card = next(node for node in dashboard.iter() if node.get(NAME) == 'GuidelinesCard')
    check(card.get('MouseLeftButtonDown') == 'OnGuidelinesCardMouseDown', 'whole card receives double click')
    check(any(node.get('Click') == 'OnGuidelines' for node in card.iter()), 'single-click keyboard-accessible button retained')
    check('تین تیار نمونے' not in ET.tostring(card, encoding='unicode'), 'dashboard stale sample caption removed')
    # Mutation probes run against strings only, never modify repository files.
    for original, replacement, target in [
        ('item.Items.Add(CreateBranch(child))', 'item.Items.Add(child)', 'guide'),
        ('selected.IsSelected = true', 'selected.IsExpanded = true', 'guide'),
        ('finally { _changing = wasChanging; }', 'finally { _changing = true; }', 'guide'),
        ('e.ClickCount != 2', 'e.ClickCount != 1', 'dashboard'),
    ]:
        try:
            source_contract(code.replace(original, replacement) if target == 'guide' else code,
                            dashboard_code.replace(original, replacement) if target == 'dashboard' else dashboard_code)
        except AssertionError:
            check(True, 'reject source mutation ' + original)
        else:
            raise AssertionError('Mutation escaped: ' + original)
    topics = json.loads((ROOT / 'dotnet/Area11.Desktop/Resources/Guide/guide.json').read_text())['topics']
    by_id = {topic['id']: topic for topic in topics}
    children = {topic['id']: [child['id'] for child in topics if child['parent'] == topic['id']] for topic in topics}
    for topic in topics:
        visited = {topic['id']}
        current = topic
        while current['parent']:
            parent = by_id[current['parent']]
            check(current['id'] in children[parent['id']], 'branch membership ' + current['id'])
            check(parent['id'] not in visited, 'acyclic breadcrumb path ' + topic['id'])
            visited.add(parent['id'])
            current = parent
    check(children['sales'][:1] == ['sales.pos'] and len(children['sales.pos']) > 1, 'Sales has category and subcategory levels')
    check('system.settings.fields.native' in children['system.settings.fields'], 'deep Settings leaf is not flattened')
    print(f'PASS {checks} native-guide navigation source/data assertions; NOT .NET/WPF/layout/double-click/focus execution')


if __name__ == '__main__':
    main()
