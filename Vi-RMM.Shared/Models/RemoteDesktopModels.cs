namespace ViRMM.Shared.Models;

public class ScreenFrameDto
{
    public string AgentId { get; set; } = "";
    public string Base64Image { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public long TimestampUtcTicks { get; set; } = DateTime.UtcNow.Ticks;
}

public class RemoteInputDto
{
    public string AgentId { get; set; } = "";
    public string EventType { get; set; } = ""; // "mouse_move", "mouse_click", "mouse_down", "mouse_up", "key_down", "key_up", "key_press"
    public int X { get; set; }
    public int Y { get; set; }
    public string Button { get; set; } = "left"; // "left", "right", "middle"
    public string? Key { get; set; }
    public int KeyCode { get; set; }
    public bool Ctrl { get; set; }
    public bool Alt { get; set; }
    public bool Shift { get; set; }
}
