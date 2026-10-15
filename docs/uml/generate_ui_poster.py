#!/usr/bin/env python3
"""Create diagram 10 from the current diagram 4 without modifying other pages."""

from generate_poster import (
    OUT, PosterPage, Y_SCALE, absolute_box, poster_y, read_cells,
    save_poster, style_values,
)

SLUG = '10-poster-ui-classes'


def build_ui_poster():
    cells = read_cells(OUT / '04-ui-classes.drawio')
    ordered = list(cells.values())
    page = PosterPage(SLUG, '10. Blazor UI classes — poster', 'Blazor UI classes', 2320, 1220)
    class_ids = {cell['parent'] for cell in ordered if cell['id'].endswith('-title')}

    for cell in ordered:
        if 'shape=swimlane;' in cell.get('style', ''):
            x, y, w, h = absolute_box(cell, cells)
            page.package(cell['id'], cell['value'], x, y * Y_SCALE, w, h * Y_SCALE + 40)
            page.cells[-1] = page.cells[-1].replace('startSize=38;', 'startSize=50;').replace('fontSize=17;', 'fontSize=26;')

    for identifier in sorted(class_ids):
        parts = cells[identifier + '-title']['value'].splitlines()
        stereotype = parts.pop(0) if parts[0].startswith('«') else ''
        parts = [part for part in parts if not part.startswith('Components.')]
        name = ''.join(part.strip() for part in parts)
        page.class_name(identifier, name, stereotype, absolute_box(cells[identifier], cells), style_values(cells[identifier])['fillColor'])

    multiplicity_labels = set()
    for index, cell in enumerate(ordered):
        if cell.get('edge') != '1':
            continue
        source, target = cell['source'], cell['target']
        assert source in class_ids and target in class_ids
        style = style_values(cell)
        relation = 'dependency'
        if style.get('endArrow') == 'none':
            relation = 'association'
        elif style.get('endArrow') == 'block':
            relation = 'realization' if style.get('dashed') == '1' else 'inheritance'
        fractions = {(0, .5): 'left', (1, .5): 'right', (.5, 0): 'top', (.5, 1): 'bottom'}
        sides = tuple(fractions[tuple(float(style[prefix + axis]) for axis in ('X', 'Y'))] for prefix in ('exit', 'entry'))
        points = [(x, poster_y(y)) for x, y in cell['points']]
        if points:
            # Keep the original lanes and move their endpoint runs with the
            # shortened class boxes, preserving right-angle connector bends.
            for reverse, node, port in ((False, source, sides[0]), (True, target, sides[1])):
                if reverse:
                    points.reverse()
                axis = 1 if port in ('left', 'right') else 0
                old = points[0][axis]
                new = page.anchor(node, port)[axis]
                for point_index, point in enumerate(points):
                    if point[axis] != old:
                        break
                    replacement = list(point)
                    replacement[axis] = new
                    points[point_index] = tuple(replacement)
                if reverse:
                    points.reverse()
        page.edge(source, target, relation=relation, sides=sides, points=points)
        page.cells[-1] = page.cells[-1].replace('strokeWidth=1.5;', 'strokeWidth=2.4;dashPattern=10 7;endSize=18;startSize=18;')

        if relation == 'association':
            # Read both multiplicities from diagram 4; rendered single children
            # are 1-to-1, while a window can render zero or more message bubbles.
            multiplicities = []
            for following in ordered[index + 1:]:
                if following.get('edge') == '1':
                    break
                if following['id'].startswith('multiplicity-'):
                    multiplicities.append(following['value'])
                    if len(multiplicities) == 2:
                        break
            assert len(multiplicities) == 2, ('missing association multiplicities', cell['id'])
            for node, port, value in ((source, sides[0], multiplicities[0]), (target, sides[1], multiplicities[1])):
                if (node, port, value) in multiplicity_labels:
                    continue
                multiplicity_labels.add((node, port, value))
                x, y = page.anchor(node, port)
                if port in ('left', 'right'):
                    x += 12 if port == 'right' else -64
                    y -= 36
                else:
                    x += 16
                    y += 6 if port == 'bottom' else -36
                page.text(page.ident('multiplicity'), value, x, y, 64, 30, 24, True)

    assert len(page.nodes) == len(class_ids)
    assert len(page.edges) == sum(cell.get('edge') == '1' for cell in ordered)
    return page


def main():
    save_poster(build_ui_poster(), 10)


if __name__ == '__main__':
    main()
