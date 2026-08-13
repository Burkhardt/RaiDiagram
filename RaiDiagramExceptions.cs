using RaiUtils;

namespace RaiDiagram;

/// <summary>Base exception for RaiDiagram operations.</summary>
public class RaiDiagramException : RaiException
{
	public RaiDiagramException(string message) : base(message) { }
	public RaiDiagramException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Thrown when a .raid manifest cannot be parsed or validated.</summary>
public sealed class RaidSchemaException : RaiDiagramException
{
	public RaidSchemaException(string message) : base(message) { }
	public RaidSchemaException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Thrown when a model provider cannot be resolved for a manifest.</summary>
public sealed class DiagramModelProviderNotFoundException : RaiDiagramException
{
	public DiagramModelProviderNotFoundException(string scheme)
		: base($"No diagram model provider is registered for scheme '{scheme}'.")
	{
		Scheme = scheme;
	}

	public string Scheme { get; }
}

/// <summary>Thrown when a renderer cannot faithfully represent a diagram construct.</summary>
public sealed class UnsupportedDiagramConstructException : RaiDiagramException
{
	public UnsupportedDiagramConstructException(string constructKind)
		: base($"The diagram construct '{constructKind}' is not supported by the selected renderer.")
	{
		ConstructKind = constructKind;
	}

	public string ConstructKind { get; }
}

/// <summary>Thrown when a renderer fails after accepting a diagram.</summary>
public sealed class DiagramRenderingException : RaiDiagramException
{
	public DiagramRenderingException(string message) : base(message) { }
	public DiagramRenderingException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Thrown when SVG provenance metadata is absent, malformed, or inconsistent.</summary>
public sealed class SvgProvenanceException : RaiDiagramException
{
	public SvgProvenanceException(string message) : base(message) { }
	public SvgProvenanceException(string message, Exception innerException) : base(message, innerException) { }
}
