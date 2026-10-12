# RaiDiagram

## 4.5.10

Coordinated 4.5.10 release; public behavior is aligned with the synchronized platform.

Latest release notes: [RaiDiagram 4.5.10 release notes](https://github.com/Burkhardt/RAIkeep/blob/main/doc/releases/v4.x/RaiDiagram_RELEASE_NOTES_4.x.md#4510).

## Previous Versions

For release notes and changelogs of earlier releases, see the [RaiDiagram 4.x Release Notes](https://github.com/Burkhardt/RAIkeep/blob/main/doc/releases/v4.x/RaiDiagram_RELEASE_NOTES_4.x.md).

## Installation

### .NET CLI

```bash
dotnet add package RaiDiagram --version 4.5.10
```

### PackageReference

```xml
<PackageReference Include="RaiDiagram" Version="4.5.10" />
```

RaiDiagram is RAIkeep's domain-neutral diagram package. Its `.raid` files are agent-readable JSON5 manifests that keep semantic projection, model references, and presentation intent separate from application domain models.

## Object and deployment import (CR052)

```plantuml
@startuml Orders
object "Order42 : Order" as order {
  Number = 42
  Details = { 'Currency': 'EUR', 'Total': '25.00' }
}
actor "Customer" as customer
customer --> order : places
@enduml
```

```bash
raid import --puml Orders.puml --out ./artifacts --name Orders --name-ext OD
```

ObjectProperties preserves ordered text slot values in schema 1.1 and renders
visible SVG compartments. Existing schema 1.0 remains readable. Deployment
PlantUML supports `node`, `cloud`, `component`, `database`, `artifact`, `folder`,
and `frame`, with resident containment and protocol labels. Use `_VD` via
`--name-ext VD` or `DistributionDiagramBuilder` for deployment models.

Parsing errors report source:line:column and codes PUML001–PUML005 before writes.
PUML101 warns about retained presentation hints not applied by the canvas renderer.
RAID201 identifies unsupported rendering. Unsupported constructs, includes, and
macros are rejected instead of silently dropping content or inventing classes.

PlantUML is the active interchange format. The retained, frozen deployment-only
Poseidon/OTW importer is available as `raid import-xmi file.xmi --list-diagrams`
and `raid import-xmi file.xmi --diagram <id-or-name> --out ./artifacts --name Servers`.
`import-otw` is an alias. It preserves drawing bounds and authored paths, without
claiming complete legacy visual fidelity or support for other XMI diagram families.

## End-to-end example

The checked-in [ScheduleRehearsal example](https://github.com/Burkhardt/RaiDiagram/tree/main/examples/ScheduleRehearsal)
contains an authoritative JSON5 `.raid` manifest, its deterministic generated
PlantUML source, its resolved PlantUML config, and the SVG rendered by the real
PlantUML CLI. The generated `.puml` contains diagram declarations only; theme,
handwritten mode, and style are injected with PlantUML `-config`. The SVG embeds
the manifest identity plus semantic, config, and render hashes. Regression tests
load the `.raid` directly, verify all four artifacts remain synchronized, and
execute the same production rendering path used by consumers.

The resolved config selects PlantUML's built-in `cerulean` theme, enables
handwritten rendering with `!option handwritten true`, and declares
`Chalkduster, Comic Sans MS` as an ordered SVG font-family fallback. No theme,
style, include, or deprecated handwritten `skinparam` is emitted into the
generated diagram source.

`PumlStyleCatalog` combines common styles with the style files registered for
the `.raid` `DiagramKind`. `DiagramStyleProvider` resolves those layers into one
immutable config snapshot before rendering. `PumlStyleFile`, `PumlThemeFile`,
and `PumlConfigFile` use the existing ImageTree subscriber and `ItemTreePath`
conventions, just like generated source, config, SVG, PNG, and WebP artifacts.
Subscriber is a storage-routing segment, not an authenticated identity.
The checked-in
[raikeep-sketch theme](https://github.com/Burkhardt/RaiDiagram/blob/main/themes/puml-theme-raikeep-sketch.puml)
remains a portable Git source example; deployments may also materialize it in a
local subscriber ImageTree. RaiDiagram never resolves a remote theme URL or
infers a parent subscriber hierarchy.

Each production render materializes one subscriber-co-located ImageTree artifact
set: the authoritative `.raid` snapshot, clean `.puml`, resolved
`_config.puml`, and rendered `.svg`. The config is a `.puml` file using
`NameExt = "config"`; for example `ScheduleRehearsal_config.puml`. SVG uses the
same subscriber and path convention as PNG and WebP; only the file format
differs.

Style fallback locations are always supplied explicitly in least-specific to
most-specific order. RaiDiagram performs no identity management, authorization,
parent-directory walking, or application/subscription/tenant inference.

## Testing with PlantUML

The compiler, manifest, style, provenance, and command-contract tests do not
require external diagramming tools. Four `PlantUMLIntegration` tests additionally
exercise the real renderer. They run when `plantuml -version` confirms a complete
local installation (including Graphviz for graph-based diagrams) and are reported
as skipped when that environment is unavailable. Set
`RAIDIAGRAM_REQUIRE_REAL_PLANTUML=1` to turn an unavailable renderer into a test
failure, for example in a dedicated integration environment.

See the approved
[CR009 package design](https://github.com/Burkhardt/RAIkeep/blob/main/doc/CR009_AIA_to_RAIkeep_RaiDiagram_Package.md),
the [package-boundary ADR](https://github.com/Burkhardt/RAIkeep/blob/main/doc/ADR-0001-RaiDiagram-Package-Boundary.md),
the [subscriber artifact and style request](https://github.com/Burkhardt/RAIkeep/blob/main/doc/CR010_AfricaStage_to_RAIkeep_RaiDiagram_Subscriber_Scoped_Artifacts_and_Styles.md),
the [subscriber placement and lookup ADR](https://github.com/Burkhardt/RAIkeep/blob/main/doc/ADR-0002-RaiDiagram-Subscriber-Scoped-Artifacts-and-Styles.md),
and the [foldable API reference](https://github.com/Burkhardt/RaiDiagram/blob/main/API.md).
