namespace Plugin.Maui.LocalStore.Sample;

public sealed class Person
{
    public string? Id { get; set; }
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public string Status { get; set; } = "active";
    public string? City { get; set; }
}
