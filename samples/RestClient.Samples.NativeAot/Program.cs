using Mafesoft.RestClient;
using System.Text.Json;
using System.Text.Json.Serialization;

// Native AOT and trimming: the JSON metadata comes from a source generated JsonSerializerContext, passed to Json(...).
// Publish it with: dotnet publish -c Release -r <runtime identifier>

RestBuilder api = Rest.Build()
    .Url("https://jsonplaceholder.typicode.com")
    .Header("User-Agent", "Mafesoft.RestClient.Samples.NativeAot")
    .Json(AppJsonContext.Default)
    .Retry(2);

RestResult<Todo> todo = await api.Command("todos").Command(1).GetAsync<Todo>();
Console.WriteLine(todo.IsSuccessStatusCode
    ? $"GET /todos/1 -> {todo.Content!.Title} (completed: {todo.Content.Completed})"
    : $"GET /todos/1 -> {(int)todo.StatusCode} {todo.Exception?.Message}");

RestResult<List<Todo>> todos = await api.Command("todos").Parameter("userId", 1).GetAsync<List<Todo>>();
Console.WriteLine($"GET /todos?userId=1 -> {todos.Content?.Count(t => t.Completed)} of {todos.Content?.Count} completed");

RestResult<Todo> created = await api.Command("todos").Payload(new Todo { UserId = 1, Title = "Publish with Native AOT" }).PostAsync<Todo>();
Console.WriteLine($"POST /todos -> {(int)created.StatusCode}: id {created.Content?.Id}");

return todo.IsSuccessStatusCode ? 0 : 1;

public sealed class Todo
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string? Title { get; set; }

    public bool Completed { get; set; }
}

// Every type sent or received as JSON must be listed here
[JsonSerializable(typeof(Todo))]
[JsonSerializable(typeof(List<Todo>))]
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
internal partial class AppJsonContext : JsonSerializerContext
{
}
