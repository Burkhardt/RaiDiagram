using OsLib;
using RaiImage;

namespace RaiDiagram;

/// <summary>Clean generated PlantUML source stored under an ImageTree subscriber.</summary>
public sealed class PumlSourceFile : ItemTreeTextFile
{
	public PumlSourceFile(ItemTreePath itemPath)
		: this(itemPath, string.Empty)
	{
	}

	public PumlSourceFile(ItemTreePath itemPath, string nameExt)
		: this(itemPath, ItemTreeTextFile.NoItemNumber, nameExt)
	{
	}

	public PumlSourceFile(ItemTreePath itemPath, int itemNumber, string nameExt)
		: base(itemPath ?? throw new ArgumentNullException(nameof(itemPath)), itemNumber, nameExt, "puml")
	{
	}

	public PumlSourceFile(
		RaiPath subscriberRoot,
		string itemId,
		PathConventionType convention = PathConventionType.ItemIdTree8x2)
		: base(subscriberRoot, itemId, string.Empty, "puml", convention)
	{
	}

	public PumlSourceFile(RaiPath path, string name) : base(path, name, string.Empty, "puml")
	{
	}

	public PumlSourceFile Write(string source)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(source);
		DeleteAll().Append(source).Save();
		return this;
	}
}

/// <summary>All authoritative and derived files for one subscriber-local diagram render.</summary>
public sealed class DiagramArtifactSet
{
	public DiagramArtifactSet(ItemTreePath itemPath)
		: this(itemPath, string.Empty)
	{
	}

	public DiagramArtifactSet(ItemTreePath itemPath, string nameExt)
		: this(itemPath, ItemTreeTextFile.NoItemNumber, nameExt)
	{
	}

	public DiagramArtifactSet(ItemTreePath itemPath, int itemNumber, string nameExt)
	{
		ArgumentNullException.ThrowIfNull(itemPath);
		SubscriberRoot = itemPath.RootPath;
		ItemId = itemPath.ItemId;
		ItemNumber = itemNumber;
		NameExt = nameExt ?? throw new ArgumentNullException(nameof(nameExt));
		Convention = itemPath.Convention;
		RaidManifest = new RaidFile(itemPath, ItemNumber, NameExt);
		PlantUmlSource = new PumlSourceFile(itemPath, ItemNumber, NameExt);
		PlantUmlConfig = new PumlConfigFile(itemPath, ItemNumber, NameExt);
		Svg = new ImageTreeFile(itemPath, NameExt, "svg", ImageNamingConvention.Structured);
		if (ItemNumber != ItemTreeTextFile.NoItemNumber)
			Svg.ImageNumber = ItemNumber;
	}

	public DiagramArtifactSet(
		RaiPath subscriberRoot,
		string itemId,
		PathConventionType convention = PathConventionType.ItemIdTree8x2)
		: this(new ItemTreePath(
			subscriberRoot ?? throw new ArgumentNullException(nameof(subscriberRoot)),
			itemId,
			convention))
	{
	}

	/// <summary>Create an artifact set from the explicit ImageTree root and subscriber segment.</summary>
	public DiagramArtifactSet(
		RaiPath imageTreeRoot,
		string subscriber,
		string itemId,
		PathConventionType convention = PathConventionType.ItemIdTree8x2)
		: this(new ItemTreePath(
			imageTreeRoot ?? throw new ArgumentNullException(nameof(imageTreeRoot)),
			subscriber,
			itemId,
			convention))
	{
	}

	/// <summary>Create a named diagram artifact set under an explicit subscriber.</summary>
	public DiagramArtifactSet(
		RaiPath imageTreeRoot,
		string subscriber,
		string itemId,
		string nameExt,
		int itemNumber = ItemTreeTextFile.NoItemNumber,
		PathConventionType convention = PathConventionType.ItemIdTree8x2)
		: this(new ItemTreePath(
			imageTreeRoot ?? throw new ArgumentNullException(nameof(imageTreeRoot)),
			subscriber,
			itemId,
			convention), itemNumber, nameExt)
	{
	}

	public RaiPath SubscriberRoot { get; }
	public string ItemId { get; }
	public int ItemNumber { get; }
	public string NameExt { get; }
	public string DiagramId
	{
		get
		{
			var stem = ItemNumber == ItemTreeTextFile.NoItemNumber ? ItemId : $"{ItemId}_{ItemNumber:D2}";
			return string.IsNullOrEmpty(NameExt) ? stem : $"{stem}_{NameExt}";
		}
	}
	public PathConventionType Convention { get; }
	public RaidFile RaidManifest { get; }
	public PumlSourceFile PlantUmlSource { get; }
	public PumlConfigFile PlantUmlConfig { get; }
	public ImageTreeFile Svg { get; }

	/// <summary>
	/// Persist the authoritative manifest and its deterministic PlantUML source as
	/// co-located ItemTree siblings. Each file is written directly at its final
	/// cloud path through the existing RaiFile boundary.
	/// </summary>
	public DiagramArtifactSet Write(
		DiagramManifest manifest,
		PlantUmlDiagramCompiler? compiler = null)
	{
		ArgumentNullException.ThrowIfNull(manifest);
		EnsureManifestIdentity(manifest);
		var compilation = (compiler ?? new PlantUmlDiagramCompiler()).Compile(manifest);
		RaidManifest.SaveManifest(manifest);
		PlantUmlSource.Write(compilation.Source);
		return this;
	}

	/// <summary>
	/// Persist SVG returned by a browser or another renderer beside its authoritative
	/// .raid/.puml siblings and embed the CR009 identity and semantic hash.
	/// </summary>
	public ImageTreeFile SaveClientSvg(
		DiagramManifest manifest,
		string svg,
		string? manifestUri = null)
	{
		ArgumentNullException.ThrowIfNull(manifest);
		ArgumentException.ThrowIfNullOrWhiteSpace(svg);
		EnsureManifestIdentity(manifest);
		PlantUmlSvgDiagnostics.ThrowIfErrorDocument(svg);
		var withMetadata = SvgProvenanceMetadata.Embed(svg, new SvgProvenance
		{
			RaidId = manifest.Diagram.Id,
			SemanticHash = DiagramSemanticHasher.Compute(manifest),
			SchemaVersion = manifest.SchemaVersion,
			ManifestUri = manifestUri,
			ModelRevision = manifest.Model.CapturedRevision
		});
		new TextFile(Svg.FullName).DeleteAll().Append(withMetadata).Save();
		return Svg;
	}

	public ImageTreeFile CreateRenderedImage(string ext)
	{
		var image = new ImageTreeFile(
			new ItemTreePath(SubscriberRoot, ItemId, Convention),
			NameExt,
			ext,
			ImageNamingConvention.Structured);
		if (ItemNumber != ItemTreeTextFile.NoItemNumber)
			image.ImageNumber = ItemNumber;
		return image;
	}

	private void EnsureManifestIdentity(DiagramManifest manifest)
	{
		manifest.Validate();
		if (!string.Equals(DiagramId, manifest.Diagram.Id, StringComparison.Ordinal))
			throw new RaidSchemaException(
				$"Artifact identity '{DiagramId}' does not match manifest diagram id '{manifest.Diagram.Id}'.");
	}
}
