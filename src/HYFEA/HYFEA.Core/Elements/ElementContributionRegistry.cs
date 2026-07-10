using HYFEA.Core.Model;

namespace HYFEA.Core.Elements;

/// <summary>
/// Registry for element contribution implementations (P1.v0.1b).
/// Enables polymorphic dispatch and Track A/B dual-implementation (see 00 §三, 02 §5, 03 §2.1).
/// </summary>
public sealed class ElementContributionRegistry
{
    private readonly Dictionary<Type, IElementContribution> _map = new();

    public ElementContributionRegistry()
    {
        // P1: register built-in contributions
        Register(new Spring1DContribution());
        Register(new Truss2DContribution());
        Register(new EulerBeam2DContribution());
    }

    /// <summary>Registers a contribution for its element type. Replaces existing registration if any.</summary>
    public void Register(IElementContribution contribution)
    {
        if (contribution == null)
            throw new ArgumentNullException(nameof(contribution));
        _map[contribution.ElementDefinitionType] = contribution;
    }

    /// <summary>Resolves the contribution for the given element definition.</summary>
    /// <exception cref="NotSupportedException">No registered contribution for the element type.</exception>
    public IElementContribution Resolve(FemElementDefinition element)
    {
        var t = element.GetType();
        if (!_map.TryGetValue(t, out var contrib))
            throw new NotSupportedException($"No contribution registered for element type {t.Name}. Register it via ElementContributionRegistry.Register().");
        return contrib;
    }
}
