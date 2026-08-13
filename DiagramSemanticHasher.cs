using Newtonsoft.Json.Linq;
using OsLib;

namespace RaiDiagram;

public static class DiagramSemanticHasher
{
	public static string Compute(DiagramManifest manifest)
	{
		ArgumentNullException.ThrowIfNull(manifest);
		manifest.Validate();

		var semantic = new JObject
		{
			["schemaVersion"] = manifest.SchemaVersion,
			["diagram"] = new JObject
			{
				["id"] = manifest.Diagram.Id,
				["kind"] = manifest.Diagram.Kind.ToString(),
				["purpose"] = manifest.Diagram.Purpose
			},
			["model"] = new JObject
			{
				["providerScheme"] = manifest.Model.ProviderScheme,
				["modelId"] = manifest.Model.ModelId
			},
			["projection"] = CreateProjection(manifest.Projection),
			["annotations"] = new JArray(manifest.Annotations
				.Where(item => item.Semantic)
				.OrderBy(item => item.Id, StringComparer.Ordinal)
				.Select(item => new JObject
				{
					["id"] = item.Id,
					["text"] = item.Text,
					["elementId"] = item.ElementId
				}))
		};

		return CanonicalJson.CanonicalizeWithHash(semantic).Sha256;
	}

	public static string ComputePresentation(DiagramManifest manifest)
	{
		ArgumentNullException.ThrowIfNull(manifest);
		var token = JObject.FromObject(manifest.Presentation);
		return CanonicalJson.CanonicalizeWithHash(token).Sha256;
	}

	private static JObject CreateProjection(DiagramProjection projection)
	{
		return new JObject
		{
			["elements"] = new JArray(projection.Elements
				.OrderBy(item => item.Id, StringComparer.Ordinal)
				.Select(CreateElement)),
			["relationships"] = new JArray(projection.Relationships
				.OrderBy(item => item.Id, StringComparer.Ordinal)
				.Select(item => new JObject
				{
					["id"] = item.Id,
					["kind"] = item.Kind,
					["sourceId"] = item.SourceId,
					["targetId"] = item.TargetId,
					["label"] = item.Label,
					["cardinality"] = item.Cardinality,
					["guard"] = item.Guard,
					["sourceReference"] = CreateReference(item.SourceReference)
				})),
			["selectionRules"] = new JArray(projection.SelectionRules
				.OrderBy(item => item.Id, StringComparer.Ordinal)
				.Select(item => new JObject
				{
					["id"] = item.Id,
					["query"] = item.Query,
					["parameters"] = CreateFacts(item.Parameters)
				}))
		};
	}

	private static JObject CreateElement(DiagramElement item)
	{
		return new JObject
		{
			["id"] = item.Id,
			["kind"] = item.Kind,
			["displayName"] = item.DisplayName,
			["namespace"] = item.Namespace,
			["description"] = item.Description,
			["parentId"] = item.ParentId,
			["source"] = CreateReference(item.Source),
			["relevantFacts"] = CreateFacts(item.RelevantFacts),
			["sourceRelationships"] = new JArray(item.SourceRelationships
				.OrderBy(reference => reference.Key, StringComparer.Ordinal)
				.Select(CreateReference)),
			["sourceSemanticHash"] = item.SourceSemanticHash,
			["selectedBy"] = new JArray(item.SelectedBy.OrderBy(id => id, StringComparer.Ordinal))
		};
	}

	private static JObject CreateFacts(IReadOnlyDictionary<string, ModelFactValue> facts)
	{
		var result = new JObject();
		foreach (var fact in facts.OrderBy(item => item.Key, StringComparer.Ordinal))
			result[fact.Key] = fact.Value.ToJsonToken();
		return result;
	}

	private static JToken CreateReference(ModelElementReference? reference)
		=> reference is null
			? JValue.CreateNull()
			: new JObject
			{
				["scheme"] = reference.Scheme,
				["id"] = reference.Id,
				["kind"] = reference.Kind
			};
}
