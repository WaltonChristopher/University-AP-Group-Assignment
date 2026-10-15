#!/usr/bin/env python3
"""Create diagram 9 from the current, possibly hand-edited diagram 8.

Keep the class inventory and UML relationships, replacing member compartments
with large class names. Run independently to preserve edits to diagrams 1–8.
"""

from pathlib import Path
import re
import pyexpat

from generate import Page, mxfile, validate_layout, validate_xml

OUT = Path(__file__).resolve().parent
SLUG = '09-poster-class-overview'
NAME_SIZE = 36
Y_SCALE = .72


def read_cells(path):
    cells, stack = [], []

    def start(tag, attributes):
        stack.append(tag)
        if tag == 'mxCell':
            cells.append(dict(attributes, points=[]))
        elif tag == 'mxGeometry' and stack[-2] == 'mxCell':
            cells[-1]['geometry'] = dict(attributes)
        elif tag == 'mxPoint' and 'as' not in attributes:
            cells[-1]['points'].append(tuple(float(attributes[k]) for k in ('x', 'y')))

    parser = pyexpat.ParserCreate()
    parser.StartElementHandler = start
    parser.EndElementHandler = lambda tag: stack.pop()
    parser.Parse(path.read_bytes(), True)
    return {cell['id']: cell for cell in cells}


def style_values(cell):
    return dict(part.split('=', 1) for part in cell.get('style', '').split(';') if '=' in part)


def absolute_box(cell, cells):
    geometry = cell['geometry']
    x, y, w, h = (float(geometry.get(k, 0)) for k in ('x', 'y', 'width', 'height'))
    if cell.get('parent') not in ('0', '1', None):
        px, py, _, _ = absolute_box(cells[cell['parent']], cells)
        x, y = x + px, y + py
    return x, y, w, h


def poster_y(y):
    return y * Y_SCALE + 40


def name_lines(name, width):
    # Break identifiers at word / generic boundaries, retaining their spelling.
    if ' / ' in name:
        groups = name.split(' / ')
        lines = []
        for index, group in enumerate(groups):
            lines.extend(name_lines(group, width))
            if index < len(groups) - 1:
                lines[-1] += ' /'
        return lines
    capacity = int((width - 32) / (NAME_SIZE * .58))
    tokens = re.findall(r'[A-Z]+(?=[A-Z][a-z]|\b)|[A-Z]?[a-z]+|[<>]|[A-Z]+', name)
    assert ''.join(tokens) == name, ('unsupported class name', name)
    lines = ['']
    for token in tokens:
        if lines[-1] and (len(lines[-1] + token) > capacity or token == '<'):
            lines.append('')
        lines[-1] += token
    return lines


class PosterPage(Page):
    def __init__(self, slug=SLUG, title='9. Application and persistence classes — poster',
                 heading='Application and persistence classes', width=4240, height=1640):
        super().__init__(slug, title, '', width, height)
        # Replace the standard report heading and subtitle with a poster heading.
        self.cells = self.cells[:2]
        self.graphics, self.labels = [], {}
        self.text('title', heading, 40, 24, width - 80, 64, 48, True)

    def class_name(self, identifier, name, stereotype, box, fill):
        x, old_y, w, old_h = box
        lines = name_lines(name, w)
        stereotype_h = 32 if stereotype else 0
        height = 24 + stereotype_h + len(lines) * 42
        y = poster_y(old_y + old_h / 2) - height / 2
        self.cell(identifier, '', f'rounded=0;html=0;fillColor={fill};strokeColor=#475569;strokeWidth=2.4;container=1;collapsible=0;', x, y, w, height)
        self.nodes[identifier] = dict(x=x, y=y, w=w, h=height, name=name)
        if stereotype:
            self.text(identifier + '-stereotype', stereotype, 12, 10, w - 24, 30, 24, color='#475569', align='center', parent=identifier)
            self.graphics[-1][1].update(x=x + 12, y=y + 10)
        self.text(identifier + '-title', '\n'.join(lines), 12, 12 + stereotype_h, w - 24, len(lines) * 42, NAME_SIZE, True, '#0f172a', 'center', identifier)
        self.graphics[-1][1].update(x=x + 12, y=y + 12 + stereotype_h)
        self.graphics.append(('class', dict(x=x, y=y, w=w, h=height, head=height, fill=fill, compartments=[])))

    def svg(self):
        svg = super().svg()
        for kind, item in self.graphics:
            if kind == 'package':
                x, y = item['x'], item['y']
                svg = svg.replace(f'<text x="{x+14}" y="{y+25}"', f'<text x="{x+14}" y="{y+34}"')
        svg = svg.replace('height="38" fill="#f8fafc"', 'height="50" fill="#f8fafc"')
        svg = svg.replace('font-size="17"', 'font-size="26"')
        # Scale outlines, dashes and arrowheads along with the poster lettering.
        svg = svg.replace('stroke-width="1.4"', 'stroke-width="2.4"')
        svg = svg.replace('stroke-width="1.5"', 'stroke-width="2.4"')
        svg = svg.replace('stroke-dasharray="7 5"', 'stroke-dasharray="10 7"')
        return svg


