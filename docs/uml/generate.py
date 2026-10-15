#!/usr/bin/env python3
"""Generate the reviewed UML model as editable draw.io XML and SVG previews.

Uses Python's built-in modules only. This is a manually maintained code model,
not a C# parser; review the model below when application or test classes change.
"""

from pathlib import Path
import math
import re
import pyexpat

OUT = Path(__file__).resolve().parent
COLORS = {
    'domain': '#e7f0ff', 'service': '#e5f5ed', 'agent': '#fff2d5',
    'data': '#f0e9ff', 'ui': '#e4f4f7', 'test': '#ffe9ee',
    'double': '#fff4df', 'external': '#f1f3f5', 'note': '#fffbe8',
}


def esc(value):
    return str(value).replace('&', '&amp;').replace('<', '&lt;').replace('>', '&gt;').replace('"', '&quot;')


def attrs(values):
    # XML parsers normalize literal whitespace in attributes to spaces.
    # Character references preserve the line breaks in draw.io cell labels.
    def attribute_value(value):
        return esc(value).replace('\r', '&#13;').replace('\n', '&#10;').replace('\t', '&#9;')
    return ' '.join(f'{key}="{attribute_value(value)}"' for key, value in values.items())


def wrap(text, width, size=13):
    """Wrap predictable monospace compartment text without losing identifiers."""
    capacity = max(12, int((width - 20) / (size * .61)))
    lines = []
    for raw in text.split('\n'):
        while len(raw) > capacity:
            candidates = [i + 1 for i, char in enumerate(raw[:capacity]) if char in ' ,_']
            split = max(candidates) if candidates else capacity
            if split < capacity // 2:
                split = capacity
            lines.append(raw[:split].rstrip())
            raw = '  ' + raw[split:].lstrip()
        lines.append(raw)
    return lines


