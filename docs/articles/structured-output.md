# Structured output

`GenerateAsync<T>` returns a strongly-typed result. A JSON schema is derived from `T` automatically,
the request is constrained to JSON output, and the reply is deserialized for you.

```csharp
record Recipe(string Title, string[] Ingredients, int Minutes);

Recipe recipe = await gemini.GenerateAsync<Recipe>("A quick pasta recipe");
Console.WriteLine(recipe.Title);
```

## How it works

1. A schema is generated from `T` (property names, types, required-ness).
2. `responseMimeType = application/json` and `responseSchema` are set on the request.
3. The model's JSON reply is deserialized into `T`.

You can still pass options (model, temperature, system instruction); the schema and MIME type are
filled in only if you didn't set them:

```csharp
var person = await gemini.GenerateAsync<Person>(
    "Pick a famous scientist.",
    new GeminiRequestOptions { Model = "gemini-3.5-flash", Temperature = 0.2f });
```

## Tips

- Prefer simple classes/records with `string`, numbers, `bool`, enums, arrays/lists, and nested objects.
- Mark optional members nullable (`string?`) — non-nullable members are marked **required** in the schema.
- Dictionaries aren't modeled (treated as plain objects); use explicit properties.
- If deserialization fails, a `GeminiSerializationException` is thrown.
