namespace MetroAPI.Models.Responses;

/// <summary>
/// Response returned by GetOrderLabels.
/// Contains one or more shipping labels as Base64-encoded PDFs.
/// </summary>
public class GetOrderLabelsResponse
{
    /// <summary>The Metro PRO / tracking number this label set belongs to.</summary>
    public string TrackingNumber { get; set; } = string.Empty;

    /// <summary>
    /// All labels combined into a single Base64-encoded PDF.
    /// Use this for printing all pieces in one print job.
    /// </summary>
    public string CombinedLabelsPdfBase64 { get; set; } = string.Empty;

    /// <summary>
    /// Individual label per piece, keyed by piece barcode.
    /// Use these when printing single pieces (e.g. reprints after damage).
    /// </summary>
    public List<PieceLabelDetail> PieceLabels { get; set; } = new();

    /// <summary>Label format that was generated (e.g. "PDF", "ZPL").</summary>
    public string LabelFormat { get; set; } = "PDF";

    /// <summary>Label size used (e.g. "4x6", "8.5x11").</summary>
    public string LabelSize { get; set; } = "4x6";

    /// <summary>Timestamp when the labels were generated (UTC).</summary>
    public DateTime GeneratedAtUtc { get; set; }
}

public class PieceLabelDetail
{
    /// <summary>Piece sequence number (matches CreateOrder response).</summary>
    public int SequenceNumber { get; set; }

    /// <summary>Barcode value for this piece.</summary>
    public string PieceBarcode { get; set; } = string.Empty;

    /// <summary>Base64-encoded PDF containing only this piece's label.</summary>
    public string LabelPdfBase64 { get; set; } = string.Empty;
}
