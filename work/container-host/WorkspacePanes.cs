namespace AsiairContainer;

public enum WorkspacePane { Asiair, Stellavita, Other }
public sealed record PanePlacement(WorkspacePane Pane, int Row, int Column, int ColumnSpan);
public static class WorkspacePanes
{
    public static string Label(WorkspacePane pane) => pane switch { WorkspacePane.Asiair => "ASIAIR", WorkspacePane.Stellavita => "Stellavita", _ => "Other" };
    public static string? Package(WorkspacePane pane) => pane switch { WorkspacePane.Asiair => RuntimeController.AsiairPackage, WorkspacePane.Stellavita => RuntimeController.StellavitaPackage, _ => null };
    public static IReadOnlyList<PanePlacement> Arrange(IEnumerable<WorkspacePane> panes)
    {
        var items = panes.Distinct().Order().ToArray();
        return items.Select((p, i) => new PanePlacement(p, items.Length == 1 ? 0 : i / 2, items.Length == 1 ? 0 : i % 2, items.Length > 1 && items.Length % 2 == 1 && i == items.Length - 1 ? 2 : 1)).ToArray();
    }
}
