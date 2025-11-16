using Databento.Client.Models;

namespace Databento.Client.Reference;

/// <summary>
/// Reference data client for querying corporate actions, adjustment factors, and security master data
/// </summary>
public interface IReferenceClient : IAsyncDisposable
{
    /// <summary>
    /// Corporate actions API
    /// </summary>
    ICorporateActionsApi CorporateActions { get; }

    /// <summary>
    /// Adjustment factors API
    /// </summary>
    IAdjustmentFactorsApi AdjustmentFactors { get; }

    /// <summary>
    /// Security master API
    /// </summary>
    ISecurityMasterApi SecurityMaster { get; }
}
