# RaiDiagram

RaiDiagram is RAIkeep's domain-neutral diagram package. Its `.raid` files are
agent-readable JSON5 manifests that keep semantic projection, model references,
and presentation intent separate from application domain models.

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

See the approved
[CR009 package design](https://github.com/Burkhardt/RAIkeep/blob/main/doc/CR009_AIA_to_RAIkeep_RaiDiagram_Package.md),
the [package-boundary ADR](https://github.com/Burkhardt/RAIkeep/blob/main/doc/ADR001_RaiDiagram_Package_Boundary.md),
and the [foldable API reference](https://github.com/Burkhardt/RaiDiagram/blob/main/API.md).