class Page:
    def __init__(self, slug, title, subtitle, width, height):
        self.slug, self.title, self.width, self.height = slug, title, width, height
        self.cells = ['<mxCell id="0"/>', '<mxCell id="1" parent="0"/>']
        self.nodes, self.edges, self.graphics, self.labels = {}, [], [], {}
        self.counter = 0
        self.text('title', title, 40, 25, width - 80, 40, 27, bold=True)
        self.text('subtitle', subtitle, 40, 76, width - 80, 50, 15)

    def ident(self, prefix):
        self.counter += 1
        return f'{prefix}-{self.counter}'

    def cell(self, identifier, value, style, x, y, w, h, parent='1'):
        self.labels[identifier] = str(value)
        self.cells.append(f'<mxCell {attrs(dict(id=identifier, value=value, style=style, vertex="1", parent=parent))}>'
                          f'<mxGeometry {attrs(dict(x=x, y=y, width=w, height=h))} as="geometry"/></mxCell>')

    def text(self, identifier, value, x, y, w, h, size=13, bold=False, color='#334155', align='left', parent='1'):
        style = f'text;html=0;whiteSpace=wrap;overflow=hidden;align={align};verticalAlign=middle;fontFamily=Helvetica;fontSize={size};fontColor={color};fontStyle={1 if bold else 0};spacing=0;'
        self.cell(identifier, value, style, x, y, w, h, parent)
        self.graphics.append(('text', dict(text=value, x=x, y=y, w=w, h=h, size=size, bold=bold, color=color, align=align)))

    def package(self, identifier, title, x, y, w, h):
        self.cell(identifier, title, 'shape=swimlane;html=0;startSize=38;horizontal=1;collapsible=0;container=0;rounded=0;fillColor=#f8fafc;swimlaneFillColor=#ffffff;strokeColor=#cbd5e1;fontColor=#334155;fontSize=17;fontStyle=1;align=left;spacingLeft=14;', x, y, w, h)
        self.graphics.append(('package', dict(title=title, x=x, y=y, w=w, h=h)))

    def klass(self, identifier, name, x, y, w=360, attributes=(), operations=(), kind='service', stereotype='', namespace='', note=''):
        title = ('«' + stereotype + '»\n' if stereotype else '') + name
        if namespace:
            title += '\n' + namespace
        title_lines = wrap(title, w, 14)
        ah = wrap('\n'.join(attributes), w, 12) if attributes else []
        oh = wrap('\n'.join(operations), w, 12) if operations else []
        nh = wrap(note, w, 12) if note else []
        head = max(42, len(title_lines) * 18 + 16)
        body = []
        for lines, is_note in [(ah, False), (oh, False), (nh, True)]:
            if lines:
                body.append((lines, max(32, len(lines) * 17 + 14), is_note))
        height = head + sum(item[1] for item in body)
        fill = COLORS[kind]
        self.cell(identifier, '', f'rounded=0;html=0;fillColor={fill};strokeColor=#475569;strokeWidth=1.4;container=1;collapsible=0;', x, y, w, height)
        self.nodes[identifier] = dict(x=x, y=y, w=w, h=height, name=name)
        # Compartments are separate text cells, so all class text remains editable.
        self.text(identifier + '-title', '\n'.join(title_lines), 8, 6, w - 16, head - 12, 14, True, '#0f172a', 'center', identifier)
        # Correct the SVG position of child cells to absolute coordinates.
        self.graphics[-1][1].update(x=x + 8, y=y + 6)
        offset = head
        compartments = []
        for index, (lines, ch, is_note) in enumerate(body):
            lid = f'{identifier}-divider-{index}'
            self.cell(lid, '', 'line;strokeColor=#64748b;strokeWidth=1;', 0, offset, w, 0, identifier)
            self.text(f'{identifier}-body-{index}', '\n'.join(lines), 10, offset + 6, w - 20, ch - 12, 12, False, '#475569' if is_note else '#1e293b', parent=identifier)
            self.graphics[-1][1].update(x=x + 10, y=y + offset + 6, mono=not is_note)
            compartments.append(offset)
            offset += ch
        self.graphics.append(('class', dict(x=x, y=y, w=w, h=height, head=head, fill=fill, compartments=compartments)))
        return identifier

    def shape(self, identifier, text, x, y, w, h, kind='action', fill='#e5f5ed'):
        shapes = {'action': 'rounded=1;arcSize=15;', 'decision': 'rhombus;', 'start': 'ellipse;', 'end': 'ellipse;shape=doubleEllipse;', 'note': 'shape=note;size=16;', 'rect': 'rounded=0;'}
        size = 12 if kind == 'note' else 14
        text = '\n'.join(wrap(text, w * .78 if kind == 'decision' else w, size))
        assert len(text.split('\n')) * (size + 4) <= h - 8, (self.slug, identifier, 'shape text exceeds height')
        style = shapes[kind] + f'html=0;whiteSpace=wrap;fillColor={fill};strokeColor=#475569;strokeWidth=1.5;fontColor=#0f172a;fontSize={size};align=center;verticalAlign=middle;spacing=8;'
        self.cell(identifier, text, style, x, y, w, h)
        self.nodes[identifier] = dict(x=x, y=y, w=w, h=h, name=text)
        self.graphics.append(('shape', dict(text=text, x=x, y=y, w=w, h=h, kind=kind, fill=fill, size=size)))
        return identifier

    def note(self, identifier, value, x, y, w, h):
        return self.shape(identifier, value, x, y, w, h, 'note', COLORS['note'])

    def anchor(self, identifier, side):
        box = self.nodes[identifier]
        fractions = {'left': (0, .5), 'right': (1, .5), 'top': (.5, 0), 'bottom': (.5, 1)}
        fx, fy = fractions[side]
        return (box['x'] + fx * box['w'], box['y'] + fy * box['h'])

    def edge(self, source, target, label='', relation='dependency', sides=('right', 'left'), points=None, label_at=None, multiplicities=None):
        start, end = self.anchor(source, sides[0]), self.anchor(target, sides[1])
        if points is None:
            if start[0] == end[0] or start[1] == end[1]:
                points = []
            elif sides[0] in ('left', 'right'):
                mid = (start[0] + end[0]) / 2
                points = [(mid, start[1]), (mid, end[1])]
            else:
                mid = (start[1] + end[1]) / 2
                points = [(start[0], mid), (end[0], mid)]
        route = [start] + points + [end]
        positions = {'left': (0, .5), 'right': (1, .5), 'top': (.5, 0), 'bottom': (.5, 1)}
        ax, ay = positions[sides[0]]
        bx, by = positions[sides[1]]
        anchors = f'exitX={ax};exitY={ay};exitDx=0;exitDy=0;entryX={bx};entryY={by};entryDx=0;entryDy=0;'
        self.raw_edge(route, label, relation, source, target, anchors, label_at)
        if multiplicities:
            for point, value, side in [(start, multiplicities[0], sides[0]), (end, multiplicities[1], sides[1])]:
                px, py = point
                # Put multiplicities next to, rather than on, the class border.
                px += 13 if side == 'right' else -45 if side == 'left' else 12
                py += 7 if side == 'bottom' else -25
                self.text(self.ident('multiplicity'), value, px, py, 34, 20, 12, bold=True)

    def raw_edge(self, route, label='', relation='dependency', source=None, target=None, anchors='', label_at=None):
        styles = {
            'dependency': 'dashed=1;endArrow=open;endFill=0;',
            'realization': 'dashed=1;endArrow=block;endFill=0;',
            'inheritance': 'endArrow=block;endFill=0;',
            'association': 'endArrow=none;',
            'composition': 'startArrow=diamond;startFill=1;endArrow=none;',
            'flow': 'endArrow=block;endFill=1;',
            'message': 'endArrow=block;endFill=1;',
            'return': 'dashed=1;endArrow=open;endFill=0;',
            'line': 'dashed=1;endArrow=none;',
        }
        identifier = self.ident('edge')
        style = 'edgeStyle=segmentEdgeStyle;rounded=0;html=0;strokeColor=#475569;strokeWidth=1.5;fontSize=13;labelBackgroundColor=#ffffff;' + styles[relation] + anchors
        options = dict(id=identifier, value='', style=style, edge='1', parent='1')
        if source:
            options['source'] = source
        if target:
            options['target'] = target
        geometry = '<mxGeometry relative="1" as="geometry">'
        if not source:
            geometry += f'<mxPoint x="{route[0][0]}" y="{route[0][1]}" as="sourcePoint"/>'
        if not target:
            geometry += f'<mxPoint x="{route[-1][0]}" y="{route[-1][1]}" as="targetPoint"/>'
        if len(route) > 2:
            geometry += '<Array as="points">' + ''.join(f'<mxPoint x="{x}" y="{y}"/>' for x, y in route[1:-1]) + '</Array>'
        self.cells.append(f'<mxCell {attrs(options)}>{geometry}</mxGeometry></mxCell>')
        self.edges.append(dict(route=route, relation=relation, source=source, target=target))
        if label:
            if label_at is None:
                a, b = route[len(route) // 2 - 1], route[len(route) // 2]
                label_at = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2 - 18)
            lx, ly = label_at
            # Edge labels use relative child geometry and move with their edge.
            lines = wrap(label, 320, 12)
            label_width = min(320, max(len(line) for line in lines) * 7.4 + 14)
            label_height = len(lines) * 17 + 4
            base = route[0]
            self.labels[identifier + '-label'] = '\n'.join(lines)
            self.cells.append(f'<mxCell {attrs(dict(id=identifier+"-label", value="\n".join(lines), vertex="1", connectable="0", parent=identifier, style="text;html=0;whiteSpace=wrap;align=center;verticalAlign=middle;fontSize=12;fontColor=#334155;fillColor=#ffffff;strokeColor=none;"))}>'
                              f'<mxGeometry x="-1" y="0" width="{label_width}" height="{label_height}" relative="1" as="geometry"><mxPoint x="{lx-base[0]-label_width/2}" y="{ly-base[1]-label_height/2}" as="offset"/></mxGeometry></mxCell>')
            self.graphics.append(('edge-label', dict(text='\n'.join(lines), x=lx-label_width/2, y=ly-label_height/2, w=label_width, h=label_height, size=12, color='#334155', align='center')))

    def legend(self, y):
        self.note('legend', '+ public   − private   # protected   ~ internal   / derived   «…» stereotype\nCT = CancellationToken; method default values and most constructors are omitted. Selected members are shown.\nDashed open arrow = dependency (including «tests»); dashed hollow triangle = interface realization; solid hollow triangle = inheritance.\nSolid line = association; filled diamond = composition. Multiplicities are shown beside association ends.', 40, y, self.width - 80, 112)

    def xml(self):
        return f'<diagram id="{self.slug}" name="{esc(self.title)}"><mxGraphModel dx="{self.width}" dy="{self.height}" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="{self.width}" pageHeight="{self.height}" math="0" shadow="0"><root>' + ''.join(self.cells) + '</root></mxGraphModel></diagram>'

    def svg(self):
        fragments = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{self.width}" height="{self.height}" viewBox="0 0 {self.width} {self.height}" role="img" aria-labelledby="title desc"><title id="title">{esc(self.title)}</title><desc id="desc">UML preview. Open the companion draw.io file to edit classes and connections.</desc><rect width="100%" height="100%" fill="white"/>']
        # Packages, class bodies and shapes precede connectors; text is last.
        for kind, item in self.graphics:
            x, y, w, h = (item.get(key) for key in ('x', 'y', 'w', 'h'))
            if kind == 'package':
                fragments += [f'<rect x="{x}" y="{y}" width="{w}" height="{h}" fill="white" stroke="#cbd5e1"/>', f'<rect x="{x}" y="{y}" width="{w}" height="38" fill="#f8fafc" stroke="#cbd5e1"/>', f'<text x="{x+14}" y="{y+25}" font-family="Arial,sans-serif" font-size="17" font-weight="bold" fill="#334155">{esc(item["title"])}</text>']
            elif kind == 'class':
                fragments.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" fill="{item["fill"]}" stroke="#475569" stroke-width="1.4"/>')
                for offset in item['compartments']:
                    fragments.append(f'<path d="M{x},{y+offset} H{x+w}" fill="none" stroke="#64748b"/>')
            elif kind == 'shape':
                k, fill = item['kind'], item['fill']
                if k == 'decision':
                    fragments.append(f'<path d="M{x+w/2},{y} L{x+w},{y+h/2} L{x+w/2},{y+h} L{x},{y+h/2} Z" fill="{fill}" stroke="#475569" stroke-width="1.5"/>')
                elif k in ('start', 'end'):
                    fragments.append(f'<ellipse cx="{x+w/2}" cy="{y+h/2}" rx="{w/2}" ry="{h/2}" fill="{fill}" stroke="#475569" stroke-width="1.5"/>')
                    if k == 'end':
                        fragments.append(f'<ellipse cx="{x+w/2}" cy="{y+h/2}" rx="{w/2-5}" ry="{h/2-5}" fill="#475569"/>')
                elif k == 'note':
                    fragments.append(f'<path d="M{x},{y} H{x+w-16} L{x+w},{y+16} V{y+h} H{x} Z M{x+w-16},{y} V{y+16} H{x+w}" fill="{fill}" stroke="#b9aa65"/>')
                else:
                    fragments.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{12 if k=="action" else 0}" fill="{fill}" stroke="#475569" stroke-width="1.5"/>')
        for item in self.edges:
            route, relation = item['route'], item['relation']
            dash = ' stroke-dasharray="7 5"' if relation in ('dependency', 'realization', 'return', 'line') else ''
            fragments.append('<polyline points="' + ' '.join(f'{x},{y}' for x, y in route) + f'" fill="none" stroke="#475569" stroke-width="1.5"{dash}/>' )
            def marker(point, previous, marker_kind):
                px, py = point
                dx, dy = px-previous[0], py-previous[1]
                distance = math.hypot(dx, dy)
                if distance == 0:
                    return
                ux, uy = dx/distance, dy/distance
                def at(back, side):
                    return (px-back*ux-side*uy, py-back*uy+side*ux)
                if marker_kind == 'diamond':
                    pts = [at(0,0),at(-10,5),at(-20,0),at(-10,-5)]
                else:
                    pts = [at(0,0),at(13,6),at(13,-6)]
                coords = ' '.join(f'{a},{b}' for a,b in pts)
                if marker_kind == 'open':
                    coords = ' '.join(f'{a},{b}' for a,b in [pts[1],pts[0],pts[2]])
                    fragments.append(f'<polyline points="{coords}" fill="none" stroke="#475569" stroke-width="1.5"/>')
                else:
                    fill = 'white' if marker_kind == 'hollow' else '#475569'
                    fragments.append(f'<polygon points="{coords}" fill="{fill}" stroke="#475569" stroke-width="1.5"/>')
            if relation in ('realization','inheritance'):
                marker(route[-1], route[-2], 'hollow')
            elif relation in ('dependency','return'):
                marker(route[-1], route[-2], 'open')
            elif relation in ('flow','message'):
                marker(route[-1], route[-2], 'filled')
            elif relation == 'composition':
                marker(route[0],route[1],'diamond')
        for kind, item in self.graphics:
            if kind not in ('text','shape','edge-label'):
                continue
            if kind == 'shape':
                if not item['text']:
                    continue
                item = dict(item, color='#0f172a', align='center')
            x,y,w,h,size = (item[key] for key in ('x','y','w','h','size'))
            lines = item['text'].split('\n')
            if kind == 'edge-label':
                fragments.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" fill="white"/>')
            mono = item.get('mono',False)
            line_height = 17 if mono else size+4
            first_y = y + (h - len(lines)*line_height)/2 + size
            align = item.get('align','left')
            tx = x+w/2 if align=='center' else x
            anchor = 'middle' if align=='center' else 'start'
            family = 'DejaVu Sans Mono,monospace' if mono else 'Arial,sans-serif'
            weight = 'bold' if item.get('bold') else 'normal'
            fragments.append(f'<text xml:space="preserve" x="{tx}" y="{first_y}" text-anchor="{anchor}" font-family="{family}" font-size="{size}" font-weight="{weight}" fill="{item["color"]}">')
            fragments += [f'<tspan x="{tx}" dy="{0 if i==0 else line_height}">{esc(line)}</tspan>' for i,line in enumerate(lines)]
            fragments.append('</text>')
        return ''.join(fragments) + '</svg>'


