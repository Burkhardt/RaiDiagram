using OsLib;
using RaiImage;

namespace RaiDiagram;

/// <summary>
/// A local ImageTree subscriber location used for style lookup. Subscriber is a
/// storage-routing segment only; it is not an authenticated identity.
/// </summary>
public sealed class DiagramStyleLocation
{
	public DiagramStyleLocation(RaiPath imageTreeRoot, string subscriber)
	{
		ImageTreeRoot = imageTreeRoot ?? throw new ArgumentNullException(nameof(imageTreeRoot));
		Subscriber = ValidateSubscriber(subscriber);
		SubscriberRoot = ImageTreeRoot / new RaiRelPath(Subscriber);
	}

	public RaiPath ImageTreeRoot { get; }
	public string Subscriber { get; }
	public RaiPath SubscriberRoot { get; }
	public string Id => Subscriber;

	private static string ValidateSubscriber(string subscriber)
	{
		if (string.IsNullOrWhiteSpace(subscriber)
			|| subscriber is "." or ".."
			|| subscriber.Contains('/')
			|| subscriber.Contains('\\')
			|| subscriber.Any(char.IsControl))
			throw new ArgumentException("A style subscriber must be one plain ImageTree path segment.", nameof(subscriber));
		return subscriber.Trim();
	}
}

public sealed class DiagramStyleResolutionContext
{
	public required DiagramManifest Manifest { get; init; }
	public PlantUmlCompileOptions Options { get; init; } = new();
	/// <summary>Explicit fallback order, least specific first. No hierarchy is inferred.</summary>
	public IReadOnlyList<DiagramStyleLocation> Locations { get; init; } = [];
	public string? ProfileId { get; init; }
	public bool RequireSubscriberProfile { get; init; }
}

public sealed class DiagramStyleLayer
{
	public required DiagramStyleLocation Location { get; init; }
	public required string Scope { get; init; }
	public required string SourceName { get; init; }
	public required string Content { get; init; }
	public required string ContentHash { get; init; }
	public required int Precedence { get; init; }

	public string ProvenanceId => $"{Location.Subscriber}|{Scope}|{SourceName}|{ContentHash}";
}

public sealed class ResolvedDiagramStyle
{
	public required PumlRenderConfiguration Configuration { get; init; }
	public IReadOnlyList<DiagramStyleLayer> Layers { get; init; } = [];

	public string StyleHash => PumlRenderConfiguration.Sha256Hex(
		string.Join("\n", Layers.OrderBy(layer => layer.Precedence).Select(layer => layer.ProvenanceId)));
}

public interface IDiagramStyleRepository
{
	IReadOnlyList<DiagramStyleLayer> ResolveLayers(DiagramStyleResolutionContext context);
	DiagramStyleLayer? ResolveTheme(DiagramStyleResolutionContext context, string themeName);
}

/// <summary>
/// Resolves local style assets from explicitly ordered ImageTree subscriber
/// locations. It performs no remote lookup, parent traversal, or directory
/// enumeration.
/// </summary>
public sealed class ImageTreeDiagramStyleRepository : IDiagramStyleRepository
{
	private readonly PathConventionType convention;

	public ImageTreeDiagramStyleRepository(
		PathConventionType convention = PathConventionType.ItemIdTree8x2)
	{
		this.convention = convention;
	}

	public IReadOnlyList<DiagramStyleLayer> ResolveLayers(DiagramStyleResolutionContext context)
	{
		ValidateContext(context);
		if (string.IsNullOrWhiteSpace(context.ProfileId))
			return [];

		var layers = new List<DiagramStyleLayer>();
		var precedence = 0;
		foreach (var location in context.Locations)
		{
			AddIfPresent(
				layers,
				location,
				"common",
				PumlStyleFile.FromSubscriberProfile(location.SubscriberRoot, context.ProfileId, "common", convention),
				precedence++);
			AddIfPresent(
				layers,
				location,
				context.Manifest.Diagram.Kind.ToString(),
				PumlStyleFile.FromSubscriberProfile(
					location.SubscriberRoot,
					context.ProfileId,
					context.Manifest.Diagram.Kind.ToString().ToLowerInvariant(),
					convention),
				precedence++);
		}
		return layers;
	}

