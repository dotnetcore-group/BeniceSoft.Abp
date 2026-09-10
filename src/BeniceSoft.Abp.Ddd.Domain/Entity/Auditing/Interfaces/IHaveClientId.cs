namespace BeniceSoft.Abp.Ddd.Domain.Entity;

/// <summary>
/// OIDC ClientId
/// </summary>
public interface IHaveClientId
{
    /// <summary>
    /// ClientId (wms-web / oms-web)
    /// </summary>
    string ClientId { get; }
}
