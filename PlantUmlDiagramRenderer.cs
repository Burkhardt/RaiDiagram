using OsLib;
using RaiImage;

namespace RaiDiagram;

public sealed class DiagramDestination
{
	public required RaiPath ImageTreeRoot { get; init; }
	/// <summary>ImageTree storage-routing segment; not an authenticated identity.</summary>
	public required string Subscriber { get; init; }
	public required string ItemId { get; init; }
	public PathConventionType Convention { get; init; } = PathConventionType.ItemIdTree8x2;

	public RaiPath CreateSubscriberRoot()
	{
		if (string.IsNullOrWhiteSpace(Subscriber)
			|| Subscriber is "." or ".."
			|| Subscriber.Contains('/')
			|| Subscriber.Contains('\\')
			|| Subscriber.Any(char.IsControl))
			throw new ArgumentException("DiagramDestination.Subscriber must be one plain ImageTree path segment.");
		return ImageTreeRoot / new RaiRelPath(Subscriber);
	}
}

public sealed class DiagramRenderOptions
{
	public PlantUmlCompileOptions PlantUml { get; init; } = new();
	public string? ManifestUri { get; init; }
	public string? StyleProfileId { get; init; }
	/// <summary>Explicit fallback order, least specific first. No hierarchy is inferred.</summary>
	public IReadOnlyList<DiagramStyleLocation> StyleLocations { get; init; } = [];
	public IDiagramStyleRepository? StyleRepository { get; init; }
	public bool RequireSubscriberStyleProfile { get; init; }
}

public sealed class DiagramRenderResult
{
	public ImageTreeFile RaidManifest { get; init; } = null!;
	public required ImageTreeFile PlantUmlSource { get; init; }
	public ImageTreeFile PlantUmlConfig { get; init; } = null!;
	public required ImageTreeFile Svg { get; init; }
	public required string Renderer { get; init; }
	public required string SemanticHash { get; init; }
	public string ConfigHash { get; init; } = string.Empty;
	public string RenderHash { get; init; } = string.Empty;
	public required DiagramCapabilityReport Capabilities { get; init; }
	public DiagramArtifactSet Artifacts { get; init; } = null!;
	public string StyleHash { get; init; } = string.Empty;
	public IReadOnlyList<string> StyleLayers { get; init; } = [];
}

public interface IDiagramRenderer
{
	string Name { get; }
	DiagramCapabilityReport Validate(DiagramModel diagram, DiagramRenderOptions? options = null);
	Task<DiagramRenderResult> RenderAsync(
		DiagramModel diagram,
		DiagramDestination destination,
		DiagramRenderOptions? options = null,
		CancellationToken cancellationToken = default);
}

public sealed class PlantUmlDiagramRenderer : IDiagramRenderer
{
	private readonly PlantUmlDiagramCompiler compiler;
	private readonly IDiagramStyleProvider? styleProvider;

	public PlantUmlDiagramRenderer(
		PlantUmlDiagramCompiler? compiler = null,
		IDiagramStyleProvider? styleProvider = null)
	{
		this.compiler = compiler ?? new PlantUmlDiagramCompiler();
		this.styleProvider = styleProvider;
	}

	public string Name => "PlantUML";

	public DiagramCapabilityReport Validate(DiagramModel diagram, DiagramRenderOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(diagram);
		return compiler.Validate(diagram.Manifest);
	}