	public DiagramStyleLayer? ResolveTheme(DiagramStyleResolutionContext context, string themeName)
	{
		ValidateContext(context);
		ArgumentException.ThrowIfNullOrWhiteSpace(themeName);
		if (string.IsNullOrWhiteSpace(context.ProfileId))
			return null;

		for (var index = context.Locations.Count - 1; index >= 0; index--)
		{
			var location = context.Locations[index];
			var file = PumlThemeFile.FromSubscriberProfile(
				location.SubscriberRoot,
				context.ProfileId,
				themeName,
				convention);
			if (!file.Exists())
				continue;
			var content = file.ReadAllText();
			return new DiagramStyleLayer
			{
				Location = location,
				Scope = "theme",
				SourceName = file.NameWithExtension,
				Content = content,
				ContentHash = PumlRenderConfiguration.Sha256Hex(content),
				// A resolved theme is the base layer. Common and diagram-kind
				// subscriber styles always follow it in explicit location order.
				Precedence = -1
			};
		}
		return null;
	}

	private static void AddIfPresent(
		ICollection<DiagramStyleLayer> layers,
		DiagramStyleLocation location,
		string scope,
		PumlStyleFile file,
		int precedence)
	{
		if (!file.Exists())
			return;
		var content = file.ReadAllText();
		layers.Add(new DiagramStyleLayer
		{
			Location = location,
			Scope = scope,
			SourceName = file.NameWithExtension,
			Content = content,
			ContentHash = PumlRenderConfiguration.Sha256Hex(content),
			Precedence = precedence
		});
	}

	private static void ValidateContext(DiagramStyleResolutionContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(context.Manifest);
		var duplicate = context.Locations
			.GroupBy(location => location.Id, StringComparer.Ordinal)
			.FirstOrDefault(group => group.Count() > 1);
		if (duplicate is not null)
			throw new RaidSchemaException($"Duplicate diagram style subscriber '{duplicate.Key}'.");
	}
}

public enum DiagramDefaultSeedBehavior
{
	PreserveExisting,
	ReplaceExisting
}

public sealed class DiagramDefaultSeedResult
{
	public required PumlThemeFile Theme { get; init; }
	public required string ContentHash { get; init; }
	public bool Changed { get; init; }
}

/// <summary>Explicitly seeds public RAIkeep defaults into a local subscriber ImageTree.</summary>
public static class RaiDiagramDefaults
{
	public const string SketchProfileId = "raikeep-sketch";

	public static DiagramDefaultSeedResult SeedTo(
		RaiPath imageTreeRoot,
		string subscriber,
		string profileId = SketchProfileId,
		DiagramDefaultSeedBehavior behavior = DiagramDefaultSeedBehavior.PreserveExisting,
		PathConventionType convention = PathConventionType.ItemIdTree8x2)
	{
		var location = new DiagramStyleLocation(imageTreeRoot, subscriber);
		ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
		var theme = PumlThemeFile.FromSubscriberProfile(
			location.SubscriberRoot,
			profileId,
			profileId,
			convention);
		var source = CreateSketchThemeSource();
		var changed = !theme.Exists() || behavior == DiagramDefaultSeedBehavior.ReplaceExisting;
		if (changed)
			theme.DeleteAll().Append(source).Save();
		return new DiagramDefaultSeedResult
		{
			Theme = theme,
			ContentHash = PumlRenderConfiguration.Sha256Hex(theme.ReadAllText()),
			Changed = changed
		};
	}

	private static string CreateSketchThemeSource()
		=> "!option handwritten true\n<style>\nroot {\n  FontName Chalkduster, Comic Sans MS\n}\n</style>";
}
