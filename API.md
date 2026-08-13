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

## JSON5 `.raid` files

- <details>
  <summary><code>RaidJson5</code></summary>

  Parses JSON5 or strict JSON, serializes manifests deterministically, and loads or saves them through OsLib-backed <code>RaidFile</code> instances.
  </details>
- <details>
  <summary><code>RaidFile</code></summary>

  Provides the canonical OsLib <code>TextFile</code> wrapper for `.raid` manifests.
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

  Compiles a supported `.raid` semantic graph and presentation options into deterministic PlantUML source and reports unsupported capabilities explicitly.
  </details>
- <details>
  <summary><code>IDiagramRenderer</code> and <code>PlantUmlDiagramRenderer</code></summary>

  Render compiled diagrams through the RaiImage/PlantUML tool boundary into a validated destination without exposing raw stream or filesystem contracts.
  </details>
- <details>
  <summary><code>SvgProvenanceMetadata</code></summary>

  Embeds and reads the authoritative `.raid` identity, schema version, and semantic hash in rendered SVG metadata.
  </details>

## Domain exceptions

- <details>
  <summary><code>RaiDiagramException</code> hierarchy</summary>

  Includes <code>RaidSchemaException</code>, <code>DiagramModelProviderNotFoundException</code>, <code>UnsupportedDiagramConstructException</code>, <code>DiagramRenderingException</code>, and <code>SvgProvenanceException</code>. All inherit from RaiUtils <code>RaiException</code>.
  </details>

The approved cross-product boundary is documented in
[CR009](https://github.com/Burkhardt/RAIkeep/blob/main/doc/CR009_AIA_to_RAIkeep_RaiDiagram_Package.md).
