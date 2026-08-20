# RaiDiagram API Reference

This document provides a foldable overview of the public RaiDiagram 4.2 API.

## Manifest and semantic model

- <details>
  <summary><code>DiagramManifest</code>, <code>DiagramIdentity</code>, and <code>DiagramModel</code></summary>

  Represent the authoritative `.raid` document, stable diagram identity, model binding, semantic projection, presentation intent, and annotations.
  </details>
- <details>
  <summary><code>DiagramElement</code>, <code>DiagramRelationship</code>, and projection types</summary>

  Describe role-first UML26 elements, typed relationships, selection rules, frames, and other view content without importing application-domain types.
  </details>
- <details>
  <summary><code>DiagramDraft.Create(...)</code></summary>

  Creates a valid initial diagram manifest from identity, model, kind, and title inputs.
  </details>
- <details>
  <summary><code>DiagramModel.FromManifest(...)</code></summary>

  Validates and snapshots a loaded authoritative manifest as the immutable model required by compilation, reconciliation, and rendering.
  </details>

## JSON5 `.raid` files

- <details>
  <summary><code>RaidJson5</code></summary>

  Parses JSON5 or strict JSON, serializes manifests deterministically, and loads or saves them through OsLib-backed <code>RaidFile</code> instances.
  </details>
- <details>
  <summary><code>RaidFile</code></summary>

  Provides the canonical OsLib <code>TextFile</code> wrapper for `.raid` manifests, including <code>LoadModel()</code> for direct rendering of an authoritative file.
  </details>
- <details>
  <summary><code>DiagramSemanticHasher</code></summary>

  Computes deterministic semantic and presentation hashes while keeping presentation-only changes outside semantic identity.
  </details>

## Model providers and reconciliation

- <details>
  <summary><code>IDiagramModelProvider</code> and <code>DiagramModelProviderRegistry</code></summary>

  Let an application expose read-only, provider-neutral model snapshots selected by scheme, including dependency-injection registration and lookup.
  </details>
- <details>
  <summary><code>ModelElementReference</code>, <code>ModelElementSnapshot</code>, and <code>ModelFactValue</code></summary>

  Carry stable model identities and a deliberately constrained, serializable fact model suitable for agents and deterministic comparisons.
  </details>
- <details>
  <summary><code>DiagramReconciler.ReconcileAsync(...)</code> and <code>DiagramModelDiff</code></summary>

  Compare a captured projection with a live provider using reference or projection reconciliation and return structured changes, unresolved references, and revision data.
  </details>

## PlantUML compilation and rendering

- <details>
  <summary><code>PlantUmlDiagramCompiler</code></summary>

  Compiles a supported `.raid` semantic graph into clean, deterministic PlantUML diagram declarations. Theme and style configuration is deliberately excluded from this source and supplied through PlantUML `-config` at render time.
  </details>
- <details>
  <summary><code>PumlStyleSheet</code>, <code>PumlStyleCatalog</code>, and <code>DiagramStyleProvider</code></summary>

  Construct validated CSS-like PlantUML rules programmatically, layer common and `DiagramKind`-specific styles, snapshot subscriber-local style files, and resolve one deterministic render configuration.
  </details>
- <details>
  <summary><code>DiagramArtifactSet</code>, <code>RaidFile</code>, and <code>PumlSourceFile</code></summary>

  Give the authoritative manifest and generated text truthful file types while placing them in the same subscriber <code>ItemTreePath</code> bucket as rendered SVG, PNG, or WebP images.
  </details>
- <details>
  <summary><code>PumlConfigFile</code> and <code>PumlStyleFile</code></summary>

  Persist resolved config or reusable style content as ordinary files or under an existing ImageTree subscriber using `ItemTreePath`. `PumlConfigFile` uses `NameExt = "config"` and `Ext = "puml"`.
  </details>
- <details>
  <summary><code>PumlThemeFile</code></summary>

  A PlantUML-specific `ImageTreeTextFile` that validates the theme identifier, maps it to PlantUML's required `puml-theme-&lt;name&gt;.puml` filename, and can be materialized under an ImageTree subscriber.
  </details>
- <details>
  <summary><code>DiagramStyleLocation</code>, <code>IDiagramStyleRepository</code>, and <code>ImageTreeDiagramStyleRepository</code></summary>

  Resolve local style artifacts from a caller-supplied least-to-most-specific list of subscriber locations. Subscriber is only an ImageTree routing segment; RaiDiagram performs no identity management, authorization, parent traversal, or organizational inference.
  </details>
- <details>
  <summary><code>IDiagramRenderer</code> and <code>PlantUmlDiagramRenderer</code></summary>

  Resolve configuration from explicit subscriber locations, persist sibling `.raid`, `.puml`, `_config.puml`, and `.svg` ImageTree artifacts under one subscriber, and invoke PlantUML with `-config` without exposing raw stream contracts.
  </details>
- <details>
  <summary><code>SvgProvenanceMetadata</code></summary>

  Embeds and reads the authoritative `.raid` identity, schema version, semantic hash, subscriber-style hash and layer provenance, resolved-config hash, and complete render hash in rendered SVG metadata.
  </details>

## Domain exceptions

- <details>
  <summary><code>RaiDiagramException</code> hierarchy</summary>

  Includes <code>RaidSchemaException</code>, <code>DiagramModelProviderNotFoundException</code>, <code>UnsupportedDiagramConstructException</code>, <code>DiagramRenderingException</code>, and <code>SvgProvenanceException</code>. All inherit from RaiUtils <code>RaiException</code>.
  </details>

The approved cross-product boundary is documented in
[CR009](https://github.com/Burkhardt/RAIkeep/blob/main/doc/CR009_AIA_to_RAIkeep_RaiDiagram_Package.md).
Subscriber-scoped artifact placement and deterministic local style lookup are
specified by
[CR010](https://github.com/Burkhardt/RAIkeep/blob/main/doc/CR010_AfricaStage_to_RAIkeep_RaiDiagram_Subscriber_Scoped_Artifacts_and_Styles.md)
and
[ADR002](https://github.com/Burkhardt/RAIkeep/blob/main/doc/ADR002_RaiDiagram_Subscriber_Scoped_Artifacts_and_Style_Lookup.md).