	public async Task<DiagramRenderResult> RenderAsync(
		DiagramModel diagram,
		DiagramDestination destination,
		DiagramRenderOptions? options = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(diagram);
		ArgumentNullException.ThrowIfNull(destination);
		options ??= new DiagramRenderOptions();
		var subscriberRoot = destination.CreateSubscriberRoot();
		var artifacts = new DiagramArtifactSet(subscriberRoot, destination.ItemId, destination.Convention);
		var compilation = compiler.Compile(diagram.Manifest, options.PlantUml);
		var repository = options.StyleRepository
			?? new ImageTreeDiagramStyleRepository(destination.Convention);
		var effectiveProvider = styleProvider
			?? new DiagramStyleProvider(profileId: options.StyleProfileId, repository: repository);
		var styleLocations = options.StyleLocations.Count == 0
			? new[] { new DiagramStyleLocation(destination.ImageTreeRoot, destination.Subscriber) }
			: options.StyleLocations;
		var resolvedStyle = effectiveProvider.Resolve(new DiagramStyleResolutionContext
		{
			Manifest = diagram.Manifest,
			Options = options.PlantUml,
			Locations = styleLocations,
			ProfileId = options.StyleProfileId,
			RequireSubscriberProfile = options.RequireSubscriberStyleProfile || options.StyleProfileId is not null
		});
		var configuration = resolvedStyle.Configuration;
		var configSource = configuration.ToPlantUml();
		var configHash = configuration.ContentHash;
		var renderHash = configuration.ComputeRenderHash(compilation.Source);
		var styleHash = resolvedStyle.StyleHash;
		var styleLayers = resolvedStyle.Layers.Select(layer => layer.ProvenanceId).ToArray();

		var rendered = await Task.Run(
			() => ImageTreeFile.RenderPlantUmlAtSubscriber(
				subscriberRoot,
				destination.ItemId,
				compilation.Source,
				configSource,
				destination.Convention),
			cancellationToken).ConfigureAwait(false);
		artifacts.RaidManifest.SaveManifest(diagram.Manifest);

		var svgText = new TextFile(rendered.Svg.FullName);
		if (!svgText.Exists())
			throw new DiagramRenderingException($"PlantUML did not materialize the expected SVG '{rendered.Svg.FullName}'.");

		var provenance = new SvgProvenance
		{
			RaidId = diagram.Manifest.Diagram.Id,
			SemanticHash = diagram.SemanticHash,
			SchemaVersion = diagram.Manifest.SchemaVersion,
			ManifestUri = options.ManifestUri,
			ModelRevision = diagram.Manifest.Model.CapturedRevision,
			ConfigHash = configHash,
			RenderHash = renderHash,
			StyleHash = styleHash,
			StyleLayers = styleLayers
		};
		var withMetadata = SvgProvenanceMetadata.Embed(svgText.ReadAllText(), provenance);
		svgText.DeleteAll().Append(withMetadata).Save();

		var verified = SvgProvenanceMetadata.Read(svgText.ReadAllText());
		if (!string.Equals(verified.RaidId, provenance.RaidId, StringComparison.Ordinal)
			|| !string.Equals(verified.SemanticHash, provenance.SemanticHash, StringComparison.Ordinal)
			|| !string.Equals(verified.SchemaVersion, provenance.SchemaVersion, StringComparison.Ordinal)
			|| !string.Equals(verified.ConfigHash, provenance.ConfigHash, StringComparison.Ordinal)
			|| !string.Equals(verified.RenderHash, provenance.RenderHash, StringComparison.Ordinal)
			|| !string.Equals(verified.StyleHash, provenance.StyleHash, StringComparison.Ordinal)
			|| !verified.StyleLayers.SequenceEqual(provenance.StyleLayers, StringComparer.Ordinal))
			throw new SvgProvenanceException("The rendered SVG provenance did not verify after persistence.");

		return new DiagramRenderResult
		{
			RaidManifest = ImageTreeFile.FromItemTree(
				subscriberRoot,
				destination.ItemId,
				string.Empty,
				"raid",
				destination.Convention),
			PlantUmlSource = rendered.Source,
			PlantUmlConfig = rendered.Config
				?? throw new DiagramRenderingException("PlantUML did not persist the resolved configuration."),
			Svg = rendered.Svg,
			Renderer = Name,
			SemanticHash = diagram.SemanticHash,
			ConfigHash = configHash,
			RenderHash = renderHash,
			Capabilities = compilation.Capabilities,
			Artifacts = artifacts,
			StyleHash = styleHash,
			StyleLayers = styleLayers
		};
	}
}
