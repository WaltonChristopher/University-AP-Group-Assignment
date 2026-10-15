# Yodaphone UML diagrams

Open **[yodaphone-uml.drawio](yodaphone-uml.drawio)** in draw.io / diagrams.net
to edit all ten diagrams in one document. Each tab is also available as a
separate `.drawio` file below.

For a single view combining diagrams 1 and 3, open
**[the combined application and persistence class diagram](08-application-persistence-classes.drawio)**
or its [SVG preview](08-application-persistence-classes.svg). Shared classes appear
once, with the repository connecting domain objects to the EF Core entities and
database context. This view is also page 8 of the complete document.

For a poster, use **[diagram 9: class overview](09-poster-class-overview.drawio)**
or its [scalable SVG](09-poster-class-overview.svg). It preserves all 30 types
and 39 relationships from diagram 8, removes attributes, methods and notes,
and increases the class-name font size from 14 to 36. The layout is shorter,
with larger package headings and multiplicities. SVG can be enlarged for print
without losing sharpness.

**[Diagram 10: poster UI classes](10-poster-ui-classes.drawio)** and its
[scalable SVG](10-poster-ui-classes.svg) apply the same treatment to diagram 4.
All 10 class boxes, 9 relationships and their multiplicities are retained,
with font size 36 for class names and no attributes, methods or notes.

The `.drawio` files contain uncompressed native draw.io XML. Class boxes,
member compartments, connectors, arrowheads and labels are editable shapes.
Use the application's file-open or import command to load them. The SVG files
are vector previews for viewing, printing or inserting into a report.

| Diagram | Editable draw.io file | SVG preview |
| --- | --- | --- |
| 1. Application classes | [Production class diagram](01-application-classes.drawio) | [Preview](01-application-classes.svg) |
| 2. Classes including testing | [Class diagram with all 19 test fixtures](02-classes-with-tests.drawio) | [Preview](02-classes-with-tests.svg) |
| 3. Persistence classes | [Domain and database mapping](03-persistence-classes.drawio) | [Preview](03-persistence-classes.svg) |
| 4. Blazor UI classes | [Components, callbacks and dependencies](04-ui-classes.drawio) | [Preview](04-ui-classes.svg) |
| 5. Send-message sequence | [Message persistence and reply workflow](05-send-message-sequence.drawio) | [Preview](05-send-message-sequence.svg) |
| 6. Copilot fallback activity | [Timeout, fallback and cancellation](06-copilot-fallback-activity.drawio) | [Preview](06-copilot-fallback-activity.svg) |
| 7. Testing infrastructure classes | [Shared helpers and nested test doubles](07-test-helper-classes.drawio) | [Preview](07-test-helper-classes.svg) |
| 8. Combined application and persistence classes | [Integrated diagrams 1 and 3](08-application-persistence-classes.drawio) | [Preview](08-application-persistence-classes.svg) |
| 9. Poster class overview | [Classes and relationships only](09-poster-class-overview.drawio) | [Poster SVG](09-poster-class-overview.svg) |
| 10. Poster UI classes | [UI classes and relationships only](10-poster-ui-classes.drawio) | [Poster SVG](10-poster-ui-classes.svg) |

## Reading the diagrams

- `+` public, `−` private, `#` protected, `~` internal, `/` derived property.
- Dashed open arrows show dependencies. `«tests»` identifies a fixture's
  dependency on the production class it exercises.
- Dashed hollow triangles show interface realization; solid hollow triangles
  show inheritance. The triangle points toward the interface or base class.
- Solid lines show associations; filled diamonds show composition at the
  owning end. Association ends carry multiplicities such as `1` and `0..*`.
- `CT` abbreviates `CancellationToken`. Selected members are shown, with most
  constructors and method default values omitted. Static members are marked
  `{static}` or belong to a `«static»` class.
- Blue denotes domain types, green services, amber agents / test helpers,
  purple database types, teal UI components, pink test fixtures, and grey
  external framework types.

The testing overview summarizes fixture behavior in the final compartment.
Diagram 7 expands the concrete helpers and their contracts. Boxes with several
qualified names summarize separate small local types that share a contract;
those names do not imply an additional class in the source. Repeated production
types in the testing overview represent the same application type.

## Scope and source references

The diagrams describe the implemented ASP.NET Core / Blazor application and
both test projects. Diagram 1 focuses on the domain, services and agents;
diagrams 3 and 4 expand persistence and UI details. Diagram 8 combines the
application and persistence views, showing one copy of each shared class.
Diagram 9 presents those same types and relationships without member details
or explanatory connector labels, for use in a poster.
Diagram 10 presents the UI types and relationships from diagram 4 in the same
poster format, preserving the original grouped domain-types box.
The UI shell and private
Copilot SDK adapters are summarized in notes. Generated EF designer files,
the model snapshot, `bin` / `obj` outputs, and the unreferenced root `main.cs`
console example are outside these views.

| Area | Source |
| --- | --- |
| Startup and service registrations | [Program.cs](../../src/Yodaphone.Web/Program.cs) |
| Domain classes and response screening | [Domain](../../src/Yodaphone.Web/Domain/) |
| Services and SQLite repository | [Services](../../src/Yodaphone.Web/Services/) |
| Agent contracts, fallback and SDK adapters | [Chat](../../src/Yodaphone.Web/Chat/) |
| Database entities and relationships | [Data](../../src/Yodaphone.Web/Data/) |
| Razor components | [Components](../../src/Yodaphone.Web/Components/) |
| Domain fixtures | [Yodaphone.Domain.Tests](../../tests/Yodaphone.Domain.Tests/) |
| Service, agent, component, hosting, integration and browser fixtures | [Yodaphone.Web.Tests](../../tests/Yodaphone.Web.Tests/) |

The persistence view distinguishes domain GUIDs from integer database keys.
The sequence view shows the customer message committed before the agent call,
and the assistant reply committed afterward. The activity view distinguishes
provider failure / timeout, caller cancellation, and blocked content replaced
by an explanation while Copilot remains active.

## Regenerating and checking

The reviewed model is maintained in [generate.py](generate.py). Update its
classes, relationships and coordinates when the source changes, then run from
the repository root:

```bash
python3 docs/uml/generate.py
```

The generator uses Python built-in modules. It validates XML syntax, unique
cell IDs, parent / connector references, decoded label text and line breaks,
node bounds, non-overlapping node
boxes, connector routes around unrelated nodes, and note text height.
It also checks that the second diagram includes
every current public test fixture. It regenerates the combined document,
individual draw.io documents and SVG previews deterministically.

To regenerate only the poster from the current diagram 8, preserving manual
edits to the other diagrams, run:

```bash
python3 docs/uml/generate_poster.py
```

This updates diagram 9's draw.io file, SVG, and tab in the complete document.

To regenerate only the UI poster from the current diagram 4, run:

```bash
python3 docs/uml/generate_ui_poster.py
```

This updates diagram 10's draw.io file, SVG, and tab, preserving the other pages.

The SVGs were also rendered in Chromium and inspected; their text fits
within the page bounds. The diagrams document test structure and behavior.