def application(combined=False):
    if combined:
        p = Page('08-application-persistence-classes', '8. Combined application and persistence class diagram', 'Diagrams 1 and 3 integrated into one view: shared classes appear once, with domain-to-entity mapping and database relationships.', 4240, 2440)
    else:
        p = Page('01-application-classes', '1. Application class diagram', 'Yodaphone.Web — production types, selected members and actual dependency injection relationships.', 2920, 2020)
    p.package('domain-package','Yodaphone.Web.Domain',40,150,920,970)
    p.package('service-package','Yodaphone.Web.Services' if combined else 'Yodaphone.Web.Services / Data',1000,150,750,1990 if combined else 1690)
    p.package('agent-package','Yodaphone.Web.Chat / Agents / Agents.Copilot',1790,150,1090,1690)
    p.klass('conversation','Conversation',75,220,420, ['+ Id: Guid {private set}','+ StartedAt: DateTimeOffset {private set}','+ LastActivityAt: DateTimeOffset {private set}','+ Status: ConversationStatus {private set}','+ Messages: IReadOnlyCollection<ChatMessage>'], ['+ AddMessage(role: MessageRole, content: string): ChatMessage','+ Close(): void','+ Restore(id, startedAt, lastActivityAt, status, restoredMessages): Conversation {static}'], 'domain')
    p.klass('domain-message','ChatMessage',550,220,375,['+ Id: Guid {private set}','+ ConversationId: Guid {private set}','+ Role: MessageRole {private set}','+ Content: string {private set}','+ SentAt: DateTimeOffset {private set}','+ /IsFromAgent: bool'],['+ Restore(id, conversationId, role, content, sentAt): ChatMessage {static}'],'domain')
    p.edge('conversation','domain-message','messages', 'composition',multiplicities=('1','0..*'),label_at=(522,205))
    p.klass('status','ConversationStatus',75,630,235,['Active','Closed'],kind='domain',stereotype='enumeration')
    p.klass('role','MessageRole',660,630,265,['Customer','Assistant','System'],kind='domain',stereotype='enumeration')
    p.edge('conversation','status',sides=('bottom','top'))
    p.edge('domain-message','role',sides=('bottom','top'))
    p.klass('sanitizer','ChatContentSanitizer',180,875,670,operations=['+ EnsureSafeUserContent(content: string): void','+ EnsureSafeAgentResponse(content: string): void'],kind='domain',stereotype='static')
    p.edge('conversation','sanitizer','screens customer content',sides=('bottom','left'),points=[(285,590),(520,590),(520,815),(130,815),(130,924)],label_at=(345,812))
    service_ops = ['+ StartConversationAsync(CT): Task<Conversation>','+ GetConversationAsync(id: string, CT): Task<Conversation>','+ GetConversationsAsync(CT): Task<IReadOnlyList<Conversation>>','+ GetActiveConversationAsync(CT): Task<Conversation?>','+ AddMessageAsync(id: string, role: MessageRole, content: string, CT): Task<ChatMessage>','+ SendMessageAsync(id: string, content: string, CT): Task<ChatMessage>','+ CloseConversationAsync(id: string, CT): Task']
    p.klass('chat-interface','IChatService',1040,220,670,operations=service_ops,stereotype='interface')
    p.klass('chat-service','ChatService',1040,675,670,['− chatAgent: IChatAgent','− conversationRepository: IConversationRepository','− currentUser: ICurrentUser','− operationLock: SemaphoreSlim'],note='Implements all IChatService operations; serializes changes within this scoped service instance.')
    p.edge('chat-service','chat-interface',relation='realization',sides=('top','bottom'))
    p.edge('chat-service','conversation','loads / changes / returns',sides=('left','right'),points=[(980,765),(980,575),(510,575),(510,380)],label_at=(745,574))
    p.klass('current-user','ICurrentUser',1040,1010,255,['+ UserId: int {get}'],stereotype='interface')
    p.klass('config-user','ConfigurationCurrentUser',1360,1010,350,['+ UserId: int {get}'],note='Reads Chat:DevelopmentUserId from IConfiguration.')
    p.edge('config-user','current-user',relation='realization',sides=('left','right'))
    p.edge('chat-service','current-user','owner identity',sides=('bottom','top'),points=[(1375,960),(1167,960)],label_at=(1250,958))
    repo_ops = ['+ AddAsync(conversation, userId: int, CT): Task','+ GetAsync(id: Guid, userId: int, CT): Task<Conversation?>','+ GetAllAsync(userId: int, CT): Task<IReadOnlyList<Conversation>>','+ GetLatestActiveAsync(userId: int, CT): Task<Conversation?>','+ SaveAsync(conversation, userId: int, CT): Task']
    p.klass('repo-interface','IConversationRepository',1040,1260,670,operations=repo_ops,stereotype='interface')
    mapping_ops = ['− ToEntity(message: Domain.ChatMessage, userId: int): Data.Entities.Message {static}','− ToDomain(entity: Data.Entities.Conversation): Domain.Conversation {static}','− CopyConversation(source: Domain.Conversation, destination: Data.Entities.Conversation): void {static}'] if combined else []
    p.klass('repo','SqliteConversationRepository',1040,1520,670,['− dbContextFactory: IDbContextFactory<ApplicationDbContext>'],operations=mapping_ops,note='Implements repository operations; maps domain objects to Data.Entities and back.')
    p.edge('repo','repo-interface',relation='realization',sides=('top','bottom'))
    p.edge('chat-service','repo-interface','owned persistence',sides=('right','right'),points=[(1730,765),(1730,1350)],label_at=(1720,1170))
    p.klass('db-factory','IDbContextFactory<ApplicationDbContext>',1040,1890 if combined else 1730,350,operations=['+ CreateDbContextAsync(CT): Task<ApplicationDbContext>'],kind='external',stereotype='external interface')
    p.edge('repo','db-factory',sides=('bottom','top'),points=[(1375,1835 if combined else 1685),(1215,1835 if combined else 1685)])
    if not combined:
        p.klass('db-context','ApplicationDbContext',1470,1730,240,kind='data',note='EF Core DbContext; detailed on diagram 3.')
        p.edge('db-factory','db-context','creates')
    p.klass('agent-interface','IChatAgent',1960,220,690,operations=['+ GetReplyAsync(history: IReadOnlyList<ChatMessage>, CT): Task<AgentChatResponse>'],kind='agent',stereotype='interface')
    p.klass('base-agent','ChatAgentBase',1960,445,690,operations=['+ GetReplyAsync(history, CT): Task<AgentChatResponse>','# GetReplyCoreAsync(history, CT): Task<AgentChatResponse> {abstract}'],kind='agent',stereotype='abstract',note='Validates history; replaces blocked responses with a fixed explanation.')
    p.edge('base-agent','agent-interface',relation='realization',sides=('top','bottom'))
    p.edge('chat-service','agent-interface','requests replies',sides=('right','left'),points=[(1765,765),(1765,294)],label_at=(1875,292))
    p.klass('offline','OfflineChatAgent',1830,820,280,operations=['# GetReplyCoreAsync(history, CT): Task<AgentChatResponse>'],kind='agent')
    p.klass('copilot','GitHubCopilotAgent',2150,820,350,['− clientFactory: ICopilotClientFactory'],['# GetReplyCoreAsync(history, CT): Task<AgentChatResponse>','~ BuildTranscript(history): string {static}'],kind='agent')
    p.klass('fallback','CopilotFallbackChatAgent',2540,820,300,['− copilotAgent: GitHubCopilotAgent','− offlineAgent: OfflineChatAgent','− responseTimeout: TimeSpan','− offlineMode: bool'],['# GetReplyCoreAsync(history, CT): Task<AgentChatResponse>'],kind='agent')
    for cls in ['offline','copilot','fallback']:
        cx=p.anchor(cls,'top')[0]
        p.edge(cls,'base-agent',relation='inheritance',sides=('top','bottom'),points=[(cx,740),(2305,740)])
    p.edge('fallback','copilot','delegates',sides=('left','right'),label_at=(2519,789))
    p.edge('fallback','offline','fallback',sides=('bottom','bottom'),points=[(2690,1160),(1970,1160)],label_at=(2450,1160))
    p.klass('client-factory','ICopilotClientFactory',1840,1290,340,operations=['+ Create(options: CopilotClientOptions): ICopilotClient'],kind='agent',stereotype='interface')
    p.klass('client','ICopilotClient',2220,1290,310,operations=['+ StartAsync(CT): Task','+ CreateSessionAsync(config: SessionConfig, CT): Task<ICopilotSession>'],kind='agent',stereotype='interface',note='Extends IAsyncDisposable.')
    p.klass('session','ICopilotSession',2570,1290,270,operations=['+ SendAndWaitAsync(options: MessageOptions, CT): Task<AssistantMessageEvent?>'],kind='agent',stereotype='interface',note='Extends IAsyncDisposable.')
    p.edge('copilot','client-factory','uses per request',sides=('bottom','top'),points=[(2325,1110),(2010,1110)],label_at=(2250,1108))
    p.edge('client-factory','client','creates',label_at=(2199,1250))
    p.edge('client','session','creates',label_at=(2550,1250))
    p.klass('factory','CopilotClientFactory',1840,1560,340,operations=['+ Create(options): ICopilotClient'],kind='agent',note='Private ClientAdapter / SessionAdapter wrap SDK objects.')
    p.edge('factory','client-factory',relation='realization',sides=('top','bottom'))
    p.klass('options','CopilotOptions',2230,1600,300,['+ GitHubToken: string? {init}','+ ResponseTimeoutSeconds: int {init} = 30'],kind='agent')
    p.edge('factory','options','IOptions<T>',label_at=(2204,1698))
    p.edge('fallback','options','timeout configuration',sides=('right','right'),points=[(2860,970),(2860,1750),(2550,1750),(2550,1675)],label_at=(2690,1750))
    p.klass('response','AgentChatResponse',2570,1600,270,['+ Content: string {required}'],kind='agent')
    p.edge('agent-interface','response','returns',sides=('right','right'),points=[(2850,290),(2850,1520),(2850,1646)],label_at=(2780,1520))
    p.klass('validator','ChatAgentRequestValidator',60,1210,420,operations=['+ Validate(history, CT): void'],kind='agent',stereotype='internal static')
    p.klass('agent-exception','ChatAgentException',540,1210,390,operations=['+ ChatAgentException(innerException)'],kind='agent',note='Extends System.Exception; safe public error text.')
    p.edge('base-agent','sanitizer','screens replies',sides=('left','right'),points=[(1775,553),(1775,1180),(970,1180),(970,924)],label_at=(1300,1180))
    p.edge('base-agent','validator','validates history',sides=('left','bottom'),points=[(1780,553),(1780,1235),(980,1235),(980,1390),(270,1390)],label_at=(710,1390))
    p.edge('copilot','agent-exception','wraps SDK failure',sides=('left','bottom'),points=[(2130,p.anchor('copilot','left')[1]),(2130,1200),(1778,1200),(1778,1500),(735,1500)],label_at=(795,1500))
    p.note('ui-ref','The Blazor Chat page injects IChatService.\nSee diagram 4 for component classes.\nProgram.Main registers scoped services and chooses OfflineChatAgent or CopilotFallbackChatAgent from Chat:Agent.',60,1600,870,160)
    if combined:
        add_application_persistence(p)
        p.legend(2310)
    else:
        p.legend(1870)
    return p


