using MetroAPI.Models.Requests;
using MetroAPI.Models.Responses;

namespace MetroAPI.Services;

/// <summary>
/// Abstraction over the Metropolitan carrier API.
/// Separating the interface lets unit tests inject a mock without hitting
/// the real carrier endpoint.
/// </summary>
public interface IMetroService
{
    /// <summary>
    /// Submits a new shipment order to Metro and returns the tracking number,
    /// per-piece barcodes, and all label data fields SparsWeb needs to store.
    /// Corresponds to the Metro "CreateOrder" endpoint.
    /// </summary>
    Task<CreateOrderResponse> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct = default);

    /// <summary>
    /// Retrieves shipping labels (Base64-encoded PDF) for an existing order
    /// using the tracking number returned by CreateOrder.
    /// Corresponds to the Metro "GetOrderLabels" endpoint.
    /// </summary>
    Task<GetOrderLabelsResponse> GetOrderLabelsAsync(GetOrderLabelsRequest request, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the Bill of Lading (Base64-encoded PDF) for an existing order
    /// using the tracking number returned by CreateOrder.
    /// Corresponds to the Metro "GetOrderBOL" endpoint.
    /// </summary>
    Task<GetOrderBOLResponse> GetOrderBOLAsync(GetOrderBOLRequest request, CancellationToken ct = default);
}
