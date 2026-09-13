using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using OsLib;
using RaiImage;

namespace RaiDiagram;

/// <summary>JSON5-compatible parsing and deterministic structural persistence for .raid manifests.</summary>
public static class RaidJson5
{
	private static readonly JsonSerializer Serializer = JsonSerializer.Create(CreateSettings());

	public static DiagramManifest Parse(string json5)
	{
		if (string.IsNullOrWhiteSpace(json5))
			throw new RaidSchemaException("A .raid manifest cannot be empty.");

		try
		{
			var stringProtection = $"__RAIDIAGRAM_STRING_{Guid.NewGuid():N}__";
			var parserWarnings = new List<string>();
			var parsedJson5 = Json5Core.Json5.Parse(
				ProtectStringLiterals(json5, stringProtection),
				parserWarnings);
			if (parserWarnings.Count > 0)
				throw new RaidSchemaException(
					"The .raid JSON5 parser reported: " + string.Join("; ", parserWarnings));
			var normalizedJson = JsonConvert.SerializeObject(parsedJson5, new JsonSerializerSettings
			{
				FloatFormatHandling = FloatFormatHandling.Symbol,
				Culture = System.Globalization.CultureInfo.InvariantCulture
			}).Replace(stringProtection, string.Empty, StringComparison.Ordinal);

			using var textReader = new StringReader(normalizedJson);
			using var jsonReader = new JsonTextReader(textReader)
			{
				DateParseHandling = DateParseHandling.None
			};
			var token = JToken.Load(jsonReader, new JsonLoadSettings
			{
				CommentHandling = CommentHandling.Ignore,
				DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
				LineInfoHandling = LineInfoHandling.Load
			});
			if (token is not JObject root)
				throw new RaidSchemaException("A .raid manifest root must be an object.");

			RejectNonFiniteNumbers(root);
			var manifest = root.ToObject<DiagramManifest>(Serializer)
				?? throw new RaidSchemaException("The .raid manifest did not produce a diagram.");
			manifest.Validate();
			return manifest;
		}
		catch (RaidSchemaException)
		{
			throw;
		}
		catch (Exception exception)
		{
			throw new RaidSchemaException("The .raid manifest is not valid supported JSON5.", exception);
		}
	}

	private static string ProtectStringLiterals(string source, string prefix)
	{
		var protectedSource = new System.Text.StringBuilder(source.Length + prefix.Length);
		for (var index = 0; index < source.Length; index++)
		{
			var character = source[index];
			if (character == '/' && index + 1 < source.Length && source[index + 1] == '/')
			{
				do
				{
					protectedSource.Append(source[index]);
					index++;
				}
				while (index < source.Length && source[index] != '\n');
				if (index < source.Length)
					protectedSource.Append(source[index]);
				continue;
			}

			if (character == '/' && index + 1 < source.Length && source[index + 1] == '*')
			{
				protectedSource.Append(character).Append(source[++index]);
				while (++index < source.Length)
				{
					protectedSource.Append(source[index]);
					if (source[index] == '/' && index > 0 && source[index - 1] == '*')
						break;
				}
				continue;
			}

			if (character is not ('\'' or '"'))
			{
				protectedSource.Append(character);
				continue;
			}

			var quote = character;
			protectedSource.Append(quote).Append(prefix);
			while (++index < source.Length)
			{
				character = source[index];
				protectedSource.Append(character);
				if (character == '\\' && index + 1 < source.Length)
				{
					protectedSource.Append(source[++index]);
					continue;
				}
				if (character == quote)
					break;
			}
		}
		return protectedSource.ToString();
	}

	/// <summary>
	/// Emits indented strict JSON, which is a valid JSON5 subset. Free-standing source
	/// comments are intentionally not treated as durable manifest data.
	/// </summary>
	public static string Serialize(DiagramManifest manifest)
	{
		ArgumentNullException.ThrowIfNull(manifest);
		manifest.Validate();
		return JObject.FromObject(manifest, Serializer).ToString(Formatting.Indented);
	}

	public static DiagramManifest Load(RaidFile file)
	{
		ArgumentNullException.ThrowIfNull(file);
		if (!file.Exists())
			throw new RaiPathNotFoundException($"The .raid manifest does not exist: {file.FullName}", file.FullName);
		return Parse(file.ReadAllText());
	}

	public static void Save(RaidFile file, DiagramManifest manifest)
	{
		ArgumentNullException.ThrowIfNull(file);
		var text = Serialize(manifest);
		file.DeleteAll().Append(text).Save();
	}

	private static JsonSerializerSettings CreateSettings()
	{
		var settings = new JsonSerializerSettings
		{
			ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(),
			MissingMemberHandling = MissingMemberHandling.Error,
			NullValueHandling = NullValueHandling.Ignore,
			ObjectCreationHandling = ObjectCreationHandling.Replace,
			DateParseHandling = DateParseHandling.None
		};
		settings.Converters.Add(new StringEnumConverter());
		return settings;
	}

	private static void RejectNonFiniteNumbers(JToken token)
	{
		var descendants = token is JContainer container
			? container.Descendants()
			: Enumerable.Empty<JToken>();
		foreach (var value in descendants.OfType<JValue>())
		{
			if (value.Type != JTokenType.Float)
				continue;
			if (value.Value is double number && !double.IsFinite(number))
				throw new RaidSchemaException(".raid v1 does not permit NaN or Infinity.");
			if (value.Value is float single && !float.IsFinite(single))
				throw new RaidSchemaException(".raid v1 does not permit NaN or Infinity.");
		}
	}
}

/// <summary>A canonical `.raid` manifest file backed by OsLib's TextFile abstraction.</summary>
public sealed class RaidFile : ItemTreeTextFile
{
	public RaidFile(ItemTreePath itemPath)
		: this(itemPath, string.Empty)
	{
	}

	public RaidFile(ItemTreePath itemPath, string nameExt)
		: this(itemPath, ItemTreeTextFile.NoItemNumber, nameExt)
	{
	}

	public RaidFile(ItemTreePath itemPath, int itemNumber, string nameExt)
		: base(itemPath ?? throw new ArgumentNullException(nameof(itemPath)), itemNumber, nameExt, "raid")
	{
		EnsureExtension();
	}

	public RaidFile(string fullName) : base(fullName)
	{
		EnsureExtension();
	}

	public RaidFile(RaiPath path, string name) : base(path, name, string.Empty, "raid")
	{
		EnsureExtension();
	}

	public RaidFile(
		RaiPath subscriberRoot,
		string itemId,
		PathConventionType convention)
		: base(subscriberRoot, itemId, string.Empty, "raid", convention)
	{
		EnsureExtension();
	}

	public DiagramManifest LoadManifest() => RaidJson5.Load(this);
	public DiagramModel LoadModel() => DiagramModel.FromManifest(LoadManifest());
	public RaidFile SaveManifest(DiagramManifest manifest)
	{
		RaidJson5.Save(this, manifest);
		return this;
	}

	private void EnsureExtension()
	{
		if (string.IsNullOrEmpty(Ext))
			Ext = "raid";
		else if (!string.Equals(Ext, "raid", StringComparison.OrdinalIgnoreCase))
			throw new ArgumentException("RaiDiagram manifests use the .raid extension.", nameof(FullName));
	}
}
