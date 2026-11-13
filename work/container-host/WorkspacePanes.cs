namespace AsiairContainer;

public enum WorkspacePane { Asiair, Asiair2, Stellavita, Stellavita2, Other }
public sealed record PanePlacement(WorkspacePane Pane, int Row, int Column, int ColumnSpan);
public static class WorkspacePanes
{
    public static string Label(WorkspacePane pane) => pane switch { WorkspacePane.Asiair => "ASIAIR", WorkspacePane.Asiair2 => "ASIAIR 2", WorkspacePane.Stellavita => "Stellavita", WorkspacePane.Stellavita2 => "Stellavita 2", _ => "Other" };
    public static string? Package(WorkspacePane pane) => pane switch { WorkspacePane.Asiair or WorkspacePane.Asiair2 => RuntimeController.AsiairPackage, WorkspacePane.Stellavita or WorkspacePane.Stellavita2 => RuntimeController.StellavitaPackage, _ => null };
    public static bool IsClone(WorkspacePane pane) => pane is WorkspacePane.Asiair2 or WorkspacePane.Stellavita2;
    public static IReadOnlyList<PanePlacement> Arrange(IEnumerable<WorkspacePane> panes)
    {
        var items = panes.Distinct().Order().ToArray();
        return items.Select((p, i) => new PanePlacement(p, items.Length == 1 ? 0 : i / 2, items.Length == 1 ? 0 : i % 2, items.Length > 1 && items.Length % 2 == 1 && i == items.Length - 1 ? 2 : 1)).ToArray();
    }
}