def add_application_persistence(p):
    """Expand the application model with persistence, reusing its shared types."""
    p.package('persistence-package','Yodaphone.Web.Data / Data.Entities',2960,150,1240,1990)
    p.klass('data-user','User',3010,230,1020,['+ UserId: int {PK}','+ Support: bool','+ Conversations: ICollection<Conversation>','+ Messages: ICollection<Message>'],kind='data',stereotype='entity',namespace='Yodaphone.Web.Data.Entities')
    p.klass('data-conversation','Conversation',3010,610,1020,['+ ConversationId: int {PK}','+ Id: Guid {unique}','+ UserId: int {FK}','+ Start: DateTime','+ DateTime: DateTime','+ LastUpdated: DateTime','+ Closed: bool','+ User: User','+ Messages: ICollection<Message>'],kind='data',stereotype='entity',namespace='Yodaphone.Web.Data.Entities')
    p.klass('data-message','Message',3010,1080,1020,['+ MessageId: int {PK}','+ Id: Guid {unique}','+ MessageContent: string','+ DateTime: DateTime','+ Status: string {MessageRole name}','+ SenderId: int {FK}','+ ConversationId: int {FK}','+ Sender: User','+ Conversation: Conversation'],kind='data',stereotype='entity',namespace='Yodaphone.Web.Data.Entities')
    p.edge('data-user','data-conversation','owner / conversations','association',sides=('bottom','top'),multiplicities=('1','0..*'),label_at=(3650,510))
    p.edge('data-conversation','data-message','conversation / messages','association',sides=('bottom','top'),multiplicities=('1','0..*'),label_at=(3660,970))
    p.edge('data-user','data-message','sender / messages','association',sides=('right','right'),points=[(4070,p.anchor('data-user','right')[1]),(4070,p.anchor('data-message','right')[1])],multiplicities=('1','0..*'),label_at=(4070,1000))
    p.klass('db-context','ApplicationDbContext',3010,1510,1020,['+ Users: DbSet<User>','+ Conversations: DbSet<Data.Entities.Conversation>','+ Messages: DbSet<Data.Entities.Message>'],['# OnModelCreating(modelBuilder): void'],kind='data',namespace='Yodaphone.Web.Data')
    p.klass('ef-dbcontext','DbContext',3355,1870,330,kind='external',stereotype='external',namespace='Microsoft.EntityFrameworkCore')
    p.edge('db-context','ef-dbcontext',relation='inheritance',sides=('bottom','top'))
    for target, lane, label, label_y in [('data-user',4110,'Users: DbSet<User>',450),('data-conversation',4130,'Conversations: DbSet<Conversation>',915),('data-message',4150,'Messages: DbSet<Message>',1410)]:
        p.edge('db-context',target,label,sides=('right','right'),points=[(lane,p.anchor('db-context','right')[1]),(lane,p.anchor(target,'right')[1])],label_at=(3900,label_y))
    # Route cross-layer dependencies below the agent column, keeping its classes readable.
    p.edge('repo','conversation','restores domain objects',sides=('left','left'),points=[(990,p.anchor('repo','left')[1]),(990,2010),(50,2010),(50,p.anchor('conversation','left')[1])],label_at=(600,2010))
    p.edge('repo','data-conversation','maps GUID, timestamps and closed state',sides=('right','left'),points=[(1755,p.anchor('repo','right')[1]),(1755,2050),(2900,2050),(2900,p.anchor('data-conversation','left')[1])],label_at=(2350,2050))
    p.edge('repo','data-message','maps GUID, role, content and UTC timestamp',sides=('right','left'),points=[(1760,p.anchor('repo','right')[1]),(1760,2085),(2920,2085),(2920,p.anchor('data-message','left')[1])],label_at=(2350,2085))
    p.edge('db-factory','db-context','creates one context per repository operation',sides=('right','left'),points=[(1765,p.anchor('db-factory','right')[1]),(1765,2130),(2940,2130),(2940,p.anchor('db-context','left')[1])],label_at=(2350,2130))
    p.note('persistence-mapping','Domain.Conversation.Id and Domain.ChatMessage.Id map to entity Id GUIDs; integer ConversationId / MessageId are database keys.\nStartedAt → Start and DateTime; LastActivityAt → LastUpdated; ConversationStatus.Closed ↔ Closed.\nChatMessage.Role → Message.Status; Content → MessageContent; SentAt → DateTime in UTC. SenderId references the owner for all message roles.\nEach repository operation creates / disposes its own EF Core context. SaveAsync commits atomically and preserves final closure.',40,2180,4160,100)


