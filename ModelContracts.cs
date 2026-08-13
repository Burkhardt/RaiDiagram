using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OsLib;

namespace RaiDiagram;

public enum ModelFactKind
{
	Null,
	Boolean,
	Number,
	String
}

/// <summary>
/// A deliberately small provider-neutral value used for facts relevant to a diagram.
/// Arrays and objects belong in explicit model elements or relationships, not opaque facts.
/// </summary>
public sealed class ModelFactValue : IEquatable<ModelFactValue>
{
	public ModelFactKind Kind { get; set; }
	public string? Text { get; set; }

	public static ModelFactValue Null() => new() { Kind = ModelFactKind.Null };
	public static ModelFactValue Boolean(bool value) => new() { Kind = ModelFactKind.Boolean, Text = value ? "true" : "false" };
	public static ModelFactValue Number(decimal value) => new() { Kind = ModelFactKind.Number, Text = value.ToString(System.Globalization.CultureInfo.InvariantCulture) };
	public static ModelFactValue String(string? value) => value is null ? Null() : new() { Kind = ModelFactKind.String, Text = value };

	public void Validate(string name)
	{
		switch (Kind)
		{
			case ModelFactKind.Null when Text is not null:
				throw new RaidSchemaException($"Fact '{name}' is null but carries text.");
			case ModelFactKind.Boolean when Text is not ("true" or "false"):
				throw new RaidSchemaException($"Fact '{name}' is not a canonical Boolean.");
			case ModelFactKind.Number when !decimal.TryParse(Text,
				System.Globalization.NumberStyles.Number,
				System.Globalization.CultureInfo.InvariantCulture, out _):
				throw new RaidSchemaException($"Fact '{name}' is not a finite decimal number.");
			case ModelFactKind.String when Text is null:
				throw new RaidSchemaException($"Fact '{name}' is a string without a value.");
		}
	}

	internal JToken ToJsonToken() => Kind switch
	{
		ModelFactKind.Null => JValue.CreateNull(),
		ModelFactKind.Boolean => new JValue(Text == "true"),
		ModelFactKind.Number => new JValue(decimal.Parse(Text!, System.Globalization.CultureInfo.InvariantCulture)),
		ModelFactKind.String => new JValue(Text),
		_ => throw new RaidSchemaException($"Unsupported fact value kind '{Kind}'.")
	};

	public bool Equals(ModelFactValue? other)
		=> other is not null && Kind == other.Kind && string.Equals(Text, other.Text, StringComparison.Ordinal);

	public override bool Equals(object? obj) => obj is ModelFactValue other && Equals(other);
	public override int GetHashCode() => HashCode.Combine(Kind, Text);
}

public sealed class ModelElementReference : IEquatable<ModelElementReference>
{
	public string Scheme { get; set; } = string.Empty;
	public string Id { get; set; } = string.Empty;
	public string? Kind { get; set; }
	public string? Revision { get; set; }

	[JsonIgnore]
	public string Key => $"{Scheme}:{Id}";

	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(Scheme))
			throw new RaidSchemaException("A model element reference requires a scheme.");
		if (string.IsNullOrWhiteSpace(Id))
			throw new RaidSchemaException("A model element reference requires an id.");
	}

	public bool Equals(ModelElementReference? other)
		=> other is not null
			&& string.Equals(Scheme, other.Scheme, StringComparison.Ordinal)
			&& string.Equals(Id, other.Id, StringComparison.Ordinal);

	public override bool Equals(object? obj) => obj is ModelElementReference other && Equals(other);
	public override int GetHashCode() => HashCode.Combine(Scheme, Id);
	public override string ToString() => Key;
}

public sealed class DiagramModelIdentity
{
	public string ProviderScheme { get; set; } = string.Empty;
	public string ModelId { get; set; } = string.Empty;
	public string? CapturedRevision { get; set; }

	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(ProviderScheme))
			throw new RaidSchemaException("The diagram model requires a providerScheme.");
		if (string.IsNullOrWhiteSpace(ModelId))
			throw new RaidSchemaException("The diagram model requires a modelId.");
	}
}

public sealed record ModelRevision(string Value);

public sealed class ModelElementSnapshot
{
	public ModelElementReference Reference { get; set; } = new();
	public string Kind { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public Dictionary<string, ModelFactValue> RelevantFacts { get; set; } = new(StringComparer.Ordinal);
	public List<ModelElementReference> Relationships { get; set; } = [];
	public string? SemanticHash { get; set; }
	public string? SourceRevision { get; set; }

	public string GetSemanticHash()
	{
		Reference.Validate();
		foreach (var fact in RelevantFacts)
			fact.Value.Validate(fact.Key);

		if (!string.IsNullOrWhiteSpace(SemanticHash))
			return SemanticHash;

		var facts = new JObject();
		foreach (var fact in RelevantFacts.OrderBy(item => item.Key, StringComparer.Ordinal))
			facts[fact.Key] = fact.Value.ToJsonToken();

		var relationships = new JArray(Relationships
			.OrderBy(item => item.Key, StringComparer.Ordinal)
			.Select(item => item.Key));

		var token = new JObject
		{
			["reference"] = Reference.Key,
			["kind"] = Kind,
			["displayName"] = DisplayName,
			["facts"] = facts,
			["relationships"] = relationships
		};
		return CanonicalJson.CanonicalizeWithHash(token).Sha256;
	}
}

public sealed class DiagramSelectionRule
{
	public string Id { get; set; } = string.Empty;
	public string Query { get; set; } = string.Empty;
	public Dictionary<string, ModelFactValue> Parameters { get; set; } = new(StringComparer.Ordinal);

	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(Id))
			throw new RaidSchemaException("A diagram selection rule requires an id.");
		if (string.IsNullOrWhiteSpace(Query))
			throw new RaidSchemaException($"Selection rule '{Id}' requires a query.");
		foreach (var parameter in Parameters)
			parameter.Value.Validate(parameter.Key);
	}
}

public interface IDiagramModelProvider
{
	string Scheme { get; }

	ValueTask<ModelRevision> GetRevisionAsync(
		DiagramModelIdentity model,
		CancellationToken cancellationToken = default);

	IAsyncEnumerable<ModelElementSnapshot> ResolveAsync(
		DiagramModelIdentity model,
		IReadOnlyCollection<ModelElementReference> references,
		CancellationToken cancellationToken = default);

	IAsyncEnumerable<ModelElementSnapshot> QueryAsync(
		DiagramModelIdentity model,
		DiagramSelectionRule selectionRule,
		CancellationToken cancellationToken = default);
}

public sealed class DiagramModelProviderRegistry
{
	private readonly IReadOnlyDictionary<string, IDiagramModelProvider> providers;

	public DiagramModelProviderRegistry(IEnumerable<IDiagramModelProvider> providers)
	{
		this.providers = (providers ?? throw new ArgumentNullException(nameof(providers)))
			.ToDictionary(provider => provider.Scheme, StringComparer.Ordinal);
	}

	public bool TryGet(string scheme, out IDiagramModelProvider provider)
		=> providers.TryGetValue(scheme, out provider!);

	public IDiagramModelProvider GetRequired(string scheme)
		=> TryGet(scheme, out var provider)
			? provider
			: throw new DiagramModelProviderNotFoundException(scheme);
}
