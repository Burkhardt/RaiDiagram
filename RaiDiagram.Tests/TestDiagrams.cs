namespace RaiDiagram.Tests;

internal static class TestDiagrams
{
	internal static DiagramModel CreateUseCaseModel()
	{
		var draft = DiagramDraft.Create(
			"ScheduleRehearsal",
			"Schedule rehearsal",
			DiagramKind.UseCase,
			new DiagramModelIdentity
			{
				ProviderScheme = "test-model",
				ModelId = "theatre",
				CapturedRevision = "r1"
			});
		var boundary = draft.AddFrame("scheduling", "Scheduling system");
		var role = draft.AddRole("band-manager", "Band Manager");
		var useCase = draft.AddUseCase("schedule", "Schedule rehearsal", boundary.Id);
		role.Source = Reference("role/band-manager", DiagramElementKinds.Role);
		role.RelevantFacts["active"] = ModelFactValue.Boolean(true);
		role.SourceRelationships.Add(Reference("usecase/schedule", DiagramElementKinds.UseCase));
		role.SourceSemanticHash = Snapshot(role).GetSemanticHash();
		useCase.Source = Reference("usecase/schedule", DiagramElementKinds.UseCase);
		useCase.SourceSemanticHash = Snapshot(useCase).GetSemanticHash();
		draft.ConnectRoleToUseCase(role, useCase, "schedules");
		return draft.ValidateAndFreeze();
	}

	internal static ModelElementReference Reference(string id, string? kind = null)
		=> new() { Scheme = "test-model", Id = id, Kind = kind };

	internal static ModelElementSnapshot Snapshot(DiagramElement element)
		=> new()
		{
			Reference = element.Source!,
			Kind = element.Source?.Kind ?? element.Kind,
			DisplayName = element.DisplayName,
			RelevantFacts = element.RelevantFacts.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal),
			Relationships = element.SourceRelationships.ToList()
		};
}
