namespace RaiDiagram;

public enum DiagramReconciliationMode
{
	Reference,
	Projection
}

public enum DiagramModelState
{
	Current,
	Stale,
	ProviderUnavailable,
	ModelUnavailable,
	Indeterminate
}

public enum DiagramModelChangeKind
{
	Added,
	Removed,
	Changed,
	NewlyInScope,
	NoLongerInScope,
	RelationshipChanged,
	Unresolved,
	Inaccessible
}

public sealed class DiagramModelChange
{
	public DiagramModelChangeKind Kind { get; init; }
	public ModelElementReference? Reference { get; init; }
	public string Summary { get; init; } = string.Empty;
	public string? DiagramElementId { get; init; }
	public string? SelectionRuleId { get; init; }
}

public sealed class DiagramModelDiff
{
	public string? CapturedRevision { get; init; }
	public string? CurrentRevision { get; init; }
	public DiagramReconciliationMode Mode { get; init; }
	public DiagramModelState State { get; init; }
	public bool PresentationOnlyChangesIgnored { get; init; } = true;
	public IReadOnlyList<DiagramModelChange> Changes { get; init; } = [];

	public string ToAgentText()
	{
		var lines = new List<string>
		{
			$"Diagram model state: {State}.",
			$"Reconciliation mode: {Mode}.",
			$"Captured revision: {CapturedRevision ?? "unknown"}; current revision: {CurrentRevision ?? "unknown"}."
		};

		if (Changes.Count == 0)
			lines.Add("No semantic model differences were found.");
		else
			foreach (var change in Changes
				.OrderBy(item => item.Kind)
				.ThenBy(item => item.Reference?.Key, StringComparer.Ordinal)
				.ThenBy(item => item.DiagramElementId, StringComparer.Ordinal))
				lines.Add($"- {change.Kind}: {change.Summary}");

		lines.Add("Presentation-only details were excluded from this comparison.");
		return string.Join(Environment.NewLine, lines);
	}

	public static DiagramModelDiff ProviderUnavailable(
		DiagramManifest manifest,
		DiagramReconciliationMode mode)
		=> new()
		{
			CapturedRevision = manifest.Model.CapturedRevision,
			Mode = mode,
			State = DiagramModelState.ProviderUnavailable
		};
}

public sealed class DiagramReconciler
{
	public async Task<DiagramModelDiff> ReconcileAsync(
		DiagramManifest manifest,
		DiagramModelProviderRegistry registry,
		DiagramReconciliationMode mode = DiagramReconciliationMode.Reference,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(registry);
		if (!registry.TryGet(manifest.Model.ProviderScheme, out var provider))
			return DiagramModelDiff.ProviderUnavailable(manifest, mode);
		return await ReconcileAsync(manifest, provider, mode, cancellationToken).ConfigureAwait(false);
	}