def testing():
    p=Page('02-classes-with-tests','2. Class diagram including testing','All 19 existing xUnit fixture classes; application types, shared infrastructure and key test doubles. Fixture bodies summarize exercised behavior.',2860,3240)
    p.package('domain-tests','Yodaphone.Domain.Tests',40,150,1350,740)
    p.package('service-tests','Yodaphone.Web.Tests.Services',1430,150,1390,1070)
    p.package('agent-tests','Yodaphone.Web.Tests.Chat.Agents',40,930,1350,1310)
    p.package('ui-tests','Yodaphone.Web.Tests.Components',1430,1260,1390,980)
    p.package('host-tests','Yodaphone.Web.Tests.Hosting / Integration / EndToEnd',40,2280,2780,760)
    def fixture(ident,name,x,y,focus,w=460):
        return p.klass(ident,name,x,y,w,operations=['+ [Fact] / [Theory] test methods'],kind='test',stereotype='test fixture',note=focus)
    def sut(ident,name,x,y,w=420,kind='domain',note=''):
        return p.klass(ident,name,x,y,w,kind=kind,stereotype='production',note=note)
    fixture('conversation-tests','ConversationTests',80,225,'Append, activity timestamp, closure and role spoof rejection.')
    fixture('restoration-tests','MessageRestorationTests',80,445,'Preserve IDs and roles; sort restored history; validate content and conversation ownership.')
    fixture('sanitizer-tests','ChatContentSanitizerTests',80,665,'Role spoofing, injection patterns and forbidden provider response content.')
    sut('conversation','Conversation',810,240,note='Domain conversation behavior.')
    sut('message','ChatMessage',810,475,note='Domain message restoration.')
    sut('sanitizer','ChatContentSanitizer',810,700)
    p.edge('conversation-tests','conversation','«tests»')
    p.edge('restoration-tests','message','«tests»')
    p.edge('restoration-tests','conversation','«tests»',points=[(675,527),(675,294)],label_at=(680,390))
    p.edge('sanitizer-tests','sanitizer','«tests»')
    fixture('persistence-tests','ChatServicePersistenceTests',1470,225,'Separate histories; commit-before-reply; failure, cancellation and concurrent closure.')
    fixture('validation-tests','ChatServiceValidationTests',1470,420,'Dependency / input checks, malformed replies, retained messages and lock release.')
    fixture('repository-tests','SqliteConversationRepositoryTests',1470,615,'Ownership, UTC and GUID mapping; append idempotency, transactions and stale closure.')
    fixture('migration-tests','ConversationMigrationTests',1470,810,'Rollback / reapply; legacy identifiers; sender and owner navigation relationships.')
    fixture('identity-tests','ConfigurationCurrentUserTests',1470,1005,'Default / configured user ID; reject invalid IDs and missing configuration.')
    sut('chat-service','ChatService',2210,285,480,'service',note='IChatService implementation.')
    sut('repository','SqliteConversationRepository',2210,655,480,'service',note='IConversationRepository implementation.')
    sut('db-context','ApplicationDbContext',2210,850,480,'data',note='Real EF Core schema and migrations.')
    sut('identity','ConfigurationCurrentUser',2210,1040,480,'service')
    p.edge('persistence-tests','chat-service','«tests»',points=[(2050,296),(2050,339)])
    p.edge('validation-tests','chat-service','«tests»',points=[(2080,491),(2080,339)])
    p.edge('repository-tests','repository','«tests»')
    p.edge('migration-tests','db-context','«tests»')
    p.edge('identity-tests','identity','«tests»')
    agent_fixtures=[
        ('base-tests','ChatAgentBaseTests','History and cancellation checks; core delegation; empty / blocked replies.'),
        ('contract-tests','ChatAgentContractTests','Concrete agent classes inherit from ChatAgentBase.'),
        ('offline-tests','OfflineChatAgentTests','Offline support reply and shared request contract.'),
        ('copilot-tests','GitHubCopilotAgentTests','Transcript, SDK call order, configuration, cleanup, failures and cancellation.'),
        ('fallback-tests','CopilotFallbackChatAgentTests','Timeout / failed reply switches offline; blocked reply and caller cancellation keep Copilot.'),
        ('factory-tests','CopilotClientFactoryTests','Server credential / local-login options and lazy runtime creation.'),
    ]
    agent_types=[('agent-base','ChatAgentBase'),('contract-base','ChatAgentBase'),('offline','OfflineChatAgent'),('copilot','GitHubCopilotAgent'),('fallback','CopilotFallbackChatAgent'),('factory','CopilotClientFactory')]
    for i,((ident,name,focus),(sid,sname)) in enumerate(zip(agent_fixtures,agent_types)):
        y=1005+i*175
        fixture(ident,name,80,y,focus)
        sut(sid,sname,810,y+18,480,'agent')
        p.edge(ident,sid,'«tests»')
    p.klass('fake-copilot','FakeCopilotClientFactory',80,2070,550,['+ Calls: List<string>','+ FailureStage / CancellationStage: string?'],['+ Create(options): ICopilotClient'],kind='double',stereotype='test double',note='Shared by Copilot, fallback and hosting tests; nested FakeClient / FakeSession implement SDK seams.')
    p.klass('factory-interface','ICopilotClientFactory',810,2100,480,kind='agent',stereotype='interface')
    p.edge('fake-copilot','factory-interface',relation='realization')
    for ident in ['copilot-tests','fallback-tests']:
        sy=p.anchor(ident,'left')[1]
        p.edge(ident,'fake-copilot','uses',sides=('left','left'),points=[(60,sy),(60,2174)],label_at=(66,2000 if ident=='copilot-tests' else 2025))
    fixture('render-tests','ChatRenderingTests',1470,1335,'HtmlRenderer verifies message roles, pending state, hidden system messages, sidebar and restored page.')
    fixture('interaction-tests','ChatInteractionTests',1470,1560,'bUnit verifies input, callbacks, scrolling, page state, error handling and cancellation.')
    for ident,name,x,y in [('page','Components.Pages.Chat',2190,1335),('window','Components.Chat.ChatWindow',2190,1460),('input','Components.Chat.ChatInput',2190,1585),('sidebar','Components.Chat.Sidebar',2190,1710),('bubble','Components.Chat.ChatMessage',2190,1835)]:
        sut(ident,name,x,y,500,'ui')
    for ident,target in [('render-tests','page'),('render-tests','window'),('render-tests','bubble'),('render-tests','sidebar'),('interaction-tests','page'),('interaction-tests','window'),('interaction-tests','input'),('interaction-tests','sidebar')]:
        sy=p.anchor(ident,'right')[1]; ty=p.anchor(target,'left')[1]
        bus=2045 if ident=='render-tests' else 2080
        p.edge(ident,target,'«tests»' if target=='page' else '',points=[(bus,sy),(bus,ty)],label_at=(bus,1308 if ident=='render-tests' else 1690))
    p.klass('bunit','BunitContext',1470,1805,460,kind='external',stereotype='external')
    p.edge('interaction-tests','bunit',relation='inheritance',sides=('bottom','top'))
    p.klass('test-chat-service','ChatInteractionTests.TestChatService',1470,1950,550,kind='double',stereotype='nested test double',note='Implements IChatService; records calls and controls deferred replies / failures.')
    p.klass('chat-interface','IChatService',2220,2000,450,kind='service',stereotype='interface')
    p.edge('test-chat-service','chat-interface',relation='realization')
    p.edge('interaction-tests','test-chat-service','uses',sides=('left','left'),points=[(1450,1658),(1450,2020)],label_at=(1460,1900))
    p.note('render-helpers','ChatRenderingTests builds a real ChatService with OfflineChatAgent, nested TestConversationRepository : IConversationRepository, TestCurrentUser : ICurrentUser and NoOpJsRuntime : IJSRuntime.',2090,2115,660,95)
    fixture('hosting-tests','ApplicationHostingTests',80,2355,'Host startup, routes, agent registration, fallback, sanitisation and options validation.',530)
    fixture('integration-tests','ChatApplicationIntegrationTests',80,2590,'HTTP chat page applies migrations and restores the same saved welcome conversation.',530)
    fixture('journey-tests','ChatJourneyTests',80,2825,'Real browser sends a message and retains it after reload.',530)
    p.klass('host-factory','ApplicationHostingTests.TestApplicationFactory',870,2355,700,operations=['# ConfigureWebHost(builder): void','+ DisposeAsync(): ValueTask'],kind='double',stereotype='nested test host',note='WebApplicationFactory<App>; optional FakeCopilotClientFactory replaces the provider factory.')
    p.klass('integration-factory','ChatApplicationFactory',870,2590,700,operations=['# ConfigureWebHost(builder): void','# Dispose(disposing: bool): void'],kind='double',stereotype='test host',note='WebApplicationFactory<Program>; isolated SQLite file and offline agent.')
    p.klass('running-app','ChatJourneyTests.RunningChatApplication',870,2825,700,['+ BaseAddress: string'],['+ StartAsync(): Task<RunningChatApplication> {static}','+ DisposeAsync(): ValueTask'],kind='double',stereotype='nested test host',note='Starts dotnet / Kestrel as a process, owns temporary SQLite storage; implements IAsyncDisposable.')
    for a,b in [('hosting-tests','host-factory'),('integration-tests','integration-factory'),('journey-tests','running-app')]:
        p.edge(a,b,'uses')
    p.klass('web-factory','WebApplicationFactory<TEntryPoint>',1840,2400,850,kind='external',stereotype='external',note='Hosting factory uses App; integration factory uses Program.')
    p.edge('host-factory','web-factory',relation='inheritance')
    p.edge('integration-factory','web-factory',relation='inheritance',points=[(1750,2687),(1750,2454)])
    p.klass('program','Program / Components.App',1840,2620,850,kind='ui',stereotype='production entry types',note='Real startup, component rendering, SQLite migrations and service registrations are exercised.')
    p.edge('web-factory','program','boots application',sides=('bottom','top'))
    p.klass('playwright','Playwright / Chromium',1840,2835,850,kind='external',stereotype='external',note='Browser automation used by ChatJourneyTests.')
    p.edge('journey-tests','playwright','drives browser',sides=('bottom','bottom'),points=[(345,3015),(2265,3015)],label_at=(1730,3015))
    p.note('test-infra-ref','Persistence, validation, repository and migration fixtures use SqliteTestDatabase (a real isolated SQLite database and IDbContextFactory<ApplicationDbContext>). See diagram 7 for all nested helpers and interface realization relationships.\nPink = test fixture; amber = test helper; other colors = production types; grey = external framework. «tests» is a dependency, never inheritance.',40,3080,2780,112)
    return p


def persistence():
    p=Page('03-persistence-classes','3. Persistence class diagram','Separate domain GUIDs and relational integer keys; navigation associations follow ApplicationDbContext.OnModelCreating.',1800,1690)
    p.package('domain-pkg','Yodaphone.Web.Domain',40,150,770,730)
    p.package('entity-pkg','Yodaphone.Web.Data.Entities',860,150,900,960)
    p.klass('domain-conv','Conversation',90,230,630,['+ Id: Guid','+ StartedAt: DateTimeOffset','+ LastActivityAt: DateTimeOffset','+ Status: ConversationStatus','+ Messages: IReadOnlyCollection<ChatMessage>'],kind='domain')
    p.klass('domain-msg','ChatMessage',90,600,630,['+ Id: Guid','+ ConversationId: Guid','+ Role: MessageRole','+ Content: string','+ SentAt: DateTimeOffset'],kind='domain')
    p.edge('domain-conv','domain-msg','messages','composition',sides=('bottom','top'),multiplicities=('1','0..*'),label_at=(470,520))
    p.klass('user','User',950,230,680,['+ UserId: int {PK}','+ Support: bool','+ Conversations: ICollection<Conversation>','+ Messages: ICollection<Message>'],kind='data',stereotype='entity')
    p.klass('conv','Conversation',950,515,680,['+ ConversationId: int {PK}','+ Id: Guid {unique}','+ UserId: int {FK}','+ Start: DateTime','+ DateTime: DateTime','+ LastUpdated: DateTime','+ Closed: bool','+ User: User','+ Messages: ICollection<Message>'],kind='data',stereotype='entity')
    p.klass('msg','Message',950,845,680,['+ MessageId: int {PK}','+ Id: Guid {unique}','+ MessageContent: string','+ DateTime: DateTime','+ Status: string {MessageRole name}','+ SenderId: int {FK}','+ ConversationId: int {FK}','+ Sender: User','+ Conversation: Conversation'],kind='data',stereotype='entity')
    p.edge('user','conv','owner / conversations','association',sides=('bottom','top'),multiplicities=('1','0..*'),label_at=(1370,470))
    p.edge('conv','msg','conversation / messages','association',sides=('bottom','top'),multiplicities=('1','0..*'),label_at=(1430,815))
    p.edge('user','msg','sender / messages','association',sides=('right','right'),points=[(1695,331),(1695,960)],multiplicities=('1','0..*'),label_at=(1680,745))
    p.klass('repository','SqliteConversationRepository',90,1010,680,operations=['− ToEntity(message, userId): Data.Entities.Message {static}','− ToDomain(entity): Domain.Conversation {static}','− CopyConversation(source, destination): void {static}'],kind='service')
    p.edge('repository','domain-conv','restores',sides=('left','left'),points=[(65,1090),(65,331)],label_at=(110,900))
    p.edge('repository','conv','maps GUID, time and closed state',sides=('right','left'),points=[(825,1090),(825,644)],label_at=(815,935))
    p.edge('repository','msg','maps GUID, role, content and UTC time',sides=('right','left'),points=[(835,1090),(835,974)],label_at=(800,1178))
    p.klass('context','ApplicationDbContext',950,1255,680,['+ Users: DbSet<User>','+ Conversations: DbSet<Data.Entities.Conversation>','+ Messages: DbSet<Data.Entities.Message>'],['# OnModelCreating(modelBuilder): void'],kind='data')
    p.klass('dbcontext','DbContext',1150,1475,310,kind='external',stereotype='external')
    p.edge('context','dbcontext',relation='inheritance',sides=('bottom','top'))
    p.edge('context','conv','model / DbSet',sides=('right','right'),points=[(1740,1357),(1740,644)],label_at=(1690,1205))
    p.note('mapping','Mapping: Domain.Conversation.Id ↔ entity Id (GUID). Integer ConversationId / MessageId remain database keys.\nStartedAt → Start and DateTime; LastActivityAt → LastUpdated; Closed ↔ ConversationStatus.Closed.\nChatMessage.Role → Message.Status; Content → MessageContent; SentAt → DateTime in UTC.\nSenderId references the owner for Customer, Assistant and System roles; it does not identify a separate AI user.\nRepository operations create / dispose their own context. SaveAsync commits atomically and does not reopen a closed chat.',90,1255,680,270)
    p.legend(1560)
    return p


