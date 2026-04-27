namespace TradingJournal.Modules.Setups.Features.V1.TradingSetups;

public sealed record TradingSetupNodeDto(string Id, string Kind, double X, double Y, string Title, string? Notes);

public sealed record TradingSetupEdgeDto(string Id, string Source, string Target, string? Label);

public sealed record TradingSetupViewModel(int Id, string Name, string? Description, int StepCount, DateTime CreatedAt, DateTime LastUpdatedAt);

public sealed record TradingSetupDetailViewModel(
    int Id,
    string Name,
    string? Description,
    int StepCount,
    DateTime CreatedAt,
    DateTime LastUpdatedAt,
    IReadOnlyCollection<TradingSetupNodeDto> Nodes,
    IReadOnlyCollection<TradingSetupEdgeDto> Edges);

internal static class TradingSetupDiagram
{
    private static readonly HashSet<string> AllowedNodeKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "start",
        "step",
        "decision",
        "end",
    };

    public static IReadOnlyList<(string Property, string Message)> Validate(
        IReadOnlyCollection<TradingSetupNodeDto> nodes,
        IReadOnlyCollection<TradingSetupEdgeDto> edges)
    {
        List<(string Property, string Message)> issues = [];

        if (nodes.Count == 0)
        {
            issues.Add(("Nodes", "At least one setup node is required."));
            return issues;
        }

        List<string> nodeIds = [];

        foreach (TradingSetupNodeDto node in nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Id))
            {
                issues.Add(("Nodes", "Every setup node must include an id."));
                continue;
            }

            nodeIds.Add(node.Id.Trim());

            if (string.IsNullOrWhiteSpace(node.Title))
            {
                issues.Add(("Nodes", "Every setup node must include a title."));
            }

            if (!AllowedNodeKinds.Contains(NormalizeNodeKind(node.Kind)))
            {
                issues.Add(("Nodes", $"Unsupported node kind '{node.Kind}'."));
            }
        }

        if (nodeIds.Count != nodeIds.Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            issues.Add(("Nodes", "Setup node ids must be unique."));
        }

        HashSet<string> knownNodeIds = new(nodeIds, StringComparer.OrdinalIgnoreCase);
        HashSet<string> seenConnections = new(StringComparer.OrdinalIgnoreCase);

        foreach (TradingSetupEdgeDto edge in edges)
        {
            if (string.IsNullOrWhiteSpace(edge.Source) || string.IsNullOrWhiteSpace(edge.Target))
            {
                issues.Add(("Edges", "Every setup connection must include both source and target nodes."));
                continue;
            }

            if (!knownNodeIds.Contains(edge.Source.Trim()) || !knownNodeIds.Contains(edge.Target.Trim()))
            {
                issues.Add(("Edges", "Setup connections must reference existing nodes."));
            }

            if (string.Equals(edge.Source.Trim(), edge.Target.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(("Edges", "Setup connections cannot point to the same node."));
            }

            string connectionKey = $"{edge.Source.Trim()}->{edge.Target.Trim()}";
            if (!seenConnections.Add(connectionKey))
            {
                issues.Add(("Edges", "Duplicate setup connections are not allowed."));
            }
        }

        return issues;
    }

    public static List<SetupStep> BuildSteps(IReadOnlyCollection<TradingSetupNodeDto> nodes)
    {
        return nodes.Select((node, index) => new SetupStep
        {
            Id = 0,
            StepNumber = index + 1,
            Label = node.Title.Trim(),
            Description = NormalizeOptionalText(node.Notes),
            NodeType = NormalizeNodeKind(node.Kind),
            PositionX = node.X,
            PositionY = node.Y,
            Color = GetNodeColor(node.Kind),
        }).ToList();
    }

    public static IReadOnlyDictionary<string, SetupStep> MapStepsByNodeId(
        IReadOnlyCollection<TradingSetupNodeDto> nodes,
        IReadOnlyList<SetupStep> steps)
    {
        return nodes
            .Zip(steps, (node, step) => new KeyValuePair<string, SetupStep>(node.Id.Trim(), step))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
    }

    public static List<SetupConnection> BuildConnections(
        IReadOnlyCollection<TradingSetupEdgeDto> edges,
        IReadOnlyDictionary<string, SetupStep> stepsByNodeId)
    {
        return edges.Select(edge => new SetupConnection
        {
            Id = 0,
            SourceStep = stepsByNodeId[edge.Source.Trim()],
            TargetStep = stepsByNodeId[edge.Target.Trim()],
            Label = NormalizeOptionalText(edge.Label),
            IsAnimated = false,
            Color = null,
        }).ToList();
    }

    public static TradingSetupNodeDto ToNodeDto(SetupStep step)
    {
        return new(
            $"setup-step-{step.Id}",
            NormalizeNodeKind(step.NodeType),
            step.PositionX,
            step.PositionY,
            step.Label,
            step.Description);
    }

    public static TradingSetupEdgeDto ToEdgeDto(
        SetupConnection connection,
        IReadOnlyDictionary<int, string> nodeIdByStepId)
    {
        return new(
            $"setup-connection-{connection.Id}",
            nodeIdByStepId[connection.SourceStepId],
            nodeIdByStepId[connection.TargetStepId],
            connection.Label);
    }

    public static int CountActionableSteps(IEnumerable<SetupStep> steps)
    {
        return steps.Count(step => !IsTerminalNodeKind(step.NodeType));
    }

    public static string NormalizeNodeKind(string? kind)
    {
        return kind?.Trim().ToLowerInvariant() switch
        {
            "start" => "start",
            "decision" => "decision",
            "end" => "end",
            _ => "step",
        };
    }

    public static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool IsTerminalNodeKind(string? kind)
    {
        string normalizedKind = NormalizeNodeKind(kind);
        return normalizedKind is "start" or "end";
    }

    private static string GetNodeColor(string? kind)
    {
        return NormalizeNodeKind(kind) switch
        {
            "start" => "#059669",
            "decision" => "#d97706",
            "end" => "#7c3aed",
            _ => "#2563eb",
        };
    }
}