	public async Task<DiagramModelDiff> ReconcileAsync(
		DiagramManifest manifest,
		IDiagramModelProvider provider,
		DiagramReconciliationMode mode = DiagramReconciliationMode.Reference,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(manifest);
		ArgumentNullException.ThrowIfNull(provider);
		manifest.Validate();
		if (!string.Equals(provider.Scheme, manifest.Model.ProviderScheme, StringComparison.Ordinal))
			throw new ArgumentException(
				$"Provider scheme '{provider.Scheme}' does not match manifest scheme '{manifest.Model.ProviderScheme}'.",
				nameof(provider));

		ModelRevision revision;
		try
		{
			revision = await provider.GetRevisionAsync(manifest.Model, cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception exception)
		{
			return new DiagramModelDiff
			{
				CapturedRevision = manifest.Model.CapturedRevision,
				Mode = mode,
				State = DiagramModelState.ModelUnavailable,
				Changes =
				[
					new DiagramModelChange
					{
						Kind = DiagramModelChangeKind.Inaccessible,
						Summary = $"The model could not be read: {exception.Message}"
					}
				]
			};
		}

		var changes = new List<DiagramModelChange>();
		var indeterminate = false;
		try
		{
			await ReconcileReferencesAsync(manifest, provider, changes, cancellationToken).ConfigureAwait(false);
			if (mode == DiagramReconciliationMode.Projection)
				await ReconcileProjectionAsync(manifest, provider, changes, cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception exception)
		{
			indeterminate = true;
			changes.Add(new DiagramModelChange
			{
				Kind = DiagramModelChangeKind.Inaccessible,
				Summary = $"Model reconciliation could not complete: {exception.Message}"
			});
		}

		return new DiagramModelDiff
		{
			CapturedRevision = manifest.Model.CapturedRevision,
			CurrentRevision = revision.Value,
			Mode = mode,
			State = indeterminate
				? DiagramModelState.Indeterminate
				: changes.Count == 0 ? DiagramModelState.Current : DiagramModelState.Stale,
			Changes = changes
		};
	}

	private static async Task ReconcileReferencesAsync(
		DiagramManifest manifest,
		IDiagramModelProvider provider,
		List<DiagramModelChange> changes,
		CancellationToken cancellationToken)
	{
		var captured = manifest.Projection.Elements
			.Where(item => item.Source is not null)
			.ToDictionary(item => item.Source!.Key, StringComparer.Ordinal);
		if (captured.Count == 0)
			return;

		var references = captured.Values.Select(item => item.Source!).ToArray();
		var resolved = new Dictionary<string, ModelElementSnapshot>(StringComparer.Ordinal);
		await foreach (var snapshot in provider.ResolveAsync(manifest.Model, references, cancellationToken)
			.WithCancellation(cancellationToken).ConfigureAwait(false))
		{
			snapshot.Reference.Validate();
			if (!resolved.TryAdd(snapshot.Reference.Key, snapshot))
			{
				changes.Add(new DiagramModelChange
				{
					Kind = DiagramModelChangeKind.Unresolved,
					Reference = snapshot.Reference,
					Summary = $"Model reference '{snapshot.Reference.Key}' resolved ambiguously."
				});
			}
		}

		foreach (var pair in captured.OrderBy(item => item.Key, StringComparer.Ordinal))
		{
			var element = pair.Value;
			if (!resolved.TryGetValue(pair.Key, out var current))
			{
				changes.Add(new DiagramModelChange
				{
					Kind = DiagramModelChangeKind.Removed,
					Reference = element.Source,
					DiagramElementId = element.Id,
					Summary = $"Referenced model element '{pair.Key}' no longer resolves."
				});
				continue;
			}

			var capturedHash = GetCapturedHash(element);
			var currentHash = current.GetSemanticHash();
			if (!string.Equals(capturedHash, currentHash, StringComparison.Ordinal))
			{
				changes.Add(new DiagramModelChange
				{
					Kind = DiagramModelChangeKind.Changed,
					Reference = element.Source,
					DiagramElementId = element.Id,
					Summary = $"Referenced model element '{pair.Key}' changed."
				});
			}

			var expectedRelations = element.SourceRelationships.Select(item => item.Key).ToHashSet(StringComparer.Ordinal);
			var currentRelations = current.Relationships.Select(item => item.Key).ToHashSet(StringComparer.Ordinal);
			if (!expectedRelations.SetEquals(currentRelations))
			{
				changes.Add(new DiagramModelChange
				{
					Kind = DiagramModelChangeKind.RelationshipChanged,
					Reference = element.Source,
					DiagramElementId = element.Id,
					Summary = $"Relationships for model element '{pair.Key}' changed."
				});
			}
		}
	}

	private static async Task ReconcileProjectionAsync(
		DiagramManifest manifest,
		IDiagramModelProvider provider,
		List<DiagramModelChange> changes,
		CancellationToken cancellationToken)
	{
		foreach (var rule in manifest.Projection.SelectionRules)
		{
			var expected = manifest.Projection.Elements
				.Where(item => item.Source is not null && item.SelectedBy.Contains(rule.Id, StringComparer.Ordinal))
				.ToDictionary(item => item.Source!.Key, item => item.Source!, StringComparer.Ordinal);
			var current = new Dictionary<string, ModelElementSnapshot>(StringComparer.Ordinal);
			await foreach (var snapshot in provider.QueryAsync(manifest.Model, rule, cancellationToken)
				.WithCancellation(cancellationToken).ConfigureAwait(false))
			{
				if (!current.TryAdd(snapshot.Reference.Key, snapshot))
				{
					changes.Add(new DiagramModelChange
					{
						Kind = DiagramModelChangeKind.Unresolved,
						Reference = snapshot.Reference,
						SelectionRuleId = rule.Id,
						Summary = $"Selection rule '{rule.Id}' returned model reference '{snapshot.Reference.Key}' more than once."
					});
				}
			}

			foreach (var added in current.Keys.Except(expected.Keys, StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal))
			{
				changes.Add(new DiagramModelChange
				{
					Kind = DiagramModelChangeKind.NewlyInScope,
					Reference = current[added].Reference,
					SelectionRuleId = rule.Id,
					Summary = $"Model element '{added}' is newly in scope for selection rule '{rule.Id}'."
				});
			}

			foreach (var removed in expected.Keys.Except(current.Keys, StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal))
			{
				changes.Add(new DiagramModelChange
				{
					Kind = DiagramModelChangeKind.NoLongerInScope,
					Reference = expected[removed],
					SelectionRuleId = rule.Id,
					Summary = $"Model element '{removed}' is no longer in scope for selection rule '{rule.Id}'."
				});
			}
		}
	}

	private static string GetCapturedHash(DiagramElement element)
	{
		if (!string.IsNullOrWhiteSpace(element.SourceSemanticHash))
			return element.SourceSemanticHash;

		return new ModelElementSnapshot
		{
			Reference = element.Source!,
			Kind = element.Source!.Kind ?? element.Kind,
			DisplayName = element.DisplayName,
			RelevantFacts = element.RelevantFacts,
			Relationships = element.SourceRelationships
		}.GetSemanticHash();
	}
}