def ui():
    p=Page('04-ui-classes','4. Blazor UI class diagram','Razor components compile to classes; associations show rendered children, parameters, callbacks and injected services.',2320,1790)
    p.package('ui-package','Yodaphone.Web.Components.Pages / Chat',40,150,1620,1370)
    p.package('external-package','Service / domain / framework dependencies',1710,150,570,1370)
    p.klass('page','Chat',80,220,840,['− _conversation: Domain.Conversation?','− _conversations: IReadOnlyList<Domain.Conversation>','− _messages: IReadOnlyCollection<Domain.ChatMessage>','− _lifetime: CancellationTokenSource','− _isBusy / _isWaitingForReply / _disposed: bool'],['# OnInitializedAsync(): Task','− HandleUserMessageSent(text: string): Task','− StartNewChat(): Task','− SelectConversation(id: Guid): Task','− ShowHelp(): Task','− RunConversationOperation(operation, errorMessage, pendingMessage): Task','+ Dispose(): void'],kind='ui',stereotype='Razor component',namespace='Components.Pages')
    p.klass('sidebar','Sidebar',1060,220,540,['+ Conversations: IReadOnlyList<Domain.Conversation>','+ SelectedConversationId: Guid?','+ IsBusy / CanShowHelp / DarkMode: bool','+ OnConversationSelected: EventCallback<Guid>','+ OnDarkModeToggled: EventCallback','+ OnHelpRequested: EventCallback'],kind='ui',stereotype='Razor component',namespace='Components.Chat')
    p.klass('input','ChatInput',80,875,650,['+ IsSending / IsDisabled: bool','+ OnMessageSent: EventCallback<string>','− _draft: string'],['− OnDraftChanged(e): void','− OnKeyDown(e): Task','− Send(): Task'],kind='ui',stereotype='Razor component',namespace='Components.Chat')
    p.klass('window','ChatWindow',970,875,630,['+ Messages: IReadOnlyCollection<Domain.ChatMessage>','+ PendingMessage: string?','+ IsBotTyping / IsBusy: bool','+ OnNewConversation: EventCallback'],['# OnAfterRenderAsync(firstRender: bool): Task','+ ScrollToBottomAsync(): Task'],kind='ui',stereotype='Razor component',namespace='Components.Chat')
    p.klass('bubble','ChatMessage',970,1275,630,['+ Message: Domain.ChatMessage?','+ PendingContent: string?','− /IsFromAgent: bool'],kind='ui',stereotype='Razor component',namespace='Components.Chat')
    p.edge('page','sidebar','renders / parameters / callbacks','association',multiplicities=('1','1'),label_at=(990,198))
    p.edge('page','input','renders; OnMessageSent callback','association',sides=('bottom','top'),multiplicities=('1','1'),points=[(500,800),(405,800)],label_at=(550,797))
    p.edge('page','window','renders; OnNewConversation callback','association',sides=('bottom','top'),multiplicities=('1','1'),points=[(500,820),(1285,820)],label_at=(1100,820))
    p.edge('window','bubble','renders non-System and pending bubbles','association',sides=('bottom','top'),multiplicities=('1','0..*'),label_at=(1285,1200))
    p.klass('service','IChatService',1750,230,490,operations=['+ StartConversationAsync(CT)','+ GetConversationAsync(id, CT)','+ GetConversationsAsync(CT)','+ GetActiveConversationAsync(CT)','+ AddMessageAsync(id, role, text, CT)','+ SendMessageAsync(id, text, CT)'],kind='service',stereotype='interface',note='Returns domain conversations and messages; complete return types on diagram 1.')
    p.klass('domain','Conversation / ChatMessage',1750,650,490,kind='domain',stereotype='domain types',note='See diagrams 1 and 3.')
    p.klass('js','IJSRuntime',1750,900,490,operations=['+ InvokeAsync<T>(identifier, args): ValueTask<T>'],kind='external',stereotype='external interface',note='ChatWindow calls the InvokeVoidAsync extension.')
    p.klass('disposable','IDisposable',1750,1110,490,operations=['+ Dispose(): void'],kind='external',stereotype='external interface')
    p.klass('component','ComponentBase',1750,1300,490,kind='external',stereotype='external',note='Implicit base of all shown Razor components; inherited lifecycle methods.')
    p.edge('page','service','injects',sides=('right','left'),points=[(960,p.anchor('page','right')[1]),(960,750),(1668,750),(1668,p.anchor('service','left')[1])],label_at=(1668,600))
    p.edge('window','js','injects; chatInterop.scrollToBottom',label_at=(1670,870))
    p.edge('page','disposable',relation='realization',sides=('left','left'),points=[(60,438),(60,1545),(1685,1545),(1685,1156)],label_at=(1400,1545))
    p.edge('bubble','domain','Message parameter',sides=('right','left'),points=[(1690,1390),(1690,704)],label_at=(1690,1260))
    p.edge('bubble','component',relation='inheritance',label_at=(1669,1427))
    p.note('hosting','App renders Routes (InteractiveServer) and ReconnectModal. Routes uses MainLayout and the routed Chat, Error and NotFound pages.\nChat is available at / and /chat. MainLayout derives from LayoutComponentBase. UI shell / error components are summarized here.\nInput callback → Chat page → IChatService; finally the page reloads the selected conversation and sidebar list, including after failures.\nThe window hides System messages. Pending customer text is UI state and is not a domain ChatMessage.',40,1600,2240,145)
    return p


def sequence():
    p=Page('05-send-message-sequence','5. Send message sequence diagram','Successful send to an existing active conversation; each repository operation owns and disposes a fresh EF Core context.',2740,2470)
    participants=[('Customer',145),('ChatInput',450),('Chat page',755),('ChatService',1060),('Repository',1365),('Conversation',1670),('IChatAgent',1975),('SQLite / EF Core',2460)]
    for i,(name,x) in enumerate(participants):
        p.shape(f'participant-{i}',name,x-115,150,230,66,'rect',COLORS['external'] if i in (0,7) else COLORS['ui'] if i<3 else COLORS['service'] if i<5 else COLORS['domain'] if i==5 else COLORS['agent'])
        p.raw_edge([(x,216),(x,2340)],relation='line')
    y=265
    def message(a,b,label,returning=False,gap=58):
        nonlocal y
        ax,bx=participants[a][1],participants[b][1]
        if a==b:
            route=[(ax,y),(ax+155,y),(ax+155,y+28),(ax,y+28)]
            label_at=(ax+180,y-17)
        else:
            route=[(ax,y),(bx,y)]
            label_at=((ax+bx)/2,y-18)
        p.raw_edge(route,label,'return' if returning else 'message',label_at=label_at)
        y+=gap
    message(0,1,'Click Send / press Enter')
    message(1,2,'OnMessageSent.InvokeAsync(text)')
    message(2,3,'SendMessageAsync(id, text, CT)')
    message(3,3,'Validate content; await operationLock',gap=90)
    message(3,4,'GetAsync(Guid, currentUser.UserId, CT)')
    message(4,7,'Create context; load owned conversation + messages')
    message(7,4,'Read complete; dispose context',True)
    message(4,3,'Restored Conversation',True)
    message(3,5,'AddMessage(Customer, content.Trim())')
    p.note('sanitisation','Conversation screens customer content and rejects new messages when closed.',1530,y-8,320,85)
    y+=100
    message(3,4,'SaveAsync(conversation, userId, CT)')
    message(4,7,'Create context; append customer; commit; dispose')
    message(7,4,'Customer message durable',True)
    message(4,3,'Save complete',True)
    message(3,6,'GetReplyAsync(ordered history, CT)')
    p.note('agent-ref','ref diagram 6: provider, response screening and fallback.\nNo database context remains open during this wait.',1880,y-6,670,90)
    y+=105
    message(6,3,'AgentChatResponse',True)
    message(3,3,'Check caller cancellation and non-empty response',gap=90)
    message(3,5,'AddMessage(Assistant, response.Content)')
    message(3,4,'SaveAsync(conversation, userId, CT)')
    message(4,7,'Create context; append assistant; commit; dispose')
    message(7,4,'Assistant reply durable',True)
    message(4,3,'Save complete',True)
    message(3,3,'finally: operationLock.Release()',gap=90)
    message(3,2,'Saved assistant ChatMessage',True)
    message(2,3,'finally: GetConversationAsync(id, CT)')
    message(3,2,'Fresh conversation snapshot',True)
    message(2,3,'GetConversationsAsync(CT)')
    message(3,2,'Updated owned conversation list',True)
    message(2,0,'Render saved messages; clear pending / busy / typing state',True)
    p.note('failure-note','Failure behavior: once the first SaveAsync succeeds, provider failure, empty response or caller cancellation retains the customer message.\nThe UI refreshes saved history even after failure. If another service closes the chat while the agent waits, the second SaveAsync rejects the late reply.\nThe service lock covers this instance only; repository transactions enforce persisted closure across instances.',40,2370,2660,100)
    return p


