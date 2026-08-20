# RaiDiagram

RaiDiagram is RAIkeep's domain-neutral diagram package. Its `.raid` files are
agent-readable JSON5 manifests that keep semantic projection, model references,
and presentation intent separate from application domain models.

## 4.2.2

The coordinated `4.2.2` patch implements accepted CR010 and completes the CR009
consumer export surface. `DiagramModel.FromManifest(...)` and
`DiagramDestination.CreateSubscriberRoot()` are public. Typed `.raid`, clean
`.puml`, resolved `_config.puml`, and rendered image artifacts share the
existing subscriber `ItemTreePath` placement without introducing identity
management. Explicit local style locations, deterministic common/diagram-kind
layering, default seeding, and SVG style provenance are supported through the
real PlantUML `-config` path.

Current release notes:
[RaiDiagram_RELEASE_NOTES_4.2.2.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/RaiDiagram_RELEASE_NOTES_4.2.2.md).

## 4.2.0

The initial `4.2.0` release implements approved CR009 and participates in the
coordinated RAIkeep `4.2.0` release chain with OsLibCore, RaiUtils, and RaiImage.

The first release provides:

- role-first UML26 diagram semantics;
- generic model-provider and structured reconciliation contracts;
- deterministic semantic hashing;
- PlantUML compilation and rendering through RaiImage; and
- authoritative `.raid` identity and semantic hash metadata in rendered SVG.

AIA implements its WWWA bridge in `AIA.Core`; RaiDiagram has no AIA, WWWA, or
JsonPit dependency.

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

See the approved
[CR009 package design](https://github.com/Burkhardt/RAIkeep/blob/main/doc/CR009_AIA_to_RAIkeep_RaiDiagram_Package.md),
the [package-boundary ADR](https://github.com/Burkhardt/RAIkeep/blob/main/doc/ADR001_RaiDiagram_Package_Boundary.md),
the [subscriber artifact and style request](https://github.com/Burkhardt/RAIkeep/blob/main/doc/CR010_AfricaStage_to_RAIkeep_RaiDiagram_Subscriber_Scoped_Artifacts_and_Styles.md),
the [subscriber placement and lookup ADR](https://github.com/Burkhardt/RAIkeep/blob/main/doc/ADR002_RaiDiagram_Subscriber_Scoped_Artifacts_and_Style_Lookup.md),
and the [foldable API reference](https://github.com/Burkhardt/RaiDiagram/blob/main/API.md).
