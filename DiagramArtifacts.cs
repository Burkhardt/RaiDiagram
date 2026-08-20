using OsLib;
using RaiImage;

namespace RaiDiagram;

/// <summary>Clean generated PlantUML source stored under an ImageTree subscriber.</summary>
public sealed class PumlSourceFile : ImageTreeTextFile
{
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
	public DiagramArtifactSet(
		RaiPath subscriberRoot,
		string itemId,
		PathConventionType convention = PathConventionType.ItemIdTree8x2)
	{
		SubscriberRoot = subscriberRoot ?? throw new ArgumentNullException(nameof(subscriberRoot));
		ItemId = itemId;
		Convention = convention;
		RaidManifest = new RaidFile(subscriberRoot, itemId, convention);
		PlantUmlSource = new PumlSourceFile(subscriberRoot, itemId, convention);
		PlantUmlConfig = new PumlConfigFile(subscriberRoot, itemId, convention);
		Svg = ImageTreeFile.FromItemTree(subscriberRoot, itemId, string.Empty, "svg", convention);
	}

	public RaiPath SubscriberRoot { get; }
	public string ItemId { get; }
	public PathConventionType Convention { get; }
	public RaidFile RaidManifest { get; }
	public PumlSourceFile PlantUmlSource { get; }
	public PumlConfigFile PlantUmlConfig { get; }
	public ImageTreeFile Svg { get; }

	public ImageTreeFile CreateRenderedImage(string ext)
		=> ImageTreeFile.FromItemTree(SubscriberRoot, ItemId, string.Empty, ext, Convention);
}
