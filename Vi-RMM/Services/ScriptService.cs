namespace ViRMM.Services;

public class RmmScript
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "Maintenance"; // Maintenance, Network, Diagnostics, Security
    public string Shell { get; set; } = "PowerShell";
    public string ScriptBody { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ScriptService
{
    private readonly List<RmmScript> _scripts = [];

    public IReadOnlyList<RmmScript> All => _scripts;

    public void Add(RmmScript script) => _scripts.Add(script);
    public void Delete(Guid id) => _scripts.RemoveAll(s => s.Id == id);
}
