using System.Reflection;
using OpenAI;

var type = typeof(OpenAIClientOptions);
Console.WriteLine($"Type: {type.FullName}");
foreach (var prop in type.GetProperties())
{
    Console.WriteLine($"Property: {prop.Name}");
}
foreach (var prop in type.BaseType.GetProperties())
{
    Console.WriteLine($"Base Property: {prop.Name}");
}
