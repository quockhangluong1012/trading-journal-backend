namespace TradingJournal.Tests.Setups.Features.V1;

public class TradingSetupDiagramTests
{
    private static TradingSetupNodeDto Node(string id, string kind = "step", string title = "Title")
        => new(id, kind, 0, 0, title, null);

    private static TradingSetupEdgeDto Edge(string id, string source, string target)
        => new(id, source, target, null);

    [Fact]
    public void Empty_Nodes_Produces_Error()
    {
        IReadOnlyList<(string Property, string Message)> issues =
            TradingSetupDiagram.Validate([], []);

        Assert.Contains(issues, i => i.Property == "Nodes");
    }

    [Fact]
    public void Node_Missing_Id_Produces_Error()
    {
        IReadOnlyList<(string Property, string Message)> issues =
            TradingSetupDiagram.Validate([Node("", "start")], []);

        Assert.Contains(issues, i => i.Property == "Nodes" && i.Message.Contains("id"));
    }

    [Fact]
    public void Node_Missing_Title_Produces_Error()
    {
        IReadOnlyList<(string Property, string Message)> issues =
            TradingSetupDiagram.Validate([Node("n1", "start", "  ")], []);

        Assert.Contains(issues, i => i.Property == "Nodes" && i.Message.Contains("title"));
    }

    [Fact]
    public void Duplicate_Node_Ids_Are_Detected_Case_Insensitively()
    {
        IReadOnlyList<(string Property, string Message)> issues =
            TradingSetupDiagram.Validate([Node("Node1"), Node("node1")], []);

        Assert.Contains(issues, i => i.Property == "Nodes" && i.Message.Contains("unique"));
    }

    [Fact]
    public void Edge_Referencing_Missing_Node_Produces_Error()
    {
        IReadOnlyList<(string Property, string Message)> issues =
            TradingSetupDiagram.Validate([Node("n1", "start")], [Edge("e1", "n1", "ghost")]);

        Assert.Contains(issues, i => i.Property == "Edges" && i.Message.Contains("existing nodes"));
    }

    [Fact]
    public void Self_Referential_Edge_Produces_Error()
    {
        IReadOnlyList<(string Property, string Message)> issues =
            TradingSetupDiagram.Validate([Node("n1", "start")], [Edge("e1", "n1", "n1")]);

        Assert.Contains(issues, i => i.Property == "Edges" && i.Message.Contains("same node"));
    }

    [Fact]
    public void Duplicate_Edges_Produce_Error()
    {
        IReadOnlyList<(string Property, string Message)> issues =
            TradingSetupDiagram.Validate(
                [Node("a", "start"), Node("b", "end")],
                [Edge("e1", "a", "b"), Edge("e2", "a", "b")]);

        Assert.Contains(issues, i => i.Property == "Edges" && i.Message.Contains("Duplicate"));
    }

    [Fact]
    public void Valid_Simple_Diagram_Has_No_Errors()
    {
        IReadOnlyList<(string Property, string Message)> issues =
            TradingSetupDiagram.Validate(
                [Node("start", "start", "Start"), Node("step", "step", "Do work"), Node("end", "end", "End")],
                [Edge("e1", "start", "step"), Edge("e2", "step", "end")]);

        Assert.Empty(issues);
    }

    [Fact]
    public void Unsupported_Node_Kind_Normalizes_To_Step_And_Is_Valid()
    {
        // Unknown kinds normalize to "step", so they remain valid.
        IReadOnlyList<(string Property, string Message)> issues =
            TradingSetupDiagram.Validate([Node("n1", "weird-kind", "Title")], []);

        Assert.DoesNotContain(issues, i => i.Message.Contains("Unsupported"));
    }
}