def activity():
    p=Page('06-copilot-fallback-activity','6. Copilot fallback activity diagram','IChatAgent = CopilotFallbackChatAgent when Chat:Agent is copilot. State lasts for the scoped session / Blazor circuit.',1760,2050)
    p.shape('start','',810,145,28,28,'start','#475569')
    p.shape('validate','ChatAgentBase.GetReplyAsync\nValidate history and cancellation',635,220,380,90)
    p.shape('valid','Valid request?',710,365,230,110,'decision')
    p.shape('invalid','Throw validation / cancellation error\nKeep session mode',70,375,420,100,'action',COLORS['note'])
    p.shape('mode','offlineMode?',710,550,230,110,'decision')
    p.shape('request','Create linked cancellation token\nCancelAfter(responseTimeout)\nawait copilotAgent.GetReplyAsync(...).WaitAsync(token)',500,740,650,110)
    p.shape('success','Request returned\na valid reply?',690,935,270,130,'decision')
    p.shape('caller','Caller cancelled?',1210,945,260,110,'decision')
    p.shape('cancel','Throw OperationCanceledException\nKeep session mode',1210,1160,420,90,'action',COLORS['note'])
    p.shape('switch','offlineMode = true\nLog failure type; stay offline for this session',1020,1360,590,100,'action',COLORS['agent'])
    p.shape('check','Check caller cancellation\nReturn screened Copilot response',225,1150,520,100)
    p.shape('offline','offlineAgent.GetReplyAsync(history, CT)\nReturn fixed service-unavailable support reply',730,1570,680,100,'action',COLORS['agent'])
    p.shape('return','Outer ChatAgentBase screens reply\nReturn AgentChatResponse to ChatService',225,1770,860,90)
    p.shape('end','',640,1930,34,34,'end','#ffffff')
    p.shape('cancel-end','',1400,1295,34,34,'end','#ffffff')
    p.shape('invalid-end','',265,560,34,34,'end','#ffffff')
    p.edge('start','validate',relation='flow',sides=('bottom','top'))
    p.edge('validate','valid',relation='flow',sides=('bottom','top'))
    p.edge('valid','invalid','[no]',relation='flow',sides=('left','right'),label_at=(600,418))
    p.edge('invalid','invalid-end',relation='flow',sides=('bottom','top'))
    p.edge('valid','mode','[yes]',relation='flow',sides=('bottom','top'),label_at=(850,520))
    p.edge('mode','request','[no]',relation='flow',sides=('bottom','top'),label_at=(850,705))
    p.edge('mode','offline','[yes]',relation='flow',sides=('right','right'),points=[(1680,605),(1680,1620)],label_at=(1650,810))
    p.edge('request','success',relation='flow',sides=('bottom','top'))
    p.edge('success','caller','[failure / empty / timeout]',relation='flow',sides=('right','left'),label_at=(1080,969))
    p.edge('caller','cancel','[yes]',relation='flow',sides=('bottom','top'),points=[(1340,1110),(1420,1110)],label_at=(1380,1107))
    p.edge('cancel','cancel-end',relation='flow',sides=('bottom','top'))
    p.edge('caller','switch','[no]',relation='flow',sides=('right','right'),points=[(1650,1000),(1650,1410)],label_at=(1630,1330))
    p.edge('switch','offline',relation='flow',sides=('bottom','top'),points=[(1315,1510),(1070,1510)])
    p.edge('success','check','[yes]',relation='flow',sides=('left','top'),points=[(485,1000)],label_at=(588,999))
    p.edge('check','return','[caller still active]',relation='flow',sides=('bottom','top'),points=[(485,1710),(655,1710)],label_at=(390,1610))
    p.edge('check','cancel','[caller cancelled]',relation='flow',sides=('left','right'),points=[(195,1200),(195,1338),(1640,1338),(1640,1205)],label_at=(860,1338))
    p.edge('offline','return',relation='flow',sides=('bottom','right'),points=[(1070,1715),(1130,1715),(1130,1815)])
    p.edge('return','end',relation='flow',sides=('bottom','top'))
    p.note('screening-note','Inside GitHubCopilotAgent.GetReplyAsync:\nStart a fresh client → create session → send transcript → dispose session, then client.\nChatAgentBase checks the returned content. A forbidden reply becomes a fixed explanation and counts as a valid reply, so Copilot mode remains active.\nSDK errors become ChatAgentException; missing or blank content is rejected.',60,690,390,300)
    p.note('state-note','A new scoped session starts with offlineMode = false.\nCaller cancellation propagates without changing mode.\nTimeout applies to startup, send, and cleanup; late provider results are discarded.',1160,710,500,160)
    return p