def build_poster():
    cells = read_cells(OUT / '08-application-persistence-classes.drawio')
    page = PosterPage()
    class_ids = {c['parent'] for c in cells.values() if c['id'].endswith('-title')}
    for cell in cells.values():
        if 'shape=swimlane;' in cell.get('style', ''):
            x, y, w, h = absolute_box(cell, cells)
            page.package(cell['id'], cell['value'], x, y * Y_SCALE, w, h * Y_SCALE + 40)

    for identifier in sorted(class_ids):
        cell = cells[identifier]
        parts = cells[identifier + '-title']['value'].splitlines()
        stereotype = parts.pop(0) if parts[0].startswith('«') else ''
        parts = [part for part in parts if not part.startswith(('Yodaphone.', 'Microsoft.'))]
        name = ''.join(part.strip() for part in parts)
        box = absolute_box(cell, cells)
        # Make room for long names without squeezing the letters.
        if identifier == 'status':
            box = (box[0], box[1], 420, box[3])
        elif identifier == 'db-factory':
            box = (box[0], box[1], 670, box[3])
        page.class_name(identifier, name, stereotype, box, style_values(cell)['fillColor'])

    for cell in cells.values():
        if cell.get('edge') != '1':
            continue
        source, target = cell['source'], cell['target']
        assert source in class_ids and target in class_ids
        style = style_values(cell)
        if style.get('startArrow') == 'diamond':
            relation = 'composition'
        elif style.get('endArrow') == 'block':
            relation = 'realization' if style.get('dashed') == '1' else 'inheritance'
        elif style.get('endArrow') == 'none':
            relation = 'association'
        else:
            relation = 'dependency'
        # Diagram 8 contains a few manually moved ports; normalize each port to
        # its nearest box side so the simplified connectors remain attached.
        def side(prefix, default):
            if prefix + 'X' not in style:
                return default
            fx, fy = (float(style[prefix + axis]) for axis in ('X', 'Y'))
            return min(((abs(fx), 'left'), (abs(1-fx), 'right'), (abs(fy), 'top'), (abs(1-fy), 'bottom')))[1]

        sides = side('exit', 'right'), side('entry', 'right' if target == 'conversation' else 'left')
        points = [(x, poster_y(y)) for x, y in cell['points']]
        if cell['id'] in ('edge-4', 'edge-5', 'edge-13', 'edge-23', 'edge-25'):
            points = None
        if points:
            # Move each endpoint's first straight run with its resized box.
            # This retains the existing lanes without introducing diagonals.
            for reverse, node, port in ((False, source, sides[0]), (True, target, sides[1])):
                if reverse:
                    points.reverse()
                axis = 1 if port in ('left', 'right') else 0
                old = points[0][axis]
                new = page.anchor(node, port)[axis]
                for index, point in enumerate(points):
                    if point[axis] != old:
                        break
                    replacement = list(point)
                    replacement[axis] = new
                    points[index] = tuple(replacement)
                if reverse:
                    points.reverse()
        if cell['id'] == 'edge-25':
            points = [(page.anchor(source, sides[0])[0], page.anchor(target, sides[1])[1])]
        page.edge(source, target, relation=relation, sides=sides, points=points)
        page.cells[-1] = page.cells[-1].replace('strokeWidth=1.5;', 'strokeWidth=2.4;dashPattern=10 7;endSize=18;startSize=18;')
        if relation in ('association', 'composition'):
            for node, port, value in ((source, sides[0], '1'), (target, sides[1], '0..*')):
                x, y = page.anchor(node, port)
                if port in ('left', 'right'):
                    x += 12 if port == 'right' else -64
                    y -= 36
                    if relation == 'composition' and node == source:
                        y += 40
                else:
                    x += 16
                    y += 6 if port == 'bottom' else -36
                page.text(page.ident('multiplicity'), value, x, y, 64, 30, 24, True)

    # Larger package headings in both editable XML and the SVG preview.
    for index, cell_xml in enumerate(page.cells):
        if 'shape=swimlane;' in cell_xml:
            page.cells[index] = cell_xml.replace('startSize=38;', 'startSize=50;').replace('fontSize=17;', 'fontSize=26;')
    assert len(page.nodes) == len(class_ids)
    assert len(page.edges) == sum(c.get('edge') == '1' for c in cells.values())
    return page


def save_poster(page, number):
    validate_layout(page)
    xml = mxfile([page])
    validate_xml(xml, page.slug, {page.slug: page.labels})
    svg = page.svg()
    validate_xml(svg, page.slug + '.svg')

    # Retain the other pages byte for byte and keep existing tab positions.
    document_path = OUT / 'yodaphone-uml.drawio'
    document = document_path.read_text()
    pattern = r'<diagram\b[^>]*\bid="' + re.escape(page.slug) + r'"[^>]*>.*?</diagram>'
    document, replaced = re.subn(pattern, lambda match: page.xml(), document, flags=re.S)
    assert replaced <= 1, ('duplicate poster page', page.slug)
    if not replaced:
        document = document.replace('</mxfile>', page.xml() + '</mxfile>')
    validate_xml(document, document_path.name)
    page_ids = []
    parser = pyexpat.ParserCreate()
    parser.StartElementHandler = lambda tag, attributes: page_ids.append(attributes['id']) if tag == 'diagram' else None
    parser.Parse(document, True)
    assert page_ids[number - 1] == page.slug, ('incorrect page position', page.slug, page_ids)
    (OUT / (page.slug + '.drawio')).write_text(xml)
    (OUT / (page.slug + '.svg')).write_text(svg)
    document_path.write_text(document)
    print(f'{page.slug}: {len(page.nodes)} class boxes, {len(page.edges)} relationships; XML and layout checks passed; saved as page {number}')


def main():
    save_poster(build_poster(), 9)


if __name__ == '__main__':
    main()
