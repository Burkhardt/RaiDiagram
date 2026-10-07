namespace RaiDiagram;

public sealed record PlantUmlDiagnostic(string Code, string Source, int Line, int Column, string Message)
{
	public override string ToString() => $"{Source}:{Line}:{Column} [{Code}] {Message}";
}

public sealed class PlantUmlImportException : RaiDiagramException
{
	public PlantUmlImportException(PlantUmlDiagnostic diagnostic) : base(diagnostic.ToString())
		=> Diagnostic = diagnostic;
	public PlantUmlDiagnostic Diagnostic { get; }
}
