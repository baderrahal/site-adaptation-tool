# Parsons tool identity

The look of the Parsons Digital Delivery tools, taken from Navis2D3D (approved by Bader, BIM Manager, 11 and 12 Nov 2026). Use
it for any new add-in or tool window so they all look like one family. Give this file to whoever builds the tool, and keep the
rules as they are.

What is in this kit:
- `IDENTITY.md`: this file, every rule and value.
- `wpf/Theme.xaml`: the WPF resource dictionary with every colour, shadow, font and control style below, ready for a .NET
  Framework 4.8 WPF add-in (Revit, Navisworks, Civil 3D, AutoCAD).
- `fonts/`: Cairo 400, 500, 600 and 700 (SIL Open Font Licence, `OFL.txt`), Latin and Arabic.
- `parsons-p.png`: the Parsons P mark, 120 by 140 px, transparent, shown at 30 by 35 px.

## 1. The idea in one line
Floating white panels over a full work area, a slim rail of buttons on each side, cards inside panels, one orange accent. A
chosen item gets an orange edge and a soft glow, never a filled block. Closest known systems: Material 3 for the structure,
Figma UI3 for the feel, Fluent 2 for the Windows details.

## 2. Layout
- The main content (drawing, model view, table) fills the window. Nothing sits over it but floating panels.
- Left rail 72 px: buttons 60 px, a 20 px icon with one word under it at 11 px. The tool's main places at the top. Model,
  Settings and the like at the bottom. A count badge where something waits for the person.
- The Parsons P at the top of the left rail, 30 by 35 px, with an orange glow (#F0542D) coming out from its edge: a blurred
  orange copy of the P behind it. Never a round glow, never an effect on the logo itself.
- Right rail 72 px: what can be done with what is open, then (bottom) a counter with round up and down buttons for the previous
  and next item, and zoom in, out and fit in a round pill with the zoom percent under it.
- Floating panels: white, radius 18, border #E7E5E4, soft shadow. A panel holds cards (radius 14, #FAFAF9), cards hold items
  (radius 12, white).
- Dialogs: radius 22 to 24, a round close button (40 px circle) top right, a thin rule under the title, an inner card radius 16,
  two buttons bottom right (Cancel outlined, the action filled), 42 px high, radius 12.
- Choices as tiles (44 px, radius 12) with a tick box. A segmented pill for two scopes. Status as small chips.
- In a narrow window the content comes first: side lists fold away, panels dock beside the content while it keeps 300 px,
  else they float over it. A long section scrolls in its own area. Nothing may overlap.

## 3. Colour
| Use | Value | Theme key |
|---|---|---|
| Accent: filled buttons, orange text on white | #D2421C (4.6 to 1 on white) | Accent |
| Orange text on grey or tinted cards | #B5381A | AccentDeep |
| Edges, marks, glow, icons only (never text) | #F0542D | Edge |
| Glow behind a chosen item | rgba(240,84,45,0.10), 4 px | Glow |
| Window ground | #F1EFEC | Ground |
| Work area behind the content | #F7F6F4 | Viewer |
| Panels | #FFFFFF | Card |
| Cards inside panels | #FAFAF9 | Inner |
| Lines and borders | #E7E5E4 | Line |
| Text | #1C1917 (Ink), #292524 (Text) | Ink, Text |
| Second text (still dark, never grey notes) | #44403C | Quiet |
| Hover | #F5F5F4 | Hover |
| Pale orange behind a hovered or chosen item | #FFF4F0 | EdgeSoft |
| Amber chip (changed, out of date) | #8A5A00 on #FFF8EB | Amber, AmberSoft |
| Red chip (overdue, gone) | #B4232A on #FDECEC | Red, RedSoft |
| Edge of a ticked row | #F3B7A5 | EdgeTicked |

- The chosen item: white, a 1.5 px #F0542D border, glow 0 0 0 4px rgba(240,84,45,0.10). No filled blocks.
- Keyboard focus: a 2 px near black ring 3 px outside the item. Never orange, so focus and choice never look the same.
- Findings on a drawing: an orange marker, a dashed orange frame round the element, a soft orange glow on the area.
- Three shadow levels: panels 8 px blur, menus 16 px, dialogs 28 px. A shadow sits on an empty border behind the panel, never on
  a panel that holds text.

## 4. Type and icons
- Cairo for everything (bundled in `fonts/`). 14 px body, 11 px rail words, 12 px small labels, 16 to 18 px titles. Sentence
  case. Bold only for a name or a count that matters.
- Fluent System Icons (Microsoft, MIT licence), 20 px regular, filled with the text colour. The shapes used are in
  `wpf/Theme.xaml` as Geometry resources named Ic.*.

## 5. Words
- Plain words a BIM coordinator uses. BIM terms are welcome (federated model, clash, workset, CDE, transmittal, LOD,
  ISO 19650). No IT words: no pipeline, backend, deploy, stack, schema, JSON, pattern.
- No em dashes and no semicolons anywhere, in the tool or its notes.
- No grey notes and no sentence that explains a title, a field or a result. Every line on screen carries information. Help
  goes in the tooltip.
- No label the person has to decode: write "1 section differs from the profile", not "Changed 1".
- Settings in BIM words, as tables, lists and choices. Never a text pattern or a JSON file for the person to edit.
- A button says what it does ("Save the ticked views"), and the result line uses the same verb ("Saved 12").
- Counts follow each edit and hide at 0.

## 6. Bader's rules for every board and window (12 Nov 2026)
- No tick icons and no green for a result (a passed check, a value read right, a code found). Ticks only in real tick boxes.
  A passed check is a plain chip with its name under "Passed N".
- No coloured bar down the side of a card.
- Actions that bring things in sit together on the left side of a window, not in the footer.
- Nothing may overlap. A long section scrolls in its own area.
- A change asked on one board of a window goes on every board of that window at once.
- New windows and new sections are drawn and approved on the canvas before they are built. Everything drawn and approved gets
  built: keep a task list, and an answer of "keep" on something not yet built becomes a task the same day.
- A child dialog gives the focus back to its window when it closes.

## 7. Using the WPF theme in another add-in
1. Copy `wpf/Theme.xaml` into the add-in's UI project, and `fonts/` into a Fonts folder of that project.
2. In the project file, add the fonts as `<Resource Include="Fonts\*.ttf" />` and the logo as `<Resource Include="Images\*.png" />`.
3. In `Theme.xaml`, change two names to the new project:
   - the line `xmlns:ui="clr-namespace:Navis2D3D.Ui"`, to the new UI namespace
   - the two FontFamily lines `pack://application:,,,/Navis2D3D.Ui;component/Fonts/#Cairo`, to the new UI assembly name
   Some styles in it name Navis2D3D controls (sheet list rows, finding rows). Keep them as examples or delete them.
4. Merge the dictionary in each window or pane (`<ResourceDictionary Source="Theme.xaml" />`), and use the keys: Rail for rail
   buttons, IconButton for round buttons, Primary and Plain for the two dialog buttons, Segment for the scope pill, Float for a
   floating panel, Row for list rows, Field for a text box.
5. Give every control a fixed AutomationId (for example `N2.Sheet.Next`, with your own prefix) and keep a list of them, so a
   tester can drive the window by name.
