using System.Security.Cryptography;
using System.Text;

namespace RaiDiagram;

internal static class PlantUmlRoundTripMetadata
{
	private const string Header = "' @raid-roundtrip ";
	private const int ChunkLength = 120;

	public static string Embed(string source, DiagramManifest manifest)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(source);
		ArgumentNullException.ThrowIfNull(manifest);
		manifest.Validate();

		var body = Normalize(source);
		var hash = Sha256(body);
		var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(RaidJson5.Serialize(manifest)));
		var chunks = Enumerable.Range(0, (payload.Length + ChunkLength - 1) / ChunkLength)
			.Select(index => payload.Substring(
				index * ChunkLength,
				Math.Min(ChunkLength, payload.Length - index * ChunkLength)))
			.ToArray();
		var metadata = new StringBuilder()
			.Append(Header).Append("sha256=").Append(hash).AppendLine();
		for (var index = 0; index < chunks.Length; index++)
			metadata.Append(Header).Append("manifest=")
				.Append(index + 1).Append('/').Append(chunks.Length).Append(' ')
				.Append(chunks[index]).AppendLine();

		var firstLineEnd = body.IndexOf('\n');
		if (firstLineEnd < 0)
			return body + "\n" + metadata;

		var insertion = firstLineEnd + 1;
		const string allowMixing = "allowmixing\n";
		if (body.AsSpan(insertion).StartsWith(allowMixing, StringComparison.Ordinal))
			insertion += allowMixing.Length;
		return body[..insertion] + metadata + body[insertion..];
	}

	public static bool TryRead(string source, out DiagramManifest manifest)
	{
		manifest = null!;
		if (string.IsNullOrWhiteSpace(source))
			return false;

		var normalized = Normalize(source);
		var lines = normalized.Split('\n');
		var bodyLines = new List<string>(lines.Length);
		var chunks = new SortedDictionary<int, string>();
		string? expectedHash = null;
		var expectedChunks = 0;
		foreach (var line in lines)
		{
			if (!line.StartsWith(Header, StringComparison.Ordinal))
			{
				bodyLines.Add(line);
				continue;
			}

			var value = line[Header.Length..];
			if (value.StartsWith("sha256=", StringComparison.Ordinal))
			{
				expectedHash = value[7..].Trim();
				continue;
			}
			if (!value.StartsWith("manifest=", StringComparison.Ordinal))
				continue;
			var separator = value.IndexOf(' ');
			if (separator < 0)
				return false;
			var ordinal = value[9..separator].Split('/');
			if (ordinal.Length != 2
				|| !int.TryParse(ordinal[0], out var index)
				|| !int.TryParse(ordinal[1], out var count)
				|| index < 1 || count < 1 || index > count)
				return false;
			expectedChunks = expectedChunks == 0 ? count : expectedChunks;
			if (expectedChunks != count || !chunks.TryAdd(index, value[(separator + 1)..].Trim()))
				return false;
		}

		if (expectedHash is null || expectedChunks == 0 || chunks.Count != expectedChunks)
			return false;
		var body = string.Join('\n', bodyLines);
		if (!string.Equals(expectedHash, Sha256(body), StringComparison.Ordinal))
			return false;

		try
		{
			var payload = string.Concat(chunks.OrderBy(item => item.Key).Select(item => item.Value));
			var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
			manifest = RaidJson5.Parse(json);
			return true;
		}
		catch (Exception exception) when (exception is FormatException or RaidSchemaException)
		{
			manifest = null!;
			return false;
		}
	}

	private static string Normalize(string value)
		=> value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

	private static string Sha256(string value)
		=> Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
