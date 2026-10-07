namespace RaiDiagram.Builders;

/// <summary>Deployment topology with explicit host/resident containment and communication links.</summary>
public sealed class DistributionDiagramBuilder : DiagramBuilder
{
	public DistributionDiagramBuilder(string baseItemId, DiagramModelIdentity model)
		: base(baseItemId, DiagramArchetype.Distribution, DiagramKind.Deployment, model)
		=> Manifest.SchemaVersion = DiagramManifest.ExtendedSchemaVersion;

	public DistributionDiagramBuilder AddNode(string id, string name, string? parentId = null)
		=> Add(id, name, DiagramElementKinds.Node, parentId);
	public DistributionDiagramBuilder AddCloud(string id, string name, string? parentId = null)
		=> Add(id, name, DiagramElementKinds.Cloud, parentId);
	public DistributionDiagramBuilder AddComponent(string id, string name, string? hostId = null)
		=> Add(id, name, DiagramElementKinds.Component, hostId);
	public DistributionDiagramBuilder AddDatabase(string id, string name, string? hostId = null)
		=> Add(id, name, DiagramElementKinds.Database, hostId);
	public DistributionDiagramBuilder AddCommunication(string sourceId, string targetId, string protocol)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(protocol);
		AddRelationship(DiagramRelationshipKinds.Association, sourceId, targetId, $"<<{protocol}>>");
		return this;
	}
	public DiagramManifest BuildManifest() => BuildSnapshot();
	private DistributionDiagramBuilder Add(string id, string name, string kind, string? parent)
	{
		AddElement(id, kind, name, parent);
		return this;
	}
}
