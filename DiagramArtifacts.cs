using OsLib;
using RaiImage;

namespace RaiDiagram;

/// <summary>Clean generated PlantUML source stored under an ImageTree subscriber.</summary>
public sealed class PumlSourceFile : ItemTreeTextFile
{
	public PumlSourceFile(ItemTreePath itemPath)
		: base(itemPath ?? throw new ArgumentNullException(nameof(itemPath)), string.Empty, "puml")
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
	{
		ArgumentNullException.ThrowIfNull(itemPath);
		SubscriberRoot = itemPath.RootPath;
		ItemId = itemPath.ItemId;
		Convention = itemPath.Convention;
		RaidManifest = new RaidFile(itemPath);
		PlantUmlSource = new PumlSourceFile(itemPath);
		PlantUmlConfig = new PumlConfigFile(itemPath);
		Svg = new ImageTreeFile(itemPath, ext: "svg");
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

	public RaiPath SubscriberRoot { get; }
	public string ItemId { get; }
	public PathConventionType Convention { get; }
	public RaidFile RaidManifest { get; }
	public PumlSourceFile PlantUmlSource { get; }
	public PumlConfigFile PlantUmlConfig { get; }
	public ImageTreeFile Svg { get; }

	public ImageTreeFile CreateRenderedImage(string ext)
		=> new(new ItemTreePath(SubscriberRoot, ItemId, Convention), ext: ext);
}
