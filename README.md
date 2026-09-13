# RaiDiagram

RaiDiagram is RAIkeep's domain-neutral diagram package. Its `.raid` files are
agent-readable JSON5 manifests that keep semantic projection, model references,
and presentation intent separate from application domain models.

## 4.3.1

RaiDiagram 4.3.1 implements accepted CR026. `CapturedRevision` is preserved as
an opaque exact string through typed builders, model snapshots, serialization,
and hand-authored JSON5 `.raid` parsing. ISO-looking values retain their exact
precision and offset instead of being converted through the current culture or
machine time zone.

`OneUseCaseDiagramBuilder.AddObjectReference(...)` now accepts an optional
`stereotype`. Explicit verbs such as `produces` render directly as `«produces»`;
omitting the argument preserves the established `«references {bucket}»` label.

Current notes:
[RaiDiagram_RELEASE_NOTES_4.3.1.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/RaiDiagram_RELEASE_NOTES_4.3.1.md).

## 4.3.0

RaiDiagram 4.3.0 implements accepted CR025 with typed builders for one-use-case,
role-filler/object, class, activity, and sequence diagrams. Builders expose the
base `ItemId`, optional `ItemNumber`, and archetype `NameExt` separately while
producing canonical diagram identities such as `SignContract_02_UCD`.

The managed compiler injects `allowmixing` only for actual cross-grammar object
mixtures, emits native activity and sequence syntax, and remains free of Java or
Graphviz requirements. The optional server renderer is retained and its release
suite requires the complete PlantUML 1.2026.8 CLI. `.raid`, `.puml`, config, and
`.svg` files are co-located through `DiagramArtifactSet` and the existing
`ItemTreePath` conventions.

Current notes:
[RaiDiagram_RELEASE_NOTES_4.3.0.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/RaiDiagram_RELEASE_NOTES_4.3.0.md).

## 4.2.11 (superseded before publication)

This prepared line was not published; its CR023 changes are carried by v4.3.0.
The PlantUML compiler renders
typed and concise relationship kinds—including AIA's `Role` binding—with labels
and cardinality. Mixed Activity/Object projections use `allowmixing` without
flattening their declared element kinds. The compiler's exact accepted element
and relationship vocabularies are public, while unknown constructs remain
fail-closed.

Current notes:
[RaiDiagram_RELEASE_NOTES_4.2.11.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/RaiDiagram_RELEASE_NOTES_4.2.11.md).

## 4.2.10

RaiDiagram participates unchanged in the coordinated `4.2.10` dependency line.
Current notes:
[RaiDiagram_RELEASE_NOTES_4.2.10.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/RaiDiagram_RELEASE_NOTES_4.2.10.md).

## 4.2.9

RaiDiagram participates unchanged in the coordinated `4.2.9` CR022
incident-correction line and consumes the hardened OsLibCore/RaiImage cloud
filesystem boundary. Current notes:
[RaiDiagram_RELEASE_NOTES_4.2.9.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/RaiDiagram_RELEASE_NOTES_4.2.9.md).

## 4.2.8

RaiDiagram participates unchanged in the coordinated `4.2.8` CR021 dependency line.
Current notes: [RaiDiagram_RELEASE_NOTES_4.2.8.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/RaiDiagram_RELEASE_NOTES_4.2.8.md).

## 4.2.7

The coordinated `4.2.7` patch implements accepted CR020. `RaidFile`,
`PumlSourceFile`, `PumlConfigFile`, `PumlStyleFile`, `PumlThemeFile`, and
`DiagramArtifactSet` can be constructed around one `ItemTreePath`, keeping
diagram and image artifacts under the same exact-ItemId ownership and movement
contract.

Current release notes:
[RaiDiagram_RELEASE_NOTES_4.2.7.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/RaiDiagram_RELEASE_NOTES_4.2.7.md).

## 4.2.6

The coordinated `4.2.6` patch adopts the accepted CR019 RaiUtils/RaiImage
package line. Diagram behavior is unchanged from 4.2.5.

Current release notes:
[RaiDiagram_RELEASE_NOTES_4.2.6.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/RaiDiagram_RELEASE_NOTES_4.2.6.md).

## 4.2.5

The coordinated `4.2.5` patch aligns RaiDiagram's fallback dependencies with
the accepted CR017 package line. Diagram behavior is unchanged from 4.2.4.

Current release notes:
[RaiDiagram_RELEASE_NOTES_4.2.5.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/RaiDiagram_RELEASE_NOTES_4.2.5.md).

## 4.2.4

The coordinated `4.2.4` patch adopts RaiImage's accepted CR016 Unicode-safe
ImageTree placement and legacy lookup behavior. RaiDiagram's public manifest,
semantic reconciliation, PlantUML compilation, and SVG provenance APIs are
unchanged.

Current release notes:
[RaiDiagram_RELEASE_NOTES_4.2.4.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/RaiDiagram_RELEASE_NOTES_4.2.4.md).

## 4.2.3

The coordinated `4.2.3` patch carries the accepted CR010/CR009 diagram surface
forward on the CR015 dependency line. `DiagramModel.FromManifest(...)` and
`DiagramDestination.CreateSubscriberRoot()` are public. Typed `.raid`, clean
`.puml`, resolved `_config.puml`, and rendered image artifacts share the
existing subscriber `ItemTreePath` placement without introducing identity
management. Explicit local style locations, deterministic common/diagram-kind
layering, default seeding, and SVG style provenance are supported through the
real PlantUML `-config` path.

Current release notes:
[RaiDiagram_RELEASE_NOTES_4.2.3.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/RaiDiagram_RELEASE_NOTES_4.2.3.md).

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