def helpers():
    p=Page('07-test-helper-classes','7. Testing infrastructure class diagram','Concrete shared helpers, nested test doubles and their production / framework contracts. Nested names include the containing class.',2600,2770)
    p.package('persistence-helpers','Persistence fixtures and local doubles',40,150,1220,1170)
    p.package('copilot-helpers','Chat agent fixture doubles',1300,150,1260,1170)
    p.package('ui-helpers','Component fixture doubles',40,1370,1220,1060)
    p.package('host-helpers','Framework contracts / hosting helpers',1300,1370,1260,1120)
    p.klass('sqlite','SqliteTestDatabase',80,220,560,['− contexts: ConcurrentBag<ApplicationDbContext>'],['+ CreateAsync(targetMigration, interceptors): Task<SqliteTestDatabase> {static}','+ CreateDbContext(): ApplicationDbContext','+ CreateDbContextAsync(CT): Task<ApplicationDbContext>','+ AssertAllContextsDisposed(): void','+ DisposeAsync(): ValueTask'],kind='double',stereotype='test helper')
    p.klass('db-factory','IDbContextFactory<ApplicationDbContext>',760,240,455,kind='external',stereotype='external interface')
    p.edge('sqlite','db-factory',relation='realization')
    p.klass('context','ApplicationDbContext',760,445,455,kind='data',stereotype='production')
    p.edge('sqlite','context','creates isolated contexts',sides=('right','left'),points=[(690,398),(690,480)],label_at=(690,559))
    p.klass('callback-p','ChatServicePersistenceTests.CallbackAgent',80,650,560,operations=['+ GetReplyAsync(history, CT): Task<AgentChatResponse>'],kind='double',stereotype='nested test double')
    p.klass('callback-v','ChatServiceValidationTests.CallbackAgent',80,830,560,operations=['+ GetReplyAsync(history, CT): Task<AgentChatResponse>'],kind='double',stereotype='nested test double')
    p.klass('agent-interface','IChatAgent',760,750,455,kind='agent',stereotype='interface')
    p.edge('callback-p','agent-interface',relation='realization')
    p.edge('callback-v','agent-interface',relation='realization')
    p.klass('users','ChatServicePersistenceTests.TestCurrentUser\nChatServiceValidationTests.CurrentUser',80,1020,560,['+ UserId: int {get}'],kind='double',stereotype='nested test doubles')
    p.klass('current-user','ICurrentUser',760,1050,455,['+ UserId: int {get}'],kind='service',stereotype='interface')
    p.edge('users','current-user',relation='realization')
    p.klass('unused-repo','ChatServiceValidationTests.UnusedRepository',80,1200,560,kind='double',stereotype='nested test double',note='Implements IConversationRepository; throws if called.')
    p.klass('fake-factory','FakeCopilotClientFactory',1340,220,560,['+ Calls: List<string>','+ ReceivedTokens: List<CancellationToken>','+ Response: AssistantMessageEvent?','+ FailureStage / CancellationStage: string?'],['+ Create(options): ICopilotClient','+ CreateResponse(content): AssistantMessageEvent {static}'],kind='double',stereotype='test double')
    p.klass('factory-contract','ICopilotClientFactory',2100,240,420,kind='agent',stereotype='interface')
    p.edge('fake-factory','factory-contract',relation='realization')
    p.klass('fake-client','FakeCopilotClientFactory.FakeClient',1340,670,560,operations=['+ StartAsync(CT): Task','+ CreateSessionAsync(config, CT): Task<ICopilotSession>','+ DisposeAsync(): ValueTask'],kind='double',stereotype='nested test double')
    p.klass('fake-session','FakeCopilotClientFactory.FakeSession',1340,935,560,operations=['+ SendAndWaitAsync(options, CT): Task<AssistantMessageEvent?>','+ DisposeAsync(): ValueTask'],kind='double',stereotype='nested test double')
    p.klass('client-contract','ICopilotClient',2100,680,420,kind='agent',stereotype='interface')
    p.klass('session-contract','ICopilotSession',2100,965,420,kind='agent',stereotype='interface')
    p.edge('fake-client','client-contract',relation='realization')
    p.edge('fake-session','session-contract',relation='realization')
    p.edge('fake-factory','fake-client','creates',sides=('bottom','top'))
    p.edge('fake-client','fake-session','creates',sides=('bottom','top'))
    p.klass('base-doubles','ChatAgentBaseTests.StubResponseAgent\nChatAgentBaseTests.RecordingChatAgent',1340,1170,560,operations=['# GetReplyCoreAsync(history, CT): Task<AgentChatResponse>'],kind='double',stereotype='nested test doubles')
    p.klass('base-contract','ChatAgentBase',2100,1195,420,kind='agent',stereotype='abstract')
    p.edge('base-doubles','base-contract',relation='inheritance')
    p.klass('interaction-service','ChatInteractionTests.TestChatService',80,1440,560,operations=['+ IChatService operations'],kind='double',stereotype='nested test double')
    p.klass('chat-contract','IChatService',760,1465,455,kind='service',stereotype='interface')
    p.edge('interaction-service','chat-contract',relation='realization')
    p.klass('render-repo','ChatRenderingTests.TestConversationRepository',80,1650,560,['− conversations: Dictionary<Guid, Domain.Conversation>'],operations=['+ IConversationRepository operations'],kind='double',stereotype='nested test double')
    p.klass('repo-contract','IConversationRepository',760,1680,455,kind='service',stereotype='interface')
    p.edge('render-repo','repo-contract',relation='realization')
    p.edge('unused-repo','repo-contract',relation='realization',sides=('right','right'),points=[(1240,1260),(1240,1718)])
    p.klass('render-user','ChatRenderingTests.TestCurrentUser',80,1905,560,['+ UserId: int = 1'],kind='double',stereotype='nested test double')
    p.klass('ui-user-contract','ICurrentUser',760,1930,455,kind='service',stereotype='interface')
    p.edge('render-user','ui-user-contract',relation='realization')
    p.klass('js-double','ChatRenderingTests.NoOpJsRuntime',80,2145,560,operations=['+ InvokeAsync<T>(identifier, args): ValueTask<T>','+ InvokeAsync<T>(identifier, CT, args): ValueTask<T>'],kind='double',stereotype='nested test double')
    p.klass('js-contract','IJSRuntime',760,2170,455,kind='external',stereotype='external interface')
    p.edge('js-double','js-contract',relation='realization')
    p.klass('async-disposable','IAsyncDisposable',2100,1465,420,operations=['+ DisposeAsync(): ValueTask'],kind='external',stereotype='external interface')
    p.edge('client-contract','async-disposable',relation='inheritance',sides=('right','right'),points=[(2540,718),(2540,1520)])
    p.edge('session-contract','async-disposable',relation='inheritance',sides=('right','right'),points=[(2530,1003),(2530,1520)])
    p.edge('sqlite','async-disposable',relation='realization',sides=('left','left'),points=[(60,398),(60,1345),(2080,1345),(2080,1520)],label_at=(1050,1345))
    p.klass('barrier','SqliteConversationRepositoryTests.FirstUserLookupBarrier',1340,1660,560,operations=['+ ReaderExecutedAsync(...): ValueTask<DbDataReader>'],kind='double',stereotype='nested test interceptor')
    p.klass('interceptor','DbCommandInterceptor',2100,1685,420,kind='external',stereotype='external')
    p.edge('barrier','interceptor',relation='inheritance')
    p.klass('host','ApplicationHostingTests.TestApplicationFactory',1340,1875,560,operations=['# ConfigureWebHost(builder): void','+ DisposeAsync(): ValueTask'],kind='double',stereotype='nested test host')
    p.klass('waf-app','WebApplicationFactory<App>',2100,1900,420,kind='external',stereotype='external')
    p.edge('host','waf-app',relation='inheritance')
    p.klass('integration','ChatApplicationFactory',1340,2120,560,operations=['# ConfigureWebHost(builder): void','# Dispose(disposing): void'],kind='double',stereotype='test host')
    p.klass('waf-program','WebApplicationFactory<Program>',2100,2145,420,kind='external',stereotype='external')
    p.edge('integration','waf-program',relation='inheritance')
    p.klass('running','ChatJourneyTests.RunningChatApplication',1340,2340,560,operations=['+ StartAsync(): Task<RunningChatApplication> {static}','+ DisposeAsync(): ValueTask'],kind='double',stereotype='nested test host')
    p.edge('running','async-disposable',relation='realization',sides=('right','bottom'),points=[(2050,2464),(2050,1605),(2310,1605)])
    p.note('notes','Tests use real SQLite schema / transactions through SqliteTestDatabase, fake provider client / session interfaces for offline Copilot tests, HtmlRenderer for rendered HTML, bUnit for UI events, WebApplicationFactory for host integration, and Playwright for the browser journey.\nMultiple qualified names in one box represent distinct small local helpers with the same contract; no shared helper class is implied.',40,2560,2520,160)
    return p


def mxfile(pages):
    return '<?xml version="1.0" encoding="UTF-8"?>\n<mxfile host="app.diagrams.net" agent="Yodaphone UML documentation" version="24.7.17" compressed="false">' + ''.join(page.xml() for page in pages) + '</mxfile>\n'


def validate_xml(data, filename, expected_labels=None):
    parser = pyexpat.ParserCreate()
    ids, refs = set(), []
    pages, vertices, edges = 0, 0, 0
    labels = {}
    def start(name, attributes):
        nonlocal pages, vertices, edges, ids, refs, labels
        if name == 'diagram':
            pages += 1
            ids, refs = set(), []
            labels = (expected_labels or {}).get(attributes['id'], {})
        if name == 'mxCell':
            identifier = attributes['id']
            assert identifier not in ids, (filename, 'duplicate cell', identifier)
            ids.add(identifier)
            if identifier in labels:
                assert attributes.get('value', '') == labels[identifier], (filename, identifier, 'decoded label differs; check XML whitespace escaping')
            vertices += attributes.get('vertex') == '1'
            edges += attributes.get('edge') == '1'
            refs += [attributes[key] for key in ('parent','source','target') if key in attributes]
    def end(name):
        if name == 'diagram':
            assert all(ref in ids for ref in refs), (filename,'invalid cell reference')
            assert labels.keys() <= ids, (filename, 'missing expected label')
    parser.StartElementHandler = start
    parser.EndElementHandler = end
    parser.Parse(data, True)
    return pages, vertices, edges


def validate_layout(page):
    boxes=list(page.nodes.items())
    for ident,box in boxes:
        assert box['x'] >= 0 and box['y'] >= 0
        assert box['x']+box['w'] <= page.width, (page.slug,ident,'outside width')
        assert box['y']+box['h'] <= page.height, (page.slug,ident,'outside height')
    for i,(a,aa) in enumerate(boxes):
        for b,bb in boxes[i+1:]:
            area_x=min(aa['x']+aa['w'],bb['x']+bb['w'])-max(aa['x'],bb['x'])
            area_y=min(aa['y']+aa['h'],bb['y']+bb['h'])-max(aa['y'],bb['y'])
            assert area_x<=0 or area_y<=0, (page.slug,a,b,'overlapping boxes')
    # Clip each segment against unrelated node interiors, including diagonals.
    def crosses(a, b, box):
        low, high = 0.0, 1.0
        for coordinate in range(2):
            start, delta = a[coordinate], b[coordinate] - a[coordinate]
            lo = box['x' if coordinate == 0 else 'y'] + 2
            hi = lo + box['w' if coordinate == 0 else 'h'] - 4
            if delta == 0:
                if not lo < start < hi:
                    return False
            else:
                t1, t2 = (lo - start) / delta, (hi - start) / delta
                low, high = max(low, min(t1, t2)), min(high, max(t1, t2))
                if low >= high:
                    return False
        return low < high
    for edge in page.edges:
        if edge['relation'] == 'line':
            continue
        for a, b in zip(edge['route'], edge['route'][1:]):
            for identifier, box in boxes:
                if identifier not in (edge['source'], edge['target']):
                    assert not crosses(a, b, box), (page.slug, edge['source'], edge['target'], identifier, 'connector crosses unrelated node')


def main():
    pages=[application(),testing(),persistence(),ui(),sequence(),activity(),helpers(),application(combined=True)]
    repository = OUT.parent.parent
    fixtures = set()
    for source in (repository / 'tests').rglob('*.cs'):
        if 'obj' not in source.parts and 'bin' not in source.parts:
            fixtures.update(re.findall(r'public\s+sealed\s+class\s+(\w+Tests)\b', source.read_text()))
    drawn_fixtures = {box['name'] for box in pages[1].nodes.values() if box['name'].endswith('Tests')}
    assert fixtures == drawn_fixtures, ('test fixture inventory differs', fixtures - drawn_fixtures, drawn_fixtures - fixtures)
    for page in pages:
        validate_layout(page)
        xml=mxfile([page])
        count=validate_xml(xml,page.slug,{page.slug: page.labels})
        svg=page.svg()
        validate_xml(svg,page.slug+'.svg')
        (OUT/(page.slug+'.drawio')).write_text(xml)
        (OUT/(page.slug+'.svg')).write_text(svg)
        print(f'{page.slug}: {count[1]} editable cells, {count[2]} connections')
    combined=mxfile(pages)
    counts=validate_xml(combined,'yodaphone-uml.drawio',{page.slug: page.labels for page in pages})
    (OUT/'yodaphone-uml.drawio').write_text(combined)
    print(f'yodaphone-uml.drawio: {counts[0]} pages; all {len(fixtures)} test fixtures included; XML, label line breaks and layout checks passed')
    from generate_poster import main as generate_poster
    generate_poster()
    from generate_ui_poster import main as generate_ui_poster
    generate_ui_poster()


if __name__ == '__main__':
    main()
