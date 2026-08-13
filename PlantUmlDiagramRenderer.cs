using OsLib;
using RaiImage;

namespace RaiDiagram;

public sealed class DiagramDestination
{
	public required RaiPath ImageTreeRoot { get; init; }
	public required string Subscriber { get; init; }
	public required string ItemId { get; init; }
	public PathConventionType Convention { get; init; } = PathConventionType.ItemIdTree8x2;
}

public sealed class DiagramRenderOptions
{
	public PlantUmlCompileOptions PlantUml { get; init; } = new();
	public string? ManifestUri { get; init; }
}

public sealed class DiagramRenderResult
{
	public required ImageTreeFile PlantUmlSource { get; init; }
	public required ImageTreeFile Svg { get; init; }
	public required string Renderer { get; init; }
	public required string SemanticHash { get; init; }
	public required DiagramCapabilityReport Capabilities { get; init; }
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

	public PlantUmlDiagramRenderer(PlantUmlDiagramCompiler? compiler = null)
	{
		this.compiler = compiler ?? new PlantUmlDiagramCompiler();
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
		var compilation = compiler.Compile(diagram.Manifest, options.PlantUml);

		var rendered = await Task.Run(
			() => ImageTreeFile.RenderPlantUml(
				destination.ImageTreeRoot,
				destination.Subscriber,
				destination.ItemId,
				compilation.Source,
				destination.Convention),
			cancellationToken).ConfigureAwait(false);

		var svgText = new TextFile(rendered.Svg.FullName);
		if (!svgText.Exists())
			throw new DiagramRenderingException($"PlantUML did not materialize the expected SVG '{rendered.Svg.FullName}'.");

		var provenance = new SvgProvenance
		{
			RaidId = diagram.Manifest.Diagram.Id,
			SemanticHash = diagram.SemanticHash,
			SchemaVersion = diagram.Manifest.SchemaVersion,
			ManifestUri = options.ManifestUri,
			ModelRevision = diagram.Manifest.Model.CapturedRevision
		};
		var withMetadata = SvgProvenanceMetadata.Embed(svgText.ReadAllText(), provenance);
		svgText.DeleteAll().Append(withMetadata).Save();

		var verified = SvgProvenanceMetadata.Read(svgText.ReadAllText());
		if (!string.Equals(verified.RaidId, provenance.RaidId, StringComparison.Ordinal)
			|| !string.Equals(verified.SemanticHash, provenance.SemanticHash, StringComparison.Ordinal)
			|| !string.Equals(verified.SchemaVersion, provenance.SchemaVersion, StringComparison.Ordinal))
			throw new SvgProvenanceException("The rendered SVG provenance did not verify after persistence.");

		return new DiagramRenderResult
		{
			PlantUmlSource = rendered.Source,
			Svg = rendered.Svg,
			Renderer = Name,
			SemanticHash = diagram.SemanticHash,
			Capabilities = compilation.Capabilities
		};
	}
}
